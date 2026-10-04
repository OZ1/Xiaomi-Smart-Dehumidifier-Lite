﻿#pragma warning disable IDE1006 // Нарушение правила именования: Эти слова должны начинаться с прописных символов: siid
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Buffers;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DehumidifierControl;

using static Properties.Resources;

using static Math;
using static Int32;
using static String;
using static Nullable;
using static AttributeTargets;
using static MIoTAccess;
using static Comfort;

using State = DehumidifierState;

/// <summary>Свойства MIoT осушителя xiaomi.derh.lite
/// urn:miot-spec-v2:device:dehumidifier:0000A02D:xiaomi-lite:1
/// https://home.miot-spec.com/spec/xiaomi.derh.lite</summary>
public sealed class DehumidifierState
{
	[MIoT(2, 1, RW          /*      */)] public bool  ? dehumidifier                 { get; init; } // питаніе
	[MIoT(2, 2, R,   0,    9/*      */)] public byte  ? dehumidifier_fault           { get; init; } // неисправность (Faults)
	[MIoT(2, 3, RW,  0,    2/*      */)] public byte  ? dehumidifier_mode            { get; init; } // режимъ: 0 умный, 1 ночной, 2 сушка бѣлья
	[MIoT(2, 5, RW, 40,   70/* %    */)] public byte  ? dehumidifier_target_humidity { get; init; } // цѣлевая влажность
	[MIoT(3, 1, R,   0,  100/* %    */)] public byte  ? environment_relative_umidity { get; init; } // влажность въ комнатѣ
	[MIoT(3, 2, R, -30,  100/* °Ц   */)] public float ? environment_temperature      { get; init; }
	[MIoT(4, 1, RW          /*      */)] public bool  ? alarm                        { get; init; } // звукъ кнопокъ
	[MIoT(5, 1, RW          /*      */)] public bool  ? indicator_light              { get; init; } // подсвѣтка
	[MIoT(5, 2, RW,  0,    2/*      */)] public byte  ? indicator_light_mode         { get; init; } // яркость: 0 выкл., 1 тусклая, 2 яркая
	[MIoT(6, 1, RW          /*      */)] public bool  ? physical_controls_locked     { get; init; } // блокировка кнопокъ
	[MIoT(7, 1, RW          /*      */)] public bool  ? dm_service_dry_after_off     { get; init; } // просушка послѣ выключенія
	[MIoT(7, 2, R,   0, 2400/* сек. */)] public ushort? dm_service_dry_left_time     { get; init; } // осталось просушки
	[MIoT(7, 3, R           /*      */)] public bool  ? dm_service_is_warming_up     { get; init; } // идётъ прогрѣвъ послѣ включенія
	[MIoT(8, 1, RW          /*      */)] public bool  ? delay                        { get; init; } // выключеніе по таймеру
	[MIoT(8, 2, RW,  0,  720/* мин. */)] public uint  ? delay_time                   { get; init; } // длительность таймера
	[MIoT(8, 3, R,   0,  720/* мин. */)] public uint  ? delay_remain_time            { get; init; } // осталось до выключенія
	// 7 dm-service дѣйствіе 1 toggle
	//                       2 loop-mode (Цикл через режимы)
	//                       3 reset-filter;
	//              событіе  1 tank-full

	public static MIoTAttribute MIoT(string property) => typeof(State).GetProperty(property)!.GetCustomAttribute<MIoTAttribute>()!;
}

[AttributeUsage(Property)]
public sealed class MIoTAttribute(byte siid, byte piid, MIoTAccess Access, int Min = MinValue, int Max = MaxValue) : Attribute
{
	public byte siid { get; } = siid;
	public byte piid { get; } = piid;

	public MIoTAccess Access { get; } = Access;
	public int        Min    { get; } = Min;
	public int        Max    { get; } = Max;
}

[Flags]
public enum MIoTAccess : byte
{
	R = 1, W = 2, N = 4,
	RW  = R | W,
	RN  = R | N,
	RWN = R | W | N,
}

public enum Comfort : byte { TooDry, Dry, Ideal, Normal, Humid, TooHumid }

/// <summary>Xiaomi Smart Dehumidifier Lite</summary>
public sealed class Dehumidifier(miIO Client) : IDisposable
{
	/// <summary>Оцѣнка влажности въ комнатѣ по медицинскимъ рекомендаціямъ:
	/// 30…60 % — допустимо по ГОСТ 30494-2011;
	/// 40…60 % — норма (диаграмма Стерлинга, 1986), изъ нея
	/// 40…50 % — лучше всего (тамъ же и совѣтъ EPA, клиники Мэйо — не выше 50 %);
	/// ниже 30 % сохнутъ слизистыя;
	/// EPA совѣтуетъ держать ниже 60 % — съ 60 % уже влажно;
	/// съ 70 % растутъ плѣсень и клещи.
	/// Ступень — сколько началъ ComfortStarts влажность уже достигла.</summary>
	public static Comfort HumidityComfort(byte humidity)
	{
		int i = ComfortStarts.BinarySearch(humidity);
		return (Comfort)(i < 0 ? ~i : i + 1);
	}

	/// <summary>Первое значеніе каждой ступени послѣ TooDry.</summary>
	public static ReadOnlySpan<byte> ComfortStarts => [/*Dry*/30, /*Ideal*/40, /*Normal*/51, /*Humid*/60, /*TooHumid*/70];

	static readonly List<(PropertyInfo Info, MIoTAttribute MIoT)> Fields = [..
		from  Info in typeof(State).GetProperties()
		let   MIoT = Info.GetCustomAttribute<MIoTAttribute>()
		where MIoT is not null
		select (Info, MIoT)];

	/// <summary>Свойства состоянія съ атрибутомъ [MIoT], по имени.</summary>
	static readonly Dictionary<string, MIoTAttribute> MIoT = Fields.ToDictionary(p => p.Info.Name, p => p.MIoT);

	/// <summary>Тѣ же свойства по (siid, piid) — по нимъ разбираемъ отвѣты: устройство возвращаетъ siid и piid каждаго свойства.</summary>
	static readonly Dictionary<(byte siid, byte piid), PropertyInfo> Props = Fields.ToDictionary(p => (p.MIoT.siid, p.MIoT.piid), p => p.Info);

	public static string FaultText(byte code) => ResourceManager.GetString($"Fault{code}", Culture) ?? Format(FaultUnknown, code);
	public static string ComfortText(Comfort comfort) => ResourceManager.GetString($"Comfort{comfort}", Culture)!;

	public async Task<State> GetStateAsync(CancellationToken ct = default)
	{
		State state = new();
		foreach (JsonArray request in ByResponseSize(MIoT.Values.Select(Get)))
		{
			if (await Client.SendAsync("get_properties", request, ct).ConfigureAwait(false) is not JsonArray results) // не возвращаться въ контекстъ вызвавшаго — какъ и въ miIO
				throw new miIOException(Format(BadResponse, "get_properties"));
			foreach (JsonNode? r in results)
				if (r is not null && Key(r) is { } key && Code(r) == 0 && Props.TryGetValue(key, out PropertyInfo? prop))
					prop.SetValue(state, Parse(r["value"], GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType));
		}
		return state;
	}

	public async Task<JsonNode?> GetAsync(byte siid, byte piid)
	{
		JsonArray request = [Get(siid, piid)];
		// запрошено одно свойство — и отвѣтъ ровно одинъ, на него же
		if (await Client.SendAsync("get_properties", request).ConfigureAwait(false) is not (JsonArray and [var result]) || Key(result) != (siid, piid))
			throw new miIOException(Format(BadResponse, "get_properties"));
		if (Code(result) is not 0 and var code)
			throw new miIOException(Format(ReadRejected, $"{siid}.{piid}", ErrorText(code)));
		return result?["value"]?.DeepClone();
	}

	static JsonObject Get(MIoTAttribute mIoT) => Get(mIoT.siid, mIoT.piid);
	static JsonObject Get(byte siid, byte piid) => new()
	{
		[ "did" ] = "", // устройство возвращаетъ did какъ есть; безъ него вставитъ свой номеръ и удлинитъ отвѣтъ
		["siid" ] = siid,
		["piid" ] = piid,
	};

	static JsonObject Set(MIoTAttribute mIoT, JsonNode value) => Set(mIoT.siid, mIoT.piid, value);
	static JsonObject Set(byte siid, byte piid, JsonNode value) => new()
	{
		[ "did" ] = "",
		["siid" ] = siid,
		["piid" ] = piid,
		["value"] = value,
	};

	public Task SetAsync(byte siid, byte piid, JsonNode value) => SetAsync(new JsonArray(Set(siid, piid, value)));
	public Task SetAsync(params IEnumerable<(string Name, JsonNode Value)> values)
	{
		JsonArray request = [];
		foreach ((string name, JsonNode value) in values)
			request.Add(Set(MIoT[name], value));
		return SetAsync(request);
	}

	async Task SetAsync(JsonArray request)
	{
		if (await Client.SendAsync("set_properties", request).ConfigureAwait(false) is not JsonArray results)
			throw new miIOException(Format(BadResponse, "set_properties"));
		List<JsonNode?> failed = [.. results.Where(r => Code(r) != 0)];
		if (failed.Count > 0)
			throw new miIOException(Format(WriteRejected, Join("; ", failed.Select(r => $"{Name(r)} — {ErrorText(Code(r))}"))));
	}

	static JsonObject Action(byte siid, byte aiid) => new()
	{
		[ "did"] = "", // безъ did устройство вставитъ свой номеръ (провѣрено)
		["siid"] = siid,
		["aiid"] = aiid,
	//	["in"  ] = new JsonArray(), // безъ in осушитељ запросъ принимаетъ (провѣрено)
	};

	// дѣйствія службы 7 dm-service; параметровъ не принимаютъ и ничего не возвращаютъ

	/// <summary>toggle (7, 1): переключить питаніе, какъ кнопкой на корпусѣ.</summary>
	public Task ToggleAsync() => ActionAsync(7, 1);

	/// <summary>loop-mode (7, 2): слѣдующій режимъ по кругу — умный → ночной → сушка бѣлья.</summary>
	public Task LoopModeAsync() => ActionAsync(7, 2);

	/// <summary>reset-filter (7, 3): сбросить счётчикъ фильтра.</summary>
	public Task ResetFilterAsync() => ActionAsync(7, 3);

	async Task ActionAsync(byte siid, byte aiid)
	{
		JsonNode? result = await Client.SendAsync("action", Action(siid, aiid)).ConfigureAwait(false);
		if (Code(result) is not 0 and var code)
			throw new miIOException(Format(ActionRejected, siid, aiid, ErrorText(code)));
	}

	/// <summary>Предѣлъ длины отвѣта: на get_properties съ отвѣтомъ длиннѣе ~1024 байтъ
	/// xiaomi.derh.lite молчитъ (провѣрено: 18 свойствъ — отвѣтъ 981 байтъ, приходитъ; 20 — нѣтъ).</summary>
	const int MaxResponseBytes = 1024;

	/// <summary>Обёртка отвѣта {"id":…,"result":[],"exe_time":…} съ запасомъ.</summary>
	const int ResponseEnvelopeBytes = 64;

	/// <summary>Сколько каждое свойство добавляетъ въ отвѣтъ сверхъ своего запроса: ,"code":0,"value":… съ запасомъ на значеніе.</summary>
	const int ResponseExtraBytes = 32;

	/// <summary>Дѣлитъ свойства на запросы такъ, чтобы отвѣтъ на каждый помѣщался въ MaxResponseBytes.
	/// Съ пустымъ did всѣ 16 свойствъ осушителя помѣщаются въ одинъ запросъ.</summary>
	static IEnumerable<JsonArray> ByResponseSize(params IEnumerable<JsonObject> items)
	{
		JsonArray chunk = [];
		int size = ResponseEnvelopeBytes;
		ArrayBufferWriter<byte> buffer = new();
		using Utf8JsonWriter json = new(buffer);
		foreach (JsonObject item in items)
		{
			buffer.ResetWrittenCount();
			json.Reset();
			item.WriteTo(json);
			json.Flush();
			int bytes = buffer.WrittenCount + ResponseExtraBytes;
			if (bytes + size > MaxResponseBytes && chunk.Count > 0)
			{
				yield return chunk;
				chunk = [];
				size = ResponseEnvelopeBytes;
			}
			chunk.Add(item);
			size += bytes;
		}
		if (chunk.Count > 0)
			yield return chunk;
	}

	/// <summary>Коды ошибокъ MIoT въ отвѣтахъ на get/set_properties и action.</summary>
	public static string ErrorText(int code) => code switch
	{
		-4001 => Error4001,
		-4002 => Error4002,
		-4003 => Error4003,
		-4004 => Error4004,
		-4005 => Error4005,
		-4006 => Error4006,
		-4007 => Error4007,
		_ => Format(ErrorCode, code),
	};

	/// <summary>(siid, piid) изъ элемента отвѣта.</summary>
	static (byte siid, byte piid)? Key(JsonNode? r) =>
		r?["siid"] is JsonValue s && s.TryGetValue(out byte siid) &&
		r ["piid"] is JsonValue p && p.TryGetValue(out byte piid) ? (siid, piid) : null;

	/// <summary>Имя свойства для сообщенія объ ошибкѣ.</summary>
	static string Name(JsonNode? r) => Key(r) is { } key ?
		PropertyName(key.siid, key.piid) ?? $"({key.siid}, {key.piid})" : "?";

	/// <summary>Имя свойства въ DehumidifierState по номерамъ MIoT; null — такого тамъ нѣтъ.</summary>
	public static string? PropertyName(byte siid, byte piid) => Props.GetValueOrDefault((siid, piid))?.Name;

	/// <summary>Кодъ ошибки элемента отвѣта; 0 — успѣхъ. Несуществующее свойство xiaomi.derh.lite отдаётъ съ code 0,
	/// а кодъ ошибки кладётъ въ value (провѣрено: 9.9 → -4005, 2.9 → -4001) — такой value тоже считается кодомъ.</summary>
	static int Code(JsonNode? r) =>
		r?["code" ] is JsonValue c && c.TryGetValue(out int code) && code != 0 ? code :
		r?["value"] is JsonValue v && v.TryGetValue(out     code) && code is <= -4000 and >= -4999 ? code : 0;

	// разборъ значеній по форматамъ MIoT; число внѣ предѣловъ тѵпа прижимается къ границѣ

	// каждая вѣтка — object?, иначе C# сведётъ byte?/ushort?/uint?/float? къ общему float?
	static object? Parse(JsonNode? n, Type type) =>
		type == typeof(bool  ) ? (object?)Boolean(n) :
		type == typeof(byte  ) ? (object?)Integer<byte  >(n) :
		type == typeof(ushort) ? (object?)Integer<ushort>(n) :
		type == typeof(uint  ) ? (object?)Integer<uint>  (n) :
		type == typeof(float ) ? (object?)Single(n)
		: throw new NotSupportedException(Format(TypeNotSupported, type.Name));

	static  T? Integer<T>(JsonNode? n) where T : struct, INumber<T> => Double(n) is { } d ? T.CreateSaturating(Round(d)) : null;
	static  float? Single(JsonNode? n) => Double(n) is { } d ? (float)d : null;
	static double? Double(JsonNode? n) => n is JsonValue v && v.TryGetValue<double>(out var d) ? d : null;
	static  bool? Boolean(JsonNode? n) =>
		n is not JsonValue v ? null
		: v.TryGetValue<bool>(out var b) ? b
		: v.TryGetValue<int>(out var i) ? i != 0
		: null;

	public void Dispose() => Client.Dispose();
}

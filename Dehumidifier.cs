﻿#pragma warning disable IDE1006 // Нарушение правила именования: Эти слова должны начинаться с прописных символов: siid
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;

namespace DehumidifierControl;

using static String;
using static Double;
using static Encoding;
using static Nullable;
using static AttributeTargets;
using static MIoTAccess;
using static Comfort;

using State = DehumidifierState;

using uint8 = byte;
using uint16 = ushort;
using uint32 = uint;

[AttributeUsage(Property)]
public sealed class MIoTAttribute(byte siid, byte piid, MIoTAccess Access, object? Min = null, object? Max = null) : Attribute
{
	public byte siid { get; } = siid;
	public byte piid { get; } = piid;

	public MIoTAccess Access { get; } = Access;
	public object? Min { get; } = Min;
	public object? Max { get; } = Max;
}

[Flags]
public enum MIoTAccess
{
	R = 1, W = 2, N = 4,
	RW  = R | W,
	RN  = R | N,
	RWN = R | W | N,
}

/// <summary>Свойства MIoT осушителя xiaomi.derh.lite
/// urn:miot-spec-v2:device:dehumidifier:0000A02D:xiaomi-lite:1
/// https://home.miot-spec.com/spec/xiaomi.derh.lite</summary>
public sealed class DehumidifierState
{
	[MIoT(2, 1, RW          /*       */)] public bool  ? dehumidifier                 { get; init; } // питаніе
	[MIoT(2, 2, R,   0,    9/*       */)] public uint8 ? dehumidifier_fault           { get; init; } // неисправность (Faults)
	[MIoT(2, 3, RW,  0,    2/*       */)] public uint8 ? dehumidifier_mode            { get; init; } // режимъ: 0 умный, 1 ночной, 2 сушка бѣлья
	[MIoT(2, 5, RW, 40,   70/* %     */)] public uint8 ? dehumidifier_target_humidity { get; init; } // цѣлевая влажность
	[MIoT(3, 1, R,   0,  100/* %     */)] public uint8 ? environment_relative_umidity { get; init; } // влажность въ комнатѣ
	[MIoT(3, 2, R, -30,  100/* °Ц    */)] public float ? environment_temperature      { get; init; }
	[MIoT(4, 1, RW          /*       */)] public bool  ? alarm                        { get; init; } // звукъ кнопокъ
	[MIoT(5, 1, RW          /*       */)] public bool  ? indicator_light              { get; init; } // подсвѣтка
	[MIoT(5, 2, RW,  0,    2/*       */)] public uint8 ? indicator_light_mode         { get; init; } // яркость: 0 выкл., 1 тусклая, 2 яркая
	[MIoT(6, 1, RW          /*       */)] public bool  ? physical_controls_locked     { get; init; } // блокировка кнопокъ
	[MIoT(7, 1, RW          /*       */)] public bool  ? dm_service_dry_after_off     { get; init; } // просушка послѣ выключенія
	[MIoT(7, 2, R,   0, 2400/*  сек. */)] public uint16? dm_service_dry_left_time     { get; init; } // осталось просушки
	[MIoT(7, 3, R           /*       */)] public bool  ? dm_service_is_warming_up     { get; init; } // идётъ прогрѣвъ послѣ включенія
	[MIoT(8, 1, RW          /*       */)] public bool  ? delay                        { get; init; } // выключеніе по таймеру
	[MIoT(8, 2, RW,  0,  720/*  мин. */)] public uint32? delay_time                   { get; init; } // длительность таймера
	[MIoT(8, 3, R,   0,  720/*  мин. */)] public uint32? delay_remain_time            { get; init; } // осталось до выключенія
	// 7 dm-service дѣйствіе 1 toggle
	//                       2 loop-mode (Цикл через режимы)
	//                       3 reset-filter;
	//              событіе  1 tank-full

	public static MIoTAttribute MIoT(string property) => typeof(State).GetProperty(property)!.GetCustomAttribute<MIoTAttribute>()!;
}

/// <summary>Насколько влажность въ комнатѣ хороша для здоровья.</summary>
public enum Comfort { TooDry, Dry, Ideal, Normal, Humid, TooHumid }
/// <summary>Xiaomi Smart Dehumidifier Lite</summary>
public sealed class Dehumidifier(miIO Client) : IDisposable
{
	/// <summary>Свойства состоянія съ атрибутомъ [MIoT], по имени.</summary>
	static readonly Dictionary<string, (PropertyInfo Info, MIoTAttribute MIoT)> Props = (
		from  Info in typeof(State).GetProperties()
		let   MIoT = Info.GetCustomAttribute<MIoTAttribute>()
		where MIoT is not null
		select (Info, MIoT)).ToDictionary(p => p.Info.Name);

	/// <summary>Тѣ же свойства по (siid, piid) — по нимъ разбираемъ отвѣты: устройство возвращаетъ siid и piid каждаго свойства.</summary>
	static readonly Dictionary<(byte siid, byte piid), (PropertyInfo Info, MIoTAttribute MIoT)> ById = Props.Values.ToDictionary(p => (p.MIoT.siid, p.MIoT.piid));

	static readonly string[] Faults =
	[
		"отсутствуетъ",
		"бакъ полонъ",
		"ошибка датчика температуры и влажности",
		"ошибка датчика медной трубки",
		"сбой связи",
		"пора почистить фильтръ",
		"размораживаніе",
		"заклинило двигатель",
		"защита отъ перегрузки",
		"мало хладагента",
	];

	public static string FaultText(byte code) => code < Faults.Length ? Faults[code] : $"неизвѣстная ({code})";

	/// <summary>Оцѣнка влажности въ комнатѣ по медицинскимъ рекомендаціямъ:
	/// 30…60 % — допустимо по ГОСТ 30494-2011;
	/// 40…60 % — норма (диаграмма Стерлинга, 1986), изъ нея
	/// 40…50 % — лучше всего (тамъ же и совѣтъ EPA, клиники Мэйо — не выше 50 %);
	/// ниже 30 % сохнутъ слизистыя,
	/// выше 60…70 % растутъ плѣсень и клещи.</summary>
	public static Comfort HumidityComfort(byte humidity) => humidity switch
	{
		<  30 => TooDry,
		<  40 =>    Dry,
		<= 50 =>  Ideal,
		<= 60 => Normal,
		<= 70 =>  Humid,
		_  =>  TooHumid,
	};

	public async Task<State> GetStateAsync(CancellationToken ct = default)
	{
		State state = new();
		IEnumerable<JsonObject> items = Props.Values.Select(p => new JsonObject
		{
			["did" ] = "", // устройство возвращаетъ did какъ есть; безъ него вставитъ свой номеръ и удлинитъ отвѣтъ
			["siid"] = p.MIoT.siid,
			["piid"] = p.MIoT.piid,
		});
		foreach (JsonArray request in ByResponseSize(items))
		{
			if (await Client.SendAsync("get_properties", request, ct) is not JsonArray results)
				throw new miIOException("Странный отвѣтъ на get_properties.");
			foreach (JsonNode? r in results)
				if (r is not null && Key(r) is { } key && Code(r) == 0 && ById.TryGetValue(key, out (PropertyInfo Info, MIoTAttribute MIoT) prop))
					prop.Info.SetValue(state, Parse(r["value"], GetUnderlyingType(prop.Info.PropertyType) ?? prop.Info.PropertyType));
		}
		return state;
	}

	/// <summary>Записать свойства; имя — <c>nameof(DehumidifierState.Power)</c> и т. п.</summary>
	public async Task SetAsync(params (string Name, JsonNode Value)[] values)
	{
		JsonArray request = [];
		foreach ((string? name, JsonNode? value) in values)
		{
			if (!Props.TryGetValue(name, out (PropertyInfo Info, MIoTAttribute MIoT) prop))
				throw new ArgumentException($"У свойства {name} нѣтъ атрибута [MIoT].", nameof(values));
			request.Add(new JsonObject
			{
				[ "did" ] = "",
				["siid" ] = prop.MIoT.siid,
				["piid" ] = prop.MIoT.piid,
				["value"] = value,
			});
		}
		if (await Client.SendAsync("set_properties", request) is not JsonArray results)
			throw new miIOException("Странный отвѣтъ на set_properties.");
		List<JsonNode?> failed = [.. results.Where(r => Code(r) != 0)];
		if (failed.Count > 0)
			throw new miIOException("Устройство отклонило запись: " + Join("; ", failed.Select(r => $"{Name(r)} — {ErrorText(Code(r))}")));
	}

	// дѣйствія службы 7 dm-service; параметровъ не принимаютъ и ничего не возвращаютъ

	/// <summary>toggle (7, 1): переключить питаніе, какъ кнопкой на корпусѣ.</summary>
	public Task ToggleAsync() => ActionAsync(7, 1);

	/// <summary>loop-mode (7, 2): слѣдующій режимъ по кругу — умный → ночной → сушка бѣлья.</summary>
	public Task LoopModeAsync() => ActionAsync(7, 2);

	/// <summary>reset-filter (7, 3): сбросить счётчикъ фильтра.</summary>
	public Task ResetFilterAsync() => ActionAsync(7, 3);

	async Task ActionAsync(int siid, int aiid)
	{
		JsonNode? result = await Client.SendAsync("action", new JsonObject
		{
			["did" ] = $"call-{siid}-{aiid}",
			["siid"] = siid,
			["aiid"] = aiid,
			["in"  ] = new JsonArray(),
		});
		if (Code(result) != 0)
			throw new miIOException($"Устройство отклонило дѣйствіе ({siid}, {aiid}): {ErrorText(Code(result))}");
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
	static IEnumerable<JsonArray> ByResponseSize(IEnumerable<JsonObject> items)
	{
		JsonArray chunk = [];
		int size = ResponseEnvelopeBytes;
		foreach (JsonObject item in items)
		{
			int bytes = UTF8.GetByteCount(item.ToJsonString()) + ResponseExtraBytes;
			if (chunk.Count > 0 && size + bytes > MaxResponseBytes)
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
		-4001 => "свойство нельзя прочесть (-4001)",
		-4002 => "свойство сейчасъ нельзя записать (-4002); цѣлевую влажность, напримѣръ, нельзя мѣнять въ режимѣ сушки бѣлья",
		-4003 => "нѣтъ такого свойства, дѣйствія или событія (-4003)",
		-4004 => "внутренняя ошибка устройства (-4004)",
		-4005 => "недопустимое значеніе (-4005)",
		-4006 => "недопустимые параметры дѣйствія (-4006)",
		-4007 => "невѣрный did (-4007)",
		_     => $"кодъ {code}",
	};

	/// <summary>(siid, piid) изъ элемента отвѣта.</summary>
	static (byte siid, byte piid)? Key(JsonNode? r) =>
		r?["siid"] is JsonValue s && s.TryGetValue(out byte siid) &&
		r ["piid"] is JsonValue p && p.TryGetValue(out byte piid) ? (siid, piid) : null;

	/// <summary>Имя свойства для сообщенія объ ошибкѣ.</summary>
	static string Name(JsonNode? r) =>
		Key(r) is { } key ? ById.TryGetValue(key, out var prop) ? prop.Info.Name : $"({key.siid}, {key.piid})" : "?";

	static int Code(JsonNode? r) => r?["code"] is JsonValue v && v.TryGetValue<int>(out var c) ? c : 0;

	// разборъ значеній по форматамъ MIoT; число внѣ предѣловъ тѵпа прижимается къ границѣ

	// каждая вѣтка — object?, иначе C# сведётъ byte?/ushort?/uint?/float? къ общему float?
	static object? Parse(JsonNode? n, Type type) =>
		type == typeof(bool  ) ? (object?)Bool(n) :
		type == typeof(uint8 ) ? (object?)Integer<uint8 >(n) :
		type == typeof(uint16) ? (object?)Integer<uint16>(n) :
		type == typeof(uint32) ? (object?)Integer<uint32>(n) :
		type == typeof(float ) ? (object?)Float(n)
		: throw new NotSupportedException($"Тѵпъ {type.Name} не поддерживается.");

	static double? Number(JsonNode? n) => n is JsonValue v && v.TryGetValue<double>(out var d) ? d : null;

	static T? Integer<T>(JsonNode? n) where T : struct, INumber<T> => Number(n) is { } d ? T.CreateSaturating(Round(d)) : null;

	static float? Float(JsonNode? n) => Number(n) is { } d ? (float)d : null;

	static bool? Bool(JsonNode? n) =>
		n is not JsonValue v ? null
		: v.TryGetValue<bool>(out var b) ? b
		: v.TryGetValue<int>(out var i) ? i != 0
		: null;

	public void Dispose() => Client.Dispose();
}

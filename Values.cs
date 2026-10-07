using System.Net;
using System.Net.Sockets;

namespace DehumidifierControl;

using static Properties.Resources;

using static Math;
using static Byte;
using static Char;
using static UInt16;
using static String;
using static Convert;
using static IPAddress;
using static AddressFamily;

/// <summary>Разборъ текста въ значенія и показъ значеній текстомъ.</summary>
static class Values
{
	/// <summary>Адресъ осушителя — полный IPv4 (съ тремя точками) или IPv6; иначе null.
	/// «2.5» .NET тоже принялъ бы за 2.0.0.5, а это свойство MIoT для командной строки.</summary>
	public static IPAddress? Address(string? text) =>
		TryParse(text, out IPAddress? ip) && (ip.AddressFamily != InterNetwork || text.Count(c => c == '.') == 3) ? ip : null;

	/// <summary>Токенъ — 32 шестнадцатеричныхъ знака (16 байтъ); иначе null.</summary>
	public static byte[]? TokenOf(string? text) => text is { Length: 32 } && text.All(IsAsciiHexDigit) ? FromHexString(text) : null;

	/// <summary>Цѣлевая влажность: осушитель принимаетъ 0…100, хотя по спецификаціи 40…70.</summary>
	public static bool HumidityOf(ReadOnlySpan<char> text, out byte humidity) => TryParse(text.TrimEnd('%'), out humidity) && humidity <= 100;

	/// <summary>Минуты таймера: «90» или часы съ минутами «1:30»; больше 65535 — null: осушитель хранитъ delay_time въ 16 битахъ (провѣрено).
	/// Часы — ushort, произведеніе — въ int: не переполняется.</summary>
	public static ushort? Minutes(ReadOnlySpan<char> text)
	{
		int colon = text.IndexOf(':');
		if (colon < 0) return TryParse(text, out ushort time) ? time : null;
		return TryParse(text[(colon + 1)..], out byte minutes) && minutes < 60 &&
		/**/   TryParse(text[..colon],       out ushort hours) && minutes + 60 * hours <= 0xFFFF ? (ushort)(hours * 60 + minutes) : null;
	}

	/// <summary>Длительность словами: «2 ч 05 мин», «15 мин», «3 сут 4 ч».</summary>
	/// Округляется разъ — до самой мелкой показанной единицы, дальше только цѣлое дѣленіе:
	/// иначе на стыкѣ выходило бы «60 мин» вмѣсто «1 ч 00 мин» и «1 ч 59 мин» вмѣсто «2 ч 00 мин».
	public static string Duration(TimeSpan span)
	{
		long minutes = Max((long)Round(span.TotalMinutes), 0);
		if (minutes < 60)      return Format(DurationMinutes, minutes);
		if (minutes < 60 * 24) return Format(DurationHours, minutes / 60, minutes % 60);
		long hours = (minutes + 30) / 60; // въ суткахъ минутъ не видно — до часа
		return Format(DurationDays, hours / 24, hours % 24);
	}

	/// <summary>Остатокъ въ шестидесятыхъ: секунды — «мин:сс», минуты — «ч:мм».</summary>
	public static string Clock(uint value) => $"{value / 60}:{value % 60:00}";

	public static string Temperature(float celsius) => Format(TemperatureFormat, celsius);

	public static string ComfortText(Comfort comfort) => ResourceManager.GetString($"Comfort{comfort}", Culture)!;

	public static string FaultText(byte code) => ResourceManager.GetString($"Fault{code}", Culture) ?? Format(FaultUnknown, code);

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
}

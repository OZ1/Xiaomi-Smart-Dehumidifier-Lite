using System;
using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace DehumidifierControl;

using Properties;

using static Properties.Resources;

using static Byte;
using static String;
using static Console;
using static Convert;
using static Encoding;
using static ConsoleColor;
using static StringComparison;
using static Dehumidifier;
using static DehumidifierState;
using static Comfort;

using State = DehumidifierState;

static partial class CLI
{
	static string[] ModeNames => [ModeSmart, ModeSleep, ModeDry];

	static readonly Dictionary<string, byte> Modes = new(StringComparer.OrdinalIgnoreCase)
	{
		["smart"] = 0, ["умный" ] = 0, ["0"] = 0,
		["sleep"] = 1, ["ночной"] = 1, ["1"] = 1,
		["dry"  ] = 2, ["сушка" ] = 2, ["2"] = 2,
	};

	static string[] LightNames => [LightOff, LightDim, LightBright];

	/// <summary>Ширина столбца подписей въ status — по самой длинной подписи на языкѣ интерфейса.</summary>
	static int LabelWidth => new[] { CliPower, CliMode, CliLight, CliTemperature, CliHumidity, CliTarget, CliFault }.Max(l => l.Length) + 1;

	static readonly Dictionary<string, bool> LightSwitch = new(StringComparer.OrdinalIgnoreCase)
	{
		["on" ] = true,  ["включить" ] = true,
		["off"] = false, ["выключить"] = false,
	};

	static readonly Dictionary<string, byte> LightLevels = new(StringComparer.OrdinalIgnoreCase)
	{
		["dim"   ] = 1, ["тусклая"] = 1,
		["bright"] = 2, ["яркая"  ] = 2,
	};

	/// <summary>Слова-команды: въ справкѣ раскрашиваются иначе, чѣмъ ихъ параметры.</summary>
	static readonly HashSet<string> CommandWords = new(StringComparer.OrdinalIgnoreCase)
	{
		"status", "состояніе", "состояние", "on", "включить", "off", "выключить",
		"humidity", "влажность", "mode", "режимъ", "режим", "light", "подсвѣтка", "подсветка", "help", "справка",
	};

	[SuppressMessage("Interoperability", "SYSLIB1054: Используйте LibraryImportAttribute вместо DllImportAttribute для генерирования кода маршализации P/Invoke во время компиляции")]
	[DllImport("Kernel32", ExactSpelling = true)]
	static extern bool AttachConsole(int processId);

	public static async Task<int> RunAsync(string[] args)
	{
		AttachConsole(-1/*AttachParentProcess*/);
		Encoding original = OutputEncoding;
		string? current = null;
		try
		{
			OutputEncoding = UTF8;
			WriteLine();

			for (int i = 0; i < args.Length; i++)
			{
				current = args[i];
				switch (current.ToLowerInvariant())
				{
				case "help" or "-h" or "--help" or "/?" or "-?" or "справка":
					PrintUsage();
					return 0;

				case "status" or "состояніе" or "состояние": break;
				case "on" or "включить" or "off" or "выключить": break;

				case "humidity" or "влажность":
					var id = MIoT(nameof(State.dehumidifier_target_humidity));
					var min = ToInt32(id.Min);
					var max = ToInt32(id.Max);
					if (i + 1 == args.Length)
						return Wrong(Format(CliNoHumidity, current, min, max));
					current += ' ' + args[++i];
					if (!TryParse(args[i].TrimEnd('%'), out var humidity) || humidity is < 0 or > 100)
						return Wrong(Format(CliBadHumidity, current, min, max));
					break;

				case "mode" or "режимъ" or "режим":
					if (i + 1 == args.Length)
						return Wrong(Format(CliNoMode, current));
					current += ' ' + args[++i];
					if (!Modes.TryGetValue(args[i], out byte mode))
						return Wrong(Format(CliBadMode, current));
					break;

				case "light" or "lights" or "подсвѣтка" or "подсветка":
					uint on = 0;
					uint brightness = 0;
					for(; i + 1 < args.Length; i++)
						if (LightSwitch.TryGetValue(args[i + 1], out bool o)) on++; else
						if (LightLevels.TryGetValue(args[i + 1], out byte b)) brightness++;
						else break;
					if (on + brightness < 1)
						return Wrong(Format(CliBadLight, current));
					break;

				default: return Wrong(Format(CliUnknownCommand, current));
				}

				static int Wrong(string message)
				{
					Print(message, Red, Error);
					Error.WriteLine();
					Error.WriteLine();
					PrintUsage(Error);
					return 2;
				}
			}

			using Dehumidifier device = Connect();
			static Dehumidifier  Connect()
			{
				string ip = Settings.Default.IP;
				string token = Settings.Default.Token;
				if (IsNullOrWhiteSpace(ip))
					throw new ArgumentException(CliNoAddress);
				if (IsNullOrWhiteSpace(token))
					throw new ArgumentException(CliNoToken);
				return new(new(ip, token));
			}

			for (int i = 0; i < args.Length; i++)
			{
				current = args[i];
				switch (current.ToLowerInvariant())
				{
				case "status" or "состояніе" or "состояние":
					var s = await device.GetStateAsync();

					Field(CliPower      , s.dehumidifier switch { true => (CliStateOn, Green), false => (CliStateOff, DarkGray), null => null });
					Field(CliMode       , s.dehumidifier_mode is byte m and < 3 ? (ModeNames[m], Cyan) : null);
					Field(CliLight      , s.indicator_light switch { false => (LightNames[0], DarkGray), true => s.indicator_light_mode is byte l and < 3 ? (LightNames[l], l == 0 ? DarkGray : White) : null, null => null });
					Field(CliTemperature, s.environment_temperature      is { } c ? (Format(Resources.TemperatureFormat, c), White) : null);
					Field(CliHumidity   , s.environment_relative_umidity is { } h ? ($"{h} %, {ComfortText(HumidityComfort(h))}", HumidityComfort(h) switch { Ideal => Green, Normal => DarkGreen, Dry or Humid => Yellow, _ => Red }) : null);
					Field(CliTarget     , s.dehumidifier_target_humidity is { } t ? ($"{t} %", White) : null);
					Field(CliFault      , s.dehumidifier_fault is { } f ? (FaultText(f), f == 0 ? Green : Red) : null);

					static void Field(string label, (string Text, ConsoleColor Color)? value)
					{
						Print(label.PadRight(LabelWidth), Gray);
						PrintLine(value?.Text ?? "—", value?.Color ?? DarkGray);
					}
					break;

				case "on" or "включить":
					await device.SetAsync((nameof(State.dehumidifier), true));
					PrintLine(CliSwitchedOn, Green);
					break;

				case "off" or "выключить":
					await device.SetAsync((nameof(State.dehumidifier), false));
					PrintLine(CliSwitchedOff, Green);
					break;

				case "humidity" or "влажность":
					current += ' ' + args[++i];
					byte humidity = Parse(args[i].TrimEnd('%'));
					await device.SetAsync((nameof(State.dehumidifier_target_humidity), humidity));
					PrintLine(Format(CliTargetSet, humidity), Green);
					break;

				case "mode" or "режимъ" or "режим":
					current += ' ' + args[++i]; // значеніе есть: провѣрено въ первомъ проходѣ
					byte mode = Modes[args[i]];
					await device.SetAsync((nameof(State.dehumidifier_mode), mode));
					PrintLine(Format(CliModeSet, ModeNames[mode]), Green);
					break;

				case "light" or "lights" or "подсвѣтка" or "подсветка":
					int first = i;
					List<string> done = new(2);
					List<(string Name, JsonNode Value)> values = new(2);
					bool on = false, br = false;
					for(; i + 1 < args.Length; i++)
						if (!on && (on = LightSwitch.TryGetValue(args[i + 1], out bool o))) { values.Add((nameof(State.indicator_light ), o)); done.Add(o ? CliLightSwitchOn : CliLightSwitchOff); } else
						if (!br && (br = LightLevels.TryGetValue(args[i + 1], out byte b))) { values.Add((nameof(State.indicator_light_mode), b)); done.Add(Format(CliBrightness, LightNames[b])); }
						else break;
					current = Join(' ', args[first..(i + 1)]);
					await device.SetAsync([.. values]);
					PrintLine(Format(CliLightSet, Join(", ", done)), Green);
					break;
				}
			}
			return 0;
		}
		catch (Exception e) when (e is miIOException or IOException or ArgumentException or SocketException)
		{
			PrintLine(current is null ? Format(CliError, e.Message) : Format(CliErrorIn, current, e.Message), Red, Error);
			return 1;
		}
		finally
		{
			OutputEncoding = original;
		}
	}

	static void PrintUsage(TextWriter? error = null)
	{
		string[] lines = CliUsage.Split('\n');
		for (int i = 0; i < lines.Length; i++)
		{
			string line = lines[i].TrimEnd('\r');
			if (i == 0) PrintLine(line, Yellow, error);
			else if (line.StartsWith(CliExitCodePrefix))
			{
				foreach (string part in ExitCode().Split(line))
				{
					(ConsoleColor bright, ConsoleColor dark)? colors = part switch
					{
						['0', ..] => (Green, DarkGreen),
						['1', ..] => (Red, DarkRed),
						['2', ..] => (Yellow, DarkYellow),
						_  => null,
					};
					if (colors is var (bright, dark))
					{
						Print(part.AsSpan(0, 1), bright, error); // цифра ярче описанія: FOREGROUND_INTENSITY
						Print(part.AsSpan(1), dark, error);
					}
					else Print(part, Gray, error);
				}
				PrintLine(error);
			}
			else if (UsageLine().Match(line) is { Success: true } m)
			{
				string commandLine = m.Groups[1].Value;
				foreach (string word in WhiteSpace().Split(commandLine)) // пробѣлы остаются отдѣльными кусками
					Print(word,
						IsNullOrWhiteSpace(word)   ? Gray :
						word.EndsWith(".exe", OrdinalIgnoreCase) ? Magenta :
						CommandWords.Contains(word)  ? Cyan : DarkCyan,
						error);
				PrintLine(m.Groups[2].Value, Gray, error);
			}
			else PrintLine(line, Gray, error);
		}
	}

	static void Print(ReadOnlySpan<char> text, ConsoleColor color, TextWriter? error = null)
	{
		ForegroundColor = color;
		(error ?? Out).Write(text);
		ResetColor();
	}

	static void PrintLine(ReadOnlySpan<char> text, ConsoleColor color, TextWriter? error = null)
	{
		Print(text, color, error);
		PrintLine(error);
	}

	static void PrintLine(TextWriter? error = null)
	{
		(error ?? Out).WriteLine();
	}

	/// <summary>Строка справки съ примѣромъ команды: отступъ въ два пробѣла, команда, затѣмъ (необязательно) описаніе послѣ двухъ и болѣе пробѣловъ.</summary>
	[GeneratedRegex(@"^( \S.*?)(\s{2,}\S.*)?$")]
	private static partial Regex UsageLine();

	/// <summary>Одинъ кодъ выхода въ справкѣ: цифра, тире и описаніе до запятой или точки.</summary>
	[GeneratedRegex(@"(\d — [^,.]+)")]
	private static partial Regex ExitCode();

	[GeneratedRegex(@"(\s+)")]
	private static partial Regex WhiteSpace();
}

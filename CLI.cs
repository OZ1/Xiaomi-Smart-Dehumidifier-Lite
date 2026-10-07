using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace DehumidifierControl;

using Properties;

using static Properties.Resources;

using static Byte;
using static String;
using static Console;
using static TimeSpan;
using static Encoding;
using static ConsoleColor;
using static StringComparison;
using static DehumidifierState;
using static Dehumidifier;
using static ComfortScale;
using static Comfort;
using static Values;

using State = DehumidifierState;

static partial class CLI
{
	enum Command : byte { Help, Status, On, Off, Toggle, Humidity, Mode, Next, Light, Sound, Lock, DryAfterOff, Timer, ResetFilter }

	/// <summary>Слова командъ съ синонимами — одна таблица на оба прохода и на раскраску справки.</summary>
	static readonly Dictionary<string, Command>.AlternateLookup<ReadOnlySpan<char>> Commands = new Dictionary<string, Command>(StringComparer.OrdinalIgnoreCase)
	{
		{ "help",          Command.Help        }, { "справка",        Command.Help        }, { "/?", Command.Help }, { "-?", Command.Help }, { "-h", Command.Help }, { "--help", Command.Help },
		{ "on",            Command.On          }, { "включить",       Command.On          },
		{ "off",           Command.Off         }, { "выключить",      Command.Off         },
		{ "toggle",        Command.Toggle      }, { "переключить",    Command.Toggle      },
		{ "humidity",      Command.Humidity    }, { "влажность",      Command.Humidity    },
		{ "mode",          Command.Mode        }, { "режимъ",         Command.Mode        }, { "режим",         Command.Mode  },
		{ "next",          Command.Next        }, { "слѣдующій",      Command.Next        }, { "следующий",     Command.Next  },
		{ "light",         Command.Light       }, { "lights",         Command.Light       }, { "подсвѣтка",     Command.Light }, { "подсветка", Command.Light },
		{ "sound",         Command.Sound       }, { "звукъ",          Command.Sound       }, { "звук",          Command.Sound },
		{ "lock",          Command.Lock        }, { "блокировка",     Command.Lock        },
		{ "dry-after-off", Command.DryAfterOff }, { "просушка",       Command.DryAfterOff },
		{ "timer",         Command.Timer       }, { "таймеръ",        Command.Timer       }, { "таймер",        Command.Timer },
		{ "reset-filter",  Command.ResetFilter }, { "сбросъ-фильтра", Command.ResetFilter }, { "сброс-фильтра", Command.ResetFilter },
		{ "status",        Command.Status      }, { "состояніе",      Command.Status      }, { "состояние",     Command.Status },

	}.GetAlternateLookup<ReadOnlySpan<char>>();

	static string[] ModeNames => [ModeSmart, ModeSleep, ModeDry];

	static readonly Dictionary<string, byte> Modes = new(StringComparer.OrdinalIgnoreCase)
	{
		{ "smart", 0 }, { "умный",  0 }, { "0", 0 },
		{ "sleep", 1 }, { "ночной", 1 }, { "1", 1 },
		{ "dry",   2 }, { "сушка",  2 }, { "2", 2 },
	};

	static string[] LightNames => [LightOff, LightDim, LightBright];

	/// <summary>Выключатель: подсвѣтка, звукъ, блокировка, просушка, таймеръ (только off), значеніе siid.piid=.</summary>
	static readonly Dictionary<string, bool> Switch = new(StringComparer.OrdinalIgnoreCase)
	{
		{ "on",  true  }, { "включить",  true  },
		{ "off", false }, { "выключить", false },
	};

	static readonly Dictionary<string, byte> LightLevels = new(StringComparer.OrdinalIgnoreCase)
	{
		{ "dim",    1 }, { "тусклая", 1 },
		{ "bright", 2 }, { "яркая",   2 },
	};

	/// <summary>Ширина столбца подписей въ status — по самой длинной подписи на языкѣ интерфейса; языкъ за время работы не мѣняется.</summary>
	static readonly int LabelWidth = new[] { CliPower, CliMode, CliLight, CliSound, CliLock, CliTemperature, CliHumidity, CliTarget, CliDryAfterOff, CliTimer, CliWarming, CliFault }.Max(l => l.Length) + 1;

	public static async Task<int> RunAsync(string[] args)
	{
		[DllImport("Kernel32", ExactSpelling = true)]
		[SuppressMessage("Interoperability", "SYSLIB1054: Используйте LibraryImportAttribute вместо DllImportAttribute для генерирования кода маршализации P/Invoke во время компиляции")]
		static extern bool AttachConsole(int processId = -1/*AttachParentProcess*/);

		AttachConsole();
		Encoding original = OutputEncoding;
		string? current = null;
		try
		{
			OutputEncoding = UTF8;
			WriteLine();

			static int Wrong(string message)
			{
				Print(message, Red, Error);
				Error.WriteLine();
				Error.WriteLine();
				PrintUsage(Error);
				return 2;
			}

			// первый проходъ: провѣрить всю строку — при ошибкѣ въ ней не выполняется ничего
			for (int i = 0; i < args.Length; i++)
			{
				current = args[i];

				if (!Commands.TryGetValue(current, out Command command))
				{
					if (Property().Match(current) is not { Success: true } p)
						return Wrong(Format(CliUnknownCommand, current));
					if (TryParse(p.Groups["siid"].ValueSpan, out byte siid) &&
						TryParse(p.Groups["piid"].ValueSpan, out byte piid))
					{
						if (p.Groups["value"] is not { Success: true } v) continue; // get
						if (v.Length > 0 && RawValue(v.Value) is not null) continue; // set
					}
					return Wrong(Format(CliBadProperty, current));
				}

				string? value;
				// значеніе команды — слѣдующее слово; оно дописывается къ current для сообщеній
				bool Value([NotNullWhen(true)] out string? value)
				{
					if (i + 1 == args.Length) {value = null; return false;}
					current += ' ' + (value = args[++i]);
					return true;
				}

				switch (command)
				{
				case Command.Help:
					PrintUsage();
					return 0;

				case Command.Mode:
					if (!Value(out value))
						return Wrong(Format(CliNoMode, current));
					if (!Modes.ContainsKey(value))
						return Wrong(Format(CliBadMode, current));
					break;

				case Command.Humidity:
					MIoTAttribute id = MIoT(nameof(State.dehumidifier_target_humidity));
					if (!Value(out value))
						return Wrong(Format(CliNoHumidity, current, id.Min, id.Max));
					if (!HumidityOf(value, out _))
						return Wrong(Format(CliBadHumidity, current, id.Min, id.Max));
					break;

				case Command.Sound or Command.Lock or Command.DryAfterOff:
					if (!Value(out value) || !Switch.ContainsKey(value))
						return Wrong(Format(CliBadSwitch, current));
					break;

				case Command.Timer:
					if (!Value(out value) || !Switch.ContainsKey(value) && Minutes(value) is null)
						return Wrong(Format(CliBadTimer, current));
					break;

				case Command.Light:
					int first = i;
					uint on = 0, br = 0;
					for (; i + 1 < args.Length; i++)
						if (Switch     .TryGetValue(args[i + 1], out bool o)) on++; else
						if (LightLevels.TryGetValue(args[i + 1], out byte b)) br++; else
						break;
					current = Join(' ', args, first, i + 1 - first);
					if (on + br < 1)
						return Wrong(Format(CliBadLight, current));
					if (on > 1 || br > 1)
						return Wrong(Format(CliLightTwice, current));
					break;
				}
			}

			current = null;
			using  Dehumidifier device = Connect();
			static Dehumidifier          Connect()
			{
				string ip    = Settings.Default.IP;
				string token = Settings.Default.Token;
				if (IsNullOrWhiteSpace(ip))    throw new ArgumentException(CliNoAddress);
				if (IsNullOrWhiteSpace(token)) throw new ArgumentException(CliNoToken);
				return new(new(ip, token));
			}

			// второй проходъ: выполнить по порядку черезъ одно подключеніе; значенія уже провѣрены
			for (int i = 0; i < args.Length; i++)
			{
				current = args[i];

				bool on;
				byte mode;
				string value;
				string Value() { current += ' ' + args[++i]; return args[i]; }

				// on|off у звука, блокировки и просушки
				async Task Flag(string property, string label)
				{
					bool on = Switch[Value()];
					await device.SetAsync((property, on));
					PrintLine($"{label} {(on ? CliStateOn : CliStateOff)}.", Green);
				}

				if (!Commands.TryGetValue(current, out Command command))
				{
					Match p = Property().Match(current);
					ReadOnlySpan<char> siidText = p.Groups["siid"].ValueSpan;
					ReadOnlySpan<char> piidText = p.Groups["piid"].ValueSpan;
					string name = $"{siidText}.{piidText}";
					byte siid = byte.Parse(siidText);
					byte piid = byte.Parse(piidText);
					if (p.Groups["value"] is { Success: true } v) // set
					{
						JsonNode raw = RawValue(v.Value)!;
						string json = raw.ToJsonString();
						await device.SetAsync(siid, piid, raw);
						PrintLine(Format(CliPropertySet, name, json), Green);
					}
					else // get
					{
						JsonNode? read = await device.GetAsync(siid, piid);
						Print(name, Cyan);
						if (PropertyName(siid, piid) is { } property) Print($" {property}", DarkGray);
						PrintLine($" = {read?.ToJsonString() ?? "null"}", White);
					}
				}
				else switch (command)
				{
				case Command.Status:
					State s = await device.GetStateAsync();
					Field(CliPower      , s.dehumidifier switch { true => (CliStateOn, Green), false => (CliStateOff, DarkGray), null => null });
					Field(CliMode       , s.dehumidifier_mode is byte m and < 3 ? (ModeNames[m], Cyan) : null);
					Field(CliLight      , s.indicator_light switch { false => (LightNames[0], DarkGray), true => s.indicator_light_mode is byte l and < 3 ? (LightNames[l], l == 0 ? DarkGray : White) : null, null => null });
					Field(CliSound      , OnOff(s.alarm));
					Field(CliLock       , OnOff(s.physical_controls_locked));
					Field(CliTemperature, s.environment_temperature      is { } c ? (Temperature(c), White) : null);
					Field(CliHumidity   , s.environment_relative_umidity is { } h ? ($"{h} %, {ComfortText(HumidityComfort(h))}", HumidityComfort(h) switch { Ideal => Green, Normal => DarkGreen, Dry or Humid => Yellow, _ => Red }) : null);
					Field(CliTarget     , s.dehumidifier_target_humidity is { } t ? ($"{t} %", White) : null);
					Field(CliDryAfterOff, DryText(s));
					Field(CliTimer      , TimerText(s));
					Field(CliWarming    , s.dm_service_is_warming_up switch { true => (WarmingYes, Yellow), false => (WarmingNo, DarkGray), null => null });
					Field(CliFault      , s.dehumidifier_fault is { } f ? (FaultText(f), f == 0 ? Green : Red) : null);

					static void Field(string label, (string Text, ConsoleColor Color)? value)
					{
						Print(label.PadRight(LabelWidth), Gray);
						PrintLine(value?.Text ?? "—", value?.Color ?? DarkGray);
					}

					static (string Text, ConsoleColor Color)? OnOff(bool? value) => value switch
					{
						true  => (CliStateOn, White),
						false => (CliStateOff, DarkGray),
						null  => null
					};

					/// <summary>Просушка послѣ выключенія: включена ли, а пока идётъ — и сколько осталось.</summary>
					static (string Text, ConsoleColor Color)? DryText(State s) =>
						s.dm_service_dry_left_time is ushort left and > 0
							? (Format(CliRemaining, s.dm_service_dry_after_off == false ? CliStateOff : CliStateOn, Duration(FromSeconds(left))), White)
							:                 OnOff(s.dm_service_dry_after_off);

					/// <summary>Таймеръ: на сколько поставленъ и сколько осталось.</summary>
					static (string Text, ConsoleColor Color)? TimerText(State s) => s.delay switch
					{
						false => (CliStateOff, DarkGray),
						true  => (s.delay_remain_time is { } left
							? Format(CliRemaining, Duration(FromMinutes(s.delay_time ?? 0)), Duration(FromMinutes(left)))
							:                      Duration(FromMinutes(s.delay_time ?? 0)), White),
						null  => null
					};
					break;


				case Command.On:
					await device.SetAsync((nameof(State.dehumidifier), true));
					PrintLine(CliSwitchedOn , Green);
					break;

				case Command.Off:
					await device.SetAsync((nameof(State.dehumidifier), false));
					PrintLine(CliSwitchedOff, Green);
					break;

				case Command.Toggle:
					await device.ToggleAsync();
					PrintLine(CliToggled, Green);
					break;

				case Command.Next:
					await device.LoopModeAsync();
					PrintLine(CliNextMode, Green);
					break;

				case Command.ResetFilter:
					await device.ResetFilterAsync();
					PrintLine(CliFilterReset, Green);
					break;

				case Command.Mode:
					mode = Modes[Value()];
					await device.SetAsync((nameof(State.dehumidifier_mode), mode));
					PrintLine(Format(CliModeSet, ModeNames[mode]), Green);
					break;

				case Command.Humidity:
					_ = HumidityOf(Value(), out byte humidity);
					await device.SetAsync((nameof(State.dehumidifier_target_humidity), humidity));
					PrintLine(Format(CliTargetSet, humidity), Green);
					break;

				case Command.Sound:
					await Flag(nameof(State.alarm), CliSound);
					break;

				case Command.Lock:
					await Flag(nameof(State.physical_controls_locked), CliLock);
					break;

				case Command.DryAfterOff:
					await Flag(nameof(State.dm_service_dry_after_off), CliDryAfterOff);
					break;

				case Command.Light:
					StringBuilder done = new();
					bool o = false, b = false;
					List<(string Name, JsonNode Value)> values = new(2);
					for (; i + 1 < args.Length; i++)
					{
						if (!o && (o = Switch     .TryGetValue(args[i + 1], out   on))) { values.Add((nameof(State.indicator_light), on)); value = on ? CliLightSwitchOn : CliLightSwitchOff; } else
						if (!b && (b = LightLevels.TryGetValue(args[i + 1], out mode))) { values.Add((nameof(State.indicator_light_mode), mode)); value = Format(CliBrightness, LightNames[mode]); }
						else break;
						if (done.Length > 0)
							done.Append(", ");
						done.Append(value);
						current += ' ' + args[i + 1];
					}
					await device.SetAsync(values);
					PrintLine(Format(CliLightSet, done.ToString()), Green);
					break;

				case Command.Timer:
					value = Value();
					if (Switch.TryGetValue(value, out on))
					{
						await device.SetAsync((nameof(State.delay), on));
						PrintLine($"{CliTimer} {(on ? CliStateOn : CliStateOff)}.", Green);
					}
					else
					{
						ushort minutes = Minutes(value)!.Value;
						await device.SetAsync((nameof(State.delay_time), minutes));
						PrintLine($"{CliTimer} {Duration(FromMinutes(minutes))}.", Green);
					}
					break;
				}
			}
			return 0;
		}
		catch (Exception e) when (e is miIOException or IOException or ArgumentException or SocketException)
		{
			PrintLine(current is null ? Format(CliError, e.Message)
			/**/                      : Format(CliErrorIn, current, e.Message), Red, Error);
			return 1;
		}
		finally
		{
			OutputEncoding = original;
		}
	}

	// ───── разборъ значеній: одни и тѣ же для провѣрки и для выполненія ─────

	/// <summary>Значеніе для записи siid.piid=: on|off (включить|выключить) — true|false, иначе JSON (число, true, "строка"…), а что не JSON — строка.
	/// null — для JSON null: на него осушитель отвѣчаетъ code 0, но что дѣлаетъ — неизвѣстно; такое не пишемъ.</summary>
	static JsonNode? RawValue(string text)
	{
		if (Switch.TryGetValue(text, out bool on)) return on;
		try { return JsonNode.Parse(text); }
		catch (JsonException) { return text; }
	}

	// ───── справка и выводъ ─────

	static void PrintUsage(TextWriter? error = null)
	{
		bool first = true;
		ReadOnlySpan<char> usage = CliUsage;
		foreach (Range range in usage.Split('\n'))
		{
			ReadOnlySpan<char> line = usage[range].TrimEnd('\r');
			if (first)
			{
				first = false;
				PrintLine(line, Yellow, error);
			}
			else if (line.StartsWith(CliExitCodePrefix))
				PrintExitCodes(line, error);
			else if (line is [' ', ' ', not ' ', ..]) // примѣръ команды: отступъ въ два пробѣла, команда, затѣмъ (необязательно) описаніе послѣ двухъ и болѣе пробѣловъ
			{
				int gap = line[2..].IndexOf("  ");
				ReadOnlySpan<char> command = gap < 0 ? line : line[..(gap + 2)];
				PrintCommand(command, error);
				PrintLine(line[command.Length..], Gray, error);
			}
			else PrintLine(line, Gray, error);
		}
	}

	/// <summary>Слова примѣра разнымъ цвѣтомъ: exe, слова командъ (изъ той же таблицы, что и разборъ), параметры; пробѣлы — какъ есть.</summary>
	static void PrintCommand(ReadOnlySpan<char> command, TextWriter? error)
	{
		while (!command.IsEmpty)
		{
			int start = command.IndexOfAnyExcept(' ');
			if (start < 0) start = command.Length;
			Print(command[..start], Gray, error);
			command = command[start..];
			int end = command.IndexOf(' ');
			if (end < 0) end = command.Length;
			ReadOnlySpan<char> word = command[..end];
			Print(word, word.EndsWith(".exe", OrdinalIgnoreCase) ? Magenta
			/**/                : Commands.ContainsKey(word) ? Cyan : DarkCyan, error);
			command = command[end..];
		}
	}

	/// <summary>Строка кодовъ выхода: цифра ярче описанія (FOREGROUND_INTENSITY), цвѣтъ — по коду.</summary>
	static void PrintExitCodes(ReadOnlySpan<char> line, TextWriter? error)
	{
		int end = 0;
		foreach (ValueMatch code in ExitCode().EnumerateMatches(line))
		{
			Print(line[end..code.Index], Gray, error);
			ReadOnlySpan<char> part = line.Slice(code.Index, code.Length);
			(ConsoleColor bright, ConsoleColor dark) = part[0] switch
			{
				'0' => (Green , DarkGreen),
				'1' => (Red   , DarkRed),
				'2' => (Yellow, DarkYellow),
				_   => (Gray  , Gray),
			};
			Print(part[..1], bright, error);
			Print(part[1..], dark  , error);
			end = code.Index + code.Length;
		}
		PrintLine(line[end..], Gray, error);
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

	/// <summary>Одинъ кодъ выхода въ справкѣ: цифра, тире и описаніе до запятой или точки.</summary>
	[GeneratedRegex(@"(\d — [^,.]+)")]
	private static partial Regex ExitCode();

	/// <summary>Свойство MIoT по номерамъ: siid.piid — прочитать, siid.piid=значеніе — записать.</summary>
	[GeneratedRegex(@"^(?<siid>\d+)\.(?<piid>\d+)(?:=(?<value>.*))?$")]
	private static partial Regex Property();
}

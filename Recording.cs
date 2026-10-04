using System.Buffers;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace DehumidifierControl;

using static File;
using static Path;
using static Directory;
using static Byte;
using static Single;
using static DateTime;
using static Encoding;
using static FileMode;
using static FileShare;
using static FileAccess;
using static SampleKind;
using static NumberStyles;
using static MidpointRounding;
using static CollectionsMarshal;
using static Environment.SpecialFolder;
using static Environment;
using static CultureInfo;
using static Psychrometrics;
using static RecordFiles;
using static LogFormat;
using static Reading;

using State = DehumidifierState;

/// <summary>Что вліяетъ на осушеніе и что онъ показываетъ — то, что пишется въ записи.</summary>
public readonly record struct Reading(float? Temperature, float? Humidity, bool? Power, byte? Mode, byte? Target, byte? Fault, bool? Warming)
{
	/// <summary>Отъ чего отсчитывается первая строка файла: неисправности нѣтъ, прогрѣва нѣтъ — ихъ въ ней не пишемъ.</summary>
	public static readonly Reading Initial = new(null, null, null, null, null, 0, false);

	public Reading(State s) : this(s.environment_temperature,
	/**/                           s.environment_relative_umidity,
	/**/                           s.dehumidifier,
	/**/                           s.dehumidifier_mode,
	/**/                           s.dehumidifier_target_humidity,
	/**/                           s.dehumidifier_fault,
	/**/                           s.dm_service_is_warming_up){}

	/// <summary>Осушаетъ ли сейчасъ: включёнъ, прогрѣвъ кончился, неисправности (въ томъ числѣ полнаго бака) нѣтъ.
	/// Работаетъ ли компрессоръ, устройство не говоритъ; въ умномъ и ночномъ режимахъ онъ ещё останавливается у цѣли.</summary>
	public bool Working => Power == true && Warming != true && Fault is 0 or null;

	/// <summary>Абсолютная влажность, г/м³, если извѣстны температура и влажность.</summary>
	public double? Water => Temperature is { } t && Humidity is { } h ? AbsoluteHumidity(t, h) : null;
}

public enum SampleKind : byte
{
	/// <summary>Измѣнилось что-то изъ записываемаго.</summary>
	Change,
	/// <summary>rec — начало записи (или продолженіе послѣ вырѣзаннаго).</summary>
	Rec,
	/// <summary>lost — связь потеряна: до слѣдующей строки значенія не извѣстны, на графикѣ — пунктиромъ.</summary>
	Lost,
	/// <summary>stop — конецъ записи (или начало вырѣзаннаго).</summary>
	Stop,
}

/// <summary>Строка записи: когда, что за строка и состояніе послѣ нея.
/// Структура: списокъ строкъ — сплошной массивъ, безъ объекта въ кучѣ на каждую.</summary>
public readonly record struct Sample(DateTime Time, SampleKind Kind, Reading State);

/// <summary>Ключъ двоичнаго поиска по времени: «больше» всякой записи раньше cutoff и «меньше» остальныхъ.
/// Равной не бываетъ, поэтому BinarySearch всегда отдаётъ ~(число записей раньше cutoff).</summary>
readonly struct OlderThan(DateTime cutoff) : IComparable<Sample>
{
	public int CompareTo(Sample other) => other.Time < cutoff ? 1 : -1;
}

/// <summary>Какъ OlderThan, но граница включительно: «больше» всякой записи не позже time.</summary>
readonly struct NotLaterThan(DateTime time) : IComparable<Sample>
{
	public int CompareTo(Sample other) => other.Time <= time ? 1 : -1;
}

/// <summary>Формат файла записи: строки черезъ \n, въ строкѣ — время и измѣненія черезъ пробѣлъ:
/// <code>
/// 2026-10-04 14:05:00 rec on smart 40% 44,3% 24°
/// 2026-10-04 14:05:13 45%
/// 2026-10-04 14:31:10 lost
/// 2026-10-04 15:10:00 stop
/// </code>
/// on/off — питаніе; smart/night/dry — режимъ, за smart и night всегда слѣдуетъ цѣль NN%; прочія NN% — влажность,
/// NN,N° — температура; warm/ready — прогрѣвъ, okN/errN — неисправность. Первая строка — полное состояніе
/// (прогрѣвъ и неисправность — только если есть), дальше — только измѣнившееся.</summary>
public static class LogFormat
{
	/// <summary>Режимъ сушки бѣлья: цѣли у него нѣтъ — онъ сушитъ безъ остановки.</summary>
	public const byte DryMode = 2;

	public const string Extension = ".log";
	public const string TimeFormat = "yyyy-MM-dd HH:mm:ss"; // повторёнъ въ Line: въ $"{time:…}" константу не подставить

	/// <summary>Кодировка файла — 1251: всё, что въ немъ бываетъ, въ ней есть (° — 0xB0).</summary>
	public static readonly Encoding FileEncoding = GetFileEncoding();
	static                 Encoding                GetFileEncoding()
	{
		RegisterProvider(CodePagesEncodingProvider.Instance);
		return GetEncoding(1251);
	}

	/// <summary>Число — съ запятою, какъ пишутъ по-русски; въ строкѣ его отдѣляютъ пробѣлы, такъ что запятая не мѣшаетъ.</summary>
	static readonly NumberFormatInfo NumberFormat = new() { NumberDecimalSeparator = "," };

	static string? ModeWord(byte? mode) => mode switch
	{
		0       => "smart",
		1       => "night",
		DryMode => "dry",
		_       => null,
	};

	static string? KindWord(SampleKind kind) => kind switch
	{
		Rec  => "rec",
		Lost => "lost",
		Stop => "stop",
		_    => null,
	};

	/// <summary>Строка файла — въ line (прежнее въ нёмъ стирается): время, слово вида (если не простое измѣненіе) и измѣненія
	/// отъ written къ now (питаніе, режимъ съ цѣлью, влажность, температура, прогрѣвъ, неисправность); false — писать нечего.
	/// Строитель даётъ вызывающій и беретъ одинъ на много строкъ: строка не выдѣляется, а пишется прямо изъ него.
	/// written — что уже въ файлѣ: дополняется записаннымъ (числа сравниваются въ десятыхъ, какъ пишутся).</summary>
	public static bool Line(StringBuilder line, DateTime time, SampleKind kind, ref Reading written, Reading now)
	{
		line.Clear();
		if (KindWord(kind) is { } kindWord)
			Word().Append(kindWord);
		if (now.Power is { } power && power != written.Power)
		{
			written = written with { Power = power };
			Word().Append(power ? "on" : "off");
		}
		bool modeChanged   = now.Mode is byte mode && mode != written.Mode;
		bool targetChanged = now.Target is byte target && target != written.Target && now.Mode != DryMode;
		if ((targetChanged || modeChanged) && ModeWord(now.Mode) is { } word)
		{
			written = written with { Mode = now.Mode };
			Word().Append(word);
			if (now.Mode != DryMode && now.Target is byte t)
			{
				written = written with { Target = t };
				Word().Append(t).Append('%');
			}
		}
		if (Tenths(now.Humidity) is { } h && h != Tenths(written.Humidity))
		{
			written = written with { Humidity = now.Humidity };
			Word().Append(NumberFormat, $"{h / 10f:0.#}%");
		}
		if (Tenths(now.Temperature) is { } c && c != Tenths(written.Temperature))
		{
			written = written with { Temperature = now.Temperature };
			Word().Append(NumberFormat, $"{c / 10f:0.#}°");
		}
		if (now.Warming is { } w && w != written.Warming)
		{
			written = written with { Warming = w };
			Word().Append(w ? "warm" : "ready");
		}
		if (now.Fault is { } f && f != written.Fault)
		{
			written = written with { Fault = f };
			if (f == 0)
				Word().Append("ok");
			else
				Word().Append("err").Append(f);
		}
		return line.Length > 0; // пустъ — ни слова: и время не писали

		// первое слово начинаетъ строку со времени (= TimeFormat), прямо въ строитель
		StringBuilder Word() => (line.Length > 0 ? line : line.Append(InvariantCulture, $"{time:yyyy-MM-dd HH:mm:ss}")).Append(' ');

		/// <summary>Число въ десятыхъ — какимъ оно пишется въ файлъ; пишется изъ нихъ же, такъ что текстъ и сравненіе не разойдутся.</summary>
		static int? Tenths(float? value) => value is { } v ? (int)Round(v * 10, AwayFromZero) : null;
	}

	/// <summary>Разобрать текстъ файла записи въ samples, продолжая состояніе state; строки, которыя не разобрать, пропускаются.
	/// Строки и слова — срѣзы текста: ни одной строки въ кучѣ.</summary>
	public static void Parse(ReadOnlySpan<char> text, Reading state, List<Sample> samples)
	{
		samples.EnsureCapacity(samples.Count + text.Count('\n') + 1); // сразу по числу строкъ — безъ лишнихъ удвоеній и копированій
		foreach (Range lineRange in text.Split('\n'))
		{
			ReadOnlySpan<char> line = text[lineRange].Trim(); // и \r, если файлъ правили въ Блокнотѣ
			if (line.Length < TimeFormat.Length) continue;
			if (!TryParseTime(line[..TimeFormat.Length], out DateTime time)) continue;
			ReadOnlySpan<char> rest = line[TimeFormat.Length..];
			SampleKind kind = Change;
			bool first = true, targetNext = false;
			foreach (Range range in rest.Split(' '))
			{
				ReadOnlySpan<char> word = rest[range];
				if (word.IsEmpty) continue;
				if (first)
				{
					first = false;
					kind = word switch { "rec" => Rec, "lost" => Lost, "stop" => Stop, _ => Change };
					if (kind != Change) continue;
				}
				bool afterMode = targetNext;
				state = word switch
				{
					"smart" => state with { Mode    = 0       },
					"night" => state with { Mode    = 1       },
					"dry"   => state with { Mode    = DryMode },
					"on"    => state with { Power   = true    },
					"off"   => state with { Power   = false   },
					"warm"  => state with { Warming = true    },
					"ready" => state with { Warming = false   },
					"ok"    => state with { Fault   = 0       },
					['e', 'r', 'r', .. var e] when TryParse (e, out byte    fault) => state with { Fault = fault },
					[..           var n, '°'] when TryParseF(n, out float degrees) => state with { Temperature = degrees },
					[..           var n, '%'] when TryParseF(n, out float percent) => afterMode
					/**/                                                            ? state with { Target = (byte)Clamp(Round(percent), 0, 100) }
					/**/                                                            : state with { Humidity = percent },
					_                                                              => state,
				};
				targetNext = word is "smart" or "night";
			}
			samples.Add(new(time, kind, state));
		}

		/// <summary>Число съ запятою (какъ пишемъ) или съ точкою — по срѣзу, безъ замѣны въ новой строкѣ.</summary>
		static bool TryParseF(ReadOnlySpan<char> text, out float value) =>
			TryParse(text, Float, NumberFormat, out value) ||
			TryParse(text, Float, InvariantCulture, out value);

		/// <summary>Время по TimeFormat — по мѣстамъ цифръ: въ разы быстрѣе TryParseExact, который на каждой строкѣ разбираетъ и самъ шаблонъ.</summary>
		static bool TryParseTime(ReadOnlySpan<char> s, out DateTime time)
		{
			time = default;
			if (s is not [_, _, _, _, '-', _, _, '-', _, _, ' ', _, _, ':', _, _, ':', _, _]
			 || !Number(s[..4],    out int year) || !Number(s[5..7],   out int month)  || !Number(s[8..10], out int day)
			 || !Number(s[11..13], out int hour) || !Number(s[14..16], out int minute) || !Number(s[17..],  out int second)
			 || year < 1 || month is < 1 or > 12 || day < 1 || day > DaysInMonth(year, month) || hour > 23 || minute > 59 || second > 59)
				return false;
			time = new(year, month, day, hour, minute, second);
			return true;

			static bool Number(ReadOnlySpan<char> digits, out int value) => int.TryParse(digits, NumberStyles.None, InvariantCulture, out value);
		}
	}
}

/// <summary>Ведётъ запись въ файлъ: строка rec съ полнымъ состояніемъ, потомъ только измѣненія, lost при потерѣ связи, stop въ концѣ.
/// Каждая строка сразу сбрасывается на дискъ — если программа упадётъ, запись просто кончится послѣдней строкой.</summary>
public sealed class Recorder : IDisposable
{
	public string Path { get; }

	readonly StreamWriter Writer;
	readonly StringBuilder Text = new(64); // строка — каждый разъ въ этотъ же строитель: на запись безъ выдѣленій памяти
	Reading Written = Initial; // что уже въ файлѣ — отъ этого считаются измѣненія
	bool Started;
	bool LinkDown;

	/// <summary>Въ файлъ добавлена строка.</summary>
	public event EventHandler? LineWritten;

	public Recorder(DateTime start)
	{
		CreateDirectory(Folder);
		string name = start.ToString("yyyy-MM-dd HH-mm-ss", InvariantCulture);
		string path = Combine(Folder, name + Extension);
		for (int i = 2; File.Exists(path); i++) // двѣ записи въ одну секунду — рѣдко, но имя не должно совпасть
			path = Combine(Folder, $"{name} {i}{Extension}");
		Path = path;
		Writer = new(new FileStream(path, CreateNew, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete), FileEncoding) { NewLine = "\n" };
	}

	/// <summary>Пришло состояніе: пишемъ, если что-то измѣнилось (первая строка — rec съ полнымъ состояніемъ).</summary>
	public void Add(DateTime time, Reading state)
	{
		LinkDown = false;
		Write(time, Started ? Change : Rec, state);
		Started = true;
	}

	/// <summary>Связь потеряна — одна строка lost на весь обрывъ.</summary>
	public void LinkLost(DateTime time)
	{
		if (!Started || LinkDown) return;
		LinkDown = true;
		Write(time, Lost, Written);
	}

	/// <summary>Конецъ записи. Если не пришло ни одного состоянія — пустой файлъ удаляется.</summary>
	public void Stop(DateTime time)
	{
		if (Started)
			Write(time, SampleKind.Stop, Written);
		Writer.Dispose();
		if (!Started)
			File.Delete(Path);
	}

	void Write(DateTime time, SampleKind kind, Reading state)
	{
		if (!Line(Text, time, kind, ref Written, state)) return;
		Writer.WriteLine(Text);
		Writer.Flush();
		LineWritten?.Invoke(this, EventArgs.Empty);
	}

	public void Dispose() => Writer.Dispose();
}

/// <summary>Сеансъ записи — одинъ файлъ.</summary>
public sealed record Session(string Path, DateTime Start, DateTime End, int Lines);

/// <summary>Файлы записей: списокъ, чтеніе, удаленіе, вырѣзаніе отрѣзка.</summary>
public static class RecordFiles
{
	public static string Folder { get; } = Combine(GetFolderPath(LocalApplicationData), "Dehumidifier", "Data");

	/// <summary>Сеансы, новые сверху; пустые и неразборчивые файлы пропускаются.</summary>
	public static List<Session> List() => List(Load);

	/// <summary>То же, а файлы читаетъ load: окно графиковъ даётъ свой, съ прочитаннымъ раньше, — неизмѣнные файлы не разбираются заново.</summary>
	public static List<Session> List(Func<string, List<Sample>> load)
	{
		List<Session> sessions = [];
		if (!Directory.Exists(Folder))
			return sessions;
		foreach (string path in EnumerateFiles(Folder, "*" + Extension))
		{
			try
			{
				List<Sample> samples = load(path);
				if (samples.Count > 0)
					sessions.Add(new(path, samples[0].Time, samples[^1].Time, samples.Count));
			}
			catch (IOException)
			{
				// файлъ занятъ или пропалъ — пропускаемъ
			}
		}
		sessions.Sort((a, b) => b.Start.CompareTo(a.Start));
		return sessions;
	}

	/// <summary>Читаетъ и тотъ файлъ, въ который сейчасъ идётъ запись.</summary>
	public static List<Sample> Load(string path)
	{
		List<Sample> samples = [];
		Read(path, 0, wholeLines: false, samples);
		return samples;
	}

	/// <summary>Дочитать файлъ, въ который идётъ запись: съ offset, только цѣлыя строки (недописанная — въ другой разъ),
	/// продолжая состояніе послѣдней строки samples; новыя строки — въ samples. Возвращаетъ, до какого байта дочитано.</summary>
	public static long Append(string path, long offset, List<Sample> samples) => Read(path, offset, wholeLines: true, samples);

	/// <summary>Съ offset до конца — разомъ, въ заёмные буферы, и разобрать срѣзами; wholeLines — только до послѣдняго \n.
	/// 1251 — однобайтовая: байтъ — буква, такъ что мѣсто въ файлѣ и въ текстѣ одно.</summary>
	static long Read(string path, long offset, bool wholeLines, List<Sample> samples)
	{
		using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, bufferSize: 0); // читаемъ разомъ — буферъ потока не нуженъ
		int length = (int)Math.Max(0, stream.Length - offset); // Math: иначе изъ using static подхватится Single.Max
		byte[] bytes = ArrayPool<byte>.Shared.Rent(length);
		char[] chars = ArrayPool<char>.Shared.Rent(length);
		try
		{
			stream.Position = offset;
			length = stream.ReadAtLeast(bytes.AsSpan(0, length), length, throwOnEndOfStream: false);
			if (wholeLines)
				length = bytes.AsSpan(0, length).LastIndexOf((byte)'\n') + 1;
			Parse(chars.AsSpan(0, FileEncoding.GetChars(bytes, 0, length, chars, 0)), samples is [.., var last] ? last.State : Initial, samples);
			return offset + length;
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(bytes);
			ArrayPool<char>.Shared.Return(chars);
		}
	}

	public static void Delete(string path) => File.Delete(path);

	/// <summary>Состояніе на моментъ time — по послѣдней строкѣ не позже него; null — до начала записи.</summary>
	public static Reading? StateAt(ReadOnlySpan<Sample> samples, DateTime time) =>
		CountUpTo(samples, time) is var count and > 0 ? samples[count - 1].State : null;

	// Сколько строкъ раньше time / не позже time — двоичнымъ поискомъ: строки идутъ по времени.
	internal static int CountBefore(ReadOnlySpan<Sample> samples, DateTime time) => ~samples.BinarySearch(new OlderThan(time));
	internal static int CountUpTo  (ReadOnlySpan<Sample> samples, DateTime time) => ~samples.BinarySearch(new NotLaterThan(time));

	/// <summary>Вырѣзать [from; to]: на from — stop, на to — rec съ тѣмъ, что измѣнилось за вырѣзанное.
	/// Вырѣзано начало — первой строкою станетъ rec съ полнымъ состояніемъ; вырѣзанъ конецъ — запись кончится на from.</summary>
	public static List<Sample> Cut(List<Sample> samples, DateTime from, DateTime to)
	{
		ReadOnlySpan<Sample> all = AsSpan(samples);
		int before = CountBefore(all, from), after = CountUpTo(all, to); // all[..before] — до from, all[after..] — послѣ to
		List<Sample> result = new(before + 2 + all.Length - after);
		result.AddRange(all[..before]);
		if (before > 0 && all[before - 1].Kind != Stop)
			result.Add(new(from, Stop, all[before - 1].State));
		if (after > 0 && after < all.Length)
			result.Add(new(to, Rec, all[after - 1].State));
		result.AddRange(all[after..]);
		return Tidy(result);
	}

	/// <summary>Оставить только [from; to]: на from — rec съ состояніемъ въ этотъ моментъ, на to — stop, если запись шла и дальше.</summary>
	public static List<Sample> Trim(List<Sample> samples, DateTime from, DateTime to)
	{
		ReadOnlySpan<Sample> all = AsSpan(samples);
		int start = CountUpTo(all, from), end = CountUpTo(all, to); // all[start..end] — (from; to]
		List<Sample> result = new(end - start + 2);
		if (start > 0)
			result.Add(new(from, Rec, all[start - 1].State));
		result.AddRange(all[start..end]);
		if (result.Count > 0 && result[^1].Kind != Stop && end < all.Length)
			result.Add(new(to, Stop, result[^1].State));
		return Tidy(result);
	}

	/// <summary>Убрать то, что послѣ правки потеряло смыслъ: stop въ самомъ началѣ, rec сразу послѣ rec, двѣ остановки подрядъ.
	/// Правитъ на мѣстѣ: оставленное пишется позади читаемаго.</summary>
	static List<Sample> Tidy(List<Sample> samples)
	{
		Span<Sample> span = AsSpan(samples);
		int count = 0;
		foreach (Sample sample in span)
		{
			if (sample.Kind == Stop && (count == 0 || span[count - 1].Kind == Stop)) continue;
			Sample kept = count > 0 && span[count - 1].Kind == Stop && sample.Kind == Change ? sample with { Kind = Rec } : sample;
			span[count++] = kept;
		}
		samples.RemoveRange(count, samples.Count - count);
		if (count > 0 && samples[0].Kind != Rec)
			samples[0] = samples[0] with { Kind = Rec };
		return samples;
	}

	/// <summary>Переписать файлъ по образцамъ (первая строка — отъ Reading.Initial, дальше — измѣненія отъ предыдущей):
	/// сначала во временный рядомъ, потомъ замѣна — при сбоѣ старый файлъ цѣлъ.</summary>
	public static void Save(string path, IEnumerable<Sample> samples)
	{
		string temp = path + ".tmp";
		bool any = false;
		StringBuilder line = new(64); // одинъ на весь файлъ: строки пишутся прямо изъ него
		Reading written = Initial;
		using (StreamWriter writer = new(temp, append: false, FileEncoding) { NewLine = "\n" })
			foreach (Sample sample in samples)
				if (Line(line, sample.Time, sample.Kind, ref written, sample.State))
				{
					writer.WriteLine(line);
					any = true;
				}
		if (!any)
		{
			File.Delete(temp);
			File.Delete(path);
			return;
		}
		Move(temp, path, overwrite: true);
	}
}

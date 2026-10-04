using System.Runtime.InteropServices;

namespace DehumidifierControl;

using Properties;

using static Double;
using static TimeSpan;
using static CollectionsMarshal;
using static LogFormat;
using static SampleKind;
using static SegmentKind;
using static Psychrometrics;

/// <summary>Модель комнаты съ осушителемъ. Перемѣнная — абсолютная влажность ρ, г/м³: относительная скачетъ отъ температуры
/// (осушитель самъ грѣетъ комнату), а количество воды — нѣтъ.
/// <code>k·V·dρ/dt = n·V·(ρ_out − ρ) + S − c·max(0, ρ − ρ_min(T))·w</code>
/// V — объёмъ комнаты, м³; k ≥ 1 — буферъ: стѣны, мебель, ткани отдаютъ и забираютъ влагу, и воды «за воздухомъ» въ k разъ больше;
/// n — кратность воздухообмѣна, ч⁻¹; ρ_out — абсолютная влажность снаружи; S — источники въ комнатѣ, г/ч (человѣкъ — около 50);
/// c — «эффективный потокъ» осушителя, м³/ч; ρ_min — ниже этого (≈ 35 %) компрессорный осушитель почти не сушитъ;
/// w — осушаетъ ли (1/0). Правая часть безъ осушителя — мощность насыщенія комнаты влагою средою, г/ч.
/// Уравненіе линейно по ρ, поэтому ρ идётъ къ предѣлу по экспонентѣ: ρ(t) = ρ∞ + (ρ₀ − ρ∞)·e^(−t/τ).</summary>
public sealed record RoomModel(double Volume, double Buffer, double Exchange, double OutdoorWater, double Sources, double Flow)
{
	/// <summary>Ниже этой влажности компрессорный осушитель почти не сушитъ: змѣевикъ не холоднѣе точки росы.</summary>
	public const double MinHumidity = 35;

	/// <summary>Паспортная производительность — при 30 °Ц и 80 %.</summary>
	public const double RatedTemperature = 30, RatedHumidity = 80;

	/// <summary>Эффективный потокъ, м³/ч, по паспортной производительности, л/сутки: G = c·(ρ − ρ_min) въ паспортной точкѣ.</summary>
	public static double FlowFromRated(double litersPerDay) =>
		litersPerDay * 1000 / 24 / (AbsoluteHumidity(RatedTemperature, RatedHumidity) - AbsoluteHumidity(RatedTemperature, MinHumidity));

	/// <summary>Паспортная производительность, л/сутки, по эффективному потоку — обратное къ FlowFromRated.</summary>
	public static double RatedFromFlow(double flow) => flow * 24 / 1000 * (AbsoluteHumidity(RatedTemperature, RatedHumidity) - AbsoluteHumidity(RatedTemperature, MinHumidity));

	/// <summary>Изъ настроекъ.</summary>
	public static RoomModel FromSettings() => new(
		/**/                 Settings.Default.RoomVolume,
		/**/                 Settings.Default.BufferFactor,
		/**/                 Settings.Default.AirExchange,
		/**/AbsoluteHumidity(Settings.Default.OutdoorTemperature,
		/**/                 Settings.Default.OutdoorHumidity),
		/**/                 Settings.Default.MoistureSources,
		/**/   FlowFromRated(Settings.Default.RatedCapacity));

	public static double MinWater(double t) => AbsoluteHumidity(t, MinHumidity);

	/// <summary>Мощность насыщенія: сколько воды въ часъ приносятъ въ комнату воздухообмѣнъ и источники при ρ, г/ч.</summary>
	public double Inflow(double rho) => Exchange * Volume * (OutdoorWater - rho) + Sources;

	/// <summary>Сколько воды въ часъ снимаетъ осушитель при ρ и T, г/ч.</summary>
	public double Removal(double rho, double t) => Flow * Max(0, rho - MinWater(t));

	/// <summary>Куда идётъ влажность и какъ быстро: постоянная времени τ, ч, и предѣлъ ρ∞, г/м³.</summary>
	public (double Tau, double Limit) Course(double t, bool working)
	{
		double exchange = Exchange * Volume, flow = working ? Flow : 0, sum = exchange + flow;
		if (sum <= 0) return (PositiveInfinity, Sources > 0 ? PositiveInfinity : NaN);
		return (Buffer * Volume / sum, (exchange * OutdoorWater + Sources + flow * MinWater(t)) / sum);
	}

	/// <summary>Сколько воды убрать до цѣли вмѣстѣ съ буферомъ, г.</summary>
	public double WaterToRemove(double t, double rh, double target) => Buffer * Psychrometrics.WaterToRemove(Volume, t, rh, target);
}

/// <summary>Оцѣнка: когда влажность дойдётъ до цѣли. Hours — null, если не дойдётъ (предѣлъ выше цѣли).</summary>
public readonly record struct Forecast(double? Hours, double LimitHumidity)
{
	public bool Reached => Hours == 0;

	/// <summary>ρ — сейчасъ, t — температура, target — цѣль, %; τ и ρ∞ — изъ модели или изъ подгонки по наблюденію.
	/// Время — изъ ρ(t) = ρ∞ + (ρ₀ − ρ∞)·e^(−t/τ): t = τ·ln((ρ₀ − ρ∞)/(ρ_цѣль − ρ∞)).</summary>
	public static Forecast Estimate(double rho, double t, double target, double tau, double limit)
	{
		double goal = AbsoluteHumidity(t, target);
		double limitHumidity = IsFinite(limit) ? RelativeHumidity(t, limit) : PositiveInfinity;
		if (rho <= goal) return new(0, limitHumidity);
		if (!IsFinite(tau) || !IsFinite(limit) || limit >= goal)
			return new(null, limitHumidity);
		return new(tau * Log((rho - limit) / (goal - limit)), limitHumidity);
	}
}

/// <summary>Подгонка экспоненты y = ρ∞ + (ρ₀ − ρ∞)·e^(−t/τ): τ, ч; предѣлъ; начало (t = 0 — первая точка); ошибка; точекъ.
/// Bounded — τ упёрлась въ край перебора: ходъ почти прямой (или почти мгновенный), предѣлъ ненадёженъ.</summary>
public readonly record struct ExpFit(double Tau, double Limit, double Start, double Rmse, int Count, double Hours, bool Bounded);

public enum SegmentKind
{
	/// <summary>Выключенъ — влажность идётъ къ равновѣсію со средою.</summary>
	Off,
	/// <summary>Осушаетъ: включёнъ, прогрѣвъ кончился, неисправности нѣтъ, влажность выше цѣли (или режимъ сушки бѣлья).</summary>
	Drying,
}

/// <summary>Отрѣзокъ записи съ однимъ поведеніемъ и подгонкою по нему.</summary>
public sealed record Segment(DateTime Start, DateTime End, SegmentKind Kind, byte? Mode, double MeanWater, double MeanTemperature, ExpFit Fit);

/// <summary>Опознаніе модели по записямъ.</summary>
public static class MoistureFit
{
	/// <summary>Предѣлы перебора τ, ч: отъ минуты до трёхъ недѣль.</summary>
	const double MinTau = 1.0 / 60, MaxTau = 500;
	const int TauSteps = 240;

	/// <summary>Отрѣзокъ короче — не подгоняемъ.</summary>
	public static readonly TimeSpan MinSpan = FromMinutes(10);

	/// <summary>Меньше смѣнъ влажности — не подгоняемъ.</summary>
	public const int MinPoints = 4;

	/// <summary>Перебираемъ τ по логарифмической сѣткѣ; при данной τ модель линейна: y = a + b·x, x = e^(−t/τ) — МНК въ замкнутомъ видѣ.
	/// Устойчиво и при влажности съ шагомъ 1 %, когда производныя по точкамъ безполезны.
	/// Остатокъ — изъ тѣхъ же суммъ, а не вторымъ проходомъ: на каждую τ экспоненты считаются одинъ разъ.
	/// y берётся отъ средняго: суммы малы, и вычитаніе въ остаткѣ не съѣдаетъ точность.</summary>
	public static ExpFit? Exponential(ReadOnlySpan<(double Hours, double Value)> points)
	{
		int n = points.Length;
		if (n < MinPoints) return null;
		double t0 = points[0].Hours, span = points[^1].Hours - t0;
		if (span <= 0) return null;
		double mean = 0, syy = 0;
		foreach ((double _, double y) in points)
			mean += y;
		mean /= n;
		foreach ((double _, double y) in points)
			syy += (y - mean) * (y - mean);
		// шумъ округленія — отъ величины самихъ y: меньшая разница остатковъ — не лучше, а равно, и остаётся прежняя τ.
		// Ложатся точно или ровно — всѣ τ равны, остаётся первая, край перебора: Bounded — по такому ряду τ не узнать
		double noise = 1e-12 * (syy + n * mean * mean);
		ExpFit? best = null;
		double bestError = PositiveInfinity;
		for (int i = 0; i <= TauSteps; i++)
		{
			double tau = MinTau * Pow(MaxTau / MinTau, (double)i / TauSteps), rate = -1 / tau;
			double sx = 0, sxx = 0, sxy = 0; // y — отъ средняго, поэтому Σy = 0
			foreach ((double hours, double y) in points)
			{
				double x = Exp((hours - t0) * rate);
				sx += x; sxx += x * x; sxy += x * (y - mean);
			}
			double d = n * sxx - sx * sx;
			if (Abs(d) < 1e-12) continue;
			double b = n * sxy / d, a = mean - b * sx / n;
			double error = Max(0, syy - b * sxy); // Σ(a + b·x − y)² = Σ(y − ȳ)² − b·Σx·(y − ȳ) — изъ нормальныхъ уравненій
			if (error < bestError - noise)
			{
				bestError = error;
				best = new(tau, a, a + b, Sqrt(error / n), n, span, i == 0 || i == TauSteps);
			}
		}
		return best;
	}

	/// <summary>Къ какому поведенію относится состояніе; null — ни къ какому (прогрѣвъ, неисправность, нѣтъ показаній, держитъ цѣль).</summary>
	public static SegmentKind? Classify(Reading s)
	{
		if (s.Humidity is null || s.Temperature is null) return null;
		if (s.Power == false) return Off;
		if (!s.Working) return null;
		if (s.Mode == DryMode) return Drying;
		return s.Target is { } target && s.Humidity > target ? SegmentKind.Drying : null;
	}

	/// <summary>Точки для подгонки — моменты смѣны влажности: въ этотъ мигъ истинная влажность — посерединѣ между старою и новою,
	/// такъ что ошибка квантованія не копится. Абсолютная — по температурѣ въ тотъ же мигъ.</summary>
	public static List<(double Hours, double Value)> WaterPoints(ReadOnlySpan<Sample> samples, int from, int to)
	{
		List<(double, double)> points = [];
		DateTime origin = samples[from].Time;
		for (int i = from + 1; i <= to; i++)
			if (samples[i].State is { Humidity: { } h, Temperature: { } t } && samples[i - 1].State.Humidity is { } before && before != h)
				points.Add(((samples[i].Time - origin).TotalHours, AbsoluteHumidity(t, (h + before) / 2)));
		return points;
	}

	/// <summary>Дѣлитъ запись на отрѣзки одного поведенія (и одного режима) безъ обрывовъ связи и подгоняетъ каждый.</summary>
	public static List<Segment> Analyze(List<Sample> samples)
	{
		List<Segment> segments = [];
		Analyze(AsSpan(samples), 0, segments);
		return segments;
	}

	/// <summary>То же съ мѣста start (тамъ начинается отрѣзокъ) — въ segments. Послѣдній отрѣзокъ кончается съ записью и можетъ ещё продолжиться:
	/// Resume — съ какой строки онъ начался (запись дописалась — разбирать заново только отсюда: всё до него уже не измѣнится),
	/// Open — попалъ ли онъ въ segments (тогда — послѣднимъ).</summary>
	public static (int Resume, bool Open) Analyze(ReadOnlySpan<Sample> samples, int start, List<Segment> segments)
	{
		for (int i = start + 1; i <= samples.Length; i++)
		{
			bool last = i == samples.Length;
			bool end = last
				|| samples[i].Kind is Rec or Stop or Lost
				|| samples[i - 1].Kind is Stop or Lost
				|| Classify(samples[i].State) != Classify(samples[start].State)
				|| samples[i].State.Mode != samples[start].State.Mode;
			if (!end) continue;
			DateTime until = samples[int.Min(i, samples.Length - 1)].Time; // состояніе держится до слѣдующей строки
			bool added = false;
			if (samples[start].Kind is not (Stop or Lost) && Classify(samples[start].State) is { } kind && until - samples[start].Time >= MinSpan
				&& WaterPoints(samples, start, i - 1) is { Count: >= MinPoints } points && Exponential(AsSpan(points)) is { } fit)
			{
				double temperature = 0;
				foreach (Sample sample in samples[start..i])
					temperature += sample.State.Temperature ?? 0;
				segments.Add(new(samples[start].Time, until, kind, samples[start].State.Mode,
					points.Average(p => p.Value), temperature / (i - start), fit));
				added = true;
			}
			if (last) return (start, added);
			start = i;
		}
		return (start, false); // строкъ нѣтъ
	}

	/// <summary>Живая оцѣнка: подгонка по послѣднему непрерывному отрѣзку осушенія не длиннѣе window.
	/// null — данныхъ мало, считать по модели.</summary>
	public static ExpFit? Recent(List<Sample> history, TimeSpan window)
	{
		ReadOnlySpan<Sample> samples = AsSpan(history);
		if (samples.Length < 2 || samples[^1].Kind is Stop or Lost || Classify(samples[^1].State) != Drying) return null;
		Reading now = samples[^1].State;
		int from = samples.Length - 1;
		while (from > 0 && samples[from].Kind == Change && samples[from - 1].Kind is Change or Rec
			&& Classify(samples[from - 1].State) == Drying
			&& samples[from - 1].State.Mode == now.Mode && samples[from - 1].State.Target == now.Target
			&& samples[^1].Time - samples[from - 1].Time <= window)
			from--;
		if (samples[^1].Time - samples[from].Time < MinSpan) return null;
		ExpFit? fit = Exponential(AsSpan(WaterPoints(samples, from, samples.Length - 1)));
		return fit is { Count: > MinPoints } ? fit : null;
	}

	/// <summary>Уточнить модель по отрѣзкамъ. Изъ «выключенъ» — n/k = 1/τ_off и равновѣсіе ρ_eq; изъ «осушаетъ» — (n·V + c)/(k·V) = 1/τ_on.
	/// c берётся изъ паспортной производительности, V — изъ настроекъ; отсюда k, n и источники S = n·V·(ρ_eq − ρ_out).</summary>
	public static RoomModel Calibrate(RoomModel model, IEnumerable<Segment> segments)
	{
		List<Segment> good = [.. segments.Where(s => !s.Fit.Bounded && s.Fit.Tau > 0)];
		List<Segment> off = [.. good.Where(s => s.Kind == Off)];
		List<Segment> on = [.. good.Where(s => s.Kind == Drying)];
		double? invOff = off.Count > 0 ? off.Sum(s => s.Fit.Count / s.Fit.Tau) / off.Sum(s => s.Fit.Count) : null;
		double? invOn = on.Count > 0 ? on.Sum(s => s.Fit.Count / s.Fit.Tau) / on.Sum(s => s.Fit.Count) : null;
		double? equilibrium = off.Count > 0 ? off.Sum(s => s.Fit.Count * s.Fit.Limit) / off.Sum(s => s.Fit.Count) : null;

		double k = model.Buffer, n = model.Exchange, v = model.Volume, c = model.Flow;
		if (invOn is { } fast)
		{
			if (invOff is { } slow && fast > slow)
			{
				k = c / (v * (fast - slow));
				n = k * slow;
			}
			else k = (n * v + c) / (fast * v);
		}
		else if (invOff is { } slow)
			n = k * slow;
		k = Max(k, 0.1);
		double sources = equilibrium is { } eq && n > 0 ? n * v * (eq - model.OutdoorWater) : model.Sources;
		return model with { Buffer = k, Exchange = n, Sources = sources };
	}
}

using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Text;

namespace DehumidifierControl;

using static Properties.Resources;

using static Math;
using static Color;
using static SystemColors;
using static DashStyle;
using static HatchStyle;
using static ControlStyles;
using static TextFormatFlags;
using static TextRenderer;
using static SystemInformation;
using static Dehumidifier;
using static Values;
using static ComfortScale;
using static Psychrometrics;
using static SampleKind;
using static MainForm;
using static CollectionsMarshal;

/// <summary>Графики записи съ общею осью времени: вверху влажность и цѣль на полосахъ комфорта, ниже — температура и вода въ воздухѣ,
/// внизу — лента состоянія (выключенъ, режимъ, прогрѣвъ, неисправность). Значеніе держится до слѣдующей строки — линіи ступенчатыя;
/// послѣ lost до слѣдующей строки — пунктиромъ, вырѣзанное (stop…rec) — пусто.
/// Долгіе промежутки безъ показаній сжаты на оси времени въ узкія заштрихованныя полосы.
/// Колесо — масштабъ, перетаскиваніе — сдвигъ, Shift+перетаскиваніе — выдѣленіе, двойной щелчокъ — всё.
/// Графики рисуются въ картинку и перерисовываются, только когда мѣняются строки, видъ или размѣръ;
/// курсоръ, выдѣленіе и рамка фокуса — поверхъ картинки: мышь водятъ — графики заново не рисуются.</summary>
public sealed class ChartControl : Control
{
	List<Sample> Samples = [];
	double?[] Waters = []; // вода въ воздухѣ по строкамъ — разъ на новыя строки, а не экспонента на каждую перерисовку
	(double Min, double Max) HumidityRange, TemperatureRange, WaterRange; // предѣлы осей по всѣмъ строкамъ — тоже разъ
	DateTime? LiveEnd; // запись идётъ — послѣднее значеніе тянется до этого времени
	DateTime First, Last;
	double ViewFrom, ViewTo; // видимый кусокъ оси: секунды отъ First, сжатые промежутки — по GapWidth
	Point? Mouse;
	Point DragStart;
	double DragFrom, DragTo;
	DateTime SelectStart;
	bool Dragging, Selecting;

	/// <summary>Сжатый промежутокъ безъ показаній: настоящія начало и конецъ и гдѣ онъ начинается на оси.</summary>
	readonly record struct Gap(DateTime From, DateTime To, double At);
	List<Gap> Gaps = [];
	double GapWidth; // сколько секундъ оси занимаетъ каждый сжатый промежутокъ

	double AxisEnd => Axis(Last);

	// нарисованные графики — безъ курсора, выдѣленія и рамки фокуса. Буферъ GDI, какъ у двойной буферизаціи WinForms, а не Bitmap:
	// на картинкѣ изъ Graphics.FromImage ClearType у TextRenderer мѣшается не съ тѣмъ фономъ и даётъ тёмную кайму
	readonly BufferedGraphicsContext PictureContext = new(); // свой: общій BufferedGraphicsManager.Current занятъ самою перерисовкою
	BufferedGraphics? Picture;
	Size PictureSize;
	bool PictureStale = true; // строки, видъ, шрифтъ или цвѣта смѣнились — нарисовать заново
	Font? SmallFont;          // подписи — одинъ шрифтъ, пока не смѣнился шрифтъ или DPI
	Rectangle? PlotArea;      // поле графиковъ — пока не смѣнился размѣръ или DPI

	/// <summary>Выдѣленный отрѣзокъ (Shift+перетаскиваніе); null — ничего не выдѣлено.</summary>
	[Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public (DateTime From, DateTime To)? Selection { get; private set; }

	public event EventHandler? SelectionChanged;

	public ChartControl()
	{
		SetStyle(OptimizedDoubleBuffer | AllPaintingInWmPaint | UserPaint | ControlStyles.ResizeRedraw | Selectable, true);
		TabStop = true;
		AccessibleRole = AccessibleRole.Chart;
	}

	/// <summary>Показать записи. keepView — не сбрасывать масштабъ (запись идётъ, файлъ дописался).</summary>
	public void SetSamples(List<Sample> samples, DateTime? liveEnd, bool keepView = false)
	{
		Samples = samples;
		LiveEnd = liveEnd;
		Measure();
		if (samples.Count > 0)
		{
			bool atEnd = ViewTo >= AxisEnd; // смотрѣли на конецъ — и дальше идёмъ за нимъ
			double span = ViewTo - ViewFrom;
			DateTime from = TimeAt(ViewFrom), to = TimeAt(ViewTo); // видимое — по настоящему времени: сжатіе сейчасъ перестроится
			bool fromStart = from <= First;
			First = samples[0].Time;
			Last = liveEnd is { } live && live > samples[^1].Time ? live : samples[^1].Time;
			if (Last <= First) Last = First.AddMinutes(1);
			BuildGaps();
			if (!keepView)
			{
				Selection = null;
				ViewFrom = 0;
				ViewTo = AxisEnd;
			}
			else if (atEnd)
			{
				ViewTo = AxisEnd;
				ViewFrom = fromStart ? Axis(from) : ViewTo - span;
			}
			else
			{
				ViewFrom = Axis(from);
				ViewTo = Axis(to);
			}
		}
		else
		{
			Selection = null;
			Gaps = [];
		}
		Redraw();
	}

	/// <summary>Вода по строкамъ и предѣлы осей — разъ на новыя строки, безъ LINQ и упаковки каждаго числа.</summary>
	void Measure()
	{
		Waters = new double?[Samples.Count];
		Extent humidity = new(), temperature = new(), water = new();
		for (int i = 0; i < Samples.Count; i++)
		{
			Reading s = Samples[i].State;
			humidity.Add(s.Humidity);
			if (s.Mode != LogFormat.DryMode) humidity.Add(s.Target);
			temperature.Add(s.Temperature);
			water.Add(Waters[i] = s.Water);
		}
		HumidityRange    = Range(humidity,    10, 20, 0, 100);
		TemperatureRange = Range(temperature,  1,  4);
		WaterRange       = Range(water,        1,  4);
	}

	/// <summary>Наименьшее и наибольшее изъ значеній.</summary>
	struct Extent()
	{
		public double Low = double.MaxValue, High = double.MinValue;

		public void Add(double? value)
		{
			if (value is not { } v) return;
			Low = Min(Low, v);
			High = Max(High, v);
		}
	}

	/// <summary>Показать отрѣзокъ [from; to] какъ выбранъ — и тамъ, гдѣ записей нѣтъ; «показать всё» вернётся къ нему же.</summary>
	public void ShowRange(DateTime from, DateTime to)
	{
		if (to <= from) return;
		if (from < First) First = from;
		if (to > Last) Last = to;
		BuildGaps();
		ViewFrom = Axis(from);
		ViewTo = Axis(to);
		Redraw();
	}

	public void ShowAll()
	{
		ViewFrom = 0;
		ViewTo = AxisEnd;
		Redraw();
	}

	/// <summary>Графики — заново: смѣнились строки или видъ. Для курсора и выдѣленія хватаетъ Invalidate — картинка та же.</summary>
	void Redraw()
	{
		PictureStale = true;
		Invalidate();
	}

	public void ClearSelection()
	{
		if (Selection is null) return;
		Selection = null;
		SelectionChanged?.Invoke(this, EventArgs.Empty);
		Invalidate();
	}

	// ───── раскладка ─────

	int Px(int pixels) => pixels * DeviceDpi / 96;

	Rectangle Plot => PlotArea ??= Rectangle.FromLTRB(Px(48), Px(8), ClientSize.Width - Px(52), ClientSize.Height - Px(22) - Px(18) - Px(6));

	(Rectangle Humidity, Rectangle Temperature, Rectangle Band) Panes()
	{
		Rectangle plot = Plot;
		int gap = Px(10), band = Px(18);
		int free = Max(0, plot.Height - gap);
		int top = free * 55 / 100;
		Rectangle humidity = new(plot.Left, plot.Top, plot.Width, top);
		Rectangle temperature = new(plot.Left, humidity.Bottom + gap, plot.Width, free - top);
		Rectangle strip = new(plot.Left, temperature.Bottom + Px(6), plot.Width, band);
		return (humidity, temperature, strip);
	}

	float X(DateTime time)
	{
		Rectangle plot = Plot;
		return plot.Left + (float)((Axis(time) - ViewFrom) / Max(1, ViewTo - ViewFrom) * plot.Width);
	}

	/// <summary>Мѣсто на оси подъ точкою x.</summary>
	double AxisAt(int x)
	{
		Rectangle plot = Plot;
		return ViewFrom + (x - plot.Left) * (ViewTo - ViewFrom) / Max(1, plot.Width);
	}

	DateTime Time(int x) => TimeAt(AxisAt(x));

	// ───── сжатіе промежутковъ ─────

	/// <summary>Короче этого промежутокъ не сжимается.</summary>
	static readonly TimeSpan MinGap = TimeSpan.FromMinutes(5);

	/// <summary>Промежутки безъ показаній — послѣ stop и lost, до первой строки и послѣ конца записи — сжимаются до GapWidth:
	/// каждый не шире 2 % записаннаго времени, всѣ вмѣстѣ — не шире его четверти. Не длиннѣе двухъ GapWidth — остаются какъ есть.</summary>
	void BuildGaps()
	{
		Gaps = [];
		if (Samples.Count == 0) return; // ShowRange бываетъ и на періодѣ безъ записей
		List<(DateTime From, DateTime To)> empty = [];
		void Add(DateTime from, DateTime to) { if (to - from >= MinGap) empty.Add((from, to)); }
		Add(First, Samples[0].Time);
		for (int i = 0; i + 1 < Samples.Count; i++)
			if (Samples[i].Kind is Stop or Lost) Add(Samples[i].Time, Samples[i + 1].Time);
		Add(EndOf(Samples.Count - 1), Last);

		double recorded = (Last - First).TotalSeconds - empty.Sum(e => (e.To - e.From).TotalSeconds);
		GapWidth = Max(30, Min(recorded / 50, recorded / 4 / Max(1, empty.Count)));
		double cut = 0; // на сколько секундъ ось уже короче настоящаго времени
		foreach ((DateTime from, DateTime to) in empty)
		{
			double length = (to - from).TotalSeconds;
			if (length <= GapWidth * 2) continue;
			Gaps.Add(new(from, to, (from - First).TotalSeconds - cut));
			cut += length - GapWidth;
		}
	}

	/// <summary>Послѣдній сжатый промежутокъ, начавшійся раньше time; −1 — такого нѣтъ.</summary>
	int GapBefore(DateTime time)
	{
		int low = 0, high = Gaps.Count - 1, found = -1;
		while (low <= high)
		{
			int middle = (low + high) / 2;
			if (Gaps[middle].From < time)
			{
				found = middle;
				low = middle + 1;
			}
			else high = middle - 1;
		}
		return found;
	}

	/// <summary>То же по мѣсту на оси.</summary>
	int GapBefore(double axis)
	{
		int low = 0, high = Gaps.Count - 1, found = -1;
		while (low <= high)
		{
			int middle = (low + high) / 2;
			if (Gaps[middle].At < axis)
			{
				found = middle;
				low = middle + 1;
			}
			else high = middle - 1;
		}
		return found;
	}

	/// <summary>Время → мѣсто на оси: въ сжатомъ промежуткѣ время идётъ быстрѣе, внѣ ихъ — какъ есть.</summary>
	double Axis(DateTime time)
	{
		int i = GapBefore(time);
		if (i < 0) return (time - First).TotalSeconds;
		Gap gap = Gaps[i];
		return time < gap.To ? gap.At + (time - gap.From) / (gap.To - gap.From) * GapWidth
		/**/                 : gap.At + GapWidth + (time - gap.To).TotalSeconds;
	}

	/// <summary>Мѣсто на оси → время; обратное Axis.</summary>
	DateTime TimeAt(double axis)
	{
		int i = GapBefore(axis);
		if (i < 0) return First + TimeSpan.FromSeconds(axis);
		Gap gap = Gaps[i];
		return axis < gap.At + GapWidth ? gap.From + (gap.To - gap.From) * ((axis - gap.At) / GapWidth)
		/**/                            : gap.To + TimeSpan.FromSeconds(axis - gap.At - GapWidth);
	}

	/// <summary>Сжатый промежутокъ въ этомъ мѣстѣ оси; null — тамъ записанное время.</summary>
	Gap? GapAt(double axis) => GapBefore(axis) is >= 0 and int i && axis < Gaps[i].At + GapWidth ? Gaps[i] : null;

	static float Y(Rectangle pane, double value, (double Min, double Max) range) => (float)(pane.Bottom - (value - range.Min) / (range.Max - range.Min) * pane.Height);

	/// <summary>Предѣлы оси: отъ наименьшаго до наибольшаго значенія, по шагу step, не меньше minSpan.</summary>
	static (double Min, double Max) Range(Extent values, double step, double minSpan, double floor = double.MinValue, double ceiling = double.MaxValue)
	{
		double min = values.Low, max = values.High;
		if (min > max) return (0, minSpan);
		min = Floor(min / step) * step;
		max = Ceiling(max / step) * step;
		if (max - min < minSpan)
		{
			double middle = (min + max) / 2;
			min = Floor((middle - minSpan / 2) / step) * step;
			max = min + Ceiling(minSpan / step) * step;
		}
		return (Max(min, floor), Min(max, ceiling));
	}

	/// <summary>Конецъ строки i: время слѣдующей строки; у послѣдней — сейчасъ, если запись идётъ, иначе ея же время.</summary>
	DateTime EndOf(int i) => i + 1 < Samples.Count ? Samples[i + 1].Time : LiveEnd is { } live && live > Samples[i].Time ? live : Samples[i].Time;

	// ───── рисованіе ─────

	static Color HumidityLine => Accent(FromArgb(0, 102, 204));
	static Color TargetLine => Accent(DarkOrange);
	static Color TemperatureLine => Accent(Firebrick);
	static Color WaterLine => Accent(Teal);

	Font Small => SmallFont ??= new(Font.FontFamily, Font.Size * 0.85f);

	/// <summary>Картинка графиковъ — изъ готовой, если ничего не смѣнилось; поверхъ — выдѣленіе, курсоръ и рамка фокуса.</summary>
	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics;
		Size size = ClientSize;
		if (size.Width <= 0 || size.Height <= 0) return;
		if (Picture is null || PictureSize != size)
		{
			Picture?.Dispose();
			PictureContext.MaximumBuffer = size; // буферъ — свой и одинъ, пока размѣръ тотъ же
			Picture = PictureContext.Allocate(g, new(Point.Empty, size));
			PictureSize = size;
			PictureStale = true;
		}
		if (PictureStale)
		{
			DrawCharts(Picture.Graphics);
			PictureStale = false;
		}
		Picture.Render(g); // BitBlt, какъ есть
		if (Samples.Count == 0) return;
		(Rectangle humidityPane, Rectangle temperaturePane, Rectangle band) = Panes();
		if (humidityPane.Width <= 0 || humidityPane.Height <= 0 || temperaturePane.Height <= 0) return;

		if (Selection is { } selection)
		{
			float x1 = Max(X(selection.From), humidityPane.Left), x2 = Min(X(selection.To), humidityPane.Right);
			if (x2 > x1)
			{
				using SolidBrush brush = new(FromArgb(70, Highlight));
				g.FillRectangle(brush, x1, humidityPane.Top, x2 - x1, band.Bottom - humidityPane.Top);
			}
		}
		if (Mouse is { } mouse && mouse.X >= humidityPane.Left && mouse.X <= humidityPane.Right)
			DrawCursor(g, mouse, humidityPane, band, Small);
		if (Focused)
			ControlPaint.DrawFocusRectangle(g, ClientRectangle);
	}

	/// <summary>Графики безъ того, что поверхъ: полосы комфорта, сѣтка, оси, ряды, лента состоянія, рамки, легенды.</summary>
	void DrawCharts(Graphics g)
	{
		g.Clear(Window);
		if (Samples.Count == 0)
		{
			DrawText(g, ChartEmpty, Font, ClientRectangle, GrayText, HorizontalCenter | VerticalCenter | WordBreak);
			return;
		}
		(Rectangle humidityPane, Rectangle temperaturePane, Rectangle band) = Panes();
		if (humidityPane.Width <= 0 || humidityPane.Height <= 0 || temperaturePane.Height <= 0) return;

		Font small = Small;
		DrawComfort(g, humidityPane, HumidityRange);
		DrawTimeGrid(g, humidityPane, temperaturePane, band, small);
		DrawGaps(g, humidityPane, temperaturePane);
		DrawAxis(g, humidityPane, HumidityRange, 10, "0", left: true, small, HumidityLine);
		DrawAxis(g, temperaturePane, TemperatureRange, Step(TemperatureRange, temperaturePane.Height, small), "0.#", left: true, small, TemperatureLine);
		DrawAxis(g, temperaturePane, WaterRange, Step(WaterRange, temperaturePane.Height, small), "0.#", left: false, small, WaterLine);

		DrawSeries(g, humidityPane, HumidityRange, i => Samples[i].State is { Mode: not LogFormat.DryMode, Target: { } t } ? t : null, TargetLine, 2);
		DrawSeries(g, humidityPane, HumidityRange, i => Samples[i].State.Humidity, HumidityLine, 2);
		DrawSeries(g, temperaturePane, TemperatureRange, i => Samples[i].State.Temperature, TemperatureLine, 2);
		DrawSeries(g, temperaturePane, WaterRange, i => Waters[i], WaterLine, 2);
		DrawBand(g, band);
		DrawGaps(g, band);

		using (Pen frame = new(ControlDark))
		{
			g.DrawRectangle(frame, humidityPane);
			g.DrawRectangle(frame, temperaturePane);
			g.DrawRectangle(frame, band);
		}
		DrawLegend(g, humidityPane, small, (ChartHumidity, HumidityLine), (ChartTarget, TargetLine));
		DrawLegend(g, temperaturePane, small, (ChartTemperature, TemperatureLine), (ChartWater, WaterLine));
	}

	/// <summary>Шагъ подписей оси, чтобы онѣ не налѣзали другъ на друга.</summary>
	static double Step((double Min, double Max) range, int height, Font font)
	{
		double span = range.Max - range.Min;
		int fit = Max(1, height / (font.Height * 2));
		foreach (double step in AxisSteps)
			if (span / step <= fit) return step;
		return 100;
	}

	static readonly double[] AxisSteps = [0.5, 1, 2, 5, 10, 20, 50];

	static readonly double[] ComfortBounds = [0, 30, 40, 50.5, 60.5, 70.5, 100];

	/// <summary>Полосы комфорта подъ влажностью — блёдно, цвѣтами ступеней; въ высокой контрастности не рисуются.</summary>
	static void DrawComfort(Graphics g, Rectangle pane, (double Min, double Max) range)
	{
		if (HighContrast) return;
		double[] bounds = ComfortBounds;
		for (int i = 0; i + 1 < bounds.Length; i++)
		{
			double low = Max(bounds[i], range.Min), high = Min(bounds[i + 1], range.Max);
			if (high <= low) continue;
			using SolidBrush brush = new(FromArgb(28, ComfortColor(HumidityComfort((byte)Ceiling(bounds[i] + 0.1)))));
			float top = Y(pane, high, range), bottom = Y(pane, low, range);
			g.FillRectangle(brush, pane.Left, top, pane.Width, bottom - top);
		}
	}

	static readonly TimeSpan[] TimeSteps = [.. new[] { 1, 2, 5, 10, 15, 30, 60, 120, 180, 360, 720, 1440, 2880, 10080 }.Select(m => TimeSpan.FromMinutes(m))];

	/// <summary>Сѣтка и подписи времени — только на записанныхъ кускахъ, между сжатыми промежутками; подпись, налѣзающая на прежнюю, пропускается.</summary>
	void DrawTimeGrid(Graphics g, Rectangle humidity, Rectangle temperature, Rectangle band, Font font)
	{
		DateTime from = TimeAt(ViewFrom), to = TimeAt(ViewTo);
		bool days = (to - from).TotalDays > 1 || from.Date != to.Date;
		string format = days ? "dd.MM HH:mm" : "HH:mm";
		int labelWidth = MeasureText(days ? "00.00 00:00" : "00:00", font).Width + Px(12); // образецъ подписи по format: цифры — самыя широкія
		double perSecond = Plot.Width / Max(1, ViewTo - ViewFrom); // точекъ на секунду внѣ сжатыхъ промежутковъ
		TimeSpan step = TimeSteps.FirstOrDefault(s => s.TotalSeconds * perSecond >= labelWidth, TimeSteps[^1]);
		using Pen grid = new(HighContrast ? GrayText : FromArgb(40, ControlText)) { DashStyle = Dot };
		int free = int.MinValue; // съ этой точки подпись уже не налѣзетъ на прежнюю
		DateTime piece = from; // начало записаннаго куска
		for (int i = Max(0, GapBefore(from)); i <= Gaps.Count && piece < to; i++)
		{
			(DateTime gapFrom, DateTime gapTo) = i < Gaps.Count ? (Gaps[i].From, Gaps[i].To) : (to, to);
			if (piece < gapFrom) Ticks(piece, gapFrom < to ? gapFrom : to);
			if (piece < gapTo) piece = gapTo;
		}

		void Ticks(DateTime start, DateTime end)
		{
			DateTime tick = step >= TimeSpan.FromDays(1) ? start.Date : new(start.Ticks / step.Ticks * step.Ticks); // кратно шагу по мѣстному времени
			for (; tick <= end; tick += step)
			{
				if (tick < start) continue;
				float x = X(tick);
				g.DrawLine(grid, x, humidity.Top, x, humidity.Bottom);
				g.DrawLine(grid, x, temperature.Top, x, temperature.Bottom);
				string text = tick.ToString(format);
				Size size = MeasureText(text, font);
				int left = (int)x - size.Width / 2;
				if (left < free) continue;
				DrawText(g, text, font, new Point(left, band.Bottom + Px(3)), ControlText, NoPadding);
				free = left + size.Width + Px(6);
			}
		}
	}

	/// <summary>Сжатые промежутки — косою штриховкою: время тамъ идётъ не въ масштабѣ.</summary>
	void DrawGaps(Graphics g, params ReadOnlySpan<Rectangle> panes)
	{
		using HatchBrush hatch = new(LightDownwardDiagonal, HighContrast ? GrayText : FromArgb(70, GrayText), Transparent);
		for (int i = Max(0, GapBefore(ViewFrom)); i < Gaps.Count && Gaps[i].At < ViewTo; i++)
		{
			float x1 = X(Gaps[i].From), x2 = X(Gaps[i].To);
			foreach (Rectangle pane in panes)
			{
				float left = Max(x1, pane.Left), right = Min(x2, pane.Right);
				if (right > left) g.FillRectangle(hatch, left, pane.Top, right - left, pane.Height);
			}
		}
	}

	void DrawAxis(Graphics g, Rectangle pane, (double Min, double Max) range, double step, string format, bool left, Font font, Color color)
	{
		using Pen grid = new(HighContrast ? GrayText : FromArgb(30, ControlText));
		for (double v = Ceiling(range.Min / step) * step; v <= range.Max + 1e-9; v += step)
		{
			float y = Y(pane, v, range);
			if (left) g.DrawLine(grid, pane.Left, y, pane.Right, y);
			string text = v.ToString(format);
			Size size = MeasureText(text, font);
			int x = left ? pane.Left - size.Width - Px(4) : pane.Right + Px(4);
			DrawText(g, text, font, new Point(x, (int)y - size.Height / 2), color, NoPadding);
		}
	}

	/// <summary>Ступенчатая линія: горизонталь — пока значеніе держится, вертикаль — смѣна; послѣ lost — пунктиромъ.
	/// Только видимое: съ первой видимой строки (двоичнымъ поискомъ) до первой правѣе края.
	/// Отрѣзками, а не одною ломаною: у ломаной при утолщеніи частыя ступеньки замыкаютъ петли, и тѣ заливаются.</summary>
	void DrawSeries(Graphics g, Rectangle pane, (double Min, double Max) range, Func<int, double?> value, Color color, float width)
	{
		using Pen solid = new(color, width * DeviceDpi / 96);
		using Pen dashed = new(color, width * DeviceDpi / 96) { DashStyle = Dash };
		GraphicsState saved = g.Save();
		g.SetClip(pane);
		float? previousY = null;
		float left = pane.Left - 2, right = pane.Right + 2;
		for (int i = Max(0, IndexAt(TimeAt(ViewFrom))); i < Samples.Count; i++)
		{
			Sample sample = Samples[i];
			float x1 = X(sample.Time);
			if (x1 > right) break; // дальше — всё правѣе
			if (sample.Kind == Stop || value(i) is not { } v)
			{
				previousY = null;
				continue;
			}
			float x2 = X(EndOf(i)), y = Y(pane, v, range);
			if (previousY is { } py && py != y)
				g.DrawLine(solid, x1, py, x1, y);
			if (x2 > x1)
				g.DrawLine(sample.Kind == Lost ? dashed : solid, Max(x1, left), y, Min(x2, right), y);
			previousY = y;
		}
		g.Restore(saved);
	}

	/// <summary>Цвѣтъ ленты состоянія: выключенъ — сѣрый; режимы — свои цвѣта; неисправность — красный.</summary>
	static Color BandColor(Reading s) =>
		s.Fault is > 0 ? Accent(Firebrick)
		: s.Power == false ? HighContrast ? Window : Gainsboro
		: s.Mode switch
		{
			0 => Accent(MediumSeaGreen),
			1 => Accent(SlateBlue),
			2 => Accent(DarkOrange),
			_ => Accent(Silver),
		};

	/// <summary>Лента состоянія: только видимое; подрядъ идущія строки одного вида — однимъ прямоугольникомъ и одною кистью.</summary>
	void DrawBand(Graphics g, Rectangle band)
	{
		GraphicsState saved = g.Save();
		g.SetClip(band);
		(float From, float To, Color Color, bool Lost, bool Warming)? run = null;
		for (int i = Max(0, IndexAt(TimeAt(ViewFrom))); i < Samples.Count; i++)
		{
			Sample sample = Samples[i];
			float x1 = X(sample.Time);
			if (x1 > band.Right) break; // дальше — всё правѣе
			if (sample.Kind == Stop) // вырѣзанное — пусто
			{
				Fill();
				continue;
			}
			x1 = Max(x1, band.Left);
			float x2 = Min(X(EndOf(i)), band.Right);
			if (x2 <= x1) continue;
			(Color color, bool lost, bool warming) = (BandColor(sample.State), sample.Kind == Lost, sample.State.Warming == true);
			if (run is { } r && (r.Color, r.Lost, r.Warming) == (color, lost, warming))
				run = r with { To = x2 };
			else
			{
				Fill();
				run = (x1, x2, color, lost, warming);
			}
		}
		Fill();
		g.Restore(saved);

		void Fill()
		{
			if (run is not { } r) return;
			run = null;
			RectangleF rect = new(r.From, band.Top, r.To - r.From, band.Height);
			if (r.Lost)
			{
				using HatchBrush lost = new(Percent50, r.Color, Window);
				g.FillRectangle(lost, rect);
			}
			else
			{
				using SolidBrush brush = new(r.Color);
				g.FillRectangle(brush, rect);
			}
			if (r.Warming)
			{
				using HatchBrush warming = new(WideUpwardDiagonal, FromArgb(160, Window), Transparent);
				g.FillRectangle(warming, rect);
			}
		}
	}

	void DrawLegend(Graphics g, Rectangle pane, Font font, params ReadOnlySpan<(string Text, Color Color)> items)
	{
		int x = pane.Left + Px(6), y = pane.Top + Px(3);
		using SolidBrush back = new(FromArgb(200, Window));
		foreach ((string text, Color color) in items)
		{
			Size size = MeasureText(text, font);
			g.FillRectangle(back, x - 2, y, size.Width + 4, size.Height);
			DrawText(g, text, font, new Point(x, y), color, NoPadding);
			x += size.Width + Px(12);
		}
	}

	/// <summary>Вертикаль подъ мышью и подсказка: время и всё, что было въ этотъ мигъ.</summary>
	void DrawCursor(Graphics g, Point mouse, Rectangle top, Rectangle band, Font font)
	{
		using Pen pen = new(GrayText) { DashPattern = [1, 3] };
		g.DrawLine(pen, mouse.X, top.Top, mouse.X, band.Bottom);
		// горизонталь — по графику подъ мышью, точками рѣже вертикали (точка и три пропуска): по ней сравниваютъ уровни, а вертикаль — главная
		(Rectangle humidity, Rectangle temperature, _) = Panes();
		Rectangle? under = humidity.Contains(mouse) ? humidity : temperature.Contains(mouse) ? temperature : null;
		if (under is { } pane)
		{
			using Pen sparse = new(GrayText) { DashPattern = [1, 10] };
			g.DrawLine(sparse, pane.Left, mouse.Y, pane.Right, mouse.Y);
		}
		DateTime time = Time(mouse.X);
		int i = IndexAt(time);
		StringBuilder lines = new(); // Append съ $"…" пишетъ прямо въ буферъ, безъ промежуточныхъ строкъ
		if (GapAt(AxisAt(mouse.X)) is { } gap) lines.Append($"{gap.From:dd.MM HH:mm} — {gap.To:dd.MM HH:mm}"); // время въ сжатомъ — не въ масштабѣ: весь промежутокъ
		else lines.Append($"{time:dd.MM HH:mm:ss}");
		if (i < 0 || Samples[i].Kind == Stop || time > EndOf(i))
			lines.Append('\n').Append(ChartNoData);
		else
		{
			Reading s = Samples[i].State;
			if (Samples[i].Kind == Lost) lines.Append('\n').Append(ChartLost);
			lines.Append('\n'); AppendState(lines, s);
			if (s.Humidity is { } h)
			{
				lines.Append($"\n{ChartHumidity}: {h:0.#}");
				if (s.Target is { } t && s.Mode != LogFormat.DryMode) lines.Append($" → {t}");
			}
			if (s.Temperature is { } c) lines.Append($"\n{ChartTemperature}: {c:0.#}");
			if (s.Water is { } w && s.Temperature is { } tc) lines.Append($"\n{ChartWater}: {w:0.00}; {ChartDewPoint}: {DewPoint(tc, s.Humidity ?? 0):0.#} °");
		}
		string text = lines.ToString();
		Size size = MeasureText(text, font);
		int x = mouse.X + Px(12);
		if (x + size.Width + Px(8) > ClientSize.Width) x = mouse.X - Px(12) - size.Width - Px(8);
		Rectangle box = new(x, Max(top.Top + Px(20), mouse.Y - size.Height - Px(8)), size.Width + Px(8), size.Height + Px(6));
		g.FillRectangle(SystemBrushes.Info, box);
		g.DrawRectangle(SystemPens.InfoText, box);
		DrawText(g, text, font, new Point(box.Left + Px(4), box.Top + Px(3)), InfoText, NoPadding);
	}

	/// <summary>Послѣдняя строка не позже time; −1 — до начала.</summary>
	int IndexAt(DateTime time) => RecordFiles.CountUpTo(AsSpan(Samples), time) - 1;

	// ───── мышь и клавиши ─────

	const double MinView = 2 * 60; // секундъ оси

	/// <summary>Показать кусокъ оси [from; to] (секунды отъ First, промежутки сжаты).</summary>
	void SetView(double from, double to)
	{
		double span = to - from;
		double whole = AxisEnd;
		if (span < MinView) span = MinView;
		double max = whole * 1.5 + MinView;
		if (span > max) span = max;
		double middle = (from + to) / 2;
		from = middle - span / 2;
		double lowest = -whole * 0.25, highest = whole * 1.25; // не уходить далеко за края
		if (from < lowest) from = lowest;
		if (from + span > highest) from = highest - span;
		ViewFrom = from;
		ViewTo = from + span;
		Redraw();
	}

	void Zoom(double factor, double around)
	{
		double left = (around - ViewFrom) * factor, right = (ViewTo - around) * factor;
		SetView(around - left, around + right);
	}

	protected override void OnMouseWheel(MouseEventArgs e)
	{
		base.OnMouseWheel(e);
		if (Samples.Count == 0) return;
		Zoom(Pow(1.25, -e.Delta / 120.0), AxisAt(e.X));
	}

	protected override void OnMouseDown(MouseEventArgs e)
	{
		base.OnMouseDown(e);
		Focus();
		if (e.Button != MouseButtons.Left || Samples.Count == 0) return;
		DragStart = e.Location;
		if ((ModifierKeys & Keys.Shift) != 0)
		{
			Selecting = true;
			SelectStart = Clamp(Time(e.X));
			Selection = null;
		}
		else
		{
			Dragging = true;
			DragFrom = ViewFrom;
			DragTo = ViewTo;
			Cursor = Cursors.SizeWE;
		}
		Capture = true;
	}

	DateTime Clamp(DateTime time) => time < First ? First : time > Last ? Last : time;

	protected override void OnMouseMove(MouseEventArgs e)
	{
		base.OnMouseMove(e);
		Mouse = e.Location;
		if (Dragging)
		{
			double shift = (DragStart.X - e.X) * (DragTo - DragFrom) / Max(1, Plot.Width);
			SetView(DragFrom + shift, DragTo + shift);
		}
		else if (Selecting)
		{
			DateTime now = Clamp(Time(e.X));
			Selection = now < SelectStart ? (now, SelectStart) : (SelectStart, now);
		}
		Invalidate();
	}

	protected override void OnMouseUp(MouseEventArgs e)
	{
		base.OnMouseUp(e);
		if (Selecting && Abs(e.X - DragStart.X) < DragSize.Width)
			Selection = null; // щелчокъ съ Shift безъ протягиванія — снять выдѣленіе
		if (Selecting)
			SelectionChanged?.Invoke(this, EventArgs.Empty);
		Selecting = Dragging = false;
		Capture = false;
		Cursor = Cursors.Default;
		Invalidate();
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		base.OnMouseLeave(e);
		Mouse = null;
		Invalidate();
	}

	protected override void OnDoubleClick(EventArgs e)
	{
		base.OnDoubleClick(e);
		ShowAll();
	}

	protected override bool IsInputKey(Keys keyData) => keyData is Keys.Left or Keys.Right || base.IsInputKey(keyData);

	protected override void OnKeyDown(KeyEventArgs e)
	{
		base.OnKeyDown(e);
		if (Samples.Count == 0) return;
		double span = ViewTo - ViewFrom;
		double middle = ViewFrom + span / 2;
		switch (e.KeyCode)
		{
			case Keys.Left: SetView(ViewFrom - span / 10, ViewTo - span / 10); break;
			case Keys.Right: SetView(ViewFrom + span / 10, ViewTo + span / 10); break;
			case Keys.Add or Keys.Oemplus: Zoom(0.8, middle); break;
			case Keys.Subtract or Keys.OemMinus: Zoom(1.25, middle); break;
			case Keys.Home: ShowAll(); break;
			case Keys.Escape: ClearSelection(); break;
			default: return;
		}
		e.Handled = true;
	}

	protected override void OnGotFocus(EventArgs e)
	{
		base.OnGotFocus(e);
		Invalidate();
	}

	protected override void OnLostFocus(EventArgs e)
	{
		base.OnLostFocus(e);
		Invalidate();
	}

	// ───── что сбрасываетъ готовое ─────

	protected override void OnResize(EventArgs e)
	{
		PlotArea = null; // картинку другого размѣра OnPaint и такъ нарисуетъ заново
		base.OnResize(e);
	}

	protected override void OnDpiChangedAfterParent(EventArgs e)
	{
		PlotArea = null;
		base.OnDpiChangedAfterParent(e);
		Redraw();
	}

	protected override void OnFontChanged(EventArgs e)
	{
		SmallFont?.Dispose();
		SmallFont = null;
		base.OnFontChanged(e);
		Redraw();
	}

	/// <summary>Смѣнились системные цвѣта или высокая контрастность.</summary>
	protected override void OnSystemColorsChanged(EventArgs e)
	{
		base.OnSystemColorsChanged(e);
		Redraw();
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			Picture?.Dispose();
			PictureContext.Dispose();
			SmallFont?.Dispose();
		}
		base.Dispose(disposing);
	}
}

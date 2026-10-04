namespace DehumidifierControl;

using Properties;

using static Properties.Resources;

using static String;
using static Double;
using static TimeSpan;
using static Screen;
using static Environment;
using static MessageBoxIcon;
using static MessageBoxButtons;
using static FormStartPosition;
using static FormWindowState;
using static SegmentKind;
using static Psychrometrics;
using static RecordFiles;
using static MoistureFit;
using static RoomModel;
using static LogFormat;
using static Values;

/// <summary>Помощникъ разсчётовъ: воздухъ (температура и влажность ⇄ абсолютная влажность, точка росы),
/// комната (сколько воды убрать до цѣли) и прогнозъ по модели RoomModel (когда дойдётъ до цѣли, предѣлъ, нужная производительность).</summary>
public partial class CalculatorForm : Form
{
	/// <summary>Что сейчасъ показываетъ осушитель (или null, если не подключены).</summary>
	readonly Func<Reading?> Current;

	bool Updating;

	public CalculatorForm(Func<Reading?> current)
	{
		InitializeComponent();
		Current = current;
		Link(numericTemperature,        trackTemperature,        -10,  40, 0.5m);
		Link(numericHumidity,           trackHumidity,             0, 100, 1);
		Link(numericWater,              trackWater,                0,  30, 0.1m);
		Link(numericOtherTemperature,   trackOtherTemperature,   -10,  40, 0.5m);
		Link(numericVolume,             trackVolume,               5, 200, 1);
		Link(numericBuffer,             trackBuffer,               1,  10, 0.1m);
		Link(numericTarget,             trackTarget,               0, 100, 1);
		Link(numericExchange,           trackExchange,             0,   3, 0.05m);
		Link(numericOutdoorTemperature, trackOutdoorTemperature, -30,  40, 0.5m);
		Link(numericOutdoorHumidity,    trackOutdoorHumidity,      0, 100, 1);
		Link(numericSources,            trackSources,           -200, 500, 10);
		Link(numericRated,              trackRated,                5,  50, 0.5m);
	}

	/// <summary>Esc — закрыть: окно вспомогательное, положеніе сохранится какъ при обычномъ закрытіи.</summary>
	protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
	{
		if (keyData == Keys.Escape)
		{
			Close();
			return true;
		}
		return base.ProcessCmdKey(ref msg, keyData);
	}

	/// <summary>Ползунокъ при полѣ: двигаешь его — мѣняется поле, и пересчётъ идётъ обработчикомъ поля; мѣняется поле — ползунокъ за нимъ.
	/// Ходъ ползунка — обычный для величины діапазонъ [min; max] съ шагомъ step; въ полѣ можно ввести и больше — тогда ползунокъ у края.</summary>
	void Link(NumericUpDown box, TrackBar bar, decimal min, decimal max, decimal step)
	{
		bar.Maximum = (int)((max - min) / step);
		bar.LargeChange = int.Max(1, bar.Maximum / 10);
		toolTip.SetToolTip(bar, toolTip.GetToolTip(box));
		bar.Scroll += (_, _) => box.Value = decimal.Clamp(min + bar.Value * step, box.Minimum, box.Maximum);
		box.ValueChanged += (_, _) => Follow();
		Follow(); // сразу: если OnLoad поставитъ то же значеніе, что уже стоитъ въ полѣ, ValueChanged не придётъ
		void Follow() => bar.Value = (int)decimal.Clamp(decimal.Round((box.Value - min) / step), 0, bar.Maximum); // Value не вызываетъ Scroll — круга нѣтъ
	}

	protected override void OnLoad(EventArgs e)
	{
		base.OnLoad(e);
		if (DesignMode) return;
		Point location = Settings.Default.CalculatorLocation;
		if (location.X > -1000000 && AllScreens.Any(s => s.WorkingArea.Contains(location)))
		{
			StartPosition = Manual;
			Location = location;
		}
		Updating = true;
		Set(numericTemperature, 22); // пока не подключены — обычная комната
		Set(numericHumidity, 60);
		Set(numericTarget, 50);
		Set(numericOtherTemperature, 18);
		Set(numericVolume            , Settings.Default.RoomVolume);
		Set(numericBuffer            , Settings.Default.BufferFactor);
		Set(numericExchange          , Settings.Default.AirExchange);
		Set(numericOutdoorTemperature, Settings.Default.OutdoorTemperature);
		Set(numericOutdoorHumidity   , Settings.Default.OutdoorHumidity);
		Set(numericSources           , Settings.Default.MoistureSources);
		Set(numericRated             , Settings.Default.RatedCapacity);
		Updating = false;
		if (!FromDevice())
			Recalculate();
	}

	protected override void OnFormClosing(FormClosingEventArgs e)
	{
		Settings.Default.RoomVolume         = (double)numericVolume.Value;
		Settings.Default.BufferFactor       = (double)numericBuffer.Value;
		Settings.Default.AirExchange        = (double)numericExchange.Value;
		Settings.Default.OutdoorTemperature = (double)numericOutdoorTemperature.Value;
		Settings.Default.OutdoorHumidity    = (double)numericOutdoorHumidity.Value;
		Settings.Default.MoistureSources    = (double)numericSources.Value;
		Settings.Default.RatedCapacity      = (double)numericRated.Value;
		Settings.Default.CalculatorLocation = WindowState == Normal ? Location : RestoreBounds.Location;
		Settings.Default.Save();
		base.OnFormClosing(e);
	}

	/// <summary>Значеніе въ поле, прижатое къ его предѣламъ.</summary>
	static void Set(NumericUpDown box, double value)
	{
		if (IsFinite(value))
			box.Value = decimal.Clamp((decimal)Round(value, box.DecimalPlaces), box.Minimum, box.Maximum);
	}

	static double Value(NumericUpDown box) => (double)box.Value;

	/// <summary>Температура, влажность и цѣль — изъ осушителя; false — не подключены.</summary>
	bool FromDevice()
	{
		if (Current() is not { Temperature: { } t, Humidity: { } h } now)
			return false;
		Updating = true;
		Set(numericTemperature, t);
		Set(numericHumidity, h);
		if (now.Target is { } target && now.Mode != DryMode)
			Set(numericTarget, target);
		Updating = false;
		Recalculate();
		return true;
	}

	void FromDevice_Click(object? sender, EventArgs e) => FromDevice();

	void Input_ValueChanged(object? sender, EventArgs e)
	{
		if (!Updating) Recalculate();
	}

	/// <summary>Абсолютную влажность ввёлъ человѣкъ (въ полѣ или ползункомъ) — пересчитать относительную при той же температурѣ.
	/// Больше насыщенія воздухъ не удержитъ: относительная упрётся въ 100 %, а вода станетъ равной насыщенію.
	/// Фокусъ не провѣряемъ: набранное число поле принимаетъ, когда фокусъ уже ушёлъ; свои измѣненія — подъ Updating.</summary>
	void Water_ValueChanged(object? sender, EventArgs e)
	{
		if (Updating) return;
		double rh = RelativeHumidity(Value(numericTemperature), Value(numericWater));
		Updating = true;
		Set(numericHumidity, rh);
		Updating = false;
		Recalculate(keepWater: rh <= 100);
	}

	RoomModel Model() => new(Value(numericVolume), Value(numericBuffer), Value(numericExchange),
		AbsoluteHumidity(Value(numericOutdoorTemperature), Value(numericOutdoorHumidity)), Value(numericSources), FlowFromRated(Value(numericRated)));

	void Recalculate(bool keepWater = false)
	{
		double t = Value(numericTemperature), rh = Value(numericHumidity), target = Value(numericTarget);
		double rho = AbsoluteHumidity(t, rh);

		// воздухъ
		if (!keepWater)
		{
			Updating = true;
			Set(numericWater, rho);
			Updating = false;
		}
		labelDew.Text = Format(CalcDegrees, DewPoint(t, rh));
		labelRatio.Text = Format(CalcGramsPerKilogram, MixingRatio(t, rh));
		labelOther.Text = Format(CalcOther, Min(HumidityAt(t, rh, Value(numericOtherTemperature)), 999));

		// комната
		RoomModel model = Model();
		double air = WaterToRemove(model.Volume, t, rh, target);
		labelRemove.Text = air > 0 ? Format(CalcRemove, air, air * model.Buffer) : CalcNothingToRemove;

		// прогнозъ
		List<string> lines = [];
		double inflow = model.Inflow(rho), removal = model.Removal(rho, t);
		lines.Add(Format(CalcInflow, inflow, inflow * 24 / 1000));
		lines.Add(Format(CalcRemoval, removal, removal * 24 / 1000));
		(double tauOff, double limitOff) = model.Course(t, working: false);
		lines.Add(IsFinite(limitOff) && IsFinite(tauOff)
			? Format(CalcWithout, RelativeHumidity(t, limitOff), tauOff)
			: CalcWithoutLimit);
		(double tau, double limit) = model.Course(t, working: true);
		if (IsFinite(limit) && IsFinite(tau))
			lines.Add(Format(CalcWith, RelativeHumidity(t, limit), tau));
		Forecast forecast = Forecast.Estimate(rho, t, target, tau, limit);
		lines.Add(forecast.Reached ? Format(CalcReached, target)
			: forecast.Hours is { } hours ? Format(CalcEta, target, Duration(FromHours(Min(hours, 24 * 365))))
			: Format(CalcNever, target, forecast.LimitHumidity));
		// держать цѣль: снимать не меньше, чѣмъ приходитъ при цѣли — c·(ρ_цѣль − ρ_min) ≥ притокъ(ρ_цѣль)
		double goal = AbsoluteHumidity(t, target), margin = goal - MinWater(t);
		if (margin > 0)
			lines.Add(Format(CalcNeeded, target, RatedFromFlow(Max(0, model.Inflow(goal)) / margin)));
		else lines.Add(Format(CalcTooLow, MinHumidity));
		labelForecast.Text = Join(NewLine, lines);
	}

	/// <summary>Уточнить буферъ, воздухообмѣнъ и источники по всѣмъ записямъ (MoistureFit.Calibrate).</summary>
	void FromRecords_Click(object? sender, EventArgs e)
	{
		List<Segment> segments = [];
		foreach (Session session in List())
		{
			try
			{
				segments.AddRange(Analyze(RecordFiles.Load(session.Path)));
			}
			catch (IOException)
			{
				// файлъ занятъ — пропускаемъ
			}
		}
		if (!segments.Any(s => !s.Fit.Bounded))
		{
			MessageBox.Show(this, CalcFromRecordsNone, Text, OK, Information);
			return;
		}
		RoomModel model = Calibrate(Model(), segments);
		Updating = true;
		Set(numericBuffer, model.Buffer);
		Set(numericExchange, model.Exchange);
		Set(numericSources, model.Sources);
		Updating = false;
		Recalculate();
		MessageBox.Show(this, Format(CalcFromRecordsDone, segments.Count(s => s.Kind == Off && !s.Fit.Bounded),
			segments.Count(s => s.Kind == Drying && !s.Fit.Bounded), model.Buffer, model.Exchange, model.Sources), Text, OK, Information);
	}
}

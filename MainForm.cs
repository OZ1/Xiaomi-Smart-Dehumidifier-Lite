using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;

namespace DehumidifierControl;

using Properties;

using static Uri;
using static Math;
using static Size;
using static String;
using static Convert;
using static DateTime;
using static TimeSpan;
using static Color;
using static SystemColors;
using static FontStyle;
using static CheckState;
using static CloseReason;
using static TextRenderer;
using static TextFormatFlags;
using static MessageBoxIcon;
using static MessageBoxButtons;
using static FormStartPosition;
using static SystemInformation;
using static Dehumidifier;
using static DehumidifierState;
using static Comfort;

using State = DehumidifierState;

using Timer = System.Windows.Forms.Timer;

public partial class MainForm : Form
{
	Dehumidifier? Device;
	bool Updating;
	bool Refreshing;
	bool PollFailed; // послѣдній опросъ не удался — сообщеніе объ этомъ снимемъ, когда связь вернётся
	uint? TimerLeft;    // сколько минутъ осталось, если таймеръ идётъ (8.3) — для времени выключенія

	readonly Font ValueFont; // жирный, изъ дизайнера — для значеній, на которыя надо обратить вниманіе
	readonly Font QuietFont; // нежирный — для «всё въ порядкѣ»: неисправность отсутствуетъ, прогрѣва нѣтъ

	byte? RoomHumidity { get; set // влажность въ комнатѣ — для треугольника подъ шкалой
	{
		if (field == value) return;
		field = value;
		panelTargetScale.Invalidate();
	}}

	public MainForm()
	{
		InitializeComponent();//⏻\uE7E8

		ValueFont = labelFault.Font;
		QuietFont = new(ValueFont, Regular);
	}

	protected override async void OnLoad(EventArgs e)
	{
		base.OnLoad(e);
		if (DesignMode) return;
		if (Settings.Default.Location.X > -1000000)
		{
			StartPosition = Manual;
			Location = Settings.Default.Location;
		}
		textBoxIP.Text = Settings.Default.IP;
		textBoxToken.Text = Settings.Default.Token;
		if (Settings.Default.Token.Length <= 0)
			SetStatus("Введи токенъ (32 шестнадцатеричныя цифры) и нажми «Подключиться»");
		else await ConnectAsync();
	}

	protected override void OnFormClosing(FormClosingEventArgs e)
	{
		if (e.CloseReason == UserClosing)
		{
			Settings.Default.Location = Location;
			Settings.Default.Save();
		}
		base.OnFormClosing(e);
	}

	protected override void OnFormClosed(FormClosedEventArgs e)
	{
		pollTimer.Stop();
		base.OnFormClosed(e);
	}

	async void Connect_Click(object? sender, EventArgs e)
	{
		if (Device is null)
			await ConnectAsync();
		else Disconnect();
	}

	void Power_CheckedChanged(object? sender, EventArgs e)
	{
		checkBoxPower.Text = checkBoxPower.Checked ? "Включенъ" : "Выключенъ";
		SetFlag(nameof(State.dehumidifier), checkBoxPower);
	}

	void Mode_SelectedIndexChanged(object? sender, EventArgs e)
	{
		if (Updating) return;
		int mode = listBoxMode.SelectedIndex;
		if (mode < 0) return;
		_ = RunAsync(d => d.SetAsync((nameof(State.dehumidifier_mode), (byte)mode)));
	}

	void Target_ValueChanged(object? sender, EventArgs e)
	{
		labelTarget.Text = $"{trackBarTarget.Value} %";
		if (!Updating) ReStart(targetDebounceTimer);
	}

	void TargetDebounce_Tick(object? sender, EventArgs e)
	{
		targetDebounceTimer.Stop();
		byte target = (byte)trackBarTarget.Value;
		_ = RunAsync(d => d.SetAsync((nameof(State.dehumidifier_target_humidity), target)));
	}

	static void ReStart(Timer timer)
	{
		timer.Stop();
		timer.Start();
	}

	void Light_SelectedIndexChanged(object? sender, EventArgs e)
	{
		int level = listBoxLight.SelectedIndex; // 0 выключена, 1 тусклая, 2 яркая
		if (Updating || level < 0) return;
		_ = RunAsync(d => d.SetAsync((nameof(State.indicator_light_mode), (byte)level)));
	}

	void     LightOn_CheckedChanged(object? sender, EventArgs e) => SetFlag(nameof(State.indicator_light         ), checkBoxLight);
	void       Sound_CheckedChanged(object? sender, EventArgs e) => SetFlag(nameof(State.alarm                   ), checkBoxSound);
	void        Lock_CheckedChanged(object? sender, EventArgs e) => SetFlag(nameof(State.physical_controls_locked), checkBoxLock);
	void DryAfterOff_CheckedChanged(object? sender, EventArgs e) => SetFlag(nameof(State.dm_service_dry_after_off), checkBoxDryAfterOff);

	void Timer_CheckedChanged(object? sender, EventArgs e)
	{
		if (Updating) return;
		uint minutes = (uint)numericTimerMinutes.Value;
		_ = RunAsync(d => checkBoxTimer.Checked
			? d.SetAsync((nameof(State.delay_time), minutes), (nameof(State.delay), true))
			: d.SetAsync((nameof(State.delay), false)));
	}

	void TimerMinutes_ValueChanged(object? sender, EventArgs e)
	{
		ShowTimeOff();
		if (!Updating && checkBoxTimer.Checked) ReStart(delayDebounceTimer);
	}

	/// <summary>Время выключенія, пока фокусъ не въ полѣ: сейчасъ + таймеръ; если таймеръ уже идётъ — сейчасъ + осталось.</summary>
	void ShowTimeOff()
	{
		if (dateTimeOff.Focused) return; // въ полѣ пишетъ человѣкъ — не мѣшаемъ
		dateTimeOff.Value = Now.AddMinutes(TimerLeft ?? (double)numericTimerMinutes.Value);
	}

	/// <summary>Въ полѣ времени выключенія пишетъ человѣкъ: минуты таймера = это время − сейчасъ.
	/// Время, которое сегодня уже прошло, — завтрашнее; минуты прижимаются къ предѣламъ поля минутъ.</summary>
	void TimeOff_ValueChanged(object? sender, EventArgs e)
	{
		if (!dateTimeOff.Focused) return; // мѣняемъ сами изъ ShowTimeOff — не отвѣчаемъ
		DateTime now = Now;
		DateTime off = now.Date + FromMinutes(Floor(dateTimeOff.Value.TimeOfDay.TotalMinutes)); // секунды не видны — отбрасываемъ
		if (off <= now) off = off.AddDays(1);
		numericTimerMinutes.Value = Clamp((decimal)Ceiling((off - now).TotalMinutes), numericTimerMinutes.Minimum, numericTimerMinutes.Maximum);
	}

	void DelayDebounce_Tick(object? sender, EventArgs e)
	{
		delayDebounceTimer.Stop();
		uint minutes = (uint)numericTimerMinutes.Value;
		_ = RunAsync(d => d.SetAsync((nameof(State.delay_time), minutes), (nameof(State.delay), true)));
	}

	void Toggle_Click(object? sender, EventArgs e) => _ = RunAsync(d => d.ToggleAsync());
	void LoopMode_Click(object? sender, EventArgs e) => _ = RunAsync(d => d.LoopModeAsync());
	void ResetFilter_Click(object? sender, EventArgs e)
	{
		if (MessageBox.Show(this, "Сбросить счётчикъ фильтра? Дѣлай это послѣ чистки или замѣны фильтра.", Text, OKCancel, Question) == DialogResult.OK)
			_ = RunAsync(d => d.ResetFilterAsync());
	}

	async void PollTimer_Tick(object? sender, EventArgs e)
	{
		if (!Refreshing) await RefreshStateAsync();
	}

	// ───── связь съ устройствомъ ─────

	async Task ConnectAsync()
	{
		string ip = textBoxIP.Text.Trim();
		string token = textBoxToken.Text.Trim().ToUpperInvariant();
		if (!IPAddress.TryParse(ip, out _))
		{
			SetStatus("Невѣрный адресъ", error: true);
			return;
		}
		if (token.Length != 32 || !token.All(IsHexDigit))
		{
			SetStatus("Токенъ — это 32 шестнадцатеричныя цифры", error: true);
			return;
		}

		pollTimer.Stop();
		Device?.Dispose();
		Device = new(new(ip, token));
		Settings.Default.IP = ip;
		Settings.Default.Token = token;
		Settings.Default.Save();

		buttonConnect.Enabled = false;
		groupControls.Enabled = false;
		Updating = true; // новое подключеніе — режимъ, подсвѣтка и звукъ ещё неизвѣстны
		listBoxMode.SelectedIndex = -1;
		listBoxLight.SelectedIndex = -1;
		checkBoxLight.CheckState = Indeterminate;
		checkBoxSound.CheckState = Indeterminate;
		Updating = false;
		SetStatus("Подключаюсь…");
		await RefreshStateAsync();
		buttonConnect.Enabled = true;
		if (groupControls.Enabled) // первое состояніе пришло — подключены
		{
			SetConnected(true);
			SetStatus("Подключено");
			pollTimer.Start();
		}
		else // не вышло: соединеніе закрываемъ, поля остаются для правки, ошибка — въ строкѣ состоянія
		{
			Device?.Dispose();
			Device = null;
		}
	}

	void Disconnect()
	{
		pollTimer.Stop();
		targetDebounceTimer.Stop();
		delayDebounceTimer.Stop();
		Device?.Dispose();
		Device = null;
		groupControls.Enabled = false;
		RoomHumidity = null;
		SetConnected(false);
		SetStatus("Отключено");
		toolStripStatusTime.Text = "";
		textBoxIP.Focus();
	}

	/// <summary>Пока подключены, адресъ и токенъ только для чтенія, токенъ звёздочками, а кнопка отключаетъ.
	/// AcceptButton на это время снимаемъ, чтобы Enter въ любомъ полѣ не рвалъ соединеніе.</summary>
	void SetConnected(bool connected)
	{
		textBoxIP.ReadOnly = connected;
		textBoxToken.ReadOnly = connected;
		textBoxToken.UseSystemPasswordChar = connected;
		buttonConnect.Text = connected ? "&Отключиться" : "&Подключиться";
		AcceptButton = connected ? null : buttonConnect;
	}

	async Task RefreshStateAsync()
	{
		if (Device is not { } device) return;
		Refreshing = true;
		try
		{
			State state = await device.GetStateAsync();
			if (device != Device) return; // пока ждали, переподключились
			ApplyState(state);
			groupControls.Enabled = true;
			toolStripStatusTime.Text = $"{Now:T}"; // слѣва не трогаемъ: тамъ можетъ быть сообщеніе объ ошибкѣ
			if (PollFailed)
			{
				PollFailed = false;
				SetStatus("Связь возстановлена");
			}
		}
		catch (Exception ex) when (device == Device)
		{
			SetStatus(ex.Message, error: true);
			PollFailed = true;
		}
		catch
		{
			// ошибка отъ стараго подключенія — неважна
		}
		finally
		{
			Refreshing = false;
		}
	}

	async Task RunAsync(Func<Dehumidifier, Task> action)
	{
		if (Device is not { } device) return;
		UseWaitCursor = true;
		try
		{
			await action(device);
			SetStatus(""); // команда прошла — прежнее сообщеніе объ ошибкѣ больше не къ мѣсту
		}
		catch (Exception ex)
		{
			SetStatus(ex.Message, error: true);
		}
		finally
		{
			UseWaitCursor = false;
		}
		await RefreshStateAsync(); // и подтвердить, и откатить элементы при ошибкѣ
	}

	void SetFlag(string prop, CheckBox box)
	{
		bool value = box.Checked;
		if (!Updating) _ = RunAsync(d => d.SetAsync((prop, value)));
	}

	void ApplyState(State s)
	{
		Updating = true;
		try
		{
			labelHumidity.Text      = s.environment_relative_umidity is { } h ? $"{h} %" : "—";
			labelHumidity.ForeColor = s.environment_relative_umidity is { } hc ? Darker(ComfortColor(HumidityComfort(hc))) : ControlText;
			RoomHumidity            = s.environment_relative_umidity;
			labelTemperature.Text = s.environment_temperature is { } t ? $"{t:0.#} °Ц" : "—";
			bool noFault         = s.dehumidifier_fault == 0; // всё въ порядкѣ — нежирно и блёкло
			labelFault.Text      = s.dehumidifier_fault is { } f ? (noFault ? "отсутствуетъ" : FaultText(f)) : "—";
			labelFault.ForeColor = s.dehumidifier_fault is > 0 ? Firebrick : noFault ? GrayText : ControlText;
			labelFault.Font = noFault ? QuietFont : ValueFont;
			bool notWarming   = s.dm_service_is_warming_up == false;
			labelWarming.Text = s.dm_service_is_warming_up switch { true => "идётъ", false => "нѣтъ", null => "—" };
			labelWarming.ForeColor = notWarming ? GrayText : ControlText;
			labelWarming.Font      = notWarming ? QuietFont : ValueFont;
			labelDryLeft.Text = s.dm_service_dry_left_time is ushort left and > 0 ? $"{left / 60}:{left % 60:00}" : "—";
			labelTimerLeft.Text = s.delay == true && s.delay_remain_time is { } r ? $"{r / 60}:{r % 60:00}" : "—";
			TimerLeft = s.delay == true ? s.delay_remain_time : null;

			if (s.dehumidifier is { } power) checkBoxPower.Checked = power;
			listBoxMode.SelectedIndex = s.dehumidifier_mode is byte mode and < 3 ? mode : -1;
			if (s.dehumidifier_target_humidity is { } target && !targetDebounceTimer.Enabled && !trackBarTarget.Capture) trackBarTarget.Value = Clamp(target, trackBarTarget.Minimum, trackBarTarget.Maximum);
			// выключенный осушитель не принимаетъ режимъ, цѣлевую влажность и таймеръ (-4002; провѣрено опытомъ),
			// а звукъ, подсвѣтку, блокировку и просушку принимаетъ; питаніе неизвѣстно — не гасимъ
			bool poweredOn = s.dehumidifier != false;
			listBoxMode.Enabled = buttonLoopMode.Enabled = poweredOn;
			trackBarTarget.Enabled = poweredOn && s.dehumidifier_mode != 2; // въ режимѣ сушки бѣлья цѣль тоже не мѣняется (-4002)
			numericTimerMinutes.Enabled = checkBoxTimer.Enabled = dateTimeOff.Enabled = poweredOn;
			ShowTimeOff();
			listBoxLight.SelectedIndex = s.indicator_light_mode is byte level and < 3 ? level : -1;
			checkBoxLight.CheckState   = s.indicator_light switch { true => Checked, false => Unchecked, null => Indeterminate };
			checkBoxSound.CheckState   = s.alarm switch { true => Checked, false => Unchecked, null => Indeterminate };
			if (s.physical_controls_locked is { } locked) checkBoxLock.Checked = locked;
			if (s.dm_service_dry_after_off is { } dry) checkBoxDryAfterOff.Checked = dry;
			if (s.delay is { } delay) checkBoxTimer.Checked = delay;
			if (s.delay_time is uint minutes and > 0 && !delayDebounceTimer.Enabled && !numericTimerMinutes.Focused) numericTimerMinutes.Value = Clamp(minutes, 1, 720);
		}
		finally
		{
			Updating = false;
		}
	}

	void SetStatus(string text, bool error = false)
	{
		toolStripStatusLabel.Text = $"{Now:T} {text}";
		toolStripStatusLabel.ForeColor = error ? Firebrick : ControlText;
	}

	// ───── шкала подъ ползункомъ цѣлевой влажности ─────

	/// <summary>Рекомендуемый діапазонъ цѣлевой влажности изъ атрибута [MIoT] (по спецификаціи и руководству 40…70);
	/// само устройство принимаетъ 0…100 — провѣрено, поэтому ползунокъ шире.</summary>
	static readonly (int Min, int Max) Recommended = RecommendedRange();
	static (int Min, int Max) RecommendedRange()
	{
		MIoTAttribute id = MIoT(nameof(State.dehumidifier_target_humidity));
		return (ToInt32(id.Min), ToInt32(id.Max));
	}

	/// <summary>Цвѣтъ ступени Comfort въ окнѣ: и у влажности въ комнатѣ, и на полоскѣ подъ шкалой.</summary>
	/// <summary>Цвѣтъ ближе къ чёрному: каждая составляющая × factor. При 0,6 крупное число почти чёрное, но ступень ещё узнаётся.</summary>
	static Color Darker(Color color, float factor = 0.6f) => FromArgb((int)Round(color.R * factor), (int)Round(color.G * factor), (int)Round(color.B * factor));

	static Color ComfortColor(Comfort comfort) => comfort switch
	{
		Ideal => ForestGreen,
		Normal => OliveDrab,
		Dry or Humid => DarkOrange,
		_ => Firebrick,
	};

	/// <summary>Шкала подъ ползункомъ: числа, кратныя 10; рекомендуемыя (40…70) темнѣе и подчёркнуты полоской,
	/// раскрашенной по ступенямъ Comfort; подъ ней треугольникъ — влажность въ комнатѣ цвѣтомъ ея ступени.</summary>
	void TargetScale_Paint(object? sender, PaintEventArgs e)
	{
		using Font font = new(Font.FontFamily, 7f);
		int offset = trackBarTarget.Left - panelTargetScale.Left; // координаты ползунка → координаты шкалы
		int height = 0;
		for (int value = trackBarTarget.Minimum; value <= trackBarTarget.Maximum; value += 10)
		{
			string text = value.ToString();
			Size size = MeasureText(text, font, Size.Empty, NoPadding);
			bool recommended = value >= Recommended.Min && value <= Recommended.Max;
			DrawText(e.Graphics, text, font, new Point(offset + TargetX(value) - size.Width / 2, 0),
				recommended ? ControlText : GrayText, NoPadding);
			height = size.Height;
		}
		// полоска по одной единицѣ влажности: отрѣзокъ [v, v+1] цвѣтомъ ступени значенія v; края выступаютъ на 6 точекъ
		using Pen pen = new(Highlight, 2);
		for (int v = Recommended.Min; v < Recommended.Max; v++)
		{
			pen.Color = ComfortColor(HumidityComfort((byte)v));
			int x1 = offset + TargetX(v) - (v == Recommended.Min ? 6 : 0);
			int x2 = offset + TargetX(v + 1) + (v == Recommended.Max - 1 ? 6 : 0);
			e.Graphics.DrawLine(pen, x1, height + 1, x2, height + 1);
		}

		// треугольникъ остріемъ вверхъ подъ влажностью въ комнатѣ — цвѣтомъ ея ступени
		if (RoomHumidity is { } room)
		{
			int x = offset + TargetX(room), top = height + 4;
			Point[] triangle = [new(x, top), new(x - 4, top + 6), new(x + 4, top + 6)];
			using SolidBrush brush = new(ComfortColor(HumidityComfort(room)));
			e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
			e.Graphics.FillPolygon(brush, triangle);
		}

		/// <summary>Гдѣ на ползункѣ стоитъ значеніе — въ его координатахъ. Середина бѣгунка ходитъ по желобку,
		/// не доходя до краёвъ на полширины бѣгунка; такъ же стоятъ и риски.</summary>
		int TargetX(int value)
	{

			const int TBM_GETTHUMBRECT   = 0x0400 + 25;
			const int TBM_GETCHANNELRECT = 0x0400 + 26;

			RECT channel = default, thumb = default;
			SendMessage(trackBarTarget.Handle, TBM_GETCHANNELRECT, 0, ref channel);
			SendMessage(trackBarTarget.Handle, TBM_GETTHUMBRECT, 0, ref thumb);
			int half = (thumb.Right - thumb.Left) / 2;
			int from = channel.Left + half, to = channel.Right - half;
			return from + (to - from) * (value - trackBarTarget.Minimum) / (trackBarTarget.Maximum - trackBarTarget.Minimum);

			[SuppressMessage("Interoperability", "SYSLIB1054: Используйте LibraryImportAttribute вместо DllImportAttribute для генерирования кода маршализации P/Invoke во время компиляции")]
			[DllImport("User32", ExactSpelling = true, EntryPoint = "SendMessageW")]
			static extern nint SendMessage(nint hWnd, int msg, nint wParam, ref RECT lParam);
		}
	}

	[StructLayout(LayoutKind.Sequential)]
	struct RECT { public int Left, Top, Right, Bottom; }
}

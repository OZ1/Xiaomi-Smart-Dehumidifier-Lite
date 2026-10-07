using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Drawing.Drawing2D;
using System.ComponentModel;

namespace DehumidifierControl;

using Properties;

using static Properties.Resources;

using static Uri;
using static Byte;
using static Char;
using static Size;
using static Task;
using static Int32;
using static Array;
using static Single;
using static Double;
using static String;
using static DateTime;
using static TimeSpan;
using static Color;
using static SystemColors;
using static StringComparison;
using static ToolStripDropDownCloseReason;
using static FontStyle;
using static Enumerable;
using static LayoutKind;
using static CheckState;
using static CloseReason;
using static TextRenderer;
using static TextFormatFlags;
using static MessageBoxIcon;
using static MessageBoxButtons;
using static FormStartPosition;
using static SystemInformation;
using static DehumidifierState;
using static Dehumidifier;
using static Network;
using static ComfortScale;
using static Comfort;
using static Values;

using State = DehumidifierState;

using Timer = System.Windows.Forms.Timer;

public partial class MainForm : Form
{
	Dehumidifier? Device;
	bool Updating;
	bool Refreshing;
	bool PollFailed; // послѣдній опросъ не удался — сообщеніе объ этомъ снимемъ, когда связь вернётся

	readonly Font ValueFont; // жирный, изъ дизайнера — для значеній, на которыя надо обратить вниманіе
	readonly Font QuietFont; // нежирный — для «всё въ порядкѣ»: неисправность отсутствуетъ, прогрѣва нѣтъ

	byte? RoomHumidity { get; set // влажность въ комнатѣ — для треугольника подъ шкалой
	{
		if (field == value) return;
		else field = value;
		panelTargetScale.Invalidate();
	}}

	Color NotifyIconColor { get; set
	{
		if (field == value) return;
		else field = value;
		notifyIcon.Invalidate();
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
		ShowTray();
		if (Settings.Default.Location.X > -1000000)
		{
			StartPosition = Manual;
			Location = Settings.Default.Location;
		}
		if (IsNullOrWhiteSpace(Settings.Default.IP) && LocalSubNetPrefix() is { } prefix)
		{
			textBoxIP.Text = prefix; // «192.168.1.» — осталось дописать номеръ осушителя
			ActiveControl = textBoxIP;
			textBoxIP.SelectionStart = prefix.Length;
		}
		else textBoxIP.Text = Settings.Default.IP;
		textBoxToken.Text = Settings.Default.Token;
		if (Settings.Default.Token.Length <= 0)
			SetStatus(EnterToken);
		else await ConnectAsync();
	}

	protected override void OnFormClosing(FormClosingEventArgs e)
	{
		if (e.CloseReason == UserClosing)
		{
			Settings.Default.Location = WindowState == FormWindowState.Normal ? Location : RestoreBounds.Location;
			Settings.Default.Save();
		}
		base.OnFormClosing(e);
	}

	protected override void OnFormClosed(FormClosedEventArgs e)
	{
		pollTimer.Stop();
		notifyIcon.Visible = false;
		base.OnFormClosed(e);
	}

	protected override async void OnResize(EventArgs e)
	{
		base.OnResize(e);
		if (WindowState != FormWindowState.Minimized) return;
		notifyIcon.Visible = true; // свернули — въ трей: значокъ только на это время
		// Сразу прятать нельзя: анимацію сворачиванія DWM рисуетъ уже послѣ WM_SIZE, и окно, скрытое здѣсь, исчезаетъ безъ нея.
		// Даёмъ ей доиграть (у Windows — около четверти секунды) и прячемъ, если окно за это время не развернули.
		await Delay(400);
		if (WindowState == FormWindowState.Minimized) Hide();
	}

	protected override void OnSystemColorsChanged(EventArgs e)
	{
		base.OnSystemColorsChanged(e);
		panelTargetScale.Invalidate(); // включили или выключили высокую контрастность — шкалу перерисовать
	}

	async void Connect_Click(object? sender, EventArgs e)
	{
		if (Device is null)
			await ConnectAsync();
		else Disconnect();
	}

	// ───── связь съ устройствомъ ─────

	async Task ConnectAsync()
	{
		string ip = textBoxIP.Text.Trim();
		string token = textBoxToken.Text.Trim().ToUpperInvariant();
		if (!IPAddress.TryParse(ip, out _))
		{
			SetStatus(BadAddress, error: true);
			return;
		}
		if (token.Length != 32 || !token.All(IsHexDigit))
		{
			SetStatus(BadToken, error: true);
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
		SetStatus(Connecting);
		await RefreshStateAsync();
		buttonConnect.Enabled = true;
		if (groupControls.Enabled) // первое состояніе пришло — подключены
		{
			SetConnected(true);
			SetStatus(Connected);
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
		ShowTray();
		SetConnected(false);
		SetStatus(Disconnected);
		SetUpdated("");
		textBoxIP.Focus();
	}

	/// <summary>Пока подключены, адресъ и токенъ только для чтенія, токенъ звёздочками, а кнопка отключаетъ.
	/// AcceptButton на это время снимаемъ, чтобы Enter въ любомъ полѣ не рвалъ соединеніе.</summary>
	void SetConnected(bool connected)
	{
		textBoxIP.ReadOnly = connected;
		textBoxToken.ReadOnly = connected;
		textBoxToken.UseSystemPasswordChar = connected;
		buttonConnect.Text = connected ? Resources.Disconnect : Connect;
		AcceptButton = connected ? null : buttonConnect;
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
			SetUpdated($"{Now:T}"); // слѣва не трогаемъ: тамъ можетъ быть сообщеніе объ ошибкѣ
			if (PollFailed)
			{
				PollFailed = false;
				SetStatus(LinkRestored);
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

	void ApplyState(State s)
	{
		Updating = true;
		try
		{
			labelTemperature.Text   = s.environment_temperature is { } t ? Temperature(t) : "—";
			ShowTray(RoomHumidity   = s.environment_relative_umidity);
			labelHumidity.Text      = s.environment_relative_umidity is { } h ? $"{h} %" : "—";
			labelHumidity.ForeColor = s.environment_relative_umidity is { } hc ? Accent(Darker(HumidityColor(hc))) : ControlText;
			string? comfort         = s.environment_relative_umidity is { } hd ? ComfortText(HumidityComfort(hd)) : null; // ступень словами: для экраннаго чтеца и подсказкой
			if (labelHumidity.AccessibleDescription != comfort)
			{
				labelHumidity.AccessibleDescription = comfort;
				toolTip.SetToolTip(labelHumidity,     comfort);
			}
			bool noFault         = s.dehumidifier_fault == 0; // всё въ порядкѣ — нежирно и блёкло
			labelFault.Text      = s.dehumidifier_fault is { } f ? FaultText(f) : "—";
			labelFault.ForeColor = s.dehumidifier_fault is > 0 ? Accent(Firebrick) : noFault ? GrayText : ControlText;
			labelFault.Font = noFault ? QuietFont : ValueFont;
			bool notWarming   = s.dm_service_is_warming_up == false;
			labelWarming.Text = s.dm_service_is_warming_up switch { true => WarmingYes, false => WarmingNo, null => "—" };
			labelWarming.ForeColor = notWarming ? GrayText : ControlText;
			labelWarming.Font      = notWarming ? QuietFont : ValueFont;
			labelDryLeft.Text = s.dm_service_dry_left_time is ushort left and > 0 ? Clock(left) : "—";
			labelTimerLeft.Text = s.delay == true && s.delay_remain_time is { } r ? Clock(r) : "—";
			if (s.dehumidifier is { } power) checkBoxPower.Checked = power;
			listBoxMode.SelectedIndex = s.dehumidifier_mode is byte mode and < 3 ? mode : -1;
			if (s.dehumidifier_target_humidity is { } target && !targetDebounceTimer.Enabled && !trackBarTarget.Capture) trackBarTarget.Value = Clamp(target, trackBarTarget.Minimum, trackBarTarget.Maximum);
			if (s.dehumidifier_mode is byte current and < 3 && s.dehumidifier_target_humidity is { } currentTarget) ModeTargets[current] = currentTarget;
			// выключенный осушитель не принимаетъ режимъ, цѣлевую влажность и таймеръ (-4002; провѣрено опытомъ), а звукъ, подсвѣтку, блокировку и просушку принимаетъ;
			bool poweredOn = s.dehumidifier != false;
			listBoxMode.Enabled = buttonLoopMode.Enabled = poweredOn;
			trackBarTarget.Enabled = poweredOn && s.dehumidifier_mode != 2; // въ режимѣ сушки бѣлья цѣль тоже не мѣняется (-4002)
			numericTimerMinutes.Enabled = checkBoxTimer.Enabled = dateTimeOff.Enabled = poweredOn;
			listBoxLight.SelectedIndex = s.indicator_light_mode is byte level and < 3 ? level : -1;
			checkBoxLight.CheckState   = s.indicator_light switch { true => Checked, false => Unchecked, null => Indeterminate };
			checkBoxSound.CheckState   = s.alarm           switch { true => Checked, false => Unchecked, null => Indeterminate };
			if (s.physical_controls_locked is { } locked) checkBoxLock       .Checked = locked;
			if (s.dm_service_dry_after_off is { } dry   ) checkBoxDryAfterOff.Checked = dry;
			if (s.delay                    is { } delay ) checkBoxTimer      .Checked = delay;
			UpdateTimer(s);
		}
		finally
		{
			Updating = false;
		}
	}

	void SetStatus(string text, bool error = false)
	{
		toolStripStatusLabel.Text = $"{Now:T} {text}";
		toolStripStatusLabel.ForeColor = error ? Accent(Firebrick) : ControlText;
	}

	/// <summary>Время обновленія: экранный чтецъ читаетъ имя, а не текстъ, — въ имя и время.</summary>
	void SetUpdated(string time)
	{
		toolStripStatusTime.Text = time;
		toolStripStatusTime.AccessibleName = $"{toolStripStatusTime.ToolTipText} {time}".TrimEnd();
	}


	void Power_CheckedChanged(object? sender, EventArgs e)
	{
		checkBoxPower.Text = checkBoxPower.Checked ? PowerOn : PowerOff;
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

	// ───── таймеръ выключенія ─────
	// Осушитель знаетъ только минуты: delay_remain_time — сколько осталось, цѣлыхъ. Запись delay_time сама запускаетъ таймеръ,
	// delay = true сбросилъ бы его на 60 минутъ, delay = false выключаетъ (провѣрено).
	// Мигъ выключенія хранитъ само поле времени — съ датою и долями минуты, которыхъ не видно: пока таймеръ идётъ, оно стоитъ на мѣстѣ,
	// а поле минутъ отматываетъ остатокъ.

	/// <summary>Таймеръ изъ опроса. Идётъ — въ полѣ минутъ остатокъ, мигъ выключенія остаётся, если сходится съ остаткомъ;
	/// не идётъ — время выключенія = сейчасъ + минуты поля.</summary>
	void UpdateTimer(State s)
	{
		if (delayDebounceTimer.Enabled) return; // человѣкъ правитъ минуты или время — до записи опросъ ихъ не трогаетъ
		DateTime now = Now;
		if (s.delay == true && s.delay_remain_time is uint remain)
		{
			// поле минутъ отматываетъ остатокъ; TimerMinutes_ValueChanged при этомъ молчитъ — идётъ опросъ (Updating)
			if (!numericTimerMinutes.Focused) ShowTimeRemain(remain);
			KeepTimeOff(now.AddMinutes(remain));
		}
		else
		{
			// таймеръ стоитъ: время выключенія бѣжитъ вмѣстѣ съ часами — съ ихъ долею минуты таймеръ и запустится
			ShowTimeOff(now.AddMinutes((double)numericTimerMinutes.Value));
		}
	}

	/// <summary>По минутамъ мигъ выключенія извѣстенъ лишь до минуты, поэтому поле времени съ его долями минуты остаётся, пока сходится съ ними:
	/// таймеръ запустили мы или время выбрали въ полѣ. Разошлось на минуту и больше — запустили не мы (кнопкой, изъ Mi Home)
	/// или поправили минуты: ставимъ по минутамъ.</summary>
	void KeepTimeOff(DateTime byMinutes)
	{
		if (Abs((dateTimeOff.Value - byMinutes).TotalMinutes) >= 1) ShowTimeOff(byMinutes);
	}

	/// <summary>Время выключенія въ поле — если человѣкъ въ нёмъ не пишетъ. Вызоветъ TimeOff_ValueChanged, но тотъ безъ фокуса молчитъ.</summary>
	void ShowTimeOff(DateTime off)
	{
		if (!dateTimeOff.Focused)
			 dateTimeOff.Value = off; // въ полѣ пишетъ человѣкъ — не мѣшаемъ
	}

	/// <summary>Минуты въ поле, прижатыя къ его предѣламъ. Вызоветъ TimerMinutes_ValueChanged (если число измѣнилось):
	/// изъ опроса тотъ молчитъ (Updating), изъ правки времени — какъ правка минутъ.</summary>
	void ShowTimeRemain(decimal minutes) =>numericTimerMinutes.Value = decimal.Clamp(minutes, numericTimerMinutes.Minimum, numericTimerMinutes.Maximum);

	/// <summary>Человѣкъ правитъ время выключенія: часы и минуты — изъ поля, доля минуты — сейчасъ (съ нею таймеръ и запустится),
	/// поэтому до выключенія ровно цѣлыя минуты. Дата — ближайшая: прошедшее сегодня — завтра.</summary>
	void TimeOff_ValueChanged(object? sender, EventArgs e)
	{
		if (Updating || !dateTimeOff.Focused) return; // мѣняемъ сами — не отвѣчаемъ
		DateTime now = Now;
		DateTime mins = dateTimeOff.Value;
		// часы и минуты поля безъ его доли минуты + доля минуты сейчасъ: время выключенія отстоитъ отъ сейчасъ на цѣлыя минуты
		DateTime off = now.Date.AddTicks(mins.TimeOfDay.Ticks - mins.Ticks % TicksPerMinute
		/**/                                                  +  now.Ticks % TicksPerMinute);
		if (off <= now) off = off.AddDays(1); // это время сегодня уже прошло — значитъ, завтра
		ShowTimeRemain((decimal)Round((off - now).TotalMinutes)); // Round — только отъ погрѣшности double: минуты и такъ цѣлыя
		// → TimerMinutes_ValueChanged: флажокъ стоитъ — отложенная запись, нѣтъ — ShowTimeOff, который при фокусѣ здѣсь не пишетъ
	}

	/// <summary>Человѣкъ правитъ минуты: таймеръ идётъ — перезапустится съ ними, когда перестанутъ мѣнять; не идётъ — сдвигается время выключенія.</summary>
	void TimerMinutes_ValueChanged(object? sender, EventArgs e)
	{
		if (Updating) return; // поле отматываетъ остатокъ — это не правка
		if (checkBoxTimer.Checked) ReStart(delayDebounceTimer); // писать, когда перестанутъ мѣнять: стрѣлки и колесо даютъ много событій подрядъ
		else ShowTimeOff(Now.AddMinutes((double)numericTimerMinutes.Value)); // таймеръ стоитъ — запись не нужна, только время выключенія
	}

	/// <summary>Минуты или время перестали мѣнять 700 мс назадъ — перезапустить таймеръ съ ними.</summary>
	void DelayDebounce_Tick(object? sender, EventArgs e)
	{
		delayDebounceTimer.Stop();
		StartTimer();
	}

	/// <summary>Флажокъ таймера: поставили — запустить на минуты поля, сняли — выключить.</summary>
	void Timer_CheckedChanged(object? sender, EventArgs e)
	{
		if (Updating) return; // флажокъ ставитъ опросъ — это не правка
		delayDebounceTimer.Stop(); // отложенная запись больше не нужна: пишемъ сейчасъ
		if (checkBoxTimer.Checked)
		{
			StartTimer();
		}
		else
		{
			ShowTimeOff(Now.AddMinutes((double)numericTimerMinutes.Value)); // таймеръ стоитъ — время выключенія снова бѣжитъ съ часами
			_ = RunAsync(d => d.SetAsync((nameof(State.delay), false)));
		}
	}

	/// <summary>Запустить таймеръ на минуты поля — съ этого мига: его доля минуты и ложится въ поле времени, отъ неё осушитель и отсчитываетъ.</summary>
	void StartTimer()
	{
		uint minutes = (uint)numericTimerMinutes.Value;
		Updating = true; // свою запись не считать правкою — иначе на стыкѣ минутъ TimeOff_ValueChanged пересчиталъ бы минуты на одну меньше
		dateTimeOff.Value = Now.AddMinutes(minutes); // и при фокусѣ въ полѣ: правка уже кончилась — запись идётъ, когда перестали мѣнять
		Updating = false;
		_ = RunAsync(d => d.SetAsync((nameof(State.delay_time), minutes))); // только время: оно же и включаетъ таймеръ
	}

	void Toggle_Click(object? sender, EventArgs e) => _ = RunAsync(d => d.ToggleAsync());
	void LoopMode_Click(object? sender, EventArgs e) => _ = RunAsync(d => d.LoopModeAsync());
	void ResetFilter_Click(object? sender, EventArgs e)
	{
		if (MessageBox.Show(this, ResetFilterQuestion, Text, OKCancel, Question) == DialogResult.OK)
			_ = RunAsync(d => d.ResetFilterAsync());
	}

	async void PollTimer_Tick(object? sender, EventArgs e)
	{
		if (!Refreshing) await RefreshStateAsync();
	}

	/// <summary>Свой цвѣтъ — только безъ высокой контрастности; въ ней — системный цвѣтъ текста, какъ у всего окна.</summary>
	static Color Accent(Color color) => HighContrast ? ControlText : color;

	// ───── шкала подъ ползункомъ цѣлевой влажности ─────

	/// <summary>Цвѣтъ ближе къ чёрному: каждая составляющая × factor. При 0,6 крупное число почти чёрное, но ступень ещё узнаётся.</summary>
	static Color Darker(Color color, float factor = .6f) => FromArgb(
		(int)Round(color.R * factor),
		(int)Round(color.G * factor),
		(int)Round(color.B * factor));

	/// <summary>Рекомендуемый діапазонъ цѣлевой влажности изъ атрибута [MIoT] (по спецификаціи и руководству 40…70);
	/// само устройство принимаетъ 0…100 — провѣрено, поэтому ползунокъ шире.</summary>
	static readonly (int Min, int Max) Recommended = RecommendedRange();
	static          (int Min, int Max)               RecommendedRange()
	{
		MIoTAttribute id = MIoT(nameof(State.dehumidifier_target_humidity));
		return (id.Min, id.Max);
	}

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
			Point pt = new(offset + TargetX(value) - size.Width / 2, 0);
			bool recommended = Recommended.Min <= value && value <= Recommended.Max;
			DrawText(e.Graphics, text, font, pt, recommended ? ControlText : GrayText, NoPadding);
			height = size.Height;
		}

		// полоска по одной единицѣ влажности: отрѣзокъ [v, v+1] цвѣтомъ ступени значенія v; края выступаютъ на 6 точекъ
		using Pen pen = new(Highlight, 2);
		for (int v = Recommended.Min; v < Recommended.Max; v++)
		{
			pen.Color = Accent(ComfortColor(HumidityComfort((byte)v)));
			int x1 = offset + TargetX(v    ) - (v == Recommended.Min     ? 6 : 0);
			int x2 = offset + TargetX(v + 1) + (v == Recommended.Max - 1 ? 6 : 0);
			e.Graphics.DrawLine(pen, x1, height + 1, x2, height + 1);
		}

		// треугольникъ остріемъ вверхъ подъ влажностью въ комнатѣ — цвѣтомъ ея ступени
		if (RoomHumidity is { } room)
		{
			int x = offset + TargetX(room), top = height + 4;
			ReadOnlySpan<Point> triangle = [new(x, top), new(x - 4, top + 6),
			/**/                                         new(x + 4, top + 6)];
			using SolidBrush brush = new(Accent(ComfortColor(HumidityComfort(room))));
			e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
			e.Graphics.FillPolygon(brush, triangle);
		}

		/// <summary>Гдѣ на ползункѣ стоитъ значеніе — въ его координатахъ. Середина бѣгунка ходитъ по желобку,
		/// не доходя до краёвъ на полширины бѣгунка; такъ же стоятъ и риски.</summary>
		int TargetX(int value)
		{

			const int TBM_GETTHUMBRECT   = 0x0400 + 25;
			const int TBM_GETCHANNELRECT = 0x0400 + 26;

			SendMessage(trackBarTarget.Handle, TBM_GETCHANNELRECT, 0, out RECT channel);
			SendMessage(trackBarTarget.Handle, TBM_GETTHUMBRECT,   0, out RECT thumb);
			int half = (thumb.Right - thumb.Left) / 2;
			int from = channel.Left + half, to = channel.Right - half;
			return from + (to - from) * (value - trackBarTarget.Minimum) / (trackBarTarget.Maximum - trackBarTarget.Minimum);

			[SuppressMessage("Interoperability", "SYSLIB1054: Используйте LibraryImportAttribute вместо DllImportAttribute для генерирования кода маршализации P/Invoke во время компиляции")]
			[DllImport("User32", ExactSpelling = true, EntryPoint = "SendMessageW")]
			static extern nint SendMessage(nint hWnd, int msg, nint wParam, out RECT lParam);
		}
	}

	[StructLayout(Sequential)]
	struct RECT { public int Left, Top, Right, Bottom; }

	// ───── трей: капля цвѣтомъ влажности и меню управленія ─────

	/// <summary>Капля въ треѣ — цвѣтомъ влажности (переливъ тотъ же, что у надписи, цвѣта ярче), подсказка — влажность и ступень словами;
	/// безъ данныхъ — серебряная. Перерисовывается, только когда мѣняется цвѣтъ, а за темою, контрастомъ и размѣромъ значокъ слѣдитъ самъ.</summary>
	void ShowTray(byte? humidity = null)
	{
		notifyIcon.Text = humidity is { } ht ? $"{CliHumidity} {ht} %" : Text;
		NotifyIconColor = humidity is { } hc ? HumidityColor(hc, TrayComfortColor) : Silver;
	}

	void RestoreFromTray()
	{
		Show();
		WindowState = FormWindowState.Normal;
		Activate();
		notifyIcon.Visible = false; // окно на экранѣ — въ треѣ не нужно
	}

	void NotifyIcon_MouseClick(object? sender, MouseEventArgs e)
	{
		if (e.Button == MouseButtons.Left) RestoreFromTray();
	}

	void NotifyIcon_Paint(object? sender, PaintEventArgs e)
	{
		Glyph.Drop(e.Graphics, e.ClipRectangle, Accent(NotifyIconColor), notifyIcon.LightTheme);
	}

	void Open_Click(object? sender, EventArgs e) => RestoreFromTray();
	void Exit_Click(object? sender, EventArgs e) => Close();

	ToolStripMenuItem[] ModeItems  => field ??= [menuModeSmart, menuModeSleep, menuModeDry];
	ToolStripMenuItem[] LightItems => field ??= [menuLightOff, menuLightDim, menuLightBright];
	Dictionary<ToolStripMenuItem, CheckBox> FlagItems => field ??= new()
	{
		{ menuSound,       checkBoxSound       },
		{ menuLock,        checkBoxLock        },
		{ menuDryAfterOff, checkBoxDryAfterOff },
	};

	/// <summary>Цѣль въ подменю режима: (пунктъ, режимъ, влажность).</summary>
	Dictionary<ToolStripMenuItem, (byte Mode, byte Target)> TargetItems => field ??= new()
	{
		{menuSmart40, (0, 40)}, {menuSmart50, (0, 50)}, {menuSmart60, (0, 60)}, {menuSmart70, (0, 70)},
		{menuSleep40, (1, 40)}, {menuSleep50, (1, 50)}, {menuSleep60, (1, 60)}, {menuSleep70, (1, 70)},
	};

	/// <summary>Поле любой цѣли въ подменю режима: (поле, режимъ).</summary>
	Dictionary<ToolStripTextBox, byte> TargetBoxes => field ??= new() {{menuSmartValue, 0}, {menuSleepValue, 1}};

	/// <summary>Послѣдняя извѣстная цѣль каждаго режима: осушитель помнитъ ихъ всѣ, а сообщаетъ только текущую —
	/// запоминаемъ, что видѣли въ отвѣтахъ, пока режимъ былъ текущимъ. Только на время работы: послѣ запуска извѣстна лишь текущая.</summary>
	readonly byte?[] ModeTargets = new byte?[3];

	/// <summary>Меню повторяетъ окно: отмѣтки — состояніе элементовъ, что тамъ погашено — гаснетъ и здѣсь.
	/// Пункты дѣйствуютъ черезъ тѣ же элементы — команда уходитъ тѣмъ же путёмъ, что и изъ окна.
	/// Неизвѣстное (третье) состояніе флажка — пунктъ гаснетъ: щелчокъ по такому флажку ничего бы не отправилъ.</summary>
	void TrayMenu_Opening(object? sender, CancelEventArgs e)
	{
		menuPower.Checked = checkBoxPower.Checked;
		menuPower.Enabled = checkBoxPower.Enabled;
		bool modes = listBoxMode.Enabled; // выключенный осушитель не принимаетъ ни режимъ, ни цѣль
		for (int i = 0; i < ModeItems.Length; i++)
		{
			ModeItems[i].Checked = listBoxMode.SelectedIndex == i;
			ModeItems[i].Enabled = modes;
		}
		// цѣль каждаго режима — послѣдняя извѣстная: осушитель сообщаетъ только текущую
		foreach ((ToolStripMenuItem item, (byte mode, byte target)) in TargetItems)
		{
			item.Checked = ModeTargets[mode] == target;
			item.Enabled = modes;
		}
		foreach ((ToolStripTextBox box, byte mode) in TargetBoxes)
		{
			box.Text = ModeTargets[mode]?.ToString();
			box.Enabled = modes;
		}
		menuLight  .Enabled    = checkBoxLight.Enabled;
		menuLightOn.Enabled    = checkBoxLight.CheckState != Indeterminate;
		menuLightOn.CheckState = checkBoxLight.CheckState;
		for (int i = 0; i < LightItems.Length; i++)
			LightItems[i].Checked = listBoxLight.SelectedIndex == i;
		foreach ((ToolStripMenuItem item, CheckBox box) in FlagItems)
		{
			item.CheckState = box.CheckState;
			item.Enabled = box.Enabled && box.CheckState != Indeterminate;
		}
		menuResetFilter.Enabled = groupControls.Enabled;
	}

	void MenuLightLevel_Click(object? sender, EventArgs e) => listBoxLight.SelectedIndex = IndexOf(LightItems, sender);

	void MenuLightOn_Click(object? sender, EventArgs e) => checkBoxLight.Checked ^= true;
	void MenuPower_Click(object? sender, EventArgs e) => checkBoxPower.Checked ^= true;
	void MenuFlag_Click(object? sender, EventArgs e)
	{
		FlagItems[(ToolStripMenuItem)sender!].Checked ^= true;
	}

	/// <summary>Режимъ — щелчкомъ по нему самому; у «Умнаго» и «Ночного» есть подменю цѣли, поэтому меню закрываемъ сами.</summary>
	void MenuMode_Click(object? sender, EventArgs e)
	{
		listBoxMode.SelectedIndex = IndexOf(ModeItems, sender);
		menuTray.Close(ItemClicked);
	}

	/// <summary>Въ полѣ цѣли — только цифры.</summary>
	void MenuTargetValue_KeyPress(object? sender, KeyPressEventArgs e) => e.Handled = !IsControl(e.KeyChar) && !IsAsciiDigit(e.KeyChar);
	void MenuTargetValue_KeyDown(object? sender, KeyEventArgs e)
	{
		if (e.KeyCode != Keys.Enter) return;
		e.SuppressKeyPress = true;
		ToolStripTextBox box = (ToolStripTextBox)sender!;
		byte mode = TargetBoxes[box];
		if (!TryParse(box.Text, out byte target) || trackBarTarget.Minimum > target || target > trackBarTarget.Maximum) return;
		menuTray.Close(Keyboard);
		SetModeTarget((mode, target));
	}

	void MenuTarget_Click(object? sender, EventArgs e)
	{
		SetModeTarget(TargetItems[(ToolStripMenuItem)sender!]);
	}

	/// <summary>Цѣль въ режимѣ: если режимъ не тотъ — сначала онъ, потомъ цѣль (осушитель помнитъ её для каждаго режима свою).
	/// Прямо на устройство, не черезъ ползунокъ: онъ показываетъ цѣль текущаго режима, и то же число въ другомъ режимѣ его не сдвинуло бы.</summary>
	void SetModeTarget((byte Mode, byte Target) mt)
	{
		(byte mode, byte target) = mt;
		bool switchMode = listBoxMode.SelectedIndex != mode;
		_ = RunAsync(async d =>
		{
			if (switchMode) await d.SetAsync((nameof(State.dehumidifier_mode), mode));
			await d.SetAsync((nameof(State.dehumidifier_target_humidity), target));
		});
	}
}

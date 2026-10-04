using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Numerics;
using System.Runtime.InteropServices;

namespace DehumidifierControl;

using Properties;

using static Properties.Resources;

using static Uri;
using static Size;
using static Int32;
using static Single;
using static Double;
using static String;
using static Vector3;
using static Matrix3x2;
using static Matrix4x4;
using static DateTime;
using static TimeSpan;
using static Color;
using static SystemColors;
using static StringComparison;
using static FontStyle;
using static Enumerable;
using static LayoutKind;
using static CheckState;
using static CloseReason;
using static TextRenderer;
using static TextFormatFlags;
using static NetworkInterface;
using static OperationalStatus;
using static NetworkInterfaceType;
using static AddressFamily;
using static MessageBoxIcon;
using static MessageBoxButtons;
using static FormStartPosition;
using static SystemInformation;
using static DehumidifierState;
using static Dehumidifier;
using static Comfort;

using State = DehumidifierState;

using Timer = System.Windows.Forms.Timer;

public partial class MainForm : Form
{
	static readonly string[] VitualNicKeywods = ["Virtual", "Hyper-V", "vEthernet", "VMware", "VirtualBox", "WSL", "TAP", "VPN"];

	Dehumidifier? Device;
	bool Updating;
	bool Refreshing;
	bool PollFailed; // послѣдній опросъ не удался — сообщеніе объ этомъ снимемъ, когда связь вернётся

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
		if (IsNullOrWhiteSpace(Settings.Default.IP) && LocalSubnetPrefix() is { } prefix)
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

		/// <summary>Начало адреса изъ подсѣти самаго правдоподобнаго адаптера: работающій, съ частнымъ IPv4; выше — со шлюзомъ (настоящая сѣть),
		/// не виртуальный (Hyper-V, WSL, VMware, VirtualBox, VPN), затѣмъ проводной, Wi-Fi, dial-up. Октеты — цѣлые по маскѣ: /24 → «192.168.1.», /16 → «10.0.».</summary>
		static string? LocalSubnetPrefix()
		{
			byte bestType = 0;
			bool bestGw = false, bestVirt = false;
			int bestIf = int.MaxValue;
			UnicastIPAddressInformation? best = null;
			foreach (NetworkInterface nic in GetAllNetworkInterfaces())
			{
				if (nic.OperationalStatus != Up) continue;
				if (nic.NetworkInterfaceType is Loopback or Tunnel) continue;
				IPInterfaceProperties properties = nic.GetIPProperties();
				foreach (UnicastIPAddressInformation ucast in properties.UnicastAddresses)
				{
					if (ucast.Address.AddressFamily != InterNetwork) continue;
					#pragma warning disable CS0618 // Тип или член устарел
					uint ip = unchecked((uint)ucast.Address.Address); // ReadUInt32BigEndian(.TryWriteByte(stackallock[4]))
					#pragma warning restore CS0618 // можно замѣнить на ↑
					if ((ip & 0x00FF) !=     10 && // A 10/8
						(ip & 0xF0FF) != 0x10AC && // B 172.16/12
						(ip & 0xFFFF) != 0xA8C0)   // C 192.168/16
						continue;

					bool set = best is null;

					bool v4gw = properties.GatewayAddresses.Any(g => g.Address.AddressFamily == InterNetwork);
					if (     bestGw && !v4gw && !set) continue;
					set |=  !bestGw &&  v4gw;
					if (set) bestGw =   v4gw;

					bool v4virt = VitualNicKeywods.Any(word => nic.Description.Contains(word, OrdinalIgnoreCase) ||
					/**/                                       nic.Name       .Contains(word, OrdinalIgnoreCase));
					if (    !bestVirt &&  v4virt && !set) continue;
					set |=   bestVirt && !v4virt;
					if (set) bestVirt =   v4virt;

					byte v4type = nic.NetworkInterfaceType switch { Ethernet or Ethernet3Megabit or FastEthernetT or FastEthernetFx or GigabitEthernet => 3, Wireless80211 => 2, Ppp => 1, _ => 0 };
					if (     bestType > v4type && !set) continue;
					set |=   bestType < v4type;
					if (set) bestType = v4type;

					int v4if = properties.GetIPv4Properties().Index;
					if (     bestIf < v4if && !set) continue;
					set |=   bestIf > v4if;
					if (set) bestIf = v4if;

					best = ucast;
				}
			}
			if (best is null) return null;
			int octets = Clamp(best.PrefixLength / 8, 1, 3);
			Span<char> address = stackalloc char[15]; // 255.255.255.255
			best.Address.TryFormat(address, out int length);
			int end = 0;
			for (int octet = 0; octet < octets; octet++)
				end += address[end..length].IndexOf('.') + 1; // по точку послѣ octets-го октета включительно
			return new(address[..end]);
		}
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
			labelTemperature.Text   = s.environment_temperature is { } t ? Format(TemperatureFormat, t) : "—";
			RoomHumidity            = s.environment_relative_umidity;
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
			labelDryLeft.Text = s.dm_service_dry_left_time is ushort left and > 0 ? $"{left / 60}:{left % 60:00}" : "—";
			labelTimerLeft.Text = s.delay == true && s.delay_remain_time is { } r ? $"{r / 60}:{r % 60:00}" : "—";
			if (s.dehumidifier is { } power) checkBoxPower.Checked = power;
			listBoxMode.SelectedIndex = s.dehumidifier_mode is byte mode and < 3 ? mode : -1;
			if (s.dehumidifier_target_humidity is { } target && !targetDebounceTimer.Enabled && !trackBarTarget.Capture) trackBarTarget.Value = Clamp(target, trackBarTarget.Minimum, trackBarTarget.Maximum);
			// выключенный осушитель не принимаетъ режимъ, цѣлевую влажность и таймеръ (-4002; провѣрено опытомъ),
			// а звукъ, подсвѣтку, блокировку и просушку принимаетъ;
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

	/// <summary>Цвѣтъ ступени Comfort въ окнѣ: и у влажности въ комнатѣ, и на полоскѣ подъ шкалой.</summary>
	static Color ComfortColor(Comfort comfort) => comfort switch
	{
		Ideal => ForestGreen, Normal => Olive, Dry or Humid => Chocolate, _ => Firebrick
	};

	// ───── плавный цвѣтъ влажности: площадки и переходы въ OKLCH ─────

	/// <summary>Половина ширины перехода, %: внутри ступени цвѣтъ стоитъ, за 3 % до границы и 3 % послѣ — переливается въ сосѣдній.</summary>
	const double HumidityBlendPercent = 3;

	/// <summary>Матрицы OKLab (Björn Ottosson, 2020): линейный sRGB → LMS и LMS въ кубическомъ корнѣ → Lab; обратныя — ихъ обращеніемъ.</summary>
	static readonly Matrix4x4 LinearToLMS = Matrix(0.4122214708f,  0.5363325363f,  0.0514459929f,
	/**/                                           0.2119034982f,  0.6806995451f,  0.1073969566f,
	/**/                                           0.0883024619f,  0.2817188376f,  0.6299787005f);
	static readonly Matrix4x4 LMSToLab    = Matrix(0.2104542553f,  0.7936177850f, -0.0040720468f,
	/**/                                           1.9779984951f, -2.4285922050f,  0.4505937099f,
	/**/                                           0.0259040371f,  0.7827717662f, -0.8086757660f);
	static readonly Matrix4x4 LabToLMS    = Inverse(LMSToLab);
	static readonly Matrix4x4 LMSToLinear = Inverse(LinearToLMS);

	/// <summary>Матрица 3×3, записанная строками, какъ въ статьѣ. Vector3.Transform умножаетъ вектор-строку на матрицу (v·M),
	/// а въ статьѣ — матрица на вектор-столбецъ (M·v), поэтому кладётся транспонированной.</summary>
	static Matrix4x4 Matrix(float m11, float m12, float m13,
	/**/                    float m21, float m22, float m23,
	/**/                    float m31, float m32, float m33) => new(
		m11, m21, m31, 0,
		m12, m22, m32, 0,
		m13, m23, m33, 0,
		0,   0,   0,   1);

	static Matrix4x4 Inverse(Matrix4x4 matrix) => Invert(matrix, out Matrix4x4 inverse) ? inverse : throw new ArgumentException(null, nameof(matrix));

	/// <summary>Цвѣтъ влажности: на площадкѣ — цвѣтъ ступени, у границы — переходъ въ OKLCH по smoothstep (3t² − 2t³):
	/// у краёвъ перехода цвѣтъ мѣняется медленно, поэтому площадка переходитъ въ переливъ безъ излома.
	/// Граница ступеней — посерединѣ между послѣднимъ значеніемъ одной и первымъ слѣдующей (ComfortStarts − 0,5);
	/// ступени шире двухъ переходовъ, поэтому влажность бываетъ у одной границы самое большее.</summary>
	static Color HumidityColor(byte humidity)
	{
		for (int i = 0; i < ComfortStarts.Length; i++)
		{
			double  d =  humidity - ComfortStarts[i] + 0.5; // разстояніе до границы между ступенями i и i + 1
			if (Abs(d) > HumidityBlendPercent) continue;
			double t = (d + HumidityBlendPercent) / (2 * HumidityBlendPercent);
			Color a = ComfortColor((Comfort) i);
			Color b = ComfortColor((Comfort)(i + 1));
			return MixOKLCH(a, b, (float)(t * t * (3 - 2 * t)));

			/// <summary>Смѣсь двухъ цвѣтовъ въ OKLCH: всѣ три составляющія — линейно, тонъ — по короткой дугѣ.
			/// Въ OKLCH равные шаги и на глазъ равны, поэтому переливъ ровный, безъ грязной середины, какъ въ RGB.</summary>
			static Color MixOKLCH(Color a, Color b, float t)
			{
				Vector3 from = ToOKLCH(a), to = ToOKLCH(b);
				float dh = to.Z - from.Z;
				if (dh >  float.Pi) dh -= float.Tau;
				if (dh < -float.Pi) dh += float.Tau;
				return FromOKLCH(Lerp(from, to with { Z = from.Z + dh }, t));
			}

			/// <summary>sRGB → OKLCH: (свѣтлота 0…1, насыщенность, тонъ въ радіанахъ).</summary>
			static Vector3 ToOKLCH(Color color)
			{
				static float Linear(byte c)
				{
					float  v = c / 255f;
					return v <= 0.04045f ? v / 12.92f : Pow((v + 0.055f) / 1.055f, 2.4f);
				}
				Vector3 lms = Transform(new(Linear(color.R), Linear(color.G), Linear(color.B)), LinearToLMS);
				Vector3 lab = Transform(new(Cbrt(lms.X), Cbrt(lms.Y), Cbrt(lms.Z)), LMSToLab);
				return new(lab.X, new Vector2(lab.Y, lab.Z).Length(), Atan2(lab.Z, lab.Y));
			}

			/// <summary>OKLCH → sRGB; что вышло за охватъ sRGB — прижимается къ 0…255.</summary>
			static Color FromOKLCH(Vector3 lch)
			{
				(float sin, float cos) = SinCos(lch.Z);
				Vector3 lms = Transform(new(lch.X, lch.Y * cos, lch.Y * sin), LabToLMS);
				Vector3 rgb = Transform(lms * lms * lms, LMSToLinear);
				static int Gamma(float c) => (int)Round(255 * Clamp(c <= 0.0031308f ? 12.92f * c : 1.055f * Pow(c, 1 / 2.4f) - 0.055f, 0, 1));
				return FromArgb(Gamma(rgb.X), Gamma(rgb.Y), Gamma(rgb.Z));
			}
		}
		return ComfortColor(HumidityComfort(humidity)); // далеко отъ всѣхъ границъ — на площадкѣ
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
			e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
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
}

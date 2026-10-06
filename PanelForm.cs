using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace DehumidifierControl;

using Properties;

using static Properties.Resources;

using static Math;
using static Color;
using static LogFormat;
using static Values;
using static SystemInformation;

using State = DehumidifierState;
using Timer = System.Windows.Forms.Timer;

/// <summary>Маленькое окно, похожее на верхъ осушителя: кругъ съ квадратнымъ уголкомъ справа внизу; тонкія сегментныя цифры влажности,
/// подъ ними кружки — авто (умный), ночной, сушка бѣлья, подъ ночнымъ — выключатель. Таскается за любое мѣсто, тянется за край (растётъ всё);
/// гаечный ключъ въ уголкѣ — большое окно.
/// Опросъ, подключеніе, запись остаются въ MainForm — панель только показываетъ его состояніе и шлётъ команды черезъ него.</summary>
public sealed class PanelForm : Form
{
	readonly MainForm Main;
	readonly Action ShowMain;

	readonly PanelButton buttonPower, buttonAuto, buttonNight, buttonDry, buttonWifi, buttonMain;
	readonly List<PanelButton> Buttons = []; // въ порядкѣ Tab
	PanelButton? Hover, Pressed, FocusedButton;
	bool KeyboardCues; // клавиатурою уже ходили — рамка фокуса видна
	readonly ToolTip toolTip = new() { AutoPopDelay = 10000, InitialDelay = 500 };
	readonly ContextMenuStrip menu = new(), wifiMenu = new();
	readonly Timer revertTimer = new() { Interval = 3000 }; // цѣль на экранѣ — 3 с послѣ послѣдняго нажатія
	readonly Timer flashTimer = new() { Interval = 30 };

	State? Current;          // послѣднее состояніе изъ опроса; null — связи нѣтъ
	bool ShowTarget;         // на экранѣ цѣль, а не влажность въ комнатѣ
	bool? PendingPower;      // команда ушла, опросъ её ещё не подтвердилъ — показываемъ уже желаемое
	byte? PendingMode;
	byte? PendingTarget;
	int Running;             // сколько командъ ещё идётъ: ожидаемое сбрасываемъ, когда кончилась послѣдняя
	DateTime FlashStart;
	float Flash;             // 1 → 0: цифры «подбѣливаются» при смѣнѣ цѣли

	public PanelForm(MainForm main, Action showMain)
	{
		Main = main;
		ShowMain = showMain;
		Text = main.Text;
		Icon = main.Icon;
		FormBorderStyle = FormBorderStyle.None;
		MaximizeBox = MinimizeBox = false;
		ShowInTaskbar = true;
		AutoScaleMode = AutoScaleMode.None; // раскладка своя, по DeviceDpi
		StartPosition = FormStartPosition.CenterScreen;
		KeyPreview = true;
		AccessibleName = main.Text;

		buttonPower = Add(PanelPower, (g, r, ink) => DrawIcon(g, r, ink, ""), () => Power_Click());
		buttonAuto  = Add(Capital(ModeSmart), DrawAuto, () => Mode_Click(0));
		buttonNight = Add(Capital(ModeSleep), (g, r, ink) => DrawIcon(g, r, ink, ""), () => Mode_Click(1));
		buttonDry   = Add(Capital(ModeDry), DrawShirt, () => Mode_Click(DryMode));
		buttonWifi  = Add(PanelNoLink, (g, r, ink) => DrawIcon(g, r, ink, ""), () => wifiMenu.Show(this, new Point(buttonWifi!.Bounds.Left, buttonWifi.Bounds.Bottom)));
		buttonMain  = Add(PanelBigWindow, (g, r, ink) => DrawIcon(g, r, ink, ""), () => ShowMain());
		buttonWifi.Circle = buttonMain.Circle = false;
		buttonMain.Faint = true;
		Buttons.Clear(); // Tab — сверху внизъ: Wi‑Fi, рядъ режимовъ, выключатель, ключъ
		Buttons.AddRange([buttonWifi, buttonAuto, buttonNight, buttonDry, buttonPower, buttonMain]);

		menu.Items.Add(PanelBigWindow, null, (_, _) => ShowMain());
		menu.Items.Add(new ToolStripSeparator());
		menu.Items.Add(PanelExit, null, (_, _) => Close());
		wifiMenu.Items.Add(Connect, null, async (_, _) => await Main.ReconnectAsync());
		wifiMenu.Items.Add(PanelAddress, null, (_, _) => { ShowMain(); Main.ShowConnectionEditor(); });
		ContextMenuStrip = menu;

		revertTimer.Tick += (_, _) => { revertTimer.Stop(); ShowTarget = false; Redraw(); };
		flashTimer.Tick += FlashTimer_Tick;
		Main.StateChanged += Main_StateChanged;
		Current = Main.LastState;
		UpdateView();

		PanelButton Add(string tip, Action<Graphics, RectangleF, Color> glyph, Action click)
		{
			PanelButton button = new() { Glyph = glyph, Tip = tip, Click = click };
			Buttons.Add(button);
			return button;
		}
	}

	// ───── окно: форма, размѣръ, перетаскиваніе, положеніе ─────

	/// <summary>Окно квадратное, вся раскладка — въ единицахъ квадрата 200×200: растягивается окно — растётъ и всё въ нёмъ.</summary>
	const float Unit = 200;
	float S(float units) => units * ClientSize.Width / Unit;

	/// <summary>Предѣлы стороны окна, точки при 96 DPI.</summary>
	const int MinSide = 150, MaxSide = 600;

	int Px(int pixels) => pixels * DeviceDpi / 96;

	/// <summary>Кругъ, у котораго правая нижняя четверть — квадратный уголокъ (тамъ ключъ). inset — отступъ внутрь (для обводки).</summary>
	GraphicsPath Outline(float inset)
	{
		float w = ClientSize.Width - 2 * inset, h = ClientSize.Height - 2 * inset, r = S(14);
		GraphicsPath path = new();
		path.AddArc(inset, inset, w, h, 90, 270);                         // низъ → лѣво → верхъ → право
		path.AddLine(inset + w, inset + h / 2, inset + w, inset + h - r); // правый край уголка
		path.AddArc(inset + w - 2 * r, inset + h - 2 * r, 2 * r, 2 * r, 0, 90);
		path.CloseFigure();                                               // нижній край уголка — къ низу круга
		return path;
	}

	/// <summary>Раскладка: цифры въ верхней половинѣ, подъ ними рядъ — авто, ночной, сушка, подъ ночнымъ — выключатель; Wi‑Fi слѣва отъ цифръ, ключъ въ уголкѣ.</summary>
	void LayoutPanel()
	{
		if (ClientSize.Width <= 0) return;
		int size = (int)S(32); // кружокъ 24 + мѣсто для кольца выбраннаго режима
		Rectangle At(float x, float y, int side) => new((int)(S(x) - side / 2f), (int)(S(y) - side / 2f), side, side);
		buttonAuto .Bounds = At(62, 108, size);
		buttonNight.Bounds = At(100, 108, size);
		buttonDry  .Bounds = At(138, 108, size);
		buttonPower.Bounds = At(100, 144, size);
		buttonWifi .Bounds = At(34, 60, (int)S(22));
		buttonMain .Bounds = At(181, 181, (int)S(20));
		Redraw();
	}

	void SetLimits()
	{
		MinimumSize = new(Px(MinSide), Px(MinSide));
		MaximumSize = new(Px(MaxSide), Px(MaxSide));
	}

	/// <summary>При смѣнѣ DPI Windows само предлагаетъ размѣръ въ той же мѣрѣ — сдвигаемъ только предѣлы.</summary>
	protected override void OnDpiChanged(DpiChangedEventArgs e)
	{
		MinimumSize = MaximumSize = Size.Empty; // прежніе предѣлы не дали бы принять новый размѣръ
		base.OnDpiChanged(e);
		SetLimits();
	}

	protected override void OnResize(EventArgs e)
	{
		base.OnResize(e);
		LayoutPanel();
	}

	/// <summary>Сторона окна въ точкахъ при 96 DPI — въ настройки.</summary>
	int LogicalSide => ClientSize.Width * 96 / DeviceDpi;

	protected override async void OnLoad(EventArgs e)
	{
		// размѣръ — здѣсь, а не при созданіи окна: тамъ Form потомъ ещё разъ ставитъ свой ClientSize изъ конструктора; до base.OnLoad — чтобы CenterScreen центрировалъ уже этотъ
		SetLimits();
		int side = Px(Math.Clamp(Settings.Default.PanelSize, MinSide, MaxSide));
		Size = new(side, side);
		base.OnLoad(e);
		Point location = Settings.Default.PanelLocation;
		if (location.X > -1000000 && Screen.AllScreens.Any(s => s.WorkingArea.Contains(location)))
		{
			StartPosition = FormStartPosition.Manual;
			Location = location;
		}
		await Main.StartAsync(); // запустились съ панелью — большое окно не показывали, подключаемся отсюда
	}

	protected override void OnVisibleChanged(EventArgs e)
	{
		base.OnVisibleChanged(e);
		if (!Visible && IsHandleCreated) Remember(); // спрятали ради большого окна
	}

	protected override void OnFormClosing(FormClosingEventArgs e)
	{
		if (Visible) Remember();
		Settings.Default.Save();
		base.OnFormClosing(e);
	}

	void Remember()
	{
		Settings.Default.PanelLocation = Location;
		Settings.Default.PanelSize = LogicalSide;
	}

	protected override void OnFormClosed(FormClosedEventArgs e)
	{
		Main.StateChanged -= Main_StateChanged;
		revertTimer.Dispose();
		flashTimer.Dispose();
		base.OnFormClosed(e);
	}

	/// <summary>Окно безъ рамки таскается за любое свободное мѣсто: тамъ, гдѣ нѣтъ кнопокъ, оно отвѣчаетъ, что это заголовокъ;
	/// у края — что это край рамки: тянется за него, оставаясь квадратнымъ. Правый щелчокъ по «заголовку» — своё меню, а не системное.</summary>
	protected override void WndProc(ref Message m)
	{
		const int WM_NCHITTEST = 0x84, WM_CONTEXTMENU = 0x7B, WM_NCRBUTTONUP = 0xA5, WM_SIZING = 0x214, HTCLIENT = 1, HTCAPTION = 2;
		switch (m.Msg)
		{
			case WM_CONTEXTMENU when menu.Visible: // уже показано по правому щелчку
				return;
			case WM_CONTEXTMENU:
				Point at = m.LParam == -1 ? PointToScreen(new(Width / 2, Height / 2)) : ScreenPoint(m.LParam);
				menu.Show(at);
				return;
			case WM_NCRBUTTONUP: // правый щелчокъ по «заголовку» или краю: самъ Windows WM_CONTEXTMENU слоистому окну тутъ не шлётъ
				menu.Show(ScreenPoint(m.LParam));
				return;
			case WM_SIZING:
				KeepSquare((int)m.WParam, m.LParam);
				m.Result = 1;
				return;
		}
		base.WndProc(ref m);
		if (m.Msg == WM_NCHITTEST && m.Result == HTCLIENT)
		{
			Point p = PointToClient(ScreenPoint(m.LParam));
			m.Result = Edge(p) ?? (ButtonAt(p) is null ? HTCAPTION : HTCLIENT);
		}
	}

	PanelButton? ButtonAt(Point p) => Buttons.FirstOrDefault(b => b.Contains(p));

	/// <summary>Точка экрана изъ lParam: двѣ знаковыя половины — лѣвѣе или выше главнаго экрана онѣ отрицательныя.
	/// unchecked — въ отладкѣ включена провѣрка переполненія, а (short) отъ половины больше 32767 — какъ разъ оно.</summary>
	static Point ScreenPoint(nint lParam) => unchecked(new((short)lParam, (short)(lParam >> 16)));

	/// <summary>Край, за который тянуть, или null — не у края. Кругъ: по четверти, гдѣ точка; уголокъ — правый нижній уголъ.</summary>
	int? Edge(Point p)
	{
		const int HTTOPLEFT = 13, HTTOPRIGHT = 14, HTBOTTOMLEFT = 16, HTBOTTOMRIGHT = 17;
		float grip = Px(8), half = ClientSize.Width / 2f, dx = p.X - half, dy = p.Y - half;
		if (dx > 0 && dy > 0) // уголокъ
			return p.X >= ClientSize.Width - grip || p.Y >= ClientSize.Height - grip ? HTBOTTOMRIGHT : null;
		if (dx * dx + dy * dy < (half - grip) * (half - grip)) return null;
		return dy < 0 ? dx < 0 ? HTTOPLEFT : HTTOPRIGHT : HTBOTTOMLEFT;
	}

	/// <summary>WM_SIZING: прямоугольникъ, который предлагаетъ Windows, — въ квадратъ въ предѣлахъ окна; противоположный уголъ стоитъ на мѣстѣ.</summary>
	void KeepSquare(int edge, nint rect)
	{
		const int WMSZ_LEFT = 1, WMSZ_RIGHT = 2, WMSZ_TOP = 3, WMSZ_TOPLEFT = 4, WMSZ_TOPRIGHT = 5, WMSZ_BOTTOM = 6, WMSZ_BOTTOMLEFT = 7;
		RECT r = Marshal.PtrToStructure<RECT>(rect);
		int width = r.Right - r.Left, height = r.Bottom - r.Top;
		int side = edge is WMSZ_LEFT or WMSZ_RIGHT ? width : edge is WMSZ_TOP or WMSZ_BOTTOM ? height : (width + height) / 2;
		side = Math.Clamp(side, MinimumSize.Width, MaximumSize.Width);
		if (edge is WMSZ_LEFT or WMSZ_TOPLEFT or WMSZ_BOTTOMLEFT) r.Left = r.Right - side; else r.Right = r.Left + side;
		if (edge is WMSZ_TOP or WMSZ_TOPLEFT or WMSZ_TOPRIGHT) r.Top = r.Bottom - side; else r.Bottom = r.Top + side;
		Marshal.StructureToPtr(r, rect, false);
	}

	struct RECT { public int Left, Top, Right, Bottom; }

	// ───── мышь и клавиши: кнопки — не окна, ихъ ведётъ само окно ─────

	protected override void OnMouseMove(MouseEventArgs e)
	{
		base.OnMouseMove(e);
		SetHover(ButtonAt(e.Location));
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		base.OnMouseLeave(e);
		SetHover(null);
	}

	void SetHover(PanelButton? button)
	{
		if (Hover == button) return;
		Hover = button;
		Cursor = button is { Enabled: true } ? Cursors.Hand : Cursors.Default;
		toolTip.SetToolTip(this, button?.Tip); // подсказка — той кнопки, что подъ мышью
		Redraw();
	}

	protected override void OnMouseDown(MouseEventArgs e)
	{
		base.OnMouseDown(e);
		if (e.Button == MouseButtons.Left) Pressed = ButtonAt(e.Location);
	}

	protected override void OnMouseUp(MouseEventArgs e)
	{
		base.OnMouseUp(e);
		if (e.Button == MouseButtons.Right) // и по кнопкѣ — то же меню
		{
			menu.Show(this, e.Location);
			return;
		}
		PanelButton? pressed = Pressed;
		Pressed = null;
		if (e.Button == MouseButtons.Left && pressed is { Enabled: true } && pressed == ButtonAt(e.Location))
			pressed.Click?.Invoke();
	}

	/// <summary>Tab и Shift+Tab — по кнопкамъ по кругу (видимымъ и доступнымъ), Enter и пробѣлъ — нажать.</summary>
	protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
	{
		switch (keyData)
		{
			case Keys.Tab or (Keys.Tab | Keys.Shift):
				PanelButton[] usable = [.. Buttons.Where(b => b.Visible && b.Enabled)];
				if (usable.Length == 0) return true;
				int i = FocusedButton is null ? -1 : Array.IndexOf(usable, FocusedButton);
				i = keyData == Keys.Tab ? (i + 1) % usable.Length : i <= 0 ? usable.Length - 1 : i - 1;
				FocusedButton = usable[i];
				KeyboardCues = true;
				AccessibilityNotifyClients(AccessibleEvents.Focus, Buttons.Where(b => b.Visible).ToList().IndexOf(FocusedButton));
				Redraw();
				return true;
			case Keys.Enter or Keys.Space when FocusedButton is { Visible: true, Enabled: true } focused:
				focused.Click?.Invoke();
				return true;
		}
		return base.ProcessCmdKey(ref msg, keyData);
	}

	protected override void OnActivated(EventArgs e) { base.OnActivated(e); Redraw(); }   // рамка фокуса — только у активнаго окна
	protected override void OnDeactivate(EventArgs e) { base.OnDeactivate(e); Redraw(); }

	// ───── состояніе ─────

	void Main_StateChanged(State? state)
	{
		Current = state;
		if (state is null) ShowTarget = false;
		UpdateView();
	}

	bool Linked => Current is not null;
	bool On => PendingPower ?? Current?.dehumidifier == true;
	byte? Mode => PendingMode ?? Current?.dehumidifier_mode;

	/// <summary>Цѣль, которую показываемъ: поставленная только что, иначе послѣдняя извѣстная для режима, иначе изъ опроса.
	/// Режимъ только что смѣнили, а цѣль его ещё неизвѣстна — null («− −»), а не цѣль прежняго режима изъ опроса.</summary>
	byte? Target => PendingTarget ?? (Mode is { } mode ? Main.KnownTarget(mode) : null)
		?? (PendingMode is null || PendingMode == Current?.dehumidifier_mode ? Current?.dehumidifier_target_humidity : null);

	void UpdateView()
	{
		bool linked = Linked, on = On;
		foreach (PanelButton button in (PanelButton[])[buttonPower, buttonAuto, buttonNight, buttonDry])
			button.Enabled = linked;
		buttonPower.Lit = on;
		(PanelButton Button, byte Mode)[] modes = [(buttonAuto, 0), (buttonNight, 1), (buttonDry, DryMode)];
		foreach ((PanelButton button, byte mode) in modes)
			button.Lit = button.Selected = linked && on && Mode == mode;
		buttonWifi.Visible = !linked;
		string tip = IsNullOrWhiteSpace(Main.StatusText) ? PanelNoLink : $"{PanelNoLink}\n{Main.StatusText}";
		buttonWifi.Tip = tip;
		if (Hover == buttonWifi) toolTip.SetToolTip(this, tip);
		if (FocusedButton is { Visible: false }) FocusedButton = null;
		Redraw();
	}

	static bool IsNullOrWhiteSpace(string text) => string.IsNullOrWhiteSpace(text);

	/// <summary>Что на экранѣ: «− −» безъ связи; цѣль, пока её показываемъ (кромѣ сушки бѣлья — у неё цѣли нѣтъ); иначе влажность въ комнатѣ.</summary>
	string DisplayText()
	{
		if (Current is not { } state) return "--";
		byte? value = ShowTarget && Mode != DryMode ? Target : state.environment_relative_umidity;
		return value is not { } v ? "--" : v >= 10 ? $"{Min((int)v, 99)}" : $" {v}";
	}

	// ───── кнопки ─────

	void Power_Click()
	{
		if (!Linked) return;
		bool on = !On;
		PendingPower = on;
		ShowTarget = false;
		Run(() => Main.PowerAsync(on));
	}

	/// <summary>Режимъ: выключенъ или режимъ другой — включить и поставить его, на экранѣ — его цѣль;
	/// тотъ же режимъ — показать цѣль, а если она уже на экранѣ — слѣдующая цѣль. У сушки бѣлья цѣли нѣтъ.</summary>
	void Mode_Click(byte mode)
	{
		if (!Linked) return;
		if (!On || Mode != mode)
		{
			PendingPower = true;
			PendingMode = mode;
			PendingTarget = null;
			ShowTarget = mode != DryMode;
			Run(() => Main.ModeAsync(mode));
		}
		else if (mode != DryMode)
		{
			if (!ShowTarget) ShowTarget = true;
			else if (Target is { } target)
			{
				byte next = NextTarget(target);
				PendingTarget = next;
				StartFlash();
				Run(() => Main.TargetAsync(next));
			}
		}
		revertTimer.Stop();
		revertTimer.Start();
		UpdateView();
	}

	/// <summary>Слѣдующая цѣль: вверхъ по 5 въ рекомендуемыхъ 40…70, послѣ 70 — снова 40; нестандартная — къ ближайшей большей кратной 5.</summary>
	internal static byte NextTarget(byte target) => target < 40 || target >= 70 ? (byte)40 : (byte)((target / 5 + 1) * 5);

	/// <summary>Команда черезъ большое окно (оно же потомъ перечитываетъ состояніе); ожидаемое — пока не кончится послѣдняя.</summary>
	async void Run(Func<Task> command)
	{
		Running++;
		UpdateView();
		try
		{
			await command();
		}
		finally
		{
			if (--Running == 0)
			{
				PendingPower = null;
				PendingMode = null;
				PendingTarget = null;
			}
			UpdateView();
		}
	}

	void StartFlash()
	{
		FlashStart = DateTime.Now;
		Flash = 1;
		flashTimer.Start();
	}

	void FlashTimer_Tick(object? sender, EventArgs e)
	{
		Flash = Max(0, 1 - (float)(DateTime.Now - FlashStart).TotalMilliseconds / 450);
		if (Flash <= 0) flashTimer.Stop();
		Redraw();
	}

	// ───── рисованіе ─────

	static bool Contrast => HighContrast;
	internal static Color Pale => Contrast ? SystemColors.GrayText : FromArgb(0xC7, 0xC7, 0xCC);
	internal static Color Mid  => Contrast ? SystemColors.WindowText : FromArgb(0x8E, 0x8E, 0x93);
	internal static Color Dark => Contrast ? SystemColors.WindowText : FromArgb(0x3A, 0x3A, 0x3C);

	// окно слоистое: Windows беретъ готовую картинку съ прозрачностью (UpdateLayeredWindow), WM_PAINT не нуженъ
	protected override void OnPaintBackground(PaintEventArgs e) { }
	protected override void OnPaint(PaintEventArgs e) { }

	/// <summary>Нарисовать всё въ картинку съ прозрачностью и отдать окну: край сглаженъ, внѣ формы — прозрачно (и мышь проходитъ насквозь);
	/// смѣна картинки — разомъ, безъ мерцанія.</summary>
	void Redraw()
	{
		if (!IsHandleCreated || ClientSize.Width <= 0) return;
		using Bitmap frame = new(ClientSize.Width, ClientSize.Height, PixelFormat.Format32bppArgb);
		using (Graphics g = Graphics.FromImage(frame))
			Draw(g);
		nint screen = GetDC(0), memory = CreateCompatibleDC(screen), bitmap = frame.GetHbitmap(Empty /* ARGB 0: прозрачность остаётся */), old = SelectObject(memory, bitmap);
		try
		{
			SIZE size = new(frame.Width, frame.Height);
			POINT source = default;
			BLENDFUNCTION blend = new() { BlendOp = 0 /*AC_SRC_OVER*/, SourceConstantAlpha = 255, AlphaFormat = 1 /*AC_SRC_ALPHA*/ };
			_ = UpdateLayeredWindow(Handle, screen, 0, ref size, memory, ref source, 0, ref blend, 2 /*ULW_ALPHA*/);
		}
		finally
		{
			SelectObject(memory, old);
			DeleteObject(bitmap);
			DeleteDC(memory);
			_ = ReleaseDC(0, screen);
		}
	}

	void Draw(Graphics g)
	{
		g.SmoothingMode = SmoothingMode.AntiAlias;
		// фонъ окна съ тонкою обводкою
		using (GraphicsPath shape = Outline(S(0.6f)))
		{
			using Brush back = Contrast ? new SolidBrush(SystemColors.Window)
				: new LinearGradientBrush(ClientRectangle, FromArgb(0xFD, 0xFD, 0xFE), FromArgb(0xF0, 0xF0, 0xF3), LinearGradientMode.Vertical);
			g.FillPath(back, shape);
			using Pen edge = new(Contrast ? SystemColors.WindowFrame : FromArgb(0xD1, 0xD1, 0xD6), Max(1, S(1.2f)));
			g.DrawPath(edge, shape);
		}
		// дискъ, какъ верхъ осушителя
		RectangleF disc = new(S(8), S(8), S(184), S(184));
		if (!Contrast)
		{
			using LinearGradientBrush fill = new(disc, FromArgb(0xF7, 0xF7, 0xF9), FromArgb(0xE6, 0xE6, 0xEA), LinearGradientMode.Vertical);
			g.FillEllipse(fill, disc);
		}
		using (Pen rim = new(Contrast ? SystemColors.WindowText : FromArgb(0xD8, 0xD8, 0xDD), S(1)))
			g.DrawEllipse(rim, disc);
		DrawDigits(g, DisplayText());
		bool cues = KeyboardCues && ContainsFocus;
		foreach (PanelButton button in Buttons)
			if (button.Visible)
				button.Draw(g, button == Hover, cues && button == FocusedButton);
	}

	/// <summary>Сегменты каждой цифры: a b c d e f g (верхъ, правый верхъ, правый низъ, низъ, лѣвый низъ, лѣвый верхъ, середина).</summary>
	static readonly Dictionary<char, string> Segments = new()
	{
		{ '0', "abcdef" }, { '1', "bc" }, { '2', "abdeg" }, { '3', "abcdg" }, { '4', "bcfg" },
		{ '5', "acdfg" }, { '6', "acdefg" }, { '7', "abc" }, { '8', "abcdefg" }, { '9', "abcdfg" }, { '-', "g" }, { ' ', "" },
	};

	/// <summary>Двѣ тонкія сегментныя цифры, какъ на осушителѣ: отрѣзки — линіи со скруглёнными концами и зазорами на стыкахъ.
	/// Flash — «подбѣливаніе»: свѣтлый ореолъ подъ цифрами и сами цифры свѣтлѣе.</summary>
	void DrawDigits(Graphics g, string text)
	{
		float w = S(30), h = S(52), gap = S(14), top = S(34), left = S(100) - (2 * w + gap) / 2;
		Color ink = Linked ? Dark : Pale;
		if (Flash > 0) ink = Blend(ink, White, 0.6f * Flash);
		using Pen pen = new(ink, S(3.2f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
		using Pen glow = new(FromArgb((int)(220 * Flash), White), S(9)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
		for (int i = 0; i < 2 && i < text.Length; i++)
		{
			float x = left + i * (w + gap);
			if (Flash > 0) DrawSegments(g, glow, text[i], x, top, w, h);
			DrawSegments(g, pen, text[i], x, top, w, h);
		}
	}

	void DrawSegments(Graphics g, Pen pen, char digit, float x0, float y0, float w, float h)
	{
		if (!Segments.TryGetValue(digit, out string? on)) return;
		float x1 = x0 + w, y1 = y0 + h, ym = y0 + h / 2, d = S(2.6f); // d — зазоръ на стыкѣ
		foreach (char s in on)
			switch (s)
			{
				case 'a': g.DrawLine(pen, x0 + d, y0, x1 - d, y0); break;
				case 'b': g.DrawLine(pen, x1, y0 + d, x1, ym - d); break;
				case 'c': g.DrawLine(pen, x1, ym + d, x1, y1 - d); break;
				case 'd': g.DrawLine(pen, x0 + d, y1, x1 - d, y1); break;
				case 'e': g.DrawLine(pen, x0, ym + d, x0, y1 - d); break;
				case 'f': g.DrawLine(pen, x0, y0 + d, x0, ym - d); break;
				case 'g': g.DrawLine(pen, x0 + d, ym, x1 - d, ym); break;
			}
	}

	static Color Blend(Color a, Color b, float t) => FromArgb(
		(int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));

	// ───── значки ─────

	/// <summary>Шрифтъ значковъ: Segoe Fluent Icons (Windows 11), иначе Segoe MDL2 Assets (Windows 10) — коды тѣ же.</summary>
	static readonly string IconFont = new InstalledFontCollection().Families.Any(f => f.Name == "Segoe Fluent Icons") ? "Segoe Fluent Icons" : "Segoe MDL2 Assets";

	static void DrawIcon(Graphics g, RectangleF r, Color ink, string glyph)
	{
		using Font font = new(IconFont, r.Height * 0.62f, GraphicsUnit.Pixel);
		DrawCentered(g, glyph, font, r, ink);
	}

	/// <summary>Текстъ по серединѣ — сглаживаніемъ безъ ClearType: на прозрачной кнопкѣ ClearType даётъ цвѣтную кайму.</summary>
	static void DrawCentered(Graphics g, string text, Font font, RectangleF r, Color ink)
	{
		TextRenderingHint hint = g.TextRenderingHint;
		g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
		using SolidBrush brush = new(ink);
		using StringFormat center = new(StringFormat.GenericTypographic) { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
		g.DrawString(text, font, brush, r, center);
		g.TextRenderingHint = hint;
	}

	/// <summary>Авто (умный режимъ): капля съ буквою «A» — влажность держится сама.</summary>
	static void DrawAuto(Graphics g, RectangleF r, Color ink)
	{
		float u = r.Width / 16f, cx = r.X + r.Width / 2;
		using Pen pen = new(ink, Max(1, 1.1f * u)) { LineJoin = LineJoin.Round };
		using GraphicsPath drop = new();
		float radius = 5.2f * u, cy = r.Y + 10.2f * u, tip = r.Y + 1.2f * u;
		float a = (float)(Acos(radius / (cy - tip)) * 180 / PI);
		drop.AddArc(cx - radius, cy - radius, 2 * radius, 2 * radius, -90 + a, 360 - 2 * a);
		drop.AddLine(drop.GetLastPoint(), new PointF(cx, tip));
		drop.CloseFigure();
		g.DrawPath(pen, drop);
		using Font font = new("Segoe UI Semibold", 6.4f * u, GraphicsUnit.Pixel);
		DrawCentered(g, "A", font, new RectangleF(cx - 4 * u, cy - 4.2f * u, 8 * u, 8 * u), ink);
	}

	/// <summary>Сушка бѣлья: футболка контуромъ.</summary>
	static void DrawShirt(Graphics g, RectangleF r, Color ink)
	{
		PointF P(float x, float y) => new(r.X + x * r.Width, r.Y + y * r.Height);
		using Pen pen = new(ink, Max(1, r.Width / 16f * 1.1f)) { LineJoin = LineJoin.Round };
		using GraphicsPath shirt = new();
		shirt.AddLines([P(0.36f, 0.17f), P(0.17f, 0.25f), P(0.05f, 0.43f), P(0.18f, 0.51f), P(0.25f, 0.45f),
		                P(0.25f, 0.88f), P(0.75f, 0.88f), P(0.75f, 0.45f), P(0.82f, 0.51f), P(0.95f, 0.43f), P(0.83f, 0.25f), P(0.64f, 0.17f)]);
		shirt.AddBezier(P(0.64f, 0.17f), P(0.60f, 0.27f), P(0.40f, 0.27f), P(0.36f, 0.17f)); // воротъ
		shirt.CloseFigure();
		g.DrawPath(pen, shirt);
	}

	// ───── слоистое окно ─────

	protected override CreateParams CreateParams
	{
		get
		{
			CreateParams p = base.CreateParams;
			p.ExStyle |= 0x80000; // WS_EX_LAYERED: окно съ прозрачностью по точкамъ
			return p;
		}
	}

	[StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
	[StructLayout(LayoutKind.Sequential)] record struct SIZE(int Width, int Height);
	[StructLayout(LayoutKind.Sequential, Pack = 1)] struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }

	[DllImport("user32", ExactSpelling = true)]
	static extern bool UpdateLayeredWindow(nint hwnd, nint hdcDst, nint pptDst, ref SIZE psize, nint hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);
	[DllImport("user32", ExactSpelling = true)] static extern nint GetDC(nint hwnd);
	[DllImport("user32", ExactSpelling = true)] static extern int ReleaseDC(nint hwnd, nint hdc);
	[DllImport("gdi32", ExactSpelling = true)] static extern nint CreateCompatibleDC(nint hdc);
	[DllImport("gdi32", ExactSpelling = true)] static extern bool DeleteDC(nint hdc);
	[DllImport("gdi32", ExactSpelling = true)] static extern nint SelectObject(nint hdc, nint obj);
	[DllImport("gdi32", ExactSpelling = true)] static extern bool DeleteObject(nint obj);

	// ───── доступность: кружки — дѣти окна для экранныхъ чтецовъ ─────

	protected override AccessibleObject CreateAccessibilityInstance() => new PanelAccessible(this);

	sealed class PanelAccessible(PanelForm panel) : ControlAccessibleObject(panel)
	{
		PanelButton[] Shown => [.. panel.Buttons.Where(b => b.Visible)];
		public override int GetChildCount() => Shown.Length;
		public override AccessibleObject? GetChild(int index) => index >= 0 && index < Shown.Length ? new ButtonAccessible(panel, Shown[index], this) : null;
	}

	sealed class ButtonAccessible(PanelForm panel, PanelButton button, AccessibleObject parent) : AccessibleObject
	{
		public override string? Name { get => button.Tip; set { } }
		public override AccessibleRole Role => AccessibleRole.PushButton;
		public override AccessibleObject Parent => parent;
		public override Rectangle Bounds => panel.RectangleToScreen(button.Bounds);
		public override AccessibleStates State =>
			(button.Enabled ? AccessibleStates.Focusable : AccessibleStates.Unavailable)
			| (button.Selected || button.Lit ? AccessibleStates.Pressed : 0)
			| (panel.FocusedButton == button && panel.ContainsFocus ? AccessibleStates.Focused : 0);
		public override string DefaultAction => "Press";
		public override void DoDefaultAction() { if (button.Enabled) button.Click?.Invoke(); }
	}
}

/// <summary>Кнопка панели — не окно, а мѣсто на ней: слоистое окно не показываетъ дочернихъ оконъ, всё рисуетъ само.
/// Кружокъ съ тонкимъ контуромъ и значкомъ (или просто значокъ). Lit — значокъ и контуръ темнѣе (включёнъ, текущій режимъ);
/// Selected — второе кольцо вокругъ (выбранный режимъ); Faint — блёклая, темнѣе при наведеніи (гаечный ключъ).</summary>
sealed class PanelButton
{
	public Action<Graphics, RectangleF, Color>? Glyph;
	public Action? Click;
	public string Tip = "";
	public Rectangle Bounds;
	public bool Circle = true, Faint, Lit, Selected, Enabled = true, Visible = true;

	public bool Contains(Point p) => Visible && (Circle
		? Pow(p.X - (Bounds.X + Bounds.Width / 2f), 2) + Pow(p.Y - (Bounds.Y + Bounds.Height / 2f), 2) <= Pow(Bounds.Width / 2f, 2)
		: Bounds.Contains(p));

	/// <summary>Рисуетъ себя на мѣстѣ Bounds; размѣры колецъ — въ долѣ кружка: растётъ окно — растутъ и кольца.</summary>
	public void Draw(Graphics g, bool hover, bool focused)
	{
		GraphicsState saved = g.Save();
		g.TranslateTransform(Bounds.X, Bounds.Y);
		float w = Bounds.Width, scale = w / 40f;
		Color ink = !Enabled ? PanelForm.Pale
			: Faint ? (hover ? PanelForm.Mid : PanelForm.Pale)
			: Lit ? PanelForm.Dark
			: hover ? PanelForm.Dark : PanelForm.Mid;
		RectangleF glyph;
		if (Circle)
		{
			float inset = 5 * scale; // мѣсто для кольца выбраннаго режима
			RectangleF round = new(inset, inset, w - 2 * inset, w - 2 * inset);
			if (Lit && !SystemInformation.HighContrast)
			{
				using SolidBrush fill = new(FromArgb(200, White));
				g.FillEllipse(fill, round);
			}
			using (Pen pen = new(Lit ? PanelForm.Dark : Enabled && hover ? PanelForm.Mid : PanelForm.Pale, 1.2f * scale))
				g.DrawEllipse(pen, round);
			if (Selected)
			{
				using Pen ring = new(PanelForm.Dark, 1.4f * scale);
				g.DrawEllipse(ring, 1.2f * scale, 1.2f * scale, w - 2.4f * scale, w - 2.4f * scale);
			}
			float g0 = round.Width * 0.24f;
			glyph = RectangleF.Inflate(round, -g0, -g0);
		}
		else glyph = new(0, 0, w, Bounds.Height);
		Glyph?.Invoke(g, glyph, ink);
		if (focused)
		{
			using Pen focus = new(PanelForm.Mid) { DashStyle = DashStyle.Dot };
			if (Circle) g.DrawEllipse(focus, 0.5f, 0.5f, w - 1, w - 1);
			else g.DrawRectangle(focus, 0, 0, w - 1, Bounds.Height - 1);
		}
		g.Restore(saved);
	}
}

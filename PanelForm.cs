using System.Drawing.Drawing2D;
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
		DoubleBuffered = true;
		AccessibleName = main.Text;

		buttonPower = Add(PanelPower, (g, r, ink) => DrawIcon(g, r, ink, ""), Power_Click);
		buttonAuto  = Add(Capital(ModeSmart), DrawAuto, (_, _) => Mode_Click(0));
		buttonNight = Add(Capital(ModeSleep), (g, r, ink) => DrawIcon(g, r, ink, ""), (_, _) => Mode_Click(1));
		buttonDry   = Add(Capital(ModeDry), DrawShirt, (_, _) => Mode_Click(DryMode));
		buttonWifi  = Add(PanelNoLink, (g, r, ink) => DrawIcon(g, r, ink, ""), (_, _) => wifiMenu.Show(buttonWifi!, new Point(0, buttonWifi!.Height)));
		buttonMain  = Add(PanelBigWindow, (g, r, ink) => DrawIcon(g, r, ink, ""), (_, _) => ShowMain());
		buttonWifi.Circle = buttonMain.Circle = false;
		buttonMain.Faint = true;

		menu.Items.Add(PanelBigWindow, null, (_, _) => ShowMain());
		menu.Items.Add(new ToolStripSeparator());
		menu.Items.Add(PanelExit, null, (_, _) => Close());
		wifiMenu.Items.Add(Connect, null, async (_, _) => await Main.ReconnectAsync());
		wifiMenu.Items.Add(PanelAddress, null, (_, _) => { ShowMain(); Main.ShowConnectionEditor(); });
		ContextMenuStrip = menu;

		revertTimer.Tick += (_, _) => { revertTimer.Stop(); ShowTarget = false; Invalidate(); };
		flashTimer.Tick += FlashTimer_Tick;
		Main.StateChanged += Main_StateChanged;
		Current = Main.LastState;
		UpdateView();

		PanelButton Add(string tip, Action<Graphics, RectangleF, Color> glyph, EventHandler click)
		{
			PanelButton button = new() { Glyph = glyph, AccessibleName = tip };
			button.Click += click;
			toolTip.SetToolTip(button, tip);
			Controls.Add(button);
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
		int size = (int)S(40); // кружокъ 30 + мѣсто для кольца выбраннаго режима
		Rectangle At(float x, float y, int side) => new((int)(S(x) - side / 2f), (int)(S(y) - side / 2f), side, side);
		buttonAuto .Bounds = At(56, 110, size);
		buttonNight.Bounds = At(100, 110, size);
		buttonDry  .Bounds = At(144, 110, size);
		buttonPower.Bounds = At(100, 154, size);
		buttonWifi .Bounds = At(36, 66, (int)S(24));
		buttonMain .Bounds = At(181, 181, (int)S(20));
		using GraphicsPath shape = Outline(0);
		Region = new(shape);
		Invalidate(true);
	}

	protected override void OnHandleCreated(EventArgs e)
	{
		base.OnHandleCreated(e);
		int square = 1; // DWMWCP_DONOTROUND: форму задаётъ Region, скругленіе Windows 11 ей не нужно
		_ = DwmSetWindowAttribute(Handle, 33 /*DWMWA_WINDOW_CORNER_PREFERENCE*/, ref square, sizeof(int));
		SetLimits();
		int side = Px(Math.Clamp(Settings.Default.PanelSize, MinSide, MaxSide));
		ClientSize = new(side, side);
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
		const int WM_NCHITTEST = 0x84, WM_CONTEXTMENU = 0x7B, WM_SIZING = 0x214, HTCLIENT = 1, HTCAPTION = 2;
		switch (m.Msg)
		{
			case WM_CONTEXTMENU:
				Point at = m.LParam == -1 ? PointToScreen(new(Width / 2, Height / 2)) : ScreenPoint(m.LParam);
				menu.Show(at);
				return;
			case WM_SIZING:
				KeepSquare((int)m.WParam, m.LParam);
				m.Result = 1;
				return;
		}
		base.WndProc(ref m);
		if (m.Msg == WM_NCHITTEST && m.Result == HTCLIENT)
			m.Result = Edge(PointToClient(ScreenPoint(m.LParam))) ?? HTCAPTION;
	}

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
		toolTip.SetToolTip(buttonWifi, tip);
		foreach (Control control in Controls) control.Invalidate();
		Invalidate();
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

	void Power_Click(object? sender, EventArgs e)
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
		Invalidate();
	}

	// ───── рисованіе ─────

	static bool Contrast => HighContrast;
	internal static Color Pale => Contrast ? SystemColors.GrayText : FromArgb(0xC7, 0xC7, 0xCC);
	internal static Color Mid  => Contrast ? SystemColors.WindowText : FromArgb(0x8E, 0x8E, 0x93);
	internal static Color Dark => Contrast ? SystemColors.WindowText : FromArgb(0x3A, 0x3A, 0x3C);

	protected override void OnPaintBackground(PaintEventArgs e)
	{
		if (Contrast)
		{
			e.Graphics.Clear(SystemColors.Window);
			return;
		}
		if (ClientSize.Width <= 0) return;
		using LinearGradientBrush back = new(ClientRectangle, FromArgb(0xFD, 0xFD, 0xFE), FromArgb(0xF0, 0xF0, 0xF3), LinearGradientMode.Vertical);
		e.Graphics.FillRectangle(back, ClientRectangle);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics;
		g.SmoothingMode = SmoothingMode.AntiAlias;
		// тонкая обводка окна; край Region зубчатый — сглаженная обводка по нему его прячетъ
		using (Pen edge = new(Contrast ? SystemColors.WindowFrame : FromArgb(0xD1, 0xD1, 0xD6), Max(1, S(1.2f))))
		using (GraphicsPath frame = Outline(S(0.6f)))
			g.DrawPath(edge, frame);
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
		float w = S(26), h = S(46), gap = S(12), top = S(32), left = S(100) - (2 * w + gap) / 2;
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

	[DllImport("dwmapi", ExactSpelling = true)]
	static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);
}

/// <summary>Кнопка панели: кружокъ съ тонкимъ контуромъ и значкомъ (или просто значокъ). Lit — значокъ и контуръ темнѣе (включёнъ, текущій режимъ);
/// Selected — второе кольцо вокругъ (выбранный режимъ); Faint — блёклая, темнѣе при наведеніи (гаечный ключъ).</summary>
sealed class PanelButton : Control
{
	public Action<Graphics, RectangleF, Color>? Glyph;
	public bool Circle = true;
	public bool Faint;
	bool hover;

	[System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
	public bool Lit { get; set { if (field != value) { field = value; Invalidate(); } } }
	[System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
	public bool Selected { get; set { if (field != value) { field = value; Invalidate(); } } }

	public PanelButton()
	{
		SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
		         ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.StandardClick, true);
		BackColor = Color.Transparent;
		TabStop = true;
		Cursor = Cursors.Hand;
		AccessibleRole = AccessibleRole.PushButton;
	}

	protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover = true; Invalidate(); }
	protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = false; Invalidate(); }
	protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
	protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
	protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }

	protected override void OnKeyDown(KeyEventArgs e)
	{
		base.OnKeyDown(e);
		if (e.KeyCode is Keys.Enter or Keys.Space)
		{
			OnClick(EventArgs.Empty);
			e.Handled = true;
		}
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics;
		g.SmoothingMode = SmoothingMode.AntiAlias;
		float scale = Width / 40f, w = Width; // размѣры — въ долѣ кружка: растётъ окно — растутъ и кольца
		Color ink = !Enabled ? PanelForm.Pale
			: Faint ? (hover ? PanelForm.Mid : PanelForm.Pale)
			: Lit ? PanelForm.Dark
			: hover ? PanelForm.Dark : PanelForm.Mid;
		RectangleF glyph;
		if (Circle)
		{
			float inset = 5 * scale; // мѣсто для кольца выбраннаго режима
			RectangleF round = new(inset, inset, w - 2 * inset - 1, w - 2 * inset - 1);
			if (Lit && !SystemInformation.HighContrast)
			{
				using SolidBrush fill = new(Color.FromArgb(200, Color.White));
				g.FillEllipse(fill, round);
			}
			using (Pen pen = new(Lit ? PanelForm.Dark : Enabled && hover ? PanelForm.Mid : PanelForm.Pale, 1.2f * scale))
				g.DrawEllipse(pen, round);
			if (Selected)
			{
				using Pen ring = new(PanelForm.Dark, 1.4f * scale);
				g.DrawEllipse(ring, 1.2f * scale, 1.2f * scale, w - 2.4f * scale - 1, w - 2.4f * scale - 1);
			}
			float g0 = round.Width * 0.24f;
			glyph = RectangleF.Inflate(round, -g0, -g0);
		}
		else glyph = new(0, 0, w, Height);
		Glyph?.Invoke(g, glyph, ink);
		if (Focused && ShowFocusCues)
		{
			using Pen focus = new(PanelForm.Mid) { DashStyle = DashStyle.Dot };
			if (Circle) g.DrawEllipse(focus, 0.5f, 0.5f, w - 2, w - 2);
			else g.DrawRectangle(focus, 0, 0, w - 1, Height - 1);
		}
	}
}

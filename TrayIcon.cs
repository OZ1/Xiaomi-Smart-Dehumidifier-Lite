using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace DehumidifierControl;

using static SystemInformation;
using static SystemEvents;
using static Graphics;

using HICON = nint;

/// <summary>Значокъ въ треѣ, нарисованный въ событіи Paint. NotifyIcon запечатанъ — поэтому онъ внутри, а не предокъ.
/// Invalidate даётъ обработчику Paint чистую картинку размѣра SmallIconSize, дѣлаетъ изъ нея HICON и самъ его освобождаетъ.
/// Перерисовывается самъ, когда смѣнилась тема панели задачъ.</summary>
[DefaultEvent(nameof(Paint))]
sealed class TrayIcon : Component
{
	public event PaintEventHandler? Paint;
	public event MouseEventHandler? MouseClick;

	readonly NotifyIcon NotifyIcon = new();
	HICON hIcon; // Icon.FromHandle его не освобождаетъ — освобождаемъ сами, когда замѣняемъ

	public TrayIcon(IContainer container) : this() => container.Add(this);
	public TrayIcon()
	{
		NotifyIcon.MouseClick += Icon_MouseClick;
		UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
	}

	/// <summary>Подсказка; длиннѣе 127 знаковъ Windows не принимаетъ — обрѣзается.</summary>
	[Localizable(true), DefaultValue("")]
	public string Text
	{
		get => NotifyIcon.Text;
		set => NotifyIcon.Text = value.Length <= 127 ? value : value[..127];
	}

	[DefaultValue(false)]
	public bool Visible
	{
		get => NotifyIcon.Visible;
		set => NotifyIcon.Visible = value;
	}

	[DefaultValue(null)]
	public ContextMenuStrip? ContextMenuStrip
	{
		get => NotifyIcon.ContextMenuStrip;
		set => NotifyIcon.ContextMenuStrip = value;
	}

	/// <summary>Панель задачъ свѣтлая (иначе тёмная) — отъ этого, напримѣръ, цвѣтъ ободка; смѣнилась — перерисовать.</summary>
	[Browsable(false)]
	public bool LightTheme { get; private set
	{
		if (field == value) return;
		else field = value;
		Invalidate();
	}}        = SystemUsesLightTheme;
	static bool SystemUsesLightTheme => Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "SystemUsesLightTheme", 0) is 1;

	/// <summary>Перерисовать: Paint на свѣжей прозрачной картинкѣ — и въ трей.</summary>
	public void Invalidate()
	{
		Size size = SmallIconSize;
		using Bitmap bitmap = new(size.Width, size.Height);
		using (Graphics g = FromImage(bitmap))
		using (PaintEventArgs e = new(g, new(default, size)))
			Paint?.Invoke(this, e);
		HICON hOld = hIcon;
		hIcon = bitmap.GetHicon();
		NotifyIcon.Icon = Icon.FromHandle(hIcon);
		DestroyIcon(hOld);
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			UserPreferenceChanged -= SystemEvents_UserPreferenceChanged; // событіе статическое: безъ отписки держитъ значокъ въ памяти
			NotifyIcon.Dispose();
		}
		DestroyIcon(hIcon);
		hIcon = 0;
		base.Dispose(disposing);
	}

	[SuppressMessage("Interoperability", "SYSLIB1054: Используйте LibraryImportAttribute вместо DllImportAttribute для генерирования кода маршализации P/Invoke во время компиляции")]
	[DllImport("User32", ExactSpelling = true)]
	static extern bool DestroyIcon(HICON hIcon);

	/// <summary>Смѣна темы (WM_SETTINGCHANGE съ «ImmersiveColorSet») — перерисовать сразу, если панель задачъ стала свѣтлѣе или темнѣе.
	/// Вызывается въ UI-потокѣ: SystemEvents шлётъ въ контекстъ подписавшагося, а значокъ создаётся въ InitializeComponent окна.</summary>
	void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
	{
		LightTheme = SystemUsesLightTheme;
	}

	void Icon_MouseClick(object? sender, MouseEventArgs e)
	{
		MouseClick?.Invoke(this, e);
	}
}

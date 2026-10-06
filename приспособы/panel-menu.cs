#:project ..\Dehumidifier.csproj
#:property TargetFramework=net10.0-windows
#:property UseWindowsForms=true
#:property PublishAot=false
#:property PublishTrimmed=false
// Правый щелчокъ по свободному мѣсту панели («заголовокъ»): открывается ли меню и остаётся ли на экранѣ.
// Панель ненадолго показывается въ углу экрана. Съ аргументомъ ctx вмѣсто щелчка шлётся WM_CONTEXTMENU (какъ клавиша меню).
// dotnet run --file приспособы\panel-menu.cs [ctx]
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using DehumidifierControl;

ApplicationConfiguration.Initialize();
using MainForm main = new();
using PanelForm panel = new(main, () => { });
panel.StartPosition = FormStartPosition.Manual;
panel.Location = new(30, 30);
ContextMenuStrip menu = (ContextMenuStrip)typeof(PanelForm).GetField("menu", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(panel)!;
menu.Opened += (_, _) => Console.WriteLine($"{DateTime.Now:ss.fff} меню открыто {menu.Bounds}");
menu.Closed += (_, e) => Console.WriteLine($"{DateTime.Now:ss.fff} меню закрыто: {e.CloseReason}");
panel.Show();
Application.DoEvents();
Point at = panel.PointToScreen(new(panel.Width / 2, panel.Height / 5));
nint lParam = (at.Y << 16) | (at.X & 0xFFFF);
Console.WriteLine($"WM_NCHITTEST → {SendMessage(panel.Handle, 0x84, 0, lParam)} (2 — заголовокъ)");
if (args is ["ctx"]) PostMessage(panel.Handle, 0x7B, panel.Handle, lParam);
else
{
	PostMessage(panel.Handle, 0xA4, 2, lParam); // WM_NCRBUTTONDOWN на HTCAPTION
	PostMessage(panel.Handle, 0xA5, 2, lParam); // WM_NCRBUTTONUP
}
System.Windows.Forms.Timer timer = new() { Interval = 1500 };
timer.Tick += (_, _) => { Console.WriteLine($"черезъ 1,5 с меню видно: {menu.Visible}"); Application.Exit(); };
timer.Start();
Application.Run();

[DllImport("user32")] static extern bool PostMessage(nint hwnd, int msg, nint wParam, nint lParam);
[DllImport("user32")] static extern nint SendMessage(nint hwnd, int msg, nint wParam, nint lParam);

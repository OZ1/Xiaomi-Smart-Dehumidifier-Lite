#:project ..\Dehumidifier.csproj
#:property TargetFramework=net10.0-windows
#:property UseWindowsForms=true
#:property PublishAot=false
#:property PublishTrimmed=false
// Снимокъ панели безъ осушителя (связи нѣтъ: «− −», Wi‑Fi) въ нѣсколькихъ размѣрахъ, на синемъ фонѣ — видно прозрачность края.
// dotnet run --file приспособы\panel.cs [папка] [стороны…]   (по умолчанію — %TEMP%, 150 200 320)
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using DehumidifierControl;

string folder = args.Length > 0 ? args[0] : Path.GetTempPath();
int[] sides = args.Length > 1 ? [.. args.Skip(1).Select(int.Parse)] : [150, 200, 320];
ApplicationConfiguration.Initialize();
using MainForm main = new();
using PanelForm panel = new(main, () => { });
panel.StartPosition = FormStartPosition.Manual;
panel.Show();
panel.Location = new(-4000, -4000);
MethodInfo draw = typeof(PanelForm).GetMethod("Draw", BindingFlags.NonPublic | BindingFlags.Instance)!;
using Bitmap all = new(sides.Sum() + 20 * (sides.Length + 1), sides.Max() + 40);
using (Graphics g = Graphics.FromImage(all))
{
	g.Clear(Color.FromArgb(0x30, 0x60, 0x90));
	int x = 20;
	foreach (int side in sides)
	{
		panel.MinimumSize = panel.MaximumSize = Size.Empty;
		panel.Size = new(side, side);
		Application.DoEvents();
		using Bitmap frame = new(side, side);
		using (Graphics f = Graphics.FromImage(frame)) draw.Invoke(panel, [f]);
		g.DrawImage(frame, x, 20);
		x += side + 20;
	}
}
string path = Path.Combine(folder, "panel.png");
all.Save(path);
Console.WriteLine(path);

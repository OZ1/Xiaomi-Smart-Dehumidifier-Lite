#:project ..\Dehumidifier.csproj
#:property TargetFramework=net10.0-windows
#:property UseWindowsForms=true
#:property PublishAot=false
#:property PublishTrimmed=false
// Большое окно со спрятанною рамкою подключенія: слѣва — спрятана у показаннаго окна, справа — до перваго показа (какъ при запускѣ съ панели);
// потомъ рамка возвращается. Печатаетъ высоту окна и мѣста рамокъ.
// dotnet run --file приспособы\main-layout.cs [папка]   (по умолчанію — %TEMP%)
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using DehumidifierControl;

const BindingFlags F = BindingFlags.NonPublic | BindingFlags.Instance;
string folder = args.Length > 0 ? args[0] : Path.GetTempPath();
ApplicationConfiguration.Initialize();
using Bitmap shown = Shot(beforeShow: false), hidden = Shot(beforeShow: true);
using Bitmap all = new(shown.Width + hidden.Width, Math.Max(shown.Height, hidden.Height));
using (Graphics g = Graphics.FromImage(all))
{
	g.DrawImage(shown, 0, 0);
	g.DrawImage(hidden, shown.Width, 0);
}
string path = Path.Combine(folder, "main-layout.png");
all.Save(path);
Console.WriteLine(path);

Bitmap Shot(bool beforeShow)
{
	MainForm form = new() { StartPosition = FormStartPosition.Manual };
	MethodInfo show = typeof(MainForm).GetMethod("ShowConnection", F)!;
	_ = form.Handle;
	if (beforeShow) show.Invoke(form, [false]);
	form.Show();
	form.Location = new(-4000, -4000);
	if (!beforeShow) show.Invoke(form, [false]);
	Application.DoEvents();
	string label = beforeShow ? "до показа" : "у показаннаго";
	Console.WriteLine($"{label}: окно {form.ClientSize}, " + string.Join(", ", new[] { "groupState", "groupControls", "groupWatch" }
		.Select(n => $"{n} {((Control)typeof(MainForm).GetField(n, F)!.GetValue(form)!).Bounds}")));
	Bitmap bitmap = new(form.Width, form.Height);
	form.DrawToBitmap(bitmap, new(0, 0, form.Width, form.Height));
	show.Invoke(form, [true]);
	Console.WriteLine($"{label}, рамка снова видна: окно {form.ClientSize}");
	form.Close();
	return bitmap;
}

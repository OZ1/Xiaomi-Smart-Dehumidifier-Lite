#:project ..\Dehumidifier.csproj
#:property TargetFramework=net10.0-windows
#:property UseWindowsForms=true
#:property PublishAot=false
#:property PublishTrimmed=false
// Графикъ на придуманныхъ данныхъ: сеансъ 3 ч, двое сутокъ пустоты, сеансъ 9 ч съ 5 ч безъ связи; періодъ шире записи съ обѣихъ сторонъ.
// Вверху — весь періодъ (подсказка подъ мышью въ точкѣ x), внизу — приближеніе колесомъ къ правому сеансу.
// dotnet run --file приспособы\chart-gaps.cs [папка] [x подсказки]   (по умолчанію — %TEMP%, 330)
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using DehumidifierControl;

const BindingFlags F = BindingFlags.NonPublic | BindingFlags.Instance;
string folder = args.Length > 0 ? args[0] : Path.GetTempPath();
int mouseX = args.Length > 1 ? int.Parse(args[1]) : 330;
List<Sample> samples = [];
DateTime t0 = new(2026, 10, 1, 9, 0, 0);
Session(t0, 3);
Session(t0.AddDays(2), 9, lostAt: 2, lostHours: 5);

ApplicationConfiguration.Initialize();
using Form form = new() { StartPosition = FormStartPosition.Manual, Location = new(-4000, -4000), ClientSize = new(1000, 520), ShowInTaskbar = false };
ChartControl chart = new() { Dock = DockStyle.Fill };
form.Controls.Add(chart);
form.Show();
chart.SetSamples(samples, null);
chart.ShowRange(t0.AddDays(-1), t0.AddDays(3.5));
FieldInfo mouse = typeof(ChartControl).GetField("Mouse", F)!;
using Bitmap all = new(1000, 1040);
using (Graphics g = Graphics.FromImage(all))
{
	using Bitmap b = new(1000, 520);
	mouse.SetValue(chart, (Point?)new Point(mouseX, 150));
	chart.DrawToBitmap(b, new(0, 0, 1000, 520));
	g.DrawImage(b, 0, 0);
	object around = typeof(ChartControl).GetMethod("AxisAt", F)!.Invoke(chart, [700])!;
	typeof(ChartControl).GetMethod("Zoom", F)!.Invoke(chart, [0.5, around]);
	mouse.SetValue(chart, (Point?)new Point(640, 150));
	chart.DrawToBitmap(b, new(0, 0, 1000, 520));
	g.DrawImage(b, 0, 520);
}
string path = Path.Combine(folder, "chart-gaps.png");
all.Save(path);
Console.WriteLine(path);

void Session(DateTime start, double hours, double lostAt = -1, double lostHours = 0)
{
	samples.Add(new(start, SampleKind.Rec, R(55)));
	for (double h = 0.25; h < hours; h += 0.25)
	{
		if (lostAt >= 0 && h >= lostAt && h < lostAt + lostHours)
		{
			if (h == lostAt) samples.Add(new(start.AddHours(h), SampleKind.Lost, R(50)));
			continue;
		}
		samples.Add(new(start.AddHours(h), SampleKind.Change, R(55 - 10 * Math.Sin(h))));
	}
	samples.Add(new(start.AddHours(hours), SampleKind.Stop, R(50)));
}

static Reading R(double humidity) => new(24f, (float)humidity, true, 0, 50, 0, false);

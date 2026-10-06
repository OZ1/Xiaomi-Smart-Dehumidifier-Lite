#:project ..\Dehumidifier.csproj
#:property TargetFramework=net10.0-windows
#:property UseWindowsForms=true
#:property PublishAot=false
#:property PublishTrimmed=false
// Два клиента съ разныхъ сокетовъ (для осушителя — какъ два компьютера) одновременно читаютъ состояніе; у перваго id далеко впереди.
// Только чтеніе: осушитель не мѣняется. Адресъ и токенъ — изъ настроекъ пульта (user.config), не выводятся.
// dotnet run --file приспособы\two-clients.cs [id перваго] [чтеній]   (по умолчанію — 500000, 25)
using System.Diagnostics;
using System.Reflection;
using System.Xml.Linq;
using DehumidifierControl;

int firstId = args.Length > 0 ? int.Parse(args[0]) : 500_000;
int count = args.Length > 1 ? int.Parse(args[1]) : 25;
XDocument config = Directory.EnumerateFiles(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Dehumidifier"), "user.config", SearchOption.AllDirectories)
	.Select(f => (File: f, X: XDocument.Load(f))).Where(c => Value(c.X, "Token")?.Length == 32 && Value(c.X, "IP") is { Length: > 0 })
	.OrderByDescending(c => File.GetLastWriteTimeUtc(c.File)).First().X;
string ip = Value(config, "IP")!, token = Value(config, "Token")!;

using miIO a = new(ip, token), b = new(ip, token);
FieldInfo id = typeof(miIO).GetField("MessageId", BindingFlags.NonPublic | BindingFlags.Instance)!;
id.SetValue(a, firstId);
id.SetValue(b, 100);
int[] ok = [0, 0], failed = [0, 0];
await Task.WhenAll(Loop(new(a), 0, $"A (id {firstId}+)"), Loop(new(b), 1, "B (id 100+)"));
Console.WriteLine($"A: {ok[0]} удачно, {failed[0]} неудачно; B: {ok[1]} удачно, {failed[1]} неудачно");

async Task Loop(Dehumidifier device, int n, string name)
{
	for (int i = 0; i < count; i++)
	{
		Stopwatch watch = Stopwatch.StartNew();
		try
		{
			DehumidifierState s = await device.GetStateAsync();
			ok[n]++;
			if (i % 8 == 0) Console.WriteLine($"{name} #{i}: влажность {s.environment_relative_umidity}, {watch.ElapsedMilliseconds} мс");
		}
		catch (Exception e)
		{
			failed[n]++;
			Console.WriteLine($"{name} #{i}: {e.GetType().Name} {e.Message}");
		}
	}
}

static string? Value(XDocument x, string name) => x.Descendants("setting").FirstOrDefault(s => (string?)s.Attribute("name") == name)?.Element("value")?.Value;

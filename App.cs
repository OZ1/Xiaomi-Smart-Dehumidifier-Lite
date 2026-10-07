using System.Net;

namespace DehumidifierControl;

using Properties;

using static Values;

static class App
{
	/// <summary>Осушитель изъ командной строки (FromArgs), иначе изъ настроекъ; null — не данъ или невѣренъ.</summary>
	public static IPAddress? IP { get; private set; }
	public static byte[]? Token { get; private set; }
	/// <summary>Адресъ данъ въ командной строкѣ: окно и командная строка работаютъ съ нимъ, а адресъ и токенъ въ настройки не пишутъ.</summary>
	public static bool FromArgs { get; private set; }

	/// <summary>Безъ аргументовъ — окно съ адресомъ и токеномъ изъ настроекъ;
	/// «Dehumidifier адресъ [токенъ]» — окно съ этимъ осушителемъ, настройки не мѣняются;
	/// «Dehumidifier [адресъ токенъ] команды…» — командная строка съ этимъ осушителемъ или съ осушителемъ изъ настроекъ.</summary>
	[STAThread]
	static int Main(string[] args)
	{
		int skip = 0;
		if (args.Length > 0 && Address(args[0]) is { } ip)
		{
			IP = ip;
			skip = 1;
			FromArgs = true;
			if (args.Length > 1 && TokenOf(args[1]) is { } token)
			{
				Token = token;
				skip = 2;
			}
		}
		else
		{
			IP    = Address(Settings.Default.IP);
			Token = TokenOf(Settings.Default.Token);
		}

		if (args.Length > skip)
			return CLI.RunAsync(args, skip).GetAwaiter().GetResult();

		ApplicationConfiguration.Initialize();
		Application.Run(new MainForm());
		return 0;
	}
}

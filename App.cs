namespace DehumidifierControl;

static class App
{
	[STAThread]
	static int Main(string[] args)
	{
		if (args.Length > 0)
			return CLI.RunAsync(args).GetAwaiter().GetResult();

		ApplicationConfiguration.Initialize();
		Application.Run(new MainForm());
		return 0;
	}
}

using System.Globalization;
using System.Runtime.InteropServices;

namespace DehumidifierControl;

using Properties;

using static Properties.Resources;

using static Math;
using static String;
using static DateTime;
using static Enumerable;
using static MessageBoxIcon;
using static MessageBoxButtons;
using static FormStartPosition;
using static CultureInfo;
using static CollectionsMarshal;
using static Psychrometrics;
using static Values;

/// <summary>Просмотръ записей: слѣва сеансы, справа графики за выбранный періодъ — изъ всѣхъ файловъ подрядъ, — и что изъ этого видно о комнатѣ.
/// Выборъ сеансовъ ставитъ періодъ отъ начала перваго до конца послѣдняго; періодъ можно задать и самому.
/// Сеансы можно удалять цѣликомъ, а когда на экранѣ одинъ сеансъ — вырѣзать выдѣленный отрѣзокъ или оставить только его.</summary>
public partial class ChartsForm : Form
{
	/// <summary>Запись, которая идётъ сейчасъ (или null) — ея файлъ показывается вживую и не правится.</summary>
	readonly Func<Recorder?> CurrentRecorder;

	List<Session> Sessions = [];
	List<Session> ShownSessions = []; // сеансы, попавшіе въ періодъ на экранѣ
	List<Sample> ShownSamples = [];   // ихъ строки подрядъ, обрѣзанныя по періоду
	bool Updating;                    // періодъ и выборъ въ спискѣ мѣняемъ сами — не показывать заново
	bool FollowLive;                  // періодъ доходитъ до «сейчасъ» — его конецъ тянется за идущею записью

	/// <summary>Прочитанные файлы: пока не измѣнились — не читать снова (идущая запись перерисовывается каждыя 5 секундъ);
	/// Parsed — до какого байта разобрано: идущую запись дочитываемъ съ него.</summary>
	readonly Dictionary<string, (DateTime Written, long Length, long Parsed, List<Sample> Samples)> Loaded = [];

	/// <summary>Отрѣзки модели, которые уже не измѣнятся, пока запись только дописывается, и съ какой строки разбирать дальше;
	/// ModelSeam — строки по обѣ стороны этого мѣста: стали другими — показано уже не то, разбирать съ начала.</summary>
	List<Segment> Settled = [];
	int ModelResume;
	(Sample Before, Sample At) ModelSeam;

	Font? LiveFont; // жирный — идущая запись въ спискѣ; одинъ на всё время окна, а не новый на каждое обновленіе списка

	public ChartsForm(Func<Recorder?> currentRecorder)
	{
		InitializeComponent();
		CurrentRecorder = currentRecorder;
		Disposed += (_, _) => LiveFont?.Dispose();
	}

	/// <summary>Открыть разсчётъ — какъ F7 въ главномъ окнѣ.</summary>
	[System.ComponentModel.Browsable(false), System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
	public Action? OpenCalculator { get; init; }

	/// <summary>F7 — разсчётъ, какъ въ главномъ окнѣ.</summary>
	protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
	{
		if (keyData == Keys.F7 && OpenCalculator is { } open)
		{
			open();
			return true;
		}
		return base.ProcessCmdKey(ref msg, keyData);
	}

	protected override void OnLoad(EventArgs e)
	{
		base.OnLoad(e);
		if (DesignMode) return;
		Rectangle bounds = Settings.Default.ChartsBounds;
		if (bounds.Width > 0 && Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(bounds)))
		{
			StartPosition = Manual;
			Bounds = bounds;
		}
		dateFrom.CustomFormat = dateTo.CustomFormat = $"{CurrentCulture.DateTimeFormat.ShortDatePattern} {CurrentCulture.DateTimeFormat.ShortTimePattern}";
		ReloadSessions();
	}

	protected override void OnFormClosing(FormClosingEventArgs e)
	{
		Settings.Default.ChartsBounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
		Settings.Default.Save();
		base.OnFormClosing(e);
	}

	string? LivePath => CurrentRecorder()?.Path;

	/// <summary>Конецъ сеанса; у идущей записи — «сейчасъ».</summary>
	DateTime EndOf(Session session) => session.Path == LivePath ? Now : session.End;

	List<string> SelectedPaths() => [.. listSessions.SelectedItems.Cast<ListViewItem>().Select(i => ((Session)i.Tag!).Path)];

	/// <summary>Запись началась или кончилась — списокъ обновить; ничего не выбрано — выбрать идущую.</summary>
	public void RecordingChanged()
	{
		List<string> selected = SelectedPaths();
		if (selected.Count == 0 && LivePath is { } live) selected.Add(live);
		ReloadSessions(selected);
	}

	/// <summary>Въ идущую запись добавилась строка: если она на экранѣ — дорисовать.</summary>
	public void LineWritten()
	{
		string? live = LivePath;
		if (live is null) return;
		if (!Sessions.Any(s => s.Path == live))
		{
			ReloadSessions(SelectedPaths());
			return;
		}
		if (ShownSessions.Any(s => s.Path == live))
			ShowRange(keepView: true);
	}

	/// <summary>Списокъ заново; выбрать сеансы изъ select (нѣтъ такихъ — самый новый) и показать ихъ.</summary>
	void ReloadSessions(params List<string> select)
	{
		Sessions = RecordFiles.List(LoadSamples);
		string? live = LivePath;
		foreach (string gone in Loaded.Keys.Where(path => !Sessions.Any(s => s.Path == path)).ToList())
			Loaded.Remove(gone);
		Updating = true;
		listSessions.BeginUpdate();
		listSessions.Items.Clear();
		foreach (Session session in Sessions)
		{
			ListViewItem item = new([$"{session.Start:g}", Duration(EndOf(session) - session.Start), $"{session.Lines}"]) { Tag = session };
			if (session.Path == live)
				item.Font = LiveFont ??= new(listSessions.Font, FontStyle.Bold);
			listSessions.Items.Add(item);
		}
		List<int> indices = [.. Range(0, Sessions.Count).Where(i => select.Contains(Sessions[i].Path))];
		if (indices.Count == 0 && Sessions.Count > 0) indices.Add(0);
		foreach (int i in indices)
			listSessions.Items[i].Selected = true;
		if (indices.Count > 0)
		{
			listSessions.Items[indices[0]].Focused = true;
			listSessions.EnsureVisible(indices[0]);
		}
		listSessions.EndUpdate();
		Updating = false;
		if (indices.Count > 0) ShowSelected();
		else
		{
			ShownSessions = [];
			ShownSamples = [];
			chart.SetSamples(ShownSamples, null);
			ShowModel(more: false);
		}
		UpdateButtons();
	}

	void Sessions_SelectedIndexChanged(object? sender, EventArgs e)
	{
		if (Updating) return;
		ShowSelected();
		UpdateButtons();
	}

	/// <summary>Періодъ — отъ начала перваго выбраннаго сеанса до конца послѣдняго; ничего не выбрано — какъ былъ.</summary>
	void ShowSelected()
	{
		List<Session> selected = [.. listSessions.SelectedItems.Cast<ListViewItem>().Select(i => (Session)i.Tag!)];
		if (selected.Count > 0)
			SetRange(selected.Min(s => s.Start), selected.Max(EndOf));
	}

	void SetRange(DateTime from, DateTime to)
	{
		Updating = true;
		dateFrom.Value = from;
		dateTo.Value = to;
		Updating = false;
		ShowRange(keepView: false);
	}

	void Range_ValueChanged(object? sender, EventArgs e)
	{
		if (!Updating) ShowRange(keepView: false);
	}

	void AllRecords_Click(object? sender, EventArgs e)
	{
		if (Sessions.Count > 0)
			SetRange(Sessions[^1].Start, Sessions.Max(EndOf)); // новые сверху: самый ранній — послѣдній
	}

	/// <summary>Строки файла: изъ прочитаннаго, если файлъ съ тѣхъ поръ не мѣнялся; идущая запись только дописывается — дочитать хвостъ.</summary>
	List<Sample> LoadSamples(string path)
	{
		FileInfo file = new(path);
		bool known = Loaded.TryGetValue(path, out var loaded);
		if (known && loaded.Written == file.LastWriteTimeUtc && loaded.Length == file.Length)
			return loaded.Samples;
		if (path == LivePath)
		{
			(long offset, List<Sample> samples) = known && file.Length >= loaded.Parsed ? (loaded.Parsed, loaded.Samples) : (0, []);
			long parsed = RecordFiles.Append(path, offset, samples);
			Loaded[path] = (file.LastWriteTimeUtc, file.Length, parsed, samples);
			return samples;
		}
		List<Sample> all = RecordFiles.Load(path);
		Loaded[path] = (file.LastWriteTimeUtc, file.Length, file.Length, all);
		return all;
	}

	/// <summary>Періодъ изъ полей — на графикъ: всѣ сеансы, что въ него попадаютъ, подрядъ, обрѣзанные по его краямъ.
	/// keepView — не сбрасывать масштабъ (запись идётъ, файлъ дописался).</summary>
	void ShowRange(bool keepView)
	{
		DateTime from = dateFrom.Value, to = dateTo.Value;
		string? live = LivePath;
		FollowLive = live is not null && to >= Now.AddMinutes(-1);
		List<Session> shown = [];
		List<Sample> samples = [];
		string? error = null;
		if (to > from)
			for (int i = Sessions.Count - 1; i >= 0; i--) // новые сверху, а подрядъ нужны отъ ранняго: съ конца
			{
				Session session = Sessions[i];
				if (session.Start > to || EndOf(session) < from) continue;
				List<Sample> part;
				try
				{
					part = LoadSamples(session.Path);
				}
				catch (IOException ex)
				{
					error = ex.Message;
					continue;
				}
				if (part.Count > 0 && (part[0].Time < from || part[^1].Time > to))
					part = RecordFiles.Trim(part, from, to);
				if (part.Count == 0) continue;
				// предыдущій сеансъ оборвался безъ stop (программу закрыли не по-хорошему) — разрывъ, чтобы линія не тянулась черезъ пустоту
				if (samples.Count > 0 && samples[^1].Kind != SampleKind.Stop)
					samples.Add(new(samples[^1].Time, SampleKind.Stop, samples[^1].State));
				samples.AddRange(part);
				shown.Add(session);
			}
		ShownSessions = shown;
		ShownSamples = samples;
		bool liveEnds = live is not null && shown.Count > 0 && shown[^1].Path == live && samples[^1].Kind != SampleKind.Stop;
		chart.SetSamples(samples, liveEnds ? Now < to ? Now : to : null, keepView);
		if (!keepView) chart.ShowRange(from, to);
		chart.AccessibleName = $"{Text}: {from:g} — {to:g}";
		ShowModel(more: keepView);
		if (error is not null) labelModel.Text = error;
		UpdateButtons();
	}

	/// <summary>Что видно изъ періода: по каждому ровному отрѣзку — τ, куда шла влажность и сколько воды въ часъ.
	/// Граммы въ часъ — изъ k·V·dρ/dt, поэтому зависятъ отъ объёма и буфера изъ разсчётовъ.
	/// more — показанное только дописалось (идётъ запись): разбирать не всё, а съ начала послѣдняго отрѣзка.</summary>
	void ShowModel(bool more)
	{
		ReadOnlySpan<Sample> samples = AsSpan(ShownSamples);
		if (!more || ModelResume == 0 || ModelResume >= samples.Length || (samples[ModelResume - 1], samples[ModelResume]) != ModelSeam)
		{
			Settled = [];
			ModelResume = 0;
		}
		List<Segment> segments = [.. Settled];
		(ModelResume, bool open) = MoistureFit.Analyze(samples, ModelResume, segments);
		Settled = open ? segments.GetRange(0, segments.Count - 1) : segments;
		if (ModelResume > 0)
			ModelSeam = (samples[ModelResume - 1], samples[ModelResume]);
		if (segments.Count == 0)
		{
			labelModel.Text = ShownSamples.Count > 0 ? ModelNone : "";
			return;
		}
		RoomModel model = RoomModel.FromSettings();
		double capacity = model.Buffer * model.Volume; // k·V, м³
		List<string> lines = [];
		foreach (Segment s in segments)
		{
			double limitHumidity = RelativeHumidity(s.MeanTemperature, s.Fit.Limit);
			double rate = capacity * Abs(s.Fit.Limit - s.MeanWater) / s.Fit.Tau; // г/ч: k·V·|ρ∞ − ρ̄|/τ
			string line = s.Kind == SegmentKind.Off
				? Format(ModelOff, s.Start, s.End, s.Fit.Tau, s.Fit.Limit, limitHumidity, rate)
				: Format(ModelDrying, s.Start, s.End, s.Fit.Tau, s.Fit.Limit, limitHumidity, rate, StateText(new(null, null, true, s.Mode, null, 0, false)));
			lines.Add(s.Fit.Bounded ? line + ModelBounded : line);
		}
		labelModel.Text = Join(Environment.NewLine, lines.TakeLast(4)) + Environment.NewLine + Format(ModelBasis, model.Volume, model.Buffer);
	}

	/// <summary>Сеансъ, который можно править: на экранѣ ровно одинъ, и запись въ него не идётъ.</summary>
	Session? Editable => ShownSessions is [{ } only] && only.Path != LivePath ? only : null;

	void UpdateButtons()
	{
		buttonDelete.Enabled = listSessions.SelectedItems.Count > 0;
		buttonCut.Enabled = buttonTrim.Enabled = Editable is not null && chart.Selection is not null;
		buttonShowAll.Enabled = ShownSamples.Count > 0;
		buttonAllRecords.Enabled = Sessions.Count > 0;
	}

	void Chart_SelectionChanged(object? sender, EventArgs e) => UpdateButtons();

	void Sessions_KeyDown(object? sender, KeyEventArgs e)
	{
		if (e.KeyCode == Keys.Delete)
		{
			Delete_Click(sender, e);
			e.Handled = true;
		}
		else if (e.KeyCode == Keys.A && e.Control)
		{
			Updating = true;
			foreach (ListViewItem item in listSessions.Items)
				item.Selected = true;
			Updating = false;
			ShowSelected();
			UpdateButtons();
			e.Handled = true;
		}
	}

	void Delete_Click(object? sender, EventArgs e)
	{
		List<Session> selected = [.. listSessions.SelectedItems.Cast<ListViewItem>().Select(i => (Session)i.Tag!)];
		if (selected.Count == 0) return;
		if (selected.Any(s => s.Path == LivePath))
		{
			MessageBox.Show(this, SessionLive, Text, OK, Information);
			return;
		}
		if (MessageBox.Show(this, Format(SessionsDeleteQuestion, selected.Count), Text, OKCancel, Question) != DialogResult.OK)
			return;
		try
		{
			foreach (Session session in selected)
				RecordFiles.Delete(session.Path);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			MessageBox.Show(this, ex.Message, Text, OK, Error);
		}
		ReloadSessions();
	}

	void Cut_Click(object? sender, EventArgs e) => Edit(CutQuestion, RecordFiles.Cut);

	void Trim_Click(object? sender, EventArgs e) => Edit(TrimQuestion, RecordFiles.Trim);

	/// <summary>Правка выдѣленнаго отрѣзка: спросить, переписать файлъ цѣликомъ (не только видимое въ періодѣ), показать заново.</summary>
	void Edit(string question, Func<List<Sample>, DateTime, DateTime, List<Sample>> edit)
	{
		if (Editable is not { } session || chart.Selection is not { } selection)
			return;
		if (MessageBox.Show(this, Format(question, selection.From, selection.To), Text, OKCancel, Question) != DialogResult.OK)
			return;
		try
		{
			RecordFiles.Save(session.Path, edit(RecordFiles.Load(session.Path), selection.From, selection.To));
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			MessageBox.Show(this, ex.Message, Text, OK, Error);
		}
		chart.ClearSelection();
		ReloadSessions(SelectedPaths());
	}

	void ShowAll_Click(object? sender, EventArgs e) => chart.ShowAll();

	/// <summary>Періодъ доходитъ до «сейчасъ» — конецъ его тянется за идущею записью (если полемъ конца сейчасъ не правятъ).</summary>
	void LiveTimer_Tick(object? sender, EventArgs e)
	{
		if (!FollowLive || dateTo.Focused) return;
		Updating = true;
		dateTo.Value = Now;
		Updating = false;
		ShowRange(keepView: true);
	}
}

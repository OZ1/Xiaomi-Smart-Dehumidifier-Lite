using Timer = System.Windows.Forms.Timer;

namespace DehumidifierControl;

partial class ChartsForm
{
	/// <summary>
	///  Required designer variable.
	/// </summary>
	private System.ComponentModel.IContainer components = null;

	/// <summary>
	///  Clean up any resources being used.
	/// </summary>
	/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			components?.Dispose();
		}
		base.Dispose(disposing);
	}

	#region Windows Form Designer generated code

	/// <summary>
	///  Required method for Designer support - do not modify
	///  the contents of this method with the code editor.
	/// </summary>
	private void InitializeComponent()
	{
		components = new System.ComponentModel.Container();
		SplitContainer splitContainer;
		System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ChartsForm));
		ColumnHeader columnStart;
		ColumnHeader columnLength;
		ColumnHeader columnLines;
		FlowLayoutPanel flowButtons;
		Label labelHint;
		listSessions = new ListView();
		chart = new ChartControl();
		labelModel = new Label();
		buttonDelete = new Button();
		buttonCut = new Button();
		buttonTrim = new Button();
		buttonShowAll = new Button();
		flowRange = new FlowLayoutPanel();
		labelFrom = new Label();
		dateFrom = new DateTimePicker();
		labelTo = new Label();
		dateTo = new DateTimePicker();
		buttonAllRecords = new Button();
		liveTimer = new Timer(components);
		splitContainer = new SplitContainer();
		columnStart = new ColumnHeader();
		columnLength = new ColumnHeader();
		columnLines = new ColumnHeader();
		flowButtons = new FlowLayoutPanel();
		labelHint = new Label();
		((System.ComponentModel.ISupportInitialize)splitContainer).BeginInit();
		splitContainer.Panel1.SuspendLayout();
		splitContainer.Panel2.SuspendLayout();
		splitContainer.SuspendLayout();
		flowButtons.SuspendLayout();
		flowRange.SuspendLayout();
		SuspendLayout();
		// 
		// splitContainer
		// 
		resources.ApplyResources(splitContainer, "splitContainer");
		splitContainer.FixedPanel = FixedPanel.Panel1;
		splitContainer.Name = "splitContainer";
		// 
		// splitContainer.Panel1
		// 
		splitContainer.Panel1.Controls.Add(listSessions);
		// 
		// splitContainer.Panel2
		// 
		splitContainer.Panel2.Controls.Add(chart);
		splitContainer.Panel2.Controls.Add(labelModel);
		splitContainer.Panel2.Controls.Add(flowButtons);
		splitContainer.Panel2.Controls.Add(flowRange);
		// 
		// listSessions
		// 
		resources.ApplyResources(listSessions, "listSessions");
		listSessions.Columns.AddRange(new ColumnHeader[] { columnStart, columnLength, columnLines });
		listSessions.FullRowSelect = true;
		listSessions.Name = "listSessions";
		listSessions.UseCompatibleStateImageBehavior = false;
		listSessions.View = View.Details;
		listSessions.SelectedIndexChanged += Sessions_SelectedIndexChanged;
		listSessions.KeyDown += Sessions_KeyDown;
		// 
		// columnStart
		// 
		resources.ApplyResources(columnStart, "columnStart");
		// 
		// columnLength
		// 
		resources.ApplyResources(columnLength, "columnLength");
		// 
		// columnLines
		// 
		resources.ApplyResources(columnLines, "columnLines");
		// 
		// chart
		// 
		resources.ApplyResources(chart, "chart");
		chart.AccessibleRole = AccessibleRole.Chart;
		chart.Name = "chart";
		chart.SelectionChanged += Chart_SelectionChanged;
		// 
		// labelModel
		// 
		resources.ApplyResources(labelModel, "labelModel");
		labelModel.Name = "labelModel";
		// 
		// flowButtons
		// 
		resources.ApplyResources(flowButtons, "flowButtons");
		flowButtons.Controls.Add(buttonDelete);
		flowButtons.Controls.Add(buttonCut);
		flowButtons.Controls.Add(buttonTrim);
		flowButtons.Controls.Add(buttonShowAll);
		flowButtons.Controls.Add(labelHint);
		flowButtons.Name = "flowButtons";
		// 
		// buttonDelete
		// 
		resources.ApplyResources(buttonDelete, "buttonDelete");
		buttonDelete.Name = "buttonDelete";
		buttonDelete.UseVisualStyleBackColor = true;
		buttonDelete.Click += Delete_Click;
		// 
		// buttonCut
		// 
		resources.ApplyResources(buttonCut, "buttonCut");
		buttonCut.Name = "buttonCut";
		buttonCut.UseVisualStyleBackColor = true;
		buttonCut.Click += Cut_Click;
		// 
		// buttonTrim
		// 
		resources.ApplyResources(buttonTrim, "buttonTrim");
		buttonTrim.Name = "buttonTrim";
		buttonTrim.UseVisualStyleBackColor = true;
		buttonTrim.Click += Trim_Click;
		// 
		// buttonShowAll
		// 
		resources.ApplyResources(buttonShowAll, "buttonShowAll");
		buttonShowAll.Name = "buttonShowAll";
		buttonShowAll.UseVisualStyleBackColor = true;
		buttonShowAll.Click += ShowAll_Click;
		// 
		// labelHint
		// 
		resources.ApplyResources(labelHint, "labelHint");
		labelHint.ForeColor = SystemColors.GrayText;
		labelHint.Name = "labelHint";
		// 
		// flowRange
		// 
		resources.ApplyResources(flowRange, "flowRange");
		flowRange.Controls.Add(labelFrom);
		flowRange.Controls.Add(dateFrom);
		flowRange.Controls.Add(labelTo);
		flowRange.Controls.Add(dateTo);
		flowRange.Controls.Add(buttonAllRecords);
		flowRange.Name = "flowRange";
		// 
		// labelFrom
		// 
		resources.ApplyResources(labelFrom, "labelFrom");
		labelFrom.Name = "labelFrom";
		// 
		// dateFrom
		// 
		dateFrom.Format = DateTimePickerFormat.Custom;
		resources.ApplyResources(dateFrom, "dateFrom");
		dateFrom.Name = "dateFrom";
		dateFrom.ValueChanged += Range_ValueChanged;
		// 
		// labelTo
		// 
		resources.ApplyResources(labelTo, "labelTo");
		labelTo.Name = "labelTo";
		// 
		// dateTo
		// 
		dateTo.Format = DateTimePickerFormat.Custom;
		resources.ApplyResources(dateTo, "dateTo");
		dateTo.Name = "dateTo";
		dateTo.ValueChanged += Range_ValueChanged;
		// 
		// buttonAllRecords
		// 
		resources.ApplyResources(buttonAllRecords, "buttonAllRecords");
		buttonAllRecords.Name = "buttonAllRecords";
		buttonAllRecords.UseVisualStyleBackColor = true;
		buttonAllRecords.Click += AllRecords_Click;
		// 
		// liveTimer
		// 
		liveTimer.Enabled = true;
		liveTimer.Interval = 5000;
		liveTimer.Tick += LiveTimer_Tick;
		// 
		// ChartsForm
		// 
		resources.ApplyResources(this, "$this");
		AutoScaleMode = AutoScaleMode.Font;
		Controls.Add(splitContainer);
		Name = "ChartsForm";
		splitContainer.Panel1.ResumeLayout(false);
		splitContainer.Panel2.ResumeLayout(false);
		splitContainer.Panel2.PerformLayout();
		((System.ComponentModel.ISupportInitialize)splitContainer).EndInit();
		splitContainer.ResumeLayout(false);
		flowButtons.ResumeLayout(false);
		flowButtons.PerformLayout();
		flowRange.ResumeLayout(false);
		flowRange.PerformLayout();
		ResumeLayout(false);
	}

	#endregion

	private ListView listSessions;
	private ChartControl chart;
	private Label labelModel;
	private Button buttonDelete;
	private Button buttonCut;
	private Button buttonTrim;
	private Button buttonShowAll;
	private Timer liveTimer;
	private DateTimePicker dateFrom;
	private DateTimePicker dateTo;
	private Button buttonAllRecords;
	private FlowLayoutPanel flowRange;
	private Label labelFrom;
	private Label labelTo;
}

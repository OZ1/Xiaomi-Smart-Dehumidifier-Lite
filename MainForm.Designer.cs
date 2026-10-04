using Timer = System.Windows.Forms.Timer;

namespace DehumidifierControl;

partial class MainForm
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
			QuietFont.Dispose();
			Device?.Dispose();
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
		Label labelToken;
		System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
		Label labelIp;
		Label labelTimerLeftCaption;
		Label labelDryLeftCaption;
		Label labelWarmingCaption;
		Label labelFaultCaption;
		Label labelTemperatureCaption;
		Label labelHumidityCaption;
		Button buttonToggle;
		Button buttonResetFilter;
		Label labelTargetCaption;
		Label labelModeCaption;
		Label labelPowerCaption;
		GroupBox groupConnection;
		GroupBox groupState;
		Label labelHumidityId;
		Label labelTemperatureId;
		Label labelFaultId;
		Label labelWarmingId;
		Label labelDryLeftId;
		Label labelTimerLeftId;
		StatusStrip statusStrip;
		Label labelTimeOff;
		Label labelPowerId;
		Label labelModeId;
		Label labelTargetId;
		Label labelLightId;
		Label labelSoundId;
		Label labelLockId;
		Label labelDryAfterOffId;
		Label labelTimerId;
		Label labelLightModeId;
		Label labelTimerValueId;
		Label labelTimerMnutes;
		textBoxIP = new TextBox();
		textBoxToken = new TextBox();
		buttonConnect = new Button();
		labelHumidity = new Label();
		labelTemperature = new Label();
		labelFault = new Label();
		labelWarming = new Label();
		labelDryLeft = new Label();
		labelTimerLeft = new Label();
		toolStripStatusLabel = new ToolStripStatusLabel();
		toolStripStatusTime = new ToolStripStatusLabel();
		groupControls = new GroupBox();
		checkBoxPower = new CheckBox();
		listBoxMode = new ListBox();
		buttonLoopMode = new Button();
		labelTarget = new Label();
		panelTargetScale = new Panel();
		trackBarTarget = new TrackBar();
		checkBoxLight = new CheckBox();
		listBoxLight = new ListBox();
		checkBoxSound = new CheckBox();
		checkBoxLock = new CheckBox();
		checkBoxDryAfterOff = new CheckBox();
		checkBoxTimer = new CheckBox();
		numericTimerMinutes = new NumericUpDown();
		dateTimeOff = new DateTimePicker();
		toolTip = new ToolTip(components);
		pollTimer = new Timer(components);
		targetDebounceTimer = new Timer(components);
		delayDebounceTimer = new Timer(components);
		labelToken = new Label();
		labelIp = new Label();
		labelTimerLeftCaption = new Label();
		labelDryLeftCaption = new Label();
		labelWarmingCaption = new Label();
		labelFaultCaption = new Label();
		labelTemperatureCaption = new Label();
		labelHumidityCaption = new Label();
		buttonToggle = new Button();
		buttonResetFilter = new Button();
		labelTargetCaption = new Label();
		labelModeCaption = new Label();
		labelPowerCaption = new Label();
		groupConnection = new GroupBox();
		groupState = new GroupBox();
		labelHumidityId = new Label();
		labelTemperatureId = new Label();
		labelFaultId = new Label();
		labelWarmingId = new Label();
		labelDryLeftId = new Label();
		labelTimerLeftId = new Label();
		statusStrip = new StatusStrip();
		labelTimeOff = new Label();
		labelPowerId = new Label();
		labelModeId = new Label();
		labelTargetId = new Label();
		labelLightId = new Label();
		labelSoundId = new Label();
		labelLockId = new Label();
		labelDryAfterOffId = new Label();
		labelTimerId = new Label();
		labelLightModeId = new Label();
		labelTimerValueId = new Label();
		labelTimerMnutes = new Label();
		groupConnection.SuspendLayout();
		groupState.SuspendLayout();
		statusStrip.SuspendLayout();
		groupControls.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)trackBarTarget).BeginInit();
		((System.ComponentModel.ISupportInitialize)numericTimerMinutes).BeginInit();
		SuspendLayout();
		// 
		// labelToken
		// 
		resources.ApplyResources(labelToken, "labelToken");
		labelToken.Name = "labelToken";
		// 
		// labelIp
		// 
		resources.ApplyResources(labelIp, "labelIp");
		labelIp.Name = "labelIp";
		// 
		// labelTimerLeftCaption
		// 
		resources.ApplyResources(labelTimerLeftCaption, "labelTimerLeftCaption");
		labelTimerLeftCaption.Name = "labelTimerLeftCaption";
		// 
		// labelDryLeftCaption
		// 
		resources.ApplyResources(labelDryLeftCaption, "labelDryLeftCaption");
		labelDryLeftCaption.Name = "labelDryLeftCaption";
		// 
		// labelWarmingCaption
		// 
		resources.ApplyResources(labelWarmingCaption, "labelWarmingCaption");
		labelWarmingCaption.Name = "labelWarmingCaption";
		// 
		// labelFaultCaption
		// 
		resources.ApplyResources(labelFaultCaption, "labelFaultCaption");
		labelFaultCaption.Name = "labelFaultCaption";
		// 
		// labelTemperatureCaption
		// 
		resources.ApplyResources(labelTemperatureCaption, "labelTemperatureCaption");
		labelTemperatureCaption.Name = "labelTemperatureCaption";
		// 
		// labelHumidityCaption
		// 
		resources.ApplyResources(labelHumidityCaption, "labelHumidityCaption");
		labelHumidityCaption.Name = "labelHumidityCaption";
		// 
		// buttonToggle
		// 
		resources.ApplyResources(buttonToggle, "buttonToggle");
		buttonToggle.Name = "buttonToggle";
		toolTip.SetToolTip(buttonToggle, resources.GetString("buttonToggle.ToolTip"));
		buttonToggle.UseVisualStyleBackColor = true;
		buttonToggle.Click += Toggle_Click;
		// 
		// buttonResetFilter
		// 
		resources.ApplyResources(buttonResetFilter, "buttonResetFilter");
		buttonResetFilter.Name = "buttonResetFilter";
		buttonResetFilter.UseVisualStyleBackColor = true;
		buttonResetFilter.Click += ResetFilter_Click;
		// 
		// labelTargetCaption
		// 
		resources.ApplyResources(labelTargetCaption, "labelTargetCaption");
		labelTargetCaption.Name = "labelTargetCaption";
		// 
		// labelModeCaption
		// 
		resources.ApplyResources(labelModeCaption, "labelModeCaption");
		labelModeCaption.Name = "labelModeCaption";
		// 
		// labelPowerCaption
		// 
		resources.ApplyResources(labelPowerCaption, "labelPowerCaption");
		labelPowerCaption.Name = "labelPowerCaption";
		// 
		// groupConnection
		// 
		resources.ApplyResources(groupConnection, "groupConnection");
		groupConnection.Controls.Add(labelIp);
		groupConnection.Controls.Add(textBoxIP);
		groupConnection.Controls.Add(labelToken);
		groupConnection.Controls.Add(textBoxToken);
		groupConnection.Controls.Add(buttonConnect);
		groupConnection.Name = "groupConnection";
		groupConnection.TabStop = false;
		// 
		// textBoxIP
		// 
		resources.ApplyResources(textBoxIP, "textBoxIP");
		textBoxIP.Name = "textBoxIP";
		// 
		// textBoxToken
		// 
		resources.ApplyResources(textBoxToken, "textBoxToken");
		textBoxToken.CharacterCasing = CharacterCasing.Upper;
		textBoxToken.Name = "textBoxToken";
		// 
		// buttonConnect
		// 
		resources.ApplyResources(buttonConnect, "buttonConnect");
		buttonConnect.Name = "buttonConnect";
		buttonConnect.UseVisualStyleBackColor = true;
		buttonConnect.Click += Connect_Click;
		// 
		// groupState
		// 
		resources.ApplyResources(groupState, "groupState");
		groupState.Controls.Add(labelHumidityId);
		groupState.Controls.Add(labelHumidityCaption);
		groupState.Controls.Add(labelHumidity);
		groupState.Controls.Add(labelTemperatureId);
		groupState.Controls.Add(labelTemperatureCaption);
		groupState.Controls.Add(labelTemperature);
		groupState.Controls.Add(labelFaultId);
		groupState.Controls.Add(labelFaultCaption);
		groupState.Controls.Add(labelFault);
		groupState.Controls.Add(labelWarmingId);
		groupState.Controls.Add(labelWarmingCaption);
		groupState.Controls.Add(labelWarming);
		groupState.Controls.Add(labelDryLeftId);
		groupState.Controls.Add(labelDryLeftCaption);
		groupState.Controls.Add(labelDryLeft);
		groupState.Controls.Add(labelTimerLeftId);
		groupState.Controls.Add(labelTimerLeftCaption);
		groupState.Controls.Add(labelTimerLeft);
		groupState.Name = "groupState";
		groupState.TabStop = false;
		// 
		// labelHumidityId
		// 
		resources.ApplyResources(labelHumidityId, "labelHumidityId");
		labelHumidityId.ForeColor = SystemColors.GrayText;
		labelHumidityId.Name = "labelHumidityId";
		// 
		// labelHumidity
		// 
		resources.ApplyResources(labelHumidity, "labelHumidity");
		labelHumidity.Name = "labelHumidity";
		// 
		// labelTemperatureId
		// 
		resources.ApplyResources(labelTemperatureId, "labelTemperatureId");
		labelTemperatureId.ForeColor = SystemColors.GrayText;
		labelTemperatureId.Name = "labelTemperatureId";
		// 
		// labelTemperature
		// 
		resources.ApplyResources(labelTemperature, "labelTemperature");
		labelTemperature.Name = "labelTemperature";
		// 
		// labelFaultId
		// 
		resources.ApplyResources(labelFaultId, "labelFaultId");
		labelFaultId.ForeColor = SystemColors.GrayText;
		labelFaultId.Name = "labelFaultId";
		// 
		// labelFault
		// 
		resources.ApplyResources(labelFault, "labelFault");
		labelFault.Name = "labelFault";
		// 
		// labelWarmingId
		// 
		resources.ApplyResources(labelWarmingId, "labelWarmingId");
		labelWarmingId.ForeColor = SystemColors.GrayText;
		labelWarmingId.Name = "labelWarmingId";
		// 
		// labelWarming
		// 
		resources.ApplyResources(labelWarming, "labelWarming");
		labelWarming.Name = "labelWarming";
		// 
		// labelDryLeftId
		// 
		resources.ApplyResources(labelDryLeftId, "labelDryLeftId");
		labelDryLeftId.ForeColor = SystemColors.GrayText;
		labelDryLeftId.Name = "labelDryLeftId";
		// 
		// labelDryLeft
		// 
		resources.ApplyResources(labelDryLeft, "labelDryLeft");
		labelDryLeft.Name = "labelDryLeft";
		// 
		// labelTimerLeftId
		// 
		resources.ApplyResources(labelTimerLeftId, "labelTimerLeftId");
		labelTimerLeftId.ForeColor = SystemColors.GrayText;
		labelTimerLeftId.Name = "labelTimerLeftId";
		// 
		// labelTimerLeft
		// 
		resources.ApplyResources(labelTimerLeft, "labelTimerLeft");
		labelTimerLeft.Name = "labelTimerLeft";
		// 
		// statusStrip
		// 
		resources.ApplyResources(statusStrip, "statusStrip");
		statusStrip.Items.AddRange(new ToolStripItem[] { toolStripStatusLabel, toolStripStatusTime });
		statusStrip.Name = "statusStrip";
		statusStrip.ShowItemToolTips = true;
		statusStrip.SizingGrip = false;
		// 
		// toolStripStatusLabel
		// 
		toolStripStatusLabel.LiveSetting = System.Windows.Forms.Automation.AutomationLiveSetting.Polite;
		toolStripStatusLabel.Name = "toolStripStatusLabel";
		resources.ApplyResources(toolStripStatusLabel, "toolStripStatusLabel");
		toolStripStatusLabel.Spring = true;
		// 
		// toolStripStatusTime
		// 
		resources.ApplyResources(toolStripStatusTime, "toolStripStatusTime");
		toolStripStatusTime.Name = "toolStripStatusTime";
		// 
		// labelTimeOff
		// 
		resources.ApplyResources(labelTimeOff, "labelTimeOff");
		labelTimeOff.Name = "labelTimeOff";
		// 
		// labelPowerId
		// 
		resources.ApplyResources(labelPowerId, "labelPowerId");
		labelPowerId.ForeColor = SystemColors.GrayText;
		labelPowerId.Name = "labelPowerId";
		// 
		// labelModeId
		// 
		resources.ApplyResources(labelModeId, "labelModeId");
		labelModeId.ForeColor = SystemColors.GrayText;
		labelModeId.Name = "labelModeId";
		// 
		// labelTargetId
		// 
		resources.ApplyResources(labelTargetId, "labelTargetId");
		labelTargetId.ForeColor = SystemColors.GrayText;
		labelTargetId.Name = "labelTargetId";
		// 
		// labelLightId
		// 
		resources.ApplyResources(labelLightId, "labelLightId");
		labelLightId.ForeColor = SystemColors.GrayText;
		labelLightId.Name = "labelLightId";
		// 
		// labelSoundId
		// 
		resources.ApplyResources(labelSoundId, "labelSoundId");
		labelSoundId.ForeColor = SystemColors.GrayText;
		labelSoundId.Name = "labelSoundId";
		// 
		// labelLockId
		// 
		resources.ApplyResources(labelLockId, "labelLockId");
		labelLockId.ForeColor = SystemColors.GrayText;
		labelLockId.Name = "labelLockId";
		// 
		// labelDryAfterOffId
		// 
		resources.ApplyResources(labelDryAfterOffId, "labelDryAfterOffId");
		labelDryAfterOffId.ForeColor = SystemColors.GrayText;
		labelDryAfterOffId.Name = "labelDryAfterOffId";
		// 
		// labelTimerId
		// 
		resources.ApplyResources(labelTimerId, "labelTimerId");
		labelTimerId.ForeColor = SystemColors.GrayText;
		labelTimerId.Name = "labelTimerId";
		// 
		// labelLightModeId
		// 
		resources.ApplyResources(labelLightModeId, "labelLightModeId");
		labelLightModeId.ForeColor = SystemColors.GrayText;
		labelLightModeId.Name = "labelLightModeId";
		// 
		// labelTimerValueId
		// 
		resources.ApplyResources(labelTimerValueId, "labelTimerValueId");
		labelTimerValueId.ForeColor = SystemColors.GrayText;
		labelTimerValueId.Name = "labelTimerValueId";
		// 
		// labelTimerMnutes
		// 
		resources.ApplyResources(labelTimerMnutes, "labelTimerMnutes");
		labelTimerMnutes.Name = "labelTimerMnutes";
		// 
		// groupControls
		// 
		resources.ApplyResources(groupControls, "groupControls");
		groupControls.Controls.Add(labelPowerId);
		groupControls.Controls.Add(labelPowerCaption);
		groupControls.Controls.Add(checkBoxPower);
		groupControls.Controls.Add(buttonToggle);
		groupControls.Controls.Add(labelModeId);
		groupControls.Controls.Add(labelModeCaption);
		groupControls.Controls.Add(listBoxMode);
		groupControls.Controls.Add(buttonLoopMode);
		groupControls.Controls.Add(labelTargetId);
		groupControls.Controls.Add(labelTargetCaption);
		groupControls.Controls.Add(labelTarget);
		groupControls.Controls.Add(panelTargetScale);
		groupControls.Controls.Add(trackBarTarget);
		groupControls.Controls.Add(labelLightId);
		groupControls.Controls.Add(checkBoxLight);
		groupControls.Controls.Add(labelLightModeId);
		groupControls.Controls.Add(listBoxLight);
		groupControls.Controls.Add(labelSoundId);
		groupControls.Controls.Add(checkBoxSound);
		groupControls.Controls.Add(labelLockId);
		groupControls.Controls.Add(checkBoxLock);
		groupControls.Controls.Add(labelDryAfterOffId);
		groupControls.Controls.Add(checkBoxDryAfterOff);
		groupControls.Controls.Add(labelTimerId);
		groupControls.Controls.Add(checkBoxTimer);
		groupControls.Controls.Add(labelTimerValueId);
		groupControls.Controls.Add(numericTimerMinutes);
		groupControls.Controls.Add(labelTimerMnutes);
		groupControls.Controls.Add(dateTimeOff);
		groupControls.Controls.Add(labelTimeOff);
		groupControls.Controls.Add(buttonResetFilter);
		groupControls.Name = "groupControls";
		groupControls.TabStop = false;
		// 
		// checkBoxPower
		// 
		resources.ApplyResources(checkBoxPower, "checkBoxPower");
		checkBoxPower.Name = "checkBoxPower";
		checkBoxPower.UseVisualStyleBackColor = true;
		checkBoxPower.CheckedChanged += Power_CheckedChanged;
		// 
		// listBoxMode
		// 
		resources.ApplyResources(listBoxMode, "listBoxMode");
		listBoxMode.FormattingEnabled = true;
		listBoxMode.Items.AddRange(new object[] { resources.GetString("listBoxMode.Items"), resources.GetString("listBoxMode.Items1"), resources.GetString("listBoxMode.Items2") });
		listBoxMode.Name = "listBoxMode";
		listBoxMode.SelectedIndexChanged += Mode_SelectedIndexChanged;
		// 
		// buttonLoopMode
		// 
		resources.ApplyResources(buttonLoopMode, "buttonLoopMode");
		buttonLoopMode.Name = "buttonLoopMode";
		toolTip.SetToolTip(buttonLoopMode, resources.GetString("buttonLoopMode.ToolTip"));
		buttonLoopMode.UseVisualStyleBackColor = true;
		buttonLoopMode.Click += LoopMode_Click;
		// 
		// labelTarget
		// 
		resources.ApplyResources(labelTarget, "labelTarget");
		labelTarget.Name = "labelTarget";
		// 
		// panelTargetScale
		// 
		resources.ApplyResources(panelTargetScale, "panelTargetScale");
		panelTargetScale.Name = "panelTargetScale";
		panelTargetScale.Paint += TargetScale_Paint;
		// 
		// trackBarTarget
		// 
		resources.ApplyResources(trackBarTarget, "trackBarTarget");
		trackBarTarget.Maximum = 100;
		trackBarTarget.Name = "trackBarTarget";
		trackBarTarget.TickFrequency = 10;
		trackBarTarget.Value = 50;
		trackBarTarget.ValueChanged += Target_ValueChanged;
		// 
		// checkBoxLight
		// 
		resources.ApplyResources(checkBoxLight, "checkBoxLight");
		checkBoxLight.Name = "checkBoxLight";
		checkBoxLight.UseVisualStyleBackColor = true;
		checkBoxLight.CheckedChanged += LightOn_CheckedChanged;
		// 
		// listBoxLight
		// 
		resources.ApplyResources(listBoxLight, "listBoxLight");
		listBoxLight.FormattingEnabled = true;
		listBoxLight.Items.AddRange(new object[] { resources.GetString("listBoxLight.Items"), resources.GetString("listBoxLight.Items1"), resources.GetString("listBoxLight.Items2") });
		listBoxLight.Name = "listBoxLight";
		listBoxLight.SelectedIndexChanged += Light_SelectedIndexChanged;
		// 
		// checkBoxSound
		// 
		resources.ApplyResources(checkBoxSound, "checkBoxSound");
		checkBoxSound.Name = "checkBoxSound";
		checkBoxSound.UseVisualStyleBackColor = true;
		checkBoxSound.CheckedChanged += Sound_CheckedChanged;
		// 
		// checkBoxLock
		// 
		resources.ApplyResources(checkBoxLock, "checkBoxLock");
		checkBoxLock.Name = "checkBoxLock";
		checkBoxLock.UseVisualStyleBackColor = true;
		checkBoxLock.CheckedChanged += Lock_CheckedChanged;
		// 
		// checkBoxDryAfterOff
		// 
		resources.ApplyResources(checkBoxDryAfterOff, "checkBoxDryAfterOff");
		checkBoxDryAfterOff.Name = "checkBoxDryAfterOff";
		checkBoxDryAfterOff.UseVisualStyleBackColor = true;
		checkBoxDryAfterOff.CheckedChanged += DryAfterOff_CheckedChanged;
		// 
		// checkBoxTimer
		// 
		resources.ApplyResources(checkBoxTimer, "checkBoxTimer");
		checkBoxTimer.Name = "checkBoxTimer";
		checkBoxTimer.UseVisualStyleBackColor = true;
		checkBoxTimer.CheckedChanged += Timer_CheckedChanged;
		// 
		// numericTimerMinutes
		// 
		resources.ApplyResources(numericTimerMinutes, "numericTimerMinutes");
		numericTimerMinutes.Increment = new decimal(new int[] { 15, 0, 0, 0 });
		numericTimerMinutes.Maximum = new decimal(new int[] { 65535, 0, 0, 0 });
		numericTimerMinutes.Name = "numericTimerMinutes";
		numericTimerMinutes.Value = new decimal(new int[] { 60, 0, 0, 0 });
		numericTimerMinutes.ValueChanged += TimerMinutes_ValueChanged;
		// 
		// dateTimeOff
		// 
		resources.ApplyResources(dateTimeOff, "dateTimeOff");
		dateTimeOff.Format = DateTimePickerFormat.Custom;
		dateTimeOff.Name = "dateTimeOff";
		dateTimeOff.ShowUpDown = true;
		dateTimeOff.ValueChanged += TimeOff_ValueChanged;
		// 
		// pollTimer
		// 
		pollTimer.Interval = 1000;
		pollTimer.Tick += PollTimer_Tick;
		// 
		// targetDebounceTimer
		// 
		targetDebounceTimer.Interval = 700;
		targetDebounceTimer.Tick += TargetDebounce_Tick;
		// 
		// delayDebounceTimer
		// 
		delayDebounceTimer.Interval = 700;
		delayDebounceTimer.Tick += DelayDebounce_Tick;
		// 
		// MainForm
		// 
		AcceptButton = buttonConnect;
		resources.ApplyResources(this, "$this");
		AutoScaleMode = AutoScaleMode.Font;
		Controls.Add(groupConnection);
		Controls.Add(groupState);
		Controls.Add(groupControls);
		Controls.Add(statusStrip);
		FormBorderStyle = FormBorderStyle.FixedSingle;
		MaximizeBox = false;
		Name = "MainForm";
		groupConnection.ResumeLayout(false);
		groupConnection.PerformLayout();
		groupState.ResumeLayout(false);
		groupState.PerformLayout();
		statusStrip.ResumeLayout(false);
		statusStrip.PerformLayout();
		groupControls.ResumeLayout(false);
		groupControls.PerformLayout();
		((System.ComponentModel.ISupportInitialize)trackBarTarget).EndInit();
		((System.ComponentModel.ISupportInitialize)numericTimerMinutes).EndInit();
		ResumeLayout(false);
		PerformLayout();
	}

	#endregion

	private TextBox textBoxIP;
	private TextBox textBoxToken;
	private Button buttonConnect;
	private Label labelHumidity;
	private Label labelTemperature;
	private Label labelFault;
	private Label labelWarming;
	private Label labelDryLeft;
	private Label labelTimerLeft;
	private CheckBox checkBoxPower;
	private ListBox listBoxMode;
	private TrackBar trackBarTarget;
	private Panel panelTargetScale;
	private Label labelTarget;
	private ListBox listBoxLight;
	private CheckBox checkBoxLight;
	private CheckBox checkBoxSound;
	private CheckBox checkBoxLock;
	private CheckBox checkBoxDryAfterOff;
	private NumericUpDown numericTimerMinutes;
	private DateTimePicker dateTimeOff;
	private CheckBox checkBoxTimer;
	private GroupBox groupControls;
	private Button buttonLoopMode;
	private ToolStripStatusLabel toolStripStatusLabel;
	private ToolStripStatusLabel toolStripStatusTime;
	private ToolTip toolTip;
	private Timer pollTimer;
	private Timer targetDebounceTimer;
	private Timer delayDebounceTimer;
}

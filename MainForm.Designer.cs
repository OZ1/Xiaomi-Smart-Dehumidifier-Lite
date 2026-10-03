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
		System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
		buttonConnect = new Button();
		textBoxToken = new TextBox();
		textBoxIP = new TextBox();
		labelTimerLeft = new Label();
		labelDryLeft = new Label();
		labelWarming = new Label();
		labelFault = new Label();
		labelTemperature = new Label();
		labelHumidity = new Label();
		toolStripStatusLabel = new ToolStripStatusLabel();
		toolStripStatusTime = new ToolStripStatusLabel();
		groupControls = new GroupBox();
		buttonLoopMode = new Button();
		checkBoxTimer = new CheckBox();
		numericTimerMinutes = new NumericUpDown();
		dateTimeOff = new DateTimePicker();
		labelTimeOff = new Label();
		checkBoxDryAfterOff = new CheckBox();
		checkBoxLock = new CheckBox();
		checkBoxSound = new CheckBox();
		listBoxLight = new ListBox();
		checkBoxLight = new CheckBox();
		labelTarget = new Label();
		panelTargetScale = new Panel();
		trackBarTarget = new TrackBar();
		listBoxMode = new ListBox();
		checkBoxPower = new CheckBox();
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
		((System.ComponentModel.ISupportInitialize)numericTimerMinutes).BeginInit();
		((System.ComponentModel.ISupportInitialize)trackBarTarget).BeginInit();
		SuspendLayout();
		// 
		// labelToken
		// 
		labelToken.AutoSize = true;
		labelToken.Location = new Point(15, 57);
		labelToken.Name = "labelToken";
		labelToken.Size = new Size(50, 15);
		labelToken.TabIndex = 3;
		labelToken.Text = "&Токенъ:";
		// 
		// labelIp
		// 
		labelIp.AutoSize = true;
		labelIp.Location = new Point(15, 28);
		labelIp.Name = "labelIp";
		labelIp.Size = new Size(50, 15);
		labelIp.TabIndex = 0;
		labelIp.Text = "&Адресъ:";
		// 
		// labelTimerLeftCaption
		// 
		labelTimerLeftCaption.AutoSize = true;
		labelTimerLeftCaption.Location = new Point(35, 185);
		labelTimerLeftCaption.Name = "labelTimerLeftCaption";
		labelTimerLeftCaption.Size = new Size(115, 15);
		labelTimerLeftCaption.TabIndex = 16;
		labelTimerLeftCaption.Text = "Таймеръ, осталось:";
		// 
		// labelDryLeftCaption
		// 
		labelDryLeftCaption.AutoSize = true;
		labelDryLeftCaption.Location = new Point(28, 153);
		labelDryLeftCaption.Name = "labelDryLeftCaption";
		labelDryLeftCaption.Size = new Size(122, 15);
		labelDryLeftCaption.TabIndex = 13;
		labelDryLeftCaption.Text = "Просушки осталось:";
		// 
		// labelWarmingCaption
		// 
		labelWarmingCaption.AutoSize = true;
		labelWarmingCaption.Location = new Point(85, 123);
		labelWarmingCaption.Name = "labelWarmingCaption";
		labelWarmingCaption.Size = new Size(65, 15);
		labelWarmingCaption.TabIndex = 10;
		labelWarmingCaption.Text = "Прогрѣвъ:";
		// 
		// labelFaultCaption
		// 
		labelFaultCaption.AutoSize = true;
		labelFaultCaption.Location = new Point(55, 92);
		labelFaultCaption.Name = "labelFaultCaption";
		labelFaultCaption.Size = new Size(95, 15);
		labelFaultCaption.TabIndex = 7;
		labelFaultCaption.Text = "Неисправность:";
		// 
		// labelTemperatureCaption
		// 
		labelTemperatureCaption.AutoSize = true;
		labelTemperatureCaption.Location = new Point(68, 62);
		labelTemperatureCaption.Name = "labelTemperatureCaption";
		labelTemperatureCaption.Size = new Size(82, 15);
		labelTemperatureCaption.TabIndex = 4;
		labelTemperatureCaption.Text = "Температура:";
		// 
		// labelHumidityCaption
		// 
		labelHumidityCaption.AutoSize = true;
		labelHumidityCaption.Location = new Point(80, 31);
		labelHumidityCaption.Name = "labelHumidityCaption";
		labelHumidityCaption.Size = new Size(70, 15);
		labelHumidityCaption.TabIndex = 1;
		labelHumidityCaption.Text = "Влажность:";
		// 
		// buttonToggle
		// 
		buttonToggle.AccessibleDescription = "Дѣйствіе MIoT 7.1 toggle";
		buttonToggle.AccessibleName = "Переключить питаніе";
		buttonToggle.Font = new Font("Segoe Fluent Icons", 12F);
		buttonToggle.Location = new Point(265, 26);
		buttonToggle.Name = "buttonToggle";
		buttonToggle.Size = new Size(32, 32);
		buttonToggle.TabIndex = 3;
		buttonToggle.Text = "";
		toolTip.SetToolTip(buttonToggle, "Переключить питаніе (дѣйствіе toggle 7.1)");
		buttonToggle.UseVisualStyleBackColor = true;
		buttonToggle.Click += Toggle_Click;
		// 
		// buttonResetFilter
		// 
		buttonResetFilter.AccessibleDescription = "Дѣйствіе MIoT 7.3 reset-filter";
		buttonResetFilter.AccessibleName = "Сбросить счётчикъ фильтра";
		buttonResetFilter.Location = new Point(144, 413);
		buttonResetFilter.Name = "buttonResetFilter";
		buttonResetFilter.Size = new Size(200, 27);
		buttonResetFilter.TabIndex = 29;
		buttonResetFilter.Text = "Сбросить счётчикъ &фильтра…";
		buttonResetFilter.UseVisualStyleBackColor = true;
		buttonResetFilter.Click += ResetFilter_Click;
		// 
		// labelTargetCaption
		// 
		labelTargetCaption.AutoSize = true;
		labelTargetCaption.Location = new Point(30, 149);
		labelTargetCaption.Name = "labelTargetCaption";
		labelTargetCaption.Size = new Size(39, 15);
		labelTargetCaption.TabIndex = 9;
		labelTargetCaption.Text = "&Цѣль:";
		// 
		// labelModeCaption
		// 
		labelModeCaption.AutoSize = true;
		labelModeCaption.Location = new Point(28, 82);
		labelModeCaption.Name = "labelModeCaption";
		labelModeCaption.Size = new Size(55, 15);
		labelModeCaption.TabIndex = 5;
		labelModeCaption.Text = "&Режимъ:";
		// 
		// labelPowerCaption
		// 
		labelPowerCaption.AutoSize = true;
		labelPowerCaption.Location = new Point(30, 34);
		labelPowerCaption.Name = "labelPowerCaption";
		labelPowerCaption.Size = new Size(53, 15);
		labelPowerCaption.TabIndex = 1;
		labelPowerCaption.Text = "&Питаніе:";
		// 
		// groupConnection
		// 
		groupConnection.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
		groupConnection.Controls.Add(buttonConnect);
		groupConnection.Controls.Add(textBoxToken);
		groupConnection.Controls.Add(labelToken);
		groupConnection.Controls.Add(textBoxIP);
		groupConnection.Controls.Add(labelIp);
		groupConnection.Location = new Point(12, 12);
		groupConnection.Name = "groupConnection";
		groupConnection.Size = new Size(370, 90);
		groupConnection.TabIndex = 0;
		groupConnection.TabStop = false;
		groupConnection.Text = "Подключеніе";
		// 
		// buttonConnect
		// 
		buttonConnect.Location = new Point(192, 23);
		buttonConnect.Name = "buttonConnect";
		buttonConnect.Size = new Size(105, 25);
		buttonConnect.TabIndex = 2;
		buttonConnect.Text = "&Подключиться";
		buttonConnect.UseVisualStyleBackColor = true;
		buttonConnect.Click += Connect_Click;
		// 
		// textBoxToken
		// 
		textBoxToken.AccessibleDescription = "32 шестнадцатеричныя цифры";
		textBoxToken.AccessibleName = "Токенъ";
		textBoxToken.CharacterCasing = CharacterCasing.Upper;
		textBoxToken.Location = new Point(75, 54);
		textBoxToken.MaxLength = 32;
		textBoxToken.Name = "textBoxToken";
		textBoxToken.PlaceholderText = "00112233445566778899AABBCCDDEEFF";
		textBoxToken.Size = new Size(222, 23);
		textBoxToken.TabIndex = 4;
		textBoxToken.TextAlign = HorizontalAlignment.Center;
		// 
		// textBoxIP
		// 
		textBoxIP.AccessibleDescription = "IP-адресъ въ локальной сѣти";
		textBoxIP.AccessibleName = "Адресъ осушителя";
		textBoxIP.Location = new Point(75, 25);
		textBoxIP.Name = "textBoxIP";
		textBoxIP.PlaceholderText = "255.255.255.255";
		textBoxIP.Size = new Size(112, 23);
		textBoxIP.TabIndex = 1;
		textBoxIP.TextAlign = HorizontalAlignment.Center;
		// 
		// groupState
		// 
		groupState.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
		groupState.Controls.Add(labelTimerLeft);
		groupState.Controls.Add(labelTimerLeftCaption);
		groupState.Controls.Add(labelDryLeft);
		groupState.Controls.Add(labelDryLeftCaption);
		groupState.Controls.Add(labelWarming);
		groupState.Controls.Add(labelWarmingCaption);
		groupState.Controls.Add(labelFault);
		groupState.Controls.Add(labelFaultCaption);
		groupState.Controls.Add(labelTemperature);
		groupState.Controls.Add(labelTemperatureCaption);
		groupState.Controls.Add(labelHumidity);
		groupState.Controls.Add(labelHumidityCaption);
		groupState.Controls.Add(labelHumidityId);
		groupState.Controls.Add(labelTemperatureId);
		groupState.Controls.Add(labelFaultId);
		groupState.Controls.Add(labelWarmingId);
		groupState.Controls.Add(labelDryLeftId);
		groupState.Controls.Add(labelTimerLeftId);
		groupState.Location = new Point(12, 108);
		groupState.Name = "groupState";
		groupState.Size = new Size(370, 214);
		groupState.TabIndex = 1;
		groupState.TabStop = false;
		groupState.Text = "Состояніе";
		// 
		// labelTimerLeft
		// 
		labelTimerLeft.AutoSize = true;
		labelTimerLeft.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
		labelTimerLeft.Location = new Point(161, 185);
		labelTimerLeft.Name = "labelTimerLeft";
		labelTimerLeft.Size = new Size(19, 15);
		labelTimerLeft.TabIndex = 17;
		labelTimerLeft.Text = "—";
		// 
		// labelDryLeft
		// 
		labelDryLeft.AutoSize = true;
		labelDryLeft.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
		labelDryLeft.Location = new Point(161, 154);
		labelDryLeft.Name = "labelDryLeft";
		labelDryLeft.Size = new Size(19, 15);
		labelDryLeft.TabIndex = 14;
		labelDryLeft.Text = "—";
		// 
		// labelWarming
		// 
		labelWarming.AutoSize = true;
		labelWarming.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
		labelWarming.Location = new Point(161, 123);
		labelWarming.Name = "labelWarming";
		labelWarming.Size = new Size(19, 15);
		labelWarming.TabIndex = 11;
		labelWarming.Text = "—";
		// 
		// labelFault
		// 
		labelFault.AutoSize = true;
		labelFault.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
		labelFault.Location = new Point(161, 92);
		labelFault.Name = "labelFault";
		labelFault.Size = new Size(19, 15);
		labelFault.TabIndex = 8;
		labelFault.Text = "—";
		// 
		// labelTemperature
		// 
		labelTemperature.AutoSize = true;
		labelTemperature.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
		labelTemperature.Location = new Point(161, 60);
		labelTemperature.Name = "labelTemperature";
		labelTemperature.Size = new Size(26, 21);
		labelTemperature.TabIndex = 5;
		labelTemperature.Text = "—";
		// 
		// labelHumidity
		// 
		labelHumidity.AutoSize = true;
		labelHumidity.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
		labelHumidity.Location = new Point(161, 24);
		labelHumidity.Name = "labelHumidity";
		labelHumidity.Size = new Size(35, 30);
		labelHumidity.TabIndex = 2;
		labelHumidity.Text = "—";
		// 
		// labelHumidityId
		// 
		labelHumidityId.AutoSize = true;
		labelHumidityId.Font = new Font("Segoe UI Light", 7F);
		labelHumidityId.ForeColor = SystemColors.GrayText;
		labelHumidityId.Location = new Point(6, 34);
		labelHumidityId.Name = "labelHumidityId";
		labelHumidityId.Size = new Size(16, 12);
		labelHumidityId.TabIndex = 0;
		labelHumidityId.Text = "3.1";
		// 
		// labelTemperatureId
		// 
		labelTemperatureId.AutoSize = true;
		labelTemperatureId.Font = new Font("Segoe UI Light", 7F);
		labelTemperatureId.ForeColor = SystemColors.GrayText;
		labelTemperatureId.Location = new Point(6, 64);
		labelTemperatureId.Name = "labelTemperatureId";
		labelTemperatureId.Size = new Size(17, 12);
		labelTemperatureId.TabIndex = 3;
		labelTemperatureId.Text = "3.2";
		// 
		// labelFaultId
		// 
		labelFaultId.AutoSize = true;
		labelFaultId.Font = new Font("Segoe UI Light", 7F);
		labelFaultId.ForeColor = SystemColors.GrayText;
		labelFaultId.Location = new Point(6, 92);
		labelFaultId.Name = "labelFaultId";
		labelFaultId.Size = new Size(17, 12);
		labelFaultId.TabIndex = 6;
		labelFaultId.Text = "2.2";
		// 
		// labelWarmingId
		// 
		labelWarmingId.AutoSize = true;
		labelWarmingId.Font = new Font("Segoe UI Light", 7F);
		labelWarmingId.ForeColor = SystemColors.GrayText;
		labelWarmingId.Location = new Point(6, 123);
		labelWarmingId.Name = "labelWarmingId";
		labelWarmingId.Size = new Size(17, 12);
		labelWarmingId.TabIndex = 9;
		labelWarmingId.Text = "7.3";
		// 
		// labelDryLeftId
		// 
		labelDryLeftId.AutoSize = true;
		labelDryLeftId.Font = new Font("Segoe UI Light", 7F);
		labelDryLeftId.ForeColor = SystemColors.GrayText;
		labelDryLeftId.Location = new Point(6, 154);
		labelDryLeftId.Name = "labelDryLeftId";
		labelDryLeftId.Size = new Size(17, 12);
		labelDryLeftId.TabIndex = 12;
		labelDryLeftId.Text = "7.2";
		// 
		// labelTimerLeftId
		// 
		labelTimerLeftId.AutoSize = true;
		labelTimerLeftId.Font = new Font("Segoe UI Light", 7F);
		labelTimerLeftId.ForeColor = SystemColors.GrayText;
		labelTimerLeftId.Location = new Point(6, 185);
		labelTimerLeftId.Name = "labelTimerLeftId";
		labelTimerLeftId.Size = new Size(17, 12);
		labelTimerLeftId.TabIndex = 15;
		labelTimerLeftId.Text = "8.3";
		// 
		// statusStrip
		// 
		statusStrip.Items.AddRange(new ToolStripItem[] { toolStripStatusLabel, toolStripStatusTime });
		statusStrip.Location = new Point(0, 795);
		statusStrip.Name = "statusStrip";
		statusStrip.ShowItemToolTips = true;
		statusStrip.Size = new Size(394, 22);
		statusStrip.SizingGrip = false;
		statusStrip.TabIndex = 3;
		// 
		// toolStripStatusLabel
		// 
		toolStripStatusLabel.Name = "toolStripStatusLabel";
		toolStripStatusLabel.Size = new Size(379, 17);
		toolStripStatusLabel.Spring = true;
		toolStripStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
		// 
		// toolStripStatusTime
		// 
		toolStripStatusTime.AccessibleName = "Обновлено въ";
		toolStripStatusTime.Name = "toolStripStatusTime";
		toolStripStatusTime.Size = new Size(0, 17);
		toolStripStatusTime.ToolTipText = "Обновлено въ";
		// 
		// labelPowerId
		// 
		labelPowerId.AutoSize = true;
		labelPowerId.Font = new Font("Segoe UI Light", 7F);
		labelPowerId.ForeColor = SystemColors.GrayText;
		labelPowerId.Location = new Point(6, 36);
		labelPowerId.Name = "labelPowerId";
		labelPowerId.Size = new Size(16, 12);
		labelPowerId.TabIndex = 0;
		labelPowerId.Text = "2.1";
		// 
		// labelModeId
		// 
		labelModeId.AutoSize = true;
		labelModeId.Font = new Font("Segoe UI Light", 7F);
		labelModeId.ForeColor = SystemColors.GrayText;
		labelModeId.Location = new Point(6, 84);
		labelModeId.Name = "labelModeId";
		labelModeId.Size = new Size(17, 12);
		labelModeId.TabIndex = 4;
		labelModeId.Text = "2.3";
		// 
		// labelTargetId
		// 
		labelTargetId.AutoSize = true;
		labelTargetId.Font = new Font("Segoe UI Light", 7F);
		labelTargetId.ForeColor = SystemColors.GrayText;
		labelTargetId.Location = new Point(6, 151);
		labelTargetId.Name = "labelTargetId";
		labelTargetId.Size = new Size(17, 12);
		labelTargetId.TabIndex = 8;
		labelTargetId.Text = "2.5";
		// 
		// labelLightId
		// 
		labelLightId.AutoSize = true;
		labelLightId.Font = new Font("Segoe UI Light", 7F);
		labelLightId.ForeColor = SystemColors.GrayText;
		labelLightId.Location = new Point(6, 215);
		labelLightId.Name = "labelLightId";
		labelLightId.Size = new Size(16, 12);
		labelLightId.TabIndex = 13;
		labelLightId.Text = "5.1";
		// 
		// labelSoundId
		// 
		labelSoundId.AutoSize = true;
		labelSoundId.Font = new Font("Segoe UI Light", 7F);
		labelSoundId.ForeColor = SystemColors.GrayText;
		labelSoundId.Location = new Point(122, 267);
		labelSoundId.Name = "labelSoundId";
		labelSoundId.Size = new Size(16, 12);
		labelSoundId.TabIndex = 17;
		labelSoundId.Text = "4.1";
		// 
		// labelLockId
		// 
		labelLockId.AutoSize = true;
		labelLockId.Font = new Font("Segoe UI Light", 7F);
		labelLockId.ForeColor = SystemColors.GrayText;
		labelLockId.Location = new Point(122, 292);
		labelLockId.Name = "labelLockId";
		labelLockId.Size = new Size(16, 12);
		labelLockId.TabIndex = 19;
		labelLockId.Text = "6.1";
		// 
		// labelDryAfterOffId
		// 
		labelDryAfterOffId.AutoSize = true;
		labelDryAfterOffId.Font = new Font("Segoe UI Light", 7F);
		labelDryAfterOffId.ForeColor = SystemColors.GrayText;
		labelDryAfterOffId.Location = new Point(122, 317);
		labelDryAfterOffId.Name = "labelDryAfterOffId";
		labelDryAfterOffId.Size = new Size(16, 12);
		labelDryAfterOffId.TabIndex = 21;
		labelDryAfterOffId.Text = "7.1";
		// 
		// labelTimerId
		// 
		labelTimerId.AutoSize = true;
		labelTimerId.Font = new Font("Segoe UI Light", 7F);
		labelTimerId.ForeColor = SystemColors.GrayText;
		labelTimerId.Location = new Point(6, 350);
		labelTimerId.Name = "labelTimerId";
		labelTimerId.Size = new Size(16, 12);
		labelTimerId.TabIndex = 23;
		labelTimerId.Text = "8.1";
		// 
		// labelLightModeId
		// 
		labelLightModeId.AutoSize = true;
		labelLightModeId.Font = new Font("Segoe UI Light", 7F);
		labelLightModeId.ForeColor = SystemColors.GrayText;
		labelLightModeId.Location = new Point(122, 215);
		labelLightModeId.Name = "labelLightModeId";
		labelLightModeId.Size = new Size(17, 12);
		labelLightModeId.TabIndex = 15;
		labelLightModeId.Text = "5.2";
		// 
		// labelTimerValueId
		// 
		labelTimerValueId.AutoSize = true;
		labelTimerValueId.Font = new Font("Segoe UI Light", 7F);
		labelTimerValueId.ForeColor = SystemColors.GrayText;
		labelTimerValueId.Location = new Point(122, 349);
		labelTimerValueId.Name = "labelTimerValueId";
		labelTimerValueId.Size = new Size(17, 12);
		labelTimerValueId.TabIndex = 25;
		labelTimerValueId.Text = "8.2";
		// 
		// labelTimerMnutes
		// 
		labelTimerMnutes.AutoSize = true;
		labelTimerMnutes.Location = new Point(220, 347);
		labelTimerMnutes.Name = "labelTimerMnutes";
		labelTimerMnutes.Size = new Size(48, 15);
		labelTimerMnutes.TabIndex = 27;
		labelTimerMnutes.Text = "минутъ";
		// 
		// groupControls
		// 
		groupControls.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
		groupControls.Controls.Add(labelTimerValueId);
		groupControls.Controls.Add(labelLightModeId);
		groupControls.Controls.Add(buttonToggle);
		groupControls.Controls.Add(buttonLoopMode);
		groupControls.Controls.Add(buttonResetFilter);
		groupControls.Controls.Add(checkBoxTimer);
		groupControls.Controls.Add(numericTimerMinutes);
		groupControls.Controls.Add(dateTimeOff);
		groupControls.Controls.Add(labelTimeOff);
		groupControls.Controls.Add(checkBoxDryAfterOff);
		groupControls.Controls.Add(checkBoxLock);
		groupControls.Controls.Add(checkBoxSound);
		groupControls.Controls.Add(listBoxLight);
		groupControls.Controls.Add(checkBoxLight);
		groupControls.Controls.Add(labelTarget);
		groupControls.Controls.Add(panelTargetScale);
		groupControls.Controls.Add(trackBarTarget);
		groupControls.Controls.Add(labelTimerMnutes);
		groupControls.Controls.Add(labelTargetCaption);
		groupControls.Controls.Add(listBoxMode);
		groupControls.Controls.Add(labelModeCaption);
		groupControls.Controls.Add(checkBoxPower);
		groupControls.Controls.Add(labelPowerCaption);
		groupControls.Controls.Add(labelPowerId);
		groupControls.Controls.Add(labelModeId);
		groupControls.Controls.Add(labelTargetId);
		groupControls.Controls.Add(labelLightId);
		groupControls.Controls.Add(labelSoundId);
		groupControls.Controls.Add(labelLockId);
		groupControls.Controls.Add(labelDryAfterOffId);
		groupControls.Controls.Add(labelTimerId);
		groupControls.Enabled = false;
		groupControls.Location = new Point(12, 328);
		groupControls.Name = "groupControls";
		groupControls.Size = new Size(370, 456);
		groupControls.TabIndex = 1;
		groupControls.TabStop = false;
		groupControls.Text = "Управленіе";
		// 
		// buttonLoopMode
		// 
		buttonLoopMode.AccessibleDescription = "Дѣйствіе MIoT 7.2 loop-mode";
		buttonLoopMode.AccessibleName = "Слѣдующій режимъ";
		buttonLoopMode.Font = new Font("Segoe UI", 12F);
		buttonLoopMode.Location = new Point(265, 76);
		buttonLoopMode.Name = "buttonLoopMode";
		buttonLoopMode.Size = new Size(32, 49);
		buttonLoopMode.TabIndex = 7;
		buttonLoopMode.Text = "↓";
		toolTip.SetToolTip(buttonLoopMode, "Слѣдующій режимъ по кругу (дѣйствіе loop-mode 7.2)");
		buttonLoopMode.UseVisualStyleBackColor = true;
		buttonLoopMode.Click += LoopMode_Click;
		// 
		// checkBoxTimer
		// 
		checkBoxTimer.AccessibleDescription = "MIoT 8.1";
		checkBoxTimer.AccessibleName = "Таймеръ выключенія включёнъ";
		checkBoxTimer.AutoSize = true;
		checkBoxTimer.CheckAlign = ContentAlignment.MiddleRight;
		checkBoxTimer.Location = new Point(37, 347);
		checkBoxTimer.Name = "checkBoxTimer";
		checkBoxTimer.Size = new Size(78, 19);
		checkBoxTimer.TabIndex = 24;
		checkBoxTimer.Text = "Тай&меръ:";
		checkBoxTimer.UseVisualStyleBackColor = true;
		checkBoxTimer.CheckedChanged += Timer_CheckedChanged;
		// 
		// dateTimeOff
		// 
		dateTimeOff.AccessibleDescription = "Сейчасъ плюсъ таймеръ; если измѣнить — таймеръ станетъ этимъ временемъ минусъ сейчасъ";
		dateTimeOff.AccessibleName = "Время выключенія по таймеру";
		dateTimeOff.CustomFormat = "HH:mm";
		dateTimeOff.Format = DateTimePickerFormat.Custom;
		dateTimeOff.Location = new Point(144, 376);
		dateTimeOff.Name = "dateTimeOff";
		dateTimeOff.ShowUpDown = true;
		dateTimeOff.Size = new Size(70, 23);
		dateTimeOff.TabIndex = 28;
		dateTimeOff.ValueChanged += TimeOff_ValueChanged;
		// 
		// labelTimeOff
		// 
		labelTimeOff.AutoSize = true;
		labelTimeOff.Location = new Point(220, 380);
		labelTimeOff.Name = "labelTimeOff";
		labelTimeOff.Size = new Size(97, 15);
		labelTimeOff.TabIndex = 30;
		labelTimeOff.Text = "время выключенія";
		// 
		// numericTimerMinutes
		// 
		numericTimerMinutes.AccessibleDescription = "MIoT 8.2";
		numericTimerMinutes.AccessibleName = "Таймеръ выключенія, минутъ";
		numericTimerMinutes.Location = new Point(144, 345);
		numericTimerMinutes.Maximum = new decimal(new int[] { 720, 0, 0, 0 });
		numericTimerMinutes.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
		numericTimerMinutes.Name = "numericTimerMinutes";
		numericTimerMinutes.Size = new Size(70, 23);
		numericTimerMinutes.TabIndex = 26;
		numericTimerMinutes.TextAlign = HorizontalAlignment.Center;
		numericTimerMinutes.Value = new decimal(new int[] { 60, 0, 0, 0 });
		numericTimerMinutes.ValueChanged += TimerMinutes_ValueChanged;
		// 
		// checkBoxDryAfterOff
		// 
		checkBoxDryAfterOff.AccessibleDescription = "MIoT 7.1";
		checkBoxDryAfterOff.AccessibleName = "Просушка послѣ выключенія";
		checkBoxDryAfterOff.AutoSize = true;
		checkBoxDryAfterOff.Location = new Point(144, 314);
		checkBoxDryAfterOff.Name = "checkBoxDryAfterOff";
		checkBoxDryAfterOff.Size = new Size(191, 19);
		checkBoxDryAfterOff.TabIndex = 22;
		checkBoxDryAfterOff.Text = "Просу&шка послѣ выключенія";
		checkBoxDryAfterOff.UseVisualStyleBackColor = true;
		checkBoxDryAfterOff.CheckedChanged += DryAfterOff_CheckedChanged;
		// 
		// checkBoxLock
		// 
		checkBoxLock.AccessibleDescription = "MIoT 6.1";
		checkBoxLock.AccessibleName = "Блокировка кнопокъ";
		checkBoxLock.AutoSize = true;
		checkBoxLock.Location = new Point(144, 289);
		checkBoxLock.Name = "checkBoxLock";
		checkBoxLock.Size = new Size(142, 19);
		checkBoxLock.TabIndex = 20;
		checkBoxLock.Text = "&Блокировка кнопокъ";
		checkBoxLock.UseVisualStyleBackColor = true;
		checkBoxLock.CheckedChanged += Lock_CheckedChanged;
		// 
		// checkBoxSound
		// 
		checkBoxSound.AccessibleDescription = "MIoT 4.1";
		checkBoxSound.AccessibleName = "Звукъ кнопокъ";
		checkBoxSound.AutoSize = true;
		checkBoxSound.Location = new Point(144, 264);
		checkBoxSound.Name = "checkBoxSound";
		checkBoxSound.Size = new Size(108, 19);
		checkBoxSound.TabIndex = 18;
		checkBoxSound.Text = "&Звукъ кнопокъ";
		checkBoxSound.UseVisualStyleBackColor = true;
		checkBoxSound.CheckedChanged += Sound_CheckedChanged;
		// 
		// listBoxLight
		// 
		listBoxLight.AccessibleDescription = "MIoT 5.2";
		listBoxLight.AccessibleName = "Яркость подсвѣтки";
		listBoxLight.FormattingEnabled = true;
		listBoxLight.Items.AddRange(new object[] { "Выключена", "Тусклая", "Яркая" });
		listBoxLight.Location = new Point(144, 207);
		listBoxLight.Name = "listBoxLight";
		listBoxLight.Size = new Size(115, 49);
		listBoxLight.TabIndex = 16;
		listBoxLight.SelectedIndexChanged += Light_SelectedIndexChanged;
		// 
		// checkBoxLight
		// 
		checkBoxLight.AccessibleDescription = "MIoT 5.1";
		checkBoxLight.AccessibleName = "Подсвѣтка включена";
		checkBoxLight.AutoSize = true;
		checkBoxLight.CheckAlign = ContentAlignment.MiddleRight;
		checkBoxLight.Location = new Point(28, 212);
		checkBoxLight.Name = "checkBoxLight";
		checkBoxLight.Size = new Size(87, 19);
		checkBoxLight.TabIndex = 14;
		checkBoxLight.Text = "Под&свѣтка:";
		checkBoxLight.UseVisualStyleBackColor = true;
		checkBoxLight.CheckedChanged += LightOn_CheckedChanged;
		// 
		// labelTarget
		// 
		labelTarget.AutoSize = true;
		labelTarget.Location = new Point(98, 149);
		labelTarget.Name = "labelTarget";
		labelTarget.Size = new Size(32, 15);
		labelTarget.TabIndex = 11;
		labelTarget.Text = "50 %";
		labelTarget.TextAlign = ContentAlignment.MiddleRight;
		// 
		// panelTargetScale
		// 
		panelTargetScale.AccessibleDescription = "Риски черезъ 10 %, рекомендуемый диапазонъ и влажность въ комнатѣ";
		panelTargetScale.AccessibleName = "Шкала цѣлевой влажности";
		panelTargetScale.Location = new Point(136, 175);
		panelTargetScale.Name = "panelTargetScale";
		panelTargetScale.Size = new Size(220, 24);
		panelTargetScale.TabIndex = 12;
		panelTargetScale.Paint += TargetScale_Paint;
		// 
		// trackBarTarget
		// 
		trackBarTarget.AccessibleDescription = "MIoT 2.5; рекомендуется 40…70";
		trackBarTarget.AccessibleName = "Цѣлевая влажность, %";
		trackBarTarget.Location = new Point(136, 141);
		trackBarTarget.Maximum = 100;
		trackBarTarget.Name = "trackBarTarget";
		trackBarTarget.Size = new Size(220, 45);
		trackBarTarget.TabIndex = 10;
		trackBarTarget.TickFrequency = 10;
		trackBarTarget.Value = 50;
		trackBarTarget.ValueChanged += Target_ValueChanged;
		// 
		// listBoxMode
		// 
		listBoxMode.AccessibleDescription = "MIoT 2.3";
		listBoxMode.AccessibleName = "Режимъ";
		listBoxMode.FormattingEnabled = true;
		listBoxMode.Items.AddRange(new object[] { "Умный", "Ночной", "Сушка бѣлья" });
		listBoxMode.Location = new Point(144, 76);
		listBoxMode.Name = "listBoxMode";
		listBoxMode.Size = new Size(115, 49);
		listBoxMode.TabIndex = 6;
		listBoxMode.SelectedIndexChanged += Mode_SelectedIndexChanged;
		// 
		// checkBoxPower
		// 
		checkBoxPower.AccessibleDescription = "MIoT 2.1";
		checkBoxPower.AccessibleName = "Питаніе";
		checkBoxPower.Appearance = Appearance.Button;
		checkBoxPower.Location = new Point(144, 26);
		checkBoxPower.Name = "checkBoxPower";
		checkBoxPower.Size = new Size(115, 32);
		checkBoxPower.TabIndex = 2;
		checkBoxPower.Text = "Выключенъ";
		checkBoxPower.TextAlign = ContentAlignment.MiddleCenter;
		checkBoxPower.UseVisualStyleBackColor = true;
		checkBoxPower.CheckedChanged += Power_CheckedChanged;
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
		AutoScaleDimensions = new SizeF(7F, 15F);
		AutoScaleMode = AutoScaleMode.Font;
		ClientSize = new Size(394, 817);
		Controls.Add(statusStrip);
		Controls.Add(groupControls);
		Controls.Add(groupState);
		Controls.Add(groupConnection);
		FormBorderStyle = FormBorderStyle.FixedSingle;
		Icon = (Icon)resources.GetObject("$this.Icon");
		MaximizeBox = false;
		MaximumSize = new Size(1000, 856);
		MinimumSize = new Size(400, 856);
		Name = "MainForm";
		StartPosition = FormStartPosition.CenterScreen;
		Text = "Xiaomi Smart Dehumidifier Lite";
		groupConnection.ResumeLayout(false);
		groupConnection.PerformLayout();
		groupState.ResumeLayout(false);
		groupState.PerformLayout();
		statusStrip.ResumeLayout(false);
		statusStrip.PerformLayout();
		groupControls.ResumeLayout(false);
		groupControls.PerformLayout();
		((System.ComponentModel.ISupportInitialize)numericTimerMinutes).EndInit();
		((System.ComponentModel.ISupportInitialize)trackBarTarget).EndInit();
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

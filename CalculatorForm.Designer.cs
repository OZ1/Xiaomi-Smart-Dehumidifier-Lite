namespace DehumidifierControl;

partial class CalculatorForm
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
		GroupBox groupAir;
		Label labelTemperatureCaption;
		System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(CalculatorForm));
		Label labelHumidityCaption;
		Label labelWaterCaption;
		Button buttonFromDevice;
		Label labelDewCaption;
		Label labelRatioCaption;
		Label labelOtherCaption;
		GroupBox groupRoom;
		Label labelVolumeCaption;
		Label labelBufferCaption;
		Label labelTargetCaption;
		Label labelRemoveCaption;
		GroupBox groupForecast;
		Label labelExchangeCaption;
		Label labelOutdoorCaption;
		Label labelSourcesCaption;
		Label labelRatedCaption;
		Label labelRatedHint;
		Button buttonFromRecords;
		numericTemperature = new NumericUpDown();
		trackTemperature = new TrackBar();
		numericHumidity = new NumericUpDown();
		trackHumidity = new TrackBar();
		numericWater = new NumericUpDown();
		trackWater = new TrackBar();
		labelDew = new Label();
		labelRatio = new Label();
		numericOtherTemperature = new NumericUpDown();
		trackOtherTemperature = new TrackBar();
		labelOther = new Label();
		numericVolume = new NumericUpDown();
		trackVolume = new TrackBar();
		numericBuffer = new NumericUpDown();
		trackBuffer = new TrackBar();
		numericTarget = new NumericUpDown();
		trackTarget = new TrackBar();
		labelRemove = new Label();
		numericExchange = new NumericUpDown();
		trackExchange = new TrackBar();
		numericOutdoorTemperature = new NumericUpDown();
		trackOutdoorTemperature = new TrackBar();
		numericOutdoorHumidity = new NumericUpDown();
		trackOutdoorHumidity = new TrackBar();
		numericSources = new NumericUpDown();
		trackSources = new TrackBar();
		numericRated = new NumericUpDown();
		trackRated = new TrackBar();
		labelForecast = new Label();
		toolTip = new ToolTip(components);
		groupAir = new GroupBox();
		labelTemperatureCaption = new Label();
		labelHumidityCaption = new Label();
		labelWaterCaption = new Label();
		buttonFromDevice = new Button();
		labelDewCaption = new Label();
		labelRatioCaption = new Label();
		labelOtherCaption = new Label();
		groupRoom = new GroupBox();
		labelVolumeCaption = new Label();
		labelBufferCaption = new Label();
		labelTargetCaption = new Label();
		labelRemoveCaption = new Label();
		groupForecast = new GroupBox();
		labelExchangeCaption = new Label();
		labelOutdoorCaption = new Label();
		labelSourcesCaption = new Label();
		labelRatedCaption = new Label();
		labelRatedHint = new Label();
		buttonFromRecords = new Button();
		groupAir.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)numericTemperature).BeginInit();
		((System.ComponentModel.ISupportInitialize)trackTemperature).BeginInit();
		((System.ComponentModel.ISupportInitialize)numericHumidity).BeginInit();
		((System.ComponentModel.ISupportInitialize)trackHumidity).BeginInit();
		((System.ComponentModel.ISupportInitialize)numericWater).BeginInit();
		((System.ComponentModel.ISupportInitialize)trackWater).BeginInit();
		((System.ComponentModel.ISupportInitialize)numericOtherTemperature).BeginInit();
		((System.ComponentModel.ISupportInitialize)trackOtherTemperature).BeginInit();
		groupRoom.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)numericVolume).BeginInit();
		((System.ComponentModel.ISupportInitialize)trackVolume).BeginInit();
		((System.ComponentModel.ISupportInitialize)numericBuffer).BeginInit();
		((System.ComponentModel.ISupportInitialize)trackBuffer).BeginInit();
		((System.ComponentModel.ISupportInitialize)numericTarget).BeginInit();
		((System.ComponentModel.ISupportInitialize)trackTarget).BeginInit();
		groupForecast.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)numericExchange).BeginInit();
		((System.ComponentModel.ISupportInitialize)trackExchange).BeginInit();
		((System.ComponentModel.ISupportInitialize)numericOutdoorTemperature).BeginInit();
		((System.ComponentModel.ISupportInitialize)trackOutdoorTemperature).BeginInit();
		((System.ComponentModel.ISupportInitialize)numericOutdoorHumidity).BeginInit();
		((System.ComponentModel.ISupportInitialize)trackOutdoorHumidity).BeginInit();
		((System.ComponentModel.ISupportInitialize)numericSources).BeginInit();
		((System.ComponentModel.ISupportInitialize)trackSources).BeginInit();
		((System.ComponentModel.ISupportInitialize)numericRated).BeginInit();
		((System.ComponentModel.ISupportInitialize)trackRated).BeginInit();
		SuspendLayout();
		// 
		// groupAir
		// 
		groupAir.Controls.Add(labelTemperatureCaption);
		groupAir.Controls.Add(numericTemperature);
		groupAir.Controls.Add(trackTemperature);
		groupAir.Controls.Add(labelHumidityCaption);
		groupAir.Controls.Add(numericHumidity);
		groupAir.Controls.Add(trackHumidity);
		groupAir.Controls.Add(labelWaterCaption);
		groupAir.Controls.Add(numericWater);
		groupAir.Controls.Add(trackWater);
		groupAir.Controls.Add(buttonFromDevice);
		groupAir.Controls.Add(labelDewCaption);
		groupAir.Controls.Add(labelDew);
		groupAir.Controls.Add(labelRatioCaption);
		groupAir.Controls.Add(labelRatio);
		groupAir.Controls.Add(labelOtherCaption);
		groupAir.Controls.Add(numericOtherTemperature);
		groupAir.Controls.Add(trackOtherTemperature);
		groupAir.Controls.Add(labelOther);
		resources.ApplyResources(groupAir, "groupAir");
		groupAir.Name = "groupAir";
		groupAir.TabStop = false;
		toolTip.SetToolTip(groupAir, resources.GetString("groupAir.ToolTip"));
		// 
		// labelTemperatureCaption
		// 
		resources.ApplyResources(labelTemperatureCaption, "labelTemperatureCaption");
		labelTemperatureCaption.Name = "labelTemperatureCaption";
		toolTip.SetToolTip(labelTemperatureCaption, resources.GetString("labelTemperatureCaption.ToolTip"));
		// 
		// numericTemperature
		// 
		numericTemperature.DecimalPlaces = 1;
		numericTemperature.Increment = new decimal(new int[] { 5, 0, 0, 65536 });
		resources.ApplyResources(numericTemperature, "numericTemperature");
		numericTemperature.Maximum = new decimal(new int[] { 60, 0, 0, 0 });
		numericTemperature.Minimum = new decimal(new int[] { 30, 0, 0, int.MinValue });
		numericTemperature.Name = "numericTemperature";
		toolTip.SetToolTip(numericTemperature, resources.GetString("numericTemperature.ToolTip"));
		numericTemperature.ValueChanged += Input_ValueChanged;
		// 
		// trackTemperature
		// 
		resources.ApplyResources(trackTemperature, "trackTemperature");
		trackTemperature.Name = "trackTemperature";
		trackTemperature.TickStyle = TickStyle.None;
		// 
		// labelHumidityCaption
		// 
		resources.ApplyResources(labelHumidityCaption, "labelHumidityCaption");
		labelHumidityCaption.Name = "labelHumidityCaption";
		toolTip.SetToolTip(labelHumidityCaption, resources.GetString("labelHumidityCaption.ToolTip"));
		// 
		// numericHumidity
		// 
		numericHumidity.DecimalPlaces = 1;
		resources.ApplyResources(numericHumidity, "numericHumidity");
		numericHumidity.Name = "numericHumidity";
		toolTip.SetToolTip(numericHumidity, resources.GetString("numericHumidity.ToolTip"));
		numericHumidity.ValueChanged += Input_ValueChanged;
		// 
		// trackHumidity
		// 
		resources.ApplyResources(trackHumidity, "trackHumidity");
		trackHumidity.Name = "trackHumidity";
		trackHumidity.TickStyle = TickStyle.None;
		// 
		// labelWaterCaption
		// 
		resources.ApplyResources(labelWaterCaption, "labelWaterCaption");
		labelWaterCaption.Name = "labelWaterCaption";
		toolTip.SetToolTip(labelWaterCaption, resources.GetString("labelWaterCaption.ToolTip"));
		// 
		// numericWater
		// 
		numericWater.DecimalPlaces = 2;
		numericWater.Increment = new decimal(new int[] { 1, 0, 0, 65536 });
		resources.ApplyResources(numericWater, "numericWater");
		numericWater.Maximum = new decimal(new int[] { 200, 0, 0, 0 });
		numericWater.Name = "numericWater";
		toolTip.SetToolTip(numericWater, resources.GetString("numericWater.ToolTip"));
		numericWater.ValueChanged += Water_ValueChanged;
		// 
		// trackWater
		// 
		resources.ApplyResources(trackWater, "trackWater");
		trackWater.Name = "trackWater";
		trackWater.TickStyle = TickStyle.None;
		// 
		// buttonFromDevice
		// 
		resources.ApplyResources(buttonFromDevice, "buttonFromDevice");
		buttonFromDevice.Name = "buttonFromDevice";
		toolTip.SetToolTip(buttonFromDevice, resources.GetString("buttonFromDevice.ToolTip"));
		buttonFromDevice.UseVisualStyleBackColor = true;
		buttonFromDevice.Click += FromDevice_Click;
		// 
		// labelDewCaption
		// 
		resources.ApplyResources(labelDewCaption, "labelDewCaption");
		labelDewCaption.Name = "labelDewCaption";
		toolTip.SetToolTip(labelDewCaption, resources.GetString("labelDewCaption.ToolTip"));
		// 
		// labelDew
		// 
		resources.ApplyResources(labelDew, "labelDew");
		labelDew.Name = "labelDew";
		toolTip.SetToolTip(labelDew, resources.GetString("labelDew.ToolTip"));
		// 
		// labelRatioCaption
		// 
		resources.ApplyResources(labelRatioCaption, "labelRatioCaption");
		labelRatioCaption.Name = "labelRatioCaption";
		toolTip.SetToolTip(labelRatioCaption, resources.GetString("labelRatioCaption.ToolTip"));
		// 
		// labelRatio
		// 
		resources.ApplyResources(labelRatio, "labelRatio");
		labelRatio.Name = "labelRatio";
		toolTip.SetToolTip(labelRatio, resources.GetString("labelRatio.ToolTip"));
		// 
		// labelOtherCaption
		// 
		resources.ApplyResources(labelOtherCaption, "labelOtherCaption");
		labelOtherCaption.Name = "labelOtherCaption";
		toolTip.SetToolTip(labelOtherCaption, resources.GetString("labelOtherCaption.ToolTip"));
		// 
		// numericOtherTemperature
		// 
		numericOtherTemperature.DecimalPlaces = 1;
		numericOtherTemperature.Increment = new decimal(new int[] { 5, 0, 0, 65536 });
		resources.ApplyResources(numericOtherTemperature, "numericOtherTemperature");
		numericOtherTemperature.Maximum = new decimal(new int[] { 60, 0, 0, 0 });
		numericOtherTemperature.Minimum = new decimal(new int[] { 30, 0, 0, int.MinValue });
		numericOtherTemperature.Name = "numericOtherTemperature";
		toolTip.SetToolTip(numericOtherTemperature, resources.GetString("numericOtherTemperature.ToolTip"));
		numericOtherTemperature.ValueChanged += Input_ValueChanged;
		// 
		// trackOtherTemperature
		// 
		resources.ApplyResources(trackOtherTemperature, "trackOtherTemperature");
		trackOtherTemperature.Name = "trackOtherTemperature";
		trackOtherTemperature.TickStyle = TickStyle.None;
		// 
		// labelOther
		// 
		resources.ApplyResources(labelOther, "labelOther");
		labelOther.Name = "labelOther";
		toolTip.SetToolTip(labelOther, resources.GetString("labelOther.ToolTip"));
		// 
		// groupRoom
		// 
		groupRoom.Controls.Add(labelVolumeCaption);
		groupRoom.Controls.Add(numericVolume);
		groupRoom.Controls.Add(trackVolume);
		groupRoom.Controls.Add(labelBufferCaption);
		groupRoom.Controls.Add(numericBuffer);
		groupRoom.Controls.Add(trackBuffer);
		groupRoom.Controls.Add(labelTargetCaption);
		groupRoom.Controls.Add(numericTarget);
		groupRoom.Controls.Add(trackTarget);
		groupRoom.Controls.Add(labelRemoveCaption);
		groupRoom.Controls.Add(labelRemove);
		resources.ApplyResources(groupRoom, "groupRoom");
		groupRoom.Name = "groupRoom";
		groupRoom.TabStop = false;
		toolTip.SetToolTip(groupRoom, resources.GetString("groupRoom.ToolTip"));
		// 
		// labelVolumeCaption
		// 
		resources.ApplyResources(labelVolumeCaption, "labelVolumeCaption");
		labelVolumeCaption.Name = "labelVolumeCaption";
		toolTip.SetToolTip(labelVolumeCaption, resources.GetString("labelVolumeCaption.ToolTip"));
		// 
		// numericVolume
		// 
		numericVolume.DecimalPlaces = 1;
		resources.ApplyResources(numericVolume, "numericVolume");
		numericVolume.Maximum = new decimal(new int[] { 10000, 0, 0, 0 });
		numericVolume.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
		numericVolume.Name = "numericVolume";
		toolTip.SetToolTip(numericVolume, resources.GetString("numericVolume.ToolTip"));
		numericVolume.Value = new decimal(new int[] { 1, 0, 0, 0 });
		numericVolume.ValueChanged += Input_ValueChanged;
		// 
		// trackVolume
		// 
		resources.ApplyResources(trackVolume, "trackVolume");
		trackVolume.Name = "trackVolume";
		trackVolume.TickStyle = TickStyle.None;
		// 
		// labelBufferCaption
		// 
		resources.ApplyResources(labelBufferCaption, "labelBufferCaption");
		labelBufferCaption.Name = "labelBufferCaption";
		toolTip.SetToolTip(labelBufferCaption, resources.GetString("labelBufferCaption.ToolTip"));
		// 
		// numericBuffer
		// 
		numericBuffer.DecimalPlaces = 1;
		numericBuffer.Increment = new decimal(new int[] { 5, 0, 0, 65536 });
		resources.ApplyResources(numericBuffer, "numericBuffer");
		numericBuffer.Maximum = new decimal(new int[] { 50, 0, 0, 0 });
		numericBuffer.Minimum = new decimal(new int[] { 1, 0, 0, 65536 });
		numericBuffer.Name = "numericBuffer";
		toolTip.SetToolTip(numericBuffer, resources.GetString("numericBuffer.ToolTip"));
		numericBuffer.Value = new decimal(new int[] { 1, 0, 0, 65536 });
		numericBuffer.ValueChanged += Input_ValueChanged;
		// 
		// trackBuffer
		// 
		resources.ApplyResources(trackBuffer, "trackBuffer");
		trackBuffer.Name = "trackBuffer";
		trackBuffer.TickStyle = TickStyle.None;
		// 
		// labelTargetCaption
		// 
		resources.ApplyResources(labelTargetCaption, "labelTargetCaption");
		labelTargetCaption.Name = "labelTargetCaption";
		toolTip.SetToolTip(labelTargetCaption, resources.GetString("labelTargetCaption.ToolTip"));
		// 
		// numericTarget
		// 
		resources.ApplyResources(numericTarget, "numericTarget");
		numericTarget.Name = "numericTarget";
		toolTip.SetToolTip(numericTarget, resources.GetString("numericTarget.ToolTip"));
		numericTarget.ValueChanged += Input_ValueChanged;
		// 
		// trackTarget
		// 
		resources.ApplyResources(trackTarget, "trackTarget");
		trackTarget.Name = "trackTarget";
		trackTarget.TickStyle = TickStyle.None;
		// 
		// labelRemoveCaption
		// 
		resources.ApplyResources(labelRemoveCaption, "labelRemoveCaption");
		labelRemoveCaption.Name = "labelRemoveCaption";
		toolTip.SetToolTip(labelRemoveCaption, resources.GetString("labelRemoveCaption.ToolTip"));
		// 
		// labelRemove
		// 
		resources.ApplyResources(labelRemove, "labelRemove");
		labelRemove.Name = "labelRemove";
		toolTip.SetToolTip(labelRemove, resources.GetString("labelRemove.ToolTip"));
		// 
		// groupForecast
		// 
		groupForecast.Controls.Add(labelExchangeCaption);
		groupForecast.Controls.Add(numericExchange);
		groupForecast.Controls.Add(trackExchange);
		groupForecast.Controls.Add(labelOutdoorCaption);
		groupForecast.Controls.Add(numericOutdoorTemperature);
		groupForecast.Controls.Add(trackOutdoorTemperature);
		groupForecast.Controls.Add(numericOutdoorHumidity);
		groupForecast.Controls.Add(trackOutdoorHumidity);
		groupForecast.Controls.Add(labelSourcesCaption);
		groupForecast.Controls.Add(numericSources);
		groupForecast.Controls.Add(trackSources);
		groupForecast.Controls.Add(labelRatedCaption);
		groupForecast.Controls.Add(numericRated);
		groupForecast.Controls.Add(trackRated);
		groupForecast.Controls.Add(labelRatedHint);
		groupForecast.Controls.Add(buttonFromRecords);
		groupForecast.Controls.Add(labelForecast);
		resources.ApplyResources(groupForecast, "groupForecast");
		groupForecast.Name = "groupForecast";
		groupForecast.TabStop = false;
		toolTip.SetToolTip(groupForecast, resources.GetString("groupForecast.ToolTip"));
		// 
		// labelExchangeCaption
		// 
		resources.ApplyResources(labelExchangeCaption, "labelExchangeCaption");
		labelExchangeCaption.Name = "labelExchangeCaption";
		toolTip.SetToolTip(labelExchangeCaption, resources.GetString("labelExchangeCaption.ToolTip"));
		// 
		// numericExchange
		// 
		numericExchange.DecimalPlaces = 2;
		numericExchange.Increment = new decimal(new int[] { 1, 0, 0, 65536 });
		resources.ApplyResources(numericExchange, "numericExchange");
		numericExchange.Maximum = new decimal(new int[] { 20, 0, 0, 0 });
		numericExchange.Name = "numericExchange";
		toolTip.SetToolTip(numericExchange, resources.GetString("numericExchange.ToolTip"));
		numericExchange.ValueChanged += Input_ValueChanged;
		// 
		// trackExchange
		// 
		resources.ApplyResources(trackExchange, "trackExchange");
		trackExchange.Name = "trackExchange";
		trackExchange.TickStyle = TickStyle.None;
		// 
		// labelOutdoorCaption
		// 
		resources.ApplyResources(labelOutdoorCaption, "labelOutdoorCaption");
		labelOutdoorCaption.Name = "labelOutdoorCaption";
		toolTip.SetToolTip(labelOutdoorCaption, resources.GetString("labelOutdoorCaption.ToolTip"));
		// 
		// numericOutdoorTemperature
		// 
		resources.ApplyResources(numericOutdoorTemperature, "numericOutdoorTemperature");
		numericOutdoorTemperature.DecimalPlaces = 1;
		numericOutdoorTemperature.Maximum = new decimal(new int[] { 50, 0, 0, 0 });
		numericOutdoorTemperature.Minimum = new decimal(new int[] { 40, 0, 0, int.MinValue });
		numericOutdoorTemperature.Name = "numericOutdoorTemperature";
		toolTip.SetToolTip(numericOutdoorTemperature, resources.GetString("numericOutdoorTemperature.ToolTip"));
		numericOutdoorTemperature.ValueChanged += Input_ValueChanged;
		// 
		// trackOutdoorTemperature
		// 
		resources.ApplyResources(trackOutdoorTemperature, "trackOutdoorTemperature");
		trackOutdoorTemperature.Name = "trackOutdoorTemperature";
		trackOutdoorTemperature.TickStyle = TickStyle.None;
		// 
		// numericOutdoorHumidity
		// 
		resources.ApplyResources(numericOutdoorHumidity, "numericOutdoorHumidity");
		numericOutdoorHumidity.Name = "numericOutdoorHumidity";
		toolTip.SetToolTip(numericOutdoorHumidity, resources.GetString("numericOutdoorHumidity.ToolTip"));
		numericOutdoorHumidity.ValueChanged += Input_ValueChanged;
		// 
		// trackOutdoorHumidity
		// 
		resources.ApplyResources(trackOutdoorHumidity, "trackOutdoorHumidity");
		trackOutdoorHumidity.Name = "trackOutdoorHumidity";
		trackOutdoorHumidity.TickStyle = TickStyle.None;
		// 
		// labelSourcesCaption
		// 
		resources.ApplyResources(labelSourcesCaption, "labelSourcesCaption");
		labelSourcesCaption.Name = "labelSourcesCaption";
		toolTip.SetToolTip(labelSourcesCaption, resources.GetString("labelSourcesCaption.ToolTip"));
		// 
		// numericSources
		// 
		numericSources.Increment = new decimal(new int[] { 10, 0, 0, 0 });
		resources.ApplyResources(numericSources, "numericSources");
		numericSources.Maximum = new decimal(new int[] { 5000, 0, 0, 0 });
		numericSources.Minimum = new decimal(new int[] { 5000, 0, 0, int.MinValue });
		numericSources.Name = "numericSources";
		toolTip.SetToolTip(numericSources, resources.GetString("numericSources.ToolTip"));
		numericSources.ValueChanged += Input_ValueChanged;
		// 
		// trackSources
		// 
		resources.ApplyResources(trackSources, "trackSources");
		trackSources.Name = "trackSources";
		trackSources.TickStyle = TickStyle.None;
		// 
		// labelRatedCaption
		// 
		resources.ApplyResources(labelRatedCaption, "labelRatedCaption");
		labelRatedCaption.Name = "labelRatedCaption";
		toolTip.SetToolTip(labelRatedCaption, resources.GetString("labelRatedCaption.ToolTip"));
		// 
		// numericRated
		// 
		numericRated.DecimalPlaces = 1;
		resources.ApplyResources(numericRated, "numericRated");
		numericRated.Minimum = new decimal(new int[] { 1, 0, 0, 65536 });
		numericRated.Name = "numericRated";
		toolTip.SetToolTip(numericRated, resources.GetString("numericRated.ToolTip"));
		numericRated.Value = new decimal(new int[] { 1, 0, 0, 65536 });
		numericRated.ValueChanged += Input_ValueChanged;
		// 
		// trackRated
		// 
		resources.ApplyResources(trackRated, "trackRated");
		trackRated.Name = "trackRated";
		trackRated.TickStyle = TickStyle.None;
		// 
		// labelRatedHint
		// 
		resources.ApplyResources(labelRatedHint, "labelRatedHint");
		labelRatedHint.ForeColor = SystemColors.GrayText;
		labelRatedHint.Name = "labelRatedHint";
		toolTip.SetToolTip(labelRatedHint, resources.GetString("labelRatedHint.ToolTip"));
		// 
		// buttonFromRecords
		// 
		resources.ApplyResources(buttonFromRecords, "buttonFromRecords");
		buttonFromRecords.Name = "buttonFromRecords";
		toolTip.SetToolTip(buttonFromRecords, resources.GetString("buttonFromRecords.ToolTip"));
		buttonFromRecords.UseVisualStyleBackColor = true;
		buttonFromRecords.Click += FromRecords_Click;
		// 
		// labelForecast
		// 
		resources.ApplyResources(labelForecast, "labelForecast");
		labelForecast.Name = "labelForecast";
		toolTip.SetToolTip(labelForecast, resources.GetString("labelForecast.ToolTip"));
		// 
		// toolTip
		// 
		toolTip.AutoPopDelay = 32767;
		toolTip.InitialDelay = 500;
		toolTip.ReshowDelay = 100;
		// 
		// CalculatorForm
		// 
		resources.ApplyResources(this, "$this");
		AutoScaleMode = AutoScaleMode.Font;
		Controls.Add(groupAir);
		Controls.Add(groupRoom);
		Controls.Add(groupForecast);
		FormBorderStyle = FormBorderStyle.FixedSingle;
		MaximizeBox = false;
		Name = "CalculatorForm";
		groupAir.ResumeLayout(false);
		groupAir.PerformLayout();
		((System.ComponentModel.ISupportInitialize)numericTemperature).EndInit();
		((System.ComponentModel.ISupportInitialize)trackTemperature).EndInit();
		((System.ComponentModel.ISupportInitialize)numericHumidity).EndInit();
		((System.ComponentModel.ISupportInitialize)trackHumidity).EndInit();
		((System.ComponentModel.ISupportInitialize)numericWater).EndInit();
		((System.ComponentModel.ISupportInitialize)trackWater).EndInit();
		((System.ComponentModel.ISupportInitialize)numericOtherTemperature).EndInit();
		((System.ComponentModel.ISupportInitialize)trackOtherTemperature).EndInit();
		groupRoom.ResumeLayout(false);
		groupRoom.PerformLayout();
		((System.ComponentModel.ISupportInitialize)numericVolume).EndInit();
		((System.ComponentModel.ISupportInitialize)trackVolume).EndInit();
		((System.ComponentModel.ISupportInitialize)numericBuffer).EndInit();
		((System.ComponentModel.ISupportInitialize)trackBuffer).EndInit();
		((System.ComponentModel.ISupportInitialize)numericTarget).EndInit();
		((System.ComponentModel.ISupportInitialize)trackTarget).EndInit();
		groupForecast.ResumeLayout(false);
		groupForecast.PerformLayout();
		((System.ComponentModel.ISupportInitialize)numericExchange).EndInit();
		((System.ComponentModel.ISupportInitialize)trackExchange).EndInit();
		((System.ComponentModel.ISupportInitialize)numericOutdoorTemperature).EndInit();
		((System.ComponentModel.ISupportInitialize)trackOutdoorTemperature).EndInit();
		((System.ComponentModel.ISupportInitialize)numericOutdoorHumidity).EndInit();
		((System.ComponentModel.ISupportInitialize)trackOutdoorHumidity).EndInit();
		((System.ComponentModel.ISupportInitialize)numericSources).EndInit();
		((System.ComponentModel.ISupportInitialize)trackSources).EndInit();
		((System.ComponentModel.ISupportInitialize)numericRated).EndInit();
		((System.ComponentModel.ISupportInitialize)trackRated).EndInit();
		ResumeLayout(false);
	}

	#endregion

	private NumericUpDown numericTemperature;
	private NumericUpDown numericHumidity;
	private NumericUpDown numericWater;
	private Label labelDew;
	private Label labelRatio;
	private NumericUpDown numericOtherTemperature;
	private Label labelOther;
	private NumericUpDown numericVolume;
	private NumericUpDown numericBuffer;
	private NumericUpDown numericTarget;
	private Label labelRemove;
	private NumericUpDown numericExchange;
	private NumericUpDown numericOutdoorTemperature;
	private NumericUpDown numericOutdoorHumidity;
	private NumericUpDown numericSources;
	private NumericUpDown numericRated;
	private Label labelForecast;
	private ToolTip toolTip;
	private TrackBar trackTemperature;
	private TrackBar trackHumidity;
	private TrackBar trackWater;
	private TrackBar trackOtherTemperature;
	private TrackBar trackVolume;
	private TrackBar trackBuffer;
	private TrackBar trackTarget;
	private TrackBar trackExchange;
	private TrackBar trackOutdoorTemperature;
	private TrackBar trackOutdoorHumidity;
	private TrackBar trackSources;
	private TrackBar trackRated;
}

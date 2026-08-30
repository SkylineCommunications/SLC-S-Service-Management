namespace SLC_SM_IAS_Profiles.Views
{
	using System;
	using System.Linq;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;
	using SLC_SM_IAS_Profiles.Presenters;

	public class NumberRow : ConfigurationRow
	{
		public NumberRow(ConfigurationRowData data)
			: base(data)
		{
		}

		public override InteractiveWidget Value { get; set; }

		public override Row Configure()
		{
			base.Configure();

			double minimum = Data.Record.ConfigurationParameter.NumberOptions.MinRange ?? double.MinValue;
			double maximum = Data.Record.ConfigurationParameter.NumberOptions.MaxRange ?? double.MaxValue;
			int decimalVal = Convert.ToInt32(Data.Record.ConfigurationParameter.NumberOptions.Decimals);
			double stepSize = Data.Record.ConfigurationParameter.NumberOptions.StepSize ?? 1;
			var value = new Numeric(Data.Record.ConfigurationParameter.NumberOptions.DefaultValue ?? 0)
			{
				Minimum = minimum,
				Maximum = maximum,
				StepSize = stepSize,
				Decimals = decimalVal,
				IsEnabled = Data.Record.RecordType != RecordType.Reference,
			};
			Unit.Selected = Unit.Options.FirstOrDefault(x => x?.DisplayValue == Data.Record.ConfigurationParameter.NumberOptions.DefaultUnit?.Name)?.Value;
			Unit.IsEnabled = Data.Record.RecordType != RecordType.Reference;
			Start.Value = minimum;
			Start.IsEnabled = Data.Record.RecordType != RecordType.Reference;
			End.Value = maximum;
			End.IsEnabled = Data.Record.RecordType != RecordType.Reference;
			Decimals.Value = decimalVal;
			Decimals.IsEnabled = Data.Record.RecordType != RecordType.Reference;
			Step.Value = stepSize;
			Step.StepSize = 1 / Math.Pow(10, decimalVal);
			Step.Decimals = decimalVal;
			Step.IsEnabled = Data.Record.RecordType != RecordType.Reference;

			Start.Changed += (sender, args) => Data.Callbacks.ConfigurationParameter.Handle_Number_Start_Changed(Data.Record, Step, value, args.Value);
			End.Changed += (sender, args) => Data.Callbacks.ConfigurationParameter.Handle_Number_End_Changed(Data.Record, Step, value, args.Value);
			Decimals.Changed += (sender, args) => Data.Callbacks.ConfigurationParameter.Handle_Number_Decimals_Changed(Data.Record, Step, value, args.Value);
			Step.Changed += (sender, args) => Data.Callbacks.ConfigurationParameter.Handle_Number_Step_Changed(Data.Record, value, args.Value);
			Unit.Changed += (sender, args) => Data.Callbacks.ConfigurationParameter.Handle_Number_Unit_Changed(Data.Record, args.Selected);
			value.Changed += (sender, args) => Data.Callbacks.ConfigurationParameter.Handle_Number_Value_Changed(Data.Record, args.Value);

			Value = value;
			return this;
		}
	}
}

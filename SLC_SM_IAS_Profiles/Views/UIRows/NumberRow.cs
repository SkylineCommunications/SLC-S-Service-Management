namespace SLC_SM_IAS_Profiles.Views
{
	using System;
	using System.Linq;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;

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

			double minimum = Data.Record.ConfigurationParameterValue.NumberOptions.MinRange ?? -10_000;
			double maximum = Data.Record.ConfigurationParameterValue.NumberOptions.MaxRange ?? 10_000;
			int decimalVal = Convert.ToInt32(Data.Record.ConfigurationParameterValue.NumberOptions.Decimals);
			double stepSize = Data.Record.ConfigurationParameterValue.NumberOptions.StepSize ?? 1;
			var value = new Numeric(Data.Record.ConfigurationParameterValue.NumberOptions.DefaultValue ?? 0)
			{
				Minimum = minimum,
				Maximum = maximum,
				StepSize = stepSize,
				Decimals = decimalVal,
				IsEnabled = true,
			};

			Unit.Selected = Unit.Options.FirstOrDefault(x => x?.DisplayValue == Data.Record.ConfigurationParameterValue.NumberOptions.DefaultUnit?.Name)?.Value;
			Unit.IsEnabled = true;
			Start.Value = minimum;
			Start.IsEnabled = true;
			End.Value = maximum;
			End.IsEnabled = true;
			Decimals.Value = decimalVal;
			Decimals.IsEnabled = true;
			Step.Value = stepSize;
			Step.StepSize = 1 / Math.Pow(10, decimalVal);
			Step.Decimals = decimalVal;
			Step.IsEnabled = true;
			value.Value = Data.Record.ConfigurationParameterValue.DoubleValue ?? 0;

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

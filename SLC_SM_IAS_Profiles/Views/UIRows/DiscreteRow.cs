namespace SLC_SM_IAS_Profiles.Views
{
	using System.Collections.Generic;
	using System.Linq;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;

	public class DiscreteRow : ConfigurationRow
	{
		public DiscreteRow(ConfigurationRowData data)
			: base(data)
		{
		}

		public override InteractiveWidget Value { get; set; }

		public override Row Configure()
		{
			base.Configure();

			var options = Data.Record.ConfigurationParameterValue.DiscreteOptions;
			var discretes = options.DiscreteValues
				.Select(x => new Option<Models.DiscreteValue>(x.Value, x))
				.OrderBy(x => x.DisplayValue)
				.ToList();

			BuildAndConfigureValue(discretes);
			ConfigureButtonSettings();

			return this;
		}

		private void BuildAndConfigureValue(List<Option<Models.DiscreteValue>> discretes)
		{
			var value = new DropDown<Models.DiscreteValue>(discretes);
			value.IsEnabled = true;

			value.Selected = value.Options.First(x => x.DisplayValue == Data.Record.ConfigurationParameterValue.StringValue).Value;
			Value = value;
			value.Changed += (sender, args) => Data.Callbacks.ConfigurationParameter.Handle_Discrete_Value_Change(Data.Record, value.Selected);
		}

		private void ConfigureButtonSettings()
		{
			BtnSettings.IsEnabled = true;
			BtnSettings.Pressed += (sender, args) => Data.Callbacks.ConfigurationParameter.Handle_Discrete_Values_Button_Pressed(Data.Record, Value as DropDown<Models.DiscreteValue>);
		}
	}
}
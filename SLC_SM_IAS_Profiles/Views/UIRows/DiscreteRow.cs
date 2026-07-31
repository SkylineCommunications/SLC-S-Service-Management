namespace SLC_SM_IAS_Profiles.Views
{
	using System.Collections.Generic;
	using System.Linq;
	using Skyline.DataMiner.Automation;
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

		private static void SetDropDownValidation(DropDown<Models.DiscreteValue> dropDown, bool isMissingMandatoryValue)
		{
			dropDown.ValidationState = isMissingMandatoryValue ? UIValidationState.Invalid : UIValidationState.Valid;
			dropDown.ValidationText = isMissingMandatoryValue ? "Mandatory parameter. Please select a value" : string.Empty;
		}

		private void BuildAndConfigureValue(List<Option<Models.DiscreteValue>> discretes)
		{
			discretes.Insert(0, new Option<Models.DiscreteValue>("- Select -", null));

			var value = new DropDown<Models.DiscreteValue>(discretes);
			value.IsEnabled = true;

			var stringValue = Data.Record.ConfigurationParameterValue.StringValue;
			var match = value.Options.FirstOrDefault(x => x.Value != null && x.DisplayValue == stringValue);

			value.Selected = match != null ? match.Value : null;
			SetDropDownValidation(value, Data.IsMandatory && match == null);

			Value = value;
			value.Changed += (sender, args) =>
			{
				SetDropDownValidation(value, Data.IsMandatory && value.Selected == null);
				Data.Callbacks.ConfigurationParameter.Handle_Discrete_Value_Change(Data.Record, value.Selected);
			};
		}

		private void ConfigureButtonSettings()
		{
			BtnSettings.IsEnabled = true;
			BtnSettings.Pressed += (sender, args) =>
				Data.Callbacks.ConfigurationParameter.Handle_Discrete_Values_Button_Pressed(Data.Record, Value as DropDown<Models.DiscreteValue>);
		}
	}
}
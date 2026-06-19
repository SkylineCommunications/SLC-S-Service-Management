namespace SLC_SM_IAS_Profiles.Views
{
	using System.Text.RegularExpressions;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;
	using SLC_SM_IAS_Profiles.Presenters;

	public class TextRow : ConfigurationRow
	{
		public TextRow(ConfigurationRowData data)
			: base(data)
		{
		}

		public override InteractiveWidget Value { get; set; }

		public override Row Configure()
		{
			base.Configure();

			var value = new TextBox(Data.Record.ConfigurationParameterValue.StringValue ?? string.Empty)
			{
				Tooltip = Data.Record.ConfigurationParameterValue.TextOptions.UserMessage ?? string.Empty,
				IsEnabled = true,
			};

			BtnSettings.IsEnabled = true;
			BtnSettings.Pressed += (sender, args) => Data.Callbacks.ConfigurationParameter.Handle_Text_Values_Button_Pressed(Data.Record, value, ValidateTextValue);

			value.Changed += (sender, args) => Data.Callbacks.ConfigurationParameter.Handle_Text_Value_Changed(Data.Record, value, args.Value, ValidateTextValue);

			Value = value;

			return this;
		}

		private bool ValidateTextValue(ConfigurationDataRecord record, TextBox textBox, string newValue)
		{
			if (record.ConfigurationParameterValue.TextOptions.Regex != null
				&& !Regex.IsMatch(newValue, record.ConfigurationParameterValue.TextOptions.Regex))
			{
				textBox.ValidationState = UIValidationState.Invalid;
				textBox.ValidationText = string.IsNullOrEmpty(record.ConfigurationParameterValue.TextOptions.UserMessage) ?
					$"Input did not match Regex '{record.ConfigurationParameterValue.TextOptions.Regex}' - reverted to previous value" :
					record.ConfigurationParameterValue.TextOptions.UserMessage;
				textBox.Text = string.Empty;
				return false;
			}

			textBox.ValidationState = UIValidationState.Valid;
			textBox.ValidationText = record.ConfigurationParameterValue.TextOptions.UserMessage;

			return true;
		}
	}
}

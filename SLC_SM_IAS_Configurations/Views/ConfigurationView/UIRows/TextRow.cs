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

			var value = new TextBox(Data.Record.ConfigurationParameter.TextOptions.Default ?? string.Empty)
			{
				Tooltip = Data.Record.ConfigurationParameter.TextOptions.UserMessage ?? string.Empty,
				IsEnabled = Data.Record.RecordType != RecordType.Reference,
			};

			BtnSettings.IsEnabled = Data.Record.RecordType != RecordType.Reference;

			value.Changed += (sender, args) => Data.Callbacks.ConfigurationParameter.Handle_Text_Value_Changed(Data.Record, value, args.Value, ValidateTextValue);
			BtnSettings.Pressed += (sender, args) => Data.Callbacks.ConfigurationParameter.Handle_Text_Values_Button_Pressed(Data.Record, value, ValidateTextValue);

			Value = value;
			return this;
		}

		private bool ValidateTextValue(ConfigurationDataRecord record, TextBox textBox, string newValue)
		{
			if (record.ConfigurationParameter.TextOptions.Regex != null && !Regex.IsMatch(newValue, record.ConfigurationParameter.TextOptions.Regex))
			{
				textBox.ValidationState = UIValidationState.Invalid;
				textBox.ValidationText = $"Input did not match Regex '{record.ConfigurationParameter.TextOptions.Regex}' - reverted to previous value";
				textBox.Text = string.Empty;
				return true;
			}

			textBox.ValidationState = UIValidationState.Valid;
			textBox.ValidationText = record.ConfigurationParameter.TextOptions.UserMessage;

			return true;
		}
	}
}

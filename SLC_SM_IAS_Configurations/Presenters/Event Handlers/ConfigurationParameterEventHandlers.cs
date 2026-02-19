namespace SLC_SM_IAS_Profiles.Presenters
{
	using System;
	using System.Linq;
	using DomHelpers.SlcConfigurations;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;
	using SLC_SM_IAS_Profiles.Views;

	public class ConfigurationParameterEventHandlers : AbstractEventHandlers
	{
		public ConfigurationParameterEventHandlers(IEngine engine, ConfigurationPresenter presenter)
			: base(engine, presenter)
		{
		}

		public void Handle_Configuration_Delete_Pressed(DataRecordPage page, ConfigurationDataRecord record)
		{
			var id = record.ConfigurationParameter.ID;

			if (page is ProfilePage profilePage)
				RemoveConfigurationParameterReference(profilePage.ProfileDefinitionRecord, id);

			record.State = State.Removed;

			presenter.BuildUI();
		}

		public void Handle_ConfigurationParameterDropdown_Changed(
			DataRecordPage page,
			ConfigurationDataRecord record,
			TextBox label,
			Button button,
			DropDown<Models.ConfigurationParameter>.DropDownChangedEventArgs args)
		{
			var profilePage = page as ProfilePage;
			if (profilePage == null)
				return;

			if (args.Previous == null && args.Selected == null) // this happens when leaving the page.
				return;

			var navigator = presenter.Navigator;

			record.State = State.Removed;

			// Remove previous reference
			RemoveConfigurationParameterReference(profilePage.ProfileDefinitionRecord, record.ConfigurationParameter.ID);

			if (args.Selected != null)
			{
				// User selected an existing profile definition
				AddConfigurationParameterReference(profilePage.ProfileDefinitionRecord, args.Selected.ID);
				navigator.AddRecordToCurrentPage(
					DataRecordFactory.CreateDataRecord(args.Selected, State.Equal, RecordType.Reference));

				// Update UI
				label.IsEnabled = false;
				label.Text = args.Selected.Name;
				button.IsEnabled = false;
			}
			else
			{
				// User selected "-New-"
				var newConfigurationParameter = CreateNewConfigurationParameter(navigator);

				AddConfigurationParameterReference(profilePage.ProfileDefinitionRecord, newConfigurationParameter.ID);
				navigator.AddRecordToCurrentPage(
					DataRecordFactory.CreateDataRecord(newConfigurationParameter, State.Updated, RecordType.New));

				// Update UI
				label.IsEnabled = true;
				label.Text = newConfigurationParameter.Name;
				button.IsEnabled = true;
			}

			presenter.BuildUI();
		}

		public void Handle_Type_Changed(ConfigurationDataRecord record, SlcConfigurationsIds.Enums.Type selected)
		{
			if (record.State == State.Removed)
				return;

			record.ConfigurationParameter.Type = selected;
			switch (selected)
			{
				case SlcConfigurationsIds.Enums.Type.Number:
					if (record.ConfigurationParameter.NumberOptions == null)
					{
						record.ConfigurationParameter.NumberOptions = new Models.NumberParameterOptions();
					}

					break;

				case SlcConfigurationsIds.Enums.Type.Discrete:
					if (record.ConfigurationParameter.DiscreteOptions == null)
					{
						record.ConfigurationParameter.DiscreteOptions = new Models.DiscreteParameterOptions();
					}

					break;

				case SlcConfigurationsIds.Enums.Type.Text:
					if (record.ConfigurationParameter.TextOptions == null)
					{
						record.ConfigurationParameter.TextOptions = new Models.TextParameterOptions();
					}

					break;

				default:
					break;
			}

			presenter.BuildUI();
		}

		public void Handle_Discrete_Values_Button_Pressed(ConfigurationDataRecord record, DropDown<Models.DiscreteValue> value)
		{
			if (record.State == State.Removed)
				return;

			var optionsView = new DiscreteValuesView(engine);
			var optionsPresenter = new DiscreteValuesPresenter(engine, optionsView, record.ConfigurationParameter.DiscreteOptions);

			var controller = presenter.Controller;
			var view = presenter.View;

			optionsView.BtnReturn.Pressed += (o, eventArgs) => controller.ShowDialog(view);
			optionsView.BtnApply.Pressed += (o, eventArgs) =>
			{
				var options = record.ConfigurationParameter.DiscreteOptions;
				var discretes = options.DiscreteValues
				.Select(x => new Option<Models.DiscreteValue>(x.Value, x))
				.OrderBy(x => x.DisplayValue)
				.ToList();

				value.SetOptions(discretes);
				record.State = State.Updated;
				controller.ShowDialog(view);
			};

			controller.ShowDialog(optionsView);
		}

		public void Handle_Text_Values_Button_Pressed(ConfigurationDataRecord record, TextBox textBox, Func<ConfigurationDataRecord, TextBox, string, bool> textValidator)
		{
			if (record.RecordType == RecordType.Reference)
				return;

			var optionsView = new TextOptionsView(engine);
			optionsView.Regex.Text = record.ConfigurationParameter.TextOptions.Regex;
			optionsView.UserMessage.Text = record.ConfigurationParameter.TextOptions.UserMessage;

			var controller = presenter.Controller;
			var view = presenter.View;

			optionsView.BtnReturn.Pressed += (o, eventArgs) => controller.ShowDialog(view);
			optionsView.BtnApply.Pressed += (o, eventArgs) =>
			{
				record.ConfigurationParameter.TextOptions.Regex = optionsView.Regex.Text;
				record.ConfigurationParameter.TextOptions.UserMessage = optionsView.UserMessage.Text;
				textValidator(record, textBox, textBox.Text);

				record.State = State.Updated;
				controller.ShowDialog(view);
			};

			controller.ShowDialog(optionsView);
		}

		public void Handle_Text_Value_Changed(ConfigurationDataRecord record, TextBox value, string strValue, Func<ConfigurationDataRecord, TextBox, string, bool> textValidator)
		{
			if (record.State == State.Removed)
				return;

			if (!textValidator(record, value, strValue))
			{
				return;
			}

			record.ConfigurationParameter.TextOptions.Default = strValue;
			record.State = State.Updated;
		}

		public void Handle_Discrete_Value_Change(ConfigurationDataRecord record, Models.DiscreteValue value)
		{
			if (record.State == State.Removed)
				return;

			record.ConfigurationParameter.DiscreteOptions.Default = value;
			record.State = State.Updated;
		}

		public void Handle_Number_Value_Changed(ConfigurationDataRecord record, double value)
		{
			if (record.State == State.Removed)
				return;

			record.ConfigurationParameter.NumberOptions.DefaultValue = value;
			record.State = State.Updated;
		}

		public void Handle_Number_Unit_Changed(ConfigurationDataRecord record, Models.ConfigurationUnit unit)
		{
			if (record.State == State.Removed)
				return;

			record.ConfigurationParameter.NumberOptions.DefaultUnit = unit;
			if (record.ConfigurationParameter.NumberOptions.Units == null)
			{
				record.ConfigurationParameter.NumberOptions.Units = new System.Collections.Generic.List<Models.ConfigurationUnit>();
			}

			record.ConfigurationParameter.NumberOptions.Units.Add(unit);
			record.ConfigurationParameter.NumberOptions.Units =	record.ConfigurationParameter.NumberOptions.Units.Distinct().ToList();
			record.State = State.Updated;
		}

		public void Handle_Number_Step_Changed(ConfigurationDataRecord record, Numeric value, double step)
		{
			if (record.State == State.Removed)
				return;

			value.StepSize = step;
			record.ConfigurationParameter.NumberOptions.StepSize = step;
			record.State = State.Updated;
		}

		public void Handle_Number_Decimals_Changed(ConfigurationDataRecord record, Numeric step, Numeric value, double stepSize)
		{
			if (record.State == State.Removed)
				return;

			value.Decimals = Convert.ToInt32(stepSize);
			step.Decimals = Convert.ToInt32(stepSize);
			double newStepsize = 1 / Math.Pow(10, stepSize);
			value.StepSize = newStepsize;
			step.StepSize = newStepsize;
			record.ConfigurationParameter.NumberOptions.Decimals = Convert.ToInt32(stepSize);
			record.State = State.Updated;
		}

		public void Handle_Number_End_Changed(ConfigurationDataRecord record, Numeric step, Numeric value, double end)
		{
			if (record.State == State.Removed)
				return;

			value.Maximum = end;
			step.Maximum = end;
			record.ConfigurationParameter.NumberOptions.MaxRange = end;
			record.State = State.Updated;
		}

		public void Handle_Number_Start_Changed(ConfigurationDataRecord record, Numeric step, Numeric value, double start)
		{
			if (record.State == State.Removed)
				return;

			value.Minimum = start;
			step.Minimum = start;
			record.ConfigurationParameter.NumberOptions.MinRange = start;
			record.State = State.Updated;
		}

		public void RemoveConfigurationParameterReference(ProfileDefinitionDataRecord record, Guid id)
		{
			var refs = record.ProfileDefinition.ConfigurationParameters;

			var toDelete = refs
				.Where(r => r.ConfigurationParameter == id)
				.ToList();

			if (toDelete.Count == 0)
				return;

			refs.RemoveAll(r => r.ConfigurationParameter == id);

			record.State = State.Updated;

			presenter.Model.TryDelete(toDelete);
		}
	}
}
namespace SLC_SM_IAS_Profiles.Presenters
{
	using System;
	using System.Linq;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;
	using SLC_SM_IAS_Profiles.Views;

	public class ConfigurationParameterEventHandlers : AbstractEventHandlers
	{
		public ConfigurationParameterEventHandlers(IEngine engine, ProfilePresenter presenter)
			: base(engine, presenter)
		{
		}

		public void Handle_Configuration_Delete_Pressed(DataRecordPage page, ConfigurationDataRecord record)
		{
			var id = record.ConfigurationParameterValue.ID;

			if (page is ProfilePage profilePage)
				RemoveConfigurationValueFromProfile(profilePage.ProfileDataRecord, id);

			record.State = State.Removed;

			presenter.BuildUI();
		}

		public void Handle_Discrete_Values_Button_Pressed(ConfigurationDataRecord record, DropDown<Models.DiscreteValue> values)
		{
			if (record.State == State.Removed)
				return;

			var controller = presenter.Controller;

			var allDiscretes = record.ReferredConfigurationParameter.DiscreteOptions.DiscreteValues
				.Select(x => new Option<Models.DiscreteValue>(x.Value, x))
				.OrderBy(x => x.DisplayValue)
				.ToList();

			var optionsView = new DiscreteValuesView(engine);
			optionsView.Options.SetOptions(allDiscretes);

			foreach (var option in optionsView.Options.Values.ToList())
			{
				if (values.Options.Any(o => o.Value.Equals(option)))
					optionsView.Options.Check(option);
			}

			optionsView.BtnReturn.Pressed += (_, __) => controller.ShowDialog(presenter.View);
			optionsView.BtnApply.Pressed += (_, __) =>
			{
				var checkedOptions = optionsView.Options.CheckedOptions.ToList();

				values.SetOptions(checkedOptions);
				record.ConfigurationParameterValue.DiscreteOptions.DiscreteValues = checkedOptions.Select(o => o.Value).ToList();

				record.State = State.Updated;
				controller.ShowDialog(presenter.View);
			};

			controller.ShowDialog(optionsView);
		}

		public void Handle_Text_Values_Button_Pressed(ConfigurationDataRecord record, TextBox textBox, Func<ConfigurationDataRecord, TextBox, string, bool> textValidator)
		{
			if (record.State == State.Removed)
				return;

			var controller = presenter.Controller;

			var optionsView = new TextOptionsView(engine);
			optionsView.Regex.Text = record.ConfigurationParameterValue.TextOptions.Regex;
			optionsView.UserMessage.Text = record.ConfigurationParameterValue.TextOptions.UserMessage;

			optionsView.BtnReturn.Pressed += (o, eventArgs) => controller.ShowDialog(presenter.View);
			optionsView.BtnApply.Pressed += (o, eventArgs) =>
			{
				record.ConfigurationParameterValue.TextOptions.Regex = optionsView.Regex.Text;
				record.ConfigurationParameterValue.TextOptions.UserMessage = optionsView.UserMessage.Text;
				textValidator(record, textBox, textBox.Text);

				record.State = State.Updated;
				controller.ShowDialog(presenter.View);
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

			record.ConfigurationParameterValue.TextOptions.Default = strValue;
			record.State = State.Updated;
		}

		public void Handle_Discrete_Value_Change(ConfigurationDataRecord record, Models.DiscreteValue value)
		{
			if (record.State == State.Removed)
				return;

			record.ConfigurationParameterValue.DiscreteOptions.Default = value;
			record.State = State.Updated;
		}

		public void Handle_Number_Value_Changed(ConfigurationDataRecord record, double value)
		{
			if (record.State == State.Removed)
				return;

			record.ConfigurationParameterValue.NumberOptions.DefaultValue = value;
			record.State = State.Updated;
		}

		public void Handle_Number_Unit_Changed(ConfigurationDataRecord record, Models.ConfigurationUnit unit)
		{
			if (record.State == State.Removed)
				return;

			record.ConfigurationParameterValue.NumberOptions.DefaultUnit = unit;
			if (record.ConfigurationParameterValue.NumberOptions.Units == null)
			{
				record.ConfigurationParameterValue.NumberOptions.Units = new System.Collections.Generic.List<Models.ConfigurationUnit>();
			}

			record.ConfigurationParameterValue.NumberOptions.Units.Add(unit);
			record.ConfigurationParameterValue.NumberOptions.Units = record.ConfigurationParameterValue.NumberOptions.Units.Distinct().ToList();
			record.State = State.Updated;
		}

		public void Handle_Number_Step_Changed(ConfigurationDataRecord record, Numeric value, double step)
		{
			if (record.State == State.Removed)
				return;

			value.StepSize = step;
			record.ConfigurationParameterValue.NumberOptions.StepSize = step;
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

			record.ConfigurationParameterValue.NumberOptions.Decimals = Convert.ToInt32(stepSize);
			record.State = State.Updated;
		}

		public void Handle_Number_End_Changed(ConfigurationDataRecord record, Numeric step, Numeric value, double end)
		{
			if (record.State == State.Removed)
				return;

			value.Maximum = end;
			step.Maximum = end;
			record.ConfigurationParameterValue.NumberOptions.MaxRange = end;
			record.State = State.Updated;
		}

		public void Handle_Number_Start_Changed(ConfigurationDataRecord record, Numeric step, Numeric value, double start)
		{
			if (record.State == State.Removed)
				return;

			value.Minimum = start;
			step.Minimum = start;
			record.ConfigurationParameterValue.NumberOptions.MinRange = start;
			record.State = State.Updated;
		}

		public void RemoveConfigurationValueFromProfile(ProfileDataRecord record, Guid id)
		{
			var refs = record.Profile.ConfigurationParameterValues;

			var toDelete = refs
				.Where(r => r.ID == id)
				.ToList();

			if (toDelete.Count == 0)
				return;

			refs.RemoveAll(r => r.ID == id);

			record.State = State.Updated;

			presenter.Model.TryDeleteConfigurationValues(toDelete);
		}
	}
}
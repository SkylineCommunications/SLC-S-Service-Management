namespace SLC_SM_IAS_Service_Spec_Configuration.Views
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using DomHelpers.SlcConfigurations;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;
	using Skyline.DataMiner.SDM;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;
	using SLC_SM_IAS_Service_Spec_Configuration.Model.DataRecords;

	public class NestedProfileView : Dialog
	{
		internal const int ValueColumnIndex = 3;

		public NestedProfileView(IEngine engine) : base(engine)
		{
			Title = "Edit Nested Profile";
			MinWidth = 900;
		}

		public Button ShowValueDetails { get; } = new Button("Show Value Details");

		public Button BackButton { get; } = new Button("Back");

		public Button SaveButton { get; } = new Button("Save") { Style = ButtonStyle.CallToAction };

		public void RenderParameterHeaders(int row, bool showDetails)
		{
			AddWidget(new Label("Label") { Style = TextStyle.Heading }, row, 0);
			AddWidget(new Label("Parameter") { Style = TextStyle.Heading }, row, 1);
			AddWidget(new Label("Value") { Style = TextStyle.Heading }, row, ValueColumnIndex);
			AddWidget(new Label("Unit") { Style = TextStyle.Heading, MaxWidth = 80 }, row, 4);

			if (!showDetails)
			{
				return;
			}

			AddWidget(new Label("Start") { Style = TextStyle.Heading, MaxWidth = 100 }, row, 6);
			AddWidget(new Label("End") { Style = TextStyle.Heading, MaxWidth = 100 }, row, 7);
			AddWidget(new Label("Step Size") { Style = TextStyle.Heading, MaxWidth = 100 }, row, 8);
			AddWidget(new Label("Decimals") { Style = TextStyle.Heading, MaxWidth = 80 }, row, 9);
		}

		internal void AddParameterValueWidget(IParameterDataRecord record, int row, bool isReusable, bool showDetails)
		{
			bool isDisabled = record.ConfigurationParamValue.ValueFixed || isReusable;

			switch (record.ConfigurationParam?.Type)
			{
				case SlcConfigurationsIds.Enums.Type.Discrete:
					AddDiscreteWidget(record, row, isDisabled);
					break;

				case SlcConfigurationsIds.Enums.Type.Number:
					AddNumericWidget(record, row, isDisabled, showDetails);
					break;

				default:
					var textBox = new TextBox(record.ConfigurationParamValue.StringValue ?? String.Empty) { IsEnabled = !isDisabled };
					textBox.Changed += (s, a) => record.ConfigurationParamValue.StringValue = a.Value;
					AddWidget(textBox, row, ValueColumnIndex);
					break;
			}
		}

		private void AddDiscreteWidget(IParameterDataRecord record, int row, bool isDisabled)
		{
			if (record.DiscreteOptions == null)
			{
				return;
			}

			var options = record.DiscreteValues
				.Select(x => new Option<DiscreteValue>(x.Value, x))
				.OrderBy(x => x.DisplayValue)
				.ToList();

			var dropDown = new DropDown<DiscreteValue>(options) { IsEnabled = !isDisabled };

			string currentValue = record.ConfigurationParamValue.StringValue;
			if (currentValue != null && options.Any(o => o.DisplayValue == currentValue))
			{
				dropDown.Selected = options.First(o => o.DisplayValue == currentValue).Value;
			}

			dropDown.Changed += (s, a) => record.ConfigurationParamValue.StringValue = a.SelectedOption.DisplayValue;
			AddWidget(dropDown, row, ValueColumnIndex);
		}

		private void AddNumericWidget(IParameterDataRecord record, int row, bool isDisabled, bool showDetails)
		{
			var numOptions = record.NumberOptions;
			double min = numOptions?.MinRange ?? int.MinValue;
			double max = numOptions?.MaxRange ?? int.MaxValue;
			int decimals = Convert.ToInt32(numOptions?.Decimals ?? 0);
			double step = numOptions?.StepSize ?? 1;

			var numericWidget = new Numeric(record.ConfigurationParamValue.DoubleValue ?? numOptions?.DefaultValue ?? 0)
			{
				Minimum = min,
				Maximum = max,
				Decimals = decimals,
				StepSize = step,
				IsEnabled = !isDisabled,
			};
			numericWidget.Changed += (s, a) => record.ConfigurationParamValue.DoubleValue = a.Value;
			AddWidget(numericWidget, row, ValueColumnIndex);

			var unitOptions = (record.Units ?? new List<ConfigurationUnit>())
				.Select(u => new Option<ConfigurationUnit>(u.Name, u))
				.ToList();
			unitOptions.Insert(0, new Option<ConfigurationUnit>("-", null));

			var unitDropDown = new DropDown<ConfigurationUnit>(unitOptions) { IsEnabled = !isDisabled, MaxWidth = 80 };
			var defaultUnit = record.Units?.FirstOrDefault(u => u.Identifier == numOptions?.DefaultUnitId.Identifier);
			if (defaultUnit != null && unitOptions.Any(o => o.Value?.Identifier == defaultUnit.Identifier))
			{
				unitDropDown.Selected = defaultUnit;
			}

			unitDropDown.Changed += (s, a) =>
			{
				if (numOptions != null)
				{
					numOptions.DefaultUnitId = a.Selected == null
						? default
						: new SdmObjectReference<ConfigurationUnit>(a.Selected.Identifier);
				}
			};
			AddWidget(unitDropDown, row, 4);

			if (!showDetails)
			{
				return;
			}

			var startWidget = new Numeric(min) { IsEnabled = !isDisabled, MaxWidth = 100 };
			startWidget.Changed += (s, a) =>
			{
				numericWidget.Minimum = a.Value;
				if (numOptions != null)
				{
					numOptions.MinRange = a.Value;
				}
			};
			AddWidget(startWidget, row, 6);

			var endWidget = new Numeric(max) { IsEnabled = !isDisabled, MaxWidth = 100 };
			endWidget.Changed += (s, a) =>
			{
				numericWidget.Maximum = a.Value;
				if (numOptions != null)
				{
					numOptions.MaxRange = a.Value;
				}
			};
			AddWidget(endWidget, row, 7);

			var stepWidget = new Numeric(step)
			{
				Minimum = 0,
				Maximum = 1,
				StepSize = 1 / Math.Pow(10, decimals),
				Decimals = decimals,
				IsEnabled = !isDisabled,
				MaxWidth = 100,
			};
			stepWidget.Changed += (s, a) =>
			{
				numericWidget.StepSize = a.Value;
				if (numOptions != null)
				{
					numOptions.StepSize = a.Value;
				}
			};
			AddWidget(stepWidget, row, 8);

			var decimalsWidget = new Numeric(decimals)
			{
				StepSize = 1,
				Minimum = 0,
				Maximum = 6,
				IsEnabled = !isDisabled,
				MaxWidth = 80,
			};
			decimalsWidget.Changed += (s, a) =>
			{
				int d = Convert.ToInt32(a.Value);
				numericWidget.Decimals = d;
				stepWidget.Decimals = d;
				stepWidget.StepSize = 1 / Math.Pow(10, d);
				if (numOptions != null)
				{
					numOptions.Decimals = d;
				}
			};
			AddWidget(decimalsWidget, row, 9);
		}
	}
}
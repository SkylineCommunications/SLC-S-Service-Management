namespace SLC_SM_IAS_Profiles.Presenters
{
	using System;
	using System.Linq;
	using Library;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;
	using SLC_SM_IAS_Profiles.Views;

	public class DiscreteValuesPresenter
	{
		private readonly IEngine engine;
		private readonly DiscreteValuesView view;
		private readonly Models.DiscreteParameterOptions options;

		public DiscreteValuesPresenter(IEngine engine, DiscreteValuesView view, Models.DiscreteParameterOptions options)
		{
			this.engine = engine;
			this.view = view;
			this.options = options;

			view.Value.Changed += (sender, args) => ValidateOption(args.Value);
			view.BtnAddOption.Pressed += (sender, args) =>
			{
				if (!ValidateOption(view.Value.Text))
				{
					return;
				}

				options.DiscreteValues.Add(new Models.DiscreteValue { Value = view.Value.Text });

				view.Value.Text = String.Empty;
				ValidateOption(view.Value.Text);
				Build();
			};

			ValidateOption(view.Value.Text);
			Build();
		}

		private bool ValidateOption(string value)
		{
			if (String.IsNullOrEmpty(value))
			{
				view.ErrorValue.Text = "Please enter a value";
				view.Value.ValidationState = UIValidationState.Invalid;
				return false;
			}

			if (options.DiscreteValues.Exists(d => d.Value == value))
			{
				view.ErrorValue.Text = "Option already exists";
				view.Value.ValidationState = UIValidationState.Invalid;
				return false;
			}

			view.ErrorValue.Text = String.Empty;
			view.Value.ValidationState = UIValidationState.Valid;
			return true;
		}

		private void Build()
		{
			view.Clear();
			view.Options.Clear();

			int row = 0;
			view.AddWidget(new Label("Manage Discrete Options") { Style = TextStyle.Heading }, row, 0);
			view.AddWidget(view.Value, ++row, 0, 1, 2);
			view.AddWidget(view.BtnAddOption, row, 2, 1, 2);
			view.AddWidget(view.ErrorValue, ++row, 0, 1, 2);

			view.AddWidget(new WhiteSpace(), ++row, 0);
			view.AddWidget(new Label("Options") { Style = TextStyle.Heading }, ++row, 0, 1, 2);

			int d = 0;
			foreach (Models.DiscreteValue discrete in options.DiscreteValues.OrderBy(x => x.Value))
			{
				var btnRemove = new Button(Defaults.SymbolCross) { Width = 60 };
				btnRemove.Pressed += (s, e) =>
				{
					options.DiscreteValues.Remove(discrete);
					Build();
				};

				view.Options.AddWidget(new Label(" • " + discrete.Value), d, 0, 1, 2);
				view.Options.AddWidget(btnRemove, d, 2);
				d++;
			}

			view.AddSection(view.Options, ++row, 0);
			row += options.DiscreteValues.Count;

			view.AddWidget(new WhiteSpace(), ++row, 0);
			view.AddWidget(view.BtnReturn, ++row, 0);
			view.AddWidget(view.BtnApply, row, 1);
			view.AddWidget(new WhiteSpace() { Width = 150 }, row, 2);
			view.AddWidget(new WhiteSpace() { Width = 150 }, row, 3);
			view.AddWidget(new WhiteSpace() { Width = 150 }, row, 4);
			view.AddWidget(new WhiteSpace() { Width = 150 }, row, 5);
			view.AddWidget(new WhiteSpace() { Width = 150 }, row, 6);
			view.AddWidget(new WhiteSpace() { Width = 150 }, row, 7);
			view.AddWidget(new WhiteSpace() { Width = 150 }, row, 8);
			view.AddWidget(new WhiteSpace() { Width = 150 }, row, 9);
			view.AddWidget(new WhiteSpace() { Width = 150 }, row, 10);
		}
	}
}
namespace SLC_SM_IAS_Profiles.Views
{
	using System.Linq;
	using DomHelpers.SlcConfigurations;
	using Library;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;
	using SLC_SM_IAS_Profiles.Presenters;

	public abstract class ConfigurationRow : Row
	{
		protected ConfigurationRow(ConfigurationRowData data) : base(data)
		{
			Reference = new DropDown<Models.ConfigurationParameter>() { IsEnabled = false };
			Label = new TextBox(Data.Record.ConfigurationParameterValue.Label) { MinWidth = 120 };
			ParamType = new EnumDropDown<SlcConfigurationsIds.Enums.Type> { Selected = Data.Record.ConfigurationParameterValue.Type, IsEnabled = false };
			Unit = new DropDown<Models.ConfigurationUnit>(data.CachedUnits) { IsEnabled = false, MaxWidth = 80 };
			Start = new Numeric { IsEnabled = false, MaxWidth = 100 };
			End = new Numeric { IsEnabled = false, MaxWidth = 100 };
			Step = new Numeric { IsEnabled = false, Minimum = 0, Maximum = 1, MaxWidth = 100 };
			Decimals = new Numeric { StepSize = 1, Minimum = 0, Maximum = 6, IsEnabled = false, MaxWidth = 80 };
			BtnSettings = new Button("...") { Width = 100, IsEnabled = false };
			Delete = new Button(Defaults.SymbolCross) { Width = 100, IsEnabled = data.CanDelete };

			BuildReference();
		}

		public new ConfigurationRowData Data => base.Data as ConfigurationRowData;

		public abstract InteractiveWidget Value { get; set; }

		public DropDown<Models.ConfigurationParameter> Reference { get; private set; }

		public TextBox Label { get; }

		public EnumDropDown<SlcConfigurationsIds.Enums.Type> ParamType { get; }

		public DropDown<Models.ConfigurationUnit> Unit { get; }

		public Numeric Start { get; }

		public Numeric End { get; }

		public Numeric Step { get; }

		public Numeric Decimals { get; }

		public Button BtnSettings { get; }

		public Button Delete { get; }

		public override Row Configure()
		{
			Delete.Pressed += (sender, args) => Data.Callbacks.ConfigurationParameter.Handle_Configuration_Delete_Pressed(Data.Page, Data.Record);
			Label.Changed += (sender, args) => Data.Callbacks.Common.Handle_Label_Changed(Data.Record, sender as TextBox, args.Value, args.Previous);

			if (string.IsNullOrEmpty(Label.Text))
			{
				Label.ValidationState = UIValidationState.Invalid;
				Label.ValidationText = "A name must be provided";
			}

			return this;
		}

		public override void BuildRow(Dialog view)
		{
			view.AddWidget(Label, Data.RowIndex, 0);
			view.AddWidget(Reference, Data.RowIndex, 1);
			view.AddWidget(ParamType, Data.RowIndex, 2);
			view.AddWidget(Value, Data.RowIndex, 3);
			view.AddWidget(Unit, Data.RowIndex, 4);
			view.AddWidget(Start, Data.RowIndex, 5);
			view.AddWidget(End, Data.RowIndex, 6);
			view.AddWidget(Step, Data.RowIndex, 7);
			view.AddWidget(Decimals, Data.RowIndex, 8);
			view.AddWidget(BtnSettings, Data.RowIndex, 9);
			view.AddWidget(Delete, Data.RowIndex, 10);
		}

		private void BuildReference()
		{
			Reference = new DropDown<Models.ConfigurationParameter>();
			Reference.IsEnabled = false;

			var record = Data.Record;
			var options = Data.ReferenceOptions
				.Select(cp => new Option<Models.ConfigurationParameter>(cp.Name, cp))
				.ToList();

			options.Insert(0, new Option<Models.ConfigurationParameter>(record.ReferredConfigurationParameter.Name, record.ReferredConfigurationParameter));

			Reference.Options = options;
			Reference.SelectedOption = options.FirstOrDefault(o => o?.Value?.ID == record.ReferredConfigurationParameter.ID);
		}
	}
}

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
			Name = new TextBox(Data.Record.ConfigurationParameter.Name) { MinWidth = 120, IsEnabled = Data.Record.RecordType != RecordType.Reference };
			ParamType = new EnumDropDown<SlcConfigurationsIds.Enums.Type> { Selected = Data.Record.ConfigurationParameter.Type, IsEnabled = Data.Record.RecordType != RecordType.Reference };
			Unit = new DropDown<Models.ConfigurationUnit>(data.CachedUnits) { IsEnabled = false, MaxWidth = 80 };
			Start = new Numeric { IsEnabled = false, MaxWidth = 100 };
			End = new Numeric { IsEnabled = false, MaxWidth = 100 };
			Step = new Numeric { IsEnabled = false, Minimum = 0, Maximum = 1, MaxWidth = 100 };
			Decimals = new Numeric { StepSize = 1, Minimum = 0, Maximum = 6, IsEnabled = false, MaxWidth = 80 };
			BtnSettings = new Button("...") { Width = 100, IsEnabled = false };
			Delete = new Button(Defaults.SymbolCross) { Width = 100 };

			BuildReference();
			BuildAllowMultipleCheck();
			BuildMandatoryCheck();
		}

		public abstract InteractiveWidget Value { get; set; }

		public DropDown<Models.ConfigurationParameter> Reference { get; private set; }

		public new ConfigurationRowData Data => base.Data as ConfigurationRowData;

		public TextBox Name { get; }

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
			Name.Changed += (sender, args) => Data.Callbacks.Common.Handle_Label_Changed(Data.Record, sender as TextBox, args.Value, args.Previous);

			ParamType.Changed += (sender, args) => Data.Callbacks.ConfigurationParameter.Handle_Type_Changed(Data.Record, args.Selected);

			if (string.IsNullOrEmpty(Name.Text))
			{
				Name.ValidationState = UIValidationState.Invalid;
				Name.ValidationText = "A name must be provided";
			}

			Reference.Changed += (sender, args) =>
				Data.Callbacks.ConfigurationParameter.Handle_ConfigurationParameterDropdown_Changed(
					Data.Page, Data.Record, Name, BtnSettings, args);

			MultipleAllowed.Changed += (sender, args) => Data.Callbacks.Common.Handle_AllowMultiple_Changed(Data.Page as ProfilePage, Data.Record, args);
			Mandatory.Changed += (sender, args) => Data.Callbacks.Common.Handle_Mandatory_Changed(Data.Page as ProfilePage, Data.Record, args);

			return this;
		}

		public override void BuildRow(Dialog view)
		{
			view.AddWidget(Name, Data.RowIndex, 0);

			if (Data.Page is ProfilePage)
			{
				view.AddWidget(Reference, Data.RowIndex, 1);
				view.AddWidget(MultipleAllowed, Data.RowIndex, 2);
				view.AddWidget(Mandatory, Data.RowIndex, 3);
			}

			view.AddWidget(ParamType, Data.RowIndex, 4);
			view.AddWidget(Unit, Data.RowIndex, 5);
			view.AddWidget(Start, Data.RowIndex, 6);
			view.AddWidget(End, Data.RowIndex, 7);
			view.AddWidget(Step, Data.RowIndex, 8);
			view.AddWidget(Decimals, Data.RowIndex, 9);
			view.AddWidget(Value, Data.RowIndex, 10);
			view.AddWidget(BtnSettings, Data.RowIndex, 11);
			view.AddWidget(Delete, Data.RowIndex, 12);
		}

		private void BuildReference()
		{
			if (Data.Record.RecordType == RecordType.Original)
				return;

			Reference.IsEnabled = Reference.IsVisible = true;

			var record = Data.Record;
			bool isReference = record.RecordType == RecordType.Reference;

			var options = Data.ReferenceOptions
				.Select(c => new Option<Models.ConfigurationParameter>(c.Name, c))
				.ToList();

			if (isReference)
			{
				options.Insert(0, new Option<Models.ConfigurationParameter>(record.ConfigurationParameter.Name,record.ConfigurationParameter));
			}

			options.Insert(0, new Option<Models.ConfigurationParameter>("- New -", null));

			Reference.Options = options;

			if (isReference)
			{
				Reference.SelectedOption =
					options.FirstOrDefault(o => o?.Value?.ID == record.ConfigurationParameter.ID);
			}
		}

		private void BuildMandatoryCheck()
		{
			bool mandatory = false;
			if (Data.Page is ProfilePage profilePage)
			{
				var reference =
					profilePage.ProfileDefinitionRecord.ProfileDefinition.ConfigurationParameters
					.Single(r => r.ConfigurationParameter == Data.Record.ConfigurationParameter.ID);

				mandatory = reference.Mandatory;
			}

			Mandatory = new CheckBox
			{
				IsEnabled = Data.Record.RecordType == RecordType.Reference,
				IsChecked = mandatory,
			};
		}

		private void BuildAllowMultipleCheck()
		{
			bool allowMultiple = false;
			if (Data.Page is ProfilePage profilePage)
			{
				var reference =
					profilePage.ProfileDefinitionRecord.ProfileDefinition.ConfigurationParameters
					.Single(r => r.ConfigurationParameter == Data.Record.ConfigurationParameter.ID);

				allowMultiple = reference.AllowMultiple;
			}

			MultipleAllowed = new CheckBox
			{
				IsEnabled = Data.Record.RecordType == RecordType.Reference,
				IsChecked = allowMultiple,
			};
		}
	}
}

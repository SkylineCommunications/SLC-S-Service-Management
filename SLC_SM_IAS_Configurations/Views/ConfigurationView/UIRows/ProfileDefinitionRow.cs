namespace SLC_SM_IAS_Profiles.Views
{
	using System.Linq;
	using Library;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;
	using SLC_SM_IAS_Profiles.Presenters;

	public class ProfileDefinitionRow : Row
	{
		internal ProfileDefinitionRow(ProfileDefinitionRowData data) : base(data)
		{
			BuildReferenceDropdown();

			BuildAllowMultipleCheck();

			BuildMandatoryCheck();

			BuildLabel();

			BuildButtonOpen();

			Delete = new Button(Defaults.SymbolCross) { Width = 100 };
		}

		public new ProfileDefinitionRowData Data => base.Data as ProfileDefinitionRowData;

		public TextBox Label { get; private set; }

		public DropDown<Models.ProfileDefinition> Reference { get; private set; }

		public Button BtnOpen { get; private set; }

		public Button Delete { get; }

		public override Row Configure()
		{
			ConfigureLabel();

			ConfigureReferenceDropdown();

			ConfigureButtons();

			MultipleAllowed.Changed += (sender, args) => Data.Callbacks.Common.Handle_AllowMultiple_Changed(Data.Page as ProfilePage, Data.Record, args);
			Mandatory.Changed += (sender, args) => Data.Callbacks.Common.Handle_Mandatory_Changed(Data.Page as ProfilePage, Data.Record, args);

			return this;
		}

		public override void BuildRow(Dialog view)
		{
			view.AddWidget(Label, Data.RowIndex, 0);

			if (Data.Page is ProfilePage)
			{
				view.AddWidget(Reference, Data.RowIndex, 1);
				view.AddWidget(MultipleAllowed, Data.RowIndex, 2);
				view.AddWidget(Mandatory, Data.RowIndex, 3);
			}

			view.AddWidget(BtnOpen, Data.RowIndex, 4);
			view.AddWidget(Delete, Data.RowIndex, 5);
			view.AddWidget(new Label { Width = 100 }, Data.RowIndex, 6);
			view.AddWidget(new Label { Width = 100 }, Data.RowIndex, 7);
			view.AddWidget(new Label { Width = 100 }, Data.RowIndex, 8);
			view.AddWidget(new Label { Width = 100 }, Data.RowIndex, 9);
			view.AddWidget(new Label { Width = 100 }, Data.RowIndex, 10);
			view.AddWidget(new Label { Width = 100 }, Data.RowIndex, 11);
			view.AddWidget(new Label { Width = 100 }, Data.RowIndex, 12);
		}

		private void BuildMandatoryCheck()
		{
			bool mandatory = false;
			if (Data.Page is ProfilePage profilePage)
			{
				var reference =
					profilePage.ProfileDefinitionRecord.ProfileDefinition.ProfileDefinitions
					.Single(r => r.ProfileDefinitionReference == Data.Record.ProfileDefinition.ID);

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
					profilePage.ProfileDefinitionRecord.ProfileDefinition.ProfileDefinitions
					.Single(r => r.ProfileDefinitionReference == Data.Record.ProfileDefinition.ID);

				allowMultiple = reference.AllowMultiple;
			}

			MultipleAllowed = new CheckBox
			{
				IsEnabled = Data.Record.RecordType == RecordType.Reference,
				IsChecked = allowMultiple,
			};
		}

		private void BuildButtonOpen()
		{
			BtnOpen = new Button("Open") { IsEnabled = false, Width = 100 };
			BtnOpen.IsEnabled = true;
		}

		private void BuildLabel()
		{
			Label = new TextBox(Data.Record.ProfileDefinition.Name) { MinWidth = 120 };
			Label.IsEnabled = Data.Record.RecordType != RecordType.Reference;
			if (string.IsNullOrWhiteSpace(Label.Text))
			{
				Label.ValidationState = UIValidationState.Invalid;
				Label.ValidationText = "A name must be provided";
			}
		}

		private void BuildReferenceDropdown()
		{
			Reference = new DropDown<Models.ProfileDefinition>() { IsEnabled = false };

			if (Data.Record.RecordType == RecordType.Original)
				return;

			Reference.IsEnabled = true;

			var record = Data.Record;
			bool isReference = record.RecordType == RecordType.Reference;

			var options = Data.ReferenceOptions
				.Select(p => new Option<Models.ProfileDefinition>(p.Name, p))
				.ToList();

			if (isReference)
			{
				options.Insert(0,new Option<Models.ProfileDefinition>(record.ProfileDefinition.Name, record.ProfileDefinition));
			}

			options.Insert(0, new Option<Models.ProfileDefinition>("- New -", null));

			Reference.Options = options;

			if (record.RecordType != RecordType.New)
			{
				Reference.SelectedOption = options.FirstOrDefault(o => o?.Value?.ID == record.ProfileDefinition.ID);
			}
		}

		private void ConfigureButtons()
		{
			BtnOpen.Pressed += (s, args) =>
				Data.Callbacks.ProfileDefinition.Handle_ProfileDefinition_Value_Pressed(Data.Record);

			Delete.Pressed += (s, args) =>
				Data.Callbacks.ProfileDefinition.Handle_Profile_Delete_Pressed(Data.Page, Data.Record);
		}

		private void ConfigureReferenceDropdown()
		{
			Reference.Changed += (s, args) =>
				Data.Callbacks.ProfileDefinition.Handle_ProfileDefinitionDropdown_Changed(
					Data.Page, Data.Record, Label, BtnOpen, args);
		}

		private void ConfigureLabel()
		{
			Label.Changed += (s, args) =>
				Data.Callbacks.Common.Handle_Label_Changed(Data.Record, Label, args.Value, args.Previous);
		}
	}
}

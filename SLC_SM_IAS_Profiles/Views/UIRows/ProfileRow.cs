namespace SLC_SM_IAS_Profiles.Views
{
	using System.Linq;
	using Library;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;

	public class ProfileRow : Row
	{
		internal ProfileRow(ProfileRowData data) : base(data)
		{
			BuildReferenceDropdown();

			BuildLabel();

			BuildButtonOpen();

			Delete = new Button(Defaults.SymbolCross) { Width = 100, IsEnabled = data.CanDelete };
		}

		public new ProfileRowData Data => base.Data as ProfileRowData;

		public TextBox Label { get; private set; }

		public DropDown<Models.ProfileDefinition> Reference { get; private set; }

		public Button BtnOpen { get; private set; }

		public Button Delete { get; }

		public override Row Configure()
		{
			ConfigureLabel();

			ConfigureButtons();

			return this;
		}

		public override void BuildRow(Dialog view)
		{
			view.AddWidget(Label, Data.RowIndex, 0);
			view.AddWidget(Reference, Data.RowIndex, 1);
			view.AddWidget(BtnOpen, Data.RowIndex, 2);
			view.AddWidget(Delete, Data.RowIndex, 3);
			view.AddWidget(new Label { Width = 100 }, Data.RowIndex, 4);
			view.AddWidget(new Label { Width = 100 }, Data.RowIndex, 5);
			view.AddWidget(new Label { Width = 100 }, Data.RowIndex, 6);
			view.AddWidget(new Label { Width = 100 }, Data.RowIndex, 7);
			view.AddWidget(new Label { Width = 100 }, Data.RowIndex, 8);
			view.AddWidget(new Label { Width = 100 }, Data.RowIndex, 9);
			view.AddWidget(new Label { Width = 100 }, Data.RowIndex, 10);
		}

		private void BuildButtonOpen()
		{
			BtnOpen = new Button("Open") { IsEnabled = false, Width = 100 };
			BtnOpen.IsEnabled = true;
		}

		private void BuildLabel()
		{
			Label = new TextBox(Data.Record.Profile.Name) { MinWidth = 120 };
			if (string.IsNullOrWhiteSpace(Label.Text))
			{
				Label.ValidationState = UIValidationState.Invalid;
				Label.ValidationText = "A name must be provided";
			}
		}

		private void BuildReferenceDropdown()
		{
			Reference = new DropDown<Models.ProfileDefinition>();
			Reference.IsEnabled = false;

			var record = Data.Record;
			var options = Data.ReferenceOptions
				.Select(p => new Option<Models.ProfileDefinition>(p.Name, p))
				.ToList();

			options.Insert(0, new Option<Models.ProfileDefinition>(record.ReferredProfileDefinition.Name, record.ReferredProfileDefinition));

			Reference.Options = options;
			Reference.SelectedOption = options.FirstOrDefault(o => o?.Value?.ID == record.ReferredProfileDefinition.ID);
		}

		private void ConfigureButtons()
		{
			BtnOpen.Pressed += (s, args) =>
				Data.Callbacks.Profile.Handle_Profile_Value_Pressed(Data.Record);

			Delete.Pressed += (s, args) =>
				Data.Callbacks.Profile.Handle_Profile_Delete_Pressed(Data.Page, Data.Record);
		}

		private void ConfigureLabel()
		{
			Label.Changed += (s, args) =>
				Data.Callbacks.Common.Handle_Label_Changed(Data.Record, Label, args.Value, args.Previous);
		}
	}
}

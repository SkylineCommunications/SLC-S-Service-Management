namespace SLC_SM_IAS_Profiles.Views
{
	using Library;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;

	public class ProfileDefinitionRow : Row
	{
		internal ProfileDefinitionRow(ProfileDefinitionRowData data) : base(data)
		{
			BuildLabel();

			BuildButtonOpen();

			Delete = new Button(Defaults.SymbolCross) { Width = 100 };
		}

		public new ProfileDefinitionRowData Data => base.Data as ProfileDefinitionRowData;

		public TextBox Label { get; private set; }

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

		private void BuildButtonOpen()
		{
			BtnOpen = new Button("Open") { Width = 100 };
		}

		private void BuildLabel()
		{
			Label = new TextBox(Data.Record.ProfileDefinition.Name) { MinWidth = 120 };
			if (string.IsNullOrWhiteSpace(Label.Text))
			{
				Label.ValidationState = UIValidationState.Invalid;
				Label.ValidationText = "A name must be provided";
			}
		}

		private void ConfigureButtons()
		{
			BtnOpen.Pressed += (s, args) =>
				Data.Callbacks.ProfileDefinition.Handle_ProfileDefinition_Value_Pressed(Data.Record);

			Delete.Pressed += (s, args) =>
				Data.Callbacks.ProfileDefinition.Handle_Profile_Delete_Pressed(Data.Page, Data.Record);
		}

		private void ConfigureLabel()
		{
			Label.Changed += (s, args) =>
				Data.Callbacks.Common.Handle_Label_Changed(Data.Record, Label, args.Value, args.Previous);
		}
	}
}

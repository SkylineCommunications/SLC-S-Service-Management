namespace SLC_SM_IAS_Add_Service_Order_1.Views
{
	using System;
	using DomHelpers.SlcPeople_Organizations;
	using DomHelpers.SlcServicemanagement;
	using Library;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;

	public class ServiceOrderView : Dialog
	{
		public ServiceOrderView(IEngine engine) : base(engine)
		{
			Title = "Manage Service Order";

			int row = 0;
			AddWidget(LblOrderID, row, 0);
			AddWidget(OrderId, row, 1);
			AddWidget(LblName, ++row, 0);
			AddWidget(TboxName, row, 1, 1, 2);
			AddWidget(ErrorName, row, 3);

			AddWidget(LblDescription, ++row, 0);
			AddWidget(Description, row, 1, 1, 2);

			AddWidget(LblExternalId, ++row, 0);
			AddWidget(ExternalId, row, 1, 1, 2);

			AddWidget(LblPriority, ++row, 0);
			AddWidget(Priority, row, 1, 1, 2);

			AddWidget(LblOrg, ++row, 0);
			AddWidget(Org, row, 1, 1, 2);

			AddWidget(LblContact, ++row, 0);
			AddWidget(Contact, row, 1, 1, 2);

			AddWidget(BtnCompletedBy, ++row, 0);
			AddWidget(LblCompletionInfoState, ++row, 0);
			AddWidget(CompletionInfoState, row, 1);
			AddWidget(LblCompletedByStart, ++row, 0);
			AddWidget(CompletedByStart, row, 1);
			AddWidget(LblFullyCompletedBy, ++row, 0);
			AddWidget(FullyCompletedBy, row, 1);
			BtnCompletedBy.LinkedWidgets.Add(LblCompletionInfoState);
			BtnCompletedBy.LinkedWidgets.Add(CompletionInfoState);
			BtnCompletedBy.LinkedWidgets.Add(LblCompletedByStart);
			BtnCompletedBy.LinkedWidgets.Add(CompletedByStart);
			BtnCompletedBy.LinkedWidgets.Add(LblFullyCompletedBy);
			BtnCompletedBy.LinkedWidgets.Add(FullyCompletedBy);

			AddWidget(new WhiteSpace(), ++row, 0);
			AddWidget(BtnAdd, ++row, 1);
			AddWidget(BtnCancel, row, 2);
		}

		public Label LblOrderID { get; } = new Label("Order ID");

		public Label OrderId { get; } = new Label();

		public Label LblName { get; } = new Label("Label");

		public TextBox TboxName { get; } = new TextBox { Width = Defaults.WidgetWidth };

		public Label ErrorName { get; } = new Label(String.Empty);

		public Label LblExternalId { get; } = new Label("External ID");

		public TextBox ExternalId { get; } = new TextBox { Width = Defaults.WidgetWidth };

		public Label LblPriority { get; } = new Label("Priority");

		public DropDown<SlcServicemanagementIds.Enums.ServiceorderpriorityEnum> Priority { get; } = new DropDown<SlcServicemanagementIds.Enums.ServiceorderpriorityEnum> { Width = Defaults.WidgetWidth };

		public Label LblDescription { get; } = new Label("Description");

		public TextBox Description { get; } = new TextBox { Width = Defaults.WidgetWidth, IsMultiline = true };

		public Label LblOrg { get; } = new Label("Organization");

		public DropDown<OrganizationsInstance> Org { get; } = new DropDown<OrganizationsInstance> { Width = Defaults.WidgetWidth };

		public Label LblContact { get; } = new Label("Order Contact");

		public CheckBoxList<PeopleInstance> Contact { get; } = new CheckBoxList<PeopleInstance> { Width = Defaults.WidgetWidth, MaxHeight = Defaults.WidgetWidth };

		public CollapseButton BtnCompletedBy { get; } = new CollapseButton(true) { CollapseText = "Hide Completion Details", ExpandText = "Show Completion Details", Tooltip = "Show all 'To be completed by' settings." };

		public Label LblCompletionInfoState { get; } = new Label("Completion Settings Enabled");

		public CheckBox CompletionInfoState { get; } = new CheckBox { IsChecked = false, Tooltip = "Indicate whether the completion info details will be configured or not." };

		public Label LblCompletedByStart { get; } = new Label("Requested Start Date");

		public DateTimePicker CompletedByStart { get; } = new DateTimePicker { Width = Defaults.WidgetWidth };

		public Label LblFullyCompletedBy { get; } = new Label("Requested Completed Date");

		public DateTimePicker FullyCompletedBy { get; } = new DateTimePicker { Width = Defaults.WidgetWidth };

		public Button BtnAdd { get; } = new Button("Create") { Style = ButtonStyle.CallToAction };

		public Button BtnCancel { get; } = new Button("Cancel");
	}
}
namespace SLC_SM_Create_Service_Inventory_Item.Views
{
	using System;

	using Library;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;

	using SLC_SM_Create_Service_Inventory_Item;

	public class ServiceView : Dialog
	{
		public ServiceView(IEngine engine, Defaults.ScriptAction_CreateServiceInventoryItem action) : base(engine)
		{
			Title = "Manage Service";

			int row = 0;
			AddWidget(LblServiceId, row, 0);
			AddWidget(ServiceId, row, 1);
			AddWidget(LblName, ++row, 0);
			AddWidget(TboxName, row, 1, 1, 2);
			AddWidget(ErrorName, row, 3);

			AddWidget(LblServiceCategory, ++row, 0);
			AddWidget(ServiceCategory, row, 1, 1, 2);

			AddWidget(LblSpecification, ++row, 0);
			AddWidget(Specs, row, 1, 1, 2);

			if (action == Defaults.ScriptAction_CreateServiceInventoryItem.Edit)
			{
				AddWidget(LblServiceConfigurationVersion, ++row, 0);
				AddWidget(ConfigurationVersions, row, 1, 1, 2);
				AddWidget(ErrorConfigurationVersion, row, 3);
			}

			AddWidget(LblOrganization, ++row, 0);
			AddWidget(Organizations, row, 1, 1, 2);

			AddWidget(LblStart, ++row, 0);
			AddWidget(Start, row, 1, 1, 2);
			AddWidget(ErrorStart, row, 3);
			AddWidget(LblEnd, ++row, 0);
			AddWidget(End, row, 1, 1, 2);
			AddWidget(IndefiniteRuntime, row, 3);

			AddWidget(GenerateMonitoringService, ++row, 1);

			AddWidget(new WhiteSpace(), ++row, 0);
			AddWidget(BtnAdd, ++row, 1);
			AddWidget(BtnCancel, row, 2);
		}

		public Label LblServiceId { get; } = new Label("Service ID");

		public Label ServiceId { get; } = new Label();

		public Label LblName { get; } = new Label("Name");

		public TextBox TboxName { get; } = new TextBox { Width = Defaults.WidgetWidth };

		public Label ErrorName { get; } = new Label(String.Empty);

		public Button BtnCancel { get; } = new Button("Cancel");

		public Label LblServiceCategory { get; } = new Label("Category");

		public DropDown<Models.ServiceCategory> ServiceCategory { get; } = new DropDown<Models.ServiceCategory> { Width = Defaults.WidgetWidth };

		public Label LblSpecification { get; } = new Label("Service Specification");

		public DropDown<Models.ServiceSpecification> Specs { get; } = new DropDown<Models.ServiceSpecification> { Width = Defaults.WidgetWidth };

		public Label LblOrganization { get; } = new Label("Organization");

		public DropDown<Skyline.DataMiner.ProjectApi.ServiceManagement.API.PeopleAndOrganization.Models.Organization> Organizations { get; } = new DropDown<Skyline.DataMiner.ProjectApi.ServiceManagement.API.PeopleAndOrganization.Models.Organization> { Width = Defaults.WidgetWidth };

		public Label LblStart { get; } = new Label("Start Time");

		public DateTimePicker Start { get; } = new DateTimePicker
		{
			Height = 25,
			Width = Defaults.WidgetWidth,
			IsTimePickerVisible = true,
			Kind = DateTimeKind.Local,
			HasSpinnerButton = true,
			AutoCloseCalendar = true,
		};

		public Label ErrorStart { get; } = new Label(String.Empty);

		public Label LblEnd { get; } = new Label("End Time");

		public DateTimePicker End { get; } = new DateTimePicker
		{
			Height = 25,
			Width = Defaults.WidgetWidth,
			IsTimePickerVisible = true,
			Kind = DateTimeKind.Local,
			HasSpinnerButton = true,
			AutoCloseCalendar = true,
		};

		public CheckBox IndefiniteRuntime { get; } = new CheckBox("Indefinite (no end time)") { IsChecked = false };

		public CheckBox GenerateMonitoringService { get; set; } = new CheckBox("Generate DataMiner Monitoring Service") { IsChecked = false, IsEnabled = false };

		public Button BtnAdd { get; } = new Button("Create") { Style = ButtonStyle.CallToAction };

		public Label LblServiceConfigurationVersion { get; } = new Label("Configuration Version");

		public DropDown<Models.ServiceConfigurationVersion> ConfigurationVersions { get; } = new DropDown<Models.ServiceConfigurationVersion> { Width = Defaults.WidgetWidth };

		public Label ErrorConfigurationVersion { get; } = new Label(String.Empty);
	}
}
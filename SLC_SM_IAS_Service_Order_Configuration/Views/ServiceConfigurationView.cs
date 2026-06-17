namespace SLC_SM_IAS_Service_Order_Configuration.Views
{
	using Library;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;

	public class ServiceConfigurationView : Dialog
	{
		public ServiceConfigurationView(IEngine engine) : base(engine)
		{
			Title = "Manage Service Order Item Configuration";
			MinWidth = Defaults.DialogMinWidth;
		}

		public Label TitleDetails { get; } = new Label("Service Configuration Details") { Style = TextStyle.Bold };

		public Button BtnUpdate { get; } = new Button("Update") { Style = ButtonStyle.CallToAction };

		public Button BtnCancel { get; } = new Button("Cancel");

		public CollapseButton BtnShowValueDetails { get; } = new CollapseButton { IsCollapsed = false, ExpandText = "Show Value Details", CollapseText = "Hide Value Details" };

		public CollapseButton BtnShowLifeCycleDetails { get; } = new CollapseButton { IsCollapsed = false, ExpandText = "Show Lifecycle Details", CollapseText = "Hide Lifecycle Details" };

		public DropDown<Models.ConfigurationParameter> AddParameter { get; } = new DropDown<Models.ConfigurationParameter> { IsDisplayFilterShown = true };

		public Button BtnAddParameter { get; } = new Button("Add") { MaxWidth = 70 };
	}
}
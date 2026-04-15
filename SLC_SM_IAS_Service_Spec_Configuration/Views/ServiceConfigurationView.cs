namespace SLC_SM_IAS_Service_Spec_Configuration.Views
{
	using System.Collections.Generic;

	using Library;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;

	using SLC_SM_IAS_Service_Spec_Configuration.Model;

	public class ServiceConfigurationView : Dialog
	{
		private const string _standaloneParameterCollapseButtonTitle = "Standalone Parameters";

		public ServiceConfigurationView(IEngine engine) : base(engine)
		{
			Title = "Manage Service Configuration";
			MinWidth = Defaults.DialogMinWidth;
		}

		public static string StandaloneCollapseButtonTitle { get => _standaloneParameterCollapseButtonTitle; }

		public Label TitleDetails { get; } = new Label("Service Configuration Details") { Style = TextStyle.Bold };

		public Button BtnUpdate { get; } = new Button("Update") { Style = ButtonStyle.CallToAction };

		public Button BtnCancel { get; } = new Button("Cancel");

		public Button BtnShowValueDetails { get; } = new Button("Show Value Details");

		public Button BtnShowLifeCycleDetails { get; } = new Button("Show Lifecycle Details");

		public Dictionary<string, Section> Details { get; } = new Dictionary<string, Section>();

		public Dictionary<string, Section> LifeCycleDetails { get; } = new Dictionary<string, Section>();

		public DropDown<Models.ConfigurationParameter> AddParameter { get; } = new DropDown<Models.ConfigurationParameter> { IsDisplayFilterShown = true};

		public DropDown<ProfileOption> AddProfile { get; } = new DropDown<ProfileOption> { IsDisplayFilterShown = true};

		public CollapseButton StandaloneParameters { get; } = new CollapseButton(true) { ExpandText = "+", CollapseText = "-", Tooltip = _standaloneParameterCollapseButtonTitle };

		public Dictionary<string, CollapseButton> ProfileCollapseButtons { get; } = new Dictionary<string, CollapseButton>();
	}
}
namespace SLC_SM_IAS_Service_Configuration.Views
{
	using System.Collections.Generic;
	using Library;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;

	public class ServiceConfigurationView : Dialog
	{
		private const string _standaloneParameterCollapseButtonTitle = "Standalone Parameters";
		private const string _generalSettingsCollapseButtonTitle = "General Settings";

		public ServiceConfigurationView(IEngine engine) : base(engine)
		{
			Title = "Manage Service Configuration";
			MinWidth = Defaults.DialogMinWidth;
		}

		public static string StandaloneCollapseButtonTitle => _standaloneParameterCollapseButtonTitle;

		public static string GeneralSettingsCollapseButtonTitle => _generalSettingsCollapseButtonTitle;

		public Label TitleDetails { get; } = new Label("Service Configuration Details") { Style = TextStyle.Bold };

		public Button BtnUpdate { get; } = new Button("Update") { Style = ButtonStyle.CallToAction };

		public Button BtnCancel { get; } = new Button("Cancel");

		public Button BtnShowValueDetails { get; } = new Button("Show Value Details");

		public Button BtnCopyConfiguration { get; } = new Button("Copy") { IsVisible = true, MaxWidth = 100 };

		public CollapseButton StandaloneParameters { get; } = new CollapseButton(true) { ExpandText = "+", CollapseText = "-", Tooltip = _standaloneParameterCollapseButtonTitle };

		public CollapseButton GeneralSettings { get; } = new CollapseButton(true) { ExpandText = "+", CollapseText = "-", Tooltip = _generalSettingsCollapseButtonTitle };

		public Dictionary<string, CollapseButton> ProfileCollapseButtons { get; } = new Dictionary<string, CollapseButton>();

		public Dictionary<string, Section> Details { get; } = new Dictionary<string, Section>();

		public DropDown<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameter> StandaloneParametersToAdd { get; set; }
			= new DropDown<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameter>();

		public DropDown<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ProfileDefinition> ProfileDefinitionToAdd { get; set; }
			= new DropDown<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ProfileDefinition>
			{
				IsDisplayFilterShown = true,
			};

		public DropDown<Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement.Models.ServiceConfigurationVersion> ConfigurationVersions { get; } =
			new DropDown<Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement.Models.ServiceConfigurationVersion>();

		public CheckBox ConfirmExceedNumberOfVersions { get; } = new CheckBox { MaxWidth = 25 };

		public Label ConfirmExceedNumberOfVersionsLabel { get; } = new Label();
	}
}
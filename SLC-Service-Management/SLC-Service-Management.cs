using System;
using Skyline.DataMiner.Automation;
using Skyline.DataMiner.Net.AppPackages;
using SLC_ServiceManagement.SolutionInstallerHelper;

/// <summary>
///     DataMiner Script Class.
/// </summary>
internal partial class Script
{
	/// <summary>
	///     The script entry point.
	/// </summary>
	/// <param name="engine">Provides access to the Automation engine.</param>
	/// <param name="context">Provides access to the installation context.</param>
	[AutomationEntryPoint(AutomationEntryPointType.Types.InstallAppPackage)]
	public static void Install(IEngine engine, AppInstallContext context)
	{
		try
		{
			var solutionInstaller = new SMSolutionInstallerHelper(engine, context, "sm-");

			engine.Timeout = new TimeSpan(0, 10, 0);
			engine.GenerateInformation("Starting installation");

			//solutionInstaller.InstallDefaultContent();

			//solutionInstaller.DeployMediaOpsDependencies();
			//solutionInstaller.ImportSolutionDom(); // TODO: create script to create dom module instead of importing it
			//solutionInstaller.DeployDataMinerApp();
			solutionInstaller.DeployAssistantContext();
			solutionInstaller.RegisterAssistantTools();
			//solutionInstaller.RegisterSolution();
		}
		catch (Exception e)
		{
			engine.ExitFail($"Exception encountered during installation: {e}");
		}
	}
}
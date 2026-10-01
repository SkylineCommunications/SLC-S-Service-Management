using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

using Skyline.AppInstaller;
using Skyline.DataMiner.Automation;
using Skyline.DataMiner.Net.AppPackages;
using Skyline.DataMiner.Utils.AppInstallerHelper;

/// <summary>
///     DataMiner Script Class.
/// </summary>
internal class Script
{
	private static string _setupContentPath;

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
			engine.Timeout = new TimeSpan(0, 10, 0);
			engine.GenerateInformation("Starting installation");
			//var installer = new AppInstaller(Engine.SLNetRaw, context);
			//installer.InstallDefaultContent();

			//installerHelper.GetDomInstaller().InstallDomModules();
			// string setupContentPath = installer.GetSetupContentDirectory();
			//var installer = new AppInstaller(Engine.SLNetRaw, context);
			//_setupContentPath = installer.GetSetupContentDirectory();

			//// Custom installation logic can be added here for each individual install package.
			//var exceptions = new List<Exception>();
			//installer.Log("Importing DOM...");
			//exceptions.AddRange(ImportDom(engine));

			//if (exceptions.Any())
			//{
			//	throw new AggregateException(exceptions);
			//}

			var installerHelper = new AppInstallerHelper(Engine.SLNetRaw, context);
			installerHelper.InstallDefaultContent();

			installerHelper.WebApplications.RegisterStaticApp(
				id: "service-management",
				name: "Service Management",
				url: "/public/service-management",
				icon: "ms-Icon--BuildDefinition",
				color: Color.FromArgb(220, 38, 38));

		}
		catch (Exception e)
		{
			engine.ExitFail($"Exception encountered during installation: {e}");
		}
	}

	private static List<Exception> ImportDom(IEngine engine)
	{
		var exceptions = new List<Exception>();

		try
		{
			// Will import all dom modules that are found in this folder
			// ImportDom(engine, @"c:\Skyline DataMiner\DOM\EventManager");
			string path = _setupContentPath + @"\DOMImportExport";
			engine.GenerateInformation($"setupContentPath for DOM: {path}");

			ImportDom(engine, path);
		}
		catch (Exception e)
		{
			exceptions.Add(e);
		}

		return exceptions;
	}

	private static void ImportDom(IEngine engine, string path)
	{
		var subScript = engine.PrepareSubScript("DOM ImportExport");
		subScript.SelectScriptParam("Action", "Import");
		subScript.SelectScriptParam("Path", path);
		subScript.SelectScriptParam("ModuleNames", "-1");
		subScript.Synchronous = true;
		subScript.StartScript();
	}
}
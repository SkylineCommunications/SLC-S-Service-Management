/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE        VERSION        AUTHOR            COMMENTS

28/09/2026  1.0.0.1        SKA, Skyline      Initial version
****************************************************************************
*/

using System;

using Skyline.DataMiner.Automation;
using Skyline.DataMiner.Net.AppPackages;

using SLC_Service_Management_RT.RegressionTests;

/// <summary>
/// Installs the Service Management regression tests and registers them on the QAPortal element.
/// The tests are not started automatically; run them from the QAPortal element.
/// </summary>
internal class Script
{
	/// <summary>
	/// The script entry point.
	/// </summary>
	/// <param name="engine">Provides access to the Automation engine.</param>
	/// <param name="context">Provides access to the installation context.</param>
	[AutomationEntryPoint(AutomationEntryPointType.Types.InstallAppPackage)]
	public void Install(IEngine engine, AppInstallContext context)
	{
		try
		{
			engine.Timeout = new TimeSpan(0, 30, 0);
			engine.SetFlag(RunTimeFlags.NoKeyCaching);
			engine.GenerateInformation("Starting installation of the Service Management regression tests");

			var regressionTestInstaller = new RegressionTestInstaller(engine, context);
			if (regressionTestInstaller.AreTestsRunning())
			{
				throw new InvalidOperationException("Cannot install the regression tests while tests are running on the QAPortal element.");
			}

			regressionTestInstaller.InstallDefaultContent();
			regressionTestInstaller.AddOrUpdateQaPortalElement();
		}
		catch (Exception e)
		{
			engine.ExitFail($"Exception encountered during installation: {e}");
		}
	}
}

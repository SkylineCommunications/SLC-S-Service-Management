/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE		VERSION		AUTHOR			COMMENTS

30/05/2025	1.0.0.1		RCA, Skyline	Initial version
****************************************************************************
*/

namespace SLCSMIASManageRelationships
{
	using System;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.IAS;
	using SLC_SM_IAS_ManageRelationships.Controller;

	/// <summary>
	/// Represents a DataMiner Automation script.
	/// </summary>
	public class Script
	{
		/// <summary>
		/// The script entry point.
		/// </summary>
		/// <param name="engine">Link with SLAutomation process.</param>
		public void Run(IEngine engine)
		{
			// DO NOT REMOVE THIS COMMENTED-OUT CODE OR THE SCRIPT WON'T RUN!
			// DataMiner evaluates if the script needs to launch in interactive mode.
			// This is determined by a simple string search looking for "engine.ShowUI" in the source code.
			// However, because of the toolkit NuGet package, this string cannot be found here.
			// So this comment is here as a workaround.
			//// engine.ShowUI();
			try
			{
				RunSafe(engine);
			}
			catch (ScriptAbortException)
			{
				// Catch normal abort exceptions (engine.ExitFail or engine.ExitSuccess)
				// throw; // Comment if it should be treated as a normal exit of the script.
			}
			catch (ScriptForceAbortException)
			{
				// Catch forced abort exceptions, caused via external maintenance messages.
				// throw;
			}
			catch (InteractiveUserDetachedException)
			{
				// Catch a user detaching from the interactive script by closing the window.
				// Only applicable for interactive scripts, can be removed for non-interactive scripts.
				// throw;
			}
			catch (Exception e)
			{
				engine.ShowPopupDialog("Attention!", e.Message, "OK");
			}
		}

		private void RunSafe(IEngine engine)
		{
			var data = new ScriptData(engine);
			////engine.GenerateInformation(data.ToString());
			data.Validate();

			var controller = new ManageConnectionsController(engine, data);
			if (data.HasDefinitionReference)
				controller.CreateServiceItem();

			controller.BuildLinkMap();
			controller.HandleNext();
		}
	}
}

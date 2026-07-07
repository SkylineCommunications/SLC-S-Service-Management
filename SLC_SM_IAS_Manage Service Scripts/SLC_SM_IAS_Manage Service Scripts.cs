/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE		VERSION		AUTHOR			COMMENTS

07/07/2026	1.0.0.1		SKA, Skyline	Initial version
****************************************************************************
*/

namespace SLCSMIASManageServiceScripts
{
	using System;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.IAS;

	using SLC_SM_IAS_Manage_Service_Scripts.Model;
	using SLC_SM_IAS_Manage_Service_Scripts.Presenters;

	public class Script
	{
		public static void Run(IEngine engine)
		{
			/*
             * Note:
             * Do not remove the commented methods below!
             * The lines are needed to execute an interactive automation script from the non-interactive automation script or from Visio!
             *
             * engine.ShowUI();
             */

			try
			{
				RunSafe(engine);
			}
			catch (ScriptAbortException)
			{
				// Catch normal abort exceptions (engine.ExitFail or engine.ExitSuccess)
				throw; // Comment if it should be treated as a normal exit of the script.
			}
			catch (ScriptForceAbortException)
			{
				// Catch forced abort exceptions, caused via external maintenance messages.
				throw;
			}
			catch (ScriptTimeoutException)
			{
				// Catch timeout exceptions for when a script has been running for too long.
				throw;
			}
			catch (InteractiveUserDetachedException)
			{
				// Catch a user detaching from the interactive script by closing the window.
				// Only applicable for interactive scripts, can be removed for non-interactive scripts.
				throw;
			}
			catch (Exception e)
			{
				engine.ShowErrorDialog(e);
			}
		}

		private static void RunSafe(IEngine engine)
		{
			var data = new ScriptData(engine);
			data.ValidateInputParameters();

			var presenter = new UiPresenter(engine, data);
			presenter.Handle();
		}
	}
}

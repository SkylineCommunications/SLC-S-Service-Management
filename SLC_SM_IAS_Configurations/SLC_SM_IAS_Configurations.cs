/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE		VERSION		AUTHOR			COMMENTS

28/05/2025	1.0.0.1		RME, Skyline	Initial version
****************************************************************************
*/

namespace SLC_SM_IAS_Profiles
{
	using System;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.IAS;
	using SLC_SM_IAS_Profiles.Data;
	using SLC_SM_IAS_Profiles.Presenters;

	/// <summary>
	/// Represents a DataMiner Automation script.
	/// </summary>
	public class Script
	{
		private IEngine _engine;

		/// <summary>
		/// The script entry point.
		/// </summary>
		/// <param name="engine">Link with SLAutomation process.</param>
		public void Run(IEngine engine)
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
				_engine = engine;
				RunSafe();
			}
			catch (ScriptAbortException)
			{
				// Catch normal abort exceptions (engine.ExitFail or engine.ExitSuccess)
			}
			catch (ScriptForceAbortException)
			{
				// Catch forced abort exceptions, caused via external maintenance messages.
			}
			catch (ScriptTimeoutException)
			{
				// Catch timeout exceptions for when a script has been running for too long.
			}
			catch (InteractiveUserDetachedException)
			{
				// Catch a user detaching from the interactive script by closing the window.
				// Only applicable for interactive scripts, can be removed for non-interactive scripts.
			}
			catch (Exception e)
			{
				engine.Log(e.ToString());
				engine.ShowErrorDialog(e);
			}
		}

		private void RunSafe()
		{
			_engine.SetFlag(RunTimeFlags.NoCheckingSets);
			_engine.SetFlag(RunTimeFlags.NoKeyCaching);
			_engine.Timeout = TimeSpan.FromHours(1);

			// Model-View-Presenter
			var scriptData = new ScriptData(_engine);
			var presenter = new ConfigurationPresenter(_engine, scriptData);
			presenter.LoadFromModel();
			presenter.ShowDialog();
		}
	}
}
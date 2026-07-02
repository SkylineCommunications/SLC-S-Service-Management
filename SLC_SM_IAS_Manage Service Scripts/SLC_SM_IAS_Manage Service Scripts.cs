namespace SLCSMIASManageServiceScripts
{
	using System;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.IAS;
	using SLC_SM_IAS_Manage_Service_Scripts.Model;
	using SLC_SM_IAS_Manage_Service_Scripts.Presenters;
	using SLC_SM_IAS_Manage_Service_Scripts.Views;

	public class Script
	{
		public void Run(IEngine engine)
		{
			/*
             * Note:
             * Do not remove the commented methods below!
             * The lines are needed to execute an interactive automation script from the non-interactive automation script or from Visio!
             *
             * engine.ShowUI();
             */
			if (engine.IsInteractive)
			{
				engine.FindInteractiveClient("Failed to run script in interactive mode", 1);
			}

			try
			{
				RunSafe(engine);
			}
			catch (ScriptAbortException)
			{

			}
			catch (ScriptForceAbortException)
			{

			}
			catch (ScriptTimeoutException)
			{

			}
			catch (InteractiveUserDetachedException)
			{

			}
			catch (Exception e)
			{
				engine.ShowErrorDialog(e);
			}
		}

		private void RunSafe(IEngine engine)
		{
			var data = new ScriptData(engine);
			data.Validate();

			var view = new ManageServiceScriptsView(engine);
			var presenter = new UiPresenter(engine, view, data);
			presenter.Handle();
		}
	}
}

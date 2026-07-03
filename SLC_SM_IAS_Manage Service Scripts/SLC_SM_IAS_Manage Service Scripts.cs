namespace SLCSMIASManageServiceScripts
{
	using System;
	using System.Reflection;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement;
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
			LogAssemblyVersion(engine, typeof(Models.Service).Assembly, "ServiceManagement");

			var data = new ScriptData(engine);
			data.Validate();

			var view = new ManageServiceScriptsView(engine);
			var presenter = new UiPresenter(engine, view, data);
			presenter.Handle();
		}

		private static void LogAssemblyVersion(IEngine engine, Assembly assembly, string label)
		{
			var assemblyName = assembly.GetName();
			var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "n/a";
			var fileVersion = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version ?? "n/a";
			engine.GenerateInformation(
				$"{label} assembly loaded: {assemblyName.Name}, version={assemblyName.Version}, informationalVersion={informationalVersion}, fileVersion={fileVersion}, location={assembly.Location}");
		}
	}
}

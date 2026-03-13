namespace ServiceStateTransitions
{
	using System;
	using System.Linq;
	using Library.Dom;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.IAS;
	using static DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Service_Behavior;

	/// <summary>
	///     Represents a DataMiner Automation script.
	/// </summary>
	public class Script
	{
		private IEngine engine;

		/// <summary>
		///     The script entry point.
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
			if (engine.IsInteractive)
			{
				engine.FindInteractiveClient("Failed to run script in interactive mode", 1);
			}

			try
			{
				this.engine = engine;
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
				engine.ShowErrorDialog(e);
			}
		}

		private void RunSafe()
		{
			var serviceReference = engine.ReadScriptParamFromApp<Guid>("ServiceReference");
			var previousState = engine.ReadScriptParamFromApp("PreviousState").ToLower();
			var nextState = engine.ReadScriptParamFromApp("NextState").ToLower();

			TransitionsEnum transition = Enum.GetValues(typeof(TransitionsEnum))
											 .Cast<TransitionsEnum?>()
											 .FirstOrDefault(t => t.ToString().Equals($"{previousState}_to_{nextState}", StringComparison.OrdinalIgnoreCase))
										 ?? throw new NotSupportedException($"The provided previousState '{previousState}' is not supported for nextState '{nextState}'");

			var srvHelper = new DataHelperService(engine.GetUserConnection());
			var service = srvHelper.Read(ServiceExposers.Guid.Equal(serviceReference)).FirstOrDefault()
						  ?? throw new NotSupportedException($"No Service with ID '{serviceReference}' exists on the system");

			switch (transition)
			{
				case TransitionsEnum.Reserved_To_Active:
				case TransitionsEnum.Terminated_To_Active:
					service.UpdateStatusToActive(engine.GetUserConnection());
					break;

				case TransitionsEnum.Active_To_Terminated:
					service.UpdateStatusToTerminated(engine);
					break;

				default:
					engine.GenerateInformation($"[SMS] Status Transition: {service.Name} → {transition}");
					srvHelper.UpdateState(service, transition);
					break;
			}
		}
	}
}
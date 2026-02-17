namespace SLCSMASServiceItemContextMenuActions
{
	using System;
	using System.Linq;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.IAS;

	/// <summary>
	/// Represents a DataMiner Automation script.
	/// </summary>
	public class Script
	{
		IEngine _engine;

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
				engine.ShowErrorDialog(e);
				engine.Log(e.ToString());
			}
		}

		private void RunSafe()
		{
			Guid domId = _engine.ReadScriptParamFromApp<Guid>("DOM ID");
			if (domId == Guid.Empty)
			{
				throw new InvalidOperationException("No DOM ID provided as input to the script");
			}

			string label = _engine.ReadScriptParamFromApp("Service Item Label");
			string contextMenuAction = _engine.ReadScriptParamFromApp("ContextMenu Action");

			var service = new DataHelperService(_engine.GetUserConnection()).Read(ServiceExposers.Guid.Equal(domId)).FirstOrDefault()
				?? throw new NotSupportedException($"No Service item with ID '{domId}' exists on the system!");

			var serviceItem = service.ServiceItems?.FirstOrDefault(s => s.Label == label)
							  ?? throw new NotSupportedException($"No Service item with label '{label}' exists on the service with ID '{domId}'!");

			if (serviceItem.Type == DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Enums.ServiceitemtypesEnum.SRMBooking)
			{
				RunSrmBookingManagerActions(serviceItem, contextMenuAction);
			}
			else
			{
				throw new NotSupportedException($"Service item with label '{label}' is of type '{serviceItem.Type}', which is currently not supported by this context menu action");
			}
		}

		private void RunSrmBookingManagerActions(Models.ServiceItem serviceItem, string contextMenuAction)
		{
			string bookingManager = serviceItem.DefinitionReference;
			string reservationId = serviceItem.ImplementationReference;

			if (String.IsNullOrEmpty(bookingManager) || String.IsNullOrEmpty(reservationId) || reservationId == Guid.Empty.ToString())
			{
				throw new InvalidOperationException($"Service item with label '{serviceItem.Label}' does not have a valid booking manager or reservation ID configured");
			}

			if (!_engine.ShowConfirmDialog($"Are you sure you wish to {contextMenuAction} the booking?"))
			{
				return;
			}

			var script = _engine.PrepareSubScript("SRM_ReservationAction");
			script.SelectScriptParam("Booking Manager Info", $"{{\"Element\":\"{bookingManager}\",\"TableIndex\":\"{reservationId}\"}}");
			script.SelectScriptParam("Action", contextMenuAction);
			script.SelectScriptParam("Is Silent", "false");
			script.Synchronous = true;
			script.StartScript();

			if (script.HadError)
			{
				throw new InvalidOperationException($"An error occurred while executing the action:{Environment.NewLine}{String.Join(Environment.NewLine, script.GetErrorMessages())}");
			}
		}
	}
}

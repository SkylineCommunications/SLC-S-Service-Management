namespace ServiceStateTransitions
{
	using System;
	using System.Linq;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.IAS;
	using static DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Service_Behavior;

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

		private void RunSafe(IEngine engine)
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

			engine.GenerateInformation($"Service Status Transition starting: previousState: {previousState}, nextState: {nextState}");
			srvHelper.UpdateState(service, transition);

			if (transition == TransitionsEnum.Reserved_To_Active)
			{
				// Transition order item to Complete
				var itemHelper = new DataHelperServiceOrderItem(engine.GetUserConnection());
				var orderItem = itemHelper.Read().Find(o => o.ServiceId == service.ID);
				if (orderItem != null && orderItem.Status == DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Serviceorderitem_Behavior.StatusesEnum.InProgress)
				{
					engine.GenerateInformation($" - Transitioning Service Order Item '{orderItem.Name}' to Completed");
					orderItem = itemHelper.UpdateState(orderItem, DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Serviceorderitem_Behavior.TransitionsEnum.Inprogress_To_Completed);

					var orderHelper = new DataHelperServiceOrder(engine.GetUserConnection());
					var order = orderHelper.Read(ServiceOrderExposers.ServiceOrderItemsExposers.ServiceOrderItem.Equal(orderItem)).FirstOrDefault();
					if (order != null
						&& order.Status == DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Serviceorder_Behavior.StatusesEnum.InProgress
						&& order.OrderItems.All(o => o.ServiceOrderItem.Status == DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Serviceorderitem_Behavior.StatusesEnum.Completed))
					{
						// Transition order to Completed
						engine.GenerateInformation($" - Transitioning Service Order '{order.Name}' to Completed");
						orderHelper.UpdateState(order, DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Serviceorder_Behavior.TransitionsEnum.Inprogress_To_Completed);
					}
				}
			}
		}
	}
}

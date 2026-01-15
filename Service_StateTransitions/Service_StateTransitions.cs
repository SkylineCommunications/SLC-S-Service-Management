namespace ServiceStateTransitions
{
	using System;
	using System.Linq;
	using DomHelpers.SlcServicemanagement;
	using Library;
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

		private static void SetOrderItemToComplete(IEngine engine, Models.Service service, OrderActionType type)
		{
			var itemHelper = new DataHelperServiceOrderItem(engine.GetUserConnection());
			var orderItem = itemHelper.Read(ServiceOrderItemExposers.ServiceID.Equal(service.ID).AND(ServiceOrderItemExposers.Action.Equal(type.ToString()))).FirstOrDefault();
			if (orderItem == null)
			{
				return;
			}

			if (orderItem.Status == SlcServicemanagementIds.Behaviors.Serviceorderitem_Behavior.StatusesEnum.InProgress)
			{
				engine.GenerateInformation($" - Transitioning Service Order Item '{orderItem.Name}' to Completed");
				orderItem = itemHelper.UpdateState(orderItem, SlcServicemanagementIds.Behaviors.Serviceorderitem_Behavior.TransitionsEnum.Inprogress_To_Completed);
			}

			var orderHelper = new DataHelperServiceOrder(engine.GetUserConnection());
			var order = orderHelper.Read(ServiceOrderExposers.ServiceOrderItemsExposers.ServiceOrderItem.Equal(orderItem)).FirstOrDefault();
			if (order == null
				|| order.Status != SlcServicemanagementIds.Behaviors.Serviceorder_Behavior.StatusesEnum.InProgress
				|| order.OrderItems.Any(o => o.ServiceOrderItem.Status != SlcServicemanagementIds.Behaviors.Serviceorderitem_Behavior.StatusesEnum.Completed))
			{
				return;
			}

			// Transition order to Completed as well since all Service Order items are in state completed.
			engine.GenerateInformation($" - Transitioning Service Order '{order.Name}' to Completed");
			orderHelper.UpdateState(order, SlcServicemanagementIds.Behaviors.Serviceorder_Behavior.TransitionsEnum.Inprogress_To_Completed);
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
			service = srvHelper.UpdateState(service, transition);

			switch (transition)
			{
				case TransitionsEnum.Reserved_To_Active:
					SetOrderItemToComplete(engine, service, OrderActionType.Add);
					break;

				case TransitionsEnum.Active_To_Terminated:
					SetOrderItemToComplete(engine, service, OrderActionType.Add);
					break;

				default:
					// Nothing to do
					break;
			}
		}
	}
}
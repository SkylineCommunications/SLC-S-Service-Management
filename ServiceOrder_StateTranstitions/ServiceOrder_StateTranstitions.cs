namespace ServiceOrder_StateTranstitions_1
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Library;
	using Library.Dom;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.IAS;
	using SLC_SM_Common.Dom;
	using static DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Serviceorder_Behavior;
	using static DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Serviceorderitem_Behavior.StatusesEnum;
	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

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

		private static void RunScriptInitServiceInventoryItem(IEngine engine, Models.ServiceOrderItem orderItem)
		{
			engine.GenerateInformation($"Creating Service Inventory Item for Order Item ID {orderItem.Identifier}/{orderItem.Name}");

			// Prepare a subscript
			SubScriptOptions subScript = engine.PrepareSubScript("SLC_SM_Create Service Inventory Item");

			// Link the main script dummies to the subscript
			subScript.SelectScriptParam("DOM ID", orderItem.Identifier);
			subScript.SelectScriptParam("Action", Defaults.ScriptAction_CreateServiceInventoryItem.AddItemSilent.ToString());

			// Set some more options
			subScript.Synchronous = true;
			subScript.InheritScriptOutput = true;

			// Launch the script
			subScript.StartScript();
			if (subScript.HadError)
			{
				throw new InvalidOperationException("Script failed");
			}
		}

		private static void TransitionOrderItemsToInit(IEngine engine, Models.ServiceOrder order)
		{
			bool transitionItems = engine.ShowConfirmDialog("Do you wish to transition all Service Order Items to In Progress as well?\r\nNote: this will initialize the items in the Service Inventory Portal.");
			if (!transitionItems)
			{
				return;
			}

			// Transition all items to In Progress as well
			IConnection connection = engine.GetUserConnection();
			var api = connection.GetServiceManagementApiHelper("Service Ordering");
			foreach (var item in GetOrderItems(api, order))
			{
				if (item.TryStatusUpdateToInProgress(connection))
				{
					RunScriptInitServiceInventoryItem(engine, item); // Init inventory item automatically
				}
			}
		}

		private static void TransitionOrderItemsToRejected(IEngine engine, Models.ServiceOrder order)
		{
			var connection = engine.GetUserConnection();
			var api = connection.GetServiceManagementApiHelper("Service Ordering");
			var orderItems = GetOrderItems(api, order);

			if (orderItems.Any(o => !o.CanBeRejected(connection)))
			{
				throw new NotSupportedException("Some underlying order items or linked service items are already in progress, it's not possible to reject the order at this point");
			}

			if (!engine.ShowConfirmDialog("Do you wish to reject the current order?"))
			{
				return;
			}

			string cancellationReason = engine.ShowFeedbackDialog("Please provide a reason for cancellation");

			foreach (var item in orderItems)
			{
				item.UpdateStatusToRejected(connection);
			}

			order.StatusUpdateToRejected(connection, cancellationReason);
		}

		private static void TransitionToCancelled(IEngine engine, Models.ServiceOrder order)
		{
			var connection = engine.GetUserConnection();
			var api = connection.GetServiceManagementApiHelper("Service Ordering");
			if (GetOrderItems(api, order).Any(o => o.Status != Cancelled))
			{
				throw new NotSupportedException("Some underlying order items are still in progress, it's not possible to cancel the order at this point");
			}

			if (!engine.ShowConfirmDialog("Do you wish to cancel the current order?"))
			{
				return;
			}

			string cancellationReason = engine.ShowFeedbackDialog("Please provide a reason for cancellation");

			order.StatusUpdateToCanceled(connection, cancellationReason);
		}

		private static void TransitionToComplete(IEngine engine, Models.ServiceOrder order)
		{
			if (!order.TryUpdateStatusToCompleted(engine.GetUserConnection()))
			{
				throw new NotSupportedException("Some underlying order items are not yet completed, it's not possible to complete the order at this point");
			}
		}

		private static void TransitionOrderItemsToAck(IEngine engine, Models.ServiceOrder order)
		{
			var connection = engine.GetUserConnection();
			var api = connection.GetServiceManagementApiHelper("Service Ordering");
			foreach (var item in GetOrderItems(api, order))
			{
				item.TryUpdateStatusToAcknowledged(connection);
			}

			order.UpdateStatusToAcknowledged(connection);
		}

		private void RunSafe()
		{
			var instanceId = engine.ReadScriptParamFromApp<Guid>("ServiceOrderReference");
			var previousState = engine.ReadScriptParamFromApp("PreviousState").ToLower();
			var nextState = engine.ReadScriptParamFromApp("NextState").ToLower();

			TransitionsEnum transition = Enum.GetValues(typeof(TransitionsEnum))
											 .Cast<TransitionsEnum?>()
											 .FirstOrDefault(t => t.ToString().Equals($"{previousState}_to_{nextState}", StringComparison.OrdinalIgnoreCase))
										 ?? throw new NotSupportedException($"The provided previousState '{previousState}' is not supported for nextState '{nextState}'");

			var api = engine.GetUserConnection().GetServiceManagementApiHelper("Service Ordering");
			var order = api.ServiceOrder.ServiceOrders.Read(ServiceOrderExposers.Identifier.Equal(instanceId.ToString())).FirstOrDefault()
						?? throw new NotSupportedException($"No Order with ID '{instanceId}' exists on the system");

			switch (transition)
			{
				case TransitionsEnum.New_To_Acknowledged:
					// Transition all items to ACK as well
					TransitionOrderItemsToAck(engine, order);
					break;

				case TransitionsEnum.New_To_Rejected:
				case TransitionsEnum.Acknowledged_To_Rejected:
					// Transition all items to Rejected as well
					TransitionOrderItemsToRejected(engine, order);
					break;

				case TransitionsEnum.Pendingcancellation_To_Cancelled:
					TransitionToCancelled(engine, order);
					break;

				case TransitionsEnum.Acknowledged_To_Inprogress:
					TransitionOrderItemsToInit(engine, order);
					break;

				case TransitionsEnum.Inprogress_To_Completed:
					TransitionToComplete(engine, order);
					break;

				default:
					engine.GenerateInformation($"[SMS] Status Transition: {order.Name} → {transition}");
					api.ServiceOrder.ServiceOrders.TransitionStatus(order, transition);
					break;
			}
		}

		private static List<Models.ServiceOrderItem> GetOrderItems(IServiceManagementApiHelper api, Models.ServiceOrder order)
		{
			var itemIds = new HashSet<string>(
				order.OrderItems
					.Where(x => x?.ServiceOrderItemId != null && !String.IsNullOrWhiteSpace(x.ServiceOrderItemId.Identifier))
					.Select(x => x.ServiceOrderItemId.Identifier),
				StringComparer.OrdinalIgnoreCase);
			if (itemIds.Count == 0)
			{
				return new List<Models.ServiceOrderItem>();
			}

			return api.ServiceOrder.ServiceOrderItems.Read(new TRUEFilterElement<Models.ServiceOrderItem>())
				.Where(x => !String.IsNullOrWhiteSpace(x.Identifier) && itemIds.Contains(x.Identifier))
				.ToList();
		}
	}
}
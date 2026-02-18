namespace ServiceOrder_StateTranstitions_1
{
	using System;
	using System.Linq;
	using Library;
	using Library.Dom;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.IAS;
	using static DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Serviceorder_Behavior;
	using static DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Serviceorderitem_Behavior.StatusesEnum;

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

		private static void RunScriptInitServiceInventoryItem(IEngine engine, Models.ServiceOrderItem orderItem)
		{
			engine.GenerateInformation($"Creating Service Inventory Item for Order Item ID {orderItem.ID}/{orderItem.Name}");

			// Prepare a subscript
			SubScriptOptions subScript = engine.PrepareSubScript("SLC_SM_Create Service Inventory Item");

			// Link the main script dummies to the subscript
			subScript.SelectScriptParam("DOM ID", orderItem.ID.ToString());
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

		private void RunSafe(IEngine engine)
		{
			var instanceId = engine.ReadScriptParamFromApp<Guid>("ServiceOrderReference");
			var previousState = engine.ReadScriptParamFromApp("PreviousState").ToLower();
			var nextState = engine.ReadScriptParamFromApp("NextState").ToLower();

			TransitionsEnum transition = Enum.GetValues(typeof(TransitionsEnum))
											 .Cast<TransitionsEnum?>()
											 .FirstOrDefault(t => t.ToString().Equals($"{previousState}_to_{nextState}", StringComparison.OrdinalIgnoreCase))
										 ?? throw new NotSupportedException($"The provided previousState '{previousState}' is not supported for nextState '{nextState}'");

			var orderHelper = new DataHelperServiceOrder(engine.GetUserConnection());
			var order = orderHelper.Read(ServiceOrderExposers.Guid.Equal(instanceId)).FirstOrDefault()
						?? throw new NotSupportedException($"No Order with ID '{instanceId}' exists on the system");

			switch (transition)
			{
				case TransitionsEnum.New_To_Acknowledged:
					// Transition all items to ACK as well
					TransitionOrderItemsToAck(engine, orderHelper, order, transition);
					break;

				case TransitionsEnum.New_To_Rejected:
				case TransitionsEnum.Acknowledged_To_Rejected:
					// Transition all items to Rejected as well
					TransitionOrderItemsToRejected(engine, orderHelper, order, transition);
					break;

				case TransitionsEnum.Pendingcancellation_To_Cancelled:
					TransitionToCancelled(engine, orderHelper, order, transition);
					break;

				case TransitionsEnum.Acknowledged_To_Inprogress:
					TransitionOrderItemsToInit(engine, orderHelper, order, transition);
					break;

				case TransitionsEnum.Inprogress_To_Completed:
					TransitionToComplete(engine, orderHelper, order, transition);
					break;

				default:
					engine.GenerateInformation($"Service Order Status Transition starting: previousState: {previousState}, nextState: {nextState}");
					orderHelper.UpdateState(order, transition);
					break;
			}
		}

		private static void TransitionOrderItemsToInit(IEngine engine, DataHelperServiceOrder orderHelper, Models.ServiceOrder order, TransitionsEnum transition)
		{
			bool transitionItems = engine.ShowConfirmDialog("Do you wish to transition all Service Order Items to In Progress as well?\r\nNote: this will initialize the items in the Service Inventory Portal.");
			if (transitionItems)
			{
				// Transition all items to In Progress as well
				var itemHelper = new DataHelperServiceOrderItem(engine.GetUserConnection());
				foreach (var item in order.OrderItems.Where(x => x.ServiceOrderItem.Status == Acknowledged))
				{
					var updatedItem = itemHelper.Read(ServiceOrderItemExposers.Guid.Equal(item.ServiceOrderItem.ID)).FirstOrDefault()
									  ?? throw new InvalidOperationException($"Service Order Item with ID '{item.ServiceOrderItem.ID}' no longer exists.");
					if (updatedItem.Status == Acknowledged)
					{
						engine.GenerateInformation($" - Transitioning Service Order Item '{item.ServiceOrderItem.Name}' to In Progress");
						itemHelper.UpdateState(item.ServiceOrderItem, DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Serviceorderitem_Behavior.TransitionsEnum.Acknowledged_To_Inprogress);

						RunScriptInitServiceInventoryItem(engine, item.ServiceOrderItem); // Init inventory item automatically
					}
				}

				engine.GenerateInformation($"Service Order Status Transition starting: {transition}");
				order = orderHelper.Read(ServiceOrderExposers.Guid.Equal(order.ID)).FirstOrDefault()
							?? throw new NotSupportedException($"No Order with ID '{order.ID}' exists on the system");
				if (order.Status == StatusesEnum.Acknowledged)
				{
					orderHelper.UpdateState(order, transition);
				}
			}
		}

		private static void TransitionOrderItemsToRejected(IEngine engine, DataHelperServiceOrder orderHelper, Models.ServiceOrder order, TransitionsEnum transition)
		{
			if (order.OrderItems.Any(o => !o.ServiceOrderItem.CanBeRejected(engine.GetUserConnection())))
			{
				throw new NotSupportedException("Some underlying order items or linked service items are already in progress, it's not possible to reject the order at this point");
			}

			if (!engine.ShowConfirmDialog("Do you wish to reject the current order?"))
			{
				return;
			}

			string cancellationReason = engine.ShowFeedbackDialog("Please provide a reason for cancellation");
			order.CancellationInfo.Reason = cancellationReason;
			order.CancellationInfo.CancellationDate = DateTime.UtcNow;
			orderHelper.CreateOrUpdate(order);

			foreach (var item in order.OrderItems)
			{
				item.ServiceOrderItem.SetStatusToRejected(engine);
			}

			engine.GenerateInformation($"Service Order Status Transition starting: {transition}");
			orderHelper.UpdateState(order, transition);
		}

		private static void TransitionToCancelled(IEngine engine, DataHelperServiceOrder orderHelper, Models.ServiceOrder order, TransitionsEnum transition)
		{
			if (order.OrderItems.Any(o => o.ServiceOrderItem.Status != Cancelled))
			{
				throw new NotSupportedException("Some underlying order items are still in progress, it's not possible to cancel the order at this point");
			}

			if (!engine.ShowConfirmDialog("Do you wish to cancel the current order?"))
			{
				return;
			}

			string cancellationReason = engine.ShowFeedbackDialog("Please provide a reason for cancellation");
			order.CancellationInfo.Reason = cancellationReason;
			order.CancellationInfo.CancellationDate = DateTime.UtcNow;
			orderHelper.CreateOrUpdate(order);

			engine.GenerateInformation($"Service Order Status Transition starting: {transition}");
			orderHelper.UpdateState(order, transition);
		}

		private static void TransitionToComplete(IEngine engine, DataHelperServiceOrder orderHelper, Models.ServiceOrder order, TransitionsEnum transition)
		{
			if (order.OrderItems.Any(o => o.ServiceOrderItem.Status != Completed && o.ServiceOrderItem.Status != Cancelled))
			{
				throw new NotSupportedException("Some underlying order items are not yet completed, it's not possible to complete the order at this point");
			}

			engine.GenerateInformation($"Service Order Status Transition starting: {transition}");
			orderHelper.UpdateState(order, transition);
		}

		private static void TransitionOrderItemsToAck(IEngine engine, DataHelperServiceOrder orderHelper, Models.ServiceOrder order, TransitionsEnum transition)
		{
			var itemHelper = new DataHelperServiceOrderItem(engine.GetUserConnection());
			foreach (var item in order.OrderItems.Where(x => x.ServiceOrderItem.Status == New))
			{
				engine.GenerateInformation($" - Transitioning Service Order Item '{item.ServiceOrderItem.Name}' to Acknowledged");
				itemHelper.UpdateState(item.ServiceOrderItem, DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Serviceorderitem_Behavior.TransitionsEnum.New_To_Acknowledged);
			}

			engine.GenerateInformation($"Service Order Status Transition starting: {transition}");
			orderHelper.UpdateState(order, transition);
		}
	}
}
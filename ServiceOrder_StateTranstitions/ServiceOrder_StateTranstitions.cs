namespace ServiceOrder_StateTranstitions_1
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
	using static DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Serviceorder_Behavior;

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

		public void RunSafe(IEngine engine)
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

			engine.GenerateInformation($"Service Order Status Transition starting: previousState: {previousState}, nextState: {nextState}");
			order = orderHelper.UpdateState(order, transition);

			if (transition == TransitionsEnum.New_To_Acknowledged)
			{
				// Transition all items to ACK as well
				var itemHelper = new DataHelperServiceOrderItem(engine.GetUserConnection());
				foreach (var item in order.OrderItems.Where(x => x.ServiceOrderItem.Status == DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Serviceorderitem_Behavior.StatusesEnum.New))
				{
					engine.GenerateInformation($" - Transitioning Service Order Item '{item.ServiceOrderItem.Name}' to Acknowledged");
					itemHelper.UpdateState(item.ServiceOrderItem, DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Serviceorderitem_Behavior.TransitionsEnum.New_To_Acknowledged);
				}
			}
			else if (transition == TransitionsEnum.Acknowledged_To_Rejected)
			{
				// Transition all items to Rejected as well
				var itemHelper = new DataHelperServiceOrderItem(engine.GetUserConnection());
				foreach (var item in order.OrderItems.Where(x => x.ServiceOrderItem.Status == DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Serviceorderitem_Behavior.StatusesEnum.Acknowledged))
				{
					engine.GenerateInformation($" - Transitioning Service Order Item '{item.ServiceOrderItem.Name}' to Rejected");
					itemHelper.UpdateState(item.ServiceOrderItem, DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Serviceorderitem_Behavior.TransitionsEnum.Acknowledged_To_Rejected);
				}
			}
			else if (transition == TransitionsEnum.Acknowledged_To_Inprogress)
			{
				bool transitionItems = engine.ShowConfirmDialog("Do you wish to transition all Service Order Items to In Progress as well?\r\nNote: this will initialize the items in the Service Inventory Portal.");
				if (transitionItems)
				{
					// Transition all items to In Progress as well
					var itemHelper = new DataHelperServiceOrderItem(engine.GetUserConnection());
					foreach (var item in order.OrderItems.Where(x => x.ServiceOrderItem.Status == DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Serviceorderitem_Behavior.StatusesEnum.Acknowledged))
					{
						var updatedItem = itemHelper.Read(ServiceOrderItemExposers.Guid.Equal(item.ServiceOrderItem.ID)).FirstOrDefault() ?? throw new InvalidOperationException($"Service Order Item with ID '{item.ServiceOrderItem.ID}' no longer exists.");
						if (updatedItem.Status == DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Serviceorderitem_Behavior.StatusesEnum.Acknowledged)
						{
							engine.GenerateInformation($" - Transitioning Service Order Item '{item.ServiceOrderItem.Name}' to In Progress");
							itemHelper.UpdateState(updatedItem, DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Serviceorderitem_Behavior.TransitionsEnum.Acknowledged_To_Inprogress);
						}

						RunScriptInitServiceInventoryItem(engine, item.ServiceOrderItem); // Init inventory item automatically
					}
				}
			}
		}

		private static void RunScriptInitServiceInventoryItem(IEngine engine, Models.ServiceOrderItem orderItem)
		{
			engine.GenerateInformation($"Creating Service Inventory Item for Order Item ID {orderItem.ID}/{orderItem.Name}");

			// Prepare a subscript
			SubScriptOptions subScript = engine.PrepareSubScript("SLC_SM_Create Service Inventory Item");

			// Link the main script dummies to the subscript
			subScript.SelectScriptParam("DOM ID", orderItem.ID.ToString());
			subScript.SelectScriptParam("Action", "AddItemSilent");

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
	}
}
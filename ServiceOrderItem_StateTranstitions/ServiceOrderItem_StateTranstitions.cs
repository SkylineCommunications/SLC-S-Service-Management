namespace ServiceOrderItemStateTranstitions
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
	using static DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Serviceorderitem_Behavior;

	/// <summary>
	///     Represents a DataMiner Automation script.
	/// </summary>
	public class Script
	{
		public static void RunSafe(IEngine engine)
		{
			Guid domInstanceId = engine.ReadScriptParamFromApp<Guid>("Id");
			string previousState = engine.ReadScriptParamFromApp("PreviousState").ToLower();
			string nextState = engine.ReadScriptParamFromApp("NextState").ToLower();

			TransitionsEnum transition = Enum.GetValues(typeof(TransitionsEnum))
				.Cast<TransitionsEnum?>()
				.FirstOrDefault(t => t.ToString().Equals($"{previousState}_to_{nextState}", StringComparison.OrdinalIgnoreCase))
				?? throw new NotSupportedException($"The provided previousState '{previousState}' is not supported for nextState '{nextState}'");

			var orderItemHelper = new DataHelperServiceOrderItem(engine.GetUserConnection());
			var orderItem = orderItemHelper.Read(ServiceOrderItemExposers.Guid.Equal(domInstanceId)).FirstOrDefault()
						  ?? throw new NotSupportedException($"No Order Item with ID '{domInstanceId}' exists on the system");

			engine.GenerateInformation($"Service Order Item Status Transition starting: previousState: {previousState}, nextState: {nextState}");
			orderItem = orderItemHelper.UpdateState(orderItem, transition);

			switch (transition)
			{
				case TransitionsEnum.New_To_Acknowledged:
					// Transition parent order to ACK as well
					TransitionOrderToAck(engine, orderItem);
					break;

				case TransitionsEnum.Acknowledged_To_Inprogress:
					// Transition parent order to In Progress as well
					TransitionOrderToInprogress(engine, orderItem);
					break;

				case TransitionsEnum.Inprogress_To_Completed:
					// Transition parent order to Active as well
					TransitionOrderToCompleted(engine, orderItem);
					break;
			}
		}

		private static void TransitionOrderToCompleted(IEngine engine, Models.ServiceOrderItem orderItem)
		{
			var orderHelper = new DataHelperServiceOrder(engine.GetUserConnection());
			var order = orderHelper.Read(ServiceOrderExposers.ServiceOrderItemsExposers.ServiceOrderItem.Equal(orderItem)).FirstOrDefault()
			            ?? throw new NotSupportedException($"No Service Order exists that contains Child ID '{orderItem.ID}' on the system");
			if (order.Status == DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Serviceorder_Behavior.StatusesEnum.InProgress)
			{
				engine.GenerateInformation($" - Transitioning Parent Service Order '{order.Name}' to Activated");
				orderHelper.UpdateState(order, DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Serviceorder_Behavior.TransitionsEnum.Inprogress_To_Completed);
			}
		}

		private static void TransitionOrderToInprogress(IEngine engine, Models.ServiceOrderItem orderItem)
		{
			var orderHelper = new DataHelperServiceOrder(engine.GetUserConnection());
			var order = orderHelper.Read(ServiceOrderExposers.ServiceOrderItemsExposers.ServiceOrderItem.Equal(orderItem)).FirstOrDefault()
			            ?? throw new NotSupportedException($"No Service Order exists that contains Child ID '{orderItem.ID}' on the system");
			if (order.Status == DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Serviceorder_Behavior.StatusesEnum.Acknowledged)
			{
				engine.GenerateInformation($" - Transitioning Parent Service Order '{order.Name}' to In Progress");
				orderHelper.UpdateState(order, DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Serviceorder_Behavior.TransitionsEnum.Acknowledged_To_Inprogress);
			}
		}

		private static void TransitionOrderToAck(IEngine engine, Models.ServiceOrderItem orderItem)
		{
			var orderHelper = new DataHelperServiceOrder(engine.GetUserConnection());
			var order = orderHelper.Read(ServiceOrderExposers.ServiceOrderItemsExposers.ServiceOrderItem.Equal(orderItem)).FirstOrDefault()
			            ?? throw new NotSupportedException($"No Service Order exists that contains Child ID '{orderItem.ID}' on the system");
			if (order.Status == DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Serviceorder_Behavior.StatusesEnum.New && order.OrderItems.All(x => x.ServiceOrderItem.Status == StatusesEnum.Acknowledged))
			{
				engine.GenerateInformation($" - Transitioning Parent Service Order '{order.Name}' to Acknowledged");
				orderHelper.UpdateState(order, DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Serviceorder_Behavior.TransitionsEnum.New_To_Acknowledged);
			}
		}

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
	}
}
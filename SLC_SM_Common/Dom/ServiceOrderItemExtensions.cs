namespace Library.Dom
{
	using System.Linq;
	using DomHelpers.SlcServicemanagement;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM;
	using static DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Serviceorderitem_Behavior.StatusesEnum;

	public static class ServiceOrderItemExtensions
	{
		/// <summary>
		/// Transitions the specified service order item to the Completed status if it is currently In Progress. If all items
		/// in the parent service order are completed or cancelled, transitions the parent service order to Completed as well.
		/// </summary>
		/// <param name="orderItem">The service order item to transition to the Completed status. Must not be null.</param>
		/// <param name="engine">The engine used to perform status transitions and generate informational messages. Must not be null.</param>
		public static void SetStatusToCompleted(this Models.ServiceOrderItem orderItem, IEngine engine)
		{
			if (orderItem == null || orderItem.Status != InProgress)
			{
				return;
			}

			engine.GenerateInformation($" - Transitioning Service Order Item '{orderItem.Name}' to Completed");
			orderItem = new DataHelperServiceOrderItem(engine.GetUserConnection()).UpdateState(orderItem, SlcServicemanagementIds.Behaviors.Serviceorderitem_Behavior.TransitionsEnum.Inprogress_To_Completed);

			var orderHelper = new DataHelperServiceOrder(engine.GetUserConnection());
			var order = orderHelper.Read(ServiceOrderExposers.ServiceOrderItemsExposers.ServiceOrderItem.Equal(orderItem)).FirstOrDefault();
			if (order == null
				|| order.Status != SlcServicemanagementIds.Behaviors.Serviceorder_Behavior.StatusesEnum.InProgress
				|| order.OrderItems.Any(o => o.ServiceOrderItem.Status != Completed && o.ServiceOrderItem.Status != Cancelled))
			{
				return;
			}

			// Transition order to Completed as well since all Service Order items are in state completed.
			engine.GenerateInformation($" - Transitioning Service Order '{order.Name}' to Completed");
			orderHelper.UpdateState(order, SlcServicemanagementIds.Behaviors.Serviceorder_Behavior.TransitionsEnum.Inprogress_To_Completed);
		}

		/// <summary>
		/// Transitions the specified service order item to the Rejected status if it is currently in the New or Acknowledged
		/// state.
		/// </summary>
		/// <param name="orderItem">The service order item to update. The status must be New or Acknowledged for the transition to occur.</param>
		/// <param name="engine">The engine instance used to perform the status transition and access related data.</param>
		public static void SetStatusToRejected(this Models.ServiceOrderItem orderItem, IEngine engine)
		{
			if (orderItem == null || (orderItem.Status != New && orderItem.Status != Acknowledged))
			{
				return;
			}

			SlcServicemanagementIds.Behaviors.Serviceorderitem_Behavior.TransitionsEnum transition;
			if (orderItem.Status == New)
			{
				transition = SlcServicemanagementIds.Behaviors.Serviceorderitem_Behavior.TransitionsEnum.New_To_Rejected;
			}
			else if (orderItem.Status == Acknowledged)
			{
				transition = SlcServicemanagementIds.Behaviors.Serviceorderitem_Behavior.TransitionsEnum.Acknowledged_To_Rejected;
			}
			else
			{
				return;
			}

			var itemHelper = new DataHelperServiceOrderItem(engine.GetUserConnection());
			engine.GenerateInformation($" - Transitioning Service Order Item '{orderItem.Name}' to Rejected");
			itemHelper.UpdateState(orderItem, transition);

			if (orderItem.ServiceId.HasValue)
			{
				var srvHelper = new DataHelperService(engine.GetUserConnection());
				var srv = srvHelper.Read(ServiceExposers.Guid.Equal(orderItem.ServiceId.Value)).FirstOrDefault();
				srv?.SetStatusToRetired(engine);
			}
		}

		public static bool CanBeRejected(this Models.ServiceOrderItem orderItem, IConnection connection)
		{
			if (orderItem.Status != New && orderItem.Status != Acknowledged)
			{
				return false;
			}

			if (!orderItem.ServiceId.HasValue)
			{
				return true;
			}

			var linkedService = new DataHelperService(connection).Read(ServiceExposers.Guid.Equal(orderItem.ServiceId.Value)).FirstOrDefault();
			if (linkedService == null)
			{
				return true;
			}

			if (linkedService.Status == SlcServicemanagementIds.Behaviors.Service_Behavior.StatusesEnum.Designed
				|| linkedService.Status == SlcServicemanagementIds.Behaviors.Service_Behavior.StatusesEnum.Active)
			{
				return false;
			}

			return true;
		}
	}
}
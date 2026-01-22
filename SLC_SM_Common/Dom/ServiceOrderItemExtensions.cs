namespace Library.Dom
{
	using System.Linq;
	using DomHelpers.SlcServicemanagement;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM;
	using static DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Serviceorderitem_Behavior.StatusesEnum;

	public static class ServiceOrderItemExtensions
	{
		public static void SetStatusToCompleted(this Models.ServiceOrderItem orderItem, IEngine engine)
		{
			if (orderItem.Status != InProgress)
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
	}
}
namespace Library.Dom
{
	using System;
	using System.Linq;
	using DomHelpers.SlcServicemanagement;
	using Skyline.DataMiner.Net;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using SLC_SM_Common.Dom;
	using SLC_SM_Common.Extensions;
	using static DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Serviceorderitem_Behavior;
	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	public static class ServiceOrderItemExtensions
	{
		/// <summary>
		/// Updates the status of the specified service order item to InProgress if its current status is Acknowledged or
		/// Pending.
		/// </summary>
		/// <param name="orderItem">The service order item whose status will be updated. If null, the method returns without performing any action.</param>
		/// <param name="connection">DataMiner connection reference.</param>
		/// <returns>true if the status update was successful and the order item is now in the InProgress status; otherwise, false.</returns>
		public static bool TryStatusUpdateToInProgress(this Models.ServiceOrderItem orderItem, IConnection connection)
		{
			if (orderItem == null)
			{
				return false;
			}

			TransitionsEnum transition;
			if (orderItem.Status == StatusesEnum.Acknowledged)
			{
				transition = TransitionsEnum.Acknowledged_To_Inprogress;
			}
			else if (orderItem.Status == StatusesEnum.Pending)
			{
				transition = TransitionsEnum.Pending_To_Inprogress;
			}
			else
			{
				return false;
			}

			var api = connection.GetServiceManagementApiHelper("Service Ordering");
			connection.GenerateInformationMessage($"[SMS] Status Transition: {orderItem.Name} → {transition}");
			orderItem = api.ServiceOrder.ServiceOrderItems.TransitionStatus(orderItem, transition);

			var order = FindParentOrder(api, orderItem);
			order?.StatusUpdateToInProgress(connection);

			return orderItem?.Status == StatusesEnum.InProgress;
		}

		/// <summary>
		/// Transitions the status of the specified service order item from New to Acknowledged, if applicable.
		/// </summary>
		/// <param name="orderItem">The service order item to update. If null, the method performs no action.</param>
		/// <param name="connection">DataMiner connection reference.</param>
		/// <returns>true if the status update was successful and the order item is now in the Acknowledged status; otherwise, false.</returns>
		public static bool TryUpdateStatusToAcknowledged(this Models.ServiceOrderItem orderItem, IConnection connection)
		{
			if (orderItem == null)
			{
				return false;
			}

			TransitionsEnum transition;
			if (orderItem.Status == StatusesEnum.New)
			{
				transition = TransitionsEnum.New_To_Acknowledged;
			}
			else
			{
				return false;
			}

			var api = connection.GetServiceManagementApiHelper("Service Ordering");
			connection.GenerateInformationMessage($"[SMS] Status Transition: {orderItem.Name} → {transition}");
			orderItem = api.ServiceOrder.ServiceOrderItems.TransitionStatus(orderItem, transition);

			var order = FindParentOrder(api, orderItem);
			order?.UpdateStatusToAcknowledged(connection);

			return orderItem?.Status == StatusesEnum.Acknowledged;
		}

		/// <summary>
		/// Transitions the specified service order item to the Completed status if it is currently In Progress. If all items
		/// in the parent service order are completed or cancelled, transitions the parent service order to Completed as well.
		/// </summary>
		/// <param name="orderItem">The service order item to transition to the Completed status. Must not be null.</param>
		/// <param name="connection">DataMiner connection reference.</param>
		public static void UpdateStatusToCompleted(this Models.ServiceOrderItem orderItem, IConnection connection)
		{
			if (orderItem == null)
			{
				return;
			}

			TransitionsEnum transition;
			if (orderItem.Status == StatusesEnum.InProgress)
			{
				transition = TransitionsEnum.Inprogress_To_Completed;
			}
			else
			{
				return;
			}

			var api = connection.GetServiceManagementApiHelper("Service Ordering");
			connection.GenerateInformationMessage($"[SMS] Status Transition: {orderItem.Name} → {transition}");
			orderItem = api.ServiceOrder.ServiceOrderItems.TransitionStatus(orderItem, transition);

			var order = FindParentOrder(api, orderItem);
			order?.StatusUpdateToCompleted(connection);
		}

		/// <summary>
		/// Transitions the specified service order item to the Rejected status if it is currently in the New or Acknowledged
		/// state.
		/// </summary>
		/// <param name="orderItem">The service order item to update. The status must be New or Acknowledged for the transition to occur.</param>
		/// <param name="connection">DataMiner connection reference.</param>
		/// <returns>The updated service order item with the Rejected status if the transition was successful; otherwise, returns the original service order item.</returns>
		public static Models.ServiceOrderItem UpdateStatusToRejected(this Models.ServiceOrderItem orderItem, IConnection connection)
		{
			if (orderItem == null)
			{
				return orderItem;
			}

			if (!orderItem.CanBeRejected(connection))
			{
				throw new NotSupportedException("Some underlying order items or linked service items are already in progress, it's not possible to reject the order at this point");
			}

			TransitionsEnum transition;
			if (orderItem.Status == StatusesEnum.New)
			{
				transition = TransitionsEnum.New_To_Rejected;
			}
			else if (orderItem.Status == StatusesEnum.Acknowledged)
			{
				transition = TransitionsEnum.Acknowledged_To_Rejected;
			}
			else
			{
				return orderItem;
			}

			var orderingApi = connection.GetServiceManagementApiHelper("Service Ordering");
			connection.GenerateInformationMessage($"[SMS] Status Transition: {orderItem.Name} → {transition}");
			orderItem = orderingApi.ServiceOrder.ServiceOrderItems.TransitionStatus(orderItem, transition);

			var linkedServiceId = GetLinkedServiceIdentifier(orderItem);
			if (!String.IsNullOrWhiteSpace(linkedServiceId)
				&& Guid.TryParse(linkedServiceId, out Guid _))
			{
				var inventoryApi = connection.GetServiceManagementApiHelper("Service Inventory");
				var srv = inventoryApi.ServiceInventory.Services.Read(ServiceExposers.Identifier.Equal(linkedServiceId)).FirstOrDefault();
				srv?.UpdateStatusToRetired(connection);
			}

			return orderItem;
		}

		/// <summary>
		/// Determines whether the specified service order item can be rejected based on its current status and the status of
		/// its linked service.
		/// </summary>
		/// <param name="orderItem">The service order item to evaluate for rejection eligibility. Must not be null.</param>
		/// <param name="connection">DataMiner connection reference.</param>
		/// <returns>true if the service order item can be rejected; otherwise, false.</returns>
		public static bool CanBeRejected(this Models.ServiceOrderItem orderItem, IConnection connection)
		{
			if (orderItem.Status != StatusesEnum.New && orderItem.Status != StatusesEnum.Acknowledged)
			{
				return false;
			}

			var linkedServiceId = GetLinkedServiceIdentifier(orderItem);
			if (String.IsNullOrWhiteSpace(linkedServiceId) || !Guid.TryParse(linkedServiceId, out Guid _))
			{
				return true;
			}

			var linkedService = connection.GetServiceManagementApiHelper("Service Inventory")
				.ServiceInventory.Services.Read(ServiceExposers.Identifier.Equal(linkedServiceId))
				.FirstOrDefault();
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

		private static Models.ServiceOrder FindParentOrder(IServiceManagementApiHelper api, Models.ServiceOrderItem orderItem)
		{
			if (orderItem == null || String.IsNullOrWhiteSpace(orderItem.Identifier))
			{
				return null;
			}

			return api.ServiceOrder.ServiceOrders.Read(new TRUEFilterElement<Models.ServiceOrder>())
				.FirstOrDefault(order => order.OrderItems.Any(entry =>
					entry?.ServiceOrderItemId != null
					&& String.Equals(entry.ServiceOrderItemId.Identifier, orderItem.Identifier, StringComparison.OrdinalIgnoreCase)));
		}

		private static string GetLinkedServiceIdentifier(Models.ServiceOrderItem orderItem)
		{
			if (orderItem?.ServiceInfo == null)
			{
				return String.Empty;
			}

			return orderItem.ServiceInfo.ServiceId.Identifier ?? String.Empty;
		}
	}
}
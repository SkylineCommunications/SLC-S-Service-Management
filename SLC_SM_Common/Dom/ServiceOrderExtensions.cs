namespace SLC_SM_Common.Dom
{
	using System;
	using System.Linq;
	using DomHelpers.SlcServicemanagement;
	using Skyline.DataMiner.Net;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement;
	using SLC_SM_Common.Extensions;
	using static DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Serviceorder_Behavior;

	public static class ServiceOrderExtensions
	{
		public static bool TryUpdateStatusToCompleted(this Models.ServiceOrder order, IConnection connection)
		{
			var updatedOrder = order?.StatusUpdateToCompleted(connection);
			return updatedOrder?.Status == StatusesEnum.Completed;
		}

		/// <summary>
		/// Transitions the specified service order to the Completed status if all order items are completed or cancelled.
		/// </summary>
		/// <param name="order">The service order to update. Must not be null and must have a status of InProgress.</param>
		/// <param name="connection">DataMiner connection reference.</param>
		/// <returns>The updated service order with its status set to Completed if succeeded.</returns>
		public static Models.ServiceOrder StatusUpdateToCompleted(this Models.ServiceOrder order, IConnection connection)
		{
			if (order == null)
			{
				return order;
			}

			// Order can only be completed if all order items are either completed or cancelled. If there is at least one order item that is not in one of these two states, the order cannot be completed.
			if (order.OrderItems.Any(o => o.ServiceOrderItem.Status != SlcServicemanagementIds.Behaviors.Serviceorderitem_Behavior.StatusesEnum.Completed && o.ServiceOrderItem.Status != SlcServicemanagementIds.Behaviors.Serviceorderitem_Behavior.StatusesEnum.Cancelled))
			{
				return order;
			}

			TransitionsEnum transition;
			if (order.Status == StatusesEnum.InProgress)
			{
				transition = TransitionsEnum.Inprogress_To_Completed;
			}
			else
			{
				return order;
			}

			connection.GenerateInformationMessage($"[SMS] Status Transition: {order.Name} → {transition}");
			var orderHelper = new DataHelperServiceOrder(connection);
			return orderHelper.UpdateState(order, transition);
		}

		/// <summary>
		/// Updates the status of the specified service order to InProgress if its current status is Pending or Acknowledged.
		/// </summary>
		/// <param name="order">The service order to update. If null or not in a valid state for transition, the original order is returned.</param>
		/// <param name="connection">DataMiner connection reference.</param>
		/// <returns>A new service order instance with the status set to InProgress if the transition is valid; otherwise, the original
		/// order.</returns>
		public static Models.ServiceOrder StatusUpdateToInProgress(this Models.ServiceOrder order, IConnection connection)
		{
			if (order == null)
			{
				return order;
			}

			TransitionsEnum transition;
			if (order.Status == StatusesEnum.Acknowledged)
			{
				transition = TransitionsEnum.Acknowledged_To_Inprogress;
			}
			else if (order.Status == StatusesEnum.Pending)
			{
				transition = TransitionsEnum.Pending_To_Inprogress;
			}
			else
			{
				return order;
			}

			connection.GenerateInformationMessage($"[SMS] Status Transition: {order.Name} → {transition}");
			var orderHelper = new DataHelperServiceOrder(connection);
			return orderHelper.UpdateState(order, transition);
		}

		/// <summary>
		/// Transitions the status of the specified service order from New to Acknowledged, if applicable.
		/// </summary>
		/// <param name="order">The service order to update. If the order is null or not in the New status, no changes are made.</param>
		/// <param name="connection">DataMiner connection reference.</param>
		/// <returns>A new instance of the service order with its status updated to Acknowledged if the original status was New;
		/// otherwise, returns the original order.</returns>
		public static Models.ServiceOrder UpdateStatusToAcknowledged(this Models.ServiceOrder order, IConnection connection)
		{
			if (order == null)
			{
				return order;
			}

			if (order.OrderItems.Any(o => o.ServiceOrderItem.Status != SlcServicemanagementIds.Behaviors.Serviceorderitem_Behavior.StatusesEnum.Acknowledged))
			{
				return order;
			}

			TransitionsEnum transition;
			if (order.Status == StatusesEnum.New)
			{
				transition = TransitionsEnum.New_To_Acknowledged;
			}
			else
			{
				return order;
			}

			connection.GenerateInformationMessage($"[SMS] Status Transition: {order.Name} → {transition}");
			var orderHelper = new DataHelperServiceOrder(connection);
			return orderHelper.UpdateState(order, transition);
		}

		/// <summary>
		/// Updates the status of the service order to Canceled if all order items are already canceled and the order is
		/// pending cancellation.
		/// </summary>
		/// <param name="order">The service order to update. If null, the method returns null.</param>
		/// <param name="connection">The connection used to persist changes to the service order.</param>
		/// <param name="cancellationReason">The reason for canceling the service order. This value is recorded in the order's cancellation information.</param>
		/// <returns>The updated service order with status set to Canceled if the transition is valid; otherwise, returns the original
		/// order.</returns>
		public static Models.ServiceOrder StatusUpdateToCanceled(this Models.ServiceOrder order, IConnection connection, string cancellationReason)
		{
			if (order == null)
			{
				return order;
			}

			if (order.OrderItems.Any(o => o.ServiceOrderItem.Status != SlcServicemanagementIds.Behaviors.Serviceorderitem_Behavior.StatusesEnum.Cancelled))
			{
				return order;
			}

			TransitionsEnum transition;
			if (order.Status == StatusesEnum.PendingCancellation)
			{
				transition = TransitionsEnum.Pendingcancellation_To_Cancelled;
			}
			else
			{
				return order;
			}

			var orderHelper = new DataHelperServiceOrder(connection);

			order.CancellationInfo.Reason = cancellationReason;
			order.CancellationInfo.CancellationDate = DateTime.UtcNow;
			orderHelper.CreateOrUpdate(order);

			connection.GenerateInformationMessage($"[SMS] Status Transition: {order.Name} → {transition}");
			return orderHelper.UpdateState(order, transition);
		}

		/// <summary>
		/// Updates the status of the service order to Rejected if all order items are already rejected and the current status
		/// allows the transition.
		/// </summary>
		/// <param name="order">The service order to update. If null, the method returns null.</param>
		/// <param name="connection">The connection used to persist changes to the service order.</param>
		/// <param name="reasonForRejection">The reason for rejecting the service order. This value is recorded in the cancellation information.</param>
		/// <returns>The updated service order with its status set to Rejected if the transition is valid; otherwise, returns the
		/// original order.</returns>
		public static Models.ServiceOrder StatusUpdateToRejected(this Models.ServiceOrder order, IConnection connection, string reasonForRejection)
		{
			if (order == null)
			{
				return order;
			}

			if (order.OrderItems.Any(o => o.ServiceOrderItem.Status != SlcServicemanagementIds.Behaviors.Serviceorderitem_Behavior.StatusesEnum.Rejected))
			{
				return order;
			}

			TransitionsEnum transition;
			if (order.Status == StatusesEnum.New)
			{
				transition = TransitionsEnum.New_To_Rejected;
			}
			else if (order.Status == StatusesEnum.Acknowledged)
			{
				transition = TransitionsEnum.Acknowledged_To_Rejected;
			}
			else
			{
				return order;
			}

			var orderHelper = new DataHelperServiceOrder(connection);

			order.CancellationInfo.Reason = reasonForRejection;
			order.CancellationInfo.CancellationDate = DateTime.UtcNow;
			orderHelper.CreateOrUpdate(order);

			connection.GenerateInformationMessage($"[SMS] Status Transition: {order.Name} → {transition}");
			return orderHelper.UpdateState(order, transition);
		}
	}
}

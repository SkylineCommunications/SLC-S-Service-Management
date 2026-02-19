namespace Library.Dom
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using DomHelpers.SlcServicemanagement;
	using Newtonsoft.Json;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net;
	using Skyline.DataMiner.Net.Messages;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.Relationship;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM;
	using Skyline.DataMiner.Utils.MediaOps.Common.IOData.Scheduling.Scripts.JobHandler;
	using Skyline.DataMiner.Utils.MediaOps.Helpers.Scheduling;
	using static DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Service_Behavior;
	using static SLC_SM_Common.Extensions.GqiDmsExtensions;
	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement.Models;

	public static class ServiceItemExtensions
	{
		/// <summary>
		/// Transitions the specified service to the Active state if it is currently in the Reserved or Terminated state.
		/// </summary>
		/// <param name="service">The service instance to update. If null or not in a valid state for transition, the service is returned unchanged.</param>
		/// <param name="connection">DataMiner connection reference.</param>
		/// <returns>The updated service instance with its status set to Active if a valid transition was performed; otherwise, the
		/// original service instance.</returns>
		public static Models.Service UpdateStatusToActive(this Models.Service service, IConnection connection)
		{
			if (service == null)
			{
				return service;
			}

			TransitionsEnum transition;
			if (service.Status == StatusesEnum.Reserved)
			{
				transition = TransitionsEnum.Reserved_To_Active;
			}
			else if (service.Status == StatusesEnum.Terminated)
			{
				transition = TransitionsEnum.Terminated_To_Active;
			}
			else
			{
				return service;
			}

			var srvHelper = new DataHelperService(connection);

			connection.GenerateInformationMessage($"[SMS] Status Transition: {service.Name} → {transition}");
			service = srvHelper.UpdateState(service, transition);

			var itemHelper = new DataHelperServiceOrderItem(connection);
			var orderItem = itemHelper.Read(ServiceOrderItemExposers.ServiceID.Equal(service.ID)
				.AND(ServiceOrderItemExposers.Action.Equal(OrderActionType.Add.ToString()))).FirstOrDefault();
			orderItem?.UpdateStatusToCompleted(connection);

			return service;
		}

		/// <summary>
		/// Transitions the specified service to the Retired status if it is in a state that allows retirement.
		/// </summary>
		/// <param name="service">The service instance to transition to the Retired status. If null, the method performs no action.</param>
		/// <param name="connection">DataMiner connection reference.</param>
		/// <returns>The updated service instance with the Retired status if the transition was successful; otherwise, returns the original service instance.</returns>
		public static Models.Service UpdateStatusToRetired(this Models.Service service, IConnection connection)
		{
			if (service == null)
			{
				return service;
			}

			TransitionsEnum transition;
			if (service.Status == StatusesEnum.New)
			{
				transition = TransitionsEnum.New_To_Retired;
			}
			else if (service.Status == StatusesEnum.Designed)
			{
				transition = TransitionsEnum.Designed_To_Retired;
			}
			else if (service.Status == StatusesEnum.Reserved)
			{
				transition = TransitionsEnum.Reserved_To_Retired;
			}
			else if (service.Status == StatusesEnum.Terminated)
			{
				transition = TransitionsEnum.Terminated_To_Retired;
			}
			else
			{
				return service;
			}

			var srvHelper = new DataHelperService(connection);

			connection.GenerateInformationMessage($"[SMS] Status Transition: {service.Name} → {transition}");
			return srvHelper.UpdateState(service, transition);
		}

		/// <summary>
		/// Updates the status of the specified service to Terminated if it is currently Active and has no linked references
		/// still active.
		/// </summary>
		/// <param name="service">The service instance whose status is to be updated.</param>
		/// <param name="engine">Automation engine reference.</param>
		/// <returns>The original service instance if its status is not updated; otherwise, the service instance with its status set to
		/// Terminated.</returns>
		public static Models.Service UpdateStatusToTerminated(this Models.Service service, IEngine engine)
		{
			if (service.ServiceItems.Any(s => s.LinkedReferenceStillActive(engine)))
			{
				return service;
			}

			TransitionsEnum transition;
			if (service.Status == StatusesEnum.Active)
			{
				transition = TransitionsEnum.Active_To_Terminated;
			}
			else
			{
				return service;
			}

			var srvHelper = new DataHelperService(engine.GetUserConnection());

			engine.GenerateInformation($"[SMS] Status Transition: {service.Name} → {transition}");
			service = srvHelper.UpdateState(service, transition);

			var itemHelper = new DataHelperServiceOrderItem(engine.GetUserConnection());
			var orderItem = itemHelper.Read(ServiceOrderItemExposers.ServiceID.Equal(service.ID)
				.AND(ServiceOrderItemExposers.Action.Equal(OrderActionType.Delete.ToString()))).FirstOrDefault();
			orderItem?.UpdateStatusToCompleted(engine.GetUserConnection());

			return service;
		}

		/// <summary>
		/// Updates the status of the specified service when its service items are updated and meet certain criteria.
		/// </summary>
		/// <remarks>The method checks that all service items have a valid, non-empty implementation reference that
		/// can be parsed as a GUID before updating the service status. If the service is in the 'New' state and the criteria
		/// are met, its status is transitioned to 'Designed'. No action is taken if the criteria are not met.</remarks>
		/// <param name="service">The service instance whose status may be updated.</param>
		/// <param name="connection">Automation connection reference.</param>
		/// <returns>The updated service instance if the status was changed; otherwise, the original service instance.</returns>
		public static Models.Service UpdateStatusOnServiceItem(this Models.Service service, IConnection connection)
		{
			if (service?.ServiceItems == null)
			{
				return service;
			}

			if (!service.ServiceItems.All(x => !String.IsNullOrEmpty(x.ImplementationReference) && Guid.TryParse(x.ImplementationReference, out Guid _)))
			{
				return service;
			}

			var srvHelper = new DataHelperService(connection);
			if (service.Status == StatusesEnum.New)
			{
				service = srvHelper.UpdateState(service, TransitionsEnum.New_To_Designed);
			}

			return service;
		}

		/// <summary>
		/// Determines whether the linked reference associated with the specified service item is still active.
		/// </summary>
		/// <param name="serviceItem">The service item whose linked reference is to be checked for activity. The ImplementationReference property must
		/// contain a valid GUID.</param>
		/// <param name="engine">Automation engine reference.</param>
		/// <returns>true if the linked reference is still active; otherwise, false.</returns>
		public static bool LinkedReferenceStillActive(this Models.ServiceItem serviceItem, IEngine engine)
		{
			if (!Guid.TryParse(serviceItem.ImplementationReference, out Guid refId))
			{
				return false;
			}

			if (serviceItem.Type == SlcServicemanagementIds.Enums.ServiceitemtypesEnum.Workflow)
			{
				// Check job
				return LinkedJobStillActive(engine, refId);
			}

			if (serviceItem.Type == SlcServicemanagementIds.Enums.ServiceitemtypesEnum.SRMBooking)
			{
				// Check booking
				return LinkedBookingStillActive(engine, refId);
			}

			if (serviceItem.Type == SlcServicemanagementIds.Enums.ServiceitemtypesEnum.Service)
			{
				// Check linked item
				return LinksStillExist(engine.GetUserConnection(), refId);
			}

			return false;
		}

		private static bool LinkedBookingStillActive(IEngine engine, Guid refId)
		{
			var rm = new ResourceManagerHelper(engine.SendSLNetSingleResponseMessage);
			var reservation = rm.GetReservationInstance(refId);
			if (reservation.StartTimeUTC > DateTime.UtcNow
				&& (reservation.Status == ReservationStatus.Pending || reservation.Status == ReservationStatus.Confirmed))
			{
				rm.RemoveReservationInstances(reservation);
				return false;
			}

			if (reservation.EndTimeUTC < DateTime.UtcNow
				|| reservation.Status == ReservationStatus.Canceled
				|| reservation.Status == ReservationStatus.Ended)
			{
				return false;
			}

			throw new InvalidOperationException($"Booking '{reservation.Name}' still active on the system. Please finish this booking first before removing the service item from the inventory.");
		}

		private static bool LinksStillExist(IConnection connection, Guid refId)
		{
			var linkHelper = new DataHelperLink(connection);
			Skyline.DataMiner.ProjectApi.ServiceManagement.API.Relationship.Models.Link link = linkHelper.Read(LinkExposers.Guid.Equal(refId)).FirstOrDefault();
			if (link == null)
			{
				return false;
			}

			var dataHelper = new DataHelperService(connection);

			FilterElement<Models.Service> filter = new ORFilterElement<Models.Service>();
			if (link.ChildID != null && Guid.TryParse(link.ChildID, out Guid childId))
			{
				filter = filter.OR(ServiceExposers.Guid.Equal(childId));
			}

			if (link.ParentID != null && Guid.TryParse(link.ParentID, out Guid parentId))
			{
				filter = filter.OR(ServiceExposers.Guid.Equal(parentId));
			}

			var services = !filter.isEmpty() ? dataHelper.Read(filter) : new List<Models.Service>();
			if (services.Count > 1)
			{
				return true;
			}

			return false;
		}

		private static bool LinkedJobStillActive(IEngine engine, Guid refId)
		{
			var schedulingHelper = new SchedulingHelper(engine);
			var job = schedulingHelper.GetJob(refId);
			if (job == null)
			{
				return false; // If job doesn't exist, then it can't be active.
			}

			if (job.End < DateTime.UtcNow || job.Start > DateTime.UtcNow)
			{
				var cancelJobInputData = new ExecuteJobAction
				{
					DomJobId = job.Id,
					JobAction = Skyline.DataMiner.Utils.MediaOps.Common.IOData.Scheduling.Scripts.JobHandler.JobAction.CancelJob,
				};

				var cancelOutputData = cancelJobInputData.SendToJobHandler(engine, true);
				if (!cancelOutputData.TraceData.HasSucceeded())
				{
					throw new InvalidOperationException($"Could not cancel Job '{refId}' due to : {JsonConvert.SerializeObject(cancelOutputData.TraceData)}");
				}

				var deleteJobInputData = new ExecuteJobAction
				{
					DomJobId = job.Id,
					JobAction = Skyline.DataMiner.Utils.MediaOps.Common.IOData.Scheduling.Scripts.JobHandler.JobAction.DeleteJob,
				};
				var deleteOutputData = deleteJobInputData.SendToJobHandler(engine, true);

				if (!deleteOutputData.TraceData.HasSucceeded())
				{
					throw new InvalidOperationException($"Could not delete Job '{refId}' due to : {JsonConvert.SerializeObject(deleteOutputData.TraceData)}");
				}

				return false;
			}

			throw new InvalidOperationException($"Job '{refId}' still active on the system. Please finish this job first before removing the service item from the inventory.");
		}
	}
}
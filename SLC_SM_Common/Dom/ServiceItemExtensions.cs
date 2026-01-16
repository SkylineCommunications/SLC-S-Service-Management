namespace Library.Dom
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using DomHelpers.SlcServicemanagement;
	using Newtonsoft.Json;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Messages;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.Relationship;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM;
	using Skyline.DataMiner.Utils.MediaOps.Common.IOData.Scheduling.Scripts.JobHandler;
	using Skyline.DataMiner.Utils.MediaOps.Helpers.Scheduling;
	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement.Models;

	public static class ServiceItemExtensions
	{
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
				return LinksStillExist(engine, refId);
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

		private static bool LinksStillExist(IEngine engine, Guid refId)
		{
			var linkHelper = new DataHelperLink(engine.GetUserConnection());
			Skyline.DataMiner.ProjectApi.ServiceManagement.API.Relationship.Models.Link link = linkHelper.Read(LinkExposers.Guid.Equal(refId)).FirstOrDefault();
			if (link == null)
			{
				return false;
			}

			var dataHelper = new DataHelperService(engine.GetUserConnection());

			FilterElement<Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement.Models.Service> filter = new ORFilterElement<Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement.Models.Service>();
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
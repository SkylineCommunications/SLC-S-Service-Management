namespace SLC_SM_UDAPI_ServiceManagement.Helpers.Inventory
{
	using System;
	using System.Linq;
	using DomHelpers.SlcRelationships;
	using DomHelpers.SlcServicemanagement;
	using DomHelpers.SlcWorkflow;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.Messages;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.Net.ResourceManager.Objects;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.Relationship;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;
	using SLDataGateway.API.Querying;
	using IEngine = Skyline.DataMiner.Automation.IEngine;
	using Link = Skyline.DataMiner.ProjectApi.ServiceManagement.API.Relationship.Models.Link;
	using LinkExposers = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.LinkExposers;

	/// <summary>
	/// Resolves workflow and booking implementations and maintains service-item relationships.
	/// </summary>
	public sealed class ServiceItemImplementations : IServiceItemImplementations
	{
		private readonly IEngine engine;
		private bool? workflowModelExists;
		private bool? relationshipModelExists;

		public ServiceItemImplementations(IEngine engine)
		{
			this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
		}

		/// <inheritdoc/>
		public ServiceItemImplementation Describe(ServiceItem item)
		{
			if (item == null || !Guid.TryParse(item.ImplementationReference, out Guid id) || id == Guid.Empty)
			{
				return null;
			}

			switch (item.Type)
			{
				case SlcServicemanagementIds.Enums.ServiceItemTypesEnum.Workflow:
					return DescribeJob(id);

				case SlcServicemanagementIds.Enums.ServiceItemTypesEnum.SRMBooking:
					return DescribeBooking(id);

				default:
					return null;
			}
		}

		/// <inheritdoc/>
		public void Link(Service service, ServiceItem item)
		{
			if (service == null
				|| item?.Type != SlcServicemanagementIds.Enums.ServiceItemTypesEnum.Service
				|| String.IsNullOrWhiteSpace(item.ImplementationReference)
				|| !RelationshipModelExists())
			{
				return;
			}

			var links = new DataHelperLink(engine.GetUserConnection());
			if (links.Read(LinkExposers.ParentID.Equal(service.Identifier).AND(LinkExposers.ChildID.Equal(item.ImplementationReference))).Any())
			{
				return;
			}

			links.CreateOrUpdate(new Link
			{
				ParentID = service.Identifier,
				ParentName = service.Name,
				ChildID = item.ImplementationReference,
				ChildName = item.DefinitionReference,
			});
		}

		private bool RelationshipModelExists()
		{
			return relationshipModelExists
				?? (relationshipModelExists = engine.DomModelExists(
					SlcRelationshipsIds.ModuleId,
					new[] { SlcRelationshipsIds.Sections.LinkInfo.Id.Id })).Value;
		}

		private ServiceItemImplementation DescribeBooking(Guid id)
		{
			var request = new ManagerStoreStartPagingRequest<ReservationInstance>(ReservationInstanceExposers.ID.Equal(id).ToQuery(), 10);
			var reservation = (engine.SendSLNetSingleResponseMessage(request) as ManagerStorePagingResponse<ReservationInstance>)?.Objects?
				.OfType<ServiceReservationInstance>()
				.FirstOrDefault();
			if (reservation == null)
			{
				return null;
			}

			var serviceState = engine.SendSLNetSingleResponseMessage(new GetServiceStateMessage
			{
				DataMinerID = reservation.ServiceID.DataMinerID,
				ServiceID = reservation.ServiceID.SID,
			}) as ServiceStateEventMessage;

			return new ServiceItemImplementation
			{
				Name = reservation.Name,
				State = reservation.Status.ToString(),
				MonitoringServiceState = serviceState?.Level.ToString(),
			};
		}

		private ServiceItemImplementation DescribeJob(Guid id)
		{
			workflowModelExists = workflowModelExists ?? engine.DomModelExists(SlcWorkflowIds.ModuleId, null);
			if (!workflowModelExists.Value)
			{
				return null;
			}

			var instance = new DomHelper(engine.SendSLNetMessages, SlcWorkflowIds.ModuleId).DomInstances
				.Read(DomInstanceExposers.Id.Equal(id))
				.FirstOrDefault();
			if (instance == null)
			{
				return null;
			}

			return new ServiceItemImplementation
			{
				Name = instance.Name,
				State = new JobsInstance(instance).Status.ToString(),
			};
		}
	}
}

namespace SLC_SM_UDAPI_ServiceManagement.Helpers.Inventory
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using DomHelpers.SlcRelationships;
	using DomHelpers.SlcServicemanagement;
	using DomHelpers.SlcWorkflow;
	using Library.Dom;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.Messages;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.Net.ResourceManager.Objects;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.Relationship;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;
	using SLDataGateway.API.Querying;
	using SLC_SM_Common.Extensions;
	using SLC_SM_UDAPI_ServiceManagement.Models.Inventory;
	using IEngine = Skyline.DataMiner.Automation.IEngine;
	using Link = Skyline.DataMiner.ProjectApi.ServiceManagement.API.Relationship.Models.Link;
	using LinkExposers = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.LinkExposers;

	/// <summary>
	/// Resolves workflow and booking implementations and maintains service-item relationships.
	/// </summary>
	public sealed class ServiceItemImplementations : IServiceItemImplementations
	{
		private const string BookingManagerProtocolName = "Skyline Booking Manager";
		private const string BookingScriptDescription = "Booking Manager Element Info";
		private const int BookingManagerScriptPid = 195;
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

		/// <inheritdoc/>
		public bool IsActive(ServiceItem item)
		{
			if (item == null
				|| item.Type != SlcServicemanagementIds.Enums.ServiceItemTypesEnum.SRMBooking
				|| !Guid.TryParse(item.ImplementationReference, out Guid id)
				|| id == Guid.Empty)
			{
				return false;
			}

			var reservation = ReadReservation(id);
			if (reservation == null)
			{
				return false;
			}

			if (reservation.StartTimeUTC > DateTime.UtcNow
				&& (reservation.Status == ReservationStatus.Pending || reservation.Status == ReservationStatus.Confirmed))
			{
				return false;
			}

			if (reservation.EndTimeUTC < DateTime.UtcNow
				|| reservation.Status == ReservationStatus.Canceled
				|| reservation.Status == ReservationStatus.Ended)
			{
				return false;
			}

			return true;
		}

		/// <inheritdoc/>
		public void Unlink(ServiceItem item)
		{
			if (item == null
				|| item.Type != SlcServicemanagementIds.Enums.ServiceItemTypesEnum.SRMBooking
				|| !Guid.TryParse(item.ImplementationReference, out Guid id)
				|| id == Guid.Empty)
			{
				return;
			}

			var reservation = ReadReservation(id);
			if (reservation != null
				&& reservation.StartTimeUTC > DateTime.UtcNow
				&& (reservation.Status == ReservationStatus.Pending || reservation.Status == ReservationStatus.Confirmed))
			{
				var rm = new ResourceManagerHelper(engine.SendSLNetSingleResponseMessage);
				rm.RemoveReservationInstances(reservation);
			}
		}

		/// <inheritdoc/>
		public ServiceItemOptionsResponse GetOptions()
		{
			var response = new ServiceItemOptionsResponse
			{
				WorkflowAvailable = TryCheckWorkflowAvailable(),
			};

			if (response.WorkflowAvailable)
			{
				response.Workflows = TryGetWorkflows();
			}

			response.BookingScripts = TryGetBookingScripts();
			var bookingScriptSet = new HashSet<string>(response.BookingScripts, StringComparer.OrdinalIgnoreCase);
			response.BookingManagers = TryGetBookingManagers(bookingScriptSet);
			response.SrmAvailable = TryCheckSrmInstalled() || response.BookingManagers.Count > 0;
			return response;
		}

		private bool RelationshipModelExists()
		{
			return relationshipModelExists
				?? (relationshipModelExists = engine.DomModelExists(
					SlcRelationshipsIds.ModuleId,
					new[] { SlcRelationshipsIds.Sections.LinkInfo.Id.Id })).Value;
		}

		private bool TryCheckWorkflowAvailable()
		{
			try
			{
				workflowModelExists = workflowModelExists ?? engine.DomModelExists(
					SlcWorkflowIds.ModuleId,
					new[] { SlcWorkflowIds.Sections.WorkflowInfo.Id.Id });
				return workflowModelExists.Value;
			}
			catch
			{
				return false;
			}
		}

		private List<string> TryGetWorkflows()
		{
			try
			{
				return WorkflowExtensions.GetWorkflows(engine.SendSLNetMessages)
					.Select(w => w.Name)
					.Where(name => !String.IsNullOrWhiteSpace(name))
					.Distinct(StringComparer.OrdinalIgnoreCase)
					.OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
					.ToList();
			}
			catch
			{
				return new List<string>();
			}
		}

		private bool TryCheckSrmInstalled()
		{
			try
			{
				return engine.IsSrmInstalled();
			}
			catch
			{
				return false;
			}
		}

		private List<string> TryGetBookingScripts()
		{
			try
			{
				var scriptsResponse = engine.SendSLNetSingleResponseMessage(new GetInfoMessage
				{
					DataMinerID = -1,
					HostingDataMinerID = -1,
					Type = InfoType.Scripts,
				}) as GetScriptsResponseMessage;

				if (scriptsResponse?.Scripts == null)
				{
					return new List<string>();
				}

				return scriptsResponse.Scripts
					.Where(s => s.IndexOf("Booking", StringComparison.OrdinalIgnoreCase) >= 0)
					.Select(s => engine.SendSLNetSingleResponseMessage(new GetScriptInfoMessage(s)) as GetScriptInfoResponseMessage)
					.Where(info => info?.Parameters != null && info.Parameters.Any(p => p.Description == BookingScriptDescription))
					.Select(info => info.Name)
					.Where(name => !String.IsNullOrWhiteSpace(name))
					.Distinct(StringComparer.OrdinalIgnoreCase)
					.OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
					.ToList();
			}
			catch
			{
				return new List<string>();
			}
		}

		private List<BookingManagerOption> TryGetBookingManagers(HashSet<string> bookingScriptSet)
		{
			try
			{
				var names = (engine.SendSLNetMessage(new GetLiteElementInfo { ProtocolName = BookingManagerProtocolName }) ?? Array.Empty<DMSMessage>())
					.OfType<LiteElementInfoEvent>()
					.Select(e => e.Name)
					.Where(name => !String.IsNullOrWhiteSpace(name))
					.Distinct(StringComparer.OrdinalIgnoreCase)
					.OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
					.ToList();

				var result = new List<BookingManagerOption>(names.Count);
				foreach (var name in names)
				{
					result.Add(new BookingManagerOption
					{
						Name = name,
						Script = TryGetBookingManagerScript(name, bookingScriptSet),
						Bookings = TryGetActiveBookingsForManager(name),
					});
				}

				return result;
			}
			catch
			{
				return new List<BookingManagerOption>();
			}
		}

		private string TryGetBookingManagerScript(string elementName, HashSet<string> bookingScriptSet)
		{
			try
			{
				var element = engine.FindElement(elementName);
				var scriptName = Convert.ToString(element?.GetParameter(BookingManagerScriptPid)) ?? String.Empty;
				if (!String.IsNullOrWhiteSpace(scriptName) && (bookingScriptSet.Count == 0 || bookingScriptSet.Contains(scriptName)))
				{
					return scriptName;
				}
			}
			catch
			{
				// Ignore element lookup errors.
			}

			return String.Empty;
		}

		private List<BookingOption> TryGetActiveBookingsForManager(string bookingManagerName)
		{
			try
			{
				var filter = ReservationInstanceExposers.Properties.DictStringField("Booking Manager").Equal(bookingManagerName)
					.AND(ServiceReservationInstanceExposers.ContributingResourceID.Equal(Guid.Empty))
					.AND(ReservationInstanceExposers.End.GreaterThan(DateTime.UtcNow));

				var page = engine.SendSLNetSingleResponseMessage(new ManagerStoreStartPagingRequest<ReservationInstance>(filter.ToQuery(), 500))
					as ManagerStorePagingResponse<ReservationInstance>;

				return (page?.Objects ?? new List<ReservationInstance>())
					.Where(r => r != null && r.ID != Guid.Empty && !String.IsNullOrWhiteSpace(r.Name))
					.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
					.Select(r => new BookingOption
					{
						Id = r.ID.ToString(),
						Name = r.Name,
					})
					.ToList();
			}
			catch
			{
				return new List<BookingOption>();
			}
		}

		private ReservationInstance ReadReservation(Guid id)
		{
			var request = new ManagerStoreStartPagingRequest<ReservationInstance>(ReservationInstanceExposers.ID.Equal(id).ToQuery(), 10);
			return (engine.SendSLNetSingleResponseMessage(request) as ManagerStorePagingResponse<ReservationInstance>)?.Objects?
				.FirstOrDefault();
		}

		private ServiceItemImplementation DescribeBooking(Guid id)
		{
			var reservation = ReadReservation(id) as ServiceReservationInstance;
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

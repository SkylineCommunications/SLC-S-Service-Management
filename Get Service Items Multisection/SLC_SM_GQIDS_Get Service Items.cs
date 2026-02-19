namespace SLC_SM_GQIDS_Get_Service_Items
{
	// Used to process the Service Items
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using DomHelpers.SlcServicemanagement;
	using DomHelpers.SlcWorkflow;
	using Skyline.DataMiner.Analytics.GenericInterface;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.Messages;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.Net.ResourceManager.Objects;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM;
	using SLC_SM_Common.Extensions;
	using SLDataGateway.API.Querying;

	// Required to mark the interface as a GQI data source
	[GQIMetaData(Name = DataSourceName)]
	public class EventManagerGetMultipleSections : IGQIDataSource, IGQIInputArguments, IGQIOnInit, IGQIUpdateable
	{
		private const string DataSourceName = "Get_ServiceItemsMultipleSections";

		// defining input argument, will be converted to guid by OnArgumentsProcessed
		private readonly GQIStringArgument domIdArg = new GQIStringArgument("DOM ID") { IsRequired = true };

		private readonly Dictionary<Guid, ServiceReservationInstance> _reservations = new Dictionary<Guid, ServiceReservationInstance>();
		private ReservationWatcher _watcher;
		private GQIDMS _dms;
		private IGQILogger _logger;
		private IGQIUpdater _updater;
		private Guid instanceDomId; // variable where input argument will be stored
		private Models.Service _service;

		public GQIColumn[] GetColumns()
		{
			return new GQIColumn[]
			{
				new GQIStringColumn("Actions"), // Actions - used to define buttons without needing concat or rename actions within the query! Required to have real-time updates!!
				new GQIStringColumn("Label"),
				new GQIIntColumn("Service Item ID"),
				new GQIStringColumn("Service Item Type"),
				new GQIStringColumn("Definition Reference"),
				new GQIStringColumn("Service Item Script"),
				new GQIStringColumn("Implementation Reference"),
				new GQIStringColumn("Implementation Reference Name"),
				new GQIStringColumn("Implementation Reference Link"),
				new GQIBooleanColumn("Implementation Reference Has Value"),
				new GQIBooleanColumn("Implementation Reference Name Has Value"),
				new GQIBooleanColumn("Implementation Reference Link Has Value"),
				new GQIStringColumn("Implementation State"),
				new GQIStringColumn("Implementation Reference Custom Link"),
				new GQIBooleanColumn("Implementation Reference Custom Link Has Value"),
				new GQIStringColumn("Monitoring Service State"),
				new GQIStringColumn("Monitoring Service DMA ID/SID"),
				new GQIStringColumn("Log"),
			};
		}

		public GQIArgument[] GetInputArguments()
		{
			return new GQIArgument[]
			{
				domIdArg,
			};
		}

		public GQIPage GetNextPage(GetNextPageInputArgs args)
		{
			return _logger.PerformanceLogger(nameof(GetNextPage), BuildupRows);
		}

		public OnArgumentsProcessedOutputArgs OnArgumentsProcessed(OnArgumentsProcessedInputArgs args)
		{
			// adds the input argument to private variable
			if (!Guid.TryParse(args.GetArgumentValue(domIdArg), out instanceDomId))
			{
				instanceDomId = Guid.Empty;
			}

			return new OnArgumentsProcessedOutputArgs();
		}

		public OnInitOutputArgs OnInit(OnInitInputArgs args)
		{
			_dms = args.DMS;
			_logger = args.Logger;
			_logger.MinimumLogLevel = GQILogLevel.Debug;
			return default;
		}

		public void OnStartUpdates(IGQIUpdater updater)
		{
			_logger.Debug(nameof(OnStartUpdates));
			_updater = updater;

			_watcher = new ReservationWatcher(_dms.GetConnection());
			_watcher.OnChanged += Watcher_OnChanged;
		}

		public void OnStopUpdates()
		{
			_logger.Debug(nameof(OnStopUpdates));
			_updater = null;

			_watcher.OnChanged -= Watcher_OnChanged;
			_watcher.Dispose();
		}

		private GQIRow BuildRow(Models.ServiceItem item)
		{
			var implementationRef = GetImplementationDetails(item.Type, item.ImplementationReference, item.DefinitionReference);
			GQICell[] columns = new[]
				{
					new GQICell { Value = String.Empty }, // Actions - used to define buttons without needing concat or rename actions within the query! Required to have real-time updates!!
					new GQICell { Value = item.Label },
					new GQICell { Value = (int)item.ID },
					new GQICell { Value = SlcServicemanagementIds.Enums.Serviceitemtypes.ToValue(item.Type) },
					new GQICell { Value = item.DefinitionReference ?? String.Empty },
					new GQICell { Value = item.Script ?? String.Empty },
					new GQICell { Value = item.ImplementationReference ?? String.Empty },
					new GQICell { Value = implementationRef.Name },
					new GQICell { Value = implementationRef.ServiceId },
					new GQICell { Value = !String.IsNullOrEmpty(item.ImplementationReference) },
					new GQICell { Value = !String.IsNullOrEmpty(implementationRef.Name) },
					new GQICell { Value = !String.IsNullOrEmpty(implementationRef.ServiceId) },
					new GQICell { Value = implementationRef.State },
					new GQICell { Value = implementationRef.CustomLink },
					new GQICell { Value = !String.IsNullOrEmpty(implementationRef.CustomLink) },
					new GQICell { Value = implementationRef.MonServiceState },
					new GQICell { Value = implementationRef.MonServiceDmaIdSid },
					new GQICell { Value = implementationRef.LogLocation },
				};
			return new GQIRow($"{item.Label}_{item.ID}_{item.Type}", columns);
		}

		private GQIPage BuildupRows()
		{
			try
			{
				return new GQIPage(BuildRows())
				{
					HasNextPage = false,
				};
			}
			catch (Exception e)
			{
				_dms.GenerateInformationMessage($"GQIDS|{DataSourceName}|Exception: {e}");
				_logger.Error($"GQIDS|{DataSourceName}|Exception: {e}");
				return new GQIPage(Enumerable.Empty<GQIRow>().ToArray());
			}
		}

		private ImplementationItemInfo GetImplementationDetails(SlcServicemanagementIds.Enums.ServiceitemtypesEnum type, string referenceId, string definitionReference)
		{
			if (String.IsNullOrEmpty(referenceId) || !Guid.TryParse(referenceId, out Guid id))
			{
				return new ImplementationItemInfo();
			}

			if (type == SlcServicemanagementIds.Enums.ServiceitemtypesEnum.Workflow)
			{
				var inst = new DomHelper(_dms.SendMessages, SlcWorkflowIds.ModuleId).DomInstances.Read(DomInstanceExposers.Id.Equal(id)).FirstOrDefault();
				if (inst == null)
				{
					return new ImplementationItemInfo();
				}

				var jobInst = new JobsInstance(inst);
				return new ImplementationItemInfo
				{
					Name = inst.Name,
					State = jobInst.Status.ToString(),
				};
			}
			else if (type == SlcServicemanagementIds.Enums.ServiceitemtypesEnum.Service)
			{
				var serv = new DataHelperService(_dms.GetConnection()).Read(ServiceExposers.Guid.Equal(id)).FirstOrDefault();
				if (serv == null)
				{
					return new ImplementationItemInfo();
				}

				return new ImplementationItemInfo
				{
					Name = serv.Name,
				};
			}
			else if (type == SlcServicemanagementIds.Enums.ServiceitemtypesEnum.SRMBooking)
			{
				return BuildImplementationInfoForBookingType(definitionReference, id);
			}
			else
			{
				return new ImplementationItemInfo();
			}
		}

		private ImplementationItemInfo BuildImplementationInfoForBookingType(string definitionReference, Guid id)
		{
			ServiceReservationInstance reservation;
			if (_reservations.ContainsKey(id))
			{
				reservation = _reservations[id];
			}
			else
			{
				var request = new ManagerStoreStartPagingRequest<ReservationInstance>(ReservationInstanceExposers.ID.Equal(id).ToQuery(), 10);
				reservation = ((ManagerStorePagingResponse<ReservationInstance>)_dms.SendMessage(request))?.Objects?.OfType<ServiceReservationInstance>().FirstOrDefault();
				if (reservation == null)
				{
					return new ImplementationItemInfo();
				}
			}

			string customReference = null;
			string logLocation = null;
			if (!String.IsNullOrEmpty(definitionReference))
			{
				var liteElementInfoEvent = _dms.SendMessage(new GetElementByNameMessage(definitionReference)) as ElementInfoEventMessage;
				customReference = liteElementInfoEvent?.GetPropertyValue("App Link");
				logLocation = liteElementInfoEvent?.GetPropertyValue("Booking Log Location");
				if (!String.IsNullOrEmpty(logLocation))
				{
					logLocation = $"{logLocation.TrimEnd('/')}/{reservation.Name}.html";
				}
			}

			var serviceInfoEventMessage = _dms.SendMessage(new GetServiceStateMessage { DataMinerID = reservation.ServiceID.DataMinerID, ServiceID = reservation.ServiceID.SID }) as ServiceStateEventMessage;

			_reservations[reservation.ID] = reservation;
			return new ImplementationItemInfo
			{
				Name = reservation.Name,
				ServiceId = reservation.ServiceID.ToString(),
				State = reservation.Status.ToString(),
				CustomLink = customReference ?? String.Empty,
				MonServiceState = serviceInfoEventMessage?.Level.ToString() ?? String.Empty,
				MonServiceDmaIdSid = serviceInfoEventMessage != null ? $"{serviceInfoEventMessage.DataMinerID}/{serviceInfoEventMessage.ServiceID}" : String.Empty,
				LogLocation = logLocation ?? String.Empty,
			};
		}

		private GQIRow[] BuildRows()
		{
			if (instanceDomId == Guid.Empty)
			{
				// return th empty list
				return Array.Empty<GQIRow>();
			}

			_service = _service ?? _logger.PerformanceLogger("Get Service", () => new DataHelperService(_dms.GetConnection()).Read(ServiceExposers.Guid.Equal(instanceDomId)).FirstOrDefault());
			if (_service != null)
			{
				return _logger.PerformanceLogger("Build Service Rows", () => _service.ServiceItems.OrderBy(x => x.ID).Select(BuildRow).ToArray());
			}

			var spec = _logger.PerformanceLogger("Get Specification", () => new DataHelperServiceSpecification(_dms.GetConnection()).Read(ServiceSpecificationExposers.Guid.Equal(instanceDomId)).FirstOrDefault());
			if (spec != null)
			{
				return _logger.PerformanceLogger("Build Specification Rows", () => spec.ServiceItems.OrderBy(x => x.ID).Select(BuildRow).ToArray());
			}

			return Array.Empty<GQIRow>();
		}

		private void Watcher_OnChanged(object sender, ResourceManagerEventMessage e)
		{
			_logger.Debug(nameof(Watcher_OnChanged));

			bool update = false;
			foreach (var instance in e.UpdatedReservationInstances.OfType<ServiceReservationInstance>())
			{
				if (_reservations.ContainsKey(instance.ID))
				{
					_logger.Debug($"{instance.Name}: updated");
					_reservations[instance.ID] = instance;
					update = true;
				}
			}

			foreach (Guid instance in e.DeletedReservationInstances)
			{
				if (_reservations.ContainsKey(instance))
				{
					_logger.Debug($"{instance}: removed");
					_reservations.Remove(instance);
					update = true;
				}
			}

			if (!update)
			{
				return;
			}

			var rows = BuildupRows().Rows;

			foreach (GQIRow row in rows)
			{
				_updater.UpdateRow(row);
			}
		}
	}
}
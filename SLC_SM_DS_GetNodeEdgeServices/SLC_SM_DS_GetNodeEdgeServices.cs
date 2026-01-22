/*
****************************************************************************
*  Copyright (c) 2025,  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

By using this script, you expressly agree with the usage terms and
conditions set out below.
This script and all related materials are protected by copyrights and
other intellectual property rights that exclusively belong
to Skyline Communications.

A user license granted for this script is strictly for personal use only.
This script may not be used in any way by anyone without the prior
written consent of Skyline Communications. Any sublicensing of this
script is forbidden.

Any modifications to this script by the user are only allowed for
personal use and within the intended purpose of the script,
and will remain the sole responsibility of the user.
Skyline Communications will not be responsible for any damages or
malfunctions whatsoever of the script resulting from a modification
or adaptation by the user.

The content of this script is confidential information.
The user hereby agrees to keep this confidential information strictly
secret and confidential and not to disclose or reveal it, in whole
or in part, directly or indirectly to any person, entity, organization
or administration without the prior written consent of
Skyline Communications.

Any inquiries can be addressed to:

	Skyline Communications NV
	Ambachtenstraat 33
	B-8870 Izegem
	Belgium
	Tel.	: +32 51 31 35 69
	Fax.	: +32 51 31 01 29
	E-mail	: info@skyline.be
	Web		: www.skyline.be
	Contact	: Ben Vandenberghe

****************************************************************************
Revision History:

DATE		VERSION		AUTHOR			COMMENTS

08/09/2025	1.0.0.1		RCA, Skyline	Initial version
****************************************************************************
*/

namespace SLCSMDSGetNodeEdgeServices
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using DomHelpers.SlcConfigurations;
	using DomHelpers.SlcServicemanagement;
	using Skyline.DataMiner.Analytics.GenericInterface;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using SLC_SM_Common.Extensions;

	/// <summary>
	///     Represents a data source.
	///     See: https://aka.dataminer.services/gqi-external-data-source for a complete example.
	/// </summary>
	[GQIMetaData(Name = DataSourceName)]
	public sealed class SLCSMDSGetNodeEdgeServices : IGQIDataSource, IGQIOnInit, IGQIInputArguments
	{
		private const string DataSourceName = "SLC_SM_DS_GetNodeEdgeServices";
		private readonly Arguments _arguments = new Arguments();
		private IGQILogger _logger;
		private DomHelper _serviceMangerDomHelper;
		private DomHelper _configurationDomHelper;
		private string _serviceType;
		private GQIDMS _dms;

		public GQIColumn[] GetColumns()
		{
			switch (_arguments.NodeOrEdge)
			{
				case "Node":
					return GetNodeColumns();

				case "Edge":
					return GetEdgeColumns();

				default:
					throw new InvalidOperationException(
						$"Invalid NodeOrEdge argument: '{_arguments.NodeOrEdge}'. Expected 'Node' or 'Edge'.");
			}
		}

		public GQIArgument[] GetInputArguments()
		{
			return _arguments.GetInputArguments();
		}

		public GQIPage GetNextPage(GetNextPageInputArgs args)
		{
			return _logger.PerformanceLogger(nameof(GetNextPage), BuildupRows);
		}

		public OnArgumentsProcessedOutputArgs OnArgumentsProcessed(OnArgumentsProcessedInputArgs args)
		{
			return _arguments.OnArgumentsProcessed(args);
		}

		public OnInitOutputArgs OnInit(OnInitInputArgs args)
		{
			_dms = args.DMS;
			_logger = args.Logger;
			_logger.MinimumLogLevel = GQILogLevel.Debug;
			_serviceMangerDomHelper = new DomHelper(args.DMS.SendMessages, SlcServicemanagementIds.ModuleId);
			_configurationDomHelper = new DomHelper(args.DMS.SendMessages, SlcConfigurationsIds.ModuleId);
			return new OnInitOutputArgs();
		}

		private GQIPage BuildupRows()
		{
			try
			{
				if (!TryGetServiceByDomId(_arguments.DomId, out ServicesInstance service))
				{
					return new GQIPage(Array.Empty<GQIRow>());
				}

				var relatedServices = GetRelatedServices(service);
				var rows = BuildRows(service, relatedServices);

				return new GQIPage(rows);
			}
			catch (Exception e)
			{
				_dms.GenerateInformationMessage($"GQIDS|{DataSourceName}|Exception: {e}");
				_logger.Error($"GQIDS|{DataSourceName}|Exception: {e}");
				return new GQIPage(Enumerable.Empty<GQIRow>().ToArray());
			}
		}

		private GQIRow BuildEdgeRow(ServicesInstance source, ServicesInstance destination)
		{
			return new GQIRow(
				new[]
				{
					new GQICell { Value = source.ID.Id.ToString() },
					new GQICell { Value = destination.ID.Id.ToString() },
				});
		}

		private GQIRow[] BuildEdgeRows(ServicesInstance service, IEnumerable<ServicesInstance> relatedServices)
		{
			switch (_serviceType)
			{
				case "Channel Distribution":
					return relatedServices
						.Select(related => BuildEdgeRow(related, service))
						.ToArray();

				case "Channel Acquisition":
					return relatedServices
						.Select(related => BuildEdgeRow(service, related))
						.ToArray();

				default:
					throw new InvalidOperationException(
						$"Could not determine service type: '{_serviceType}'.");
			}
		}

		private GQIRow BuildNodeRow(ServicesInstance service)
		{
			bool isSelected = service.ID.Id == _arguments.DomId;
			string oppositeServiceType = _serviceType == "Channel Acquisition" ? "Channel Distribution" : "Channel Acquisition";

			return new GQIRow(
				new[]
				{
					new GQICell { Value = service.ID.Id.ToString() },
					new GQICell { Value = service.Name },
					new GQICell { Value = service.ID.Id == _arguments.DomId },
					new GQICell { Value = isSelected ? _serviceType : oppositeServiceType },
				});
		}

		private GQIRow[] BuildNodeRows(ServicesInstance service, IEnumerable<ServicesInstance> relatedServices)
		{
			return new[] { BuildNodeRow(service) }
				.Concat(relatedServices.Select(BuildNodeRow))
				.ToArray();
		}

		private FilterElement<DomInstance> BuildOrFilter(IEnumerable<FilterElement<DomInstance>> filters)
		{
			return filters.Aggregate((f1, f2) => f1.OR(f2));
		}

		private GQIRow[] BuildRows(ServicesInstance service, IEnumerable<ServicesInstance> relatedServices)
		{
			switch (_arguments.NodeOrEdge)
			{
				case "Node":
					return BuildNodeRows(service, relatedServices);

				case "Edge":
					return BuildEdgeRows(service, relatedServices);

				default:
					throw new InvalidOperationException(
						$"Invalid NodeOrEdge argument: '{_arguments.NodeOrEdge}'. Expected 'Node' or 'Edge'.");
			}
		}

		private IEnumerable<ServicesInstance> GetChannelAcquisitionRelatedServices(ServicesInstance service)
		{
			var filter =
				DomInstanceExposers.DomDefinitionId.Equal(SlcServicemanagementIds.Definitions.Services.Id)
					.AND(
						DomInstanceExposers.FieldValues.DomInstanceField(SlcServicemanagementIds.Sections.ServiceItems.ServiceItemType)
							.Equal(SlcServicemanagementIds.Enums.Serviceitemtypes.Service))
					.AND(
						DomInstanceExposers.FieldValues.DomInstanceField(SlcServicemanagementIds.Sections.ServiceItems.ImplementationReference)
							.Equal(service.ID.Id.ToString()));

			return _serviceMangerDomHelper.DomInstances.Read(filter)
				.Select(s => new ServicesInstance(s));
		}

		private IEnumerable<ServicesInstance> GetChannelDistributionRelatedServices(ServicesInstance service)
		{
			var serviceItems = service.ServiceItems
				.Where(si => si.ServiceItemType == SlcServicemanagementIds.Enums.ServiceitemtypesEnum.Service);

			var filters = serviceItems
				.Select(
					si =>
					{
						Guid guid;
						return Guid.TryParse(si.ImplementationReference, out guid)
							? (FilterElement<DomInstance>)DomInstanceExposers.Id.Equal(guid)
							: null;
					})
				.Where(f => f != null)
				.ToList();

			if (!filters.Any())
			{
				return Enumerable.Empty<ServicesInstance>();
			}

			var filter = filters.Aggregate((f1, f2) => f1.OR(f2));

			return _serviceMangerDomHelper.DomInstances.Read(filter)
				.Select(s => new ServicesInstance(s));
		}

		private IEnumerable<ConfigurationParameterValueInstance> GetConfigurationParameterValues(
			IEnumerable<ServiceConfigurationValueInstance> serviceConfigurationValues)
		{
			var filter = BuildOrFilter(
				serviceConfigurationValues
					.Select(
						scv => DomInstanceExposers.Id.Equal(
							scv.ServiceConfigurationValue.ConfigurationParameterValue.Value)));

			var domInstances = _configurationDomHelper.DomInstances.Read(filter);
			return domInstances.Select(cpv => new ConfigurationParameterValueInstance(cpv));
		}

		private IEnumerable<ConfigurationParameterValueInstance> GetConfigurationParameterValuesForService(ServicesInstance service)
		{
			var configurationVersionID = service.ServiceInfo.ServiceConfiguration;

			if (configurationVersionID == null)
			{
				return Enumerable.Empty<ConfigurationParameterValueInstance>();
			}

			var configurationVersion = GetServiceConfigurationVersion(configurationVersionID.Value);

			var paramIds = configurationVersion.ServiceConfigurationInfo?.ServiceConfigurationParameters;
			if (paramIds == null || !paramIds.Any())
			{
				return Enumerable.Empty<ConfigurationParameterValueInstance>();
			}

			var serviceConfigurationValues = GetServiceConfigurationValues(paramIds);
			if (!serviceConfigurationValues.Any())
			{
				return Enumerable.Empty<ConfigurationParameterValueInstance>();
			}

			return GetConfigurationParameterValues(serviceConfigurationValues);
		}

		private GQIColumn[] GetEdgeColumns()
		{
			return new GQIColumn[]
			{
				new GQIStringColumn("Source Id"),
				new GQIStringColumn("Destination Id"),
			};
		}

		private GQIColumn[] GetNodeColumns()
		{
			return new GQIColumn[]
			{
				new GQIStringColumn("Service Id"),
				new GQIStringColumn("Service Name"),
				new GQIBooleanColumn("Selected"),
				new GQIStringColumn("Service Type"),
			};
		}

		private IEnumerable<ServicesInstance> GetRelatedServices(ServicesInstance service)
		{
			_serviceType = GetServiceType(service);

			switch (_serviceType)
			{
				case "Channel Distribution":
					return GetChannelDistributionRelatedServices(service);

				case "Channel Acquisition":
					return GetChannelAcquisitionRelatedServices(service);

				default:
					return Enumerable.Empty<ServicesInstance>();
			}
		}

		private IEnumerable<ServiceConfigurationValueInstance> GetServiceConfigurationValues(IEnumerable<Guid> parameterIds)
		{
			var filter = BuildOrFilter(parameterIds.Select(id => DomInstanceExposers.Id.Equal(id)));
			var domInstances = _serviceMangerDomHelper.DomInstances.Read(filter);
			return domInstances.Select(cp => new ServiceConfigurationValueInstance(cp));
		}

		private ServiceConfigurationVersionInstance GetServiceConfigurationVersion(Guid configurationVersionId)
		{
			var filter = DomInstanceExposers.Id.Equal(configurationVersionId);
			var domInstances = _serviceMangerDomHelper.DomInstances.Read(filter);

			if (domInstances != null || domInstances.Count > 0)
			{
				return new ServiceConfigurationVersionInstance(domInstances[0]);
			}

			return new ServiceConfigurationVersionInstance(configurationVersionId);
		}

		private string GetServiceType(ServicesInstance service)
		{
			var parameterInfo = GetServiceTypeParameterInfo();
			var configurationParameterValues = GetConfigurationParameterValuesForService(service);

			var match = configurationParameterValues
				.SingleOrDefault(
					cpv =>
						cpv.ConfigurationParameterValue.ConfigurationParameterReference == parameterInfo.ID.Id);

			return match != null ? match.ConfigurationParameterValue.StringValue : null;
		}

		private ConfigurationParametersInstance GetServiceTypeParameterInfo()
		{
			var filter = DomInstanceExposers.FieldValues
				.DomInstanceField(SlcConfigurationsIds.Sections.ConfigurationParameterInfo.ParameterName)
				.Equal("Service Type");

			var domInfo = _configurationDomHelper.DomInstances.Read(filter).SingleOrDefault();

			if (domInfo == null)
			{
				throw new InvalidOperationException("Could not find service type configuration parameter info");
			}

			return new ConfigurationParametersInstance(domInfo);
		}

		private bool TryGetServiceByDomId(Guid domId, out ServicesInstance service)
		{
			service = null;

			var domService = _serviceMangerDomHelper.DomInstances
				.Read(DomInstanceExposers.Id.Equal(domId))
				.SingleOrDefault();

			if (domService == null)
			{
				return false;
			}

			service = new ServicesInstance(domService);
			return true;
		}
	}
}
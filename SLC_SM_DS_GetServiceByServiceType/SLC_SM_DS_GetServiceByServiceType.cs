/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE		VERSION		AUTHOR			COMMENTS

11/09/2025	1.0.0.1		RCA, Skyline	Initial version
****************************************************************************
*/

namespace SLCSMDSGetServiceByServiceType
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Skyline.DataMiner.Analytics.GenericInterface;
	using Skyline.DataMiner.Core.DataMinerSystem.Common;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using SLC_SM_Common.Extensions;
	using ConfigurationModels = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;
	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	///     Represents a data source.
	///     See: https://aka.dataminer.services/gqi-external-data-source for a complete example.
	/// </summary>
	[GQIMetaData(Name = DataSourceName)]
	public sealed class SLCSMDSGetServiceByServiceType : IGQIDataSource, IGQIOnInit
	{
		private const string DataSourceName = "SLC_SM_DS_GetServiceByServiceType";

		private static readonly string ConfigParamNameServiceType = "Service Type";
		private static readonly string ConfigParamNameReceptionType = "Reception Type";
		private static readonly string ConfigParamNameChannelId = "Channel ID";
		private static readonly string ConfigParamNameVideoFormat = "Video Format";
		private static readonly string ConfigParamNameDistributionType = "Distribution Type";
		private static readonly string ConfigParamNameRegion = "Region";
		private static readonly string[] ConfigurationParameterNames = new[]
		{
			ConfigParamNameServiceType,
			ConfigParamNameReceptionType,
			ConfigParamNameChannelId,
			ConfigParamNameVideoFormat,
			ConfigParamNameDistributionType,
			ConfigParamNameRegion,
		};

		private IGQILogger _logger;
		private Skyline.DataMiner.Net.IConnection _connection;
		private IDms _dms;
		private GQIDMS _gqiDms;
		private IServiceManagementApiHelper _serviceManagementApiHelper;

		private Guid configID_ServiceType;
		private Guid configID_ReceptionType;
		private Guid configID_ChannelId;
		private Guid configID_VideoFormat;
		private Guid configID_DistType;
		private Guid configID_Region;

		public GQIColumn[] GetColumns()
		{
			return new GQIColumn[]
			{
				new GQIStringColumn("Service Id"),
				new GQIStringColumn("Service Name"),
				new GQIStringColumn("Icon"),
				new GQIStringColumn("Status"),
				new GQIIntColumn("Alarm Level"),
				new GQIStringColumn("Service Type"),
				new GQIStringColumn("Reception Type"),
				new GQIStringColumn("Channel ID"),
				new GQIStringColumn("Video Format"),
				new GQIStringColumn("Distribution Type"),
				new GQIStringColumn("Region"),
			};
		}

		public GQIPage GetNextPage(GetNextPageInputArgs args)
		{
			return _logger.PerformanceLogger(nameof(GetNextPage), BuildupRows);
		}

		public OnInitOutputArgs OnInit(OnInitInputArgs args)
		{
			_logger = args.Logger;
			_logger.MinimumLogLevel = GQILogLevel.Debug;

			_gqiDms = args.DMS;
			_connection = _gqiDms.GetConnection();
			_dms = _connection.GetDms();
			_serviceManagementApiHelper = new ServiceManagementApiHelper(_connection, "Service Inventory");

			return new OnInitOutputArgs();
		}

		private GQIPage BuildupRows()
		{
			try
			{
				var configurationParameters = _serviceManagementApiHelper.ServiceCatalog.ConfigurationParameters
					.Read(new TRUEFilterElement<ConfigurationModels.ConfigurationParameter>())
					.Where(parameter => ConfigurationParameterNames.Contains(parameter.Name))
					.ToDictionary(p => p.Name, p => Guid.Parse(p.Identifier));

				configurationParameters.TryGetValue(ConfigParamNameServiceType, out configID_ServiceType);
				configurationParameters.TryGetValue(ConfigParamNameReceptionType, out configID_ReceptionType);
				configurationParameters.TryGetValue(ConfigParamNameChannelId, out configID_ChannelId);
				configurationParameters.TryGetValue(ConfigParamNameVideoFormat, out configID_VideoFormat);
				configurationParameters.TryGetValue(ConfigParamNameDistributionType, out configID_DistType);
				configurationParameters.TryGetValue(ConfigParamNameRegion, out configID_Region);

				var services = _serviceManagementApiHelper.ServiceInventory.Services
					.Read(new TRUEFilterElement<Models.Service>())
					.Where(service => ServiceMatchesCharacteristic(service, configurationParameters.Keys))
					.ToList();

				return new GQIPage(
					services
						.Select(service => BuildRow(service))
						.ToArray());
			}
			catch (Exception e)
			{
				_gqiDms.GenerateInformationMessage($"GQIDS|{DataSourceName}|Exception: {e}");
				_logger.Error($"GQIDS|{DataSourceName}|Exception: {e}");
				return new GQIPage(Enumerable.Empty<GQIRow>().ToArray());
			}
		}

		private GQIRow BuildRow(Models.Service service)
		{
			int alarmLevel = _logger.PerformanceLogger("Get Alarm Level", () => (int)TryGetAlarmLevel(service));

			var configs = GetConfigurationValuesByParameterId(service);

			return new GQIRow(
				new[]
				{
					new GQICell { Value = service.Identifier ?? String.Empty },
					new GQICell { Value = service.Name },
					new GQICell { Value = service.Icon ?? String.Empty },
					new GQICell { Value = service.Status.ToString() },
					new GQICell { Value = alarmLevel },
					new GQICell { Value = configs.TryGetValue(configID_ServiceType, out string st) ? st : String.Empty },
					new GQICell { Value = configs.TryGetValue(configID_ReceptionType, out string rt) ? rt : String.Empty },
					new GQICell { Value = configs.TryGetValue(configID_ChannelId, out string ci) ? ci : String.Empty },
					new GQICell { Value = configs.TryGetValue(configID_VideoFormat, out string vf) ? vf : String.Empty },
					new GQICell { Value = configs.TryGetValue(configID_DistType, out string dt) ? dt : String.Empty },
					new GQICell { Value = configs.TryGetValue(configID_Region, out string r) ? r : String.Empty },
				});
		}

		private AlarmLevel TryGetAlarmLevel(Models.Service service)
		{
			if (_dms.ServiceExistsSafe(service.Name, out IDmsService srv))
			{
				return srv.GetState().Level;
			}

			return AlarmLevel.Undefined;
		}

		private Dictionary<Guid, string> GetConfigurationValuesByParameterId(Models.Service service)
		{
			var configurationVersionId = service.ServiceConfigurationId.Identifier;

			if (String.IsNullOrWhiteSpace(configurationVersionId))
			{
				return new Dictionary<Guid, string>();
			}

			var configurationVersion = _serviceManagementApiHelper.ServiceInventory.ServiceConfigurationVersions
				.Read(ServiceConfigurationVersionExposers.Identifier.Equal(configurationVersionId))
				.FirstOrDefault();

			if (configurationVersion?.Parameters == null || configurationVersion.Parameters.Count == 0)
			{
				return new Dictionary<Guid, string>();
			}

			var configurationValueIds = configurationVersion.Parameters
				.Where(parameter => !String.IsNullOrWhiteSpace(parameter.Identifier))
				.Select(parameter => parameter.Identifier)
				.Distinct()
				.ToHashSet();

			if (configurationValueIds.Count == 0)
			{
				return new Dictionary<Guid, string>();
			}

			var serviceConfigurationValues = _serviceManagementApiHelper.ServiceInventory.ServiceConfigurationValues
				.Read(new TRUEFilterElement<Models.ServiceConfigurationValue>())
				.Where(value => configurationValueIds.Contains(value.Identifier))
				.ToList();

			var configurationParameterValueIds = serviceConfigurationValues
				.Where(value => value.ConfigurationParameterId != null && !String.IsNullOrWhiteSpace(value.ConfigurationParameterId.Identifier))
				.Select(value => value.ConfigurationParameterId.Identifier)
				.Distinct()
				.ToHashSet();

			if (configurationParameterValueIds.Count == 0)
			{
				return new Dictionary<Guid, string>();
			}

			return _serviceManagementApiHelper.ServiceCatalog.ConfigurationParameterValues
				.Read(new TRUEFilterElement<ConfigurationModels.ConfigurationParameterValue>())
				.Where(value => value.ConfigurationParameterId != null
					&& Guid.TryParse(value.ConfigurationParameterId.Identifier, out Guid parameterId)
					&& configurationParameterValueIds.Contains(value.Identifier))
				.GroupBy(value => Guid.Parse(value.ConfigurationParameterId.Identifier))
				.ToDictionary(group => group.Key, group => group.First().StringValue);
		}

		private bool ServiceMatchesCharacteristic(Models.Service service, IEnumerable<string> characteristicNames)
		{
			if (service?.ServiceID == null)
			{
				return false;
			}

			var configValues = GetConfigurationValuesByParameterId(service);
			if (configValues.Count == 0)
			{
				return false;
			}

			return characteristicNames.Any(name =>
				(String.Equals(name, ConfigParamNameServiceType, StringComparison.OrdinalIgnoreCase) && configValues.ContainsKey(configID_ServiceType))
				|| (String.Equals(name, ConfigParamNameReceptionType, StringComparison.OrdinalIgnoreCase) && configValues.ContainsKey(configID_ReceptionType))
				|| (String.Equals(name, ConfigParamNameChannelId, StringComparison.OrdinalIgnoreCase) && configValues.ContainsKey(configID_ChannelId))
				|| (String.Equals(name, ConfigParamNameVideoFormat, StringComparison.OrdinalIgnoreCase) && configValues.ContainsKey(configID_VideoFormat))
				|| (String.Equals(name, ConfigParamNameDistributionType, StringComparison.OrdinalIgnoreCase) && configValues.ContainsKey(configID_DistType))
				|| (String.Equals(name, ConfigParamNameRegion, StringComparison.OrdinalIgnoreCase) && configValues.ContainsKey(configID_Region)));
		}
	}
}
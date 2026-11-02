namespace SLCSMDSGetServicesByCharacteristic
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using DomHelpers.SlcServicemanagement;
	using Skyline.DataMiner.Analytics.GenericInterface;
	using Skyline.DataMiner.Core.DataMinerSystem.Common;
	using Skyline.DataMiner.Net;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
		using Skyline.DataMiner.Net.Messages.SLDataGateway;
		using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using SLC_SM_Common.Extensions;
		using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
		using ConfigurationModels = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;

	/// <summary>
	///     Represents a data source.
	///     See: https://aka.dataminer.services/gqi-external-data-source for a complete example.
	/// </summary>
	[GQIMetaData(Name = DataSourceName)]
	public sealed class SLCSMDSGetServicesByCharacteristic : IGQIDataSource, IGQIInputArguments, IGQIOnInit
	{
		private const string DataSourceName = "SLC_SM_DS_GetServicesByCharacteristic";
		private readonly GQIStringArgument serviceCharacteristicArg = new GQIStringArgument("Service Characteristic") { IsRequired = true };
		private readonly GQIStringArgument serviceCharacteristicValueArg = new GQIStringArgument("Service Characteristic Value") { IsRequired = true };
		private string _serviceCharacteristic;
		private string _serviceCharacteristicValue;
		private IServiceManagementApiHelper _serviceManagementApiHelper;
		private GQIDMS _gqiDms;
		private IDms _dms;
		private IGQILogger _logger;

		public GQIColumn[] GetColumns()
		{
			return new GQIColumn[]
			{
				new GQIStringColumn("DOM ID"),
				new GQIStringColumn("Service ID"),
				new GQIStringColumn("Service Name"),
				new GQIDateTimeColumn("Service Start"),
				new GQIDateTimeColumn("Service End"),
				new GQIStringColumn("Service Category"),
				new GQIStringColumn("Service Logo"),
				new GQIStringColumn("Service Specification"),
				new GQIIntColumn("Alarm Level"),
			};
		}

		public GQIArgument[] GetInputArguments()
		{
			return new GQIArgument[]
			{
				serviceCharacteristicArg,
				serviceCharacteristicValueArg,
			};
		}

		public GQIPage GetNextPage(GetNextPageInputArgs args)
		{
			return _logger.PerformanceLogger(nameof(GetNextPage), BuildupRows);
		}

		public OnArgumentsProcessedOutputArgs OnArgumentsProcessed(OnArgumentsProcessedInputArgs args)
		{
			// adds the input argument to private variable
			_serviceCharacteristic = args.GetArgumentValue(serviceCharacteristicArg);
			_serviceCharacteristicValue = args.GetArgumentValue(serviceCharacteristicValueArg);

			return new OnArgumentsProcessedOutputArgs();
		}

		public OnInitOutputArgs OnInit(OnInitInputArgs args)
		{
			_gqiDms = args.DMS;
			_logger = args.Logger;
			_logger.MinimumLogLevel = GQILogLevel.Debug;

			IConnection connection = _gqiDms.GetConnection();
			_dms = connection.GetDms();
			_serviceManagementApiHelper = new ServiceManagementApiHelper(connection, "Service Inventory");

			return default;
		}

		private GQIRow[] BuildPage()
		{
			if (_serviceCharacteristic != null && _serviceCharacteristicValue == null)
			{
				// characteristic provided but no value
				return Array.Empty<GQIRow>();
			}

			List<Models.Service> returnedServices = _logger.PerformanceLogger(
				"Get Services For Characteristic",
				() =>
					_serviceCharacteristic == null && _serviceCharacteristicValue == null
						? _serviceManagementApiHelper.ServiceInventory.Services.Read(new TRUEFilterElement<Models.Service>()).ToList()
						: GetServicesByCharacteristic(_serviceCharacteristic, _serviceCharacteristicValue));

			return returnedServices.Select(BuildRow).ToArray();
		}

		private GQIRow BuildRow(Models.Service service)
		{
			if (!Guid.TryParse(service.Identifier, out var domId))
			{
				domId = Guid.Empty;
			}

			var domInstanceId = new DomInstanceId(domId) { ModuleId = SlcServicemanagementIds.ModuleId };
			var objectRefMetadata = new ObjectRefMetadata { Object = domInstanceId };

			var alarmLevel = _logger.PerformanceLogger("Get Alarm Level", () => TryGetAlarmLevel(service));

			return new GQIRow(
					new[]
					{
						new GQICell { Value = service.Identifier ?? String.Empty },
						new GQICell { Value = service.ServiceID ?? String.Empty },
						new GQICell { Value = service.Name ?? String.Empty },
						new GQICell { Value = service.StartTime?.ToUniversalTime() },
						new GQICell { Value = service.EndTime?.ToUniversalTime() },
						new GQICell { Value = GetServiceCategoryName(service.CategoryId) },
						new GQICell { Value = service.Icon ?? String.Empty },
						new GQICell { Value = service.ServiceSpecificationId.Identifier ?? String.Empty },
						new GQICell { Value = (int)alarmLevel },
					})
			{ Metadata = new GenIfRowMetadata(new[] { objectRefMetadata }) };
		}

		private List<Models.Service> GetServicesByCharacteristic(string characteristicName, string characteristicValue)
		{
			if (String.IsNullOrWhiteSpace(characteristicName) || String.IsNullOrWhiteSpace(characteristicValue))
			{
				return new List<Models.Service>();
			}

			var parameter = _serviceManagementApiHelper.ServiceCatalog.ConfigurationParameters
				.Read(new TRUEFilterElement<ConfigurationModels.ConfigurationParameter>())
				.FirstOrDefault(p => String.Equals(p.Name, characteristicName, StringComparison.OrdinalIgnoreCase));

			if (parameter == null || !Guid.TryParse(parameter.Identifier, out var parameterId))
			{
				return new List<Models.Service>();
			}

			return _serviceManagementApiHelper.ServiceInventory.Services
				.Read(new TRUEFilterElement<Models.Service>())
				.Where(service => ServiceHasCharacteristic(service, parameterId, characteristicValue))
				.ToList();
		}

		private bool ServiceHasCharacteristic(Models.Service service, Guid parameterId, string characteristicValue)
		{
			var configurationValues = GetConfigurationValuesByParameterId(service);
			if (!configurationValues.TryGetValue(parameterId, out var value))
			{
				return false;
			}

			return String.Equals(value, characteristicValue, StringComparison.OrdinalIgnoreCase);
		}

		private Dictionary<Guid, string> GetConfigurationValuesByParameterId(Models.Service service)
		{
			var configurationVersionId = service.ServiceConfigurationId.Identifier;
			if (String.IsNullOrWhiteSpace(configurationVersionId))
			{
				return new Dictionary<Guid, string>();
			}

			var configurationVersion = _serviceManagementApiHelper.ServiceInventory.ServiceConfigurationVersions
				.Read(Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement.ServiceConfigurationVersionExposers.Identifier.Equal(configurationVersionId))
				.FirstOrDefault();

			if (configurationVersion?.Parameters == null || configurationVersion.Parameters.Count == 0)
			{
				return new Dictionary<Guid, string>();
			}

			var configurationValueIds = configurationVersion.Parameters
				.Where(parameter => !String.IsNullOrWhiteSpace(parameter.Identifier))
				.Select(parameter => parameter.Identifier)
				.Distinct()
				.ToList();

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
				.ToList();

			if (configurationParameterValueIds.Count == 0)
			{
				return new Dictionary<Guid, string>();
			}

			return _serviceManagementApiHelper.ServiceCatalog.ConfigurationParameterValues
				.Read(new TRUEFilterElement<ConfigurationModels.ConfigurationParameterValue>())
				.Where(value => value.ConfigurationParameterId != null
					&& Guid.TryParse(value.ConfigurationParameterId.Identifier, out Guid configurationParameterId)
					&& configurationParameterValueIds.Contains(value.Identifier))
				.GroupBy(value => Guid.Parse(value.ConfigurationParameterId.Identifier))
				.ToDictionary(group => group.Key, group => group.First().StringValue);
		}

		private string GetServiceCategoryName(Skyline.DataMiner.SDM.SdmObjectReference<Models.ServiceCategory> categoryId)
		{
			if (String.IsNullOrWhiteSpace(categoryId.Identifier))
			{
				return String.Empty;
			}

			var category = _serviceManagementApiHelper.ServiceCatalog.ServiceCategories
				.Read(Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement.ServiceCategoryExposers.Identifier.Equal(categoryId.Identifier))
				.FirstOrDefault();

			return category?.Name ?? String.Empty;
		}

		private GQIPage BuildupRows()
		{
			try
			{
				return new GQIPage(BuildPage())
				{
					HasNextPage = false,
				};
			}
			catch (Exception e)
			{
				_gqiDms.GenerateInformationMessage($"GQIDS|{DataSourceName}|Exception: {e}");
				_logger.Error($"GQIDS|{DataSourceName}|Exception: {e}");
				return new GQIPage(Enumerable.Empty<GQIRow>().ToArray());
			}
		}

		private AlarmLevel TryGetAlarmLevel(Models.Service service)
		{
			if (_dms.ServiceExistsSafe(service.Name, out IDmsService srv))
			{
				return srv.GetState().Level;
			}

			return AlarmLevel.Undefined;
		}
	}
}
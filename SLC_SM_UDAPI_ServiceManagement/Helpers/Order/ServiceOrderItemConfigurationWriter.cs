namespace SLC_SM_UDAPI_ServiceManagement.Helpers.Order
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using Skyline.DataMiner.SDM;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;
	using SLC_SM_UDAPI_ServiceManagement.Exceptions;
	using SLC_SM_UDAPI_ServiceManagement.Models.Inventory;
	using SLC_SM_UDAPI_ServiceManagement.Models.Order;

	internal sealed class ServiceOrderItemConfigurationWriter
	{
		private readonly IServiceManagementApiHelper _api;

		public ServiceOrderItemConfigurationWriter(IServiceManagementApiHelper api)
		{
			_api = api ?? throw new ArgumentNullException(nameof(api));
		}

		public void Save(ServiceOrderItem item, ServiceOrderItemConfigurationRequest request)
		{
			if (item == null)
			{
				throw new ArgumentNullException(nameof(item));
			}

			if (request == null)
			{
				throw new InvalidConfigurationException("A request body is required.");
			}

			var ownership = ReadOwnership(item);
			if (request.DeleteAll)
			{
				DeleteOwnedConfigurations(ownership.Configurations.Values, ownership);
				item.ServiceInfo = item.ServiceInfo ?? new ServiceOrderItemServiceInfo();
				item.ServiceInfo.Configurations = new List<SdmObjectReference<ServiceOrderItemConfigurationValue>>();
				_api.ServiceOrder.ServiceOrderItems.Update(item);
				return;
			}

			var definitions = _api.ServiceCatalog.ConfigurationParameters
				.Read(new TRUEFilterElement<ConfigurationParameter>())
				.ToDictionary(parameter => parameter.Identifier, StringComparer.OrdinalIgnoreCase);
			var parameters = request.Parameters ?? new List<ServiceConfigurationParameterRequest>();
			var deleteIds = ValidateRequest(request, parameters, definitions, ownership);

			DeleteOwnedConfigurations(
				deleteIds.Select(id => ownership.Configurations[id]),
				ownership);

			var references = new List<SdmObjectReference<ServiceOrderItemConfigurationValue>>();
			foreach (var parameter in parameters)
			{
				var definition = definitions[parameter.ParameterId];
				var value = String.IsNullOrWhiteSpace(parameter.ParameterValueId)
					? new ConfigurationParameterValue { Identifier = Guid.NewGuid().ToString() }
					: ownership.ParameterValues[parameter.ParameterValueId];

				try
				{
					value.ApplyConfigurationValue(definition, parameter.Value, parameter.Mandatory);
				}
				catch (InvalidOperationException ex)
				{
					throw new InvalidConfigurationException(ex.Message, ex);
				}

				value.ValueFixed = parameter.Fixed;
				_api.ServiceCatalog.ConfigurationParameterValues.CreateOrUpdate(new[] { value });

				var configuration = String.IsNullOrWhiteSpace(parameter.ConfigurationId)
					? new ServiceOrderItemConfigurationValue { Identifier = Guid.NewGuid().ToString() }
					: ownership.Configurations[parameter.ConfigurationId];
				configuration.ConfigurationParameterValueId = new SdmObjectReference<ConfigurationParameterValue>(value.Identifier);
				configuration.Mandatory = parameter.Mandatory;
				_api.ServiceOrder.ServiceOrderItemConfigurationValues.CreateOrUpdate(new[] { configuration });
				references.Add(new SdmObjectReference<ServiceOrderItemConfigurationValue>(configuration.Identifier));
			}

			item.ServiceInfo = item.ServiceInfo ?? new ServiceOrderItemServiceInfo();
			item.ServiceInfo.Configurations = references;
			_api.ServiceOrder.ServiceOrderItems.Update(item);
		}

		private Ownership ReadOwnership(ServiceOrderItem item)
		{
			var ownership = new Ownership();
			foreach (var reference in item.ServiceInfo?.Configurations ?? new List<SdmObjectReference<ServiceOrderItemConfigurationValue>>())
			{
				string configurationId = reference.Identifier;
				if (String.IsNullOrWhiteSpace(configurationId) || !ownership.ConfigurationIds.Add(configurationId))
				{
					continue;
				}

				var configuration = _api.ServiceOrder.ServiceOrderItemConfigurationValues
					.Read(ServiceOrderItemConfigurationValueExposers.Identifier.Equal(configurationId))
					.FirstOrDefault();
				if (configuration == null)
				{
					continue;
				}

				ownership.Configurations.Add(configurationId, configuration);
				string parameterValueId = configuration.ConfigurationParameterValueId.Identifier;
				if (String.IsNullOrWhiteSpace(parameterValueId) || !ownership.ParameterValueIds.Add(parameterValueId))
				{
					continue;
				}

				var parameterValue = _api.ServiceCatalog.ConfigurationParameterValues
					.Read(ConfigurationParameterValueExposers.Identifier.Equal(parameterValueId))
					.FirstOrDefault();
				if (parameterValue != null)
				{
					ownership.ParameterValues.Add(parameterValueId, parameterValue);
				}
			}

			return ownership;
		}

		private static HashSet<string> ValidateRequest(
			ServiceOrderItemConfigurationRequest request,
			IEnumerable<ServiceConfigurationParameterRequest> parameters,
			IReadOnlyDictionary<string, ConfigurationParameter> definitions,
			Ownership ownership)
		{
			var deletedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (string id in request.DeleteConfigurationIds ?? new List<string>())
			{
				if (String.IsNullOrWhiteSpace(id))
				{
					continue;
				}

				ValidateOwnedId(id, ownership.ConfigurationIds, ownership.Configurations.ContainsKey(id), "configuration");
				deletedIds.Add(id);
			}

			var parameterValueIdsToDelete = new HashSet<string>(
				deletedIds
					.Select(id => ownership.Configurations[id].ConfigurationParameterValueId.Identifier)
					.Where(id => !String.IsNullOrWhiteSpace(id)),
				StringComparer.OrdinalIgnoreCase);
			var usedConfigurationIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var usedParameterValueIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			foreach (var parameter in parameters)
			{
				if (parameter == null)
				{
					throw new InvalidConfigurationException("A configuration parameter entry cannot be null.");
				}

				if (String.IsNullOrWhiteSpace(parameter.ParameterId) || !definitions.TryGetValue(parameter.ParameterId, out var definition))
				{
					throw new InvalidConfigurationException($"Configuration parameter '{parameter.ParameterId}' was not found.");
				}

				if (!String.IsNullOrWhiteSpace(parameter.ConfigurationId))
				{
					ValidateOwnedId(parameter.ConfigurationId, ownership.ConfigurationIds, ownership.Configurations.ContainsKey(parameter.ConfigurationId), "configuration");
					if (deletedIds.Contains(parameter.ConfigurationId))
					{
						throw new InvalidConfigurationException($"Configuration '{parameter.ConfigurationId}' cannot be deleted and updated in the same request.");
					}

					if (!usedConfigurationIds.Add(parameter.ConfigurationId))
					{
						throw new InvalidConfigurationException($"Configuration '{parameter.ConfigurationId}' is referenced more than once.");
					}
				}

				if (!String.IsNullOrWhiteSpace(parameter.ParameterValueId))
				{
					ValidateOwnedId(parameter.ParameterValueId, ownership.ParameterValueIds, ownership.ParameterValues.ContainsKey(parameter.ParameterValueId), "configuration parameter value");
					if (parameterValueIdsToDelete.Contains(parameter.ParameterValueId))
					{
						throw new InvalidConfigurationException($"Configuration parameter value '{parameter.ParameterValueId}' cannot be deleted and updated in the same request.");
					}

					if (!usedParameterValueIds.Add(parameter.ParameterValueId))
					{
						throw new InvalidConfigurationException($"Configuration parameter value '{parameter.ParameterValueId}' is referenced more than once.");
					}

					var value = ownership.ParameterValues[parameter.ParameterValueId];
					if (!String.Equals(value.ConfigurationParameterId.Identifier, definition.Identifier, StringComparison.OrdinalIgnoreCase))
					{
						throw new InvalidConfigurationException($"Configuration parameter value '{parameter.ParameterValueId}' does not belong to parameter '{parameter.ParameterId}'.");
					}
				}

				if (!String.IsNullOrWhiteSpace(parameter.ConfigurationId))
				{
					string currentValueId = ownership.Configurations[parameter.ConfigurationId].ConfigurationParameterValueId.Identifier;
					if (!String.IsNullOrWhiteSpace(parameter.ParameterValueId)
						&& !String.Equals(currentValueId, parameter.ParameterValueId, StringComparison.OrdinalIgnoreCase))
					{
						throw new InvalidConfigurationException($"Configuration '{parameter.ConfigurationId}' does not reference parameter value '{parameter.ParameterValueId}'.");
					}
				}

				try
				{
					var candidate = new ConfigurationParameterValue { Identifier = Guid.NewGuid().ToString() };
					candidate.ApplyConfigurationValue(definition, parameter.Value, parameter.Mandatory);
				}
				catch (InvalidOperationException ex)
				{
					throw new InvalidConfigurationException(ex.Message, ex);
				}
			}

			return deletedIds;
		}

		private static void ValidateOwnedId(string id, ISet<string> ownedIds, bool exists, string kind)
		{
			if (!ownedIds.Contains(id))
			{
				throw new InvalidConfigurationException($"The requested {kind} '{id}' is not assigned to the target service order item.");
			}

			if (!exists)
			{
				throw new InvalidConfigurationException($"The {kind} '{id}' assigned to the target service order item could not be found.");
			}
		}

		private void DeleteOwnedConfigurations(IEnumerable<ServiceOrderItemConfigurationValue> configurations, Ownership ownership)
		{
			var configurationsToDelete = configurations.ToList();
			if (configurationsToDelete.Count == 0)
			{
				return;
			}

			var deletedConfigurationIds = new HashSet<string>(
				configurationsToDelete.Select(configuration => configuration.Identifier),
				StringComparer.OrdinalIgnoreCase);
			var parameterValueIds = new HashSet<string>(
				configurationsToDelete
					.Select(configuration => configuration.ConfigurationParameterValueId.Identifier)
					.Where(id => !String.IsNullOrWhiteSpace(id)),
				StringComparer.OrdinalIgnoreCase);
			var remainingParameterValueIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			if (parameterValueIds.Count > 0)
			{
				foreach (var configuration in _api.ServiceOrder.ServiceOrderItemConfigurationValues
					.Read(new TRUEFilterElement<ServiceOrderItemConfigurationValue>()))
				{
					if (deletedConfigurationIds.Contains(configuration.Identifier))
					{
						continue;
					}

					string parameterValueId = configuration.ConfigurationParameterValueId.Identifier;
					if (!String.IsNullOrWhiteSpace(parameterValueId))
					{
						remainingParameterValueIds.Add(parameterValueId);
					}
				}
			}

			foreach (var configuration in configurationsToDelete)
			{
				_api.ServiceOrder.ServiceOrderItemConfigurationValues.Delete(configuration);
			}

			foreach (string parameterValueId in parameterValueIds)
			{
				if (!remainingParameterValueIds.Contains(parameterValueId)
					&& ownership.ParameterValues.TryGetValue(parameterValueId, out var parameterValue))
				{
					_api.ServiceCatalog.ConfigurationParameterValues.Delete(parameterValue);
				}
			}
		}

		private sealed class Ownership
		{
			public HashSet<string> ConfigurationIds { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			public Dictionary<string, ServiceOrderItemConfigurationValue> Configurations { get; } =
				new Dictionary<string, ServiceOrderItemConfigurationValue>(StringComparer.OrdinalIgnoreCase);

			public HashSet<string> ParameterValueIds { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			public Dictionary<string, ConfigurationParameterValue> ParameterValues { get; } =
				new Dictionary<string, ConfigurationParameterValue>(StringComparer.OrdinalIgnoreCase);
		}
	}
}

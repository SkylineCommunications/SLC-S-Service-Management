namespace SLC_SM_RT_ScheduledConfigurationActivation.TestCases
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;

	using DomHelpers.SlcConfigurations;

	using Newtonsoft.Json;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using Skyline.DataMiner.SDM;

	using ConfigModels = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;
	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Owns the fixtures of the test and the state shared between its test cases.
	/// </summary>
	internal sealed class ScheduledConfigurationActivationTestData
	{
		public const string CurrentValue = "RT current value";

		public const string PlannedValue = "RT planned value";

		private const string CreateServiceInventoryScript = "SLC_SM_Create Service Inventory Item";

		private readonly IEngine engine;

		private readonly HashSet<string> versionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		private readonly HashSet<string> configurationValueIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		private readonly HashSet<string> parameterValueIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		public ScheduledConfigurationActivationTestData(IEngine engine)
		{
			this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
			InventoryApi = engine.GetUserConnection().GetServiceManagementApiHelper("Service Inventory");
			OrderingApi = engine.GetUserConnection().GetServiceManagementApiHelper("Service Ordering");

			DateTime now = DateTime.UtcNow;
			DateTime nowSeconds = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second, DateTimeKind.Utc);
			CurrentStart = nowSeconds.AddDays(-1);
			PlannedStart = nowSeconds.AddDays(7);
			CurrentVersionName = $"RT_ScheduledActivation_Current_{Guid.NewGuid()}";
			PlannedVersionName = $"RT_ScheduledActivation_Planned_{Guid.NewGuid()}";
			ParameterLabel = $"RT_ScheduledActivation_Value_{Guid.NewGuid()}";
		}

		public IServiceManagementApiHelper InventoryApi { get; }

		public IServiceManagementApiHelper OrderingApi { get; }

		public DateTime CurrentStart { get; }

		/// <summary>
		/// Gets the start of the planned version, which is also the end of the current version.
		/// </summary>
		public DateTime PlannedStart { get; }

		public string CurrentVersionName { get; }

		public string PlannedVersionName { get; }

		public string ParameterLabel { get; }

		public string SpecificationId { get; private set; } = String.Empty;

		public string OrderId { get; private set; } = String.Empty;

		public string OrderItemId { get; private set; } = String.Empty;

		public string ServiceId { get; private set; } = String.Empty;

		public string ConfigurationParameterId { get; private set; } = String.Empty;

		public string CurrentVersionId { get; private set; } = String.Empty;

		public string CurrentConfigurationValueId { get; private set; } = String.Empty;

		public string CurrentParameterValueId { get; private set; } = String.Empty;

		public string PlannedVersionId { get; private set; } = String.Empty;

		public string PlannedConfigurationValueId { get; private set; } = String.Empty;

		public string PlannedParameterValueId { get; private set; } = String.Empty;

		/// <summary>
		/// Converts a persisted date to UTC; dates without a kind are stored as UTC.
		/// </summary>
		/// <param name="value">The persisted date.</param>
		/// <returns>The date in UTC, or <c>null</c>.</returns>
		public static DateTime? ToUtc(DateTime? value)
		{
			if (!value.HasValue)
			{
				return null;
			}

			switch (value.Value.Kind)
			{
				case DateTimeKind.Local:
					return value.Value.ToUniversalTime();
				case DateTimeKind.Unspecified:
					return DateTime.SpecifyKind(value.Value, DateTimeKind.Utc);
				default:
					return value.Value;
			}
		}

		/// <summary>
		/// Creates the specification, order and order item fixtures, materialises the service under test
		/// and creates the configuration parameter used by the versions.
		/// </summary>
		public void CreateFixtures()
		{
			var specification = new Models.ServiceSpecification
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = $"RT_ScheduledActivation_Spec_{Guid.NewGuid()}",
				Description = "Temporary fixture for regression testing.",
				ServiceItems = new List<Models.ServiceItem> { new Models.ServiceItem() },
				ServiceItemsRelationships = new List<Models.ServiceItemRelationship> { new Models.ServiceItemRelationship() },
			};

			OrderingApi.ServiceCatalog.ServiceSpecifications.Create(specification);
			SpecificationId = specification.Identifier;

			Models.ServiceSpecification persistedSpecification = ReadSpecification()
				?? throw new InvalidOperationException($"No Service Specification with ID '{SpecificationId}' exists.");

			var orderItem = new Models.ServiceOrderItem
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = $"RT_ScheduledActivation_OrderItem_{Guid.NewGuid()}",
				ServiceInfo = new Models.ServiceOrderItemServiceInfo
				{
					SpecificationId = persistedSpecification,
					Configurations = new List<SdmObjectReference<Models.ServiceOrderItemConfigurationValue>>(),
				},
				StartTime = DateTime.UtcNow,
				EndTime = DateTime.UtcNow.AddHours(1),
			};
			OrderingApi.ServiceOrder.ServiceOrderItems.Create(orderItem);
			OrderItemId = orderItem.Identifier;

			var order = new Models.ServiceOrder
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = $"RT_ScheduledActivation_Order_{Guid.NewGuid()}",
				OrderId = $"RT-{Guid.NewGuid()}",
				ContactIds = new List<Guid>(),
				OrderItems = new List<Models.ServiceOrderEntry>
				{
					new Models.ServiceOrderEntry { ServiceOrderItemId = orderItem },
				},
			};
			OrderingApi.ServiceOrder.ServiceOrders.Create(new[] { order });
			OrderId = order.Identifier;

			MaterialiseService();

			var parameter = new ConfigModels.ConfigurationParameter
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = $"RT_ScheduledActivation_Parameter_{Guid.NewGuid()}",
				Type = SlcConfigurationsIds.Enums.Type.Text,
			};
			InventoryApi.ServiceCatalog.ConfigurationParameters.Create(parameter);
			ConfigurationParameterId = parameter.Identifier;
		}

		/// <summary>
		/// Builds the payload for the current version, which runs until the planned version starts and is activated.
		/// </summary>
		/// <returns>The JSON payload.</returns>
		public string BuildCurrentVersionPayload()
		{
			return BuildPayload(null, CurrentVersionName, CurrentStart, PlannedStart, activate: true, CurrentValue, null, null);
		}

		/// <summary>
		/// Builds the payload for the planned version (future start date, open end), with or without activating it.
		/// </summary>
		/// <param name="activate">Whether the planned version is activated.</param>
		/// <returns>The JSON payload.</returns>
		public string BuildPlannedVersionPayload(bool activate)
		{
			return activate
				? BuildPayload(PlannedVersionId, PlannedVersionName, PlannedStart, null, activate: true, PlannedValue, PlannedConfigurationValueId, PlannedParameterValueId)
				: BuildPayload(null, PlannedVersionName, PlannedStart, null, activate: false, PlannedValue, null, null);
		}

		/// <summary>
		/// Records the version the script created for the current configuration.
		/// </summary>
		public void CaptureCurrentVersion()
		{
			CurrentVersionId = FindNewVersionId();
			CurrentConfigurationValueId = ReadFirstConfigurationValueId(CurrentVersionId);
			CurrentParameterValueId = ReadConfigurationValue(CurrentConfigurationValueId)?.ConfigurationParameterId.Identifier ?? String.Empty;
		}

		/// <summary>
		/// Records the version the script created for the planned configuration.
		/// </summary>
		public void CapturePlannedVersion()
		{
			PlannedVersionId = FindNewVersionId();
			PlannedConfigurationValueId = ReadFirstConfigurationValueId(PlannedVersionId);
			PlannedParameterValueId = ReadConfigurationValue(PlannedConfigurationValueId)?.ConfigurationParameterId.Identifier ?? String.Empty;
		}

		/// <summary>
		/// Records every configuration object linked to the service, so cleanup can remove it.
		/// </summary>
		public void CollectServiceConfiguration()
		{
			Models.Service service = ReadService();
			if (service == null)
			{
				return;
			}

			var ids = new List<string>();
			if (service.ServiceConfigurationId != null)
			{
				ids.Add(service.ServiceConfigurationId.Identifier);
			}

			ids.AddRange((service.ConfigurationVersions ?? new List<SdmObjectReference<Models.ServiceConfigurationVersion>>()).Select(reference => reference.Identifier));

			foreach (string versionId in ids.Where(id => !String.IsNullOrWhiteSpace(id)))
			{
				versionIds.Add(versionId);
				Models.ServiceConfigurationVersion version = ReadConfigurationVersion(versionId);
				foreach (var parameter in version?.Parameters ?? new List<SdmObjectReference<Models.ServiceConfigurationValue>>())
				{
					if (String.IsNullOrWhiteSpace(parameter.Identifier))
					{
						continue;
					}

					configurationValueIds.Add(parameter.Identifier);
					string parameterValueId = ReadConfigurationValue(parameter.Identifier)?.ConfigurationParameterId.Identifier;
					if (!String.IsNullOrWhiteSpace(parameterValueId))
					{
						parameterValueIds.Add(parameterValueId);
					}
				}
			}
		}

		public Models.ServiceSpecification ReadSpecification()
		{
			if (String.IsNullOrWhiteSpace(SpecificationId))
			{
				return null;
			}

			return OrderingApi.ServiceCatalog.ServiceSpecifications
				.Read(Models.ServiceSpecificationExposers.Identifier.Equal(SpecificationId))
				.FirstOrDefault();
		}

		public Models.Service ReadService()
		{
			if (String.IsNullOrWhiteSpace(ServiceId))
			{
				return null;
			}

			return InventoryApi.ServiceInventory.Services
				.Read(Models.ServiceExposers.Identifier.Equal(ServiceId))
				.FirstOrDefault();
		}

		public Models.ServiceConfigurationVersion ReadConfigurationVersion(string identifier)
		{
			if (String.IsNullOrWhiteSpace(identifier))
			{
				return null;
			}

			return InventoryApi.ServiceInventory.ServiceConfigurationVersions
				.Read(Models.ServiceConfigurationVersionExposers.Identifier.Equal(identifier))
				.FirstOrDefault();
		}

		public Models.ServiceConfigurationValue ReadConfigurationValue(string identifier)
		{
			if (String.IsNullOrWhiteSpace(identifier))
			{
				return null;
			}

			return InventoryApi.ServiceInventory.ServiceConfigurationValues
				.Read(Models.ServiceConfigurationValueExposers.Identifier.Equal(identifier))
				.FirstOrDefault();
		}

		public ConfigModels.ConfigurationParameterValue ReadParameterValue(string identifier)
		{
			if (String.IsNullOrWhiteSpace(identifier))
			{
				return null;
			}

			return InventoryApi.ServiceCatalog.ConfigurationParameterValues
				.Read(ConfigModels.ConfigurationParameterValueExposers.Identifier.Equal(identifier))
				.FirstOrDefault();
		}

		/// <summary>
		/// Removes everything this test created, dependants before the objects they reference.
		/// A failing step never blocks the remaining steps.
		/// </summary>
		public void CleanUp()
		{
			TryCleanup("configuration bookkeeping", CollectServiceConfiguration);
			AddKnownIds();

			TryCleanup("service", () => Delete(InventoryApi.ServiceInventory.Services, ReadService()));
			TryCleanup(
				"order",
				() => Delete(
					OrderingApi.ServiceOrder.ServiceOrders,
					String.IsNullOrWhiteSpace(OrderId) ? null : OrderingApi.ServiceOrder.ServiceOrders.Read(Models.ServiceOrderExposers.Identifier.Equal(OrderId)).FirstOrDefault()));
			TryCleanup(
				"order item",
				() => Delete(
					OrderingApi.ServiceOrder.ServiceOrderItems,
					String.IsNullOrWhiteSpace(OrderItemId) ? null : OrderingApi.ServiceOrder.ServiceOrderItems.Read(Models.ServiceOrderItemExposers.Identifier.Equal(OrderItemId)).FirstOrDefault()));
			TryCleanup("specification", () => Delete(OrderingApi.ServiceCatalog.ServiceSpecifications, ReadSpecification()));

			foreach (string id in versionIds.ToList())
			{
				TryCleanup($"configuration version {id}", () => Delete(InventoryApi.ServiceInventory.ServiceConfigurationVersions, ReadConfigurationVersion(id)));
			}

			foreach (string id in configurationValueIds.ToList())
			{
				TryCleanup($"configuration item {id}", () => Delete(InventoryApi.ServiceInventory.ServiceConfigurationValues, ReadConfigurationValue(id)));
			}

			foreach (string id in parameterValueIds.ToList())
			{
				TryCleanup($"parameter value {id}", () => Delete(InventoryApi.ServiceCatalog.ConfigurationParameterValues, ReadParameterValue(id)));
			}

			TryCleanup(
				"configuration parameter",
				() => Delete(
					InventoryApi.ServiceCatalog.ConfigurationParameters,
					String.IsNullOrWhiteSpace(ConfigurationParameterId) ? null : InventoryApi.ServiceCatalog.ConfigurationParameters.Read(ConfigModels.ConfigurationParameterExposers.Identifier.Equal(ConfigurationParameterId)).FirstOrDefault()));
		}

		private static void Delete<T>(IDeletableRepository<T> repository, T instance)
			where T : class
		{
			if (instance != null)
			{
				repository.Delete(instance);
			}
		}

		private static string FormatDate(DateTime? value)
		{
			return value.HasValue ? value.Value.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture) : String.Empty;
		}

		private string BuildPayload(
			string versionId,
			string versionName,
			DateTime? start,
			DateTime? end,
			bool activate,
			string value,
			string configurationId,
			string parameterValueId)
		{
			var payload = new
			{
				VersionId = versionId ?? String.Empty,
				VersionName = versionName,
				Description = "Temporary fixture for regression testing.",
				StartDate = FormatDate(start),
				EndDate = FormatDate(end),
				DeleteVersion = false,
				ActivateVersion = activate,
				Parameters = new[]
				{
					new
					{
						ConfigurationId = configurationId,
						ParameterId = ConfigurationParameterId,
						ParameterValueId = parameterValueId,
						Value = value,
						Mandatory = true,
						Label = ParameterLabel,
						Fixed = false,
						IsLinked = false,
					},
				},
				Profiles = new object[0],
			};

			return JsonConvert.SerializeObject(payload);
		}

		private string FindNewVersionId()
		{
			CollectServiceConfiguration();

			Models.Service service = ReadService();
			var known = new HashSet<string>(new[] { CurrentVersionId, PlannedVersionId }.Where(id => !String.IsNullOrWhiteSpace(id)), StringComparer.OrdinalIgnoreCase);
			return (service?.ConfigurationVersions ?? new List<SdmObjectReference<Models.ServiceConfigurationVersion>>())
				.Select(reference => reference.Identifier)
				.FirstOrDefault(id => !String.IsNullOrWhiteSpace(id) && !known.Contains(id)) ?? String.Empty;
		}

		private string ReadFirstConfigurationValueId(string versionId)
		{
			return ReadConfigurationVersion(versionId)?.Parameters?.Select(reference => reference.Identifier).FirstOrDefault() ?? String.Empty;
		}

		private void AddKnownIds()
		{
			foreach (string id in new[] { CurrentVersionId, PlannedVersionId }.Where(id => !String.IsNullOrWhiteSpace(id)))
			{
				versionIds.Add(id);
			}

			foreach (string id in new[] { CurrentConfigurationValueId, PlannedConfigurationValueId }.Where(id => !String.IsNullOrWhiteSpace(id)))
			{
				configurationValueIds.Add(id);
			}

			foreach (string id in new[] { CurrentParameterValueId, PlannedParameterValueId }.Where(id => !String.IsNullOrWhiteSpace(id)))
			{
				parameterValueIds.Add(id);
			}
		}

		private void MaterialiseService()
		{
			SubScriptOptions createScript = engine.PrepareSubScript(CreateServiceInventoryScript);
			createScript.Synchronous = true;
			createScript.ExtendedErrorInfo = true;
			createScript.SelectScriptParam("DOM ID", OrderItemId);
			createScript.SelectScriptParam("Action", "AddItemSilent");
			createScript.SelectScriptParam("Payload", "[]");
			createScript.StartScript();

			if (createScript.HadError)
			{
				throw new InvalidOperationException(
					$"Subscript '{CreateServiceInventoryScript}' failed:{Environment.NewLine}{String.Join(Environment.NewLine, createScript.GetErrorMessages() ?? Enumerable.Empty<string>())}");
			}

			Models.ServiceOrderItem orderItem = OrderingApi.ServiceOrder.ServiceOrderItems
				.Read(Models.ServiceOrderItemExposers.Identifier.Equal(OrderItemId))
				.FirstOrDefault()
				?? throw new InvalidOperationException($"Order item fixture '{OrderItemId}' was not found after creating the service.");

			if (orderItem.ServiceInfo == null
				|| orderItem.ServiceInfo.ServiceId == null
				|| String.IsNullOrWhiteSpace(orderItem.ServiceInfo.ServiceId.Identifier))
			{
				throw new InvalidOperationException($"No linked service was created on order item '{OrderItemId}'.");
			}

			ServiceId = orderItem.ServiceInfo.ServiceId.Identifier;
		}

		private void TryCleanup(string subject, Action action)
		{
			try
			{
				action();
			}
			catch (Exception ex)
			{
				engine.GenerateInformation($"RT_ScheduledConfigurationActivation: cleanup failed for {subject}: {ex}");
			}
		}
	}
}

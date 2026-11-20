namespace SLC_SM_RT_ServiceUpdate.TestCases
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
	internal sealed class ServiceUpdateTestData
	{
		private const string CreateServiceInventoryScript = "SLC_SM_Create Service Inventory Item";

		private readonly IEngine engine;

		public ServiceUpdateTestData(IEngine engine)
		{
			this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
			InventoryApi = engine.GetUserConnection().GetServiceManagementApiHelper("Service Inventory");
			OrderingApi = engine.GetUserConnection().GetServiceManagementApiHelper("Service Ordering");
		}

		public IServiceManagementApiHelper InventoryApi { get; }

		public IServiceManagementApiHelper OrderingApi { get; }

		public string SpecificationId { get; private set; } = String.Empty;

		public string SpecificationName { get; private set; } = String.Empty;

		public string CategoryId { get; private set; } = String.Empty;

		public string CategoryName { get; private set; } = String.Empty;

		public string CategoryIcon { get; private set; } = String.Empty;

		public string OrderId { get; private set; } = String.Empty;

		public string OrderItemId { get; private set; } = String.Empty;

		public string ServiceId { get; private set; } = String.Empty;

		public string ConfigurationParameterId { get; private set; } = String.Empty;

		public string ParameterValueId { get; private set; } = String.Empty;

		public string ServiceConfigurationValueId { get; private set; } = String.Empty;

		public string ServiceConfigurationVersionId { get; private set; } = String.Empty;

		/// <summary>
		/// Gets the state of the service right before the first edit; everything the edit must not touch is compared against it.
		/// </summary>
		public ServiceSnapshot Baseline { get; private set; }

		public string ExpectedName { get; private set; } = String.Empty;

		public DateTime? ExpectedStartTime { get; private set; }

		public DateTime? ExpectedEndTime { get; private set; }

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
		/// Creates the specification, category, order and order item fixtures, materialises the service under test
		/// and links an active configuration version to it.
		/// </summary>
		public void CreateFixtures()
		{
			var category = new Models.ServiceCategory
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = $"RT_ServiceUpdate_Category_{Guid.NewGuid()}",
				Icon = "RT_ServiceUpdate_Icon",
			};
			OrderingApi.ServiceCatalog.ServiceCategories.Create(category);
			CategoryId = category.Identifier;
			CategoryName = category.Name;
			CategoryIcon = category.Icon;

			var specification = new Models.ServiceSpecification
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = $"RT_ServiceUpdate_Spec_{Guid.NewGuid()}",
				Description = "Temporary fixture for regression testing.",
				ServiceItems = new List<Models.ServiceItem> { new Models.ServiceItem() },
				ServiceItemsRelationships = new List<Models.ServiceItemRelationship> { new Models.ServiceItemRelationship() },
			};

			OrderingApi.ServiceCatalog.ServiceSpecifications.Create(specification);
			SpecificationId = specification.Identifier;
			SpecificationName = specification.Name;

			Models.ServiceSpecification persistedSpecification = ReadSpecification()
				?? throw new InvalidOperationException($"No Service Specification with ID '{SpecificationId}' exists.");

			var orderItem = new Models.ServiceOrderItem
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = $"RT_ServiceUpdate_OrderItem_{Guid.NewGuid()}",
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
				Name = $"RT_ServiceUpdate_Order_{Guid.NewGuid()}",
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
			CreateAndLinkServiceConfiguration();
		}

		/// <summary>
		/// Runs one lifecycle transition on the service under test.
		/// </summary>
		/// <param name="previousState">The current state.</param>
		/// <param name="nextState">The requested state.</param>
		public void InvokeStateTransition(string previousState, string nextState)
		{
			SubScriptOptions subScript = engine.PrepareSubScript(Script.StateTransitionScript);
			subScript.Synchronous = true;
			subScript.ExtendedErrorInfo = true;
			subScript.SelectScriptParam("ServiceReference", ServiceId);
			subScript.SelectScriptParam("PreviousState", previousState);
			subScript.SelectScriptParam("NextState", nextState);
			subScript.StartScript();

			if (subScript.HadError)
			{
				throw new InvalidOperationException(
					$"Transition '{previousState}' -> '{nextState}' of service '{ServiceId}' failed:{Environment.NewLine}{String.Join(Environment.NewLine, subScript.GetErrorMessages() ?? Enumerable.Empty<string>())}");
			}
		}

		/// <summary>
		/// Records the state of the service that the edits must keep.
		/// </summary>
		public void TakeBaseline()
		{
			Models.Service service = ReadService()
				?? throw new InvalidOperationException($"Service '{ServiceId}' was not found to take the baseline.");
			Baseline = new ServiceSnapshot(service);
		}

		/// <summary>
		/// Builds the "Edit" payload for the given stage and records the values that are expected afterwards.
		/// </summary>
		/// <param name="stage">The stage of the test, used to make the new values unique.</param>
		/// <param name="indefiniteRuntime">Whether the service is switched to an indefinite runtime.</param>
		/// <returns>The JSON payload.</returns>
		public string BuildEditPayload(string stage, bool indefiniteRuntime)
		{
			DateTime now = DateTime.UtcNow;
			DateTime start = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second, DateTimeKind.Utc).AddDays(1);
			DateTime end = start.AddDays(30);

			if (Baseline == null)
			{
				throw new InvalidOperationException("A service baseline must be recorded before building an edit payload.");
			}

			ExpectedName = $"RT_ServiceUpdate_{stage}_{Guid.NewGuid()}";
			ExpectedStartTime = start;
			ExpectedEndTime = indefiniteRuntime ? (DateTime?)null : end;

			var payload = new
			{
				ServiceID = Baseline.ServiceIdentifier,
				Name = ExpectedName,
				Category = CategoryName,
				Specification = SpecificationName,
				Organization = String.Empty,
				StartTime = start.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),

				// The end time is sent on purpose with an indefinite runtime: the flag must win.
				EndTime = end.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
				IndefiniteRuntime = indefiniteRuntime,
				LinkedService = String.Empty,
				LinkService = false,
				GenerateMonitoringService = false,
			};

			return JsonConvert.SerializeObject(payload);
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

		/// <summary>
		/// Removes everything this test created, dependants before the objects they reference.
		/// A failing step never blocks the remaining steps.
		/// </summary>
		public void CleanUp()
		{
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
			TryCleanup(
				"category",
				() => Delete(
					OrderingApi.ServiceCatalog.ServiceCategories,
					String.IsNullOrWhiteSpace(CategoryId) ? null : OrderingApi.ServiceCatalog.ServiceCategories.Read(Models.ServiceCategoryExposers.Identifier.Equal(CategoryId)).FirstOrDefault()));
			TryCleanup(
				"configuration version",
				() => Delete(
					InventoryApi.ServiceInventory.ServiceConfigurationVersions,
					String.IsNullOrWhiteSpace(ServiceConfigurationVersionId) ? null : InventoryApi.ServiceInventory.ServiceConfigurationVersions.Read(Models.ServiceConfigurationVersionExposers.Identifier.Equal(ServiceConfigurationVersionId)).FirstOrDefault()));
			TryCleanup(
				"configuration item",
				() => Delete(
					InventoryApi.ServiceInventory.ServiceConfigurationValues,
					String.IsNullOrWhiteSpace(ServiceConfigurationValueId) ? null : InventoryApi.ServiceInventory.ServiceConfigurationValues.Read(Models.ServiceConfigurationValueExposers.Identifier.Equal(ServiceConfigurationValueId)).FirstOrDefault()));
			TryCleanup(
				"parameter value",
				() => Delete(
					InventoryApi.ServiceCatalog.ConfigurationParameterValues,
					String.IsNullOrWhiteSpace(ParameterValueId) ? null : InventoryApi.ServiceCatalog.ConfigurationParameterValues.Read(ConfigModels.ConfigurationParameterValueExposers.Identifier.Equal(ParameterValueId)).FirstOrDefault()));
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

		private void CreateAndLinkServiceConfiguration()
		{
			var parameter = new ConfigModels.ConfigurationParameter
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = $"RT_ServiceUpdate_Parameter_{Guid.NewGuid()}",
				Type = SlcConfigurationsIds.Enums.Type.Text,
			};
			InventoryApi.ServiceCatalog.ConfigurationParameters.Create(parameter);
			ConfigurationParameterId = parameter.Identifier;

			var parameterValue = new ConfigModels.ConfigurationParameterValue
			{
				Identifier = Guid.NewGuid().ToString(),
				Label = $"RT_ServiceUpdate_Value_{Guid.NewGuid()}",
				Type = parameter.Type,
				StringValue = "RT value",
				ConfigurationParameterId = new SdmObjectReference<ConfigModels.ConfigurationParameter>(parameter.Identifier),
			};
			InventoryApi.ServiceCatalog.ConfigurationParameterValues.Create(parameterValue);
			ParameterValueId = parameterValue.Identifier;

			var serviceConfigurationValue = new Models.ServiceConfigurationValue
			{
				Identifier = Guid.NewGuid().ToString(),
				Mandatory = true,
				ConfigurationParameterId = new SdmObjectReference<ConfigModels.ConfigurationParameterValue>(parameterValue.Identifier),
			};
			InventoryApi.ServiceInventory.ServiceConfigurationValues.Create(serviceConfigurationValue);
			ServiceConfigurationValueId = serviceConfigurationValue.Identifier;

			var version = new Models.ServiceConfigurationVersion
			{
				Identifier = Guid.NewGuid().ToString(),
				VersionName = $"RT_ServiceUpdate_Version_{Guid.NewGuid()}",
				Description = "Temporary fixture for regression testing.",
				Parameters = new List<SdmObjectReference<Models.ServiceConfigurationValue>>
				{
					new SdmObjectReference<Models.ServiceConfigurationValue>(serviceConfigurationValue.Identifier),
				},
				Profiles = new List<SdmObjectReference<Models.ServiceProfile>>(),
			};
			InventoryApi.ServiceInventory.ServiceConfigurationVersions.Create(version);
			ServiceConfigurationVersionId = version.Identifier;

			Models.Service service = ReadService()
				?? throw new InvalidOperationException($"Service '{ServiceId}' was not found before linking its configuration.");
			if (service.ConfigurationVersions == null)
			{
				service.ConfigurationVersions = new List<SdmObjectReference<Models.ServiceConfigurationVersion>>();
			}

			service.ConfigurationVersions.Add(new SdmObjectReference<Models.ServiceConfigurationVersion>(version.Identifier));
			service.ServiceConfigurationId = new SdmObjectReference<Models.ServiceConfigurationVersion>(version.Identifier);
			InventoryApi.ServiceInventory.Services.Update(service);
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
				engine.GenerateInformation($"RT_ServiceUpdate: cleanup failed for {subject}: {ex}");
			}
		}
	}
}

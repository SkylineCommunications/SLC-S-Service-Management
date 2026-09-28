namespace SLC_SM_RT_ServiceConfiguration.TestCases
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using Skyline.DataMiner.SDM;

	using ConfigModels = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;
	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Owns the fixtures of the test and the state shared between its test cases.
	/// </summary>
	internal sealed class ServiceConfigurationTestData
	{
		private const string CreateServiceInventoryScript = "SLC_SM_Create Service Inventory Item";

		private readonly IEngine engine;

		public ServiceConfigurationTestData(IEngine engine)
		{
			this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
			InventoryApi = engine.GetUserConnection().GetServiceManagementApiHelper("Service Inventory");
			OrderingApi = engine.GetUserConnection().GetServiceManagementApiHelper("Service Ordering");
		}

		public IServiceManagementApiHelper InventoryApi { get; }

		public IServiceManagementApiHelper OrderingApi { get; }

		public string SpecificationId { get; private set; } = String.Empty;

		public string OrderId { get; private set; } = String.Empty;

		public string OrderItemId { get; private set; } = String.Empty;

		public string ServiceId { get; private set; } = String.Empty;

		public string ParameterValueId { get; private set; } = String.Empty;

		public string ServiceConfigurationValueId { get; private set; } = String.Empty;

		public string ServiceConfigurationVersionId { get; private set; } = String.Empty;

		/// <summary>
		/// Creates the specification, order and order item fixtures and materialises the service under test.
		/// </summary>
		public void CreateFixtures()
		{
			var specification = new Models.ServiceSpecification
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = $"RT_ServiceConfig_Spec_{Guid.NewGuid()}",
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
				Name = $"RT_ServiceConfig_OrderItem_{Guid.NewGuid()}",
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
				Name = $"RT_ServiceConfig_Order_{Guid.NewGuid()}",
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
		}

		/// <summary>
		/// Creates a configuration parameter value, a service configuration item and a configuration version,
		/// then links the version to the service and marks it as the active one.
		/// </summary>
		public void CreateAndLinkServiceConfiguration()
		{
			ConfigModels.ConfigurationParameter parameter = InventoryApi.ServiceCatalog.ConfigurationParameters
				.Read(new TRUEFilterElement<ConfigModels.ConfigurationParameter>())
				.FirstOrDefault()
				?? throw new InvalidOperationException("No Configuration Parameters exist in Service Catalog.");

			var parameterValue = new ConfigModels.ConfigurationParameterValue
			{
				Identifier = Guid.NewGuid().ToString(),
				Label = $"RT_ServiceConfig_Value_{Guid.NewGuid()}",
				Type = parameter.Type,
				ConfigurationParameterId = new SdmObjectReference<ConfigModels.ConfigurationParameter>(parameter.Identifier),
			};
			InventoryApi.ServiceCatalog.ConfigurationParameterValues.Create(parameterValue);
			ParameterValueId = parameterValue.Identifier;

			var serviceConfigurationValue = new Models.ServiceConfigurationValue
			{
				Identifier = Guid.NewGuid().ToString(),
				Mandatory = true,
				ConfigurationParameterId = new SdmObjectReference<ConfigModels.ConfigurationParameter>(parameterValue.Identifier),
			};
			InventoryApi.ServiceInventory.ServiceConfigurationValues.Create(serviceConfigurationValue);
			ServiceConfigurationValueId = serviceConfigurationValue.Identifier;

			var serviceConfigurationVersion = new Models.ServiceConfigurationVersion
			{
				Identifier = Guid.NewGuid().ToString(),
				VersionName = $"RT_ServiceConfig_Version_{Guid.NewGuid()}",
				Description = "Temporary fixture for regression testing.",
				StartDate = null,
				EndDate = null,
				Parameters = new List<SdmObjectReference<Models.ServiceConfigurationValue>>
				{
					new SdmObjectReference<Models.ServiceConfigurationValue>(serviceConfigurationValue.Identifier),
				},
				Profiles = new List<SdmObjectReference<Models.ServiceProfile>>(),
			};
			InventoryApi.ServiceInventory.ServiceConfigurationVersions.Create(serviceConfigurationVersion);
			ServiceConfigurationVersionId = serviceConfigurationVersion.Identifier;

			Models.Service service = ReadService()
				?? throw new InvalidOperationException("Service was not found before linking configuration version.");

			if (service.ConfigurationVersions == null)
			{
				service.ConfigurationVersions = new List<SdmObjectReference<Models.ServiceConfigurationVersion>>();
			}

			service.ConfigurationVersions.Add(new SdmObjectReference<Models.ServiceConfigurationVersion>(serviceConfigurationVersion.Identifier));
			service.ServiceConfigurationId = new SdmObjectReference<Models.ServiceConfigurationVersion>(serviceConfigurationVersion.Identifier);
			InventoryApi.ServiceInventory.Services.Update(service);
		}

		/// <summary>
		/// Reads the Service Specification back from the system.
		/// </summary>
		/// <returns>The specification, or <c>null</c> when it does not exist.</returns>
		public Models.ServiceSpecification ReadSpecification()
		{
			return OrderingApi.ServiceCatalog.ServiceSpecifications
				.Read(Models.ServiceSpecificationExposers.Identifier.Equal(SpecificationId))
				.FirstOrDefault();
		}

		/// <summary>
		/// Reads the service under test back from the system.
		/// </summary>
		/// <returns>The service, or <c>null</c> when it does not exist.</returns>
		public Models.Service ReadService()
		{
			return InventoryApi.ServiceInventory.Services
				.Read(Models.ServiceExposers.Identifier.Equal(ServiceId))
				.FirstOrDefault();
		}

		/// <summary>
		/// Reads the created Service Configuration Version back from the system.
		/// </summary>
		/// <returns>The configuration version, or <c>null</c> when it does not exist.</returns>
		public Models.ServiceConfigurationVersion ReadConfigurationVersion()
		{
			return InventoryApi.ServiceInventory.ServiceConfigurationVersions
				.Read(Models.ServiceConfigurationVersionExposers.Identifier.Equal(ServiceConfigurationVersionId))
				.FirstOrDefault();
		}

		/// <summary>
		/// Reads the created Service Configuration Item back from the system.
		/// </summary>
		/// <returns>The service configuration value, or <c>null</c> when it does not exist.</returns>
		public Models.ServiceConfigurationValue ReadConfigurationValue()
		{
			return InventoryApi.ServiceInventory.ServiceConfigurationValues
				.Read(new TRUEFilterElement<Models.ServiceConfigurationValue>())
				.FirstOrDefault(x => String.Equals(x.Identifier, ServiceConfigurationValueId, StringComparison.OrdinalIgnoreCase));
		}

		/// <summary>
		/// Removes everything this test created, in reverse creation order.
		/// A failing step never blocks the remaining steps.
		/// </summary>
		public void CleanUp()
		{
			TryCleanup(
				"service",
				() =>
				{
					if (String.IsNullOrWhiteSpace(ServiceId))
					{
						return;
					}

					Models.Service service = ReadService();
					if (service != null)
					{
						InventoryApi.ServiceInventory.Services.Delete(service);
					}
				});

			TryCleanup(
				"order",
				() =>
				{
					if (String.IsNullOrWhiteSpace(OrderId))
					{
						return;
					}

					Models.ServiceOrder order = OrderingApi.ServiceOrder.ServiceOrders
						.Read(Models.ServiceOrderExposers.Identifier.Equal(OrderId))
						.FirstOrDefault();
					if (order != null)
					{
						OrderingApi.ServiceOrder.ServiceOrders.Delete(order);
					}
				});

			TryCleanup(
				"order item",
				() =>
				{
					if (String.IsNullOrWhiteSpace(OrderItemId))
					{
						return;
					}

					Models.ServiceOrderItem orderItem = OrderingApi.ServiceOrder.ServiceOrderItems
						.Read(Models.ServiceOrderItemExposers.Identifier.Equal(OrderItemId))
						.FirstOrDefault();
					if (orderItem != null)
					{
						OrderingApi.ServiceOrder.ServiceOrderItems.Delete(orderItem);
					}
				});

			TryCleanup(
				"specification",
				() =>
				{
					if (String.IsNullOrWhiteSpace(SpecificationId))
					{
						return;
					}

					Models.ServiceSpecification specification = ReadSpecification();
					if (specification != null)
					{
						OrderingApi.ServiceCatalog.ServiceSpecifications.Delete(specification);
					}
				});

			TryCleanup(
				"service configuration version",
				() =>
				{
					if (String.IsNullOrWhiteSpace(ServiceConfigurationVersionId))
					{
						return;
					}

					Models.ServiceConfigurationVersion version = ReadConfigurationVersion();
					if (version != null)
					{
						InventoryApi.ServiceInventory.ServiceConfigurationVersions.Delete(version);
					}
				});

			TryCleanup(
				"service configuration item",
				() =>
				{
					if (String.IsNullOrWhiteSpace(ServiceConfigurationValueId))
					{
						return;
					}

					Models.ServiceConfigurationValue value = ReadConfigurationValue();
					if (value != null)
					{
						InventoryApi.ServiceInventory.ServiceConfigurationValues.Delete(value);
					}
				});

			TryCleanup(
				"configuration parameter value",
				() =>
				{
					if (String.IsNullOrWhiteSpace(ParameterValueId))
					{
						return;
					}

					ConfigModels.ConfigurationParameterValue value = InventoryApi.ServiceCatalog.ConfigurationParameterValues
						.Read(ConfigModels.ConfigurationParameterValueExposers.Identifier.Equal(ParameterValueId))
						.FirstOrDefault();
					if (value != null)
					{
						InventoryApi.ServiceCatalog.ConfigurationParameterValues.Delete(value);
					}
				});
		}

		private void MaterialiseService()
		{
			SubScriptOptions createScript = engine.PrepareSubScript(CreateServiceInventoryScript);
			createScript.Synchronous = true;
			createScript.ExtendedErrorInfo = true;
			createScript.SelectScriptParam("DOM ID", OrderItemId);
			createScript.SelectScriptParam("Action", "AddItemSilent");
			createScript.StartScript();

			if (createScript.HadError)
			{
				throw new InvalidOperationException(
					$"Subscript '{CreateServiceInventoryScript}' failed:{Environment.NewLine}{String.Join(Environment.NewLine, createScript.GetErrorMessages() ?? Enumerable.Empty<string>())}");
			}

			Models.ServiceOrderItem orderItem = OrderingApi.ServiceOrder.ServiceOrderItems
				.Read(Models.ServiceOrderItemExposers.Identifier.Equal(OrderItemId))
				.FirstOrDefault()
				?? throw new InvalidOperationException("Order item fixture was not found after creating service.");

			if (orderItem.ServiceInfo == null
				|| orderItem.ServiceInfo.ServiceId == null
				|| String.IsNullOrWhiteSpace(orderItem.ServiceInfo.ServiceId.Identifier))
			{
				throw new InvalidOperationException("No linked service was created on the order item.");
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
				engine.GenerateInformation($"RT_ServiceConfiguration: cleanup failed for {subject}: {ex}");
			}
		}
	}
}

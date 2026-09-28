namespace SLC_SM_RT_ServiceOrderItemConfiguration.TestCases
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
	internal sealed class ServiceOrderItemConfigurationTestData
	{
		private readonly IEngine engine;

		public ServiceOrderItemConfigurationTestData(IEngine engine)
		{
			this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
			Api = engine.GetUserConnection().GetServiceManagementApiHelper("Service Ordering");
		}

		public IServiceManagementApiHelper Api { get; }

		public string SpecificationId { get; private set; } = String.Empty;

		public string OrderItemId { get; private set; } = String.Empty;

		public string ParameterValueId { get; private set; } = String.Empty;

		public string OrderItemConfigurationId { get; private set; } = String.Empty;

		/// <summary>
		/// Creates the Service Specification and the Service Order Item the configuration is linked to.
		/// </summary>
		public void CreateFixtures()
		{
			var specification = new Models.ServiceSpecification
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = $"RT_OrderItemConfig_Spec_{Guid.NewGuid()}",
				Description = "Temporary fixture for regression testing.",
				ServiceItems = new List<Models.ServiceItem> { new Models.ServiceItem() },
				ServiceItemsRelationships = new List<Models.ServiceItemRelationship> { new Models.ServiceItemRelationship() },
			};

			Api.ServiceCatalog.ServiceSpecifications.Create(specification);
			SpecificationId = specification.Identifier;

			Models.ServiceSpecification persistedSpecification = ReadSpecification()
				?? throw new InvalidOperationException($"No Service Specification with ID '{SpecificationId}' exists.");

			var orderItem = new Models.ServiceOrderItem
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = $"RT_OrderItemConfig_OrderItem_{Guid.NewGuid()}",
				ServiceInfo = new Models.ServiceOrderItemServiceInfo
				{
					SpecificationId = persistedSpecification,
					Configurations = new List<SdmObjectReference<Models.ServiceOrderItemConfigurationValue>>(),
				},
				StartTime = DateTime.UtcNow,
				EndTime = DateTime.UtcNow.AddHours(1),
			};

			Api.ServiceOrder.ServiceOrderItems.Create(orderItem);
			OrderItemId = orderItem.Identifier;
		}

		/// <summary>
		/// Creates a Configuration Parameter Value plus a Service Order Item Configuration Value,
		/// and links the latter to the order item.
		/// </summary>
		public void CreateAndLinkOrderItemConfiguration()
		{
			ConfigModels.ConfigurationParameter parameter = Api.ServiceCatalog.ConfigurationParameters
				.Read(new TRUEFilterElement<ConfigModels.ConfigurationParameter>())
				.FirstOrDefault()
				?? throw new InvalidOperationException("No Configuration Parameters exist in Service Catalog.");

			var parameterValue = new ConfigModels.ConfigurationParameterValue
			{
				Identifier = Guid.NewGuid().ToString(),
				Label = $"RT_OrderItemConfig_Value_{Guid.NewGuid()}",
				Type = parameter.Type,
				ConfigurationParameterId = new SdmObjectReference<ConfigModels.ConfigurationParameter>(parameter.Identifier),
			};
			Api.ServiceCatalog.ConfigurationParameterValues.Create(parameterValue);
			ParameterValueId = parameterValue.Identifier;

			var orderItemConfiguration = new Models.ServiceOrderItemConfigurationValue
			{
				Identifier = Guid.NewGuid().ToString(),
				Mandatory = true,
				ConfigurationParameterValueId = new SdmObjectReference<ConfigModels.ConfigurationParameterValue>(parameterValue.Identifier),
			};
			Api.ServiceOrder.ServiceOrderItemConfigurationValues.Create(orderItemConfiguration);
			OrderItemConfigurationId = orderItemConfiguration.Identifier;

			Models.ServiceOrderItem orderItem = ReadOrderItem()
				?? throw new InvalidOperationException("Order item was not found before linking configuration.");

			if (orderItem.ServiceInfo == null)
			{
				orderItem.ServiceInfo = new Models.ServiceOrderItemServiceInfo();
			}

			if (orderItem.ServiceInfo.Configurations == null)
			{
				orderItem.ServiceInfo.Configurations = new List<SdmObjectReference<Models.ServiceOrderItemConfigurationValue>>();
			}

			orderItem.ServiceInfo.Configurations.Add(new SdmObjectReference<Models.ServiceOrderItemConfigurationValue>(orderItemConfiguration.Identifier));
			Api.ServiceOrder.ServiceOrderItems.Update(orderItem);
		}

		/// <summary>
		/// Reads the Service Specification back from the system.
		/// </summary>
		/// <returns>The specification, or <c>null</c> when it does not exist.</returns>
		public Models.ServiceSpecification ReadSpecification()
		{
			return Api.ServiceCatalog.ServiceSpecifications
				.Read(Models.ServiceSpecificationExposers.Identifier.Equal(SpecificationId))
				.FirstOrDefault();
		}

		/// <summary>
		/// Reads the Service Order Item back from the system.
		/// </summary>
		/// <returns>The order item, or <c>null</c> when it does not exist.</returns>
		public Models.ServiceOrderItem ReadOrderItem()
		{
			return Api.ServiceOrder.ServiceOrderItems
				.Read(Models.ServiceOrderItemExposers.Identifier.Equal(OrderItemId))
				.FirstOrDefault();
		}

		/// <summary>
		/// Reads the Service Order Item Configuration Value back from the system.
		/// </summary>
		/// <returns>The order item configuration value, or <c>null</c> when it does not exist.</returns>
		public Models.ServiceOrderItemConfigurationValue ReadOrderItemConfiguration()
		{
			return Api.ServiceOrder.ServiceOrderItemConfigurationValues
				.Read(Models.ServiceOrderItemConfigurationValueExposers.Identifier.Equal(OrderItemConfigurationId))
				.FirstOrDefault();
		}

		/// <summary>
		/// Removes everything this test created, starting with the link on the order item.
		/// A failing step never blocks the remaining steps.
		/// </summary>
		public void CleanUp()
		{
			TryCleanup(
				"order item link",
				() =>
				{
					if (String.IsNullOrWhiteSpace(OrderItemId) || String.IsNullOrWhiteSpace(OrderItemConfigurationId))
					{
						return;
					}

					Models.ServiceOrderItem orderItem = ReadOrderItem();
					if (orderItem == null || orderItem.ServiceInfo == null || orderItem.ServiceInfo.Configurations == null)
					{
						return;
					}

					orderItem.ServiceInfo.Configurations.RemoveAll(x => x.Identifier == OrderItemConfigurationId);
					Api.ServiceOrder.ServiceOrderItems.Update(orderItem);
				});

			TryCleanup(
				"order item configuration",
				() =>
				{
					if (String.IsNullOrWhiteSpace(OrderItemConfigurationId))
					{
						return;
					}

					Models.ServiceOrderItemConfigurationValue config = ReadOrderItemConfiguration();
					if (config != null)
					{
						Api.ServiceOrder.ServiceOrderItemConfigurationValues.Delete(config);
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

					ConfigModels.ConfigurationParameterValue value = Api.ServiceCatalog.ConfigurationParameterValues
						.Read(ConfigModels.ConfigurationParameterValueExposers.Identifier.Equal(ParameterValueId))
						.FirstOrDefault();
					if (value != null)
					{
						Api.ServiceCatalog.ConfigurationParameterValues.Delete(value);
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

					Models.ServiceOrderItem orderItem = ReadOrderItem();
					if (orderItem != null)
					{
						Api.ServiceOrder.ServiceOrderItems.Delete(orderItem);
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
						Api.ServiceCatalog.ServiceSpecifications.Delete(specification);
					}
				});
		}

		private void TryCleanup(string subject, Action action)
		{
			try
			{
				action();
			}
			catch (Exception ex)
			{
				engine.GenerateInformation($"RT_ServiceOrderItemConfiguration: cleanup failed for {subject}: {ex}");
			}
		}
	}
}

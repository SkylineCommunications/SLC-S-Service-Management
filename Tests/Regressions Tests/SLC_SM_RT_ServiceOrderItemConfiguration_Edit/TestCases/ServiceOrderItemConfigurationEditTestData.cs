namespace SLC_SM_RT_ServiceOrderItemConfiguration_Edit.TestCases
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
	internal sealed class ServiceOrderItemConfigurationEditTestData
	{
		private readonly IEngine engine;

		public ServiceOrderItemConfigurationEditTestData(IEngine engine)
		{
			this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
			Api = engine.GetUserConnection().GetServiceManagementApiHelper("Service Ordering");
		}

		public IServiceManagementApiHelper Api { get; }

		public string SpecificationId { get; private set; } = String.Empty;

		public string OrderItemId { get; private set; } = String.Empty;

		public string ParameterValueId { get; private set; } = String.Empty;

		public string OrderItemConfigurationId { get; private set; } = String.Empty;

		public string EditedLabel { get; private set; } = String.Empty;

		/// <summary>
		/// Creates the Service Specification and the Service Order Item the configuration is linked to.
		/// </summary>
		public void CreateFixtures()
		{
			var specification = new Models.ServiceSpecification
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = $"RT_OrderItemCfgEdit_Spec_{Guid.NewGuid()}",
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
				Name = $"RT_OrderItemCfgEdit_OrderItem_{Guid.NewGuid()}",
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
		/// Creates the parameter value and order item configuration, and links the latter to the order item.
		/// </summary>
		public void CreateAndLinkConfiguration()
		{
			ConfigModels.ConfigurationParameter parameter = Api.ServiceCatalog.ConfigurationParameters
				.Read(new TRUEFilterElement<ConfigModels.ConfigurationParameter>())
				.FirstOrDefault()
				?? throw new InvalidOperationException("No Configuration Parameters exist in Service Catalog.");

			var parameterValue = new ConfigModels.ConfigurationParameterValue
			{
				Identifier = Guid.NewGuid().ToString(),
				Label = $"RT_OrderItemCfgEdit_Value_{Guid.NewGuid()}",
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

			LinkConfigurationToOrderItem(orderItemConfiguration.Identifier, "Order item was not found while linking configuration.");
		}

		/// <summary>
		/// Renames the parameter value and clears the Mandatory flag of the order item configuration.
		/// </summary>
		public void EditConfiguration()
		{
			ConfigModels.ConfigurationParameterValue parameterValue = ReadParameterValue(ParameterValueId)
				?? throw new InvalidOperationException("Configuration parameter value to edit was not found.");

			EditedLabel = $"RT_OrderItemCfgEdit_Edited_{Guid.NewGuid()}";
			parameterValue.Label = EditedLabel;
			Api.ServiceCatalog.ConfigurationParameterValues.Update(parameterValue);

			Models.ServiceOrderItemConfigurationValue configuration = ReadOrderItemConfiguration(OrderItemConfigurationId)
				?? throw new InvalidOperationException("Order item configuration to edit was not found.");

			configuration.Mandatory = false;
			Api.ServiceOrder.ServiceOrderItemConfigurationValues.Update(configuration);
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
			if (String.IsNullOrWhiteSpace(OrderItemId))
			{
				return null;
			}

			return Api.ServiceOrder.ServiceOrderItems
				.Read(Models.ServiceOrderItemExposers.Identifier.Equal(OrderItemId))
				.FirstOrDefault();
		}

		/// <summary>
		/// Reads a Service Order Item Configuration Value back from the system.
		/// </summary>
		/// <param name="identifier">The identifier of the order item configuration.</param>
		/// <returns>The order item configuration value, or <c>null</c> when it does not exist.</returns>
		public Models.ServiceOrderItemConfigurationValue ReadOrderItemConfiguration(string identifier)
		{
			if (String.IsNullOrWhiteSpace(identifier))
			{
				return null;
			}

			return Api.ServiceOrder.ServiceOrderItemConfigurationValues
				.Read(Models.ServiceOrderItemConfigurationValueExposers.Identifier.Equal(identifier))
				.FirstOrDefault();
		}

		/// <summary>
		/// Reads a Configuration Parameter Value back from the system.
		/// </summary>
		/// <param name="identifier">The identifier of the parameter value.</param>
		/// <returns>The parameter value, or <c>null</c> when it does not exist.</returns>
		public ConfigModels.ConfigurationParameterValue ReadParameterValue(string identifier)
		{
			if (String.IsNullOrWhiteSpace(identifier))
			{
				return null;
			}

			return Api.ServiceCatalog.ConfigurationParameterValues
				.Read(ConfigModels.ConfigurationParameterValueExposers.Identifier.Equal(identifier))
				.FirstOrDefault();
		}

		/// <summary>
		/// Removes everything this test created, starting with the links on the order item.
		/// A failing step never blocks the remaining steps.
		/// </summary>
		public void CleanUp()
		{
			TryCleanup(
				"order item links",
				() =>
				{
					Models.ServiceOrderItem orderItem = ReadOrderItem();
					if (orderItem == null || orderItem.ServiceInfo == null || orderItem.ServiceInfo.Configurations == null)
					{
						return;
					}

					orderItem.ServiceInfo.Configurations.RemoveAll(x => x.Identifier == OrderItemConfigurationId);
					Api.ServiceOrder.ServiceOrderItems.Update(orderItem);
				});

			TryCleanup("order item configuration", () => DeleteOrderItemConfiguration(OrderItemConfigurationId));
			TryCleanup("configuration parameter value", () => DeleteConfigurationParameterValue(ParameterValueId));

			TryCleanup(
				"order item",
				() =>
				{
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

		private void LinkConfigurationToOrderItem(string orderItemConfigurationId, string notFoundMessage)
		{
			Models.ServiceOrderItem orderItem = ReadOrderItem()
				?? throw new InvalidOperationException(notFoundMessage);

			if (orderItem.ServiceInfo == null)
			{
				orderItem.ServiceInfo = new Models.ServiceOrderItemServiceInfo();
			}

			if (orderItem.ServiceInfo.Configurations == null)
			{
				orderItem.ServiceInfo.Configurations = new List<SdmObjectReference<Models.ServiceOrderItemConfigurationValue>>();
			}

			orderItem.ServiceInfo.Configurations.Add(new SdmObjectReference<Models.ServiceOrderItemConfigurationValue>(orderItemConfigurationId));
			Api.ServiceOrder.ServiceOrderItems.Update(orderItem);
		}

		private void DeleteOrderItemConfiguration(string identifier)
		{
			Models.ServiceOrderItemConfigurationValue value = ReadOrderItemConfiguration(identifier);
			if (value != null)
			{
				Api.ServiceOrder.ServiceOrderItemConfigurationValues.Delete(value);
			}
		}

		private void DeleteConfigurationParameterValue(string identifier)
		{
			ConfigModels.ConfigurationParameterValue value = ReadParameterValue(identifier);
			if (value != null)
			{
				Api.ServiceCatalog.ConfigurationParameterValues.Delete(value);
			}
		}

		private void TryCleanup(string subject, Action action)
		{
			try
			{
				action();
			}
			catch (Exception ex)
			{
				engine.GenerateInformation($"RT_ServiceOrderItemConfiguration_Edit: cleanup failed for {subject}: {ex}");
			}
		}
	}
}

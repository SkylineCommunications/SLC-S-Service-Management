namespace SLC_SM_RT_ServiceConfiguration_EditDuplicate.TestCases
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using DomHelpers.SlcConfigurations;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using Skyline.DataMiner.SDM;

	using ConfigModels = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;
	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Owns the fixtures of the test and the state shared between its test cases.
	/// </summary>
	internal sealed class ServiceConfigurationEditDuplicateTestData
	{
		private const string CreateServiceInventoryScript = "SLC_SM_Create Service Inventory Item";

		private readonly IEngine engine;

		private readonly HashSet<string> duplicatedVersionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		private readonly HashSet<string> duplicatedConfigurationValueIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		private readonly HashSet<string> duplicatedParameterValueIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		private readonly HashSet<string> duplicatedServiceProfileIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		public ServiceConfigurationEditDuplicateTestData(IEngine engine)
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

		public string ConfigurationParameterId { get; private set; } = String.Empty;

		public string ParameterValueId { get; private set; } = String.Empty;

		public string ServiceConfigurationValueId { get; private set; } = String.Empty;

		public string ServiceConfigurationVersionId { get; private set; } = String.Empty;

		public string DuplicatedServiceId { get; private set; } = String.Empty;

		public string DuplicatedServiceName { get; private set; } = String.Empty;

		public string DuplicatedParameterValueId { get; private set; } = String.Empty;

		public string DuplicatedServiceConfigurationValueId { get; private set; } = String.Empty;

		public string DuplicatedServiceConfigurationVersionId { get; private set; } = String.Empty;

		public string EditedVersionName { get; private set; } = String.Empty;

		/// <summary>
		/// Creates the specification, order and order item fixtures and materialises the service under test.
		/// </summary>
		public void CreateFixtures()
		{
			var specification = new Models.ServiceSpecification
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = $"RT_ServiceConfigEditDup_Spec_{Guid.NewGuid()}",
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
				Name = $"RT_ServiceConfigEditDup_OrderItem_{Guid.NewGuid()}",
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
				Name = $"RT_ServiceConfigEditDup_Order_{Guid.NewGuid()}",
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
		/// Creates the configuration parameter, configuration parameter value, configuration item and configuration version,
		/// then links the version to the service and marks it as the active one.
		/// </summary>
		public void CreateAndLinkServiceConfiguration()
		{
			var parameter = new ConfigModels.ConfigurationParameter
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = $"RT_ServiceConfigEditDup_Parameter_{Guid.NewGuid()}",
				Type = SlcConfigurationsIds.Enums.Type.Text,
			};
			InventoryApi.ServiceCatalog.ConfigurationParameters.Create(parameter);
			ConfigurationParameterId = parameter.Identifier;

			var parameterValue = new ConfigModels.ConfigurationParameterValue
			{
				Identifier = Guid.NewGuid().ToString(),
				Label = $"RT_ServiceConfigEditDup_Value_{Guid.NewGuid()}",
				Type = parameter.Type,
				ConfigurationParameterId = new SdmObjectReference<ConfigModels.ConfigurationParameter>(parameter.Identifier),
			};

			// The item below is mandatory: a configuration version with an empty mandatory value cannot be activated.
			if (parameterValue.Type == SlcConfigurationsIds.Enums.Type.Number)
			{
				parameterValue.DoubleValue = 1;
			}
			else
			{
				parameterValue.StringValue = "RT value";
			}

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

			var serviceConfigurationVersion = new Models.ServiceConfigurationVersion
			{
				Identifier = Guid.NewGuid().ToString(),
				VersionName = $"RT_ServiceConfigEditDup_Version_{Guid.NewGuid()}",
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

			ActivateConfigurationVersion(serviceConfigurationVersion.Identifier, "Service was not found before linking configuration.");
		}

		/// <summary>
		/// Renames the source configuration version and changes its description.
		/// </summary>
		public void EditServiceConfiguration()
		{
			Models.ServiceConfigurationVersion version = ReadConfigurationVersion(ServiceConfigurationVersionId)
				?? throw new InvalidOperationException("Source service configuration version was not found for edit.");

			EditedVersionName = $"RT_ServiceConfigEditDup_Edited_{Guid.NewGuid()}";
			version.VersionName = EditedVersionName;
			version.Description = "Edited by RT.";
			InventoryApi.ServiceInventory.ServiceConfigurationVersions.CreateOrUpdate(new[] { version });
		}

		/// <summary>
		/// Duplicates the service through the product script ("Duplicate" action), which copies the whole
		/// configuration chain onto a new "(Copy)" service and activates the copy of the active version there.
		/// </summary>
		public void DuplicateServiceConfiguration()
		{
			Models.Service source = ReadService()
				?? throw new InvalidOperationException("Source service was not found for duplication.");

			DuplicatedServiceName = $"{source.Name} (Copy)";

			SubScriptOptions duplicateScript = engine.PrepareSubScript(CreateServiceInventoryScript);
			duplicateScript.Synchronous = true;
			duplicateScript.ExtendedErrorInfo = true;
			duplicateScript.SelectScriptParam("DOM ID", ServiceId);
			duplicateScript.SelectScriptParam("Action", "Duplicate");

			// A non-empty payload selects the non-interactive API flow of the script.
			duplicateScript.SelectScriptParam("Payload", "{}");
			duplicateScript.StartScript();

			// Resolve the duplicate before failing so that cleanup can remove whatever was created.
			Models.Service duplicate = ReadDuplicatedService();
			if (duplicate != null)
			{
				DuplicatedServiceId = duplicate.Identifier;
				CollectDuplicatedConfiguration(duplicate);
			}

			if (duplicateScript.HadError)
			{
				throw new InvalidOperationException(
					$"Subscript '{CreateServiceInventoryScript}' (Duplicate) failed:{Environment.NewLine}{String.Join(Environment.NewLine, duplicateScript.GetErrorMessages() ?? Enumerable.Empty<string>())}");
			}

			if (duplicate == null)
			{
				throw new InvalidOperationException($"No duplicated service named '{DuplicatedServiceName}' was created.");
			}

			DuplicatedServiceConfigurationVersionId = (duplicate.ServiceConfigurationId != null ? duplicate.ServiceConfigurationId.Identifier : null) ?? String.Empty;

			Models.ServiceConfigurationVersion duplicatedVersion = ReadConfigurationVersion(DuplicatedServiceConfigurationVersionId);
			DuplicatedServiceConfigurationValueId = duplicatedVersion?.Parameters?.Select(x => x.Identifier).FirstOrDefault() ?? String.Empty;
			DuplicatedParameterValueId = ReadConfigurationValue(DuplicatedServiceConfigurationValueId)?.ConfigurationParameterId.Identifier ?? String.Empty;
		}

		/// <summary>
		/// Reads the duplicated service back from the system.
		/// </summary>
		/// <returns>The duplicated service, or <c>null</c> when it does not exist.</returns>
		public Models.Service ReadDuplicatedService()
		{
			if (!String.IsNullOrWhiteSpace(DuplicatedServiceId))
			{
				return InventoryApi.ServiceInventory.Services
					.Read(Models.ServiceExposers.Identifier.Equal(DuplicatedServiceId))
					.FirstOrDefault();
			}

			if (String.IsNullOrWhiteSpace(DuplicatedServiceName))
			{
				return null;
			}

			return InventoryApi.ServiceInventory.Services
				.Read(Models.ServiceExposers.Name.Equal(DuplicatedServiceName))
				.FirstOrDefault();
		}

		/// <summary>
		/// Indicates whether the configuration parameter value holds a value, using the same rule as the product
		/// applies before a configuration version is activated.
		/// </summary>
		/// <param name="value">The configuration parameter value.</param>
		/// <returns><c>true</c> when a value is filled in.</returns>
		public static bool HasValue(ConfigModels.ConfigurationParameterValue value)
		{
			if (value == null)
			{
				return false;
			}

			return value.Type == SlcConfigurationsIds.Enums.Type.Number
				? value.DoubleValue.HasValue
				: !String.IsNullOrWhiteSpace(value.StringValue);
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
			if (String.IsNullOrWhiteSpace(ServiceId))
			{
				return null;
			}

			return InventoryApi.ServiceInventory.Services
				.Read(Models.ServiceExposers.Identifier.Equal(ServiceId))
				.FirstOrDefault();
		}

		/// <summary>
		/// Reads a Service Configuration Version back from the system.
		/// </summary>
		/// <param name="identifier">The identifier of the configuration version.</param>
		/// <returns>The configuration version, or <c>null</c> when it does not exist.</returns>
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

		/// <summary>
		/// Reads a Service Configuration Item back from the system.
		/// </summary>
		/// <param name="identifier">The identifier of the service configuration value.</param>
		/// <returns>The service configuration value, or <c>null</c> when it does not exist.</returns>
		public Models.ServiceConfigurationValue ReadConfigurationValue(string identifier)
		{
			if (String.IsNullOrWhiteSpace(identifier))
			{
				return null;
			}

			return InventoryApi.ServiceInventory.ServiceConfigurationValues
				.Read(new TRUEFilterElement<Models.ServiceConfigurationValue>())
				.FirstOrDefault(x => String.Equals(x.Identifier, identifier, StringComparison.OrdinalIgnoreCase));
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

			return InventoryApi.ServiceCatalog.ConfigurationParameterValues
				.Read(ConfigModels.ConfigurationParameterValueExposers.Identifier.Equal(identifier))
				.FirstOrDefault();
		}

		/// <summary>
		/// Removes everything this test created, duplicates before sources.
		/// A failing step never blocks the remaining steps.
		/// </summary>
		public void CleanUp()
		{
			TryCleanup(
				"duplicated service",
				() =>
				{
					Models.Service duplicate = ReadDuplicatedService();
					if (duplicate != null)
					{
						CollectDuplicatedConfiguration(duplicate);
						InventoryApi.ServiceInventory.Services.Delete(duplicate);
					}
				});

			TryCleanup(
				"service",
				() =>
				{
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

			foreach (string id in duplicatedVersionIds.ToList())
			{
				TryCleanup($"duplicated configuration version {id}", () => DeleteConfigurationVersion(id));
			}

			TryCleanup("source configuration version", () => DeleteConfigurationVersion(ServiceConfigurationVersionId));

			foreach (string id in duplicatedServiceProfileIds.ToList())
			{
				TryCleanup($"duplicated service profile {id}", () => DeleteServiceProfile(id));
			}

			foreach (string id in duplicatedConfigurationValueIds.ToList())
			{
				TryCleanup($"duplicated configuration item {id}", () => DeleteConfigurationItem(id));
			}

			TryCleanup("source configuration item", () => DeleteConfigurationItem(ServiceConfigurationValueId));

			foreach (string id in duplicatedParameterValueIds.ToList())
			{
				TryCleanup($"duplicated parameter value {id}", () => DeleteParameterValue(id));
			}

			TryCleanup("source parameter value", () => DeleteParameterValue(ParameterValueId));
			TryCleanup("configuration parameter", DeleteConfigurationParameter);
		}

		private void DeleteConfigurationParameter()
		{
			if (String.IsNullOrWhiteSpace(ConfigurationParameterId))
			{
				return;
			}

			ConfigModels.ConfigurationParameter parameter = InventoryApi.ServiceCatalog.ConfigurationParameters
				.Read(ConfigModels.ConfigurationParameterExposers.Identifier.Equal(ConfigurationParameterId))
				.FirstOrDefault();
			if (parameter != null)
			{
				InventoryApi.ServiceCatalog.ConfigurationParameters.Delete(parameter);
			}
		}

		private void CollectDuplicatedConfiguration(Models.Service duplicate)
		{
			var versionIds = new List<string>();
			if (duplicate.ServiceConfigurationId != null)
			{
				versionIds.Add(duplicate.ServiceConfigurationId.Identifier);
			}

			if (duplicate.ConfigurationVersions != null)
			{
				versionIds.AddRange(duplicate.ConfigurationVersions.Where(x => x != null).Select(x => x.Identifier));
			}

			foreach (string versionId in versionIds.Where(id => !String.IsNullOrWhiteSpace(id)))
			{
				duplicatedVersionIds.Add(versionId);

				Models.ServiceConfigurationVersion version = ReadConfigurationVersion(versionId);
				if (version == null)
				{
					continue;
				}

				foreach (var profile in version.Profiles ?? new List<SdmObjectReference<Models.ServiceProfile>>())
				{
					if (!String.IsNullOrWhiteSpace(profile.Identifier))
					{
						duplicatedServiceProfileIds.Add(profile.Identifier);
					}
				}

				foreach (var parameter in version.Parameters ?? new List<SdmObjectReference<Models.ServiceConfigurationValue>>())
				{
					if (String.IsNullOrWhiteSpace(parameter.Identifier))
					{
						continue;
					}

					duplicatedConfigurationValueIds.Add(parameter.Identifier);

					string parameterValueId = ReadConfigurationValue(parameter.Identifier)?.ConfigurationParameterId.Identifier;
					if (!String.IsNullOrWhiteSpace(parameterValueId))
					{
						duplicatedParameterValueIds.Add(parameterValueId);
					}
				}
			}

			// Never delete the source objects through the duplicate bookkeeping.
			duplicatedVersionIds.Remove(ServiceConfigurationVersionId);
			duplicatedConfigurationValueIds.Remove(ServiceConfigurationValueId);
			duplicatedParameterValueIds.Remove(ParameterValueId);
		}

		private void DeleteServiceProfile(string identifier)
		{
			Models.ServiceProfile profile = InventoryApi.ServiceInventory.ServiceProfiles
				.Read(new TRUEFilterElement<Models.ServiceProfile>())
				.FirstOrDefault(x => String.Equals(x.Identifier, identifier, StringComparison.OrdinalIgnoreCase));
			if (profile != null)
			{
				InventoryApi.ServiceInventory.ServiceProfiles.Delete(profile);
			}
		}

		private void ActivateConfigurationVersion(string configurationVersionId, string notFoundMessage)
		{
			Models.Service service = ReadService()
				?? throw new InvalidOperationException(notFoundMessage);

			if (service.ConfigurationVersions == null)
			{
				service.ConfigurationVersions = new List<SdmObjectReference<Models.ServiceConfigurationVersion>>();
			}

			service.ConfigurationVersions.Add(new SdmObjectReference<Models.ServiceConfigurationVersion>(configurationVersionId));
			service.ServiceConfigurationId = new SdmObjectReference<Models.ServiceConfigurationVersion>(configurationVersionId);
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
				?? throw new InvalidOperationException("Order item fixture was not found after creating service.");

			if (orderItem.ServiceInfo == null
				|| orderItem.ServiceInfo.ServiceId == null
				|| String.IsNullOrWhiteSpace(orderItem.ServiceInfo.ServiceId.Identifier))
			{
				throw new InvalidOperationException("No linked service was created on the order item.");
			}

			ServiceId = orderItem.ServiceInfo.ServiceId.Identifier;
		}

		private void DeleteConfigurationVersion(string identifier)
		{
			Models.ServiceConfigurationVersion version = ReadConfigurationVersion(identifier);
			if (version != null)
			{
				InventoryApi.ServiceInventory.ServiceConfigurationVersions.Delete(version);
			}
		}

		private void DeleteConfigurationItem(string identifier)
		{
			Models.ServiceConfigurationValue value = ReadConfigurationValue(identifier);
			if (value != null)
			{
				InventoryApi.ServiceInventory.ServiceConfigurationValues.Delete(value);
			}
		}

		private void DeleteParameterValue(string identifier)
		{
			ConfigModels.ConfigurationParameterValue value = ReadParameterValue(identifier);
			if (value != null)
			{
				InventoryApi.ServiceCatalog.ConfigurationParameterValues.Delete(value);
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
				engine.GenerateInformation($"RT_ServiceConfiguration_EditDuplicate: cleanup failed for {subject}: {ex}");
			}
		}
	}
}

namespace SLC_SM_RT_ServiceConfigurationItem_EditDuplicate.TestCases
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
	internal sealed class ServiceConfigurationItemEditDuplicateTestData
	{
		private readonly IEngine engine;

		public ServiceConfigurationItemEditDuplicateTestData(IEngine engine)
		{
			this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
			Api = engine.GetUserConnection().GetServiceManagementApiHelper("Service Inventory");
		}

		public IServiceManagementApiHelper Api { get; }

		public string ConfigurationParameterId { get; private set; } = String.Empty;

		public string ParameterValueId { get; private set; } = String.Empty;

		public string ServiceConfigurationValueId { get; private set; } = String.Empty;

		public string DuplicatedParameterValueId { get; private set; } = String.Empty;

		public string DuplicatedServiceConfigurationValueId { get; private set; } = String.Empty;

		/// <summary>
		/// Creates the Configuration Parameter, the Configuration Parameter Value and the Service Configuration Item under test.
		/// </summary>
		public void CreateConfigurationItem()
		{
			var parameter = new ConfigModels.ConfigurationParameter
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = $"RT_ServiceCfgItemEditDup_Parameter_{Guid.NewGuid()}",
				Type = SlcConfigurationsIds.Enums.Type.Text,
			};
			Api.ServiceCatalog.ConfigurationParameters.Create(parameter);
			ConfigurationParameterId = parameter.Identifier;

			var parameterValue = new ConfigModels.ConfigurationParameterValue
			{
				Identifier = Guid.NewGuid().ToString(),
				Label = $"RT_ServiceCfgItemEditDup_Value_{Guid.NewGuid()}",
				Type = parameter.Type,
				ConfigurationParameterId = new SdmObjectReference<ConfigModels.ConfigurationParameter>(parameter.Identifier),
			};

			// The item is made mandatory later on; mandatory items need a value before their configuration can be activated.
			if (parameterValue.Type == SlcConfigurationsIds.Enums.Type.Number)
			{
				parameterValue.DoubleValue = 1;
			}
			else
			{
				parameterValue.StringValue = "RT value";
			}

			Api.ServiceCatalog.ConfigurationParameterValues.Create(parameterValue);
			ParameterValueId = parameterValue.Identifier;

			var serviceConfigurationValue = new Models.ServiceConfigurationValue
			{
				Identifier = Guid.NewGuid().ToString(),
				Mandatory = false,
				ConfigurationParameterId = new SdmObjectReference<ConfigModels.ConfigurationParameterValue>(parameterValue.Identifier),
			};
			Api.ServiceInventory.ServiceConfigurationValues.Create(serviceConfigurationValue);
			ServiceConfigurationValueId = serviceConfigurationValue.Identifier;
		}

		/// <summary>
		/// Flips the Mandatory flag of the source Service Configuration Item from false to true.
		/// </summary>
		public void EditConfigurationItem()
		{
			Models.ServiceConfigurationValue value = ReadServiceConfigurationValue(ServiceConfigurationValueId)
				?? throw new InvalidOperationException("Source service configuration item was not found for edit.");

			value.Mandatory = true;
			Api.ServiceInventory.ServiceConfigurationValues.Update(value);
		}

		/// <summary>
		/// Creates a copy of the parameter value and of the edited Service Configuration Item,
		/// the same way the product duplicates configuration items (label and value are kept).
		/// </summary>
		public void DuplicateConfigurationItem()
		{
			ConfigModels.ConfigurationParameterValue sourceParameterValue = ReadParameterValue(ParameterValueId)
				?? throw new InvalidOperationException("Source parameter value was not found for duplication.");

			Models.ServiceConfigurationValue sourceServiceConfigurationValue = ReadServiceConfigurationValue(ServiceConfigurationValueId)
				?? throw new InvalidOperationException("Source service configuration item was not found for duplication.");

			var duplicatedParameterValue = new ConfigModels.ConfigurationParameterValue
			{
				Identifier = Guid.NewGuid().ToString(),
				Label = sourceParameterValue.Label,
				Type = sourceParameterValue.Type,
				DoubleValue = sourceParameterValue.DoubleValue,
				StringValue = sourceParameterValue.StringValue,
				ConfigurationParameterId = sourceParameterValue.ConfigurationParameterId,
				NumberOptionsId = sourceParameterValue.NumberOptionsId,
				DiscreteOptionsId = sourceParameterValue.DiscreteOptionsId,
				TextOptionsId = sourceParameterValue.TextOptionsId,
				ValueFixed = sourceParameterValue.ValueFixed,
				LinkedConsumers = sourceParameterValue.LinkedConsumers != null ? new List<Guid>(sourceParameterValue.LinkedConsumers) : new List<Guid>(),
				LinkedScript = sourceParameterValue.LinkedScript,
				IsLinked = sourceParameterValue.IsLinked,
			};
			Api.ServiceCatalog.ConfigurationParameterValues.Create(duplicatedParameterValue);
			DuplicatedParameterValueId = duplicatedParameterValue.Identifier;

			var duplicatedServiceConfigurationValue = new Models.ServiceConfigurationValue
			{
				Identifier = Guid.NewGuid().ToString(),
				Mandatory = sourceServiceConfigurationValue.Mandatory,
				ConfigurationParameterId = new SdmObjectReference<ConfigModels.ConfigurationParameterValue>(duplicatedParameterValue.Identifier),
			};
			Api.ServiceInventory.ServiceConfigurationValues.Create(duplicatedServiceConfigurationValue);
			DuplicatedServiceConfigurationValueId = duplicatedServiceConfigurationValue.Identifier;
		}

		/// <summary>
		/// Reads a Service Configuration Item back from the system.
		/// </summary>
		/// <param name="identifier">The identifier of the service configuration value.</param>
		/// <returns>The service configuration value, or <c>null</c> when it does not exist.</returns>
		public Models.ServiceConfigurationValue ReadServiceConfigurationValue(string identifier)
		{
			if (String.IsNullOrWhiteSpace(identifier))
			{
				return null;
			}

			return Api.ServiceInventory.ServiceConfigurationValues
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

			return Api.ServiceCatalog.ConfigurationParameterValues
				.Read(ConfigModels.ConfigurationParameterValueExposers.Identifier.Equal(identifier))
				.FirstOrDefault();
		}

		/// <summary>
		/// Removes everything this test created, duplicates first.
		/// A failing step never blocks the remaining steps.
		/// </summary>
		public void CleanUp()
		{
			TryCleanup("duplicated service configuration item", () => DeleteServiceConfigurationItem(DuplicatedServiceConfigurationValueId));
			TryCleanup("source service configuration item", () => DeleteServiceConfigurationItem(ServiceConfigurationValueId));
			TryCleanup("duplicated configuration parameter value", () => DeleteConfigurationParameterValue(DuplicatedParameterValueId));
			TryCleanup("source configuration parameter value", () => DeleteConfigurationParameterValue(ParameterValueId));
			TryCleanup("configuration parameter", DeleteConfigurationParameter);
		}

		private void DeleteConfigurationParameter()
		{
			if (String.IsNullOrWhiteSpace(ConfigurationParameterId))
			{
				return;
			}

			ConfigModels.ConfigurationParameter parameter = Api.ServiceCatalog.ConfigurationParameters
				.Read(ConfigModels.ConfigurationParameterExposers.Identifier.Equal(ConfigurationParameterId))
				.FirstOrDefault();
			if (parameter != null)
			{
				Api.ServiceCatalog.ConfigurationParameters.Delete(parameter);
			}
		}

		private void DeleteServiceConfigurationItem(string identifier)
		{
			Models.ServiceConfigurationValue value = ReadServiceConfigurationValue(identifier);
			if (value != null)
			{
				Api.ServiceInventory.ServiceConfigurationValues.Delete(value);
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
				engine.GenerateInformation($"RT_ServiceConfigurationItem_EditDuplicate: cleanup failed for {subject}: {ex}");
			}
		}
	}
}

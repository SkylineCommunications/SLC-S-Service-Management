namespace SLC_SM_RT_ServiceConfigurationItem.TestCases
{
	using System;
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
	internal sealed class ServiceConfigurationItemTestData
	{
		private readonly IEngine engine;

		public ServiceConfigurationItemTestData(IEngine engine)
		{
			this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
			Api = engine.GetUserConnection().GetServiceManagementApiHelper("Service Inventory");
		}

		public IServiceManagementApiHelper Api { get; }

		public string ParameterValueId { get; private set; } = String.Empty;

		public string ServiceConfigurationValueId { get; private set; } = String.Empty;

		/// <summary>
		/// Creates a Configuration Parameter Value and the Service Configuration Item that refers to it.
		/// </summary>
		public void CreateConfigurationItem()
		{
			ConfigModels.ConfigurationParameter parameter = Api.ServiceCatalog.ConfigurationParameters
				.Read(new TRUEFilterElement<ConfigModels.ConfigurationParameter>())
				.FirstOrDefault()
				?? throw new InvalidOperationException("No Configuration Parameters exist in Service Catalog.");

			var parameterValue = new ConfigModels.ConfigurationParameterValue
			{
				Identifier = Guid.NewGuid().ToString(),
				Label = $"RT_ServiceConfigItem_Value_{Guid.NewGuid()}",
				Type = parameter.Type,
				ConfigurationParameterId = new SdmObjectReference<ConfigModels.ConfigurationParameter>(parameter.Identifier),
			};
			Api.ServiceCatalog.ConfigurationParameterValues.Create(parameterValue);
			ParameterValueId = parameterValue.Identifier;

			var serviceConfigurationValue = new Models.ServiceConfigurationValue
			{
				Identifier = Guid.NewGuid().ToString(),
				Mandatory = false,
				ConfigurationParameterId = new SdmObjectReference<ConfigModels.ConfigurationParameter>(parameterValue.Identifier),
			};
			Api.ServiceInventory.ServiceConfigurationValues.Create(serviceConfigurationValue);
			ServiceConfigurationValueId = serviceConfigurationValue.Identifier;
		}

		/// <summary>
		/// Flips the Mandatory flag of the Service Configuration Item from false to true.
		/// </summary>
		public void UpdateConfigurationItem()
		{
			Models.ServiceConfigurationValue persisted = ReadServiceConfigurationValue()
				?? throw new InvalidOperationException("Service configuration item was not found before update.");

			persisted.Mandatory = true;
			Api.ServiceInventory.ServiceConfigurationValues.Update(persisted);
		}

		/// <summary>
		/// Reads the Service Configuration Item back from the system.
		/// </summary>
		/// <returns>The service configuration value, or <c>null</c> when it does not exist.</returns>
		public Models.ServiceConfigurationValue ReadServiceConfigurationValue()
		{
			return Api.ServiceInventory.ServiceConfigurationValues
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
				"service configuration item",
				() =>
				{
					if (String.IsNullOrWhiteSpace(ServiceConfigurationValueId))
					{
						return;
					}

					Models.ServiceConfigurationValue value = ReadServiceConfigurationValue();
					if (value != null)
					{
						Api.ServiceInventory.ServiceConfigurationValues.Delete(value);
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
		}

		private void TryCleanup(string subject, Action action)
		{
			try
			{
				action();
			}
			catch (Exception ex)
			{
				engine.GenerateInformation($"RT_ServiceConfigurationItem: cleanup failed for {subject}: {ex}");
			}
		}
	}
}

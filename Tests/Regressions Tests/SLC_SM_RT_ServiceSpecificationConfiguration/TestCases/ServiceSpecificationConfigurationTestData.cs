namespace SLC_SM_RT_ServiceSpecificationConfiguration.TestCases
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
	internal sealed class ServiceSpecificationConfigurationTestData
	{
		private readonly IEngine engine;

		public ServiceSpecificationConfigurationTestData(IEngine engine)
		{
			this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
			Api = engine.GetUserConnection().GetServiceManagementApiHelper("Service Ordering");
		}

		public IServiceManagementApiHelper Api { get; }

		public string SpecificationId { get; private set; } = String.Empty;

		public string ParameterValueId { get; private set; } = String.Empty;

		public string SpecificationConfigurationId { get; private set; } = String.Empty;

		/// <summary>
		/// Creates the Service Specification the configuration is linked to.
		/// </summary>
		public void CreateSpecification()
		{
			var specification = new Models.ServiceSpecification
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = $"RT_SpecConfig_Spec_{Guid.NewGuid()}",
				Description = "Temporary fixture for regression testing.",
				ServiceItems = new List<Models.ServiceItem> { new Models.ServiceItem() },
				ServiceItemsRelationships = new List<Models.ServiceItemRelationship> { new Models.ServiceItemRelationship() },
			};

			Api.ServiceCatalog.ServiceSpecifications.Create(specification);
			SpecificationId = specification.Identifier;
		}

		/// <summary>
		/// Creates a Configuration Parameter Value plus a Service Specification Configuration Value,
		/// and links the latter to the specification.
		/// </summary>
		public void CreateAndLinkSpecificationConfiguration()
		{
			ConfigModels.ConfigurationParameter parameter = Api.ServiceCatalog.ConfigurationParameters
				.Read(new TRUEFilterElement<ConfigModels.ConfigurationParameter>())
				.FirstOrDefault()
				?? throw new InvalidOperationException("No Configuration Parameters exist in Service Catalog.");

			var parameterValue = new ConfigModels.ConfigurationParameterValue
			{
				Identifier = Guid.NewGuid().ToString(),
				Label = $"RT_SpecConfig_Value_{Guid.NewGuid()}",
				Type = parameter.Type,
				ConfigurationParameterId = new SdmObjectReference<ConfigModels.ConfigurationParameter>(parameter.Identifier),
			};
			Api.ServiceCatalog.ConfigurationParameterValues.Create(parameterValue);
			ParameterValueId = parameterValue.Identifier;

			var specificationConfiguration = new Models.ServiceSpecificationConfigurationValue
			{
				Identifier = Guid.NewGuid().ToString(),
				ExposeAtServiceOrder = true,
				MandatoryAtServiceOrder = false,
				MandatoryAtService = false,
				ConfigurationParameterId = new SdmObjectReference<ConfigModels.ConfigurationParameter>(parameterValue.Identifier),
			};
			Api.ServiceCatalog.ServiceSpecificationConfigurationValues.Create(specificationConfiguration);
			SpecificationConfigurationId = specificationConfiguration.Identifier;

			Models.ServiceSpecification specification = ReadSpecification()
				?? throw new InvalidOperationException("Specification was not found before linking configuration.");

			if (specification.ConfigurationParameters == null)
			{
				specification.ConfigurationParameters = new List<SdmObjectReference<Models.ServiceSpecificationConfigurationValue>>();
			}

			specification.ConfigurationParameters.Add(new SdmObjectReference<Models.ServiceSpecificationConfigurationValue>(specificationConfiguration.Identifier));
			Api.ServiceCatalog.ServiceSpecifications.Update(specification);
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
		/// Reads the Service Specification Configuration Value back from the system.
		/// </summary>
		/// <returns>The specification configuration value, or <c>null</c> when it does not exist.</returns>
		public Models.ServiceSpecificationConfigurationValue ReadSpecificationConfiguration()
		{
			return Api.ServiceCatalog.ServiceSpecificationConfigurationValues
				.Read(Models.ServiceSpecificationConfigurationValueExposers.Identifier.Equal(SpecificationConfigurationId))
				.FirstOrDefault();
		}

		/// <summary>
		/// Removes everything this test created, starting with the link on the specification.
		/// A failing step never blocks the remaining steps.
		/// </summary>
		public void CleanUp()
		{
			TryCleanup(
				"specification link",
				() =>
				{
					if (String.IsNullOrWhiteSpace(SpecificationId) || String.IsNullOrWhiteSpace(SpecificationConfigurationId))
					{
						return;
					}

					Models.ServiceSpecification specification = ReadSpecification();
					if (specification == null || specification.ConfigurationParameters == null)
					{
						return;
					}

					specification.ConfigurationParameters.RemoveAll(x => x.Identifier == SpecificationConfigurationId);
					Api.ServiceCatalog.ServiceSpecifications.Update(specification);
				});

			TryCleanup(
				"specification configuration",
				() =>
				{
					if (String.IsNullOrWhiteSpace(SpecificationConfigurationId))
					{
						return;
					}

					Models.ServiceSpecificationConfigurationValue specificationConfiguration = ReadSpecificationConfiguration();
					if (specificationConfiguration != null)
					{
						Api.ServiceCatalog.ServiceSpecificationConfigurationValues.Delete(specificationConfiguration);
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
				engine.GenerateInformation($"RT_ServiceSpecificationConfiguration: cleanup failed for {subject}: {ex}");
			}
		}
	}
}

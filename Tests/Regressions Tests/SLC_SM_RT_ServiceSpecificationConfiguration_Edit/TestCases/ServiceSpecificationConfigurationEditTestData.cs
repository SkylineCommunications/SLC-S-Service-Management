namespace SLC_SM_RT_ServiceSpecificationConfiguration_Edit.TestCases
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
	internal sealed class ServiceSpecificationConfigurationEditTestData
	{
		private readonly IEngine engine;

		public ServiceSpecificationConfigurationEditTestData(IEngine engine)
		{
			this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
			Api = engine.GetUserConnection().GetServiceManagementApiHelper("Service Ordering");
		}

		public IServiceManagementApiHelper Api { get; }

		public string SpecificationId { get; private set; } = String.Empty;

		public string ParameterValueId { get; private set; } = String.Empty;

		public string SpecificationConfigurationId { get; private set; } = String.Empty;

		public string EditedLabel { get; private set; } = String.Empty;

		/// <summary>
		/// Creates the Service Specification the configurations are linked to.
		/// </summary>
		public void CreateSpecification()
		{
			var specification = new Models.ServiceSpecification
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = $"RT_SpecCfgEdit_Spec_{Guid.NewGuid()}",
				Description = "Temporary fixture for regression testing.",
				ServiceItems = new List<Models.ServiceItem> { new Models.ServiceItem() },
				ServiceItemsRelationships = new List<Models.ServiceItemRelationship> { new Models.ServiceItemRelationship() },
			};

			Api.ServiceCatalog.ServiceSpecifications.Create(specification);
			SpecificationId = specification.Identifier;
		}

		/// <summary>
		/// Creates the parameter value and specification configuration, and links the latter to the specification.
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
				Label = $"RT_SpecCfgEdit_Value_{Guid.NewGuid()}",
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

			LinkConfigurationToSpecification(specificationConfiguration.Identifier, "Specification was not found while linking configuration.");
		}

		/// <summary>
		/// Renames the parameter value and inverts the exposure and mandatory flags of the specification configuration.
		/// </summary>
		public void EditConfiguration()
		{
			ConfigModels.ConfigurationParameterValue parameterValue = ReadParameterValue(ParameterValueId)
				?? throw new InvalidOperationException("Configuration parameter value to edit was not found.");

			EditedLabel = $"RT_SpecCfgEdit_Edited_{Guid.NewGuid()}";
			parameterValue.Label = EditedLabel;
			Api.ServiceCatalog.ConfigurationParameterValues.CreateOrUpdate(new[] { parameterValue });

			Models.ServiceSpecificationConfigurationValue configuration = ReadSpecificationConfiguration(SpecificationConfigurationId)
				?? throw new InvalidOperationException("Specification configuration to edit was not found.");

			configuration.ExposeAtServiceOrder = false;
			configuration.MandatoryAtServiceOrder = true;
			configuration.MandatoryAtService = true;
			Api.ServiceCatalog.ServiceSpecificationConfigurationValues.CreateOrUpdate(new[] { configuration });
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
		/// Reads a Service Specification Configuration Value back from the system.
		/// </summary>
		/// <param name="identifier">The identifier of the specification configuration.</param>
		/// <returns>The specification configuration value, or <c>null</c> when it does not exist.</returns>
		public Models.ServiceSpecificationConfigurationValue ReadSpecificationConfiguration(string identifier)
		{
			if (String.IsNullOrWhiteSpace(identifier))
			{
				return null;
			}

			return Api.ServiceCatalog.ServiceSpecificationConfigurationValues
				.Read(Models.ServiceSpecificationConfigurationValueExposers.Identifier.Equal(identifier))
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
		/// Removes everything this test created, starting with the links on the specification.
		/// A failing step never blocks the remaining steps.
		/// </summary>
		public void CleanUp()
		{
			TryCleanup(
				"specification links",
				() =>
				{
					if (String.IsNullOrWhiteSpace(SpecificationId))
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

			TryCleanup("specification configuration", () => DeleteSpecificationConfiguration(SpecificationConfigurationId));
			TryCleanup("configuration parameter value", () => DeleteConfigurationParameterValue(ParameterValueId));

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

		private void LinkConfigurationToSpecification(string specificationConfigurationId, string notFoundMessage)
		{
			Models.ServiceSpecification specification = ReadSpecification()
				?? throw new InvalidOperationException(notFoundMessage);

			if (specification.ConfigurationParameters == null)
			{
				specification.ConfigurationParameters = new List<SdmObjectReference<Models.ServiceSpecificationConfigurationValue>>();
			}

			specification.ConfigurationParameters.Add(new SdmObjectReference<Models.ServiceSpecificationConfigurationValue>(specificationConfigurationId));
			Api.ServiceCatalog.ServiceSpecifications.Update(specification);
		}

		private void DeleteSpecificationConfiguration(string identifier)
		{
			Models.ServiceSpecificationConfigurationValue value = ReadSpecificationConfiguration(identifier);
			if (value != null)
			{
				Api.ServiceCatalog.ServiceSpecificationConfigurationValues.Delete(value);
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
				engine.GenerateInformation($"RT_ServiceSpecificationConfiguration_Edit: cleanup failed for {subject}: {ex}");
			}
		}
	}
}

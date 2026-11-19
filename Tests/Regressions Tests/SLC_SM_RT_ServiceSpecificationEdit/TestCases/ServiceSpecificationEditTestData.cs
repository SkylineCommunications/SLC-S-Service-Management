namespace SLC_SM_RT_ServiceSpecificationEdit.TestCases
{
	using System;
	using System.Collections.Generic;
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
	internal sealed class ServiceSpecificationEditTestData
	{
		private const string SourceValue = "RT source value";

		private readonly IEngine engine;

		public ServiceSpecificationEditTestData(IEngine engine)
		{
			this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
			CatalogApi = engine.GetUserConnection().GetServiceManagementApiHelper("Service Catalog");
			DefinitionReference = $"RT_SpecEdit_Item_{Guid.NewGuid()}";
			EditedName = $"RT_SpecEdit_Edited_{Guid.NewGuid()}";
			EditedDescription = $"Edited by RT {Guid.NewGuid()}";
			EditedIcon = "RT_Icon";
		}

		public IServiceManagementApiHelper CatalogApi { get; }

		public string DefinitionReference { get; }

		public string EditedName { get; }

		public string EditedDescription { get; }

		public string EditedIcon { get; }

		public string ExpectedSourceValue => SourceValue;

		public string SpecificationId { get; private set; } = String.Empty;

		public string ConfigurationParameterId { get; private set; } = String.Empty;

		public string ParameterValueId { get; private set; } = String.Empty;

		public string SpecificationConfigurationValueId { get; private set; } = String.Empty;

		public string RelationshipId { get; } = Guid.NewGuid().ToString();

		/// <summary>
		/// Builds the non-interactive "Edit" payload of the script under test.
		/// </summary>
		/// <param name="name">The new name.</param>
		/// <param name="description">The new description.</param>
		/// <param name="icon">The new icon.</param>
		/// <returns>The JSON payload.</returns>
		public static string BuildEditPayload(string name, string description, string icon)
		{
			return JsonConvert.SerializeObject(new { Name = name, Description = description, Icon = icon });
		}

		/// <summary>
		/// Creates the configuration parameter, its value, the specification configuration item and the specification.
		/// </summary>
		public void CreateFixtures()
		{
			var parameter = new ConfigModels.ConfigurationParameter
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = $"RT_SpecEdit_Parameter_{Guid.NewGuid()}",
				Type = SlcConfigurationsIds.Enums.Type.Text,
			};
			CatalogApi.ServiceCatalog.ConfigurationParameters.Create(parameter);
			ConfigurationParameterId = parameter.Identifier;

			var parameterValue = new ConfigModels.ConfigurationParameterValue
			{
				Identifier = Guid.NewGuid().ToString(),
				Label = $"RT_SpecEdit_Value_{Guid.NewGuid()}",
				Type = parameter.Type,
				StringValue = SourceValue,
				ConfigurationParameterId = new SdmObjectReference<ConfigModels.ConfigurationParameter>(parameter.Identifier),
			};
			CatalogApi.ServiceCatalog.ConfigurationParameterValues.Create(parameterValue);
			ParameterValueId = parameterValue.Identifier;

			// Like the product, the specification configuration item references the configuration parameter value.
			var specificationConfigurationValue = new Models.ServiceSpecificationConfigurationValue
			{
				Identifier = Guid.NewGuid().ToString(),
				ConfigurationParameterId = new SdmObjectReference<ConfigModels.ConfigurationParameter>(parameterValue.Identifier),
				MandatoryAtService = true,
				MandatoryAtServiceOrder = false,
				ExposeAtServiceOrder = true,
			};
			CatalogApi.ServiceCatalog.ServiceSpecificationConfigurationValues.Create(specificationConfigurationValue);
			SpecificationConfigurationValueId = specificationConfigurationValue.Identifier;

			var specification = new Models.ServiceSpecification
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = $"RT_SpecEdit_Spec_{Guid.NewGuid()}",
				Description = "Temporary fixture for regression testing.",
				Icon = String.Empty,
				ServiceItems = new List<Models.ServiceItem> { new Models.ServiceItem() },
				ServiceItemsRelationships = new List<Models.ServiceItemRelationship> { new Models.ServiceItemRelationship { Id = RelationshipId } },
				ConfigurationParameters = new List<SdmObjectReference<Models.ServiceSpecificationConfigurationValue>>
				{
					new SdmObjectReference<Models.ServiceSpecificationConfigurationValue>(specificationConfigurationValue.Identifier),
				},
				ConfigurationProfiles = new List<SdmObjectReference<Models.ServiceSpecificationProfile>>(),
			};
			CatalogApi.ServiceCatalog.ServiceSpecifications.Create(specification);
			SpecificationId = specification.Identifier;

			if (ReadSpecification(SpecificationId) == null)
			{
				throw new InvalidOperationException($"Service Specification fixture '{SpecificationId}' was not found after creation.");
			}
		}

		public Models.ServiceSpecification ReadSpecification(string identifier)
		{
			if (String.IsNullOrWhiteSpace(identifier))
			{
				return null;
			}

			return CatalogApi.ServiceCatalog.ServiceSpecifications
				.Read(Models.ServiceSpecificationExposers.Identifier.Equal(identifier))
				.FirstOrDefault();
		}

		public Models.ServiceSpecificationConfigurationValue ReadSpecificationConfigurationValue(string identifier)
		{
			if (String.IsNullOrWhiteSpace(identifier))
			{
				return null;
			}

			return CatalogApi.ServiceCatalog.ServiceSpecificationConfigurationValues
				.Read(Models.ServiceSpecificationConfigurationValueExposers.Identifier.Equal(identifier))
				.FirstOrDefault();
		}

		public ConfigModels.ConfigurationParameterValue ReadParameterValue(string identifier)
		{
			if (String.IsNullOrWhiteSpace(identifier))
			{
				return null;
			}

			return CatalogApi.ServiceCatalog.ConfigurationParameterValues
				.Read(ConfigModels.ConfigurationParameterValueExposers.Identifier.Equal(identifier))
				.FirstOrDefault();
		}

		/// <summary>
		/// Removes everything this test created, in reverse order of creation.
		/// A failing step never blocks the remaining steps.
		/// </summary>
		public void CleanUp()
		{
			TryCleanup("specification", () => DeleteSpecification(SpecificationId));
			TryCleanup("specification configuration item", () => DeleteSpecificationConfigurationValue(SpecificationConfigurationValueId));
			TryCleanup("parameter value", () => DeleteParameterValue(ParameterValueId));
			TryCleanup("configuration parameter", DeleteConfigurationParameter);
		}

		private void DeleteSpecification(string identifier)
		{
			Models.ServiceSpecification specification = ReadSpecification(identifier);
			if (specification != null)
			{
				CatalogApi.ServiceCatalog.ServiceSpecifications.Delete(specification);
			}
		}

		private void DeleteSpecificationConfigurationValue(string identifier)
		{
			Models.ServiceSpecificationConfigurationValue value = ReadSpecificationConfigurationValue(identifier);
			if (value != null)
			{
				CatalogApi.ServiceCatalog.ServiceSpecificationConfigurationValues.Delete(value);
			}
		}

		private void DeleteParameterValue(string identifier)
		{
			ConfigModels.ConfigurationParameterValue value = ReadParameterValue(identifier);
			if (value != null)
			{
				CatalogApi.ServiceCatalog.ConfigurationParameterValues.Delete(value);
			}
		}

		private void DeleteConfigurationParameter()
		{
			if (String.IsNullOrWhiteSpace(ConfigurationParameterId))
			{
				return;
			}

			ConfigModels.ConfigurationParameter parameter = CatalogApi.ServiceCatalog.ConfigurationParameters
				.Read(ConfigModels.ConfigurationParameterExposers.Identifier.Equal(ConfigurationParameterId))
				.FirstOrDefault();
			if (parameter != null)
			{
				CatalogApi.ServiceCatalog.ConfigurationParameters.Delete(parameter);
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
				engine.GenerateInformation($"RT_ServiceSpecificationEdit: cleanup failed for {subject}: {ex}");
			}
		}
	}
}

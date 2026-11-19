namespace SLC_SM_RT_ServiceProfiles.TestCases
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
	internal sealed class ServiceProfilesTestData
	{
		public const string InitialProfileValue = "RT profile value v1";

		public const string EditedProfileValue = "RT profile value v2";

		public const string InitialStandaloneValue = "RT standalone value v1";

		public const string EditedStandaloneValue = "RT standalone value v2";

		private readonly IEngine engine;

		public ServiceProfilesTestData(IEngine engine)
		{
			this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
			CatalogApi = engine.GetUserConnection().GetServiceManagementApiHelper("Service Catalog");
			ProfileName = $"RT_ServiceProfiles_Profile_{Guid.NewGuid()}";
			ProfileValueLabel = $"RT_ServiceProfiles_ProfileValue_{Guid.NewGuid()}";
			StandaloneValueLabel = $"RT_ServiceProfiles_StandaloneValue_{Guid.NewGuid()}";
		}

		public IServiceManagementApiHelper CatalogApi { get; }

		public string ProfileName { get; }

		public string ProfileValueLabel { get; }

		public string StandaloneValueLabel { get; }

		public string ConfigurationParameterId { get; private set; } = String.Empty;

		public string ReferencedConfigurationParameterId { get; private set; } = String.Empty;

		public string ProfileDefinitionId { get; private set; } = String.Empty;

		public string SpecificationId { get; private set; } = String.Empty;

		public string SpecificationProfileId { get; private set; } = String.Empty;

		public string ProfileId { get; private set; } = String.Empty;

		public string ProfileParameterValueId { get; private set; } = String.Empty;

		public string SpecificationConfigurationValueId { get; private set; } = String.Empty;

		public string StandaloneParameterValueId { get; private set; } = String.Empty;

		/// <summary>
		/// Creates the configuration parameter, the profile definition referencing it and the specification.
		/// </summary>
		public void CreateFixtures()
		{
			var parameter = new ConfigModels.ConfigurationParameter
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = $"RT_ServiceProfiles_Parameter_{Guid.NewGuid()}",
				Type = SlcConfigurationsIds.Enums.Type.Text,
			};
			CatalogApi.ServiceCatalog.ConfigurationParameters.Create(parameter);
			ConfigurationParameterId = parameter.Identifier;

			// The SDM mapper does not write boolean fields that are false, but the DOM section requires both.
			// The script's API path ignores these flags (they only drive the interactive UI), so true is safe here.
			var referencedParameter = new ConfigModels.ReferencedConfigurationParameter
			{
				Identifier = Guid.NewGuid().ToString(),
				ConfigurationParameterId = new SdmObjectReference<ConfigModels.ConfigurationParameter>(parameter.Identifier),
				Mandatory = true,
				AllowMultiple = true,
			};
			CatalogApi.ServiceCatalog.ReferencedConfigurationParameters.Create(referencedParameter);
			ReferencedConfigurationParameterId = referencedParameter.Identifier;

			var definition = new ConfigModels.ProfileDefinition
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = $"RT_ServiceProfiles_Definition_{Guid.NewGuid()}",
				ConfigurationParameters = new List<SdmObjectReference<ConfigModels.ReferencedConfigurationParameter>>
				{
					new SdmObjectReference<ConfigModels.ReferencedConfigurationParameter>(referencedParameter.Identifier),
				},
			};
			CatalogApi.ServiceCatalog.ProfileDefinitions.Create(definition);
			ProfileDefinitionId = definition.Identifier;

			var specification = new Models.ServiceSpecification
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = $"RT_ServiceProfiles_Spec_{Guid.NewGuid()}",
				Description = "Temporary fixture for regression testing.",
				ServiceItems = new List<Models.ServiceItem> { new Models.ServiceItem() },
				ServiceItemsRelationships = new List<Models.ServiceItemRelationship> { new Models.ServiceItemRelationship() },
			};
			CatalogApi.ServiceCatalog.ServiceSpecifications.Create(specification);
			SpecificationId = specification.Identifier;

			if (ReadSpecification() == null)
			{
				throw new InvalidOperationException($"Service Specification fixture '{SpecificationId}' was not found after creation.");
			}
		}

		/// <summary>
		/// Builds the payload that creates (without identifiers) or updates (with identifiers) the profile and the standalone parameter.
		/// The script replaces the configuration of the specification with the payload, so the full set is always sent.
		/// </summary>
		/// <param name="profileValue">The value of the profile parameter.</param>
		/// <param name="standaloneValue">The value of the standalone parameter.</param>
		/// <param name="exposeAtServiceOrder">Whether the profile is exposed at service order level.</param>
		/// <param name="includeIdentifiers">Whether the identifiers of the existing objects are included.</param>
		/// <returns>The JSON payload.</returns>
		public string BuildSavePayload(string profileValue, string standaloneValue, bool exposeAtServiceOrder, bool includeIdentifiers)
		{
			var payload = new
			{
				Parameters = new[] { BuildStandaloneParameter(standaloneValue, includeIdentifiers) },
				Profiles = new[]
				{
					new
					{
						Id = includeIdentifiers ? SpecificationProfileId : null,
						Name = ProfileName,
						DefinitionId = ProfileDefinitionId,
						Reusable = false,
						MandatoryAtService = true,
						MandatoryAtServiceOrder = false,
						ExposeAtServiceOrder = exposeAtServiceOrder,
						NestedProfileIds = new string[0],
						NestedProfiles = new object[0],
						Parameters = new[]
						{
							new
							{
								ParameterId = ConfigurationParameterId,
								ParameterValueId = includeIdentifiers ? ProfileParameterValueId : null,
								Value = profileValue,
								Label = ProfileValueLabel,
								Mandatory = false,
							},
						},
					},
				},
			};

			return JsonConvert.SerializeObject(payload);
		}

		/// <summary>
		/// Builds the payload that deletes the profile and its parameter value and keeps the standalone parameter.
		/// </summary>
		/// <returns>The JSON payload.</returns>
		public string BuildDeletePayload()
		{
			var payload = new
			{
				Parameters = new[] { BuildStandaloneParameter(EditedStandaloneValue, includeIdentifiers: true) },
				Profiles = new object[0],
				DeletedProfileIds = new[] { SpecificationProfileId },
				DeletedProfileParameterIds = new[] { ProfileParameterValueId },
			};

			return JsonConvert.SerializeObject(payload);
		}

		/// <summary>
		/// Records the identifiers of the objects the script created, so validation can check they stay stable and cleanup can remove them.
		/// </summary>
		public void CaptureCreatedIds()
		{
			Models.ServiceSpecification specification = ReadSpecification();
			if (specification == null)
			{
				return;
			}

			SpecificationConfigurationValueId = specification.ConfigurationParameters?.Select(reference => reference.Identifier).FirstOrDefault() ?? String.Empty;
			StandaloneParameterValueId = ReadSpecificationConfigurationValue(SpecificationConfigurationValueId)?.ConfigurationParameterId.Identifier ?? String.Empty;

			SpecificationProfileId = specification.ConfigurationProfiles?.Select(reference => reference.Identifier).FirstOrDefault() ?? String.Empty;
			ProfileId = ReadSpecificationProfile(SpecificationProfileId)?.ProfileId.Identifier ?? String.Empty;
			ProfileParameterValueId = ReadProfile(ProfileId)?.ConfigurationParameterValues?.Select(reference => reference.Identifier).FirstOrDefault() ?? String.Empty;
		}

		public Models.ServiceSpecification ReadSpecification()
		{
			if (String.IsNullOrWhiteSpace(SpecificationId))
			{
				return null;
			}

			return CatalogApi.ServiceCatalog.ServiceSpecifications
				.Read(Models.ServiceSpecificationExposers.Identifier.Equal(SpecificationId))
				.FirstOrDefault();
		}

		public Models.ServiceSpecificationProfile ReadSpecificationProfile(string identifier)
		{
			if (String.IsNullOrWhiteSpace(identifier))
			{
				return null;
			}

			return CatalogApi.ServiceCatalog.ServiceSpecificationProfiles
				.Read(Models.ServiceSpecificationProfileExposers.Identifier.Equal(identifier))
				.FirstOrDefault();
		}

		public ConfigModels.Profile ReadProfile(string identifier)
		{
			if (String.IsNullOrWhiteSpace(identifier))
			{
				return null;
			}

			return CatalogApi.ServiceCatalog.Profiles
				.Read(ConfigModels.ProfileExposers.Identifier.Equal(identifier))
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
		/// Removes everything this test created, dependants before the objects they reference.
		/// A failing step never blocks the remaining steps.
		/// </summary>
		public void CleanUp()
		{
			TryCleanup("specification", () => Delete(CatalogApi.ServiceCatalog.ServiceSpecifications, ReadSpecification()));
			TryCleanup("specification profile", () => Delete(CatalogApi.ServiceCatalog.ServiceSpecificationProfiles, ReadSpecificationProfile(SpecificationProfileId)));

			// The script does not delete the profile behind a removed specification profile, so the test removes it.
			TryCleanup("profile", () => Delete(CatalogApi.ServiceCatalog.Profiles, ReadProfile(ProfileId)));
			TryCleanup("profile parameter value", () => Delete(CatalogApi.ServiceCatalog.ConfigurationParameterValues, ReadParameterValue(ProfileParameterValueId)));
			TryCleanup("specification configuration item", () => Delete(CatalogApi.ServiceCatalog.ServiceSpecificationConfigurationValues, ReadSpecificationConfigurationValue(SpecificationConfigurationValueId)));
			TryCleanup("standalone parameter value", () => Delete(CatalogApi.ServiceCatalog.ConfigurationParameterValues, ReadParameterValue(StandaloneParameterValueId)));
			TryCleanup(
				"profile definition",
				() => Delete(
					CatalogApi.ServiceCatalog.ProfileDefinitions,
					String.IsNullOrWhiteSpace(ProfileDefinitionId) ? null : CatalogApi.ServiceCatalog.ProfileDefinitions.Read(ConfigModels.ProfileDefinitionExposers.Identifier.Equal(ProfileDefinitionId)).FirstOrDefault()));
			TryCleanup(
				"referenced configuration parameter",
				() => Delete(
					CatalogApi.ServiceCatalog.ReferencedConfigurationParameters,
					String.IsNullOrWhiteSpace(ReferencedConfigurationParameterId) ? null : CatalogApi.ServiceCatalog.ReferencedConfigurationParameters.Read(ConfigModels.ReferencedConfigurationParameterExposers.Identifier.Equal(ReferencedConfigurationParameterId)).FirstOrDefault()));
			TryCleanup(
				"configuration parameter",
				() => Delete(
					CatalogApi.ServiceCatalog.ConfigurationParameters,
					String.IsNullOrWhiteSpace(ConfigurationParameterId) ? null : CatalogApi.ServiceCatalog.ConfigurationParameters.Read(ConfigModels.ConfigurationParameterExposers.Identifier.Equal(ConfigurationParameterId)).FirstOrDefault()));
		}

		private static void Delete<T>(IDeletableRepository<T> repository, T instance)
			where T : class
		{
			if (instance != null)
			{
				repository.Delete(instance);
			}
		}

		private object BuildStandaloneParameter(string value, bool includeIdentifiers)
		{
			return new
			{
				ConfigurationId = includeIdentifiers ? SpecificationConfigurationValueId : null,
				ParameterId = ConfigurationParameterId,
				ParameterValueId = includeIdentifiers ? StandaloneParameterValueId : null,
				Value = value,
				Label = StandaloneValueLabel,
				Mandatory = false,
				MandatoryAtService = true,
				MandatoryAtServiceOrder = false,
				ExposeAtServiceOrder = false,
			};
		}

		private void TryCleanup(string subject, Action action)
		{
			try
			{
				action();
			}
			catch (Exception ex)
			{
				engine.GenerateInformation($"RT_ServiceProfiles: cleanup failed for {subject}: {ex}");
			}
		}
	}
}

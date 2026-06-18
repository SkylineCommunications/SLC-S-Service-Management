namespace SLC_SM_IAS_Service_Configuration.Model
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;

	using ConfigurationModels = Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models;

	public class HelperMethods
	{
		public static void RemoveServiceParameterOptionsLinks(Models.ServiceConfigurationValue config)
		{
			if (config.ConfigurationParameter.NumberOptions != null)
			{
				config.ConfigurationParameter.NumberOptions.ID = Guid.NewGuid();
			}

			if (config.ConfigurationParameter.DiscreteOptions != null)
			{
				config.ConfigurationParameter.DiscreteOptions.ID = Guid.NewGuid();
			}

			if (config.ConfigurationParameter.TextOptions != null)
			{
				config.ConfigurationParameter.TextOptions.ID = Guid.NewGuid();
			}
		}

		public static void RemoveParameterOptionsLinks(Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameterValue config)
		{
			if (config.NumberOptions != null)
			{
				config.NumberOptions.ID = Guid.NewGuid();
			}

			if (config.DiscreteOptions != null)
			{
				config.DiscreteOptions.ID = Guid.NewGuid();
			}

			if (config.TextOptions != null)
			{
				config.TextOptions.ID = Guid.NewGuid();
			}
		}

		public static Models.ServiceConfigurationVersion CreateNewServiceConfigurationVersion(Models.ServiceSpecification serviceSpecifivation, Models.Service instanceService)
		{
			var configurationVersion = new Models.ServiceConfigurationVersion
			{
				ID = Guid.NewGuid(),
				VersionName = "New Version",
				Description = String.Empty,
				StartDate = null,
				EndDate = null,
				CreatedAt = DateTime.UtcNow,
				Parameters = new List<Models.ServiceConfigurationValue>(),
				Profiles = new List<Models.ServiceProfile>(),
			};

			if (serviceSpecifivation == null)
			{
				return configurationVersion;
			}

			AddServiceSpecStandaloneParameters(serviceSpecifivation.ConfigurationParameters, configurationVersion);
			AddServiceSpecProfiles(serviceSpecifivation.ConfigurationProfiles, instanceService, configurationVersion);

			return configurationVersion;
		}

		public static Models.ServiceConfigurationVersion CreateNewServiceConfigurationVersionFromExisting(Models.ServiceConfigurationVersion serviceConfigurationVersion)
		{
			if (serviceConfigurationVersion == null)
			{
				return new Models.ServiceConfigurationVersion
				{
					VersionName = "- Copy",
					CreatedAt = DateTime.UtcNow,
					Parameters = new List<Models.ServiceConfigurationValue>(),
					Profiles = new List<Models.ServiceProfile>(),
				};
			}

			var newConfigurationVersion = new Models.ServiceConfigurationVersion
			{
				ID = Guid.NewGuid(),
				VersionName = $"{serviceConfigurationVersion.VersionName} - Copy",
				Description = serviceConfigurationVersion.Description,
				StartDate = serviceConfigurationVersion.StartDate,
				EndDate = serviceConfigurationVersion.EndDate,
				CreatedAt = DateTime.UtcNow,
				Parameters = new List<Models.ServiceConfigurationValue>(),
				Profiles = new List<Models.ServiceProfile>(),
			};

			AddServiceSpecStandaloneParameters(serviceConfigurationVersion.Parameters, newConfigurationVersion);
			AddServiceProfiles(serviceConfigurationVersion.Profiles, newConfigurationVersion);

			return newConfigurationVersion;
		}

		internal static ConfigurationModels.ConfigurationParameterValue BuildConfigurationParameter(ConfigurationModels.ConfigurationParameter configurationParameterInstance)
		{
			var configurationParameterValue = new ConfigurationModels.ConfigurationParameterValue
			{
				Label = String.Empty,
				Type = configurationParameterInstance.Type,
				ConfigurationParameterId = configurationParameterInstance.ID,
				NumberOptions = configurationParameterInstance.NumberOptions,
				DiscreteOptions = configurationParameterInstance.DiscreteOptions,
				TextOptions = configurationParameterInstance.TextOptions,
			};

			RemoveParameterOptionsLinks(configurationParameterValue);

			return configurationParameterValue;
		}

		internal static List<ConfigurationModels.ConfigurationParameter> GetConfigParameters(DataHelpersConfigurations dataHelperConfigurations, List<ConfigurationModels.ReferencedConfigurationParameters> referencedConfigurationParameters)
		{
			if (referencedConfigurationParameters == null || referencedConfigurationParameters.Count == 0)
			{
				return new List<ConfigurationModels.ConfigurationParameter>();
			}

			FilterElement<ConfigurationModels.ConfigurationParameter> configParamFilter = null;
			List<ConfigurationModels.ConfigurationParameter> configParams = new List<ConfigurationModels.ConfigurationParameter>();

			for (int i = 0; i < referencedConfigurationParameters.Count; i++)
			{
				if (i == 0)
				{
					configParamFilter = ConfigurationParameterExposers.Guid.Equal(referencedConfigurationParameters[i].ConfigurationParameter);
				}
				else
				{
					configParamFilter = configParamFilter.OR(ConfigurationParameterExposers.Guid.Equal(referencedConfigurationParameters[i].ConfigurationParameter));
				}
			}

			if (configParamFilter != null)
			{
				configParams = dataHelperConfigurations.ConfigurationParameters.Read(configParamFilter);
			}

			return configParams;
		}

		internal static List<ConfigurationModels.ConfigurationParameter> GetConfigParameters(DataHelpersConfigurations dataHelperConfigurations, ConfigurationModels.Profile profile)
		{
			if (profile == null)
			{
				return new List<ConfigurationModels.ConfigurationParameter>();
			}

			FilterElement<ConfigurationModels.ConfigurationParameter> configParamFilter = null;
			List<ConfigurationModels.ConfigurationParameter> configParams = new List<ConfigurationModels.ConfigurationParameter>();

			for (int i = 0; i < profile.ConfigurationParameterValues.Count; i++)
			{
				if (i == 0)
				{
					configParamFilter = ConfigurationParameterExposers.Guid.Equal(profile.ConfigurationParameterValues[i].ConfigurationParameterId);
				}
				else
				{
					configParamFilter = configParamFilter.OR(ConfigurationParameterExposers.Guid.Equal(profile.ConfigurationParameterValues[i].ConfigurationParameterId));
				}
			}

			if (configParamFilter != null)
			{
				configParams = dataHelperConfigurations.ConfigurationParameters.Read(configParamFilter);
			}

			return configParams;
		}

		private static void AddServiceSpecProfiles(List<Models.ServiceSpecificationProfile> configurationProfiles, Models.Service instanceService, Models.ServiceConfigurationVersion configurationVersion)
		{
			foreach (var configProfile in configurationProfiles)
			{
				var profileConfig = new Models.ServiceProfile
				{
					ID = Guid.NewGuid(),
					Mandatory = configProfile.MandatoryAtService,
					ProfileDefinition = configProfile.ProfileDefinition,
					Profile = new Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.Profile
					{
						ID = Guid.NewGuid(),
						Name = configProfile.Profile.Name.ReplaceTrailingParentesisContent(instanceService.ServiceID),
						ProfileDefinitionReference = configProfile.Profile.ProfileDefinitionReference,
						Profiles = configProfile.Profile.Profiles,
						TestedProtocols = configProfile.Profile.TestedProtocols,
						ConfigurationParameterValues = configProfile.Profile.ConfigurationParameterValues
						.Select(cpv =>
						{
							cpv.ID = Guid.NewGuid();
							RemoveParameterOptionsLinks(cpv);
							return cpv;
						})
						.ToList(),
					},
				};

				configurationVersion.Profiles.Add(profileConfig);
			}
		}

		private static void AddServiceProfiles(List<Models.ServiceProfile> configurationProfiles, Models.ServiceConfigurationVersion configurationVersion)
		{
			foreach (var configProfile in configurationProfiles)
			{
				var profileConfig = new Models.ServiceProfile
				{
					ID = Guid.NewGuid(),
					Mandatory = configProfile.Mandatory,
					ProfileDefinition = configProfile.ProfileDefinition,
					Profile = new Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.Profile
					{
						ID = Guid.NewGuid(),
						Name = configProfile.Profile.Name,
						ProfileDefinitionReference = configProfile.Profile.ProfileDefinitionReference,
						Profiles = configProfile.Profile.Profiles,
						TestedProtocols = configProfile.Profile.TestedProtocols,
						ConfigurationParameterValues = configProfile.Profile.ConfigurationParameterValues
						.Select(cpv =>
						{
							cpv.ID = Guid.NewGuid();
							RemoveParameterOptionsLinks(cpv);
							return cpv;
						})
						.ToList(),
					},
				};

				configurationVersion.Profiles.Add(profileConfig);
			}
		}

		private static void AddServiceSpecStandaloneParameters(List<Models.ServiceSpecificationConfigurationValue> configurationParameters, Models.ServiceConfigurationVersion configurationVersion)
		{
			foreach (var standaloneParameter in configurationParameters)
			{
				var config = new Models.ServiceConfigurationValue
				{
					ID = Guid.NewGuid(),
					Mandatory = standaloneParameter.MandatoryAtService,
					ConfigurationParameter = new Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameterValue
					{
						ID = Guid.NewGuid(),
						Label = String.Empty,
						Type = standaloneParameter.ConfigurationParameter.Type,
						ConfigurationParameterId = standaloneParameter.ConfigurationParameter.ConfigurationParameterId,
						NumberOptions = standaloneParameter.ConfigurationParameter.NumberOptions,
						DiscreteOptions = standaloneParameter.ConfigurationParameter.DiscreteOptions,
						TextOptions = standaloneParameter.ConfigurationParameter.TextOptions,
					},
				};

				RemoveServiceParameterOptionsLinks(config);
				configurationVersion.Parameters.Add(config);
			}
		}

		private static void AddServiceSpecStandaloneParameters(List<Models.ServiceConfigurationValue> configurationParameters, Models.ServiceConfigurationVersion configurationVersion)
		{
			foreach (var standaloneParameter in configurationParameters)
			{
				var config = new Models.ServiceConfigurationValue
				{
					ID = Guid.NewGuid(),
					Mandatory = standaloneParameter.Mandatory,
					ConfigurationParameter = new Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameterValue
					{
						ID = Guid.NewGuid(),
						Label = String.Empty,
						Type = standaloneParameter.ConfigurationParameter.Type,
						ConfigurationParameterId = standaloneParameter.ConfigurationParameter.ConfigurationParameterId,
						NumberOptions = standaloneParameter.ConfigurationParameter.NumberOptions,
						DiscreteOptions = standaloneParameter.ConfigurationParameter.DiscreteOptions,
						TextOptions = standaloneParameter.ConfigurationParameter.TextOptions,
					},
				};

				RemoveServiceParameterOptionsLinks(config);
				configurationVersion.Parameters.Add(config);
			}
		}
	}
}
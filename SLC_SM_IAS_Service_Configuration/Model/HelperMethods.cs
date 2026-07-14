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

	using static Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models;

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

		public static void RemoveParameterOptionsLinks(ConfigurationParameterValue config)
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

			var parameterIdMap = new Dictionary<Guid, Guid>();

			AddServiceSpecStandaloneParameters(serviceConfigurationVersion.Parameters, newConfigurationVersion, parameterIdMap);
			AddServiceProfiles(serviceConfigurationVersion.Profiles, newConfigurationVersion, parameterIdMap);

			RemapLinkedConsumers(newConfigurationVersion, parameterIdMap);

			return newConfigurationVersion;
		}

		internal static ConfigurationParameterValue BuildConfigurationParameter(ConfigurationParameter configurationParameterInstance)
		{
			var configurationParameterValue = new ConfigurationParameterValue
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

		internal static List<ConfigurationParameter> GetConfigParameters(DataHelpersConfigurations dataHelperConfigurations, List<ReferencedConfigurationParameters> referencedConfigurationParameters)
		{
			if (referencedConfigurationParameters == null || referencedConfigurationParameters.Count == 0)
			{
				return new List<ConfigurationParameter>();
			}

			FilterElement<ConfigurationParameter> configParamFilter = null;
			List<ConfigurationParameter> configParams = new List<ConfigurationParameter>();

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

		internal static List<ConfigurationParameter> GetConfigParameters(DataHelpersConfigurations dataHelperConfigurations, Profile profile)
		{
			if (profile == null)
			{
				return new List<ConfigurationParameter>();
			}

			FilterElement<ConfigurationParameter> configParamFilter = null;
			List<ConfigurationParameter> configParams = new List<ConfigurationParameter>();

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

		private static void RemapLinkedConsumers(Models.ServiceConfigurationVersion version, Dictionary<Guid, Guid> parameterIdMap)
		{
			if (parameterIdMap.Count == 0)
			{
				return;
			}

			foreach (var serviceConfigValue in version.Parameters)
			{
				RemapLinkedConsumers(serviceConfigValue.ConfigurationParameter, parameterIdMap);
			}

			foreach (var serviceProfile in version.Profiles)
			{
				if (serviceProfile?.Profile?.ConfigurationParameterValues == null)
				{
					continue;
				}

				foreach (var paramValue in serviceProfile.Profile.ConfigurationParameterValues)
				{
					RemapLinkedConsumers(paramValue, parameterIdMap);
				}
			}
		}

		private static void RemapLinkedConsumers(ConfigurationParameterValue paramValue, Dictionary<Guid, Guid> parameterIdMap)
		{
			if (paramValue?.LinkedConsumers == null || paramValue.LinkedConsumers.Count == 0)
			{
				return;
			}

			for (int i = 0; i < paramValue.LinkedConsumers.Count; i++)
			{
				if (parameterIdMap.TryGetValue(paramValue.LinkedConsumers[i], out Guid newId))
				{
					paramValue.LinkedConsumers[i] = newId;
				}
			}
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
					Profile = new Profile
					{
						ID = Guid.NewGuid(),
						Name = configProfile.Profile.Name.ReplaceTrailingParentesisContent(instanceService.ServiceID),
						ProfileDefinitionReference = configProfile.Profile.ProfileDefinitionReference,
						Profiles = configProfile.Profile.Profiles,
						TestedProtocols = configProfile.Profile.TestedProtocols,
						ConfigurationParameterValues = configProfile.Profile.ConfigurationParameterValues != null
							? configProfile.Profile.ConfigurationParameterValues.Select(DuplicateConfigurationParameterValue).ToList()
							: new List<ConfigurationParameterValue>(),
					},
				};

				configurationVersion.Profiles.Add(profileConfig);
			}
		}

		private static void AddServiceProfiles(List<Models.ServiceProfile> configurationProfiles, Models.ServiceConfigurationVersion configurationVersion, Dictionary<Guid, Guid> parameterIdMap)
		{
			if (configurationProfiles == null)
			{
				return;
			}

			var profilesMapping = new Dictionary<Guid, Guid>();
			var duplicatedProfiles = new List<Models.ServiceProfile>();

			foreach (var configProfile in configurationProfiles)
			{
				if (configProfile?.Profile == null)
				{
					continue;
				}

				var sourceProfile = configProfile.Profile;
				var duplicatedProfile = new Profile
				{
					ID = Guid.NewGuid(),
					Name = sourceProfile.Name,
					ProfileDefinitionReference = sourceProfile.ProfileDefinitionReference,
					Profiles = sourceProfile.Profiles,
					TestedProtocols = sourceProfile.TestedProtocols,
					ConfigurationParameterValues = sourceProfile.ConfigurationParameterValues != null
						? sourceProfile.ConfigurationParameterValues.Select(p => DuplicateConfigurationParameterValue(p, parameterIdMap)).ToList()
						: new List<ConfigurationParameterValue>(),
				};

				profilesMapping[sourceProfile.ID] = duplicatedProfile.ID;

				duplicatedProfiles.Add(new Models.ServiceProfile
				{
					ID = Guid.NewGuid(),
					Mandatory = configProfile.Mandatory,
					ProfileDefinition = configProfile.ProfileDefinition,
					Profile = duplicatedProfile,
				});
			}

			foreach (var serviceProfile in duplicatedProfiles)
			{
				var children = serviceProfile.Profile.Profiles;
				if (children == null || children.Count == 0)
				{
					continue;
				}

				serviceProfile.Profile.Profiles = children
					.Select(oldId => profilesMapping.TryGetValue(oldId, out var newId) ? newId : oldId)
					.ToList();
			}

			configurationVersion.Profiles.AddRange(duplicatedProfiles);
		}

		private static ConfigurationParameterValue DuplicateConfigurationParameterValue(ConfigurationParameterValue source)
		{
			return DuplicateConfigurationParameterValue(source, parameterIdMap: null);
		}

		private static ConfigurationParameterValue DuplicateConfigurationParameterValue(ConfigurationParameterValue source, Dictionary<Guid, Guid> parameterIdMap)
		{
			var newId = Guid.NewGuid();

			if (parameterIdMap != null)
			{
				parameterIdMap[source.ID] = newId;
			}

			var duplicateParameter = new ConfigurationParameterValue
			{
				ID = newId,
				Label = source.Label,
				Type = source.Type,
				ConfigurationParameterId = source.ConfigurationParameterId,
				NumberOptions = source.NumberOptions,
				DiscreteOptions = source.DiscreteOptions,
				TextOptions = source.TextOptions,
				IsLinked = source.IsLinked,
				LinkedScript = source.LinkedScript,
				LinkedConsumers = source.LinkedConsumers != null ? new List<Guid>(source.LinkedConsumers) : null,
			};

			RemoveParameterOptionsLinks(duplicateParameter);
			return duplicateParameter;
		}

		private static void AddServiceSpecStandaloneParameters(List<Models.ServiceSpecificationConfigurationValue> configurationParameters, Models.ServiceConfigurationVersion configurationVersion)
		{
			foreach (var standaloneParameter in configurationParameters)
			{
				var config = new Models.ServiceConfigurationValue
				{
					ID = Guid.NewGuid(),
					Mandatory = standaloneParameter.MandatoryAtService,
					ConfigurationParameter = new ConfigurationParameterValue
					{
						ID = Guid.NewGuid(),
						Label = String.Empty,
						Type = standaloneParameter.ConfigurationParameter.Type,
						ConfigurationParameterId = standaloneParameter.ConfigurationParameter.ConfigurationParameterId,
						NumberOptions = standaloneParameter.ConfigurationParameter.NumberOptions,
						DiscreteOptions = standaloneParameter.ConfigurationParameter.DiscreteOptions,
						TextOptions = standaloneParameter.ConfigurationParameter.TextOptions,
						IsLinked = standaloneParameter.ConfigurationParameter.IsLinked,
						LinkedScript = standaloneParameter.ConfigurationParameter.LinkedScript,
						LinkedConsumers = standaloneParameter.ConfigurationParameter.LinkedConsumers != null
							? new List<Guid>(standaloneParameter.ConfigurationParameter.LinkedConsumers)
							: null,
					},
				};

				RemoveServiceParameterOptionsLinks(config);
				configurationVersion.Parameters.Add(config);
			}
		}

		private static void AddServiceSpecStandaloneParameters(List<Models.ServiceConfigurationValue> configurationParameters, Models.ServiceConfigurationVersion configurationVersion, Dictionary<Guid, Guid> parameterIdMap)
		{
			foreach (var standaloneParameter in configurationParameters)
			{
				var sourceParam = standaloneParameter.ConfigurationParameter;
				var newParamValue = DuplicateConfigurationParameterValue(sourceParam, parameterIdMap);

				var config = new Models.ServiceConfigurationValue
				{
					ID = Guid.NewGuid(),
					Mandatory = standaloneParameter.Mandatory,
					ConfigurationParameter = newParamValue,
				};

				RemoveServiceParameterOptionsLinks(config);
				configurationVersion.Parameters.Add(config);
			}
		}
	}
}
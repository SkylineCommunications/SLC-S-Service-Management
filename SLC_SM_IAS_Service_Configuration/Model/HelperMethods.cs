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
			if (configurationParameterInstance == null)
			{
				throw new ArgumentNullException(nameof(configurationParameterInstance));
			}

			return new ConfigurationParameterValue
			{
				ID = Guid.NewGuid(),
				Label = String.Empty,
				Type = configurationParameterInstance.Type,
				ConfigurationParameterId = configurationParameterInstance.ID,
				NumberOptions = CloneNumberOptions(configurationParameterInstance.NumberOptions),
				DiscreteOptions = CloneDiscreteOptions(configurationParameterInstance.DiscreteOptions),
				TextOptions = CloneTextOptions(configurationParameterInstance.TextOptions),
			};
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
			if (configurationProfiles == null)
			{
				return;
			}

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
					Name = sourceProfile.Name.ReplaceTrailingParentesisContent(instanceService.ServiceID),
					ProfileDefinitionReference = sourceProfile.ProfileDefinitionReference,
					Profiles = sourceProfile.Profiles != null
						? new List<Guid>(sourceProfile.Profiles)
						: new List<Guid>(),
					TestedProtocols = sourceProfile.TestedProtocols != null
						? new List<ProtocolTest>(sourceProfile.TestedProtocols)
						: new List<ProtocolTest>(),
					ConfigurationParameterValues = sourceProfile.ConfigurationParameterValues != null
						? sourceProfile.ConfigurationParameterValues
							.Where(parameter => parameter != null)
							.Select(DuplicateConfigurationParameterValue)
							.ToList()
						: new List<ConfigurationParameterValue>(),
				};

				configurationVersion.Profiles.Add(
					new Models.ServiceProfile
					{
						ID = Guid.NewGuid(),
						Mandatory = configProfile.MandatoryAtService,
						ProfileDefinition = configProfile.ProfileDefinition,
						Profile = duplicatedProfile,
					});
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
					Profiles = sourceProfile.Profiles != null
						? new List<Guid>(sourceProfile.Profiles)
						: new List<Guid>(),
					TestedProtocols = sourceProfile.TestedProtocols != null
						? new List<ProtocolTest>(sourceProfile.TestedProtocols)
						: new List<ProtocolTest>(),
					ConfigurationParameterValues = sourceProfile.ConfigurationParameterValues != null
						? sourceProfile.ConfigurationParameterValues
							.Where(parameter => parameter != null)
							.Select(parameter => DuplicateConfigurationParameterValue(parameter, parameterIdMap))
							.ToList()
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
			if (source == null)
			{
				return null;
			}

			var newId = Guid.NewGuid();

			if (parameterIdMap != null)
			{
				parameterIdMap[source.ID] = newId;
			}

			return new ConfigurationParameterValue
			{
				ID = newId,
				Label = source.Label,
				Type = source.Type,
				ConfigurationParameterId = source.ConfigurationParameterId,
				StringValue = source.StringValue,
				DoubleValue = source.DoubleValue,
				ValueFixed = source.ValueFixed,
				NumberOptions = CloneNumberOptions(source.NumberOptions),
				DiscreteOptions = CloneDiscreteOptions(source.DiscreteOptions),
				TextOptions = CloneTextOptions(source.TextOptions),
				IsLinked = source.IsLinked,
				LinkedScript = source.LinkedScript,
				LinkedConsumers = source.LinkedConsumers != null ? new List<Guid>(source.LinkedConsumers) : null,
			};
		}

		private static NumberParameterOptions CloneNumberOptions(NumberParameterOptions source)
		{
			if (source == null)
			{
				return null;
			}

			var units = source.Units != null
				? source.Units
					.Where(unit => unit != null)
					.Select(CloneConfigurationUnit)
					.ToList()
				: new List<ConfigurationUnit>();

			ConfigurationUnit defaultUnit = null;

			if (source.DefaultUnit != null)
			{
				defaultUnit = units.FirstOrDefault(unit => unit.ID == source.DefaultUnit.ID);

				if (defaultUnit == null)
				{
					defaultUnit = CloneConfigurationUnit(source.DefaultUnit);
					units.Add(defaultUnit);
				}
			}

			return new NumberParameterOptions
			{
				ID = Guid.NewGuid(),
				Decimals = source.Decimals,
				DefaultUnit = defaultUnit,
				DefaultValue = source.DefaultValue,
				MaxRange = source.MaxRange,
				MinRange = source.MinRange,
				StepSize = source.StepSize,
				Units = units,
			};
		}

		private static ConfigurationUnit CloneConfigurationUnit(ConfigurationUnit source)
		{
			if (source == null)
			{
				return null;
			}

			return new ConfigurationUnit
			{
				ID = source.ID,
				Name = source.Name,
			};
		}

		private static DiscreteParameterOptions CloneDiscreteOptions(DiscreteParameterOptions source)
		{
			if (source == null)
			{
				return null;
			}

			return new DiscreteParameterOptions
			{
				ID = Guid.NewGuid(),
				Default = source.Default,
				DiscreteValues = source.DiscreteValues != null
					? source.DiscreteValues
						.Where(discreteValue => discreteValue != null)
						.Select(
							discreteValue => new DiscreteValue
							{
								Value = discreteValue.Value,
							})
						.ToList()
					: new List<DiscreteValue>(),
			};
		}

		private static TextParameterOptions CloneTextOptions(TextParameterOptions source)
		{
			if (source == null)
			{
				return null;
			}

			return new TextParameterOptions
			{
				ID = Guid.NewGuid(),
				Default = source.Default,
				Regex = source.Regex,
				UserMessage = source.UserMessage,
			};
		}

		private static void AddServiceSpecStandaloneParameters(List<Models.ServiceSpecificationConfigurationValue> configurationParameters, Models.ServiceConfigurationVersion configurationVersion)
		{
			if (configurationParameters == null)
			{
				return;
			}

			foreach (var standaloneParameter in configurationParameters)
			{
				if (standaloneParameter?.ConfigurationParameter == null)
				{
					continue;
				}

				var duplicatedParameter = DuplicateConfigurationParameterValue(standaloneParameter.ConfigurationParameter);

				duplicatedParameter.Label = String.Empty;

				configurationVersion.Parameters.Add(
					new Models.ServiceConfigurationValue
					{
						ID = Guid.NewGuid(),
						Mandatory = standaloneParameter.MandatoryAtService,
						ConfigurationParameter = duplicatedParameter,
					});
			}
		}

		private static void AddServiceSpecStandaloneParameters(List<Models.ServiceConfigurationValue> configurationParameters, Models.ServiceConfigurationVersion configurationVersion, Dictionary<Guid, Guid> parameterIdMap)
		{
			if (configurationParameters == null)
			{
				return;
			}

			foreach (var standaloneParameter in configurationParameters)
			{
				if (standaloneParameter?.ConfigurationParameter == null)
				{
					continue;
				}

				configurationVersion.Parameters.Add(
					new Models.ServiceConfigurationValue
					{
						ID = Guid.NewGuid(),
						Mandatory = standaloneParameter.Mandatory,
						ConfigurationParameter = DuplicateConfigurationParameterValue(standaloneParameter.ConfigurationParameter, parameterIdMap),
					});
			}
		}
	}
}
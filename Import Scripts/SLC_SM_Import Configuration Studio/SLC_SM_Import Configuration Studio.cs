/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE        VERSION        AUTHOR            COMMENTS

31/08/2026  1.0.0.1        SKA           Initial version
****************************************************************************
*/
namespace SLC_SM_Import_Configuration_Studio
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Security.Cryptography;
	using System.Text;
	using DomHelpers.SlcConfigurations;
	using Newtonsoft.Json;
	using Newtonsoft.Json.Linq;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.IAS;
	using ConfigModels = Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models;

	/// <summary>
	/// Represents a DataMiner Automation script.
	/// </summary>
	public class Script
	{
		private const string DefaultJsonPath = @"C:\Skyline DataMiner\Documents\DMA_COMMON_DOCUMENTS\configuration-studio.json";

		/// <summary>
		/// The script entry point.
		/// </summary>
		/// <param name="engine">Link with SLAutomation process.</param>
		public void Run(IEngine engine)
		{
			try
			{
				RunSafe(engine);
			}
			catch (ScriptAbortException)
			{
				// Catch normal abort exceptions (engine.ExitFail or engine.ExitSuccess)
			}
			catch (ScriptForceAbortException)
			{
				// Catch forced abort exceptions, caused via external maintenance messages.
			}
			catch (ScriptTimeoutException)
			{
				// Catch timeout exceptions for when a script has been running for too long.
			}
			catch (InteractiveUserDetachedException)
			{
				// Catch a user detaching from the interactive script by closing the window.
				// Only applicable for interactive scripts, can be removed for non-interactive scripts.
			}
			catch (Exception e)
			{
				engine.ExitFail($"Run|{e.Message}");
			}
		}

		private static void RunSafe(IEngine engine)
		{
			string jsonPath = engine.ReadScriptParamFromApp("JSON File Path");
			string parameterIdToImport = NormalizeParameterFilter(engine.ReadScriptParamFromApp("Parameter ID"));
			if (String.IsNullOrWhiteSpace(jsonPath))
			{
				jsonPath = DefaultJsonPath;
			}

			if (!File.Exists(jsonPath))
			{
				throw new FileNotFoundException($"JSON import file was not found: '{jsonPath}'");
			}

			var root = JsonConvert.DeserializeObject<ConfigurationStudioRoot>(File.ReadAllText(jsonPath));
			if (root == null)
			{
				throw new InvalidOperationException($"Could not deserialize configuration studio payload from '{jsonPath}'.");
			}

			var configHelper = new DataHelpersConfigurations(engine.GetUserConnection());
			var unitsByName = configHelper.ConfigurationUnits.Read()
				.Where(u => !String.IsNullOrWhiteSpace(u.Name))
				.GroupBy(u => u.Name, StringComparer.InvariantCultureIgnoreCase)
				.ToDictionary(g => g.Key, g => g.First(), StringComparer.InvariantCultureIgnoreCase);

			var existingParametersByName = configHelper.ConfigurationParameters.Read()
				.Where(p => !String.IsNullOrWhiteSpace(p.Name))
				.GroupBy(p => p.Name, StringComparer.InvariantCultureIgnoreCase)
				.ToDictionary(g => g.Key, g => g.First(), StringComparer.InvariantCultureIgnoreCase);

			var existingProfileDefinitionsByName = configHelper.ProfileDefinitions.Read()
				.Where(p => !String.IsNullOrWhiteSpace(p.Name))
				.GroupBy(p => p.Name, StringComparer.InvariantCultureIgnoreCase)
				.ToDictionary(g => g.Key, g => g.First(), StringComparer.InvariantCultureIgnoreCase);

			var existingProfilesByName = configHelper.Profiles.Read(ProfileExposers.IsReusable.Equal(true))
				.Where(p => !String.IsNullOrWhiteSpace(p.Name))
				.GroupBy(p => p.Name, StringComparer.InvariantCultureIgnoreCase)
				.ToDictionary(g => g.Key, g => g.First(), StringComparer.InvariantCultureIgnoreCase);

			var parameterIdMap = new Dictionary<string, Guid>(StringComparer.InvariantCultureIgnoreCase);
			var profileDefinitionIdMap = new Dictionary<string, Guid>(StringComparer.InvariantCultureIgnoreCase);
			var reusableProfileIdMap = new Dictionary<string, Guid>(StringComparer.InvariantCultureIgnoreCase);
			var reusableProfilesForSecondPass = new List<(ReusableProfileImport Source, ConfigModels.Profile Profile)>();

			int createdParameters = 0;
			int updatedParameters = 0;
			int createdProfileDefinitions = 0;
			int updatedProfileDefinitions = 0;
			int createdReusableProfiles = 0;
			int updatedReusableProfiles = 0;
			var failures = new List<string>();

			var sourceParameters = (root.Parameters ?? Enumerable.Empty<ParameterImport>()).ToList();
			if (!String.IsNullOrWhiteSpace(parameterIdToImport))
			{
				sourceParameters = sourceParameters
					.Where(p => String.Equals(p?.Id, parameterIdToImport, StringComparison.InvariantCultureIgnoreCase))
					.ToList();

				if (sourceParameters.Count == 0)
				{
					throw new InvalidOperationException($"No parameter with id '{parameterIdToImport}' was found in '{jsonPath}'.");
				}
			}

			foreach (var sourceParameter in sourceParameters)
			{
				try
				{
					var parameter = BuildConfigurationParameter(sourceParameter, unitsByName, existingParametersByName);
					bool exists = existingParametersByName.ContainsKey(parameter.Name);

					configHelper.ConfigurationParameters.CreateOrUpdate(parameter);
					parameterIdMap[sourceParameter.Id] = parameter.ID;

					if (exists)
					{
						updatedParameters++;
					}
					else
					{
						createdParameters++;
						existingParametersByName[parameter.Name] = parameter;
					}
				}
				catch (Exception e)
				{
					failures.Add($"Configuration parameter '{sourceParameter?.Id ?? sourceParameter?.Name}' was not imported: {e.Message}");
				}
			}

			if (String.IsNullOrWhiteSpace(parameterIdToImport))
			{
				foreach (var sourceProfileDefinition in root.ProfileDefinitions ?? Enumerable.Empty<ProfileDefinitionImport>())
				{
					try
					{
						var profileDefinition = BuildProfileDefinition(sourceProfileDefinition, parameterIdMap, profileDefinitionIdMap, existingProfileDefinitionsByName);
						bool exists = existingProfileDefinitionsByName.ContainsKey(profileDefinition.Name);

						configHelper.ProfileDefinitions.CreateOrUpdate(profileDefinition);
						profileDefinitionIdMap[sourceProfileDefinition.Id] = profileDefinition.ID;

						if (exists)
						{
							updatedProfileDefinitions++;
						}
						else
						{
							createdProfileDefinitions++;
							existingProfileDefinitionsByName[profileDefinition.Name] = profileDefinition;
						}
					}
					catch (Exception e)
					{
						failures.Add($"Profile definition '{sourceProfileDefinition?.Id ?? sourceProfileDefinition?.Name}' was not imported: {e.Message}");
					}
				}

				foreach (var sourceProfile in root.ReusableProfiles ?? Enumerable.Empty<ReusableProfileImport>())
				{
					try
					{
						var reusableProfile = BuildReusableProfile(sourceProfile, root, parameterIdMap, profileDefinitionIdMap, reusableProfileIdMap, existingProfilesByName, existingParametersByName);
						bool exists = existingProfilesByName.ContainsKey(reusableProfile.Name);

						configHelper.Profiles.CreateOrUpdate(reusableProfile);
						reusableProfileIdMap[sourceProfile.Id] = reusableProfile.ID;
						reusableProfilesForSecondPass.Add((sourceProfile, reusableProfile));

						if (exists)
						{
							updatedReusableProfiles++;
						}
						else
						{
							createdReusableProfiles++;
							existingProfilesByName[reusableProfile.Name] = reusableProfile;
						}
					}
					catch (Exception e)
					{
						failures.Add($"Reusable profile '{sourceProfile?.Id ?? sourceProfile?.Name}' was not imported: {e.Message}");
					}
				}

				foreach (var item in reusableProfilesForSecondPass)
				{
					try
					{
						item.Profile.Profiles = ResolveNestedProfiles(item.Source, reusableProfileIdMap);
						configHelper.Profiles.CreateOrUpdate(item.Profile);
					}
					catch (Exception e)
					{
						failures.Add($"Reusable profile '{item.Source?.Id ?? item.Source?.Name}' nested references were not applied: {e.Message}");
					}
				}
			}

			engine.GenerateInformation(
				"[Import Configuration Studio] Completed. " +
				$"Parameters C/U: {createdParameters}/{updatedParameters}. " +
				$"Profile definitions C/U: {createdProfileDefinitions}/{updatedProfileDefinitions}. " +
				$"Reusable profiles C/U: {createdReusableProfiles}/{updatedReusableProfiles}. " +
				$"Failed: {failures.Count}. " +
				$"Parameter filter: {(String.IsNullOrWhiteSpace(parameterIdToImport) ? "<none>" : parameterIdToImport)}. " +
				$"Source file: {jsonPath}");

			if (failures.Count > 0)
			{
				engine.ExitFail($"[Import Configuration Studio] {failures.Count} record(s) failed and were skipped: {String.Join(" | ", failures)}");
			}
		}

		private static ConfigModels.ConfigurationParameter BuildConfigurationParameter(
			ParameterImport source,
			Dictionary<string, ConfigModels.ConfigurationUnit> unitsByName,
			Dictionary<string, ConfigModels.ConfigurationParameter> existingByName)
		{
			if (source == null)
			{
				throw new InvalidOperationException("Encountered null parameter definition.");
			}

			if (String.IsNullOrWhiteSpace(source.Name))
			{
				throw new InvalidOperationException($"Parameter '{source.Id}' has no name.");
			}

			bool exists = existingByName.TryGetValue(source.Name, out var existing);
			var parameter = exists ? existing : new ConfigModels.ConfigurationParameter
			{
				ID = ToDeterministicGuid($"cfg:param:{source.Id}"),
			};

			parameter.Name = source.Name;
			parameter.Type = ParseParameterType(source.Type);
			parameter.NumberOptions = null;
			parameter.DiscreteOptions = null;
			parameter.TextOptions = null;

			switch (parameter.Type)
			{
				case SlcConfigurationsIds.Enums.Type.Text:
					parameter.TextOptions = new ConfigModels.TextParameterOptions
					{
						Default = source.DefaultValue?.ToString(),
					};
					break;

				case SlcConfigurationsIds.Enums.Type.Number:
					ConfigModels.ConfigurationUnit unit = null;
					if (!String.IsNullOrWhiteSpace(source.Unit))
					{
						unitsByName.TryGetValue(source.Unit, out unit);
					}

					parameter.NumberOptions = new ConfigModels.NumberParameterOptions
					{
						DefaultValue = source.DefaultValue?.Value<double?>(),
						MinRange = source.MinValue,
						MaxRange = source.MaxValue,
						StepSize = 1,
						Decimals = 0,
						DefaultUnit = unit,
						Units = unit == null ? new List<ConfigModels.ConfigurationUnit>() : new List<ConfigModels.ConfigurationUnit> { unit },
					};
					break;

				case SlcConfigurationsIds.Enums.Type.Discrete:
					var discreteValues = (source.AllowedValues ?? new List<string>())
						.Where(v => !String.IsNullOrWhiteSpace(v))
						.Select(v => new ConfigModels.DiscreteValue { Value = v })
						.ToList();

					ConfigModels.DiscreteValue defaultDiscrete = null;
					if (source.DefaultValue != null)
					{
						string defaultValue = source.DefaultValue.ToString();
						defaultDiscrete = discreteValues.FirstOrDefault(v => String.Equals(v.Value, defaultValue, StringComparison.InvariantCultureIgnoreCase))
							?? new ConfigModels.DiscreteValue { Value = defaultValue };
						if (!discreteValues.Any(v => String.Equals(v.Value, defaultDiscrete.Value, StringComparison.InvariantCultureIgnoreCase)))
						{
							discreteValues.Add(defaultDiscrete);
						}
					}

					parameter.DiscreteOptions = new ConfigModels.DiscreteParameterOptions
					{
						DiscreteValues = discreteValues,
						Default = defaultDiscrete,
					};
					break;

				default:
					throw new NotSupportedException($"Unsupported parameter type '{source.Type}' for parameter '{source.Name}'.");
			}

			return parameter;
		}

		private static ConfigModels.ProfileDefinition BuildProfileDefinition(
			ProfileDefinitionImport source,
			Dictionary<string, Guid> parameterIdMap,
			Dictionary<string, Guid> profileDefinitionIdMap,
			Dictionary<string, ConfigModels.ProfileDefinition> existingByName)
		{
			if (source == null)
			{
				throw new InvalidOperationException("Encountered null profile definition.");
			}

			if (String.IsNullOrWhiteSpace(source.Name))
			{
				throw new InvalidOperationException($"Profile definition '{source.Id}' has no name.");
			}

			bool exists = existingByName.TryGetValue(source.Name, out var existing);
			var profileDefinition = exists ? existing : new ConfigModels.ProfileDefinition
			{
				ID = ToDeterministicGuid($"cfg:profile-definition:{source.Id}"),
			};

			profileDefinition.Name = source.Name;
			profileDefinition.Scripts = profileDefinition.Scripts ?? new List<ConfigModels.Script>();
			profileDefinition.ConfigurationParameters = new List<ConfigModels.ReferencedConfigurationParameters>();
			profileDefinition.ProfileDefinitions = new List<ConfigModels.ReferencedProfileDefinitions>();

			foreach (var referencedParam in source.Parameters ?? Enumerable.Empty<ProfileDefinitionParameterImport>())
			{
				if (!parameterIdMap.TryGetValue(referencedParam.ParameterId, out Guid parameterId))
				{
					throw new InvalidOperationException(
						$"Profile definition '{source.Name}' references unknown parameter '{referencedParam.ParameterId}'.");
				}

				profileDefinition.ConfigurationParameters.Add(new ConfigModels.ReferencedConfigurationParameters
				{
					ConfigurationParameter = parameterId,
					Mandatory = referencedParam.Required,
					AllowMultiple = false,
				});
			}

			foreach (var nestedProfile in source.NestedProfiles ?? Enumerable.Empty<NestedProfileDefinitionImport>())
			{
				if (!profileDefinitionIdMap.TryGetValue(nestedProfile.ProfileDefinitionId, out Guid nestedProfileDefinitionId))
				{
					throw new InvalidOperationException(
						$"Profile definition '{source.Name}' references unknown nested profile definition '{nestedProfile.ProfileDefinitionId}'.");
				}

				profileDefinition.ProfileDefinitions.Add(new ConfigModels.ReferencedProfileDefinitions
				{
					ProfileDefinitionReference = nestedProfileDefinitionId,
					Mandatory = nestedProfile.Required,
					AllowMultiple = nestedProfile.AllowMultiple,
				});
			}

			return profileDefinition;
		}

		private static ConfigModels.Profile BuildReusableProfile(
			ReusableProfileImport source,
			ConfigurationStudioRoot root,
			Dictionary<string, Guid> parameterIdMap,
			Dictionary<string, Guid> profileDefinitionIdMap,
			Dictionary<string, Guid> reusableProfileIdMap,
			Dictionary<string, ConfigModels.Profile> existingByName,
			Dictionary<string, ConfigModels.ConfigurationParameter> existingParametersByName)
		{
			if (source == null)
			{
				throw new InvalidOperationException("Encountered null reusable profile.");
			}

			if (String.IsNullOrWhiteSpace(source.Name))
			{
				throw new InvalidOperationException($"Reusable profile '{source.Id}' has no name.");
			}

			if (!profileDefinitionIdMap.TryGetValue(source.ProfileDefinitionId, out Guid profileDefinitionId))
			{
				throw new InvalidOperationException(
					$"Reusable profile '{source.Name}' references unknown profile definition '{source.ProfileDefinitionId}'.");
			}

			bool exists = existingByName.TryGetValue(source.Name, out var existing);
			var profile = exists ? existing : new ConfigModels.Profile
			{
				ID = ToDeterministicGuid($"cfg:reusable-profile:{source.Id}"),
			};

			profile.Name = source.Name;
			profile.ProfileDefinitionReference = profileDefinitionId;
			profile.IsReusable = true;
			profile.TestedProtocols = profile.TestedProtocols ?? new List<ConfigModels.ProtocolTest>();
			profile.Profiles = new List<Guid>();
			profile.ConfigurationParameterValues = BuildConfigurationValuesForProfile(source, root, parameterIdMap, reusableProfileIdMap, existingParametersByName);

			return profile;
		}

		private static List<ConfigModels.ConfigurationParameterValue> BuildConfigurationValuesForProfile(
			ReusableProfileImport source,
			ConfigurationStudioRoot root,
			Dictionary<string, Guid> parameterIdMap,
			Dictionary<string, Guid> reusableProfileIdMap,
			Dictionary<string, ConfigModels.ConfigurationParameter> existingParametersByName)
		{
			var result = new List<ConfigModels.ConfigurationParameterValue>();
			if (source.Values == null)
			{
				return result;
			}

			var parameterById = (root.Parameters ?? new List<ParameterImport>())
				.Where(p => !String.IsNullOrWhiteSpace(p.Id))
				.ToDictionary(p => p.Id, p => p, StringComparer.InvariantCultureIgnoreCase);

			foreach (var valueItem in source.Values)
			{
				parameterById.TryGetValue(valueItem.Key, out var parameterDefinition);
				if (!parameterIdMap.TryGetValue(valueItem.Key, out Guid parameterGuid))
				{
					parameterGuid = ToDeterministicGuid($"cfg:param:{valueItem.Key}");
				}

				if (parameterDefinition != null
					&& !String.IsNullOrWhiteSpace(parameterDefinition.Name)
					&& existingParametersByName.TryGetValue(parameterDefinition.Name, out var existingParameter))
				{
					parameterGuid = existingParameter.ID;
				}

				var value = new ConfigModels.ConfigurationParameterValue
				{
					ID = ToDeterministicGuid($"cfg:reusable-profile:{source.Id}:param:{valueItem.Key}"),
					ConfigurationParameterId = parameterGuid,
					Label = parameterDefinition?.Name ?? valueItem.Key,
					Type = parameterDefinition != null ? ParseParameterType(parameterDefinition.Type) : InferTypeFromValue(valueItem.Value),
				};

				ApplyValueToConfigurationParameterValue(value, valueItem.Value, parameterDefinition);
				result.Add(value);
			}

			return result;
		}

		private static SlcConfigurationsIds.Enums.Type InferTypeFromValue(JToken token)
		{
			switch (token?.Type)
			{
				case JTokenType.Integer:
				case JTokenType.Float:
					return SlcConfigurationsIds.Enums.Type.Number;
				default:
					return SlcConfigurationsIds.Enums.Type.Text;
			}
		}

		private static List<Guid> ResolveNestedProfiles(ReusableProfileImport source, Dictionary<string, Guid> reusableProfileIdMap)
		{
			var result = new List<Guid>();
			if (source.NestedProfiles == null)
			{
				return result;
			}

			foreach (var nestedList in source.NestedProfiles.Values)
			{
				foreach (var nestedSelection in nestedList ?? Enumerable.Empty<NestedReusableProfileSelectionImport>())
				{
					if (!String.Equals(nestedSelection.Mode, "reusable", StringComparison.InvariantCultureIgnoreCase))
					{
						continue;
					}

					if (String.IsNullOrWhiteSpace(nestedSelection.ReusableProfileId))
					{
						continue;
					}

					if (reusableProfileIdMap.TryGetValue(nestedSelection.ReusableProfileId, out Guid nestedProfileId))
					{
						result.Add(nestedProfileId);
					}
				}
			}

			return result;
		}

		private static void ApplyValueToConfigurationParameterValue(
			ConfigModels.ConfigurationParameterValue value,
			JToken token,
			ParameterImport parameterDefinition)
		{
			switch (value.Type)
			{
				case SlcConfigurationsIds.Enums.Type.Text:
					string textValue = token?.ToString();
					value.TextOptions = new ConfigModels.TextParameterOptions
					{
						Default = textValue,
					};
					value.StringValue = textValue;
					break;

				case SlcConfigurationsIds.Enums.Type.Number:
					double? numberValue = ReadNumericValue(token);
					value.NumberOptions = new ConfigModels.NumberParameterOptions
					{
						DefaultValue = numberValue,
						MinRange = parameterDefinition?.MinValue,
						MaxRange = parameterDefinition?.MaxValue,
						StepSize = 1,
						Decimals = 0,
					};
					value.DoubleValue = numberValue;
					break;

				case SlcConfigurationsIds.Enums.Type.Discrete:
					string discreteValue = token?.ToString();
					var discreteValues = (parameterDefinition?.AllowedValues ?? new List<string>())
						.Where(v => !String.IsNullOrWhiteSpace(v))
						.Select(v => new ConfigModels.DiscreteValue { Value = v })
						.ToList();
					if (!String.IsNullOrWhiteSpace(discreteValue)
						&& !discreteValues.Any(v => String.Equals(v.Value, discreteValue, StringComparison.InvariantCultureIgnoreCase)))
					{
						discreteValues.Add(new ConfigModels.DiscreteValue { Value = discreteValue });
					}

					ConfigModels.DiscreteValue selectedValue = discreteValues
						.FirstOrDefault(v => String.Equals(v.Value, discreteValue, StringComparison.InvariantCultureIgnoreCase));

					value.DiscreteOptions = new ConfigModels.DiscreteParameterOptions
					{
						DiscreteValues = discreteValues,
						Default = selectedValue,
					};
					value.StringValue = discreteValue;
					break;

				default:
					throw new NotSupportedException($"Unsupported configuration value type '{value.Type}'.");
			}
		}

		private static double? ReadNumericValue(JToken token)
		{
			if (token == null || token.Type == JTokenType.Null)
			{
				return null;
			}

			if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float)
			{
				return token.Value<double?>();
			}

			return Double.TryParse(token.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed)
				? parsed
				: default(double?);
		}

		private static SlcConfigurationsIds.Enums.Type ParseParameterType(string raw)
		{
			if (String.IsNullOrWhiteSpace(raw))
			{
				return SlcConfigurationsIds.Enums.Type.Text;
			}

			switch (raw.Trim().ToLower(CultureInfo.InvariantCulture))
			{
				case "text":
					return SlcConfigurationsIds.Enums.Type.Text;
				case "number":
					return SlcConfigurationsIds.Enums.Type.Number;
				case "discrete":
					return SlcConfigurationsIds.Enums.Type.Discrete;
				default:
					throw new NotSupportedException($"Unsupported configuration parameter type '{raw}'.");
			}
		}

		private static Guid ToDeterministicGuid(string value)
		{
			byte[] bytes = Encoding.UTF8.GetBytes(value ?? String.Empty);
			using (var sha256 = SHA256.Create())
			{
				var hash = sha256.ComputeHash(bytes);
				var guidBytes = new byte[16];
				Array.Copy(hash, guidBytes, guidBytes.Length);
				return new Guid(guidBytes);
			}
		}

		private static string NormalizeParameterFilter(string rawParameterId)
		{
			if (String.IsNullOrWhiteSpace(rawParameterId))
			{
				return null;
			}

			string trimmed = rawParameterId.Trim();
			if (trimmed.Equals("NA", StringComparison.InvariantCultureIgnoreCase)
				|| trimmed.Equals("N/A", StringComparison.InvariantCultureIgnoreCase)
				|| trimmed.Equals("-None-", StringComparison.InvariantCultureIgnoreCase)
				|| trimmed.Equals("None", StringComparison.InvariantCultureIgnoreCase))
			{
				return null;
			}

			return trimmed;
		}

		private sealed class ConfigurationStudioRoot
		{
			[JsonProperty("parameters")]
			public List<ParameterImport> Parameters { get; set; }

			[JsonProperty("profileDefinitions")]
			public List<ProfileDefinitionImport> ProfileDefinitions { get; set; }

			[JsonProperty("reusableProfiles")]
			public List<ReusableProfileImport> ReusableProfiles { get; set; }
		}

		private sealed class ParameterImport
		{
			[JsonProperty("id")]
			public string Id { get; set; }

			[JsonProperty("name")]
			public string Name { get; set; }

			[JsonProperty("type")]
			public string Type { get; set; }

			[JsonProperty("allowedValues")]
			public List<string> AllowedValues { get; set; }

			[JsonProperty("defaultValue")]
			public JToken DefaultValue { get; set; }

			[JsonProperty("unit")]
			public string Unit { get; set; }

			[JsonProperty("minValue")]
			public double? MinValue { get; set; }

			[JsonProperty("maxValue")]
			public double? MaxValue { get; set; }
		}

		private sealed class ProfileDefinitionImport
		{
			[JsonProperty("id")]
			public string Id { get; set; }

			[JsonProperty("name")]
			public string Name { get; set; }

			[JsonProperty("parameters")]
			public List<ProfileDefinitionParameterImport> Parameters { get; set; }

			[JsonProperty("nestedProfiles")]
			public List<NestedProfileDefinitionImport> NestedProfiles { get; set; }
		}

		private sealed class ProfileDefinitionParameterImport
		{
			[JsonProperty("parameterId")]
			public string ParameterId { get; set; }

			[JsonProperty("required")]
			public bool Required { get; set; }
		}

		private sealed class NestedProfileDefinitionImport
		{
			[JsonProperty("profileDefinitionId")]
			public string ProfileDefinitionId { get; set; }

			[JsonProperty("required")]
			public bool Required { get; set; }

			[JsonProperty("allowMultiple")]
			public bool AllowMultiple { get; set; }
		}

		private sealed class ReusableProfileImport
		{
			[JsonProperty("id")]
			public string Id { get; set; }

			[JsonProperty("name")]
			public string Name { get; set; }

			[JsonProperty("profileDefinitionId")]
			public string ProfileDefinitionId { get; set; }

			[JsonProperty("values")]
			public Dictionary<string, JToken> Values { get; set; }

			[JsonProperty("nestedProfiles")]
			public Dictionary<string, List<NestedReusableProfileSelectionImport>> NestedProfiles { get; set; }
		}

		private sealed class NestedReusableProfileSelectionImport
		{
			[JsonProperty("mode")]
			public string Mode { get; set; }

			[JsonProperty("reusableProfileId")]
			public string ReusableProfileId { get; set; }
		}
	}
}

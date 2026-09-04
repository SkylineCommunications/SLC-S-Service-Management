/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE        VERSION        AUTHOR            COMMENTS

01/09/2026  1.0.0.1        SKA               Initial version
****************************************************************************
*/
namespace SLC_SM_Import_Service_Catalog
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Security.Cryptography;
	using System.Text;
	using DomHelpers.SlcConfigurations;
	using DomHelpers.SlcServicemanagement;
	using Newtonsoft.Json;
	using Newtonsoft.Json.Linq;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.IAS;
	using ConfigModels = Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models;
	using ServiceModels = Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement.Models;

	public class Script
	{
		private const string DefaultJsonPath = @"C:\Skyline DataMiner\Documents\DMA_COMMON_DOCUMENTS\service-catalog.json";
		private const string DefaultConfigurationStudioJsonPath = @"C:\Skyline DataMiner\Documents\DMA_COMMON_DOCUMENTS\configuration-studio.json";
		private const string DefaultWorkflowsJsonPath = @"C:\Skyline DataMiner\Documents\DMA_COMMON_DOCUMENTS\workflows.json";

		public void Run(IEngine engine)
		{
			try
			{
				RunSafe(engine);
			}
			catch (ScriptAbortException)
			{
			}
			catch (ScriptForceAbortException)
			{
			}
			catch (ScriptTimeoutException)
			{
			}
			catch (InteractiveUserDetachedException)
			{
			}
			catch (Exception e)
			{
				engine.ExitFail($"Run|{e.Message}");
			}
		}

		private static void RunSafe(IEngine engine)
		{
			string jsonPath = engine.ReadScriptParamFromApp("JSON File Path");
			if (String.IsNullOrWhiteSpace(jsonPath))
			{
				jsonPath = DefaultJsonPath;
			}

			string configurationStudioJsonPath = engine.ReadScriptParamFromApp("Configuration Studio JSON File Path");
			if (String.IsNullOrWhiteSpace(configurationStudioJsonPath))
			{
				configurationStudioJsonPath = DefaultConfigurationStudioJsonPath;
			}

			string workflowsJsonPath = ReadScriptParam(engine, "Workflows JSON File Path");
			if (String.IsNullOrWhiteSpace(workflowsJsonPath))
			{
				workflowsJsonPath = DefaultWorkflowsJsonPath;
			}

			if (!File.Exists(jsonPath))
			{
				throw new FileNotFoundException($"JSON import file was not found: '{jsonPath}'");
			}

			var payload = JsonConvert.DeserializeObject<ServiceCatalogRoot>(File.ReadAllText(jsonPath));
			if (payload?.ServiceSpecifications == null || payload.ServiceSpecifications.Count == 0)
			{
				throw new InvalidOperationException($"No service specifications were found in '{jsonPath}'.");
			}

			var configurationStudioSource = LoadConfigurationStudioSource(configurationStudioJsonPath);
			var workflowNameByTemplateId = ReadWorkflowTemplateNames(workflowsJsonPath);

			var serviceSpecHelper = new DataHelperServiceSpecification(engine.GetUserConnection());
			var configHelper = new DataHelpersConfigurations(engine.GetUserConnection());

			var existingSpecs = serviceSpecHelper.ReadBasicDetails();
			var existingById = existingSpecs.ToDictionary(x => x.ID, x => x);
			var existingByName = existingSpecs
				.Where(x => !String.IsNullOrWhiteSpace(x.Name))
				.GroupBy(x => x.Name, StringComparer.InvariantCultureIgnoreCase)
				.ToDictionary(x => x.Key, x => x.First(), StringComparer.InvariantCultureIgnoreCase);

			var configurationParametersById = configHelper.ConfigurationParameters.Read().ToDictionary(x => x.ID, x => x);
			var configurationParametersByName = configurationParametersById.Values
				.Where(x => !String.IsNullOrWhiteSpace(x.Name))
				.GroupBy(x => x.Name, StringComparer.InvariantCultureIgnoreCase)
				.ToDictionary(x => x.Key, x => x.First(), StringComparer.InvariantCultureIgnoreCase);
			var profileDefinitionsById = configHelper.ProfileDefinitions.Read().ToDictionary(x => x.ID, x => x);
			var profileDefinitionsByName = profileDefinitionsById.Values
				.Where(x => !String.IsNullOrWhiteSpace(x.Name))
				.GroupBy(x => x.Name, StringComparer.InvariantCultureIgnoreCase)
				.ToDictionary(x => x.Key, x => x.First(), StringComparer.InvariantCultureIgnoreCase);
			var reusableProfilesById = configHelper.Profiles.Read(ProfileExposers.IsReusable.Equal(true)).ToDictionary(x => x.ID, x => x);
			var reusableProfilesByName = reusableProfilesById.Values
				.Where(x => !String.IsNullOrWhiteSpace(x.Name))
				.GroupBy(x => x.Name, StringComparer.InvariantCultureIgnoreCase)
				.ToDictionary(x => x.Key, x => x.First(), StringComparer.InvariantCultureIgnoreCase);

			var sourceSpecificationNameById = (payload.ServiceSpecifications ?? new List<ServiceSpecificationImport>())
				.Where(s => s != null && !String.IsNullOrWhiteSpace(s.Id) && !String.IsNullOrWhiteSpace(s.Name))
				.GroupBy(s => s.Id, StringComparer.InvariantCultureIgnoreCase)
				.ToDictionary(g => g.Key, g => g.First().Name, StringComparer.InvariantCultureIgnoreCase);

			int created = 0;
			int updated = 0;
			int index = 0;
			var failures = new List<string>();

			foreach (var source in payload.ServiceSpecifications)
			{
				index++;

				if (source == null || String.IsNullOrWhiteSpace(source.Name))
				{
					failures.Add($"Service specification #{index} was skipped: a non-empty 'name' is required.");
					continue;
				}

				try
				{
					Guid deterministicId = ToDeterministicGuid($"service-spec:{source.Id ?? source.Name}");
				bool exists = existingById.TryGetValue(deterministicId, out var existingBasicSpec)
					|| existingByName.TryGetValue(source.Name, out existingBasicSpec);

				var spec = exists
					? serviceSpecHelper.Read(ServiceSpecificationExposers.Guid.Equal(existingBasicSpec.ID)).FirstOrDefault()
					: new ServiceModels.ServiceSpecification
					{
						ID = deterministicId,
					};

				if (spec == null)
				{
					spec = new ServiceModels.ServiceSpecification { ID = deterministicId };
				}

				spec.Name = source.Name;
				spec.Description = source.Description ?? String.Empty;
				spec.Icon = source.Icon ?? String.Empty;
				spec.ServiceItems = BuildServiceItems(source, sourceSpecificationNameById, workflowNameByTemplateId);
				spec.ServiceItemsRelationships = BuildServiceItemRelationships(source, spec.ServiceItems);
				spec.ConfigurationParameters = BuildSpecificationParameters(source, configurationStudioSource, configurationParametersById, configurationParametersByName);
				spec.ConfigurationProfiles = BuildSpecificationProfiles(
					source,
					spec.Name,
					configurationStudioSource,
					configurationParametersById,
					configurationParametersByName,
					profileDefinitionsById,
					profileDefinitionsByName,
					reusableProfilesById,
					reusableProfilesByName);

				serviceSpecHelper.CreateOrUpdate(spec);

				if (exists)
				{
					updated++;
				}
				else
				{
					created++;
					existingById[spec.ID] = spec;
					existingByName[spec.Name] = spec;
				}
				}
				catch (Exception e)
				{
					failures.Add($"Service specification '{source.Name}' was not imported: {e.Message}");
				}
			}

			engine.GenerateInformation($"[Import Service Catalog] Completed. Created: {created}, Updated: {updated}. Failed: {failures.Count}. Source file: {jsonPath}");

			if (failures.Count > 0)
			{
				engine.ExitFail($"[Import Service Catalog] {failures.Count} record(s) failed and were skipped: {String.Join(" | ", failures)}");
			}
		}

		private static List<ServiceModels.ServiceItem> BuildServiceItems(
			ServiceSpecificationImport source,
			Dictionary<string, string> sourceSpecificationNameById,
			Dictionary<string, string> workflowNameByTemplateId)
		{
			var items = new List<ServiceModels.ServiceItem>();
			long nextId = 1;

			foreach (var sourceItem in source.ServiceItems ?? Enumerable.Empty<ServiceItemImport>())
			{
				if (sourceItem == null)
				{
					continue;
				}

				string definitionReference = sourceItem.WorkflowTemplateId ?? String.Empty;
				var itemType = ParseServiceItemType(sourceItem.Type);

				if (itemType == SlcServicemanagementIds.Enums.ServiceitemtypesEnum.Service)
				{
					if (!String.IsNullOrWhiteSpace(sourceItem.ReferencedServiceSpecificationId)
						&& sourceSpecificationNameById.TryGetValue(sourceItem.ReferencedServiceSpecificationId, out string referencedSpecName))
					{
						definitionReference = referencedSpecName;
					}
					else
					{
						definitionReference = sourceItem.ReferencedServiceSpecificationId ?? String.Empty;
					}
				}
				else if (itemType == SlcServicemanagementIds.Enums.ServiceitemtypesEnum.Workflow
					&& !String.IsNullOrWhiteSpace(sourceItem.WorkflowTemplateId)
					&& workflowNameByTemplateId.TryGetValue(sourceItem.WorkflowTemplateId, out string workflowName)
					&& !String.IsNullOrWhiteSpace(workflowName))
				{
					definitionReference = workflowName;
				}

				items.Add(new ServiceModels.ServiceItem
				{
					ID = nextId++,
					Label = String.IsNullOrWhiteSpace(sourceItem.Label) ? sourceItem.Name ?? String.Empty : sourceItem.Label,
					Type = itemType,
					DefinitionReference = definitionReference,
					ImplementationReference = String.Empty,
					Icon = sourceItem.Icon ?? String.Empty,
					Script = String.Empty,
				});
			}

			return items;
		}

		private static List<ServiceModels.ServiceItemRelationShip> BuildServiceItemRelationships(
			ServiceSpecificationImport source,
			List<ServiceModels.ServiceItem> builtItems)
		{
			_ = builtItems;
			var relationships = new List<ServiceModels.ServiceItemRelationShip>();
			var sourceItemIds = (source.ServiceItems ?? new List<ServiceItemImport>())
				.Select((item, index) => new { Item = item, NumericId = index + 1L })
				.Where(x => x.Item != null && !String.IsNullOrWhiteSpace(x.Item.Id))
				.GroupBy(x => x.Item.Id, StringComparer.InvariantCultureIgnoreCase)
				.ToDictionary(g => g.Key, g => g.First().NumericId, StringComparer.InvariantCultureIgnoreCase);

			int relationshipIndex = 1;
			foreach (var topologyLink in source.Topology ?? Enumerable.Empty<TopologyLinkImport>())
			{
				if (topologyLink == null
					|| String.IsNullOrWhiteSpace(topologyLink.FromServiceItemId)
					|| String.IsNullOrWhiteSpace(topologyLink.ToServiceItemId))
				{
					continue;
				}

				if (!sourceItemIds.TryGetValue(topologyLink.FromServiceItemId, out long parentId)
					|| !sourceItemIds.TryGetValue(topologyLink.ToServiceItemId, out long childId))
				{
					continue;
				}

				// Parent default output ("1") -> child default input ("0").
				relationships.Add(new ServiceModels.ServiceItemRelationShip
				{
					Id = relationshipIndex.ToString(CultureInfo.InvariantCulture),
					Type = "Connection",
					ParentServiceItem = parentId.ToString(CultureInfo.InvariantCulture),
					ParentServiceItemInterfaceId = "1",
					ChildServiceItem = childId.ToString(CultureInfo.InvariantCulture),
					ChildServiceItemInterfaceId = "0",
				});
				relationshipIndex++;
			}

			return relationships;
		}

		private static string ReadScriptParam(IEngine engine, string name)
		{
			return engine.GetScriptParam(name)?.Value ?? String.Empty;
		}

		private static Dictionary<string, string> ReadWorkflowTemplateNames(string workflowsJsonPath)
		{
			if (!File.Exists(workflowsJsonPath))
			{
				return new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
			}

			var payload = JsonConvert.DeserializeObject<WorkflowsRoot>(File.ReadAllText(workflowsJsonPath));
			return payload?.WorkflowTemplates?
				.Where(w => w != null && !String.IsNullOrWhiteSpace(w.Id) && !String.IsNullOrWhiteSpace(w.Name))
				.GroupBy(w => w.Id, StringComparer.InvariantCultureIgnoreCase)
				.ToDictionary(g => g.Key, g => g.First().Name, StringComparer.InvariantCultureIgnoreCase)
				?? new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
		}

		private static List<ServiceModels.ServiceSpecificationConfigurationValue> BuildSpecificationParameters(
			ServiceSpecificationImport source,
			ConfigurationStudioSource configurationStudioSource,
			Dictionary<Guid, ConfigModels.ConfigurationParameter> configurationParametersById,
			Dictionary<string, ConfigModels.ConfigurationParameter> configurationParametersByName)
		{
			var parameters = new List<ServiceModels.ServiceSpecificationConfigurationValue>();
			foreach (var sourceParameter in source.Configurations ?? Enumerable.Empty<SpecificationParameterImport>())
			{
				if (sourceParameter == null || String.IsNullOrWhiteSpace(sourceParameter.ParameterId))
				{
					continue;
				}

				Guid parameterId = ToDeterministicGuid($"cfg:param:{sourceParameter.ParameterId}");
				configurationParametersById.TryGetValue(parameterId, out ConfigModels.ConfigurationParameter definition);
				if (definition == null
					&& configurationStudioSource.ParameterById.TryGetValue(sourceParameter.ParameterId, out var sourceDefinition)
					&& !String.IsNullOrWhiteSpace(sourceDefinition.Name))
				{
					configurationParametersByName.TryGetValue(sourceDefinition.Name, out definition);
				}

				var value = BuildParameterValueFromDefinition(definition, sourceParameter.ParameterId);
				ApplyDefault(value, sourceParameter.DefaultValue);

				parameters.Add(new ServiceModels.ServiceSpecificationConfigurationValue
				{
					ID = Guid.NewGuid(),
					ExposeAtServiceOrder = sourceParameter.ExposedOnOrder,
					MandatoryAtServiceOrder = sourceParameter.RequiredOnOrder,
					MandatoryAtService = false,
					ConfigurationParameter = value,
				});
			}

			return parameters;
		}

		private static List<ServiceModels.ServiceSpecificationProfile> BuildSpecificationProfiles(
			ServiceSpecificationImport source,
			string specificationName,
			ConfigurationStudioSource configurationStudioSource,
			Dictionary<Guid, ConfigModels.ConfigurationParameter> configurationParametersById,
			Dictionary<string, ConfigModels.ConfigurationParameter> configurationParametersByName,
			Dictionary<Guid, ConfigModels.ProfileDefinition> profileDefinitionsById,
			Dictionary<string, ConfigModels.ProfileDefinition> profileDefinitionsByName,
			Dictionary<Guid, ConfigModels.Profile> reusableProfilesById,
			Dictionary<string, ConfigModels.Profile> reusableProfilesByName)
		{
			var profiles = new List<ServiceModels.ServiceSpecificationProfile>();
			foreach (var sourceProfile in source.Profiles ?? Enumerable.Empty<SpecificationProfileImport>())
			{
				if (sourceProfile == null || String.IsNullOrWhiteSpace(sourceProfile.ProfileDefinitionId))
				{
					continue;
				}

				Guid profileDefinitionId = ToDeterministicGuid($"cfg:profile-definition:{sourceProfile.ProfileDefinitionId}");
				profileDefinitionsById.TryGetValue(profileDefinitionId, out ConfigModels.ProfileDefinition profileDefinition);
				if (profileDefinition == null)
				{
					profileDefinitionsByName.TryGetValue(sourceProfile.ProfileDefinitionId, out profileDefinition);
				}
				if (profileDefinition == null
					&& configurationStudioSource.ProfileDefinitionById.TryGetValue(sourceProfile.ProfileDefinitionId, out var sourceProfileDefinition)
					&& !String.IsNullOrWhiteSpace(sourceProfileDefinition.Name))
				{
					profileDefinitionsByName.TryGetValue(sourceProfileDefinition.Name, out profileDefinition);
				}

				ConfigModels.Profile selectedProfile = null;
				if (sourceProfile.AllowReusableProfile && !String.IsNullOrWhiteSpace(sourceProfile.DefaultReusableProfileId))
				{
					Guid reusableProfileId = ToDeterministicGuid($"cfg:reusable-profile:{sourceProfile.DefaultReusableProfileId}");
					reusableProfilesById.TryGetValue(reusableProfileId, out selectedProfile);
					if (selectedProfile == null)
					{
						reusableProfilesByName.TryGetValue(sourceProfile.DefaultReusableProfileId, out selectedProfile);
					}
					if (selectedProfile == null
						&& configurationStudioSource.ReusableProfileById.TryGetValue(sourceProfile.DefaultReusableProfileId, out var sourceReusableProfile)
						&& !String.IsNullOrWhiteSpace(sourceReusableProfile.Name))
					{
						reusableProfilesByName.TryGetValue(sourceReusableProfile.Name, out selectedProfile);
					}
				}

				if (selectedProfile == null && sourceProfile.AllowInstanceSpecific)
				{
					selectedProfile = BuildDefaultInstanceSpecificProfile(
						profileDefinition,
						sourceProfile.ProfileDefinitionId,
						specificationName,
						configurationParametersById,
						configurationParametersByName);
				}
				else if (selectedProfile == null)
				{
					// Fallback: keep the profile visible on the spec even when no reusable profile resolved.
					selectedProfile = BuildDefaultInstanceSpecificProfile(
						profileDefinition,
						sourceProfile.ProfileDefinitionId,
						specificationName,
						configurationParametersById,
						configurationParametersByName);
				}

				profiles.Add(new ServiceModels.ServiceSpecificationProfile
				{
					ID = Guid.NewGuid(),
					ExposeAtServiceOrder = sourceProfile.ExposedOnOrder,
					MandatoryAtServiceOrder = sourceProfile.RequiredOnOrder,
					MandatoryAtService = false,
					ProfileDefinition = profileDefinition ?? new ConfigModels.ProfileDefinition { ID = profileDefinitionId, Name = sourceProfile.ProfileDefinitionId },
					Profile = selectedProfile,
				});
			}

			return profiles;
		}

		private static ConfigModels.Profile BuildDefaultInstanceSpecificProfile(
			ConfigModels.ProfileDefinition profileDefinition,
			string sourceProfileDefinitionId,
			string specificationName,
			Dictionary<Guid, ConfigModels.ConfigurationParameter> configurationParametersById,
			Dictionary<string, ConfigModels.ConfigurationParameter> configurationParametersByName)
		{
			string profileName = $"{profileDefinition?.Name ?? sourceProfileDefinitionId} ({specificationName})";
			var profile = new ConfigModels.Profile
			{
				ID = Guid.NewGuid(),
				Name = profileName,
				ProfileDefinitionReference = profileDefinition?.ID ?? ToDeterministicGuid($"cfg:profile-definition:{sourceProfileDefinitionId}"),
				IsReusable = false,
				ConfigurationParameterValues = new List<ConfigModels.ConfigurationParameterValue>(),
				Profiles = new List<Guid>(),
				TestedProtocols = new List<ConfigModels.ProtocolTest>(),
			};

			foreach (var parameterRef in profileDefinition?.ConfigurationParameters ?? Enumerable.Empty<ConfigModels.ReferencedConfigurationParameters>())
			{
				if (!configurationParametersById.TryGetValue(parameterRef.ConfigurationParameter, out ConfigModels.ConfigurationParameter definition))
				{
					continue;
				}

				profile.ConfigurationParameterValues.Add(BuildParameterValueFromDefinition(definition, definition.Name));
			}

			return profile;
		}

		private static ConfigurationStudioSource LoadConfigurationStudioSource(string configurationStudioJsonPath)
		{
			if (String.IsNullOrWhiteSpace(configurationStudioJsonPath) || !File.Exists(configurationStudioJsonPath))
			{
				return new ConfigurationStudioSource();
			}

			var payload = JsonConvert.DeserializeObject<ConfigurationStudioRoot>(File.ReadAllText(configurationStudioJsonPath));
			if (payload == null)
			{
				return new ConfigurationStudioSource();
			}

			return new ConfigurationStudioSource
			{
				ParameterById = (payload.Parameters ?? new List<ConfigurationStudioParameterImport>())
					.Where(p => p != null && !String.IsNullOrWhiteSpace(p.Id))
					.ToDictionary(p => p.Id, p => p, StringComparer.InvariantCultureIgnoreCase),
				ProfileDefinitionById = (payload.ProfileDefinitions ?? new List<ConfigurationStudioProfileDefinitionImport>())
					.Where(p => p != null && !String.IsNullOrWhiteSpace(p.Id))
					.ToDictionary(p => p.Id, p => p, StringComparer.InvariantCultureIgnoreCase),
				ReusableProfileById = (payload.ReusableProfiles ?? new List<ConfigurationStudioReusableProfileImport>())
					.Where(p => p != null && !String.IsNullOrWhiteSpace(p.Id))
					.ToDictionary(p => p.Id, p => p, StringComparer.InvariantCultureIgnoreCase),
			};
		}

		private static ConfigModels.ConfigurationParameterValue BuildParameterValueFromDefinition(ConfigModels.ConfigurationParameter definition, string fallbackParameterId)
		{
			var value = new ConfigModels.ConfigurationParameterValue
			{
				ID = Guid.NewGuid(),
				ConfigurationParameterId = definition?.ID ?? ToDeterministicGuid($"cfg:param:{fallbackParameterId}"),
				Label = definition?.Name ?? fallbackParameterId,
				Type = definition?.Type ?? SlcConfigurationsIds.Enums.Type.Text,
				NumberOptions = CloneNumberOptions(definition?.NumberOptions),
				DiscreteOptions = CloneDiscreteOptions(definition?.DiscreteOptions),
				TextOptions = CloneTextOptions(definition?.TextOptions),
			};

			return value;
		}

		private static ConfigModels.NumberParameterOptions CloneNumberOptions(ConfigModels.NumberParameterOptions source)
		{
			if (source == null)
			{
				return null;
			}

			return new ConfigModels.NumberParameterOptions
			{
				ID = Guid.NewGuid(),
				MinRange = source.MinRange,
				MaxRange = source.MaxRange,
				StepSize = source.StepSize,
				Decimals = source.Decimals,
				DefaultValue = source.DefaultValue,
				DefaultUnit = source.DefaultUnit == null ? null : new ConfigModels.ConfigurationUnit { ID = Guid.NewGuid(), Name = source.DefaultUnit.Name },
				Units = source.Units?.Select(u => new ConfigModels.ConfigurationUnit { ID = Guid.NewGuid(), Name = u.Name }).ToList(),
			};
		}

		private static ConfigModels.DiscreteParameterOptions CloneDiscreteOptions(ConfigModels.DiscreteParameterOptions source)
		{
			if (source == null)
			{
				return null;
			}

			var discreteValues = source.DiscreteValues?
				.Select(d => new ConfigModels.DiscreteValue { ID = Guid.NewGuid(), Value = d.Value })
				.ToList() ?? new List<ConfigModels.DiscreteValue>();

			return new ConfigModels.DiscreteParameterOptions
			{
				ID = Guid.NewGuid(),
				DiscreteValues = discreteValues,
				Default = source.Default == null ? null : new ConfigModels.DiscreteValue { ID = Guid.NewGuid(), Value = source.Default.Value },
			};
		}

		private static ConfigModels.TextParameterOptions CloneTextOptions(ConfigModels.TextParameterOptions source)
		{
			if (source == null)
			{
				return null;
			}

			return new ConfigModels.TextParameterOptions
			{
				ID = Guid.NewGuid(),
				Default = source.Default,
			};
		}

		private static void ApplyDefault(ConfigModels.ConfigurationParameterValue value, JToken defaultValue)
		{
			if (value == null || defaultValue == null || defaultValue.Type == JTokenType.Null)
			{
				return;
			}

			switch (value.Type)
			{
				case SlcConfigurationsIds.Enums.Type.Number:
					if (Double.TryParse(defaultValue.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
					{
						value.DoubleValue = number;
						if (value.NumberOptions == null)
						{
							value.NumberOptions = new ConfigModels.NumberParameterOptions { ID = Guid.NewGuid(), StepSize = 1, Decimals = 0 };
						}

						value.NumberOptions.DefaultValue = number;
					}
					break;
				case SlcConfigurationsIds.Enums.Type.Discrete:
					string discrete = defaultValue.ToString();
					if (!String.IsNullOrWhiteSpace(discrete))
					{
						if (value.DiscreteOptions == null)
						{
							value.DiscreteOptions = new ConfigModels.DiscreteParameterOptions
							{
								ID = Guid.NewGuid(),
								DiscreteValues = new List<ConfigModels.DiscreteValue>(),
							};
						}

						if (!value.DiscreteOptions.DiscreteValues.Any(d => String.Equals(d.Value, discrete, StringComparison.InvariantCultureIgnoreCase)))
						{
							value.DiscreteOptions.DiscreteValues.Add(new ConfigModels.DiscreteValue { ID = Guid.NewGuid(), Value = discrete });
						}

						value.DiscreteOptions.Default = value.DiscreteOptions.DiscreteValues.FirstOrDefault(d => String.Equals(d.Value, discrete, StringComparison.InvariantCultureIgnoreCase))
							?? new ConfigModels.DiscreteValue { ID = Guid.NewGuid(), Value = discrete };
					}
					break;
				default:
					string text = defaultValue.ToString();
					value.StringValue = text;
					if (value.TextOptions == null)
					{
						value.TextOptions = new ConfigModels.TextParameterOptions { ID = Guid.NewGuid() };
					}

					value.TextOptions.Default = text;
					break;
			}
		}

		private static SlcServicemanagementIds.Enums.ServiceitemtypesEnum ParseServiceItemType(string type)
		{
			if (String.Equals(type, "Workflow", StringComparison.InvariantCultureIgnoreCase))
			{
				return SlcServicemanagementIds.Enums.ServiceitemtypesEnum.Workflow;
			}

			if (String.Equals(type, "SRMBooking", StringComparison.InvariantCultureIgnoreCase))
			{
				return SlcServicemanagementIds.Enums.ServiceitemtypesEnum.SRMBooking;
			}

			return SlcServicemanagementIds.Enums.ServiceitemtypesEnum.Service;
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

		private sealed class ServiceCatalogRoot
		{
			[JsonProperty("serviceSpecifications")]
			public List<ServiceSpecificationImport> ServiceSpecifications { get; set; }
		}

		private sealed class ServiceSpecificationImport
		{
			[JsonProperty("id")]
			public string Id { get; set; }

			[JsonProperty("name")]
			public string Name { get; set; }

			[JsonProperty("icon")]
			public string Icon { get; set; }

			[JsonProperty("description")]
			public string Description { get; set; }

			[JsonProperty("serviceItems")]
			public List<ServiceItemImport> ServiceItems { get; set; }

			[JsonProperty("topology")]
			public List<TopologyLinkImport> Topology { get; set; }

			[JsonProperty("configurations")]
			public List<SpecificationParameterImport> Configurations { get; set; }

			[JsonProperty("profiles")]
			public List<SpecificationProfileImport> Profiles { get; set; }
		}

		private sealed class ServiceItemImport
		{
			[JsonProperty("id")]
			public string Id { get; set; }

			[JsonProperty("name")]
			public string Name { get; set; }

			[JsonProperty("icon")]
			public string Icon { get; set; }

			[JsonProperty("type")]
			public string Type { get; set; }

			[JsonProperty("workflowTemplateId")]
			public string WorkflowTemplateId { get; set; }

			[JsonProperty("referencedServiceSpecificationId")]
			public string ReferencedServiceSpecificationId { get; set; }

			[JsonProperty("label")]
			public string Label { get; set; }
		}

		private sealed class TopologyLinkImport
		{
			[JsonProperty("fromServiceItemId")]
			public string FromServiceItemId { get; set; }

			[JsonProperty("toServiceItemId")]
			public string ToServiceItemId { get; set; }
		}

		private sealed class WorkflowsRoot
		{
			[JsonProperty("workflowTemplates")]
			public List<WorkflowTemplateImport> WorkflowTemplates { get; set; }
		}

		private sealed class WorkflowTemplateImport
		{
			[JsonProperty("id")]
			public string Id { get; set; }

			[JsonProperty("name")]
			public string Name { get; set; }
		}

		private sealed class SpecificationParameterImport
		{
			[JsonProperty("parameterId")]
			public string ParameterId { get; set; }

			[JsonProperty("exposedOnOrder")]
			public bool ExposedOnOrder { get; set; }

			[JsonProperty("requiredOnOrder")]
			public bool RequiredOnOrder { get; set; }

			[JsonProperty("defaultValue")]
			public JToken DefaultValue { get; set; }
		}

		private sealed class SpecificationProfileImport
		{
			[JsonProperty("profileDefinitionId")]
			public string ProfileDefinitionId { get; set; }

			[JsonProperty("exposedOnOrder")]
			public bool ExposedOnOrder { get; set; }

			[JsonProperty("requiredOnOrder")]
			public bool RequiredOnOrder { get; set; }

			[JsonProperty("allowReusableProfile")]
			public bool AllowReusableProfile { get; set; }

			[JsonProperty("allowInstanceSpecific")]
			public bool AllowInstanceSpecific { get; set; }

			[JsonProperty("defaultReusableProfileId")]
			public string DefaultReusableProfileId { get; set; }
		}

		private sealed class ConfigurationStudioSource
		{
			public Dictionary<string, ConfigurationStudioParameterImport> ParameterById { get; set; }
				= new Dictionary<string, ConfigurationStudioParameterImport>(StringComparer.InvariantCultureIgnoreCase);

			public Dictionary<string, ConfigurationStudioProfileDefinitionImport> ProfileDefinitionById { get; set; }
				= new Dictionary<string, ConfigurationStudioProfileDefinitionImport>(StringComparer.InvariantCultureIgnoreCase);

			public Dictionary<string, ConfigurationStudioReusableProfileImport> ReusableProfileById { get; set; }
				= new Dictionary<string, ConfigurationStudioReusableProfileImport>(StringComparer.InvariantCultureIgnoreCase);
		}

		private sealed class ConfigurationStudioRoot
		{
			[JsonProperty("parameters")]
			public List<ConfigurationStudioParameterImport> Parameters { get; set; }

			[JsonProperty("profileDefinitions")]
			public List<ConfigurationStudioProfileDefinitionImport> ProfileDefinitions { get; set; }

			[JsonProperty("reusableProfiles")]
			public List<ConfigurationStudioReusableProfileImport> ReusableProfiles { get; set; }
		}

		private sealed class ConfigurationStudioParameterImport
		{
			[JsonProperty("id")]
			public string Id { get; set; }

			[JsonProperty("name")]
			public string Name { get; set; }
		}

		private sealed class ConfigurationStudioProfileDefinitionImport
		{
			[JsonProperty("id")]
			public string Id { get; set; }

			[JsonProperty("name")]
			public string Name { get; set; }
		}

		private sealed class ConfigurationStudioReusableProfileImport
		{
			[JsonProperty("id")]
			public string Id { get; set; }

			[JsonProperty("name")]
			public string Name { get; set; }
		}
	}
}

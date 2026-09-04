/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE        VERSION        AUTHOR            COMMENTS

31/08/2026  1.0.0.1        SKA           Initial version
****************************************************************************
*/
namespace SLC_SM_Import_Service_Inventory
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
	using SLC_SM_Common.Dom;
	using ConfigModels = Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models;
	using ServiceModels = Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement.Models;
	using static DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Service_Behavior;

	public class Script
	{
		private const string DefaultInventoryJsonPath = @"C:\Skyline DataMiner\Documents\DMA_COMMON_DOCUMENTS\service-inventory.json";
		private const string DefaultCategoriesJsonPath = @"C:\Skyline DataMiner\Documents\DMA_COMMON_DOCUMENTS\categories.json";
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
			string inventoryJsonPath = engine.ReadScriptParamFromApp("JSON File Path");
			if (String.IsNullOrWhiteSpace(inventoryJsonPath))
			{
				inventoryJsonPath = DefaultInventoryJsonPath;
			}

			string categoriesJsonPath = engine.ReadScriptParamFromApp("Categories JSON File Path");
			if (String.IsNullOrWhiteSpace(categoriesJsonPath))
			{
				categoriesJsonPath = DefaultCategoriesJsonPath;
			}

			string configurationStudioJsonPath = engine.ReadScriptParamFromApp("Configuration Studio JSON File Path");
			if (String.IsNullOrWhiteSpace(configurationStudioJsonPath))
			{
				configurationStudioJsonPath = DefaultConfigurationStudioJsonPath;
			}

			string workflowsJsonPath = engine.GetScriptParam("Workflows JSON File Path")?.Value;
			if (String.IsNullOrWhiteSpace(workflowsJsonPath))
			{
				workflowsJsonPath = DefaultWorkflowsJsonPath;
			}

			if (!File.Exists(inventoryJsonPath))
			{
				throw new FileNotFoundException($"JSON import file was not found: '{inventoryJsonPath}'");
			}

			var payload = JsonConvert.DeserializeObject<ServiceInventoryRoot>(File.ReadAllText(inventoryJsonPath));
			if (payload?.Services == null || payload.Services.Count == 0)
			{
				throw new InvalidOperationException($"No services were found in '{inventoryJsonPath}'.");
			}

			var categorySourceById = ReadCategorySources(categoriesJsonPath);
			var configurationStudioSource = ReadConfigurationStudioSource(configurationStudioJsonPath);
			var workflowNameByTemplateId = ReadWorkflowTemplateNames(workflowsJsonPath);
			var repo = new DataHelpersServiceManagement(engine.GetUserConnection());
			var configRepo = new DataHelpersConfigurations(engine.GetUserConnection());

			var existingServices = repo.Services.ReadBasicDetails();
			var existingByServiceId = existingServices
				.Where(x => !String.IsNullOrWhiteSpace(x.ServiceID))
				.GroupBy(x => x.ServiceID, StringComparer.InvariantCultureIgnoreCase)
				.ToDictionary(g => g.Key, g => g.First(), StringComparer.InvariantCultureIgnoreCase);

			var categories = repo.ServiceCategories.ReadBasicDetails();
			var categoriesByNameAndType = categories
				.Where(c => !String.IsNullOrWhiteSpace(c.Name))
				.GroupBy(c => BuildCategoryKey(c.Type, c.Name), StringComparer.InvariantCultureIgnoreCase)
				.ToDictionary(g => g.Key, g => g.First(), StringComparer.InvariantCultureIgnoreCase);

			var specsByName = repo.ServiceSpecifications.ReadBasicDetails()
				.Where(s => !String.IsNullOrWhiteSpace(s.Name))
				.GroupBy(s => s.Name, StringComparer.InvariantCultureIgnoreCase)
				.ToDictionary(g => g.Key, g => g.First(), StringComparer.InvariantCultureIgnoreCase);
			var specsById = repo.ServiceSpecifications.ReadBasicDetails()
				.GroupBy(s => s.ID)
				.ToDictionary(g => g.Key, g => g.First());

			var reusableProfilesById = configRepo.Profiles.Read(ProfileExposers.IsReusable.Equal(true))
				.Where(p => p.IsReusable)
				.ToDictionary(p => p.ID, p => p);
			var reusableProfilesByName = reusableProfilesById.Values
				.Where(p => !String.IsNullOrWhiteSpace(p.Name))
				.GroupBy(p => p.Name, StringComparer.InvariantCultureIgnoreCase)
				.ToDictionary(g => g.Key, g => g.First(), StringComparer.InvariantCultureIgnoreCase);

			var configurationParametersById = configRepo.ConfigurationParameters.Read()
				.ToDictionary(p => p.ID, p => p);
			var configurationParametersByName = configurationParametersById.Values
				.Where(p => !String.IsNullOrWhiteSpace(p.Name))
				.GroupBy(p => p.Name, StringComparer.InvariantCultureIgnoreCase)
				.ToDictionary(g => g.Key, g => g.First(), StringComparer.InvariantCultureIgnoreCase);
			var profileDefinitionsById = configRepo.ProfileDefinitions.Read()
				.ToDictionary(p => p.ID, p => p);
			var profileDefinitionsByName = profileDefinitionsById.Values
				.Where(p => !String.IsNullOrWhiteSpace(p.Name))
				.GroupBy(p => p.Name, StringComparer.InvariantCultureIgnoreCase)
				.ToDictionary(g => g.Key, g => g.First(), StringComparer.InvariantCultureIgnoreCase);

			int created = 0;
			int updated = 0;
			var importedServiceIdToDomId = new Dictionary<string, Guid>(StringComparer.InvariantCultureIgnoreCase);
			var sourceServicesById = (payload.Services ?? new List<ServiceImport>())
				.Where(s => s != null && !String.IsNullOrWhiteSpace(s.Id))
				.GroupBy(s => s.Id, StringComparer.InvariantCultureIgnoreCase)
				.ToDictionary(g => g.Key, g => g.First(), StringComparer.InvariantCultureIgnoreCase);

			foreach (var source in payload.Services)
			{
				if (source == null || String.IsNullOrWhiteSpace(source.SystemServiceId))
				{
					continue;
				}

				bool exists = existingByServiceId.TryGetValue(source.SystemServiceId, out ServiceModels.Service basicService);
				var service = exists
					? repo.Services.Read(ServiceExposers.Guid.Equal(basicService.ID)).FirstOrDefault()
					: new ServiceModels.Service
					{
						ID = Guid.NewGuid(),
						ServiceItems = new List<ServiceModels.ServiceItem>(),
						ServiceItemsRelationships = new List<ServiceModels.ServiceItemRelationShip>(),
					};

				if (service == null)
				{
					service = new ServiceModels.Service
					{
						ID = Guid.NewGuid(),
						ServiceItems = new List<ServiceModels.ServiceItem>(),
						ServiceItemsRelationships = new List<ServiceModels.ServiceItemRelationShip>(),
					};
				}

				service.ServiceID = source.SystemServiceId;
				service.Name = String.IsNullOrWhiteSpace(source.Name) ? source.SystemServiceId : source.Name;
				service.Description = BuildServiceDescription(source);
				service.StartTime = source.StartDate?.ToUniversalTime();
				service.EndTime = source.EndDate?.ToUniversalTime();
				service.GenerateMonitoringService = false;

				service.ServiceSpecificationId = ResolveServiceSpecificationId(source.ServiceSpecificationId, specsByName, specsById);

				service.Category = ResolveCategory(source.CategoryId, categorySourceById, categoriesByNameAndType);
				service.Icon = source.ServiceItems?.FirstOrDefault(i => !String.IsNullOrWhiteSpace(i.Icon))?.Icon
					?? service.Category?.Icon
					?? String.Empty;

				service.ServiceConfiguration = BuildServiceConfiguration(
					source,
					configurationStudioSource,
					configurationParametersById,
					configurationParametersByName,
					reusableProfilesById,
					reusableProfilesByName,
					profileDefinitionsById,
					profileDefinitionsByName);

				BuildServiceItemsAndRelationships(service, source, workflowNameByTemplateId);

				Guid domId = repo.Services.CreateOrUpdate(service);
				importedServiceIdToDomId[source.Id] = domId;

				if (exists)
				{
					updated++;
				}
				else
				{
					created++;
					existingByServiceId[source.SystemServiceId] = new ServiceModels.Service { ID = domId, ServiceID = source.SystemServiceId, Name = service.Name };
				}
			}

			// Second pass to resolve linked service references for service items.
			foreach (var source in payload.Services.Where(s => s?.ServiceItems != null))
			{
				if (!importedServiceIdToDomId.TryGetValue(source.Id, out Guid currentServiceDomId))
				{
					continue;
				}

				var currentService = repo.Services.Read(ServiceExposers.Guid.Equal(currentServiceDomId)).FirstOrDefault();
				if (currentService == null)
				{
					continue;
				}

				var currentItems = currentService.ServiceItems ?? new List<ServiceModels.ServiceItem>();
				var sourceItemIdToNumericId = BuildSourceItemNumericIdMap(source.ServiceItems);
				foreach (var sourceItem in source.ServiceItems)
				{
					if (sourceItem == null)
					{
						continue;
					}

					var targetItem = ResolveTargetServiceItem(currentItems, sourceItem, sourceItemIdToNumericId, workflowNameByTemplateId);
					if (targetItem == null)
					{
						continue;
					}

					if (String.Equals(sourceItem.Type, "Service", StringComparison.InvariantCultureIgnoreCase))
					{
						if (TryResolveLinkedServiceDomId(sourceItem.LinkedServiceId, importedServiceIdToDomId, sourceServicesById, existingByServiceId, out Guid linkedDomId))
						{
							targetItem.ImplementationReference = linkedDomId.ToString();
						}
					}
					else if (String.Equals(sourceItem.Type, "Workflow", StringComparison.InvariantCultureIgnoreCase))
					{
						string definitionReference = ResolveWorkflowDefinitionReference(sourceItem.WorkflowTemplateId, workflowNameByTemplateId);
						if (!String.IsNullOrWhiteSpace(definitionReference))
						{
							targetItem.DefinitionReference = definitionReference;
						}

						if (Guid.TryParse(sourceItem.JobId, out Guid explicitJobDomId))
						{
							targetItem.ImplementationReference = explicitJobDomId.ToString();
						}
					}
				}

				repo.Services.CreateOrUpdate(currentService);

				ApplyState(source.State, currentService, repo.Services, engine);
			}

			engine.GenerateInformation($"[Import Service Inventory] Completed. Created: {created}, Updated: {updated}. Source file: {inventoryJsonPath}");
		}

		private static Dictionary<string, CategoryImport> ReadCategorySources(string categoriesJsonPath)
		{
			if (!File.Exists(categoriesJsonPath))
			{
				return new Dictionary<string, CategoryImport>(StringComparer.InvariantCultureIgnoreCase);
			}

			var payload = JsonConvert.DeserializeObject<CategoriesRoot>(File.ReadAllText(categoriesJsonPath));
			return payload?.Categories?
				.Where(c => c != null && !String.IsNullOrWhiteSpace(c.Id))
				.GroupBy(c => c.Id, StringComparer.InvariantCultureIgnoreCase)
				.ToDictionary(g => g.Key, g => g.First(), StringComparer.InvariantCultureIgnoreCase)
				?? new Dictionary<string, CategoryImport>(StringComparer.InvariantCultureIgnoreCase);
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

		private static Guid? ResolveServiceSpecificationId(
			string sourceServiceSpecificationId,
			Dictionary<string, ServiceModels.ServiceSpecification> specsByName,
			Dictionary<Guid, ServiceModels.ServiceSpecification> specsById)
		{
			if (String.IsNullOrWhiteSpace(sourceServiceSpecificationId))
			{
				return null;
			}

			if (specsByName.TryGetValue(sourceServiceSpecificationId, out ServiceModels.ServiceSpecification byName))
			{
				return byName.ID;
			}

			if (Guid.TryParse(sourceServiceSpecificationId, out Guid explicitGuid) && specsById.ContainsKey(explicitGuid))
			{
				return explicitGuid;
			}

			Guid deterministicGuid = ToDeterministicGuid($"service-spec:{sourceServiceSpecificationId}");
			if (specsById.ContainsKey(deterministicGuid))
			{
				return deterministicGuid;
			}

			return null;
		}

		private static Dictionary<string, long> BuildSourceItemNumericIdMap(List<ServiceItemImport> sourceItems)
		{
			var map = new Dictionary<string, long>(StringComparer.InvariantCultureIgnoreCase);
			if (sourceItems == null)
			{
				return map;
			}

			long next = 1;
			foreach (ServiceItemImport sourceItem in sourceItems)
			{
				if (!String.IsNullOrWhiteSpace(sourceItem?.Id))
				{
					map[sourceItem.Id] = next;
				}

				next++;
			}

			return map;
		}

		private static ServiceModels.ServiceItem ResolveTargetServiceItem(
			List<ServiceModels.ServiceItem> currentItems,
			ServiceItemImport sourceItem,
			Dictionary<string, long> sourceItemIdToNumericId,
			Dictionary<string, string> workflowNameByTemplateId)
		{
			if (currentItems == null || sourceItem == null)
			{
				return null;
			}

			if (!String.IsNullOrWhiteSpace(sourceItem.Id)
				&& sourceItemIdToNumericId.TryGetValue(sourceItem.Id, out long numericId))
			{
				ServiceModels.ServiceItem byId = currentItems.FirstOrDefault(i => i != null && i.ID == numericId);
				if (byId != null)
				{
					return byId;
				}
			}

			SlcServicemanagementIds.Enums.ServiceitemtypesEnum sourceType = ParseServiceItemType(sourceItem.Type);
			string desiredLabel = FirstNonEmpty(sourceItem.Label, sourceItem.Name);
			string desiredDefinition = ResolveWorkflowDefinitionReference(sourceItem.WorkflowTemplateId, workflowNameByTemplateId);

			return currentItems.FirstOrDefault(i =>
				i != null
				&& i.Type == sourceType
				&& (String.IsNullOrWhiteSpace(desiredLabel) || String.Equals(i.Label, desiredLabel, StringComparison.InvariantCultureIgnoreCase))
				&& (sourceType != SlcServicemanagementIds.Enums.ServiceitemtypesEnum.Workflow
					|| String.IsNullOrWhiteSpace(desiredDefinition)
					|| String.Equals(i.DefinitionReference, desiredDefinition, StringComparison.InvariantCultureIgnoreCase)
					|| String.Equals(i.DefinitionReference, sourceItem.WorkflowTemplateId ?? String.Empty, StringComparison.InvariantCultureIgnoreCase)));
		}

		private static bool TryResolveLinkedServiceDomId(
			string linkedServiceId,
			Dictionary<string, Guid> importedServiceIdToDomId,
			Dictionary<string, ServiceImport> sourceServicesById,
			Dictionary<string, ServiceModels.Service> existingByServiceId,
			out Guid linkedDomId)
		{
			linkedDomId = Guid.Empty;
			if (String.IsNullOrWhiteSpace(linkedServiceId))
			{
				return false;
			}

			if (importedServiceIdToDomId.TryGetValue(linkedServiceId, out linkedDomId))
			{
				return linkedDomId != Guid.Empty;
			}

			if (sourceServicesById.TryGetValue(linkedServiceId, out ServiceImport linkedSource)
				&& !String.IsNullOrWhiteSpace(linkedSource?.SystemServiceId)
				&& existingByServiceId.TryGetValue(linkedSource.SystemServiceId, out ServiceModels.Service linkedService))
			{
				linkedDomId = linkedService.ID;
				return linkedDomId != Guid.Empty;
			}

			if (Guid.TryParse(linkedServiceId, out Guid explicitGuid))
			{
				linkedDomId = explicitGuid;
				return true;
			}

			return false;
		}

		private static string ResolveWorkflowDefinitionReference(string workflowTemplateId, Dictionary<string, string> workflowNameByTemplateId)
		{
			if (String.IsNullOrWhiteSpace(workflowTemplateId))
			{
				return String.Empty;
			}

			if (workflowNameByTemplateId.TryGetValue(workflowTemplateId, out string workflowName) && !String.IsNullOrWhiteSpace(workflowName))
			{
				return workflowName;
			}

			return workflowTemplateId;
		}

		private static string FirstNonEmpty(params string[] values)
		{
			foreach (string value in values)
			{
				if (!String.IsNullOrWhiteSpace(value))
				{
					return value;
				}
			}

			return String.Empty;
		}

		private static ConfigurationStudioSource ReadConfigurationStudioSource(string configurationStudioJsonPath)
		{
			if (!File.Exists(configurationStudioJsonPath))
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
					.GroupBy(p => p.Id, StringComparer.InvariantCultureIgnoreCase)
					.ToDictionary(g => g.Key, g => g.First(), StringComparer.InvariantCultureIgnoreCase),
				ProfileDefinitionById = (payload.ProfileDefinitions ?? new List<ConfigurationStudioProfileDefinitionImport>())
					.Where(p => p != null && !String.IsNullOrWhiteSpace(p.Id))
					.GroupBy(p => p.Id, StringComparer.InvariantCultureIgnoreCase)
					.ToDictionary(g => g.Key, g => g.First(), StringComparer.InvariantCultureIgnoreCase),
				ReusableProfileById = (payload.ReusableProfiles ?? new List<ConfigurationStudioReusableProfileImport>())
					.Where(p => p != null && !String.IsNullOrWhiteSpace(p.Id))
					.GroupBy(p => p.Id, StringComparer.InvariantCultureIgnoreCase)
					.ToDictionary(g => g.Key, g => g.First(), StringComparer.InvariantCultureIgnoreCase),
			};
		}

		private static ServiceModels.ServiceCategory ResolveCategory(
			string sourceCategoryId,
			Dictionary<string, CategoryImport> categorySourceById,
			Dictionary<string, ServiceModels.ServiceCategory> categoriesByNameAndType)
		{
			if (String.IsNullOrWhiteSpace(sourceCategoryId))
			{
				return null;
			}

			if (!categorySourceById.TryGetValue(sourceCategoryId, out CategoryImport sourceCategory))
			{
				return null;
			}

			categoriesByNameAndType.TryGetValue(BuildCategoryKey(sourceCategory.CategoryType, sourceCategory.CategoryName), out ServiceModels.ServiceCategory category);
			return category;
		}

		private static string BuildCategoryKey(string type, string name)
		{
			return $"{type ?? String.Empty}|{name ?? String.Empty}";
		}

		private static ServiceModels.ServiceConfigurationVersion BuildServiceConfiguration(
			ServiceImport source,
			ConfigurationStudioSource configurationStudioSource,
			Dictionary<Guid, ConfigModels.ConfigurationParameter> configurationParametersById,
			Dictionary<string, ConfigModels.ConfigurationParameter> configurationParametersByName,
			Dictionary<Guid, ConfigModels.Profile> reusableProfilesById,
			Dictionary<string, ConfigModels.Profile> reusableProfilesByName,
			Dictionary<Guid, ConfigModels.ProfileDefinition> profileDefinitionsById,
			Dictionary<string, ConfigModels.ProfileDefinition> profileDefinitionsByName)
		{
			var configurationVersion = new ServiceModels.ServiceConfigurationVersion
			{
				ID = Guid.NewGuid(),
				VersionName = "Imported",
				Description = "Imported from service-inventory.json",
				CreatedAt = DateTime.UtcNow,
				Parameters = new List<ServiceModels.ServiceConfigurationValue>(),
				Profiles = new List<ServiceModels.ServiceProfile>(),
			};

			foreach (var characteristic in source.Characteristics ?? Enumerable.Empty<CharacteristicImport>())
			{
				if (characteristic == null || String.IsNullOrWhiteSpace(characteristic.ParameterId))
				{
					continue;
				}

				Guid parameterGuid = ToDeterministicGuid($"cfg:param:{characteristic.ParameterId}");
				configurationParametersById.TryGetValue(parameterGuid, out ConfigModels.ConfigurationParameter parameterDefinition);
				configurationStudioSource.ParameterById.TryGetValue(characteristic.ParameterId, out ConfigurationStudioParameterImport sourceParameterDefinition);
				if (parameterDefinition == null && !String.IsNullOrWhiteSpace(sourceParameterDefinition?.Name))
				{
					configurationParametersByName.TryGetValue(sourceParameterDefinition.Name, out parameterDefinition);
				}

				var parameterValue = new ConfigModels.ConfigurationParameterValue
				{
					ID = Guid.NewGuid(),
					ConfigurationParameterId = parameterDefinition?.ID ?? parameterGuid,
					Label = parameterDefinition?.Name ?? sourceParameterDefinition?.Name ?? characteristic.ParameterId,
					Type = parameterDefinition?.Type ?? ParseSourceParameterTypeOrInfer(sourceParameterDefinition?.Type, characteristic.Value),
				};

				ApplyValueToConfigurationParameterValue(parameterValue, characteristic.Value);

				configurationVersion.Parameters.Add(new ServiceModels.ServiceConfigurationValue
				{
					ID = Guid.NewGuid(),
					Mandatory = false,
					ConfigurationParameter = parameterValue,
				});
			}

			foreach (var profileSelection in source.Profiles ?? Enumerable.Empty<ServiceProfileSelectionImport>())
			{
				if (profileSelection == null || String.IsNullOrWhiteSpace(profileSelection.ProfileDefinitionId))
				{
					continue;
				}

				Guid profileDefinitionGuid = ToDeterministicGuid($"cfg:profile-definition:{profileSelection.ProfileDefinitionId}");
				profileDefinitionsById.TryGetValue(profileDefinitionGuid, out ConfigModels.ProfileDefinition profileDefinition);
				configurationStudioSource.ProfileDefinitionById.TryGetValue(profileSelection.ProfileDefinitionId, out ConfigurationStudioProfileDefinitionImport sourceProfileDefinition);
				if (profileDefinition == null && !String.IsNullOrWhiteSpace(sourceProfileDefinition?.Name))
				{
					profileDefinitionsByName.TryGetValue(sourceProfileDefinition.Name, out profileDefinition);
				}

				var serviceProfile = new ServiceModels.ServiceProfile
				{
					ID = Guid.NewGuid(),
					Mandatory = false,
					ProfileDefinition = profileDefinition ?? new ConfigModels.ProfileDefinition
					{
						ID = profileDefinitionGuid,
						Name = sourceProfileDefinition?.Name ?? profileSelection.ProfileDefinitionId,
					},
					Profile = null,
				};

				if (String.Equals(profileSelection.Mode, "reusable", StringComparison.InvariantCultureIgnoreCase)
					&& !String.IsNullOrWhiteSpace(profileSelection.ReusableProfileId))
				{
					Guid reusableProfileGuid = ToDeterministicGuid($"cfg:reusable-profile:{profileSelection.ReusableProfileId}");
					reusableProfilesById.TryGetValue(reusableProfileGuid, out ConfigModels.Profile reusableProfile);
					configurationStudioSource.ReusableProfileById.TryGetValue(profileSelection.ReusableProfileId, out ConfigurationStudioReusableProfileImport sourceReusableProfile);
					if (reusableProfile == null && !String.IsNullOrWhiteSpace(sourceReusableProfile?.Name))
					{
						reusableProfilesByName.TryGetValue(sourceReusableProfile.Name, out reusableProfile);
					}
					serviceProfile.Profile = reusableProfile;
				}
				else if (String.Equals(profileSelection.Mode, "instanceSpecific", StringComparison.InvariantCultureIgnoreCase))
				{
					serviceProfile.Profile = BuildInstanceSpecificProfile(profileSelection, configurationStudioSource, configurationParametersByName);
				}

				configurationVersion.Profiles.Add(serviceProfile);
			}

			return configurationVersion;
		}

		private static ConfigModels.Profile BuildInstanceSpecificProfile(
			ServiceProfileSelectionImport profileSelection,
			ConfigurationStudioSource configurationStudioSource,
			Dictionary<string, ConfigModels.ConfigurationParameter> configurationParametersByName)
		{
			var profile = new ConfigModels.Profile
			{
				ID = Guid.NewGuid(),
				Name = $"Imported {profileSelection.ProfileDefinitionId}",
				ProfileDefinitionReference = ToDeterministicGuid($"cfg:profile-definition:{profileSelection.ProfileDefinitionId}"),
				IsReusable = false,
				ConfigurationParameterValues = new List<ConfigModels.ConfigurationParameterValue>(),
				Profiles = new List<Guid>(),
				TestedProtocols = new List<ConfigModels.ProtocolTest>(),
			};

			foreach (var value in profileSelection.InstanceValues ?? new Dictionary<string, JToken>())
			{
				configurationStudioSource.ParameterById.TryGetValue(value.Key, out ConfigurationStudioParameterImport sourceParameter);
				ConfigModels.ConfigurationParameter existingParameter = null;
				if (!String.IsNullOrWhiteSpace(sourceParameter?.Name))
				{
					configurationParametersByName.TryGetValue(sourceParameter.Name, out existingParameter);
				}

				var profileValue = new ConfigModels.ConfigurationParameterValue
				{
					ID = Guid.NewGuid(),
					ConfigurationParameterId = existingParameter?.ID ?? ToDeterministicGuid($"cfg:param:{value.Key}"),
					Label = existingParameter?.Name ?? sourceParameter?.Name ?? value.Key,
					Type = existingParameter?.Type ?? ParseSourceParameterTypeOrInfer(sourceParameter?.Type, value.Value),
				};
				ApplyValueToConfigurationParameterValue(profileValue, value.Value);
				profile.ConfigurationParameterValues.Add(profileValue);
			}

			return profile;
		}

		private static void BuildServiceItemsAndRelationships(
			ServiceModels.Service service,
			ServiceImport source,
			Dictionary<string, string> workflowNameByTemplateId)
		{
			service.ServiceItems = new List<ServiceModels.ServiceItem>();
			service.ServiceItemsRelationships = new List<ServiceModels.ServiceItemRelationShip>();

			var sourceItemIdToNumericId = new Dictionary<string, long>(StringComparer.InvariantCultureIgnoreCase);
			long nextItemId = 1;

			foreach (var sourceItem in source.ServiceItems ?? Enumerable.Empty<ServiceItemImport>())
			{
				if (sourceItem == null)
				{
					continue;
				}

				long itemId = nextItemId++;
				if (!String.IsNullOrWhiteSpace(sourceItem.Id))
				{
					sourceItemIdToNumericId[sourceItem.Id] = itemId;
				}

				var serviceItemType = ParseServiceItemType(sourceItem.Type);
				service.ServiceItems.Add(new ServiceModels.ServiceItem
				{
					ID = itemId,
					Label = String.IsNullOrWhiteSpace(sourceItem.Label) ? sourceItem.Name : sourceItem.Label,
					Type = serviceItemType,
					DefinitionReference = serviceItemType == SlcServicemanagementIds.Enums.ServiceitemtypesEnum.Workflow
						? ResolveWorkflowDefinitionReference(sourceItem.WorkflowTemplateId, workflowNameByTemplateId)
						: sourceItem.WorkflowTemplateId ?? String.Empty,
					ImplementationReference = String.Empty,
					Icon = sourceItem.Icon ?? String.Empty,
					Script = String.Empty,
				});
			}

			int relationshipId = 1;
			foreach (var sourceTopology in source.Topology ?? Enumerable.Empty<TopologyLinkImport>())
			{
				if (sourceTopology == null
					|| String.IsNullOrWhiteSpace(sourceTopology.FromServiceItemId)
					|| String.IsNullOrWhiteSpace(sourceTopology.ToServiceItemId))
				{
					continue;
				}

				if (!sourceItemIdToNumericId.TryGetValue(sourceTopology.FromServiceItemId, out long parentItemId)
					|| !sourceItemIdToNumericId.TryGetValue(sourceTopology.ToServiceItemId, out long childItemId))
				{
					continue;
				}

				service.ServiceItemsRelationships.Add(new ServiceModels.ServiceItemRelationShip
				{
					Id = (relationshipId++).ToString(CultureInfo.InvariantCulture),
					Type = "Flow",
					ParentServiceItem = parentItemId.ToString(CultureInfo.InvariantCulture),
					ChildServiceItem = childItemId.ToString(CultureInfo.InvariantCulture),
					ParentServiceItemInterfaceId = "1",
					ChildServiceItemInterfaceId = "2",
				});
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

		private static string BuildServiceDescription(ServiceImport source)
		{
			var details = new List<string>();
			if (!String.IsNullOrWhiteSpace(source.State))
			{
				details.Add($"Source state: {source.State}");
			}

			if (!String.IsNullOrWhiteSpace(source.BasedOnServiceOrderId))
			{
				details.Add($"Order: {source.BasedOnServiceOrderId}");
			}

			return details.Count == 0 ? "Imported from service-inventory.json" : String.Join(" | ", details);
		}

		private static SlcConfigurationsIds.Enums.Type InferParameterType(JToken token)
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

		private static SlcConfigurationsIds.Enums.Type ParseSourceParameterTypeOrInfer(string sourceType, JToken token)
		{
			if (!String.IsNullOrWhiteSpace(sourceType))
			{
				switch (sourceType.Trim().ToLowerInvariant())
				{
					case "text":
						return SlcConfigurationsIds.Enums.Type.Text;
					case "number":
						return SlcConfigurationsIds.Enums.Type.Number;
					case "discrete":
						return SlcConfigurationsIds.Enums.Type.Discrete;
				}
			}

			return InferParameterType(token);
		}

		private static void ApplyValueToConfigurationParameterValue(ConfigModels.ConfigurationParameterValue value, JToken token)
		{
			switch (value.Type)
			{
				case SlcConfigurationsIds.Enums.Type.Number:
					value.NumberOptions = new ConfigModels.NumberParameterOptions
					{
						DefaultValue = token?.Value<double?>(),
						StepSize = 1,
						Decimals = 0,
					};
					break;
				case SlcConfigurationsIds.Enums.Type.Discrete:
					string discrete = token?.ToString();
					value.DiscreteOptions = new ConfigModels.DiscreteParameterOptions
					{
						DiscreteValues = String.IsNullOrWhiteSpace(discrete)
							? new List<ConfigModels.DiscreteValue>()
							: new List<ConfigModels.DiscreteValue> { new ConfigModels.DiscreteValue { Value = discrete } },
						Default = String.IsNullOrWhiteSpace(discrete) ? null : new ConfigModels.DiscreteValue { Value = discrete },
					};
					break;
				default:
					value.TextOptions = new ConfigModels.TextParameterOptions
					{
						Default = token?.ToString(),
					};
					break;
			}
		}

		private static void ApplyState(string sourceState, ServiceModels.Service service, DataHelperService serviceRepo, IEngine engine)
		{
			if (service == null || String.IsNullOrWhiteSpace(sourceState))
			{
				return;
			}

			string normalizedTarget = NormalizeStateName(sourceState);
			string current = NormalizeStateName(service.Status.ToString());
			if (current == normalizedTarget)
			{
				return;
			}

			string mappedTarget = MapInputStateToStatusName(sourceState);
			if (String.IsNullOrWhiteSpace(mappedTarget))
			{
				return;
			}

			var transitions = Enum.GetValues(typeof(TransitionsEnum))
				.Cast<TransitionsEnum>()
				.Select(t => new
				{
					Transition = t,
					Parts = t.ToString().Split(new[] { "_To_" }, StringSplitOptions.None),
				})
				.Where(x => x.Parts.Length == 2)
				.ToList();

			var queue = new Queue<string>();
			var visited = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
			var previous = new Dictionary<string, (string State, TransitionsEnum Transition)>(StringComparer.InvariantCultureIgnoreCase);

			queue.Enqueue(service.Status.ToString());
			visited.Add(service.Status.ToString());

			string finalState = null;
			while (queue.Count > 0 && finalState == null)
			{
				string state = queue.Dequeue();
				if (String.Equals(state, mappedTarget, StringComparison.InvariantCultureIgnoreCase))
				{
					finalState = state;
					break;
				}

				foreach (var candidate in transitions.Where(t => String.Equals(t.Parts[0], state, StringComparison.InvariantCultureIgnoreCase)))
				{
					string next = candidate.Parts[1];
					if (!visited.Add(next))
					{
						continue;
					}

					previous[next] = (state, candidate.Transition);
					queue.Enqueue(next);
				}
			}

			if (String.IsNullOrWhiteSpace(finalState))
			{
				return;
			}

			var sequence = new List<TransitionsEnum>();
			string cursor = finalState;
			while (previous.TryGetValue(cursor, out var link))
			{
				sequence.Add(link.Transition);
				cursor = link.State;
			}

			sequence.Reverse();
			foreach (var transition in sequence)
			{
				engine.GenerateInformation($"[Import Service Inventory] Status transition: {service.Name} -> {transition}");
				service = serviceRepo.UpdateState(service, transition);
			}
		}

		private static string MapInputStateToStatusName(string sourceState)
		{
			string normalized = NormalizeStateName(sourceState);
			switch (normalized)
			{
				case "feasibilitychecked":
					return "New";
				case "designed":
					return "Designed";
				case "reserved":
				case "inprogress":
					return "Reserved";
				case "active":
				case "completed":
					return "Active";
				case "inactive":
					return "Inactive";
				case "terminated":
				case "cancelled":
				case "rejected":
					return "Terminated";
				default:
					return null;
			}
		}

		private static string NormalizeStateName(string value)
		{
			if (String.IsNullOrWhiteSpace(value))
			{
				return String.Empty;
			}

			var chars = value.Where(Char.IsLetterOrDigit).ToArray();
			return new string(chars).ToLowerInvariant();
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

		private sealed class ServiceInventoryRoot
		{
			[JsonProperty("services")]
			public List<ServiceImport> Services { get; set; }
		}

		private sealed class ServiceImport
		{
			[JsonProperty("id")]
			public string Id { get; set; }

			[JsonProperty("systemServiceId")]
			public string SystemServiceId { get; set; }

			[JsonProperty("name")]
			public string Name { get; set; }

			[JsonProperty("state")]
			public string State { get; set; }

			[JsonProperty("serviceSpecificationId")]
			public string ServiceSpecificationId { get; set; }

			[JsonProperty("basedOnServiceOrderId")]
			public string BasedOnServiceOrderId { get; set; }

			[JsonProperty("startDate")]
			public DateTime? StartDate { get; set; }

			[JsonProperty("endDate")]
			public DateTime? EndDate { get; set; }

			[JsonProperty("categoryId")]
			public string CategoryId { get; set; }

			[JsonProperty("characteristics")]
			public List<CharacteristicImport> Characteristics { get; set; }

			[JsonProperty("profiles")]
			public List<ServiceProfileSelectionImport> Profiles { get; set; }

			[JsonProperty("serviceItems")]
			public List<ServiceItemImport> ServiceItems { get; set; }

			[JsonProperty("topology")]
			public List<TopologyLinkImport> Topology { get; set; }
		}

		private sealed class CharacteristicImport
		{
			[JsonProperty("parameterId")]
			public string ParameterId { get; set; }

			[JsonProperty("value")]
			public JToken Value { get; set; }
		}

		private sealed class ServiceProfileSelectionImport
		{
			[JsonProperty("profileDefinitionId")]
			public string ProfileDefinitionId { get; set; }

			[JsonProperty("mode")]
			public string Mode { get; set; }

			[JsonProperty("reusableProfileId")]
			public string ReusableProfileId { get; set; }

			[JsonProperty("instanceValues")]
			public Dictionary<string, JToken> InstanceValues { get; set; }
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

			[JsonProperty("label")]
			public string Label { get; set; }

			[JsonProperty("workflowTemplateId")]
			public string WorkflowTemplateId { get; set; }

			[JsonProperty("linkedServiceId")]
			public string LinkedServiceId { get; set; }

			[JsonProperty("jobId")]
			public string JobId { get; set; }
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

		private sealed class CategoriesRoot
		{
			[JsonProperty("categories")]
			public List<CategoryImport> Categories { get; set; }
		}

		private sealed class CategoryImport
		{
			[JsonProperty("id")]
			public string Id { get; set; }

			[JsonProperty("categoryType")]
			public string CategoryType { get; set; }

			[JsonProperty("categoryName")]
			public string CategoryName { get; set; }
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

			[JsonProperty("type")]
			public string Type { get; set; }
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
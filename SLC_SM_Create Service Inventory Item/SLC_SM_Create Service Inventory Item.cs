/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE        VERSION     AUTHOR          COMMENTS

dd/mm/2025  1.0.0.1     XXX, Skyline    Initial version
13/07/2026	1.0.0.2		SKA, Skyline	Implemented logic to support duplicating a service
31/07/2026	1.0.0.3		SKA, Skyline	Improved performance of service creation using optimized methods for reading the data and inverted the logic for linking a service
04/08/2026	1.0.0.4		SKA, Skyline	Added support for defining a ServiceId when creating a new service
****************************************************************************
*/

namespace SLC_SM_Create_Service_Inventory_Item
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading;
	using DomHelpers.SlcServicemanagement;
	using DomHelpers.SlcWorkflow;
	using Library;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Core.DataMinerSystem.Automation;
	using Skyline.DataMiner.Core.DataMinerSystem.Common;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using Skyline.DataMiner.SDM;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.IAS;
	using SLC_SM_Common.Extensions;
	using SLC_SM_Create_Service_Inventory_Item.Presenters;
	using SLC_SM_Create_Service_Inventory_Item.Views;
	using ConfigModels = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;
	using SdmModels = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	///     Represents a DataMiner Automation script.
	/// </summary>
	public class Script
	{
		private InteractiveController _controller;
		private IEngine _engine;

		/// <summary>
		///     The script entry point.
		/// </summary>
		/// <param name="engine">Link with SLAutomation process.</param>
		public void Run(IEngine engine)
		{
			/*
            * Note:
            * Do not remove the commented methods below!
            * The lines are needed to execute an interactive automation script from the non-interactive automation script or from Visio!
            *
            * engine.ShowUI();
            */
			if (engine.IsInteractive)
			{
				engine.FindInteractiveClient("Failed to run script in interactive mode", 1);
			}

			try
			{
				_engine = engine;
				_controller = new InteractiveController(engine) { ScriptAbortPopupBehavior = ScriptAbortPopupBehavior.HideAlways };

				RunSafe();
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
				engine.ShowErrorDialog(e);
				engine.Log(e.ToString());
			}
		}

		private static SdmModels.Service GetService(IServiceManagementApiHelper sdmHelper, Guid domId)
		{
			if (domId == Guid.Empty)
			{
				throw new InvalidOperationException("No existing DOM ID was provided as script input!");
			}

			return sdmHelper.ServiceInventory.Services.Read(SdmModels.ServiceExposers.Identifier.Equal(domId.ToString())).FirstOrDefault()
				?? throw new InvalidOperationException($"No Dom Instance with ID '{domId}' found on the system!");
		}

		private static bool ServiceIdExists(IServiceManagementApiHelper sdmHelper, string serviceId)
		{
			return sdmHelper.ServiceInventory.Services.Read(SdmModels.ServiceExposers.ServiceID.Equal(serviceId)).Any();
		}

		private static HashSet<string> GetSourceConfigurationVersionIds(SdmModels.Service sourceService)
		{
			if (sourceService.ConfigurationVersions == null || sourceService.ConfigurationVersions.Count == 0)
			{
				return new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
			}

			return sourceService.ConfigurationVersions
				.Where(r => r != null && !String.IsNullOrWhiteSpace(r.Identifier))
				.Select(r => r.Identifier)
				.ToHashSet(StringComparer.InvariantCultureIgnoreCase);
		}

		private static void RemapDuplicatedLinkedConsumers(
			IServiceManagementApiHelper sdmHelper,
			List<ConfigModels.ConfigurationParameterValue> duplicatedConfigParameterValues,
			Dictionary<string, string> configParamValueIdMap)
		{
			if (duplicatedConfigParameterValues == null || duplicatedConfigParameterValues.Count == 0)
			{
				return;
			}

			var guidMap = BuildGuidMap(configParamValueIdMap);
			if (guidMap.Count == 0)
			{
				return;
			}

			foreach (var duplicated in duplicatedConfigParameterValues)
			{
				if (!HasLinkedConsumers(duplicated))
				{
					continue;
				}

				if (RemapLinkedConsumers(duplicated.LinkedConsumers, guidMap))
				{
					sdmHelper.ServiceCatalog.ConfigurationParameterValues.Update(duplicated);
				}
			}
		}

		private void AddOrUpdateServiceViaSdm(IServiceManagementApiHelper sdmHelper, SdmModels.Service instance)
		{
			if (instance.ServiceSpecificationId != null && !String.IsNullOrWhiteSpace(instance.ServiceSpecificationId.Identifier))
			{
				var spec = sdmHelper.ServiceCatalog.ServiceSpecifications
					.Read(SdmModels.ServiceSpecificationExposers.Identifier.Equal(instance.ServiceSpecificationId.Identifier))
					.FirstOrDefault();

				if (spec != null)
				{
					instance.Description = spec.Description;
					instance.ServiceItems = spec.ServiceItems != null
						? spec.ServiceItems.ToList()
						: new List<SdmModels.ServiceItem>();
					instance.ServiceItemsRelationships = spec.ServiceItemsRelationships != null
						? spec.ServiceItemsRelationships.ToList()
						: new List<SdmModels.ServiceItemRelationship>();
					instance.ServiceItems = SanitizeServiceItemsForCreate(instance.ServiceItems);
				}
			}

			instance.ServiceItems = SanitizeServiceItemsForCreate(instance.ServiceItems);

			sdmHelper.ServiceInventory.Services.Create(instance);

			if ((bool)instance.GenerateMonitoringService)
			{
				TryCreateDmsService(instance.Name, instance.Icon);
			}
		}

		private void DuplicateServiceViaSdm(IServiceManagementApiHelper sdmHelper, SdmModels.Service source, SdmModels.Service instance)
		{
			var configurationVersionMap = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
			var duplicatedConfigurationVersions = DuplicateConfigurationVersions(sdmHelper, source, instance.ServiceID, configurationVersionMap);
			instance.ConfigurationVersions = duplicatedConfigurationVersions
				.Select(id => new SdmObjectReference<SdmModels.ServiceConfigurationVersion>(id))
				.ToList();

			if (source.ServiceConfigurationId != null
				&& !String.IsNullOrWhiteSpace(source.ServiceConfigurationId.Identifier)
				&& configurationVersionMap.TryGetValue(source.ServiceConfigurationId.Identifier, out var duplicatedActiveVersionId))
			{
				instance.ServiceConfigurationId = new SdmObjectReference<SdmModels.ServiceConfigurationVersion>(duplicatedActiveVersionId);
			}
			else
			{
				instance.ServiceConfigurationId = null;
			}

			if (instance.ServiceSpecificationId != null && !String.IsNullOrWhiteSpace(instance.ServiceSpecificationId.Identifier))
			{
				var spec = sdmHelper.ServiceCatalog.ServiceSpecifications
					.Read(SdmModels.ServiceSpecificationExposers.Identifier.Equal(instance.ServiceSpecificationId.Identifier))
					.FirstOrDefault();

				if (spec != null)
				{
					instance.ServiceItems = spec.ServiceItems != null
						? spec.ServiceItems.ToList()
						: new List<SdmModels.ServiceItem>();
					instance.ServiceItemsRelationships = spec.ServiceItemsRelationships != null
						? spec.ServiceItemsRelationships.ToList()
						: new List<SdmModels.ServiceItemRelationship>();
				}
			}

			instance.ServiceItems = SanitizeServiceItemsForCreate(instance.ServiceItems);
			sdmHelper.ServiceInventory.Services.Create(instance);

			if ((bool)instance.GenerateMonitoringService)
			{
				TryCreateDmsService(instance.Name, instance.Icon);
			}
		}

		private List<string> DuplicateConfigurationVersions(
			IServiceManagementApiHelper sdmHelper,
			SdmModels.Service sourceService,
			string newServiceId,
			Dictionary<string, string> configurationVersionMap)
		{
			var sourceConfigVersionIds = GetSourceConfigurationVersionIds(sourceService);
			if (sourceConfigVersionIds.Count == 0)
			{
				return new List<string>();
			}

			var duplicationData = LoadConfigurationDuplicationData(sdmHelper, sourceConfigVersionIds);
			var duplicatedVersionIds = new List<string>();

			foreach (var sourceVersion in duplicationData.ServiceConfigurationVersions.OrderBy(v => v.VersionName))
			{
				if (sourceVersion == null || String.IsNullOrWhiteSpace(sourceVersion.Identifier))
				{
					continue;
				}

				var state = new VersionDuplicationState();

				var duplicatedParameterRefs = DuplicateVersionParameters(
					sdmHelper,
					sourceVersion,
					duplicationData.ServiceConfigurationValuesById,
					duplicationData.ConfigParameterValuesById,
					state.ConfigParamValueIdMap,
					state.DuplicatedConfigParameterValues);

				var duplicatedProfileRefs = DuplicateVersionProfiles(
					sdmHelper,
					sourceVersion,
					newServiceId,
					duplicationData.ServiceProfilesById,
					duplicationData.ProfilesById,
					duplicationData.ConfigParameterValuesById,
					state.ServiceProfileIdMap,
					state.ProfileIdMap,
					state.ConfigParamValueIdMap,
					state.DuplicatedConfigParameterValues);

				RemapDuplicatedLinkedConsumers(sdmHelper, state.DuplicatedConfigParameterValues, state.ConfigParamValueIdMap);

				var duplicatedVersionId = CreateDuplicatedConfigurationVersion(sdmHelper, sourceVersion, duplicatedParameterRefs, duplicatedProfileRefs);
				configurationVersionMap[sourceVersion.Identifier] = duplicatedVersionId;
				duplicatedVersionIds.Add(duplicatedVersionId);
			}

			return duplicatedVersionIds;
		}

		private ConfigurationDuplicationData LoadConfigurationDuplicationData(
			IServiceManagementApiHelper sdmHelper,
			HashSet<string> sourceConfigVersionIds)
		{
			return new ConfigurationDuplicationData
			{
				ServiceConfigurationVersions = sdmHelper.ServiceInventory.ServiceConfigurationVersions
					.Read(new TRUEFilterElement<SdmModels.ServiceConfigurationVersion>())
					.Where(v => v != null && !String.IsNullOrWhiteSpace(v.Identifier) && sourceConfigVersionIds.Contains(v.Identifier))
					.ToList(),
				ServiceConfigurationValuesById = sdmHelper.ServiceInventory.ServiceConfigurationValues
					.Read(new TRUEFilterElement<SdmModels.ServiceConfigurationValue>())
					.Where(v => !String.IsNullOrWhiteSpace(v?.Identifier))
					.ToDictionary(v => v.Identifier, StringComparer.InvariantCultureIgnoreCase),
				ServiceProfilesById = sdmHelper.ServiceInventory.ServiceProfiles
					.Read(new TRUEFilterElement<SdmModels.ServiceProfile>())
					.Where(p => !String.IsNullOrWhiteSpace(p?.Identifier))
					.ToDictionary(p => p.Identifier, StringComparer.InvariantCultureIgnoreCase),
				ProfilesById = sdmHelper.ServiceCatalog.Profiles
					.Read(new TRUEFilterElement<ConfigModels.Profile>())
					.Where(p => !String.IsNullOrWhiteSpace(p?.Identifier))
					.ToDictionary(p => p.Identifier, StringComparer.InvariantCultureIgnoreCase),
				ConfigParameterValuesById = sdmHelper.ServiceCatalog.ConfigurationParameterValues
					.Read(new TRUEFilterElement<ConfigModels.ConfigurationParameterValue>())
					.Where(v => !String.IsNullOrWhiteSpace(v?.Identifier))
					.ToDictionary(v => v.Identifier, StringComparer.InvariantCultureIgnoreCase),
			};
		}

		private List<SdmObjectReference<SdmModels.ServiceConfigurationValue>> DuplicateVersionParameters(
			IServiceManagementApiHelper sdmHelper,
			SdmModels.ServiceConfigurationVersion sourceVersion,
			Dictionary<string, SdmModels.ServiceConfigurationValue> serviceConfigurationValuesById,
			Dictionary<string, ConfigModels.ConfigurationParameterValue> configParameterValuesById,
			Dictionary<string, string> configParamValueIdMap,
			List<ConfigModels.ConfigurationParameterValue> duplicatedConfigParameterValues)
		{
			var duplicatedParameterRefs = new List<SdmObjectReference<SdmModels.ServiceConfigurationValue>>();

			foreach (var sourceParameterRef in sourceVersion.Parameters ?? new List<SdmObjectReference<SdmModels.ServiceConfigurationValue>>())
			{
				if (sourceParameterRef == null || String.IsNullOrWhiteSpace(sourceParameterRef.Identifier))
				{
					continue;
				}

				if (!serviceConfigurationValuesById.TryGetValue(sourceParameterRef.Identifier, out var sourceParameter))
				{
					continue;
				}

				var duplicatedConfigParameterValueId = DuplicateConfigurationParameterValue(
					sdmHelper,
					sourceParameter.ConfigurationParameterId.Identifier,
					configParameterValuesById,
					configParamValueIdMap,
					duplicatedConfigParameterValues);

				if (String.IsNullOrWhiteSpace(duplicatedConfigParameterValueId))
				{
					continue;
				}

				var duplicatedParameterId = Guid.NewGuid().ToString();
				var duplicatedParameter = new SdmModels.ServiceConfigurationValue
				{
					Identifier = duplicatedParameterId,
					Mandatory = sourceParameter.Mandatory,
					ConfigurationParameterId = new SdmObjectReference<ConfigModels.ConfigurationParameter>(duplicatedConfigParameterValueId),
				};

				sdmHelper.ServiceInventory.ServiceConfigurationValues.Create(duplicatedParameter);
				duplicatedParameterRefs.Add(new SdmObjectReference<SdmModels.ServiceConfigurationValue>(duplicatedParameterId));
			}

			return duplicatedParameterRefs;
		}

		private List<SdmObjectReference<SdmModels.ServiceProfile>> DuplicateVersionProfiles(
			IServiceManagementApiHelper sdmHelper,
			SdmModels.ServiceConfigurationVersion sourceVersion,
			string newServiceId,
			Dictionary<string, SdmModels.ServiceProfile> serviceProfilesById,
			Dictionary<string, ConfigModels.Profile> profilesById,
			Dictionary<string, ConfigModels.ConfigurationParameterValue> configParameterValuesById,
			Dictionary<string, string> serviceProfileIdMap,
			Dictionary<string, string> profileIdMap,
			Dictionary<string, string> configParamValueIdMap,
			List<ConfigModels.ConfigurationParameterValue> duplicatedConfigParameterValues)
		{
			var duplicatedProfileRefs = new List<SdmObjectReference<SdmModels.ServiceProfile>>();

			foreach (var sourceServiceProfileRef in sourceVersion.Profiles ?? new List<SdmObjectReference<SdmModels.ServiceProfile>>())
			{
				if (sourceServiceProfileRef == null || String.IsNullOrWhiteSpace(sourceServiceProfileRef.Identifier))
				{
					continue;
				}

				var duplicatedServiceProfileId = DuplicateServiceProfile(
					sdmHelper,
					sourceServiceProfileRef.Identifier,
					newServiceId,
					serviceProfilesById,
					profilesById,
					configParameterValuesById,
					serviceProfileIdMap,
					profileIdMap,
					configParamValueIdMap,
					duplicatedConfigParameterValues);

				if (!String.IsNullOrWhiteSpace(duplicatedServiceProfileId))
				{
					duplicatedProfileRefs.Add(new SdmObjectReference<SdmModels.ServiceProfile>(duplicatedServiceProfileId));
				}
			}

			return duplicatedProfileRefs;
		}

		private string CreateDuplicatedConfigurationVersion(
			IServiceManagementApiHelper sdmHelper,
			SdmModels.ServiceConfigurationVersion sourceVersion,
			List<SdmObjectReference<SdmModels.ServiceConfigurationValue>> duplicatedParameterRefs,
			List<SdmObjectReference<SdmModels.ServiceProfile>> duplicatedProfileRefs)
		{
			var duplicatedVersionId = Guid.NewGuid().ToString();
			var duplicatedVersion = new SdmModels.ServiceConfigurationVersion
			{
				Identifier = duplicatedVersionId,
				VersionName = $"{sourceVersion.VersionName} (Copy)",
				Description = sourceVersion.Description,
				StartDate = sourceVersion.StartDate,
				EndDate = sourceVersion.EndDate,
				Parameters = duplicatedParameterRefs,
				Profiles = duplicatedProfileRefs,
			};

			sdmHelper.ServiceInventory.ServiceConfigurationVersions.Create(duplicatedVersion);
			return duplicatedVersionId;
		}

		private string DuplicateServiceProfile(
			IServiceManagementApiHelper sdmHelper,
			string sourceServiceProfileId,
			string newServiceId,
			Dictionary<string, SdmModels.ServiceProfile> serviceProfilesById,
			Dictionary<string, ConfigModels.Profile> profilesById,
			Dictionary<string, ConfigModels.ConfigurationParameterValue> configParameterValuesById,
			Dictionary<string, string> serviceProfileIdMap,
			Dictionary<string, string> profileIdMap,
			Dictionary<string, string> configParamValueIdMap,
			List<ConfigModels.ConfigurationParameterValue> duplicatedConfigParameterValues)
		{
			if (serviceProfileIdMap.TryGetValue(sourceServiceProfileId, out var alreadyDuplicatedServiceProfileId))
			{
				return alreadyDuplicatedServiceProfileId;
			}

			if (!serviceProfilesById.TryGetValue(sourceServiceProfileId, out var sourceServiceProfile))
			{
				return null;
			}

			string duplicatedProfileId = null;
			if (sourceServiceProfile.ProfileId != null && !String.IsNullOrWhiteSpace(sourceServiceProfile.ProfileId.Identifier))
			{
				duplicatedProfileId = DuplicateProfile(
					sdmHelper,
					sourceServiceProfile.ProfileId.Identifier,
					newServiceId,
					profilesById,
					configParameterValuesById,
					profileIdMap,
					configParamValueIdMap,
					duplicatedConfigParameterValues);
			}

			var duplicatedServiceProfileId = Guid.NewGuid().ToString();
			var duplicatedServiceProfile = new SdmModels.ServiceProfile
			{
				Identifier = duplicatedServiceProfileId,
				Mandatory = sourceServiceProfile.Mandatory,
				ProfileDefinitionId = sourceServiceProfile.ProfileDefinitionId != null
					? new SdmObjectReference<ConfigModels.ProfileDefinition>(sourceServiceProfile.ProfileDefinitionId.Identifier)
					: null,
				ProfileId = !String.IsNullOrWhiteSpace(duplicatedProfileId)
					? new SdmObjectReference<ConfigModels.Profile>(duplicatedProfileId)
					: null,
			};

			sdmHelper.ServiceInventory.ServiceProfiles.Create(duplicatedServiceProfile);
			serviceProfileIdMap[sourceServiceProfileId] = duplicatedServiceProfileId;
			return duplicatedServiceProfileId;
		}

		private string DuplicateProfile(
			IServiceManagementApiHelper sdmHelper,
			string sourceProfileId,
			string newServiceId,
			Dictionary<string, ConfigModels.Profile> profilesById,
			Dictionary<string, ConfigModels.ConfigurationParameterValue> configParameterValuesById,
			Dictionary<string, string> profileIdMap,
			Dictionary<string, string> configParamValueIdMap,
			List<ConfigModels.ConfigurationParameterValue> duplicatedConfigParameterValues)
		{
			if (profileIdMap.TryGetValue(sourceProfileId, out var alreadyDuplicatedProfileId))
			{
				return alreadyDuplicatedProfileId;
			}

			if (!profilesById.TryGetValue(sourceProfileId, out var sourceProfile) || sourceProfile == null)
			{
				return null;
			}

			var duplicatedChildProfiles = DuplicateChildProfiles(
				sdmHelper,
				sourceProfile,
				newServiceId,
				profilesById,
				configParameterValuesById,
				profileIdMap,
				configParamValueIdMap,
				duplicatedConfigParameterValues);

			var duplicatedConfigurationParameterValues = DuplicateProfileConfigurationParameterValues(
				sdmHelper,
				sourceProfile,
				configParameterValuesById,
				configParamValueIdMap,
				duplicatedConfigParameterValues);

			var duplicatedProfileId = CreateDuplicatedProfile(
				sdmHelper,
				sourceProfile,
				newServiceId,
				duplicatedChildProfiles,
				duplicatedConfigurationParameterValues);

			profileIdMap[sourceProfileId] = duplicatedProfileId;
			return duplicatedProfileId;
		}

		private List<SdmObjectReference<ConfigModels.Profile>> DuplicateChildProfiles(
			IServiceManagementApiHelper sdmHelper,
			ConfigModels.Profile sourceProfile,
			string newServiceId,
			Dictionary<string, ConfigModels.Profile> profilesById,
			Dictionary<string, ConfigModels.ConfigurationParameterValue> configParameterValuesById,
			Dictionary<string, string> profileIdMap,
			Dictionary<string, string> configParamValueIdMap,
			List<ConfigModels.ConfigurationParameterValue> duplicatedConfigParameterValues)
		{
			var duplicatedChildProfiles = new List<SdmObjectReference<ConfigModels.Profile>>();

			foreach (var sourceChildRef in sourceProfile.Profiles ?? new List<SdmObjectReference<ConfigModels.Profile>>())
			{
				if (sourceChildRef == null || String.IsNullOrWhiteSpace(sourceChildRef.Identifier))
				{
					continue;
				}

				var duplicatedChildProfileId = DuplicateProfile(
					sdmHelper,
					sourceChildRef.Identifier,
					newServiceId,
					profilesById,
					configParameterValuesById,
					profileIdMap,
					configParamValueIdMap,
					duplicatedConfigParameterValues);

				if (!String.IsNullOrWhiteSpace(duplicatedChildProfileId))
				{
					duplicatedChildProfiles.Add(new SdmObjectReference<ConfigModels.Profile>(duplicatedChildProfileId));
				}
			}

			return duplicatedChildProfiles;
		}

		private List<SdmObjectReference<ConfigModels.ConfigurationParameterValue>> DuplicateProfileConfigurationParameterValues(
			IServiceManagementApiHelper sdmHelper,
			ConfigModels.Profile sourceProfile,
			Dictionary<string, ConfigModels.ConfigurationParameterValue> configParameterValuesById,
			Dictionary<string, string> configParamValueIdMap,
			List<ConfigModels.ConfigurationParameterValue> duplicatedConfigParameterValues)
		{
			var duplicatedConfigurationParameterValues = new List<SdmObjectReference<ConfigModels.ConfigurationParameterValue>>();

			foreach (var sourceConfigParamValueRef in sourceProfile.ConfigurationParameterValues ?? new List<SdmObjectReference<ConfigModels.ConfigurationParameterValue>>())
			{
				if (sourceConfigParamValueRef == null || String.IsNullOrWhiteSpace(sourceConfigParamValueRef.Identifier))
				{
					continue;
				}

				var duplicatedConfigParameterValueId = DuplicateConfigurationParameterValue(
					sdmHelper,
					sourceConfigParamValueRef.Identifier,
					configParameterValuesById,
					configParamValueIdMap,
					duplicatedConfigParameterValues);

				if (!String.IsNullOrWhiteSpace(duplicatedConfigParameterValueId))
				{
					duplicatedConfigurationParameterValues.Add(new SdmObjectReference<ConfigModels.ConfigurationParameterValue>(duplicatedConfigParameterValueId));
				}
			}

			return duplicatedConfigurationParameterValues;
		}

		private string CreateDuplicatedProfile(
			IServiceManagementApiHelper sdmHelper,
			ConfigModels.Profile sourceProfile,
			string newServiceId,
			List<SdmObjectReference<ConfigModels.Profile>> duplicatedChildProfiles,
			List<SdmObjectReference<ConfigModels.ConfigurationParameterValue>> duplicatedConfigurationParameterValues)
		{
			var duplicatedProfileId = Guid.NewGuid().ToString();
			var duplicatedProfile = new ConfigModels.Profile
			{
				Identifier = duplicatedProfileId,
				Name = (sourceProfile.Name ?? String.Empty).ReplaceTrailingParentesisContent(newServiceId),
				IsReusable = sourceProfile.IsReusable,
				ProfileDefinitionId = sourceProfile.ProfileDefinitionId != null
					? new SdmObjectReference<ConfigModels.ProfileDefinition>(sourceProfile.ProfileDefinitionId.Identifier)
					: null,
				Profiles = duplicatedChildProfiles,
				ConfigurationParameterValues = duplicatedConfigurationParameterValues,
				TestedProtocols = sourceProfile.TestedProtocols?.Select(tp => new SdmObjectReference<ConfigModels.ProtocolTest>(tp.Identifier)).ToList()
					?? new List<SdmObjectReference<ConfigModels.ProtocolTest>>(),
			};

			sdmHelper.ServiceCatalog.Profiles.Create(duplicatedProfile);
			return duplicatedProfileId;
		}

		private string DuplicateConfigurationParameterValue(
			IServiceManagementApiHelper sdmHelper,
			string sourceConfigParamValueId,
			Dictionary<string, ConfigModels.ConfigurationParameterValue> configParameterValuesById,
			Dictionary<string, string> configParamValueIdMap,
			List<ConfigModels.ConfigurationParameterValue> duplicatedConfigParameterValues)
		{
			if (String.IsNullOrWhiteSpace(sourceConfigParamValueId))
			{
				return null;
			}

			if (configParamValueIdMap.TryGetValue(sourceConfigParamValueId, out var alreadyDuplicatedConfigParamValueId))
			{
				return alreadyDuplicatedConfigParamValueId;
			}

			if (!configParameterValuesById.TryGetValue(sourceConfigParamValueId, out var source))
			{
				return null;
			}

			var duplicatedId = Guid.NewGuid().ToString();
			var duplicated = new ConfigModels.ConfigurationParameterValue
			{
				Identifier = duplicatedId,
				Label = source.Label,
				Type = source.Type,
				DoubleValue = source.DoubleValue,
				StringValue = source.StringValue,
				ConfigurationParameterId = source.ConfigurationParameterId != null
					? new SdmObjectReference<ConfigModels.ConfigurationParameter>(source.ConfigurationParameterId.Identifier)
					: null,
				NumberOptionsId = source.NumberOptionsId != null
					? new SdmObjectReference<ConfigModels.NumberParameterOptions>(source.NumberOptionsId.Identifier)
					: null,
				DiscreteOptionsId = source.DiscreteOptionsId != null
					? new SdmObjectReference<ConfigModels.DiscreteParameterOptions>(source.DiscreteOptionsId.Identifier)
					: null,
				TextOptionsId = source.TextOptionsId != null
					? new SdmObjectReference<ConfigModels.TextParameterOptions>(source.TextOptionsId.Identifier)
					: null,
				LinkedConfigurationReference = source.LinkedConfigurationReference,
				ValueFixed = source.ValueFixed,
				LinkedConsumers = source.LinkedConsumers != null ? new List<Guid>(source.LinkedConsumers) : new List<Guid>(),
				LinkedScript = source.LinkedScript,
				IsLinked = source.IsLinked,
			};

			sdmHelper.ServiceCatalog.ConfigurationParameterValues.Create(duplicated);
			configParamValueIdMap[sourceConfigParamValueId] = duplicatedId;
			duplicatedConfigParameterValues.Add(duplicated);
			return duplicatedId;
		}

		private static Dictionary<Guid, Guid> BuildGuidMap(Dictionary<string, string> configParamValueIdMap)
		{
			var guidMap = new Dictionary<Guid, Guid>();
			if (configParamValueIdMap == null || configParamValueIdMap.Count == 0)
			{
				return guidMap;
			}

			foreach (var mapping in configParamValueIdMap)
			{
				if (Guid.TryParse(mapping.Key, out var oldId) && Guid.TryParse(mapping.Value, out var newId))
				{
					guidMap[oldId] = newId;
				}
			}

			return guidMap;
		}

		private static bool HasLinkedConsumers(ConfigModels.ConfigurationParameterValue duplicated)
		{
			return duplicated != null
				&& duplicated.LinkedConsumers != null
				&& duplicated.LinkedConsumers.Count > 0;
		}

		private static bool RemapLinkedConsumers(List<Guid> linkedConsumers, Dictionary<Guid, Guid> guidMap)
		{
			var changed = false;

			for (int i = 0; i < linkedConsumers.Count; i++)
			{
				if (guidMap.TryGetValue(linkedConsumers[i], out var newConsumerId))
				{
					linkedConsumers[i] = newConsumerId;
					changed = true;
				}
			}

			return changed;
		}

		private static void UpdateServiceInfoViaSdm(IServiceManagementApiHelper sdmHelper, Guid domId, SdmModels.Service source)
		{
			var service = sdmHelper.ServiceInventory.Services.Read(SdmModels.ServiceExposers.Identifier.Equal(domId.ToString())).FirstOrDefault()
				?? throw new InvalidOperationException($"No Dom Instance with ID '{domId}' found on the system!");

			service.Name = source.Name;
			service.ServiceID = source.ServiceID;
			service.Description = source.Description ?? String.Empty;
			service.StartTime = source.StartTime;
			service.EndTime = source.EndTime;
			service.GenerateMonitoringService = source.GenerateMonitoringService;
			service.MonitoringService = source.MonitoringService;
			service.Icon = source.Icon;
			service.CategoryId = source.CategoryId;
			service.ServiceSpecificationId = source.ServiceSpecificationId;

			sdmHelper.ServiceInventory.Services.Update(service);
		}

		private List<SdmModels.ServiceItem> SanitizeServiceItemsForCreate(List<SdmModels.ServiceItem> items)
		{
			if (items == null)
			{
				return null;
			}

			var sanitized = new List<SdmModels.ServiceItem>(items.Count);

			for (int i = 0; i < items.Count; i++)
			{
				var item = items[i];
				if (item == null)
				{
					continue;
				}

				sanitized.Add(new SdmModels.ServiceItem
				{
					ServiceItemID = item.ServiceItemID,
					Label = item.Label,
					Script = item.Script,
					DefinitionReference = item.DefinitionReference,
					ImplementationReference = item.ImplementationReference,

					// Workaround: avoid setting GenericEnum field explicitly.
					// The "Service Item Type" field is optional and setting it can fail with
					// DomInstanceSectionInvalidFieldValueTypes on some systems.
					Type = null,
					Icon = item.Icon,
				});
			}

			return sanitized;
		}

		private void TryCreateDmsService(string serviceName, string serviceIcon)
		{
			var dms = _engine.GetDms();

			if (dms.ServiceExistsSafe(serviceName, out IDmsService _))
			{
				throw new InvalidOperationException($"A DataMiner service with name {serviceName} already exists.");
			}

			var serviceConfiguration = new ServiceConfiguration(dms, serviceName);
			var serviceId = dms.GetAgents().First().CreateService(serviceConfiguration);

			SetServiceIcon(dms, serviceId, serviceIcon);
		}

		private IEnumerable<IDmsService> GetDmsServices()
		{
			var dms = _engine.GetDms();
			var services = dms.GetServices();
			if (!services.Any())
			{
				return Enumerable.Empty<IDmsService>();
			}

			return services;
		}

		private void SetServiceIcon(IDms dms, DmsServiceId serviceId, string icon)
		{
			if (!dms.PropertyExists("Logo", PropertyType.Service))
			{
				dms.CreateProperty("Logo", PropertyType.Service, false, false, false);
			}

			WaitUntilServiceCreated(serviceId, 5000);
			var service = dms.GetService(serviceId);

			var property = service.Properties.SingleOrDefault(p => p.Definition.Name == "Logo").AsWritable();

			property.Value = icon;
			service.Update();
		}

		private void WaitUntilServiceCreated(DmsServiceId serviceId, int timeout)
		{
			var sw = System.Diagnostics.Stopwatch.StartNew();

			while (_engine.FindServiceByKey(serviceId.Value) == null)
			{
				if (sw.ElapsedMilliseconds > timeout)
				{
					throw new TimeoutException($"Service {serviceId} was not created within {timeout} ms.");
				}

				Thread.Sleep(250);
			}
		}

		private Guid CreateServiceItemFromOrderItem(IServiceManagementApiHelper sdmHelper, SdmModels.ServiceOrderItem serviceOrderItem)
		{
			var newServiceId = Guid.NewGuid();
			var serviceIds = sdmHelper.ServiceInventory.Services.Read(new TRUEFilterElement<SdmModels.Service>()).Select(x => x.ServiceID).Where(x => !String.IsNullOrWhiteSpace(x)).ToList();
			var maxValue = serviceIds
				.Select(x =>
				{
					var parts = x.Split('-');
					return parts.Length > 1 && Int32.TryParse(parts.Last(), out var number) ? number : 0;
				})
				.DefaultIfEmpty(0)
				.Max();

			var categoryReference = serviceOrderItem.ServiceInfo != null ? serviceOrderItem.ServiceInfo.ServiceCategoryId : null;
			var specReference = serviceOrderItem.ServiceInfo != null ? serviceOrderItem.ServiceInfo.SpecificationId : null;

			var spec = specReference != null
				? sdmHelper.ServiceCatalog.ServiceSpecifications.Read(SdmModels.ServiceSpecificationExposers.Identifier.Equal(specReference.Identifier)).FirstOrDefault()
				: null;

			var category = categoryReference != null
				? sdmHelper.ServiceCatalog.ServiceCategories.Read(SdmModels.ServiceCategoryExposers.Identifier.Equal(categoryReference.Identifier)).FirstOrDefault()
				: null;

			var newService = new SdmModels.Service
			{
				Identifier = newServiceId.ToString(),
				ServiceID = $"SERVICE-{maxValue + 1:00000}",
				Name = serviceOrderItem.Name,
				Description = serviceOrderItem.Name,
				StartTime = serviceOrderItem.StartTime,
				EndTime = serviceOrderItem.EndTime,
				ServiceSpecificationId = specReference,
				CategoryId = categoryReference,
				Icon = category?.Icon ?? String.Empty,
				ServiceItems = spec?.ServiceItems != null ? spec.ServiceItems.ToList() : new List<SdmModels.ServiceItem>(),
				ServiceItemsRelationships = spec?.ServiceItemsRelationships != null ? spec.ServiceItemsRelationships.ToList() : new List<SdmModels.ServiceItemRelationship>(),
			};

			sdmHelper.ServiceInventory.Services.Create(newService);
			return newServiceId;
		}

		private void CreateNewServiceAndLinkItToServiceOrder(IServiceManagementApiHelper sdmHelper, SdmModels.ServiceOrderItem serviceOrderItem)
		{
			if (serviceOrderItem.ServiceInfo != null && serviceOrderItem.ServiceInfo.ServiceId != null
				&& sdmHelper.ServiceInventory.Services.Read(SdmModels.ServiceExposers.Identifier.Equal(serviceOrderItem.ServiceInfo.ServiceId.Identifier)).Any())
			{
				return;
			}

			Guid newServiceId = _engine.PerformanceLogger("Create Service Inventory Item", () => CreateServiceItemFromOrderItem(sdmHelper, serviceOrderItem));

			if (serviceOrderItem.ServiceInfo == null)
			{
				serviceOrderItem.ServiceInfo = new SdmModels.ServiceOrderItemServiceInfo();
			}

			serviceOrderItem.ServiceInfo.ServiceId = new Skyline.DataMiner.SDM.SdmObjectReference<SdmModels.Service>(newServiceId.ToString());
			_engine.PerformanceLogger("Update Order", () => sdmHelper.ServiceOrder.ServiceOrderItems.Update(serviceOrderItem));
		}

		private void RunSafe()
		{
			string actionRaw = _engine.ReadScriptParamFromApp("Action");
			if (!Enum.TryParse(actionRaw, true, out Defaults.ScriptAction_CreateServiceInventoryItem action))
			{
				action = Defaults.ScriptAction_CreateServiceInventoryItem.AddItem;
			}

			string domIdRaw = _engine.ReadScriptParamFromApp("DOM ID");
			if (!Guid.TryParse(domIdRaw, out Guid domId) && action != Defaults.ScriptAction_CreateServiceInventoryItem.Add)
			{
				throw new InvalidOperationException($"Please select an entry in the Service Order Items table first.{Environment.NewLine}Details: the app passed the following, unexpected UUID to the action: {domIdRaw}.");
			}

			var dataMinerServices = GetDmsServices();

			var sdmHelper = new ServiceManagementApiHelper(_engine.GetUserConnection(), "Service Inventory");

			// Init views
			var view = new ServiceView(_engine, action);
			var presenter = new ServicePresenter(_engine, sdmHelper, view, dataMinerServices);

			if (action == Defaults.ScriptAction_CreateServiceInventoryItem.AddItem)
			{
				var d = new MessageDialog(_engine, "Create Service Inventory Item from the selected service order item?") { Title = "Create Service Inventory Item From Order Item" };
				d.OkButton.Pressed += (sender, args) =>
				{
					AddServiceItemForOrder(domId, sdmHelper);
				};
				_controller.ShowDialog(d);
			}
			else if (action == Defaults.ScriptAction_CreateServiceInventoryItem.AddItemSilent)
			{
				AddServiceItemForOrder(domId, sdmHelper);
			}
			else if (action == Defaults.ScriptAction_CreateServiceInventoryItem.Add)
			{
				presenter.LoadFromModel();
				view.BtnAdd.Pressed += (sender, args) =>
				{
					if (!presenter.Validate())
					{
						return;
					}

					if (ServiceIdExists(sdmHelper, presenter.ServiceId))
					{
						presenter.ShowServiceIdExistsError();
						return;
					}

					AddOrUpdateServiceViaSdm(sdmHelper, presenter.SdmInstance);
					throw new ScriptAbortException("OK");
				};
			}
			else if (action == Defaults.ScriptAction_CreateServiceInventoryItem.Duplicate)
			{
				var sourceService = GetService(sdmHelper, domId);
				presenter.LoadFromModel(sourceService, isDuplication: true);
				view.BtnAdd.Pressed += (sender, args) =>
				{
					if (!presenter.Validate())
					{
						return;
					}

					if (ServiceIdExists(sdmHelper, presenter.ServiceId))
					{
						presenter.ShowServiceIdExistsError();
						return;
					}

					DuplicateServiceViaSdm(sdmHelper, sourceService, presenter.SdmInstance);
					throw new ScriptAbortException("OK");
				};
			}
			else
			{
				// EDIT MODE
				view.BtnAdd.Text = "Save";
				presenter.LoadFromModel(GetService(sdmHelper, domId));
				view.BtnAdd.Pressed += (sender, args) =>
				{
					if (presenter.Validate())
					{
						UpdateServiceInfoViaSdm(sdmHelper, domId, presenter.SdmInstance);
						throw new ScriptAbortException("OK");
					}
				};
			}

			// Events
			view.BtnCancel.Pressed += (sender, args) => throw new ScriptAbortException("OK");

			// Run interactive
			_controller.ShowDialog(view);
		}

		private void AddServiceItemForOrder(Guid domId, IServiceManagementApiHelper sdmHelper)
		{
			var serviceOrderItem = sdmHelper.ServiceOrder.ServiceOrderItems.Read(SdmModels.ServiceOrderItemExposers.Identifier.Equal(domId.ToString())).FirstOrDefault();
			if (domId == Guid.Empty || serviceOrderItem == null)
			{
				throw new InvalidOperationException($"Please select an entry in the service order items table first.{Environment.NewLine}Details: No Service Order Item with ID '{domId}' found on the system!");
			}

			_engine.PerformanceLogger("Create New Service Inventory Item + Link to Order", () => CreateNewServiceAndLinkItToServiceOrder(sdmHelper, serviceOrderItem));
			throw new ScriptAbortException("OK");
		}

		private sealed class ConfigurationDuplicationData
		{
			public List<SdmModels.ServiceConfigurationVersion> ServiceConfigurationVersions { get; set; }

			public Dictionary<string, SdmModels.ServiceConfigurationValue> ServiceConfigurationValuesById { get; set; }

			public Dictionary<string, SdmModels.ServiceProfile> ServiceProfilesById { get; set; }

			public Dictionary<string, ConfigModels.Profile> ProfilesById { get; set; }

			public Dictionary<string, ConfigModels.ConfigurationParameterValue> ConfigParameterValuesById { get; set; }
		}

		private sealed class VersionDuplicationState
		{
			public VersionDuplicationState()
			{
				ConfigParamValueIdMap = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
				ServiceProfileIdMap = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
				ProfileIdMap = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
				DuplicatedConfigParameterValues = new List<ConfigModels.ConfigurationParameterValue>();
			}

			public Dictionary<string, string> ConfigParamValueIdMap { get; }

			public Dictionary<string, string> ServiceProfileIdMap { get; }

			public Dictionary<string, string> ProfileIdMap { get; }

			public List<ConfigModels.ConfigurationParameterValue> DuplicatedConfigParameterValues { get; }
		}
	}
}
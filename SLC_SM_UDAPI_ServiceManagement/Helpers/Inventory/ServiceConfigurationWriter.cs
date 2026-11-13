namespace SLC_SM_UDAPI_ServiceManagement.Helpers.Inventory
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using Skyline.DataMiner.SDM;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;
	using SLC_SM_UDAPI_ServiceManagement.Exceptions;
	using SLC_SM_UDAPI_ServiceManagement.Models.Inventory;
	using static SLC_SM_UDAPI_ServiceManagement.Helpers.ConfigurationRequestHelper;

	/// <summary>
	/// Saves service configuration versions using the service configuration editor contract.
	/// </summary>
	internal sealed class ServiceConfigurationWriter
	{
		private const int MaxNestedProfileDepth = 3;

		private readonly IServiceManagementApiHelper _api;

		public ServiceConfigurationWriter(IServiceManagementApiHelper api)
		{
			_api = api ?? throw new ArgumentNullException(nameof(api));
		}

		public void Save(Service service, ServiceConfigurationRequest request)
		{
			if (service == null)
			{
				throw new ArgumentNullException(nameof(service));
			}

			if (request == null)
			{
				throw new InvalidConfigurationException("A request body is required.");
			}

			var version = GetVersion(service, request.VersionId);
			if (request.DeleteVersion)
			{
				DeleteVersion(service, version);
				return;
			}

			var ownership = ReadOwnership(version);
			ValidateRequest(request, ownership);

			var parameters = ReadAll(
				_api.ServiceCatalog.ConfigurationParameters.Read(new TRUEFilterElement<ConfigurationParameter>()),
				parameter => parameter.Identifier);
			var definitions = ReadAll(
				_api.ServiceCatalog.ProfileDefinitions.Read(new TRUEFilterElement<ProfileDefinition>()),
				definition => definition.Identifier);

			UpdateVersionMetadata(version, request);
			DeleteConfigurationValues(request);
			version.Parameters = SaveConfigurationParameters(request.Parameters, parameters, ownership);
			DeleteServiceProfiles(request);
			version.Profiles = SaveServiceProfiles(request.Profiles, definitions, parameters, ownership);
			_api.ServiceInventory.ServiceConfigurationVersions.CreateOrUpdate(new[] { version });

			service.ConfigurationVersions = service.ConfigurationVersions ?? new List<SdmObjectReference<ServiceConfigurationVersion>>();
			if (!service.ConfigurationVersions.Any(reference => String.Equals(reference.Identifier, version.Identifier, StringComparison.OrdinalIgnoreCase)))
			{
				service.ConfigurationVersions.Add(new SdmObjectReference<ServiceConfigurationVersion>(version.Identifier));
			}

			if (request.ActivateVersion)
			{
				service.ServiceConfigurationId = new SdmObjectReference<ServiceConfigurationVersion>(version.Identifier);
			}

			_api.ServiceInventory.Services.Update(service);
		}

		private ServiceConfigurationVersion GetVersion(Service service, string versionId)
		{
			if (String.IsNullOrWhiteSpace(versionId))
			{
				return new ServiceConfigurationVersion { Identifier = Guid.NewGuid().ToString() };
			}

			bool isOwned = (service.ConfigurationVersions ?? new List<SdmObjectReference<ServiceConfigurationVersion>>())
				.Any(reference => String.Equals(reference.Identifier, versionId, StringComparison.OrdinalIgnoreCase))
				|| String.Equals(service.ServiceConfigurationId.Identifier, versionId, StringComparison.OrdinalIgnoreCase);
			if (!isOwned)
			{
				throw new InvalidConfigurationException(
					$"Configuration version does not belong to service {FormatName(service.Name, "the selected service")}.",
					$"Version '{versionId}', service '{service.ServiceID}'.");
			}

			return _api.ServiceInventory.ServiceConfigurationVersions
				.Read(ServiceConfigurationVersionExposers.Identifier.Equal(versionId))
				.FirstOrDefault()
				?? throw new InvalidConfigurationException("The selected configuration version was not found.", $"Version '{versionId}'.");
		}

		private void DeleteVersion(Service service, ServiceConfigurationVersion version)
		{
			service.ConfigurationVersions = (service.ConfigurationVersions ?? new List<SdmObjectReference<ServiceConfigurationVersion>>())
				.Where(reference => !String.Equals(reference.Identifier, version.Identifier, StringComparison.OrdinalIgnoreCase))
				.ToList();
			if (String.Equals(service.ServiceConfigurationId.Identifier, version.Identifier, StringComparison.OrdinalIgnoreCase))
			{
				service.ServiceConfigurationId = service.ConfigurationVersions.FirstOrDefault();
			}

			_api.ServiceInventory.Services.Update(service);
			_api.ServiceInventory.ServiceConfigurationVersions.Delete(version);
		}

		private Ownership ReadOwnership(ServiceConfigurationVersion version)
		{
			var ownership = new Ownership();
			foreach (var reference in version.Parameters ?? new List<SdmObjectReference<ServiceConfigurationValue>>())
			{
				if (String.IsNullOrWhiteSpace(reference.Identifier))
				{
					continue;
				}

				ownership.ConfigurationIds.Add(reference.Identifier);
				var configuration = _api.ServiceInventory.ServiceConfigurationValues
					.Read(ServiceConfigurationValueExposers.Identifier.Equal(reference.Identifier))
					.FirstOrDefault();
				if (!String.IsNullOrWhiteSpace(configuration?.ConfigurationParameterId.Identifier))
				{
					ownership.ParameterValueIds.Add(configuration.ConfigurationParameterId.Identifier);
				}
			}

			var assignedProfileIds = new List<string>();
			foreach (var reference in version.Profiles ?? new List<SdmObjectReference<ServiceProfile>>())
			{
				if (String.IsNullOrWhiteSpace(reference.Identifier))
				{
					continue;
				}

				ownership.ServiceProfileIds.Add(reference.Identifier);
				var serviceProfile = _api.ServiceInventory.ServiceProfiles
					.Read(ServiceProfileExposers.Identifier.Equal(reference.Identifier))
					.FirstOrDefault();
				string profileId = serviceProfile?.ProfileId.Identifier;
				if (!String.IsNullOrWhiteSpace(profileId))
				{
					assignedProfileIds.Add(profileId);
				}
			}

			// Only read the profiles this version uses (plus the reusable ones): an unfiltered read of all profiles is truncated on large systems.
			var profiles = FullServiceBuilder.ReadProfileTree(_api, assignedProfileIds);
			foreach (var profile in _api.ServiceCatalog.Profiles.Read(ProfileExposers.IsReusable.Equal(true)))
			{
				if (!String.IsNullOrWhiteSpace(profile?.Identifier))
				{
					profiles[profile.Identifier] = profile;
					ownership.ReusableProfileIds.Add(profile.Identifier);
				}
			}

			foreach (var profileId in assignedProfileIds)
			{
				ReadProfileOwnership(profileId, profiles, ownership, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
			}

			return ownership;
		}

		private static void ReadProfileOwnership(
			string profileId,
			IReadOnlyDictionary<string, Profile> profiles,
			Ownership ownership,
			HashSet<string> visited)
		{
			if (String.IsNullOrWhiteSpace(profileId) || !visited.Add(profileId) || !profiles.TryGetValue(profileId, out var profile))
			{
				return;
			}

			ownership.ProfileIds.Add(profileId);
			if (profile.IsReusable)
			{
				ownership.ReusableProfileIds.Add(profileId);
			}

			foreach (var reference in profile.ConfigurationParameterValues ?? new List<SdmObjectReference<ConfigurationParameterValue>>())
			{
				if (!String.IsNullOrWhiteSpace(reference.Identifier))
				{
					ownership.ParameterValueIds.Add(reference.Identifier);
					if (!profile.IsReusable)
					{
						ownership.ProfileParameterValueIds.Add(reference.Identifier);
					}
				}
			}

			foreach (var child in profile.Profiles ?? new List<SdmObjectReference<Profile>>())
			{
				if (!String.IsNullOrWhiteSpace(child.Identifier))
				{
					ReadProfileOwnership(child.Identifier, profiles, ownership, visited);
				}
			}
		}

		private static void ValidateRequest(ServiceConfigurationRequest request, Ownership ownership)
		{
			ValidateOwnedIds(request.DeleteConfigurationIds, ownership.ConfigurationIds, "configuration");
			ValidateOwnedIds(request.DeletedProfileIds, ownership.ServiceProfileIds, "service profile");
			ValidateOwnedIds(request.DeletedProfileParameterIds, ownership.ProfileParameterValueIds, "profile parameter value");
			ValidateUniqueReusableProfiles(request.Profiles, ownership);

			foreach (var parameter in request.Parameters ?? new List<ServiceConfigurationParameterRequest>())
			{
				if (parameter == null)
				{
					throw new InvalidConfigurationException("A configuration parameter entry cannot be null.");
				}

				ValidateOptionalOwnedId(parameter.ConfigurationId, ownership.ConfigurationIds, "configuration");
				ValidateOptionalOwnedId(parameter.ParameterValueId, ownership.ParameterValueIds, "configuration parameter value");
			}

			foreach (var profile in request.Profiles ?? new List<ServiceConfigurationProfileRequest>())
			{
				ValidateProfileRequest(profile, ownership, 1, isServiceProfile: true);
			}
		}

		private static void ValidateProfileRequest(ServiceConfigurationProfileRequest profile, Ownership ownership, int depth, bool isServiceProfile)
		{
			if (profile == null)
			{
				throw new InvalidConfigurationException("A profile entry cannot be null.");
			}

			if (depth > MaxNestedProfileDepth)
			{
				throw new InvalidConfigurationException($"Profile nesting cannot exceed {MaxNestedProfileDepth} levels.");
			}

			if (!String.IsNullOrWhiteSpace(profile.Id))
			{
				bool isOwned = profile.Reusable
					? ownership.ServiceProfileIds.Contains(profile.Id)
						|| ownership.ReusableProfileIds.Contains(profile.Id)
						|| ownership.ReusableProfileAssignments.ContainsKey(profile.Id)
					: (isServiceProfile ? ownership.ServiceProfileIds : ownership.ProfileIds).Contains(profile.Id);
				if (!isOwned)
				{
					string kind = isServiceProfile ? "service profile" : profile.Reusable ? "reusable profile" : "profile";
					throw new InvalidConfigurationException(
						$"The requested {kind} {FormatName(profile.Name, $"the selected {kind}")} does not belong to the service configuration.",
						$"{kind} '{profile.Id}'.");
				}
			}

			if (profile.Reusable)
			{
				ValidateOwnedId(GetReusableProfileId(profile, ownership), ownership.ReusableProfileIds, "reusable profile", profile.Name);
			}

			foreach (var parameter in profile.Parameters ?? new List<ServiceConfigurationParameterRequest>())
			{
				if (parameter == null)
				{
					throw new InvalidConfigurationException("A profile parameter entry cannot be null.");
				}

				ValidateOptionalOwnedId(parameter.ParameterValueId, ownership.ParameterValueIds, "configuration parameter value");
			}

			foreach (var nestedId in profile.NestedProfileIds ?? new List<string>())
			{
				if (String.IsNullOrWhiteSpace(nestedId))
				{
					continue;
				}

				if (!ownership.ProfileIds.Contains(nestedId) && !ownership.ReusableProfileIds.Contains(nestedId))
				{
					string nestedName = (profile.NestedProfiles ?? new List<ServiceConfigurationProfileRequest>())
						.FirstOrDefault(nested => String.Equals(nested?.Id, nestedId, StringComparison.OrdinalIgnoreCase))
						?.Name;
					throw new InvalidConfigurationException(
						$"Nested profile {FormatName(nestedName, "the selected nested profile")} does not belong to the service configuration.",
						$"Nested profile '{nestedId}'.");
				}
			}

			ValidateUniqueReusableProfiles(profile.NestedProfiles, ownership);
			EnsureUniqueReusableIds(profile.NestedProfileIds, ownership.ReusableProfileIds);

			foreach (var nested in profile.NestedProfiles ?? new List<ServiceConfigurationProfileRequest>())
			{
				ValidateProfileRequest(nested, ownership, depth + 1, isServiceProfile: false);
			}
		}

		private static void ValidateUniqueReusableProfiles(
			IEnumerable<ServiceConfigurationProfileRequest> profiles,
			Ownership ownership)
		{
			EnsureUniqueReusableProfiles(profiles, profile => profile.Reusable, profile => GetReusableProfileId(profile, ownership), profile => profile.Name);
		}

		private static string GetReusableProfileId(ServiceConfigurationProfileRequest profile, Ownership ownership)
		{
			return ResolveReusableProfileId(profile?.ReusableProfileId, profile?.Id, ownership.ReusableProfileAssignments);
		}

		private static void ValidateOwnedIds(IEnumerable<string> ids, ISet<string> ownedIds, string kind)
		{
			foreach (var id in ids ?? Enumerable.Empty<string>())
			{
				ValidateOptionalOwnedId(id, ownedIds, kind);
			}
		}

		private static void ValidateOwnedId(string id, ISet<string> ownedIds, string kind, string displayName = null)
		{
			if (!String.IsNullOrWhiteSpace(id) && !ownedIds.Contains(id))
			{
				throw new InvalidConfigurationException(
					$"The requested {kind} {FormatName(displayName, "the selected item")} does not belong to the service configuration.",
					$"{kind} '{id}'.");
			}
		}

		private static void ValidateOptionalOwnedId(string id, ISet<string> ownedIds, string kind)
		{
			if (!String.IsNullOrWhiteSpace(id) && !ownedIds.Contains(id))
			{
				throw new InvalidConfigurationException($"The requested {kind} does not belong to the service configuration.", $"{kind} '{id}'.");
			}
		}

		private static void UpdateVersionMetadata(ServiceConfigurationVersion version, ServiceConfigurationRequest request)
		{
			version.VersionName = String.IsNullOrWhiteSpace(request.VersionName) ? "Current version" : request.VersionName.Trim();
			version.Description = request.Description ?? String.Empty;
			version.StartDate = ParseDate(request.StartDate);
			version.EndDate = ParseDate(request.EndDate);
		}

		private void DeleteConfigurationValues(ServiceConfigurationRequest request)
		{
			foreach (var configurationId in request.DeleteConfigurationIds ?? new List<string>())
			{
				var configuration = _api.ServiceInventory.ServiceConfigurationValues
					.Read(ServiceConfigurationValueExposers.Identifier.Equal(configurationId))
					.FirstOrDefault();
				if (configuration != null)
				{
					_api.ServiceInventory.ServiceConfigurationValues.Delete(configuration);
				}
			}

			foreach (var parameterValueId in request.DeletedProfileParameterIds ?? new List<string>())
			{
				var value = _api.ServiceCatalog.ConfigurationParameterValues
					.Read(ConfigurationParameterValueExposers.Identifier.Equal(parameterValueId))
					.FirstOrDefault();
				if (value != null)
				{
					_api.ServiceCatalog.ConfigurationParameterValues.Delete(value);
				}
			}
		}

		private List<SdmObjectReference<ServiceConfigurationValue>> SaveConfigurationParameters(
			IEnumerable<ServiceConfigurationParameterRequest> requests,
			IReadOnlyDictionary<string, ConfigurationParameter> parameters,
			Ownership ownership)
		{
			var references = new List<SdmObjectReference<ServiceConfigurationValue>>();
			foreach (var parameter in requests ?? Enumerable.Empty<ServiceConfigurationParameterRequest>())
			{
				if (String.IsNullOrWhiteSpace(parameter.ParameterId) || !parameters.TryGetValue(parameter.ParameterId, out var definition))
				{
					throw new InvalidConfigurationException(
						$"The selected configuration parameter {FormatName(parameter.Label, "the selected parameter")} was not found.",
						$"Configuration parameter '{parameter.ParameterId}'.");
				}

				string parameterName = ParameterName(parameter, definition);
				var value = SaveParameterValue(parameter, definition);
				var configuration = String.IsNullOrWhiteSpace(parameter.ConfigurationId)
					? new ServiceConfigurationValue { Identifier = Guid.NewGuid().ToString() }
					: _api.ServiceInventory.ServiceConfigurationValues
						.Read(ServiceConfigurationValueExposers.Identifier.Equal(parameter.ConfigurationId))
						.FirstOrDefault() ?? throw new InvalidConfigurationException(
							$"Configuration for parameter {FormatName(parameterName, "the selected parameter")} was not found.",
							$"Configuration '{parameter.ConfigurationId}'.");
				configuration.ConfigurationParameterId = new SdmObjectReference<ConfigurationParameterValue>(value.Identifier);
				configuration.Mandatory = parameter.Mandatory;
				_api.ServiceInventory.ServiceConfigurationValues.CreateOrUpdate(new[] { configuration });
				ownership.ParameterValueIds.Add(value.Identifier);
				ownership.ConfigurationIds.Add(configuration.Identifier);
				references.Add(new SdmObjectReference<ServiceConfigurationValue>(configuration.Identifier));
			}

			return references;
		}

		private ConfigurationParameterValue SaveParameterValue(ServiceConfigurationParameterRequest parameter, ConfigurationParameter definition)
		{
			string parameterName = ParameterName(parameter, definition);
			var value = String.IsNullOrWhiteSpace(parameter.ParameterValueId)
				? new ConfigurationParameterValue { Identifier = Guid.NewGuid().ToString() }
				: _api.ServiceCatalog.ConfigurationParameterValues
					.Read(ConfigurationParameterValueExposers.Identifier.Equal(parameter.ParameterValueId))
					.FirstOrDefault() ?? throw new InvalidConfigurationException(
						$"The value for configuration parameter {FormatName(parameterName, "the selected parameter")} was not found.",
						$"Configuration parameter value '{parameter.ParameterValueId}'.");
			string existingNumberOptionsId = value.IsValueOf(definition) ? value.NumberOptionsId.Identifier : null;
			var numberOptions = String.Equals(definition.Type.ToString(), "Number", StringComparison.OrdinalIgnoreCase)
				? PrepareNumberOptions(definition, existingNumberOptionsId, parameterName)
				: null;
			try
			{
				value.ApplyConfigurationValue(definition, parameter.Value, parameter.Mandatory);
			}
			catch (InvalidOperationException ex)
			{
				throw new InvalidConfigurationException(ex.Message, ex);
			}

			value.Label = parameter.Label ?? value.Label ?? String.Empty;
			value.IsLinked = parameter.IsLinked;
			value.ValueFixed = parameter.Fixed;
			if (numberOptions != null)
			{
				ApplyNumberOptions(numberOptions, parameter);
				value.NumberOptionsId = new SdmObjectReference<NumberParameterOptions>(numberOptions.Identifier);
				_api.ServiceCatalog.NumberParameterOptions.CreateOrUpdate(new[] { numberOptions });
			}

			_api.ServiceCatalog.ConfigurationParameterValues.CreateOrUpdate(new[] { value });
			return value;
		}

		private NumberParameterOptions PrepareNumberOptions(ConfigurationParameter definition, string existingOptionsId, string parameterName)
		{
			string definitionOptionsId = definition.NumberOptionsId.Identifier;
			if (!String.IsNullOrWhiteSpace(existingOptionsId)
				&& !String.Equals(existingOptionsId, definitionOptionsId, StringComparison.OrdinalIgnoreCase))
			{
				return _api.ServiceCatalog.NumberParameterOptions
					.Read(NumberParameterOptionsExposers.Identifier.Equal(existingOptionsId))
					.FirstOrDefault() ?? throw new InvalidConfigurationException(
						$"Number options for configuration parameter {FormatName(parameterName, "the selected parameter")} were not found.",
						$"Number options '{existingOptionsId}'.");
			}

			var source = String.IsNullOrWhiteSpace(definitionOptionsId)
				? null
				: _api.ServiceCatalog.NumberParameterOptions
					.Read(NumberParameterOptionsExposers.Identifier.Equal(definitionOptionsId))
					.FirstOrDefault();
			if (source == null)
			{
				return new NumberParameterOptions { Identifier = Guid.NewGuid().ToString() };
			}

			return new NumberParameterOptions
			{
				Identifier = Guid.NewGuid().ToString(),
				Units = source.Units?.ToList() ?? new List<SdmObjectReference<ConfigurationUnit>>(),
				DefaultUnitId = source.DefaultUnitId,
				MinRange = source.MinRange,
				MaxRange = source.MaxRange,
				Decimals = source.Decimals,
				StepSize = source.StepSize,
				DefaultValue = source.DefaultValue,
			};
		}

		private static void ApplyNumberOptions(NumberParameterOptions options, ServiceConfigurationParameterRequest parameter)
		{
			if (!String.IsNullOrWhiteSpace(parameter.UnitId))
			{
				options.DefaultUnitId = new SdmObjectReference<ConfigurationUnit>(parameter.UnitId);
			}

			if (!String.IsNullOrWhiteSpace(parameter.Start))
			{
				options.MinRange = ParseDouble(parameter.Start, "minimum");
			}

			if (!String.IsNullOrWhiteSpace(parameter.End))
			{
				options.MaxRange = ParseDouble(parameter.End, "maximum");
			}

			if (!String.IsNullOrWhiteSpace(parameter.StepSize))
			{
				options.StepSize = ParseDouble(parameter.StepSize, "step size");
			}

			if (!String.IsNullOrWhiteSpace(parameter.Decimals))
			{
				if (!Int64.TryParse(parameter.Decimals, NumberStyles.Integer, CultureInfo.InvariantCulture, out var decimals) || decimals < 0)
				{
					throw new InvalidConfigurationException($"Decimals '{parameter.Decimals}' is not a valid non-negative integer.");
				}

				options.Decimals = decimals;
			}
		}

		private void DeleteServiceProfiles(ServiceConfigurationRequest request)
		{
			foreach (var profileId in request.DeletedProfileIds ?? new List<string>())
			{
				var profile = _api.ServiceInventory.ServiceProfiles
					.Read(ServiceProfileExposers.Identifier.Equal(profileId))
					.FirstOrDefault();
				if (profile != null)
				{
					_api.ServiceInventory.ServiceProfiles.Delete(profile);
				}
			}
		}

		private List<SdmObjectReference<ServiceProfile>> SaveServiceProfiles(
			IEnumerable<ServiceConfigurationProfileRequest> requests,
			IReadOnlyDictionary<string, ProfileDefinition> definitions,
			IReadOnlyDictionary<string, ConfigurationParameter> parameters,
			Ownership ownership)
		{
			var references = new List<SdmObjectReference<ServiceProfile>>();
			foreach (var request in requests ?? Enumerable.Empty<ServiceConfigurationProfileRequest>())
			{
				var profile = SaveServiceProfile(request, definitions, parameters, ownership, 1);
				references.Add(new SdmObjectReference<ServiceProfile>(profile.Identifier));
			}

			return references;
		}

		private ServiceProfile SaveServiceProfile(
			ServiceConfigurationProfileRequest request,
			IReadOnlyDictionary<string, ProfileDefinition> definitions,
			IReadOnlyDictionary<string, ConfigurationParameter> parameters,
			Ownership ownership,
			int depth)
		{
			if (String.IsNullOrWhiteSpace(request?.DefinitionId) || !definitions.TryGetValue(request.DefinitionId, out var definition))
			{
				throw new InvalidConfigurationException(
					$"Profile definition for {FormatName(request?.Name, "the selected profile")} was not found.",
					$"Profile definition '{request?.DefinitionId}'.");
			}

			var serviceProfile = String.IsNullOrWhiteSpace(request.Id) || !ownership.ServiceProfileIds.Contains(request.Id)
				? new ServiceProfile { Identifier = Guid.NewGuid().ToString() }
				: _api.ServiceInventory.ServiceProfiles
					.Read(ServiceProfileExposers.Identifier.Equal(request.Id))
					.FirstOrDefault() ?? throw new InvalidConfigurationException(
						$"Service profile {FormatName(request.Name, "the selected profile")} was not found.",
						$"Service profile '{request.Id}'.");
			if (request.Reusable)
			{
				// Reusable profiles are shared, so they are referenced and never created or modified here.
				var reusable = ReadReusableProfile(
					_api,
					FirstNonEmpty(request.ReusableProfileId, serviceProfile.ProfileId.Identifier ?? request.Id),
					"Reusable profile",
					request.Name);
				return SaveServiceProfileReference(serviceProfile, reusable, request.Mandatory, ownership);
			}

			var profile = serviceProfile.ProfileId.Identifier == null
				? new Profile { Identifier = Guid.NewGuid().ToString() }
				: _api.ServiceCatalog.Profiles
					.Read(ProfileExposers.Identifier.Equal(serviceProfile.ProfileId.Identifier))
					.FirstOrDefault() ?? throw new InvalidConfigurationException(
						$"Profile {FormatName(request.Name, "the selected profile")} was not found.",
						$"Profile '{serviceProfile.ProfileId.Identifier}'.");
			SaveOwnedProfile(profile, request, definition, definitions, parameters, ownership, depth);
			return SaveServiceProfileReference(serviceProfile, profile, request.Mandatory, ownership);
		}

		private ServiceProfile SaveServiceProfileReference(ServiceProfile serviceProfile, Profile profile, bool mandatory, Ownership ownership)
		{
			serviceProfile.ProfileId = new SdmObjectReference<Profile>(profile.Identifier);
			serviceProfile.ProfileDefinitionId = new SdmObjectReference<ProfileDefinition>(profile.ProfileDefinitionId.Identifier);
			serviceProfile.Mandatory = mandatory;
			_api.ServiceInventory.ServiceProfiles.CreateOrUpdate(new[] { serviceProfile });
			ownership.ServiceProfileIds.Add(serviceProfile.Identifier);
			ownership.ProfileIds.Add(profile.Identifier);
			return serviceProfile;
		}

		private List<string> SaveNestedProfiles(
			ServiceConfigurationProfileRequest request,
			IReadOnlyDictionary<string, ProfileDefinition> definitions,
			IReadOnlyDictionary<string, ConfigurationParameter> parameters,
			Ownership ownership,
			int depth)
		{
			var ids = (request.NestedProfileIds ?? new List<string>())
				.Where(id => !String.IsNullOrWhiteSpace(id))
				.ToList();
			foreach (var nested in request.NestedProfiles ?? new List<ServiceConfigurationProfileRequest>())
			{
				string nestedId = SaveNestedProfile(nested, definitions, parameters, ownership, depth + 1);
				if (!ids.Contains(nestedId, StringComparer.OrdinalIgnoreCase))
				{
					ids.Add(nestedId);
				}
			}

			return ids;
		}

		private string SaveNestedProfile(
			ServiceConfigurationProfileRequest request,
			IReadOnlyDictionary<string, ProfileDefinition> definitions,
			IReadOnlyDictionary<string, ConfigurationParameter> parameters,
			Ownership ownership,
			int depth)
		{
			if (request == null || String.IsNullOrWhiteSpace(request.DefinitionId) || !definitions.TryGetValue(request.DefinitionId, out var definition))
			{
				throw new InvalidConfigurationException(
					$"Nested profile definition for {FormatName(request?.Name, "the selected nested profile")} was not found.",
					$"Profile definition '{request?.DefinitionId}'.");
			}

			if (depth > MaxNestedProfileDepth)
			{
				throw new InvalidConfigurationException($"Profile nesting cannot exceed {MaxNestedProfileDepth} levels.");
			}

			if (request.Reusable)
			{
				return ReadReusableProfile(
					_api,
					FirstNonEmpty(request.ReusableProfileId, request.Id),
					"Reusable nested profile",
					request.Name).Identifier;
			}

			var profile = String.IsNullOrWhiteSpace(request.Id)
				? new Profile { Identifier = Guid.NewGuid().ToString() }
				: _api.ServiceCatalog.Profiles
					.Read(ProfileExposers.Identifier.Equal(request.Id))
					.FirstOrDefault() ?? throw new InvalidConfigurationException(
						$"Nested profile {FormatName(request.Name, "the selected nested profile")} was not found.",
						$"Profile '{request.Id}'.");
			SaveOwnedProfile(profile, request, definition, definitions, parameters, ownership, depth);
			ownership.ProfileIds.Add(profile.Identifier);
			return profile.Identifier;
		}

		private void SaveOwnedProfile(
			Profile profile,
			ServiceConfigurationProfileRequest request,
			ProfileDefinition definition,
			IReadOnlyDictionary<string, ProfileDefinition> definitions,
			IReadOnlyDictionary<string, ConfigurationParameter> parameters,
			Ownership ownership,
			int depth)
		{
			profile.Name = String.IsNullOrWhiteSpace(request.Name) ? definition.Name : request.Name.Trim();
			profile.ProfileDefinitionId = new SdmObjectReference<ProfileDefinition>(definition.Identifier);
			profile.IsReusable = false;
			profile.Profiles = SaveNestedProfiles(request, definitions, parameters, ownership, depth)
				.Select(id => new SdmObjectReference<Profile>(id))
				.ToList();
			profile.ConfigurationParameterValues = SaveProfileParameterValues(request.Parameters, parameters, ownership);
			_api.ServiceCatalog.Profiles.CreateOrUpdate(new[] { profile });
		}

		private List<SdmObjectReference<ConfigurationParameterValue>> SaveProfileParameterValues(
			IEnumerable<ServiceConfigurationParameterRequest> requests,
			IReadOnlyDictionary<string, ConfigurationParameter> parameters,
			Ownership ownership)
		{
			var references = new List<SdmObjectReference<ConfigurationParameterValue>>();
			foreach (var parameter in requests ?? Enumerable.Empty<ServiceConfigurationParameterRequest>())
			{
				if (parameter == null || !parameters.TryGetValue(parameter?.ParameterId ?? String.Empty, out var definition))
				{
					throw new InvalidConfigurationException(
						$"Profile configuration parameter {FormatName(parameter?.Label, "the selected parameter")} was not found.",
						$"Configuration parameter '{parameter?.ParameterId}'.");
				}

				var value = SaveParameterValue(parameter, definition);
				ownership.ParameterValueIds.Add(value.Identifier);
				ownership.ProfileParameterValueIds.Add(value.Identifier);
				references.Add(new SdmObjectReference<ConfigurationParameterValue>(value.Identifier));
			}

			return references;
		}

		private static double ParseDouble(string value, string fieldName)
		{
			if (!Double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
			{
				throw new InvalidConfigurationException($"Number option {fieldName} '{value}' is invalid.");
			}

			return result;
		}

		private static DateTime? ParseDate(string value)
		{
			if (String.IsNullOrWhiteSpace(value))
			{
				return null;
			}

			if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
			{
				throw new InvalidConfigurationException($"Invalid configuration date '{value}'.");
			}

			return parsed.ToUniversalTime();
		}

		private static string ParameterName(ServiceConfigurationParameterRequest parameter, ConfigurationParameter definition)
		{
			return ConfigurationRequestHelper.ParameterName(parameter?.Label, definition);
		}

		private static Dictionary<string, T> ReadAll<T>(IEnumerable<T> items, Func<T, string> key)
		{
			var result = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
			foreach (var item in items ?? Enumerable.Empty<T>())
			{
				string id = key(item);
				if (!String.IsNullOrWhiteSpace(id))
				{
					result[id] = item;
				}
			}

			return result;
		}

		private sealed class Ownership
		{
			public HashSet<string> ConfigurationIds { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			public HashSet<string> ParameterValueIds { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			public HashSet<string> ProfileParameterValueIds { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			public HashSet<string> ProfileIds { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			public HashSet<string> ReusableProfileIds { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			public Dictionary<string, string> ReusableProfileAssignments { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

			public HashSet<string> ServiceProfileIds { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		}
	}
}

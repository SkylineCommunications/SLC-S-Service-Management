/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE        VERSION        AUTHOR            COMMENTS

09/10/2026  1.0.0.1        SKA, Skyline      DataMinerApp support
****************************************************************************
*/

namespace SLC_SM_GQIDS_Get_Service_Configurations
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;
	using DomHelpers.SlcConfigurations;
	using DomHelpers.SlcServicemanagement;
	using Skyline.DataMiner.Analytics.GenericInterface;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using SLC_SM_Common.Extensions;
	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	[GQIMetaData(Name = DataSourceName)]
	public sealed class GetServiceConfigurations : IGQIDataSource, IGQIInputArguments, IGQIOnInit
	{
		private const string DataSourceName = "Get_ServiceConfigurations";
		private const string ServiceOwner = "Service";
		private const string SpecificationOwner = "Specification";
		private const string OrderItemOwner = "OrderItem";
		private readonly GQIStringArgument domIdArg = new GQIStringArgument("DOM ID") { IsRequired = true };
		private readonly GQIStringArgument configurationTypeArg = new GQIStringArgument("Configuration Type") { IsRequired = false };
		private GQIDMS _dms;
		private IGQILogger _logger;
		private IServiceManagementApiHelper _api;
		private Guid _serviceId;
		private string _configurationType;

		public GQIColumn[] GetColumns()
		{
			return new GQIColumn[]
			{
				new GQIStringColumn("Version ID"),
				new GQIStringColumn("Version Name"),
				new GQIStringColumn("Version Description"),
				new GQIDateTimeColumn("Version Start"),
				new GQIDateTimeColumn("Version End"),
				new GQIBooleanColumn("Is Active"),
				new GQIStringColumn("Record Type"),
				new GQIStringColumn("Configuration ID"),
				new GQIStringColumn("Parameter ID"),
				new GQIStringColumn("Parameter Value ID"),
				new GQIStringColumn("Parameter Name"),
				new GQIStringColumn("Parameter Type"),
				new GQIStringColumn("Parameter Value"),
				new GQIBooleanColumn("Mandatory"),
				new GQIStringColumn("Profile ID"),
				new GQIStringColumn("Profile Name"),
				new GQIStringColumn("Profile Definition ID"),
				new GQIStringColumn("Profile Definition Name"),
				new GQIBooleanColumn("Profile Reusable"),
				new GQIBooleanColumn("Profile Mandatory"),
				new GQIStringColumn("Nested Profile ID"),
				new GQIStringColumn("Nested Profile Name"),
				new GQIStringColumn("Nested Profile Definition ID"),
				new GQIStringColumn("Nested Profile Definition Name"),
			};
		}

		public GQIArgument[] GetInputArguments() => new GQIArgument[] { domIdArg, configurationTypeArg };

		public OnInitOutputArgs OnInit(OnInitInputArgs args)
		{
			_dms = args.DMS;
			_logger = args.Logger;
			_logger.MinimumLogLevel = GQILogLevel.Debug;
			_api = new ServiceManagementApiHelper(_dms.GetConnection(), "Service Inventory");
			return default;
		}

		public OnArgumentsProcessedOutputArgs OnArgumentsProcessed(OnArgumentsProcessedInputArgs args)
		{
			_serviceId = Guid.TryParse(args.GetArgumentValue(domIdArg), out var id) ? id : Guid.Empty;
			_configurationType = args.GetArgumentValue(configurationTypeArg);
			return new OnArgumentsProcessedOutputArgs();
		}

		public GQIPage GetNextPage(GetNextPageInputArgs args)
		{
			return _logger.PerformanceLogger(nameof(GetNextPage), BuildPage);
		}

		private GQIPage BuildPage()
		{
			try
			{
				if (_serviceId == Guid.Empty)
				{
					return new GQIPage(Array.Empty<GQIRow>()) { HasNextPage = false };
				}

				var configurationType = String.IsNullOrWhiteSpace(_configurationType) ? ServiceOwner : _configurationType.Trim();
				if (!ServiceOwner.Equals(configurationType, StringComparison.OrdinalIgnoreCase)
					&& !SpecificationOwner.Equals(configurationType, StringComparison.OrdinalIgnoreCase)
					&& !OrderItemOwner.Equals(configurationType, StringComparison.OrdinalIgnoreCase))
				{
					throw new InvalidOperationException($"Unsupported configuration type '{configurationType}'.");
				}

				var service = ServiceOwner.Equals(configurationType, StringComparison.OrdinalIgnoreCase)
					? _api.ServiceInventory.Services
						.Read(ServiceExposers.Identifier.Equal(_serviceId.ToString()))
						.FirstOrDefault()
					: null;
				if (service != null)
				{
				var versions = _api.ServiceInventory.ServiceConfigurationVersions
					.Read(new TRUEFilterElement<Models.ServiceConfigurationVersion>())
					.Where(version => service.ConfigurationVersions.Any(reference => reference.Identifier == version.Identifier))
					.ToDictionary(version => version.Identifier, StringComparer.OrdinalIgnoreCase);
				if (versions.Count == 0 && !String.IsNullOrWhiteSpace(service.ServiceConfigurationId.Identifier))
				{
					var activeVersion = _api.ServiceInventory.ServiceConfigurationVersions
						.Read(ServiceConfigurationVersionExposers.Identifier.Equal(service.ServiceConfigurationId.Identifier))
						.FirstOrDefault();
					if (activeVersion != null)
					{
						versions[activeVersion.Identifier] = activeVersion;
					}
				}

				var configurationValues = _api.ServiceInventory.ServiceConfigurationValues
					.Read(new TRUEFilterElement<Models.ServiceConfigurationValue>())
					.ToDictionary(value => value.Identifier, StringComparer.OrdinalIgnoreCase);
				var parameterValues = _api.ServiceCatalog.ConfigurationParameterValues
					.Read(new TRUEFilterElement<ConfigurationParameterValue>())
					.ToDictionary(value => value.Identifier, StringComparer.OrdinalIgnoreCase);
				var parameters = _api.ServiceCatalog.ConfigurationParameters
					.Read(new TRUEFilterElement<ConfigurationParameter>())
					.ToDictionary(parameter => parameter.Identifier, StringComparer.OrdinalIgnoreCase);
				var serviceProfiles = _api.ServiceInventory.ServiceProfiles
					.Read(new TRUEFilterElement<Models.ServiceProfile>())
					.ToDictionary(profile => profile.Identifier, StringComparer.OrdinalIgnoreCase);
				var profiles = _api.ServiceCatalog.Profiles
					.Read(new TRUEFilterElement<Profile>())
					.ToDictionary(profile => profile.Identifier, StringComparer.OrdinalIgnoreCase);
				var profileDefinitions = _api.ServiceCatalog.ProfileDefinitions
					.Read(new TRUEFilterElement<ProfileDefinition>())
					.ToDictionary(definition => definition.Identifier, StringComparer.OrdinalIgnoreCase);
				var rows = new List<GQIRow>();
				rows.AddRange(parameters.Values.Select(parameter => BuildAvailableParameterRow(parameter)));
				rows.AddRange(profileDefinitions.Values.Select(definition => BuildAvailableProfileRow(definition)));
				var availableNestedProfileIds = new HashSet<string>(
					profiles.Values.Where(profile => profile.IsReusable).Select(profile => profile.Identifier),
					StringComparer.OrdinalIgnoreCase);
				rows.AddRange(profiles.Values.Where(profile => profile.IsReusable).Select(profile => BuildAvailableNestedProfileRow(profile, profileDefinitions)));
				foreach (var version in versions.Values.OrderByDescending(item => item.StartDate ?? DateTime.MinValue))
				{
					var versionParameters = version.Parameters ?? new List<Skyline.DataMiner.SDM.SdmObjectReference<Models.ServiceConfigurationValue>>();
					if (versionParameters.Count == 0)
					{
						rows.Add(BuildRow(version, service.ServiceConfigurationId.Identifier, "Version", null, null, null, null, false, false, null, null, null, null, null));
					}

					foreach (var reference in versionParameters)
					{
						if (!configurationValues.TryGetValue(reference.Identifier, out var serviceConfigurationValue))
						{
							continue;
						}

						string parameterValueId = serviceConfigurationValue.ConfigurationParameterId.Identifier;
						parameterValues.TryGetValue(parameterValueId ?? String.Empty, out var parameterValue);
						string parameterId = parameterValue == null ? String.Empty : parameterValue.ConfigurationParameterId.Identifier;
						parameters.TryGetValue(parameterId, out var parameter);
						rows.Add(BuildRow(version, service.ServiceConfigurationId.Identifier, "Parameter", null, null, null, null, false, false, parameter, parameterValue, serviceConfigurationValue, parameterValueId, null));
					}

					foreach (var profileReference in version.Profiles ?? new List<Skyline.DataMiner.SDM.SdmObjectReference<Models.ServiceProfile>>())
					{
						if (!serviceProfiles.TryGetValue(profileReference.Identifier, out var serviceProfile)
							|| serviceProfile.ProfileId == null
							|| !profiles.TryGetValue(serviceProfile.ProfileId.Identifier, out var profile))
						{
							continue;
						}

						string profileDefinitionId = !String.IsNullOrWhiteSpace(serviceProfile.ProfileDefinitionId.Identifier)
							? serviceProfile.ProfileDefinitionId.Identifier
							: profile.ProfileDefinitionId.Identifier;
						profileDefinitions.TryGetValue(profileDefinitionId, out var profileDefinition);
						var profileParameters = profile.ConfigurationParameterValues ?? new List<Skyline.DataMiner.SDM.SdmObjectReference<ConfigurationParameterValue>>();
						if (profileParameters.Count == 0)
						{
							rows.Add(BuildRow(version, service.ConfigurationVersions.Any(reference => reference.Identifier == version.Identifier) ? service.ServiceConfigurationId.Identifier : String.Empty, "Profile", serviceProfile.Identifier, profile.Name, profileDefinitionId, profileDefinition?.Name, profile.IsReusable, serviceProfile.Mandatory, null, null, null, null, null));
						}
						else
						{
							foreach (var profileParameterReference in profileParameters)
							{
								parameterValues.TryGetValue(profileParameterReference.Identifier, out var profileParameterValue);
								string profileParameterId = profileParameterValue == null ? String.Empty : profileParameterValue.ConfigurationParameterId.Identifier;
								parameters.TryGetValue(profileParameterId, out var profileParameter);
								rows.Add(BuildRow(version, service.ServiceConfigurationId.Identifier, "ProfileParameter", serviceProfile.Identifier, profile.Name, profileDefinitionId, profileDefinition?.Name, profile.IsReusable, serviceProfile.Mandatory, profileParameter, profileParameterValue, null, profileParameterReference.Identifier, null));
							}
						}

						AppendNestedProfileRows(
							rows,
							version,
							service.ServiceConfigurationId.Identifier,
							serviceProfile.Identifier,
							profile.Name,
							profile,
							profiles,
							profileDefinitions,
							parameters,
							parameterValues,
							availableNestedProfileIds,
							new HashSet<string>(StringComparer.OrdinalIgnoreCase) { profile.Identifier });
					}
				}

					return new GQIPage(rows.ToArray()) { HasNextPage = false };
				}

				if (ServiceOwner.Equals(configurationType, StringComparison.OrdinalIgnoreCase))
				{
					throw new InvalidOperationException($"Service '{_serviceId}' was not found.");
				}

				var specification = SpecificationOwner.Equals(configurationType, StringComparison.OrdinalIgnoreCase)
					? _api.ServiceCatalog.ServiceSpecifications
						.Read(ServiceSpecificationExposers.Identifier.Equal(_serviceId.ToString()))
						.FirstOrDefault()
					: null;
				if (specification != null)
				{
					return new GQIPage(BuildSpecificationRows(specification)) { HasNextPage = false };
				}

				if (SpecificationOwner.Equals(configurationType, StringComparison.OrdinalIgnoreCase))
				{
					throw new InvalidOperationException($"Service specification '{_serviceId}' was not found.");
				}

				var orderItem = _api.ServiceOrder.ServiceOrderItems
					.Read(ServiceOrderItemExposers.Identifier.Equal(_serviceId.ToString()))
					.FirstOrDefault() ?? throw new InvalidOperationException($"Order item '{_serviceId}' was not found.");
				return new GQIPage(BuildOrderItemRows(orderItem)) { HasNextPage = false };
			}
			catch (Exception exception)
			{
				_dms.GenerateInformationMessage($"GQIDS|{DataSourceName}|Exception: {exception}");
				_logger.Error($"GQIDS|{DataSourceName}|Exception: {exception}");
				throw;
			}
		}

		private GQIRow[] BuildSpecificationRows(Models.ServiceSpecification specification)
			{
				var parameterValues = _api.ServiceCatalog.ConfigurationParameterValues
					.Read(new TRUEFilterElement<ConfigurationParameterValue>())
					.ToDictionary(value => value.Identifier, StringComparer.OrdinalIgnoreCase);
				var parameters = _api.ServiceCatalog.ConfigurationParameters
					.Read(new TRUEFilterElement<ConfigurationParameter>())
					.ToDictionary(parameter => parameter.Identifier, StringComparer.OrdinalIgnoreCase);
				var profileConfigurations = _api.ServiceCatalog.ServiceSpecificationProfiles
					.Read(new TRUEFilterElement<Models.ServiceSpecificationProfile>())
					.ToDictionary(profile => profile.Identifier, StringComparer.OrdinalIgnoreCase);
				var profiles = _api.ServiceCatalog.Profiles
					.Read(new TRUEFilterElement<Profile>())
					.ToDictionary(profile => profile.Identifier, StringComparer.OrdinalIgnoreCase);
				var profileDefinitions = _api.ServiceCatalog.ProfileDefinitions
					.Read(new TRUEFilterElement<ProfileDefinition>())
					.ToDictionary(definition => definition.Identifier, StringComparer.OrdinalIgnoreCase);
				var rows = new List<GQIRow>();
				rows.AddRange(parameters.Values.Select(parameter => BuildAvailableParameterRow(parameter)));
				rows.AddRange(profileDefinitions.Values.Select(definition => BuildAvailableProfileRow(definition)));
				var availableNestedProfileIds = new HashSet<string>(
					profiles.Values.Where(profile => profile.IsReusable).Select(profile => profile.Identifier),
					StringComparer.OrdinalIgnoreCase);
				rows.AddRange(profiles.Values.Where(profile => profile.IsReusable).Select(profile => BuildAvailableNestedProfileRow(profile, profileDefinitions)));

				var version = new ServiceConfigurationVersion
				{
					Identifier = specification.Identifier,
					VersionName = specification.Name,
					Description = specification.Description,
				};
				foreach (var reference in specification.ConfigurationParameters ?? new List<Skyline.DataMiner.SDM.SdmObjectReference<Models.ServiceSpecificationConfigurationValue>>())
				{
					var configuration = _api.ServiceCatalog.ServiceSpecificationConfigurationValues
						.Read(ServiceSpecificationConfigurationValueExposers.Identifier.Equal(reference.Identifier))
						.FirstOrDefault();
					if (configuration == null) continue;

					string valueId = configuration.ConfigurationParameterId.Identifier;
					parameterValues.TryGetValue(valueId, out var parameterValue);
					string parameterId = parameterValue == null ? valueId : parameterValue.ConfigurationParameterId.Identifier;
					parameters.TryGetValue(parameterId, out var parameter);
					rows.Add(BuildSpecificationParameterRow(version, configuration, parameter, parameterValue, valueId));
				}

				foreach (var reference in specification.ConfigurationProfiles ?? new List<Skyline.DataMiner.SDM.SdmObjectReference<Models.ServiceSpecificationProfile>>())
				{
					if (!profileConfigurations.TryGetValue(reference.Identifier, out var configuration)
						|| configuration.ProfileId == null
						|| !profiles.TryGetValue(configuration.ProfileId.Identifier, out var profile))
					{
						continue;
					}

					string definitionId = !String.IsNullOrWhiteSpace(configuration.ProfileDefinitionId.Identifier)
						? configuration.ProfileDefinitionId.Identifier
						: profile.ProfileDefinitionId.Identifier;
					profileDefinitions.TryGetValue(definitionId, out var definition);
					rows.Add(BuildSpecificationProfileRow(version, configuration, profile, definition));
					foreach (var parameterReference in profile.ConfigurationParameterValues ?? new List<Skyline.DataMiner.SDM.SdmObjectReference<ConfigurationParameterValue>>())
					{
						parameterValues.TryGetValue(parameterReference.Identifier, out var parameterValue);
						string parameterId = parameterValue == null ? String.Empty : parameterValue.ConfigurationParameterId.Identifier;
						parameters.TryGetValue(parameterId, out var parameter);
						rows.Add(BuildSpecificationProfileParameterRow(version, configuration, profile, definition, parameter, parameterValue, parameterReference.Identifier));
					}
					AppendNestedProfileRows(
						rows,
						version,
						version.Identifier,
						configuration.Identifier,
						profile.Name,
						profile,
						profiles,
						profileDefinitions,
						parameters,
						parameterValues,
						availableNestedProfileIds,
						new HashSet<string>(StringComparer.OrdinalIgnoreCase) { profile.Identifier });
				}

				return rows.ToArray();
			}

			private GQIRow[] BuildOrderItemRows(Models.ServiceOrderItem orderItem)
			{
				var parameterValues = _api.ServiceCatalog.ConfigurationParameterValues
					.Read(new TRUEFilterElement<ConfigurationParameterValue>())
					.ToDictionary(value => value.Identifier, StringComparer.OrdinalIgnoreCase);
				var parameters = _api.ServiceCatalog.ConfigurationParameters
					.Read(new TRUEFilterElement<ConfigurationParameter>())
					.ToDictionary(parameter => parameter.Identifier, StringComparer.OrdinalIgnoreCase);
				var version = new ServiceConfigurationVersion
				{
					Identifier = orderItem.Identifier,
					VersionName = orderItem.Name,
					Description = orderItem.Description,
				};
				var rows = new List<GQIRow>
				{
					BuildRow(version, orderItem.Identifier, "Version", null, null, null, null, false, false, null, null, null, null, null),
				};
				rows.AddRange(parameters.Values.Select(parameter => BuildAvailableParameterRow(parameter)));
				foreach (var reference in orderItem.ServiceInfo?.Configurations ?? new List<Skyline.DataMiner.SDM.SdmObjectReference<Models.ServiceOrderItemConfigurationValue>>())
				{
					var configuration = _api.ServiceOrder.ServiceOrderItemConfigurationValues
						.Read(ServiceOrderItemConfigurationValueExposers.Identifier.Equal(reference.Identifier))
						.FirstOrDefault();
					if (configuration == null) continue;

					string valueId = configuration.ConfigurationParameterValueId.Identifier;
					parameterValues.TryGetValue(valueId, out var value);
					string parameterId = value == null ? String.Empty : value.ConfigurationParameterId.Identifier;
					parameters.TryGetValue(parameterId, out var parameter);
					rows.Add(BuildOrderItemParameterRow(version, configuration, parameter, value, valueId));
				}
				return rows.ToArray();
			}

			private static GQIRow BuildOrderItemParameterRow(ServiceConfigurationVersion version, Models.ServiceOrderItemConfigurationValue configuration, ConfigurationParameter parameter, ConfigurationParameterValue value, string valueId)
			{
				return BuildRow(version, version.Identifier, "Parameter", null, null, null, null, false, configuration.Mandatory, parameter, value, null, valueId, configuration.Identifier);
			}

		private static GQIRow BuildSpecificationParameterRow(ServiceConfigurationVersion version, Models.ServiceSpecificationConfigurationValue configuration, ConfigurationParameter parameter, ConfigurationParameterValue value, string valueId)
			{
				return BuildRow(version, version.Identifier, "Parameter", null, null, null, null, false, configuration.MandatoryAtService, parameter, value, null, valueId, configuration.Identifier);
			}

		private static GQIRow BuildSpecificationProfileRow(ServiceConfigurationVersion version, Models.ServiceSpecificationProfile configuration, Profile profile, ProfileDefinition definition)
			{
				return BuildRow(version, version.Identifier, "Profile", configuration.Identifier, profile.Name, definition?.Identifier, definition?.Name, profile.IsReusable, configuration.MandatoryAtService, null, null, null, null, null);
			}

		private static GQIRow BuildSpecificationProfileParameterRow(ServiceConfigurationVersion version, Models.ServiceSpecificationProfile configuration, Profile profile, ProfileDefinition definition, ConfigurationParameter parameter, ConfigurationParameterValue value, string valueId)
			{
				return BuildRow(version, version.Identifier, "ProfileParameter", configuration.Identifier, profile.Name, definition?.Identifier, definition?.Name, profile.IsReusable, configuration.MandatoryAtService, parameter, value, null, valueId, null);
			}
		private static GQIRow BuildRow(
			ServiceConfigurationVersion version,
			string activeVersionId,
			string recordType,
			string profileId,
			string profileName,
			string profileDefinitionId,
			string profileDefinitionName,
			bool profileReusable,
			bool profileMandatory,
			ConfigurationParameter parameter,
			ConfigurationParameterValue parameterValue,
			Models.ServiceConfigurationValue serviceConfigurationValue,
			string parameterValueId,
			string unused)
		{
			string value = parameterValue == null
				? String.Empty
				: parameterValue.DoubleValue.HasValue
					? parameterValue.DoubleValue.Value.ToString(CultureInfo.InvariantCulture)
					: parameterValue.StringValue ?? String.Empty;
			string rowId = $"{version.Identifier}:{parameterValueId ?? "version"}";
			return new GQIRow(
				rowId,
				new[]
				{
					new GQICell { Value = version.Identifier },
					new GQICell { Value = version.VersionName ?? String.Empty },
					new GQICell { Value = version.Description ?? String.Empty },
					new GQICell { Value = version.StartDate?.ToUniversalTime() },
					new GQICell { Value = version.EndDate?.ToUniversalTime() },
					new GQICell { Value = String.Equals(version.Identifier, activeVersionId, StringComparison.OrdinalIgnoreCase) },
					new GQICell { Value = recordType },
					new GQICell { Value = serviceConfigurationValue?.Identifier ?? unused ?? String.Empty },
					new GQICell { Value = parameter?.Identifier ?? String.Empty },
					new GQICell { Value = parameterValueId ?? String.Empty },
					new GQICell { Value = parameter?.Name ?? String.Empty },
					new GQICell { Value = parameter?.Type.ToString() ?? String.Empty },
					new GQICell { Value = value },
					new GQICell { Value = serviceConfigurationValue?.Mandatory ?? profileMandatory },
					new GQICell { Value = profileId ?? String.Empty },
					new GQICell { Value = profileName ?? String.Empty },
					new GQICell { Value = profileDefinitionId ?? String.Empty },
					new GQICell { Value = profileDefinitionName ?? String.Empty },
					new GQICell { Value = profileReusable },
					new GQICell { Value = profileMandatory },
					new GQICell { Value = String.Empty },
					new GQICell { Value = String.Empty },
					new GQICell { Value = String.Empty },
					new GQICell { Value = String.Empty },
				});
		}

		private static GQIRow BuildAvailableParameterRow(ConfigurationParameter parameter)
		{
			return BuildRow(
				new ServiceConfigurationVersion { Identifier = String.Empty },
				String.Empty,
				"AvailableParameter",
				null,
				null,
				null,
				null,
				false,
				false,
				parameter,
				null,
				null,
				null,
				null);
		}

		private static GQIRow BuildAvailableProfileRow(ProfileDefinition definition)
		{
			return BuildRow(
				new ServiceConfigurationVersion { Identifier = String.Empty },
				String.Empty,
				"AvailableProfile",
				null,
				null,
				definition.Identifier,
				definition.Name,
				false,
				false,
				null,
				null,
				null,
				null,
				null);
		}

		private static GQIRow BuildAvailableNestedProfileRow(Profile profile, IReadOnlyDictionary<string, ProfileDefinition> definitions)
		{
			definitions.TryGetValue(profile.ProfileDefinitionId.Identifier, out var definition);
			return BuildNestedProfileRow(
				new ServiceConfigurationVersion { Identifier = String.Empty },
				String.Empty,
				String.Empty,
				String.Empty,
				profile,
				definition,
				null,
				null,
				"AvailableNestedProfile");
		}

		private void AppendNestedProfileRows(
			List<GQIRow> rows,
			ServiceConfigurationVersion version,
			string activeVersionId,
			string parentProfileId,
			string parentProfileName,
			Profile parentProfile,
			Dictionary<string, Profile> profiles,
			IReadOnlyDictionary<string, ProfileDefinition> definitions,
			IReadOnlyDictionary<string, ConfigurationParameter> parameters,
			IReadOnlyDictionary<string, ConfigurationParameterValue> parameterValues,
			HashSet<string> availableNestedProfileIds,
			HashSet<string> ancestors)
		{
			foreach (var nestedReference in parentProfile.Profiles ?? new List<Skyline.DataMiner.SDM.SdmObjectReference<Profile>>())
			{
				if (String.IsNullOrWhiteSpace(nestedReference.Identifier))
				{
					throw new InvalidOperationException($"Profile '{parentProfile.Identifier}' contains a nested profile reference without an identifier.");
				}

				if (!profiles.TryGetValue(nestedReference.Identifier, out var nestedProfile))
				{
					nestedProfile = _api.ServiceCatalog.Profiles
						.Read(ProfileExposers.Identifier.Equal(nestedReference.Identifier))
						.FirstOrDefault() ?? throw new InvalidOperationException($"Nested profile '{nestedReference.Identifier}' referenced by profile '{parentProfile.Identifier}' was not found.");
					profiles[nestedProfile.Identifier] = nestedProfile;
				}

				if (nestedProfile.IsReusable && availableNestedProfileIds.Add(nestedProfile.Identifier))
				{
					rows.Add(BuildAvailableNestedProfileRow(nestedProfile, definitions));
				}

				definitions.TryGetValue(nestedProfile.ProfileDefinitionId.Identifier ?? String.Empty, out var definition);
				definitions.TryGetValue(parentProfile.ProfileDefinitionId.Identifier ?? String.Empty, out var parentDefinition);
				if (!ancestors.Add(nestedProfile.Identifier))
				{
					continue;
				}
				rows.Add(BuildNestedProfileRow(version, activeVersionId, parentProfileId, parentProfileName, nestedProfile, definition, parentProfile, parentDefinition));

				var nestedParameters = nestedProfile.ConfigurationParameterValues ?? new List<Skyline.DataMiner.SDM.SdmObjectReference<ConfigurationParameterValue>>();
				foreach (var parameterReference in nestedParameters)
				{
					if (!parameterValues.TryGetValue(parameterReference.Identifier, out var value))
					{
						continue;
					}

					string parameterId = value.ConfigurationParameterId.Identifier ?? String.Empty;
					parameters.TryGetValue(parameterId, out var parameter);
					rows.Add(BuildNestedProfileParameterRow(version, activeVersionId, parentProfileId, parentProfileName, parentDefinition, parentProfile.IsReusable, nestedProfile, definition, parameter, value, parameterReference.Identifier));
				}

				AppendNestedProfileRows(
					rows,
					version,
					activeVersionId,
					nestedProfile.Identifier,
					nestedProfile.Name,
					nestedProfile,
					profiles,
					definitions,
					parameters,
					parameterValues,
					availableNestedProfileIds,
					ancestors);
				ancestors.Remove(nestedProfile.Identifier);
			}
		}

		private static GQIRow BuildNestedProfileParameterRow(
			ServiceConfigurationVersion version,
			string activeVersionId,
			string parentProfileId,
			string parentProfileName,
			ProfileDefinition parentDefinition,
			bool parentReusable,
			Profile nestedProfile,
			ProfileDefinition definition,
			ConfigurationParameter parameter,
			ConfigurationParameterValue value,
			string valueId)
		{
			string parameterText = value.DoubleValue.HasValue
				? value.DoubleValue.Value.ToString(CultureInfo.InvariantCulture)
				: value.StringValue ?? String.Empty;
			return new GQIRow(
				$"{version.Identifier}:{parentProfileId}:{nestedProfile.Identifier}:{valueId}:NestedProfileParameter",
				new[]
				{
					new GQICell { Value = version.Identifier },
					new GQICell { Value = version.VersionName ?? String.Empty },
					new GQICell { Value = version.Description ?? String.Empty },
					new GQICell { Value = version.StartDate?.ToUniversalTime() },
					new GQICell { Value = version.EndDate?.ToUniversalTime() },
					new GQICell { Value = String.Equals(version.Identifier, activeVersionId, StringComparison.OrdinalIgnoreCase) },
					new GQICell { Value = "NestedProfileParameter" },
					new GQICell { Value = String.Empty },
					new GQICell { Value = parameter?.Identifier ?? String.Empty },
					new GQICell { Value = valueId ?? String.Empty },
					new GQICell { Value = parameter?.Name ?? String.Empty },
					new GQICell { Value = parameter?.Type.ToString() ?? String.Empty },
					new GQICell { Value = parameterText },
					new GQICell { Value = false },
					new GQICell { Value = parentProfileId ?? String.Empty },
					new GQICell { Value = parentProfileName ?? String.Empty },
					new GQICell { Value = parentDefinition?.Identifier ?? String.Empty },
					new GQICell { Value = parentDefinition?.Name ?? String.Empty },
					new GQICell { Value = parentReusable },
					new GQICell { Value = false },
					new GQICell { Value = nestedProfile.Identifier },
					new GQICell { Value = nestedProfile.Name ?? String.Empty },
					new GQICell { Value = definition?.Identifier ?? String.Empty },
					new GQICell { Value = definition?.Name ?? String.Empty },
				});
		}

		private static GQIRow BuildNestedProfileRow(
			ServiceConfigurationVersion version,
			string activeVersionId,
			string parentProfileId,
			string parentProfileName,
			Profile nestedProfile,
			ProfileDefinition definition,
			Profile parentProfile = null,
			ProfileDefinition parentDefinition = null,
			string recordType = "NestedProfile")
		{
			return new GQIRow(
				$"{version.Identifier}:{parentProfileId}:{nestedProfile.Identifier}:{recordType}",
				new[]
				{
					new GQICell { Value = version.Identifier },
					new GQICell { Value = version.VersionName ?? String.Empty },
					new GQICell { Value = version.Description ?? String.Empty },
					new GQICell { Value = version.StartDate?.ToUniversalTime() },
					new GQICell { Value = version.EndDate?.ToUniversalTime() },
					new GQICell { Value = String.Equals(version.Identifier, activeVersionId, StringComparison.OrdinalIgnoreCase) },
					new GQICell { Value = recordType },
					new GQICell { Value = String.Empty },
					new GQICell { Value = String.Empty },
					new GQICell { Value = String.Empty },
					new GQICell { Value = String.Empty },
					new GQICell { Value = String.Empty },
					new GQICell { Value = String.Empty },
					new GQICell { Value = false },
					new GQICell { Value = parentProfileId },
					new GQICell { Value = parentProfileName },
					new GQICell { Value = parentDefinition?.Identifier ?? String.Empty },
					new GQICell { Value = parentDefinition?.Name ?? String.Empty },
					new GQICell { Value = String.Equals(recordType, "AvailableNestedProfile", StringComparison.Ordinal) ? nestedProfile.IsReusable : (parentProfile?.IsReusable ?? false) },
					new GQICell { Value = false },
					new GQICell { Value = nestedProfile.Identifier },
					new GQICell { Value = nestedProfile.Name ?? String.Empty },
					new GQICell { Value = definition?.Identifier ?? String.Empty },
					new GQICell { Value = definition?.Name ?? String.Empty },
				});
		}

	}
}

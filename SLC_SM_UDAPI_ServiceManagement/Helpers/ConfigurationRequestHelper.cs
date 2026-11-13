namespace SLC_SM_UDAPI_ServiceManagement.Helpers
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;
	using SLC_SM_UDAPI_ServiceManagement.Exceptions;

	/// <summary>
	/// Shared validation and message helpers for the service and specification configuration writers.
	/// </summary>
	internal static class ConfigurationRequestHelper
	{
		/// <summary>Formats a name for a caller-safe message: the quoted name, or the fallback when there is no name.</summary>
		public static string FormatName(string name, string fallback)
		{
			return String.IsNullOrWhiteSpace(name) ? fallback : $"'{name.Trim()}'";
		}

		/// <summary>Gets the display name of a parameter: its label, else the parameter definition name.</summary>
		public static string ParameterName(string label, ConfigurationParameter definition)
		{
			return String.IsNullOrWhiteSpace(label) ? definition?.Name : label.Trim();
		}

		/// <summary>Returns <paramref name="preferred"/> when it has a value, else <paramref name="fallback"/>.</summary>
		public static string FirstNonEmpty(string preferred, string fallback)
		{
			return String.IsNullOrWhiteSpace(preferred) ? fallback : preferred;
		}

		/// <summary>
		/// Resolves the catalog identifier of a reusable profile request: the explicit reusable profile ID, else the catalog
		/// profile of the saved assignment with the request ID, else the request ID itself.
		/// </summary>
		public static string ResolveReusableProfileId(string reusableProfileId, string id, IReadOnlyDictionary<string, string> assignments)
		{
			if (!String.IsNullOrWhiteSpace(reusableProfileId))
			{
				return reusableProfileId;
			}

			if (!String.IsNullOrWhiteSpace(id) && assignments.TryGetValue(id, out var assignedProfileId))
			{
				return assignedProfileId;
			}

			return id;
		}

		/// <summary>Ensures that a reusable profile is assigned at most once among sibling profile requests.</summary>
		public static void EnsureUniqueReusableProfiles<T>(
			IEnumerable<T> profiles,
			Func<T, bool> isReusable,
			Func<T, string> resolveReusableId,
			Func<T, string> name)
			where T : class
		{
			var reusableIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (var profile in profiles ?? Enumerable.Empty<T>())
			{
				if (profile == null || !isReusable(profile))
				{
					continue;
				}

				string reusableId = resolveReusableId(profile);
				if (!String.IsNullOrWhiteSpace(reusableId) && !reusableIds.Add(reusableId))
				{
					throw new InvalidConfigurationException(
						$"Reusable profile {FormatName(name(profile), "the selected reusable profile")} is already assigned at this level.",
						$"Duplicate reusable profile '{reusableId}'.");
				}
			}
		}

		/// <summary>Ensures that a reusable profile ID occurs at most once in a list of sibling nested profile IDs.</summary>
		public static void EnsureUniqueReusableIds(IEnumerable<string> profileIds, ISet<string> reusableProfileIds)
		{
			var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (var profileId in profileIds ?? Enumerable.Empty<string>())
			{
				if (String.IsNullOrWhiteSpace(profileId) || !reusableProfileIds.Contains(profileId))
				{
					continue;
				}

				if (!seen.Add(profileId))
				{
					throw new InvalidConfigurationException(
						"A reusable profile is assigned more than once at this level.",
						$"Duplicate reusable profile '{profileId}'.");
				}
			}
		}

		/// <summary>Reads a reusable catalog profile.</summary>
		/// <exception cref="InvalidConfigurationException">The profile does not exist or is not reusable.</exception>
		public static Profile ReadReusableProfile(IServiceManagementApiHelper api, string id, string kind, string name)
		{
			var profile = String.IsNullOrWhiteSpace(id)
				? null
				: api.ServiceCatalog.Profiles.Read(ProfileExposers.Identifier.Equal(id)).FirstOrDefault();
			if (profile == null || !profile.IsReusable)
			{
				throw new InvalidConfigurationException(
					$"{kind} {FormatName(name, "the selected profile")} was not found.",
					$"{kind} '{id}' was not found or is not reusable.");
			}

			return profile;
		}
	}
}

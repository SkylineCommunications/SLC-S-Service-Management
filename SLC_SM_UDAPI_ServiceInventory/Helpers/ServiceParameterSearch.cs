namespace SLC_SM_UDAPI_ServiceInventory.Helpers
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	using SLC_SM_UDAPI_ServiceInventory.Exceptions;
	using SLC_SM_UDAPI_ServiceInventory.Models;

	/// <summary>
	/// Finds the services matching a <see cref="ListServicesQuery"/>.
	/// Parameter criteria are resolved bottom-up: matching parameter values, then the profiles (and their parents)
	/// and standalone values referencing them, the service profiles, the configuration versions and finally the services
	/// whose current version contains every criterion.
	/// </summary>
	public sealed class ServiceParameterSearch
	{
		private readonly IServiceSearchDataSource dataSource;

		/// <summary>
		/// Initializes a new instance of the <see cref="ServiceParameterSearch"/> class.
		/// </summary>
		/// <param name="dataSource">The data source.</param>
		public ServiceParameterSearch(IServiceSearchDataSource dataSource)
		{
			this.dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
		}

		/// <summary>
		/// Finds the services matching the query. <c>organizationId</c>, <c>fields</c>, <c>limit</c> and <c>offset</c> are not applied.
		/// </summary>
		/// <param name="query">The query.</param>
		/// <returns>The matching services, sorted when requested.</returns>
		/// <exception cref="InvalidQueryException">The query is invalid.</exception>
		public List<Service> Find(ListServicesQuery query)
		{
			if (query == null)
			{
				throw new ArgumentNullException(nameof(query));
			}

			var criteria = ParameterCriterion.FromQuery(query.Parameters);
			var comparer = CreateSortComparer(query.Sort);

			var requestedDefinitions = (query.ProfileDefinitions ?? new List<string>())
				.Where(d => !String.IsNullOrWhiteSpace(d))
				.Select(d => d.Trim())
				.ToList();

			// 1. Bottom-up: configuration versions containing every parameter criterion and requested definition.
			HashSet<string> matchingVersionIds = null;
			if (criteria.Count > 0 || requestedDefinitions.Count > 0)
			{
				matchingVersionIds = FindMatchingVersionIds(criteria, requestedDefinitions);
				if (matchingVersionIds.Count == 0)
				{
					return new List<Service>();
				}
			}

			// 2. Services whose CURRENT version matches (read on the server when narrowed), plus the service-level filters.
			var candidates = matchingVersionIds == null
				? dataSource.GetServices()
				: dataSource.GetServicesByConfigurationVersion(matchingVersionIds);

			var services = candidates
				.Where(s => s != null
					&& (matchingVersionIds == null || matchingVersionIds.Contains(s.ServiceConfigurationId.Identifier ?? String.Empty))
					&& MatchesServiceFields(s, query))
				.ToList();

			if (comparer != null)
			{
				services.Sort(comparer);
			}

			return services;
		}

		private static bool MatchesServiceFields(Service service, ListServicesQuery query)
		{
			if (!String.IsNullOrWhiteSpace(query.Name)
				&& (service.Name ?? String.Empty).IndexOf(query.Name.Trim(), StringComparison.OrdinalIgnoreCase) < 0)
			{
				return false;
			}

			if (!IdMatches(query.CategoryId, service.CategoryId.Identifier)
				|| !IdMatches(query.ServiceSpecificationId, service.ServiceSpecificationId.Identifier))
			{
				return false;
			}

			var statuses = (query.Statuses ?? new List<string>()).Where(s => !String.IsNullOrWhiteSpace(s)).ToList();
			if (statuses.Count > 0
				&& !statuses.Any(s => String.Equals(s.Trim(), service.Status.ToString(), StringComparison.OrdinalIgnoreCase)))
			{
				return false;
			}

			// Overlap with the requested window; a missing service start/end is treated as open-ended.
			if (query.StartDate.HasValue && service.EndTime.HasValue
				&& service.EndTime.Value.ToUniversalTime() < FromUnixSeconds(query.StartDate.Value))
			{
				return false;
			}

			if (query.EndDate.HasValue && service.StartTime.HasValue
				&& service.StartTime.Value.ToUniversalTime() > FromUnixSeconds(query.EndDate.Value))
			{
				return false;
			}

			return true;
		}

		private static bool IdMatches(string expected, string actual)
		{
			return String.IsNullOrWhiteSpace(expected) || String.Equals(expected.Trim(), actual, StringComparison.OrdinalIgnoreCase);
		}

		private static DateTime FromUnixSeconds(long seconds)
		{
			try
			{
				return DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime;
			}
			catch (ArgumentOutOfRangeException)
			{
				throw new InvalidQueryException("'startDate' and 'endDate' must be valid Unix timestamps in seconds.");
			}
		}

		private static Comparison<Service> CreateSortComparer(string sort)
		{
			if (String.IsNullOrWhiteSpace(sort))
			{
				return null;
			}

			var key = sort.Trim();
			bool descending = key.StartsWith("-", StringComparison.Ordinal);
			key = key.TrimStart('-', '+');

			Comparison<Service> comparison;
			switch (key.ToLowerInvariant())
			{
				case "name":
					comparison = (a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name);
					break;

				case "id":
					comparison = (a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Identifier, b.Identifier);
					break;

				case "serviceid":
					comparison = (a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.ServiceID, b.ServiceID);
					break;

				case "status":
					comparison = (a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Status.ToString(), b.Status.ToString());
					break;

				case "startdate":
				case "starttime":
					comparison = (a, b) => Nullable.Compare(a.StartTime, b.StartTime);
					break;

				case "enddate":
				case "endtime":
					comparison = (a, b) => Nullable.Compare(a.EndTime, b.EndTime);
					break;

				default:
					throw new InvalidQueryException($"Unsupported 'sort' value '{key}'. Use name, id, serviceId, status, startDate or endDate, optionally prefixed with '-'.");
			}

			return descending ? (a, b) => comparison(b, a) : comparison;
		}

		private static bool Intersect(ref HashSet<string> accumulator, IEnumerable<string> ids)
		{
			if (accumulator == null)
			{
				accumulator = new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase);
			}
			else
			{
				accumulator.IntersectWith(ids);
			}

			return accumulator.Count > 0;
		}

		private HashSet<string> FindMatchingVersionIds(IReadOnlyList<ParameterCriterion> criteria, IReadOnlyList<string> requestedDefinitions)
		{
			HashSet<string> result = null;

			// Parameters: start from the values that match, then work up to the versions.
			if (criteria.Count > 0)
			{
				var candidates = dataSource.GetConfigurationParameterValues(
					criteria.Where(c => !c.IsIdentifier).Select(c => c.Parameter).ToList(),
					criteria.Where(c => c.IsIdentifier).Select(c => c.Parameter).ToList());

				foreach (var criterion in criteria)
				{
					var valueIds = candidates.Where(criterion.Matches).Select(v => v.Identifier).ToList();
					if (valueIds.Count == 0 || !Intersect(ref result, GetVersionIdsForValues(valueIds)))
					{
						return new HashSet<string>();
					}
				}
			}

			// Profile definitions: when parameters already narrowed the versions, check only those (top-down);
			// otherwise start from the profiles of the definition and work up to the versions.
			if (requestedDefinitions.Count > 0)
			{
				var definitions = dataSource.GetProfileDefinitions();
				var definitionIdsPerRequest = new List<List<string>>();
				foreach (var requested in requestedDefinitions)
				{
					var definitionIds = definitions
						.Where(d => String.Equals(d.Identifier, requested, StringComparison.OrdinalIgnoreCase)
							|| String.Equals(d.Name?.Trim(), requested, StringComparison.OrdinalIgnoreCase))
						.Select(d => d.Identifier)
						.ToList();

					if (definitionIds.Count == 0)
					{
						return new HashSet<string>();
					}

					definitionIdsPerRequest.Add(definitionIds);
				}

				if (result != null)
				{
					result = FilterVersionsOnDefinitions(result, definitionIdsPerRequest);
				}
				else
				{
					foreach (var definitionIds in definitionIdsPerRequest)
					{
						if (!Intersect(ref result, GetVersionIdsForProfiles(dataSource.GetProfileIdsByDefinition(definitionIds), new List<string>())))
						{
							return new HashSet<string>();
						}
					}
				}
			}

			return result ?? new HashSet<string>();
		}

		private static List<string> ReferenceIds<T>(IEnumerable<Skyline.DataMiner.SDM.SdmObjectReference<T>> references)
			where T : Skyline.DataMiner.SDM.SdmObject<T>
		{
			return (references ?? Enumerable.Empty<Skyline.DataMiner.SDM.SdmObjectReference<T>>())
				.Select(r => r.Identifier)
				.Where(id => !String.IsNullOrEmpty(id))
				.ToList();
		}

		private HashSet<string> FilterVersionsOnDefinitions(HashSet<string> versionIds, IReadOnlyList<List<string>> definitionIdsPerRequest)
		{
			var versions = dataSource.GetServiceConfigurationVersions(versionIds.ToList());
			var serviceProfiles = dataSource.GetServiceProfiles(versions.SelectMany(v => ReferenceIds(v.Profiles)).ToList())
				.ToDictionary(sp => sp.Identifier, StringComparer.OrdinalIgnoreCase);

			// Load the (nested) profile trees of the candidate versions, level by level.
			var profiles = new Dictionary<string, Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations.Profile>(StringComparer.OrdinalIgnoreCase);
			var level = serviceProfiles.Values.Select(sp => sp.ProfileId.Identifier).Where(id => !String.IsNullOrEmpty(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
			while (level.Count > 0)
			{
				var loaded = dataSource.GetProfiles(level);
				foreach (var profile in loaded)
				{
					profiles[profile.Identifier] = profile;
				}

				level = loaded.SelectMany(p => ReferenceIds(p.Profiles)).Where(id => !profiles.ContainsKey(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
			}

			var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (var version in versions)
			{
				var presentDefinitions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				var pending = new Stack<string>();

				foreach (var serviceProfileId in ReferenceIds(version.Profiles))
				{
					if (serviceProfiles.TryGetValue(serviceProfileId, out var serviceProfile))
					{
						presentDefinitions.Add(serviceProfile.ProfileDefinitionId.Identifier ?? String.Empty);
						pending.Push(serviceProfile.ProfileId.Identifier);
					}
				}

				var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				while (pending.Count > 0)
				{
					var id = pending.Pop();
					if (String.IsNullOrEmpty(id) || !visited.Add(id) || !profiles.TryGetValue(id, out var profile))
					{
						continue;
					}

					presentDefinitions.Add(profile.ProfileDefinitionId.Identifier ?? String.Empty);
					foreach (var childId in ReferenceIds(profile.Profiles))
					{
						pending.Push(childId);
					}
				}

				if (definitionIdsPerRequest.All(ids => ids.Any(presentDefinitions.Contains)))
				{
					result.Add(version.Identifier);
				}
			}

			return result;
		}

		private IReadOnlyList<string> GetVersionIdsForValues(IReadOnlyCollection<string> parameterValueIds)
		{
			var profileIds = dataSource.GetProfileIdsContainingValues(parameterValueIds);
			var standaloneIds = dataSource.GetServiceConfigurationValueIds(parameterValueIds);

			return GetVersionIdsForProfiles(profileIds, standaloneIds);
		}

		private IReadOnlyList<string> GetVersionIdsForProfiles(IReadOnlyCollection<string> profileIds, IReadOnlyCollection<string> standaloneIds)
		{
			var allProfileIds = WithAncestors(profileIds);
			var serviceProfileIds = allProfileIds.Count > 0
				? dataSource.GetServiceProfileIds(allProfileIds)
				: new List<string>();

			if (serviceProfileIds.Count == 0 && standaloneIds.Count == 0)
			{
				return new List<string>();
			}

			return dataSource.GetServiceConfigurationVersionIds(serviceProfileIds, standaloneIds);
		}

		private HashSet<string> WithAncestors(IEnumerable<string> profileIds)
		{
			var result = new HashSet<string>(profileIds, StringComparer.OrdinalIgnoreCase);
			var currentLevel = result.ToList();

			while (currentLevel.Count > 0)
			{
				// HashSet.Add returns false for already visited profiles, which also protects against cycles.
				currentLevel = dataSource.GetParentProfileIds(currentLevel).Where(result.Add).ToList();
			}

			return result;
		}
	}
}
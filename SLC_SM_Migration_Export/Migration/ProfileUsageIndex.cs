namespace SLCSMMigrationExport.Migration
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	/// <summary>
	/// Finds, for every exported profile, the services and specifications that use it (directly or as a nested profile) in the legacy model.
	/// </summary>
	public sealed class ProfileUsageIndex
	{
		private static readonly Guid ServiceDefinition = new Guid("d50bac89-6629-4f3b-aff0-05a57cb7e70d");
		private static readonly Guid ServiceName = new Guid("7a8c2a26-016d-4067-bf60-7b2ff07157cb");
		private static readonly Guid ServiceActiveVersion = new Guid("f50b9d0a-009c-4ce5-bc08-5188784d7994");
		private static readonly Guid ServiceConfigurationVersions = new Guid("2aafde72-b16e-4b66-9a80-fbd85655ad45");

		private static readonly Guid VersionDefinition = new Guid("42e3f398-b966-41fa-96c7-a7a0189f0aab");
		private static readonly Guid VersionName = new Guid("e91a0f81-0c37-48e5-a00a-5f4d35dcfa10");
		private static readonly Guid VersionProfileLinks = new Guid("6a6e5360-dc29-4e16-9874-d56a584df9e0");
		private static readonly Guid VersionLinkProfile = new Guid("a163544f-d48a-42fe-916e-5f41a9f3d15e");

		private static readonly Guid SpecificationDefinition = new Guid("dbe96de4-ca9d-4eba-a06e-b089ec14ffca");
		private static readonly Guid SpecificationName = new Guid("08908eda-eb78-4d7f-a462-71769da596a3");
		private static readonly Guid SpecificationProfileLinks = new Guid("7038079c-a9ef-4f0b-8f43-798460315d7f");
		private static readonly Guid SpecificationLinkProfile = new Guid("25e8b1c8-5ab5-43b3-9426-73be75a3bd3c");

		private static readonly Guid NestedProfiles = new Guid("ccc27894-b87d-4d48-bee0-966bd0a77381");

		private readonly Dictionary<Guid, string> _profileNames = new Dictionary<Guid, string>();
		private readonly Dictionary<Guid, HashSet<Guid>> _parents = new Dictionary<Guid, HashSet<Guid>>();
		private readonly Dictionary<Guid, HashSet<string>> _services = new Dictionary<Guid, HashSet<string>>();
		private readonly Dictionary<Guid, HashSet<string>> _specifications = new Dictionary<Guid, HashSet<string>>();
		private readonly HashSet<Guid> _inVersionWithoutService = new HashSet<Guid>();

		public ProfileUsageIndex(MigrationExport export, Guid profileDefinition, Guid profileNameField)
		{
			var instances = new Dictionary<Guid, ExportedInstance>();
			foreach (var instance in export.Modules.SelectMany(m => m.Instances).Where(i => i != null))
			{
				instances[instance.Id] = instance;
			}

			foreach (var profile in instances.Values.Where(i => i.DefinitionId == profileDefinition))
			{
				_profileNames[profile.Id] = Value(profile, profileNameField) ?? profile.Name;
				foreach (Guid child in GuidValues(profile, NestedProfiles))
				{
					Add(_parents, child, profile.Id);
				}
			}

			// Per version: "<service> [<version name>, active|inactive]".
			var versionServices = new Dictionary<Guid, HashSet<string>>();
			foreach (var service in instances.Values.Where(i => i.DefinitionId == ServiceDefinition))
			{
				string name = Value(service, ServiceName) ?? service.Name;
				var activeVersion = GuidValues(service, ServiceActiveVersion).FirstOrDefault();
				foreach (Guid version in GuidValues(service, ServiceConfigurationVersions).Concat(new[] { activeVersion }).Where(v => v != Guid.Empty).Distinct())
				{
					string versionName = instances.TryGetValue(version, out var versionInstance) ? Value(versionInstance, VersionName) ?? versionInstance.Name : version.ToString();
					Add(versionServices, version, $"{name} [{versionName}, {(version == activeVersion ? "active" : "inactive")}]");
				}
			}

			foreach (var version in instances.Values.Where(i => i.DefinitionId == VersionDefinition))
			{
				versionServices.TryGetValue(version.Id, out var serviceNames);
				foreach (Guid profile in LinkedProfiles(instances, version, VersionProfileLinks, VersionLinkProfile))
				{
					if (serviceNames == null || serviceNames.Count == 0)
					{
						_inVersionWithoutService.Add(profile);
						continue;
					}

					foreach (string serviceName in serviceNames)
					{
						Add(_services, profile, serviceName);
					}
				}
			}

			foreach (var specification in instances.Values.Where(i => i.DefinitionId == SpecificationDefinition))
			{
				string name = Value(specification, SpecificationName) ?? specification.Name;
				foreach (Guid profile in LinkedProfiles(instances, specification, SpecificationProfileLinks, SpecificationLinkProfile))
				{
					Add(_specifications, profile, name);
				}
			}
		}

		public ProfileUsage GetUsage(Guid profileId)
		{
			var services = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
			var specifications = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
			bool inVersionWithoutService = false;

			// The profile itself and every profile it is nested in (guarded against cycles).
			var visited = new HashSet<Guid>();
			var pending = new Stack<Guid>();
			pending.Push(profileId);
			while (pending.Count > 0)
			{
				Guid id = pending.Pop();
				if (!visited.Add(id))
				{
					continue;
				}

				if (_services.TryGetValue(id, out var serviceNames))
				{
					// A nested profile is used through the profile it is nested in.
					string via = id == profileId ? String.Empty : $" via '{(_profileNames.TryGetValue(id, out var parentName) ? parentName : id.ToString())}'";
					services.UnionWith(serviceNames.Select(s => s + via));
				}

				if (_specifications.TryGetValue(id, out var specificationNames))
				{
					specifications.UnionWith(specificationNames);
				}

				inVersionWithoutService |= _inVersionWithoutService.Contains(id);

				if (_parents.TryGetValue(id, out var parents))
				{
					foreach (Guid parent in parents)
					{
						pending.Push(parent);
					}
				}
			}

			return new ProfileUsage(services.ToList(), specifications.ToList(), inVersionWithoutService);
		}

		private static IEnumerable<Guid> LinkedProfiles(Dictionary<Guid, ExportedInstance> instances, ExportedInstance container, Guid linksField, Guid linkProfileField)
		{
			foreach (Guid linkId in GuidValues(container, linksField))
			{
				if (instances.TryGetValue(linkId, out var link))
				{
					foreach (Guid profile in GuidValues(link, linkProfileField))
					{
						yield return profile;
					}
				}
			}
		}

		private static IEnumerable<string> Values(ExportedInstance instance, Guid fieldId)
		{
			return instance.Sections
				.SelectMany(s => s.Fields)
				.Where(f => f.FieldId == fieldId)
				.SelectMany(f => f.Values ?? new List<string>());
		}

		private static string Value(ExportedInstance instance, Guid fieldId) => Values(instance, fieldId).FirstOrDefault();

		private static IEnumerable<Guid> GuidValues(ExportedInstance instance, Guid fieldId)
		{
			foreach (string value in Values(instance, fieldId))
			{
				if (Guid.TryParse(value, out Guid id) && id != Guid.Empty)
				{
					yield return id;
				}
			}
		}

		private static void Add<T>(Dictionary<Guid, HashSet<T>> map, Guid key, T value)
		{
			if (!map.TryGetValue(key, out var set))
			{
				set = new HashSet<T>();
				map[key] = set;
			}

			set.Add(value);
		}
	}

	public sealed class ProfileUsage
	{
		public ProfileUsage(List<string> services, List<string> specifications, bool inVersionWithoutService)
		{
			Services = services;
			Specifications = specifications;
			InVersionWithoutService = inVersionWithoutService;
		}

		public List<string> Services { get; }

		public List<string> Specifications { get; }

		/// <summary>
		/// Gets a value indicating whether the profile is in a configuration version that no service references.
		/// </summary>
		public bool InVersionWithoutService { get; }

		public string Status
		{
			get
			{
				if (Services.Count > 0)
				{
					return "Service";
				}

				if (Specifications.Count > 0)
				{
					return "Specification only";
				}

				return InVersionWithoutService ? "Orphan (configuration version without service)" : "Orphan (not linked)";
			}
		}

		public bool IsOrphan => Services.Count == 0 && Specifications.Count == 0;
	}
}

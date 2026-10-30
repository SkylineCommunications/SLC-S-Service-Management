namespace SLCSMMigrationImport.Migration
{
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;

	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.Sections;

	/// <summary>
	/// Maps the legacy export onto the new DOM model and decides, per instance, what has to happen on the target.
	/// Does not write anything.
	/// </summary>
	public sealed class MigrationPlanner
	{
		private readonly IReadOnlyDictionary<string, TargetModuleSchema> _schemas;
		private readonly ITargetLookup _lookup;
		private readonly IEnvironmentChecks _environment;

		private readonly HashSet<InstanceKey> _exported = new HashSet<InstanceKey>();
		private readonly Dictionary<InstanceKey, string> _dropped = new Dictionary<InstanceKey, string>();
		private readonly Dictionary<Guid, string> _legacySectionNames = new Dictionary<Guid, string>();
		private readonly Dictionary<Guid, Dictionary<Guid, string>> _legacyFields = new Dictionary<Guid, Dictionary<Guid, string>>();
		private readonly Dictionary<Guid, string> _legacyDefinitionNames = new Dictionary<Guid, string>();
		private readonly Dictionary<Guid, Dictionary<Guid, string>> _nestedProfileLinksPerOwner = new Dictionary<Guid, Dictionary<Guid, string>>();
		private readonly Dictionary<Guid, string> _nestedProfileLinkInstances = new Dictionary<Guid, string>();

		public MigrationPlanner(IReadOnlyDictionary<string, TargetModuleSchema> schemas, ITargetLookup lookup, IEnvironmentChecks environment)
		{
			_schemas = schemas ?? throw new ArgumentNullException(nameof(schemas));
			_lookup = lookup ?? throw new ArgumentNullException(nameof(lookup));
			_environment = environment ?? throw new ArgumentNullException(nameof(environment));
		}

		public MigrationPlan Plan(MigrationExport export)
		{
			var plan = new MigrationPlan();
			var modules = SelectModules(export, plan);
			if (plan.FatalErrors.Count > 0)
			{
				return plan;
			}

			IndexLegacySchema(modules);
			IndexNestedProfileLinks(modules);

			var items = new List<PlannedItem>();
			var toBuild = new List<Tuple<ExportedModule, ExportedInstance, PlannedItem>>();

			// Pass 1: identify instances whose definition no longer exists, so references to them can be cleaned up.
			foreach (var module in modules)
			{
				var schema = _schemas[module.ModuleId];
				var seen = new HashSet<Guid>();
				foreach (var instance in module.Instances.Where(i => i != null))
				{
					var item = new PlannedItem(module.ModuleId, instance, GetDefinitionName(schema, instance.DefinitionId));
					items.Add(item);

					if (!seen.Add(instance.Id))
					{
						item.Fail("The export contains this instance ID more than once; only the first occurrence is imported.");
						continue;
					}

					_exported.Add(item.Key);
					if (schema.GetDefinition(instance.DefinitionId) == null || KnownIds.RemovedDefinitions.ContainsKey(instance.DefinitionId))
					{
						PlanDrop(item, instance);
						continue;
					}

					if (_nestedProfileLinkInstances.TryGetValue(instance.Id, out string nestedReason))
					{
						PlanDrop(item, instance, nestedReason);
						continue;
					}

					toBuild.Add(Tuple.Create(module, instance, item));
				}
			}

			foreach (var group in toBuild.GroupBy(t => t.Item1.ModuleId))
			{
				_lookup.Prefetch(group.Key, group.Select(t => t.Item2.Id));
			}

			// Pass 2: build the desired new-model instances.
			foreach (var entry in toBuild)
			{
				Build(_schemas[entry.Item1.ModuleId], entry.Item2, entry.Item3);
			}

			plan.Items.AddRange(OrderByReferences(items));
			return plan;
		}

		/// <summary>
		/// Builds a canonical representation of the field values of an instance, ignoring section IDs, empty sections and legacy parts removed by <see cref="ObsoleteDomCleanup"/>.
		/// </summary>
		public static string Canonical(DomInstance instance)
		{
			if (instance?.Sections == null)
			{
				return String.Empty;
			}

			Func<FieldValue, bool> isCompared = f => f.Value?.Value != null && !KnownIds.RemovedFields.ContainsKey(f.FieldDescriptorID.Id);
			var sections = instance.Sections
				.Where(s => s != null && !(s.SectionDefinitionID != null && KnownIds.RemovedSections.ContainsKey(s.SectionDefinitionID.Id)) && s.FieldValues.Any(isCompared))
				.Select(s => s.SectionDefinitionID?.Id.ToString("D") + "{" + String.Join(";", s.FieldValues
					.Where(isCompared)
					.OrderBy(f => f.FieldDescriptorID.Id)
					.Select(f => f.FieldDescriptorID.Id.ToString("D") + "=" + ValueCodec.Canonical(f.Value))) + "}")
				.OrderBy(s => s, StringComparer.Ordinal);

			return String.Join("|", sections);
		}

		private static IEnumerable<PlannedItem> OrderByReferences(List<PlannedItem> items)
		{
			// Stable topological sort: referenced instances are written before the instances referring to them.
			var byKey = new Dictionary<InstanceKey, PlannedItem>();
			foreach (var item in items.Where(i => !byKey.ContainsKey(i.Key)))
			{
				byKey[item.Key] = item;
			}

			var result = new List<PlannedItem>(items.Count);
			var visited = new HashSet<PlannedItem>();
			foreach (var item in items)
			{
				Visit(item, byKey, visited, result);
			}

			return result;
		}

		private static void Visit(PlannedItem item, Dictionary<InstanceKey, PlannedItem> byKey, HashSet<PlannedItem> visited, List<PlannedItem> result)
		{
			// Marking before recursing breaks reference cycles; the order stays deterministic.
			if (!visited.Add(item))
			{
				return;
			}

			foreach (var reference in item.References)
			{
				if (byKey.TryGetValue(reference, out var dependency))
				{
					Visit(dependency, byKey, visited, result);
				}
			}

			result.Add(item);
		}

		private static string Describe(ExportedField field)
		{
			var values = field?.Values ?? new List<string>();
			return field != null && field.IsList ? "[" + String.Join(", ", values) + "]" : String.Join(", ", values);
		}

		private static bool HasValue(ExportedField field)
		{
			return field?.Values != null && field.Values.Any(v => v != null);
		}

		private static IEnumerable<FieldValue> AllFieldValues(DomInstance instance, Guid sectionDefinitionId)
		{
			return instance.Sections
				.Where(s => s.SectionDefinitionID?.Id == sectionDefinitionId)
				.SelectMany(s => s.FieldValues);
		}

		private List<ExportedModule> SelectModules(MigrationExport export, MigrationPlan plan)
		{
			var modules = new List<ExportedModule>();
			if (export == null)
			{
				plan.FatalErrors.Add("The export file is empty or could not be read.");
				return modules;
			}

			if (export.FormatVersion != MigrationExport.CurrentFormatVersion)
			{
				plan.FatalErrors.Add($"Unsupported export format version {export.FormatVersion} (expected {MigrationExport.CurrentFormatVersion}). Re-export the data with the matching SLC_SM_Migration_Export script.");
				return modules;
			}

			var exported = (export.Modules ?? new List<ExportedModule>()).Where(m => m != null && !String.IsNullOrWhiteSpace(m.ModuleId)).ToList();
			foreach (var ignored in exported.Where(m => !KnownIds.ModulesInScope.Contains(m.ModuleId, StringComparer.OrdinalIgnoreCase)))
			{
				plan.Warnings.Add($"Module '{ignored.ModuleId}' is not in scope of the SMG migration and is ignored ({ignored.Instances?.Count ?? 0} instances).");
			}

			foreach (string moduleId in KnownIds.ModulesInScope)
			{
				var matches = exported.Where(m => String.Equals(m.ModuleId, moduleId, StringComparison.OrdinalIgnoreCase)).ToList();
				if (matches.Count == 0)
				{
					plan.FatalErrors.Add($"The export does not contain module '{moduleId}'. The export is incomplete; re-export the data with the SLC_SM_Migration_Export script.");
					continue;
				}

				if (matches.Count > 1)
				{
					plan.FatalErrors.Add($"The export contains module '{moduleId}' more than once.");
					continue;
				}

				if (!_schemas.TryGetValue(moduleId, out var schema) || schema == null || schema.IsEmpty)
				{
					plan.FatalErrors.Add($"DOM module '{moduleId}' is not installed on this system (no DOM definitions found). Install the new Service Management package before importing.");
					continue;
				}

				var module = matches[0];
				module.ModuleId = moduleId;
				module.Instances = module.Instances ?? new List<ExportedInstance>();
				modules.Add(module);
			}

			return modules;
		}

		private void IndexLegacySchema(IEnumerable<ExportedModule> modules)
		{
			foreach (var module in modules)
			{
				foreach (var definition in (module.Definitions ?? new List<ExportedDefinitionInfo>()).Where(d => d != null))
				{
					_legacyDefinitionNames[definition.Id] = definition.Name;
				}

				foreach (var section in (module.SectionDefinitions ?? new List<ExportedSectionDefinitionInfo>()).Where(s => s != null))
				{
					_legacySectionNames[section.Id] = section.Name;
					if (!_legacyFields.TryGetValue(section.Id, out var fields))
					{
						fields = new Dictionary<Guid, string>();
						_legacyFields[section.Id] = fields;
					}

					foreach (var field in (section.Fields ?? new List<ExportedFieldInfo>()).Where(f => f != null))
					{
						fields[field.Id] = field.Name;
					}
				}
			}
		}

		/// <summary>
		/// The legacy configuration editors linked every nested profile directly to the service configuration version
		/// (and specification) as well, next to its parent. In the new model nested profiles are only reached through
		/// their parent profile, so those extra links would show the nested profile a second time at the top level.
		/// </summary>
		private void IndexNestedProfileLinks(IEnumerable<ExportedModule> modules)
		{
			var instances = modules.SelectMany(m => m.Instances.Where(i => i != null)).ToList();
			var nestedByProfile = new Dictionary<Guid, List<Guid>>();
			var profileNames = new Dictionary<Guid, string>();
			foreach (var profile in instances.Where(i => i.DefinitionId == KnownIds.Profile.Definition))
			{
				if (!nestedByProfile.ContainsKey(profile.Id))
				{
					nestedByProfile[profile.Id] = GuidValues(profile, KnownIds.Profile.Section, KnownIds.Profile.NestedProfiles);
					profileNames[profile.Id] = profile.Name;
				}
			}

			Func<Guid, string> profileLabel = id => profileNames.TryGetValue(id, out string name) && !String.IsNullOrEmpty(name) ? $"'{name}' ({id})" : id.ToString();

			foreach (var container in KnownIds.ProfileContainers)
			{
				var profileByLink = new Dictionary<Guid, Guid>();
				foreach (var link in instances.Where(i => i.DefinitionId == container.LinkDefinition))
				{
					var profileIds = GuidValues(link, container.LinkSection, container.LinkProfileField);
					if (profileIds.Count > 0 && !profileByLink.ContainsKey(link.Id))
					{
						profileByLink[link.Id] = profileIds[0];
					}
				}

				var stripped = new Dictionary<Guid, string>();
				var keptSomewhere = new HashSet<Guid>();
				foreach (var owner in instances.Where(i => i.DefinitionId == container.Definition))
				{
					var links = GuidValues(owner, container.Section, container.LinksField);
					var reachable = links
						.Where(profileByLink.ContainsKey)
						.Select(l => profileByLink[l])
						.Distinct()
						.ToDictionary(p => p, p => Descendants(p, nestedByProfile));

					foreach (Guid link in links)
					{
						Guid? parent = null;
						if (profileByLink.TryGetValue(link, out Guid profileId))
						{
							// A profile nested in a cycle with another linked profile stays linked, so nothing becomes unreachable.
							parent = reachable.Keys
								.Where(q => q != profileId && reachable[q].Contains(profileId) && !reachable[profileId].Contains(q))
								.Select(q => (Guid?)q)
								.FirstOrDefault();
						}

						if (parent == null)
						{
							keptSomewhere.Add(link);
							continue;
						}

						string reason = $"Profile {profileLabel(profileId)} is nested under profile {profileLabel(parent.Value)}. In the new model a {container.Name} only links its top-level profiles; nested profiles are reached through their parent profile.";
						if (!_nestedProfileLinksPerOwner.TryGetValue(owner.Id, out var perOwner))
						{
							perOwner = new Dictionary<Guid, string>();
							_nestedProfileLinksPerOwner[owner.Id] = perOwner;
						}

						perOwner[link] = reason;
						stripped[link] = reason;
					}
				}

				foreach (var entry in stripped.Where(e => !keptSomewhere.Contains(e.Key)))
				{
					_nestedProfileLinkInstances[entry.Key] = entry.Value;
				}
			}
		}

		private static HashSet<Guid> Descendants(Guid profileId, Dictionary<Guid, List<Guid>> nestedByProfile)
		{
			var result = new HashSet<Guid>();
			var queue = new Queue<Guid>();
			queue.Enqueue(profileId);
			while (queue.Count > 0)
			{
				if (!nestedByProfile.TryGetValue(queue.Dequeue(), out var children))
				{
					continue;
				}

				foreach (Guid child in children.Where(result.Add))
				{
					queue.Enqueue(child);
				}
			}

			return result;
		}

		private static List<Guid> GuidValues(ExportedInstance instance, Guid sectionDefinitionId, Guid fieldId)
		{
			var result = new List<Guid>();
			foreach (var field in (instance.Sections ?? new List<ExportedSection>())
				.Where(s => s != null && s.SectionDefinitionId == sectionDefinitionId)
				.SelectMany(s => s.Fields ?? new List<ExportedField>())
				.Where(f => f != null && f.FieldId == fieldId))
			{
				foreach (string value in field.Values ?? new List<string>())
				{
					if (Guid.TryParse(value, out Guid id) && id != Guid.Empty && !result.Contains(id))
					{
						result.Add(id);
					}
				}
			}

			return result;
		}

		private string GetDefinitionName(TargetModuleSchema schema, Guid definitionId)
		{
			if (_legacyDefinitionNames.TryGetValue(definitionId, out string name) && !String.IsNullOrEmpty(name))
			{
				return name;
			}

			if (KnownIds.RemovedDefinitions.TryGetValue(definitionId, out name))
			{
				return name;
			}

			return schema.GetDefinition(definitionId)?.Name ?? definitionId.ToString();
		}

		private string GetSectionName(TargetModuleSchema schema, Guid sectionDefinitionId)
		{
			if (_legacySectionNames.TryGetValue(sectionDefinitionId, out string name) && !String.IsNullOrEmpty(name))
			{
				return name;
			}

			return schema.GetSectionDefinition(sectionDefinitionId)?.GetName() ?? sectionDefinitionId.ToString();
		}

		private string GetFieldName(Guid sectionDefinitionId, Guid fieldId, FieldDescriptor target)
		{
			if (_legacyFields.TryGetValue(sectionDefinitionId, out var fields) && fields.TryGetValue(fieldId, out string name) && !String.IsNullOrEmpty(name))
			{
				return name;
			}

			return target?.Name ?? fieldId.ToString();
		}

		private bool IsKnownInLegacy(Guid sectionDefinitionId, Guid fieldId)
		{
			return _legacyFields.TryGetValue(sectionDefinitionId, out var fields) && fields.ContainsKey(fieldId);
		}

		private void PlanDrop(PlannedItem item, ExportedInstance instance, string reason = null)
		{
			item.Action = PlannedAction.Drop;
			if (reason == null)
			{
				reason = KnownIds.RemovedDefinitions.ContainsKey(instance.DefinitionId)
					? $"Definition '{item.DefinitionName}' was removed in the new model."
					: $"Definition '{item.DefinitionName}' ({instance.DefinitionId}) does not exist on the target system.";
			}

			item.Error = reason;
			_dropped[item.Key] = $"Referenced {item.Label} is not migrated: {reason}";

			var schema = _schemas[item.ModuleId];
			foreach (var section in (instance.Sections ?? new List<ExportedSection>()).Where(s => s != null))
			{
				foreach (var field in (section.Fields ?? new List<ExportedField>()).Where(HasValue))
				{
					AddDropped(item, GetSectionName(schema, section.SectionDefinitionId), GetFieldName(section.SectionDefinitionId, field.FieldId, null), Describe(field), reason);
				}
			}

			if (!item.DroppedData.Any())
			{
				AddDropped(item, String.Empty, String.Empty, String.Empty, reason);
			}
		}

		private void Build(TargetModuleSchema schema, ExportedInstance source, PlannedItem item)
		{
			var definition = schema.GetDefinition(source.DefinitionId);
			var existing = _lookup.GetExisting(item.ModuleId, source.Id);
			if (existing != null && existing.DomDefinitionId?.Id != source.DefinitionId)
			{
				item.Fail($"An instance with ID {source.Id} already exists on the target with another definition ({existing.DomDefinitionId?.Id}). It is left untouched.");
				return;
			}

			item.Existing = existing;
			var desired = new DomInstance
			{
				ID = new DomInstanceId(source.Id) { ModuleId = item.ModuleId },
				DomDefinitionId = new DomDefinitionId(source.DefinitionId) { ModuleId = item.ModuleId },
			};

			var legacyLinks = new LegacyLinkInfo();
			foreach (var exportedSection in (source.Sections ?? new List<ExportedSection>()).Where(s => s != null))
			{
				if (!TryBuildSection(schema, definition, item, exportedSection, legacyLinks, out var section))
				{
					return;
				}

				if (section != null && section.FieldValues.Count > 0)
				{
					desired.Sections.Add(section);
				}
			}

			ApplyLegacyLinks(schema, definition, item, desired, legacyLinks);
			CheckEnvironment(item, desired);

			if (existing != null && !PreserveTargetOnlyValues(item, schema, desired, existing, legacyLinks.MappedFields))
			{
				return;
			}

			if (!PlanStatus(schema, definition, item, source, desired))
			{
				return;
			}

			item.Desired = desired;
			if (existing == null)
			{
				item.Action = PlannedAction.Create;
			}
			else if (item.Transitions.Count == 0 && String.Equals(Canonical(desired), Canonical(existing), StringComparison.Ordinal))
			{
				item.Action = PlannedAction.Unchanged;
			}
			else
			{
				item.Action = PlannedAction.Update;
			}
		}

		private bool TryBuildSection(TargetModuleSchema schema, DomDefinition definition, PlannedItem item, ExportedSection exported, LegacyLinkInfo legacyLinks, out Section section)
		{
			section = null;
			Guid sectionDefinitionId = exported.SectionDefinitionId;
			string sectionName = GetSectionName(schema, sectionDefinitionId);
			var fields = (exported.Fields ?? new List<ExportedField>()).Where(HasValue).ToList();

			if (schema.GetSectionDefinition(sectionDefinitionId) == null || !schema.IsSectionLinked(definition, sectionDefinitionId))
			{
				foreach (var field in fields)
				{
					AddDropped(item, sectionName, GetFieldName(sectionDefinitionId, field.FieldId, null), Describe(field), "Section was removed from this definition in the new model.");
				}

				return true;
			}

			section = new Section(new SectionDefinitionID(sectionDefinitionId) { ModuleId = item.ModuleId })
			{
				ID = new SectionID(exported.SectionId == Guid.Empty ? Guid.NewGuid() : exported.SectionId),
			};

			foreach (var field in fields)
			{
				// Legacy link fields are always mapped to IsLinked/LinkedConsumer, even if they still exist on the target.
				if (legacyLinks.TryCapture(sectionDefinitionId, field))
				{
					continue;
				}

				var target = schema.GetField(sectionDefinitionId, field.FieldId);
				string fieldName = GetFieldName(sectionDefinitionId, field.FieldId, target);

				if (target == null)
				{
					AddDropped(item, sectionName, fieldName, Describe(field), "Field was removed in the new model.");
					continue;
				}

				if (!ValueCodec.TryDecode(field, target, out var wrapper, out string error))
				{
					item.Fail($"{sectionName}.{fieldName}: {error}");
					return false;
				}

				wrapper = ProcessReferences(item, target, wrapper, sectionName, fieldName);
				if (wrapper != null)
				{
					section.AddOrReplaceFieldValue(new FieldValue(target.ID, wrapper));
				}
			}

			return true;
		}

		private IValueWrapper ProcessReferences(PlannedItem item, FieldDescriptor target, IValueWrapper wrapper, string sectionName, string fieldName)
		{
			var domField = target as DomInstanceFieldDescriptor;
			if (domField == null)
			{
				return wrapper;
			}

			List<Guid> ids;
			bool isList = wrapper.Value is IList;
			if (wrapper.Value is Guid single)
			{
				ids = new List<Guid> { single };
			}
			else if (wrapper.Value is IEnumerable<Guid> many)
			{
				ids = many.ToList();
			}
			else
			{
				return wrapper;
			}

			string referencedModule = String.IsNullOrWhiteSpace(domField.ModuleId) ? item.ModuleId : domField.ModuleId;
			var kept = new List<Guid>();
			_nestedProfileLinksPerOwner.TryGetValue(item.InstanceId, out var nestedLinks);
			bool isProfileLinksField = nestedLinks != null && KnownIds.ProfileContainers.Any(c => c.LinksField == target.ID.Id);
			foreach (Guid id in ids)
			{
				if (isProfileLinksField && nestedLinks.TryGetValue(id, out string nestedReason))
				{
					AddDropped(item, sectionName, fieldName, id.ToString(), nestedReason);
					continue;
				}

				var key = new InstanceKey(referencedModule, id);
				if (_dropped.TryGetValue(key, out string droppedReason))
				{
					AddDropped(item, sectionName, fieldName, id.ToString(), droppedReason);
					continue;
				}

				if (_exported.Contains(key))
				{
					item.References.Add(key);
				}
				else if (id != Guid.Empty && !_lookup.Exists(referencedModule, id))
				{
					// Dangling reference in the legacy data: the referenced instance was deleted on the source system.
					AddDropped(item, sectionName, fieldName, id.ToString(), $"Referenced {referencedModule} instance does not exist in the export nor on this system (already deleted on the source system).");
					continue;
				}

				kept.Add(id);
			}

			if (kept.Count == ids.Count)
			{
				return wrapper;
			}

			if (kept.Count == 0)
			{
				return null;
			}

			return isList ? ValueWrapperFactory.Create(kept) : ValueWrapperFactory.Create(kept[0]);
		}

		private void ApplyLegacyLinks(TargetModuleSchema schema, DomDefinition definition, PlannedItem item, DomInstance desired, LegacyLinkInfo legacy)
		{
			if (!legacy.HasLinkFlag && legacy.Producers.Count == 0 && legacy.InvalidProducers.Count == 0)
			{
				return;
			}

			Guid sectionId = KnownIds.ConfigurationParameterValue.Section;
			string sectionName = GetSectionName(schema, sectionId);
			var isLinkedField = schema.GetField(sectionId, KnownIds.ConfigurationParameterValue.IsLinked);
			var consumerField = schema.GetField(sectionId, KnownIds.ConfigurationParameterValue.LinkedConsumer);
			string linkFieldName = GetFieldName(sectionId, KnownIds.ConfigurationParameterValue.LegacyLinkedConfigurationParameter, null);

			foreach (string invalid in legacy.InvalidProducers)
			{
				AddDropped(item, sectionName, linkFieldName, invalid, "Value is not a valid parameter ID.");
			}

			var resolved = new List<Guid>();
			foreach (Guid producer in legacy.Producers)
			{
				var key = new InstanceKey(KnownIds.ConfigurationsModuleId, producer);
				if (!_dropped.ContainsKey(key) && (_exported.Contains(key) || _lookup.Exists(KnownIds.ConfigurationsModuleId, producer)))
				{
					resolved.Add(producer);
					item.References.Add(key);
				}
				else
				{
					AddDropped(item, sectionName, linkFieldName, producer.ToString(), "Linked configuration parameter does not exist in the export nor on this system, so the link cannot be recreated.");
				}
			}

			if (!schema.IsSectionLinked(definition, sectionId) || isLinkedField == null)
			{
				if (legacy.HasLinkFlag || resolved.Count > 0)
				{
					AddDropped(item, sectionName, "Link", String.Join(", ", resolved), "The new model has no IsLinked field for this instance.");
				}

				return;
			}

			var section = desired.Sections.FirstOrDefault(s => s.SectionDefinitionID?.Id == sectionId);
			if (section == null)
			{
				section = new Section(new SectionDefinitionID(sectionId) { ModuleId = item.ModuleId }) { ID = new SectionID(Guid.NewGuid()) };
				desired.Sections.Add(section);
			}

			if (resolved.Count > 0)
			{
				if (consumerField == null)
				{
					AddDropped(item, sectionName, linkFieldName, String.Join(", ", resolved), "The new model has no Linked Consumer field.");
				}
				else
				{
					var current = (section.GetFieldValueById(consumerField.ID)?.Value?.Value as IEnumerable<Guid>) ?? Enumerable.Empty<Guid>();

					// Links that already exist on the target (e.g. created on the new system) are kept.
					var onTarget = item.Existing == null
						? Enumerable.Empty<Guid>()
						: AllFieldValues(item.Existing, sectionId)
							.Where(f => f.FieldDescriptorID.Id == consumerField.ID.Id)
							.SelectMany(f => (f.Value?.Value as IEnumerable<Guid>) ?? Enumerable.Empty<Guid>());
					var merged = current.Concat(onTarget).Concat(resolved).Distinct().ToList();
					section.AddOrReplaceFieldValue(new FieldValue(consumerField.ID, ValueWrapperFactory.Create(merged)));
					legacy.MappedFields.Add(consumerField.ID.Id);
				}
			}

			if (legacy.HasLinkFlag || resolved.Count > 0)
			{
				section.AddOrReplaceFieldValue(new FieldValue(isLinkedField.ID, ValueWrapperFactory.Create(true)));
				legacy.MappedFields.Add(isLinkedField.ID.Id);
			}
		}

		private void CheckEnvironment(PlannedItem item, DomInstance desired)
		{
			foreach (var value in AllFieldValues(desired, KnownIds.ConfigurationParameterValue.Section)
				.Where(f => f.FieldDescriptorID.Id == KnownIds.ConfigurationParameterValue.LinkedScript))
			{
				CheckScript(item, "Linked Script", value.Value?.Value as string);
			}

			foreach (var value in desired.Sections.SelectMany(s => s.FieldValues).Where(f => f.FieldDescriptorID.Id == KnownIds.ServiceInfo.MonitoringService))
			{
				string key = value.Value?.Value as string;
				if (!String.IsNullOrWhiteSpace(key) && !_environment.ServiceExists(key))
				{
					item.Warnings.Add($"Monitoring Service '{key}' does not exist on this system. The reference is kept as-is.");
				}
			}

			foreach (var section in desired.Sections.Where(s => s.SectionDefinitionID?.Id == KnownIds.ServiceItems.Section))
			{
				string type = Convert.ToString(section.GetFieldValueById(new FieldDescriptorID(KnownIds.ServiceItems.ServiceItemType))?.Value?.Value, CultureInfo.InvariantCulture);
				CheckScript(item, "Service Item Script", section.GetFieldValueById(new FieldDescriptorID(KnownIds.ServiceItems.ServiceItemScript))?.Value?.Value as string);

				if (String.Equals(type, KnownIds.ServiceItems.TypeService, StringComparison.OrdinalIgnoreCase))
				{
					CheckReference(item, "Service Item Implementation Reference", section, KnownIds.ServiceItems.ImplementationReference, KnownIds.ServiceManagementModuleId);
				}
				else if (String.Equals(type, KnownIds.ServiceItems.TypeWorkflow, StringComparison.OrdinalIgnoreCase))
				{
					CheckReference(item, "Service Item Definition Reference", section, KnownIds.ServiceItems.DefinitionReference, KnownIds.WorkflowModuleId);
				}
			}
		}

		private void CheckScript(PlannedItem item, string label, string scriptName)
		{
			if (!String.IsNullOrWhiteSpace(scriptName) && !_environment.ScriptExists(scriptName))
			{
				item.Warnings.Add($"{label} '{scriptName}' does not exist on this system. The reference is kept as-is.");
			}
		}

		private void CheckReference(PlannedItem item, string label, Section section, Guid fieldId, string moduleId)
		{
			string raw = section.GetFieldValueById(new FieldDescriptorID(fieldId))?.Value?.Value as string;
			if (!Guid.TryParse(raw, out Guid id))
			{
				return;
			}

			var key = new InstanceKey(moduleId, id);
			if (_dropped.TryGetValue(key, out string droppedLabel))
			{
				item.Warnings.Add($"{label} points to {droppedLabel}, which is not migrated.");
			}
			else if (_exported.Contains(key))
			{
				item.References.Add(key);
			}
			else if (!_lookup.Exists(moduleId, id))
			{
				item.Warnings.Add($"{label} '{id}' does not exist in {moduleId} on this system. The reference is kept as-is.");
			}
		}

		private bool PreserveTargetOnlyValues(PlannedItem item, TargetModuleSchema schema, DomInstance desired, DomInstance existing, HashSet<Guid> mappedFields)
		{
			// Fields that do not exist in the legacy model (added in the new model) keep the value they already have on the target.
			foreach (var existingSection in existing.Sections.Where(s => s?.SectionDefinitionID != null && !KnownIds.RemovedSections.ContainsKey(s.SectionDefinitionID.Id)))
			{
				Guid sectionDefinitionId = existingSection.SectionDefinitionID.Id;
				var preserved = existingSection.FieldValues
					.Where(f => f.Value?.Value != null && !KnownIds.RemovedFields.ContainsKey(f.FieldDescriptorID.Id) && !IsKnownInLegacy(sectionDefinitionId, f.FieldDescriptorID.Id) && !mappedFields.Contains(f.FieldDescriptorID.Id))
					.ToList();
				if (preserved.Count == 0)
				{
					continue;
				}

				var target = desired.Sections.FirstOrDefault(s => s.ID != null && existingSection.ID != null && s.ID.Id == existingSection.ID.Id);
				if (target == null
					&& desired.Sections.Count(s => s.SectionDefinitionID?.Id == sectionDefinitionId) == 1
					&& existing.Sections.Count(s => s.SectionDefinitionID?.Id == sectionDefinitionId) == 1)
				{
					target = desired.Sections.First(s => s.SectionDefinitionID?.Id == sectionDefinitionId);
				}

				if (target == null && desired.Sections.Any(s => s.SectionDefinitionID?.Id == sectionDefinitionId))
				{
					// Adding a separate section would detach these values from the row they belong to.
					item.Fail($"{GetSectionName(schema, sectionDefinitionId)}: section {existingSection.ID?.Id} on the target holds values that only exist in the new model, but it cannot be matched to a migrated section. The instance is left untouched; resolve it manually.");
					return false;
				}

				if (target == null)
				{
					target = new Section(new SectionDefinitionID(sectionDefinitionId) { ModuleId = desired.ID.ModuleId })
					{
						ID = new SectionID(existingSection.ID?.Id ?? Guid.NewGuid()),
					};
					desired.Sections.Add(target);
				}

				foreach (var value in preserved.Where(v => target.GetFieldValueById(v.FieldDescriptorID) == null))
				{
					target.AddOrReplaceFieldValue(new FieldValue(value.FieldDescriptorID, value.Value));
				}
			}

			return true;
		}

		private bool PlanStatus(TargetModuleSchema schema, DomDefinition definition, PlannedItem item, ExportedInstance source, DomInstance desired)
		{
			var behavior = schema.GetBehavior(definition);
			if (behavior == null)
			{
				if (!String.IsNullOrEmpty(source.StatusId))
				{
					item.Warnings.Add($"Legacy status '{source.StatusId}' is not applied: the new definition has no behavior.");
				}

				return true;
			}

			string from = item.Existing?.StatusId ?? behavior.InitialStatusId;
			string target = String.IsNullOrEmpty(source.StatusId) ? from : source.StatusId;
			if (!behavior.HasStatus(target))
			{
				item.Fail($"Legacy status '{target}' does not exist in the new behavior of '{item.DefinitionName}'.");
				return false;
			}

			var path = behavior.FindPath(from, target);
			if (path == null)
			{
				item.Fail($"There is no status transition path from '{from}' to legacy status '{target}' in the new behavior.");
				return false;
			}

			desired.StatusId = from;
			item.TargetStatusId = target;
			item.Transitions = path;
			return true;
		}

		private void AddDropped(PlannedItem item, string section, string field, string value, string reason)
		{
			item.DroppedData.Add(new DroppedValue
			{
				ModuleId = item.ModuleId,
				InstanceId = item.InstanceId,
				InstanceName = item.Name,
				Definition = item.DefinitionName,
				Section = section,
				Field = field,
				Value = value,
				Reason = reason,
			});
		}

		/// <summary>
		/// Collects the values of the legacy link fields of a configuration parameter value.
		/// </summary>
		private sealed class LegacyLinkInfo
		{
			public bool HasLinkFlag { get; private set; }

			public List<Guid> Producers { get; } = new List<Guid>();

			public List<string> InvalidProducers { get; } = new List<string>();

			public HashSet<Guid> MappedFields { get; } = new HashSet<Guid>();

			public bool TryCapture(Guid sectionDefinitionId, ExportedField field)
			{
				if (sectionDefinitionId != KnownIds.ConfigurationParameterValue.Section)
				{
					return false;
				}

				var values = field.Values.Where(v => !String.IsNullOrWhiteSpace(v)).ToList();
				if (field.FieldId == KnownIds.ConfigurationParameterValue.LegacyLinkedInstanceReference)
				{
					HasLinkFlag |= values.Count > 0;
					return true;
				}

				if (field.FieldId == KnownIds.ConfigurationParameterValue.LegacyLinkedConfigurationParameter)
				{
					foreach (string value in values)
					{
						if (Guid.TryParse(value, out Guid id) && id != Guid.Empty)
						{
							Producers.Add(id);
						}
						else
						{
							InvalidProducers.Add(value);
						}
					}

					return true;
				}

				return false;
			}
		}
	}
}

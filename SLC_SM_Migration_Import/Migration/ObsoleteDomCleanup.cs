namespace SLCSMMigrationImport.Migration
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.Apps.Sections.SectionDefinitions;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.Net.Sections;

	/// <summary>
	/// Access to the DOM data of one module, as needed by <see cref="ObsoleteDomCleanup"/>.
	/// </summary>
	public interface ILegacyDomStore
	{
		string ModuleId { get; }

		IEnumerable<DomDefinition> GetDefinitions();

		IEnumerable<SectionDefinition> GetSectionDefinitions();

		IEnumerable<DomInstance> GetAllInstances();

		IEnumerable<DomInstance> GetInstancesOfDefinition(Guid definitionId);

		IEnumerable<DomInstance> GetInstances(IEnumerable<Guid> ids);

		void UpdateInstance(DomInstance instance);

		void DeleteInstance(DomInstance instance);

		void UpdateSectionDefinition(SectionDefinition section);

		void DeleteSectionDefinition(SectionDefinition section);

		void DeleteDefinition(DomDefinition definition);
	}

	/// <summary>
	/// Removes the legacy DOM model parts (definitions, sections and fields listed in <see cref="KnownIds"/>) that are left behind
	/// when a legacy system is upgraded in place, including the instances and values that use them.
	/// Only needed on such a system; on a clean new system nothing is found. The legacy data itself is in the export file.
	/// </summary>
	/// <remarks>
	/// Runs in two phases around the execution of an already validated <see cref="MigrationPlan"/>:
	/// <see cref="CleanInstances"/> before it, leaving the instances of failed or updated items untouched,
	/// and <see cref="CleanModel"/> after it, keeping the model parts still used by instances whose migration failed.
	/// This way a rejected item keeps its legacy values, and a failed update can still be rolled back to them.
	/// </remarks>
	public sealed class ObsoleteDomCleanup
	{
		private const int MaxListedInstances = 10;

		private readonly Action<string> _log;
		private readonly bool _dryRun;
		private readonly HashSet<InstanceKey> _notCleaned = new HashSet<InstanceKey>();

		public ObsoleteDomCleanup(Action<string> log, bool dryRun)
		{
			_log = log ?? (_ => { });
			_dryRun = dryRun;
		}

		public List<string> Results { get; } = new List<string>();

		public List<string> Failures { get; } = new List<string>();

		/// <summary>
		/// Gets the legacy model parts that were kept because instances whose migration failed still use them.
		/// </summary>
		public List<string> Warnings { get; } = new List<string>();

		/// <summary>
		/// Checks that the new DOM model is installed, so the cleanup never runs on a system that still runs the legacy model.
		/// </summary>
		public static bool IsNewModelInstalled(DomHelper serviceManagementHelper)
		{
			return serviceManagementHelper.SectionDefinitions.Read(new TRUEFilterElement<SectionDefinition>())
				.Where(s => s.GetID()?.Id == KnownIds.NewModelMarker.Section)
				.SelectMany(s => s.GetAllFieldDescriptors())
				.Any(f => f.ID.Id == KnownIds.NewModelMarker.Field);
		}

		/// <summary>
		/// Gets the target instances <see cref="CleanInstances"/> must leave untouched: those of items that failed validation
		/// (their legacy values are preserved) and those the import overwrites (so a failed update leaves them as they were).
		/// </summary>
		public static HashSet<InstanceKey> InstancesToLeaveUntouched(MigrationPlan plan)
		{
			var written = new HashSet<InstanceKey>(plan.Items.Where(i => i.Action != PlannedAction.Fail).Select(i => i.Key));
			return new HashSet<InstanceKey>(plan.Items
				.Where(i => i.Action == PlannedAction.Update || (i.Action == PlannedAction.Fail && !written.Contains(i.Key)))
				.Select(i => i.Key));
		}

		/// <summary>
		/// Phase 1, before the plan is executed: deletes the instances of removed definitions and strips removed sections and fields
		/// from the other instances, except for <paramref name="leaveUntouched"/>.
		/// </summary>
		public void CleanInstances(ILegacyDomStore store, ICollection<InstanceKey> leaveUntouched)
		{
			string moduleId = store.ModuleId;
			var parts = LegacyParts.Read(store);
			if (parts.IsEmpty)
			{
				Results.Add($"'{moduleId}': no legacy DOM model parts found.");
				return;
			}

			_log($"Legacy DOM model parts in '{moduleId}': {parts.Describe()}.");

			var toDelete = parts.Definitions.SelectMany(d => store.GetInstancesOfDefinition(d.ID.Id)).ToList();
			var deleteIds = new HashSet<Guid>(toDelete.Select(i => i.ID.Id));
			int cleaned = 0;
			int untouched = 0;
			foreach (var instance in store.GetAllInstances().Where(i => !deleteIds.Contains(i.ID.Id)))
			{
				var key = new InstanceKey(moduleId, instance.ID.Id);
				if (leaveUntouched.Contains(key))
				{
					untouched += HoldsLegacyValues(instance) ? 1 : 0;
					continue;
				}

				if (!StripRemoved(instance))
				{
					continue;
				}

				cleaned++;
				if (!_dryRun && !Try(() => store.UpdateInstance(instance), $"remove legacy values from instance {instance.ID.Id} in '{moduleId}'"))
				{
					_notCleaned.Add(key);
				}
			}

			var deleted = toDelete.Where(i => !leaveUntouched.Contains(new InstanceKey(moduleId, i.ID.Id))).ToList();
			untouched += toDelete.Count - deleted.Count;
			if (!_dryRun)
			{
				foreach (var instance in deleted)
				{
					if (!Try(() => store.DeleteInstance(instance), $"delete instance {instance.ID.Id} in '{moduleId}'"))
					{
						_notCleaned.Add(new InstanceKey(moduleId, instance.ID.Id));
					}
				}
			}

			string untouchedNote = untouched == 0
				? String.Empty
				: $", left {untouched} instance(s) with legacy values for the import (overwritten when migrated, kept when their migration fails)";
			Results.Add(_dryRun
				? $"'{moduleId}': would delete {deleted.Count} instance(s) of removed definitions and clean {cleaned} instance(s){untouchedNote}."
				: $"'{moduleId}': deleted {deleted.Count} instance(s) of removed definitions and cleaned {cleaned} instance(s){untouchedNote}.");
		}

		/// <summary>
		/// Phase 2, after the plan is executed: removes the legacy fields, definitions and sections, except those still used by
		/// the instances of <paramref name="failedItems"/> (or instances phase 1 could not clean), so their legacy values stay intact.
		/// </summary>
		public void CleanModel(ILegacyDomStore store, IEnumerable<InstanceKey> failedItems)
		{
			string moduleId = store.ModuleId;
			var parts = LegacyParts.Read(store);
			if (parts.IsEmpty)
			{
				return;
			}

			var candidateIds = (failedItems ?? Enumerable.Empty<InstanceKey>())
				.Concat(_notCleaned)
				.Where(k => String.Equals(k.ModuleId, moduleId, StringComparison.OrdinalIgnoreCase))
				.Select(k => k.Id)
				.Distinct()
				.ToList();
			var holders = candidateIds.Count == 0
				? new List<DomInstance>()
				: store.GetInstances(candidateIds).Where(i => HoldsLegacyValues(i) || IsOfRemovedDefinition(i)).ToList();

			var kept = parts.KeepUsedBy(holders);
			if (!kept.IsEmpty)
			{
				string ids = String.Join(", ", holders.Take(MaxListedInstances).Select(i => i.ID.Id)) + (holders.Count > MaxListedInstances ? ", ..." : String.Empty);
				string warning = $"'{moduleId}': {(_dryRun ? "would keep" : "kept")} legacy {kept.Describe()} because {holders.Count} instance(s) whose migration failed still use them ({ids}). "
					+ "Their legacy values are preserved: fix the reported errors and re-run the import to migrate them and finish the cleanup.";
				Warnings.Add(warning);
				_log(warning);
			}

			var removed = parts.Except(kept);
			if (removed.IsEmpty)
			{
				return;
			}

			if (_dryRun)
			{
				Results.Add($"'{moduleId}': would remove {removed.Describe()}.");
				return;
			}

			// Removed fields first, then definitions, then the sections they used.
			foreach (var section in removed.SectionsWithFields)
			{
				Try(
					() =>
					{
						removed.FieldsOf(section).ForEach(f => section.RemoveFieldDescriptor(f.ID));
						store.UpdateSectionDefinition(section);
					},
					$"remove legacy fields from section '{section.GetName()}' in '{moduleId}'");
			}

			removed.Definitions.ForEach(d => Try(() => store.DeleteDefinition(d), $"delete definition '{d.Name}' in '{moduleId}'"));
			removed.Sections.ForEach(s => Try(() => store.DeleteSectionDefinition(s), $"delete section '{s.GetName()}' in '{moduleId}'"));

			Results.Add($"'{moduleId}': removed {removed.Describe()}.");
		}

		private static bool IsOfRemovedDefinition(DomInstance instance)
		{
			return instance.DomDefinitionId != null && KnownIds.RemovedDefinitions.ContainsKey(instance.DomDefinitionId.Id);
		}

		private static bool IsRemovedSection(Section section)
		{
			return section.SectionDefinitionID != null && KnownIds.RemovedSections.ContainsKey(section.SectionDefinitionID.Id);
		}

		private static bool HoldsLegacyValues(DomInstance instance)
		{
			return instance.Sections.Any(s => IsRemovedSection(s) || s.FieldValues.Any(f => KnownIds.RemovedFields.ContainsKey(f.FieldDescriptorID.Id)));
		}

		private static bool StripRemoved(DomInstance instance)
		{
			bool changed = instance.Sections.RemoveAll(IsRemovedSection) > 0;
			foreach (var section in instance.Sections)
			{
				foreach (var field in section.FieldValues.Where(f => KnownIds.RemovedFields.ContainsKey(f.FieldDescriptorID.Id)).ToList())
				{
					changed |= section.RemoveFieldValueById(field.FieldDescriptorID);
				}
			}

			return changed;
		}

		private bool Try(Action action, string description)
		{
			try
			{
				action();
				return true;
			}
			catch (Exception e)
			{
				string failure = $"Failed to {description}: {e.Message}";
				Failures.Add(failure);
				_log(failure);
				return false;
			}
		}

		/// <summary>
		/// The legacy definitions, sections and fields present in one module.
		/// </summary>
		private sealed class LegacyParts
		{
			private readonly Dictionary<CustomSectionDefinition, List<FieldDescriptor>> _fields;

			private LegacyParts(List<DomDefinition> definitions, List<SectionDefinition> sections, Dictionary<CustomSectionDefinition, List<FieldDescriptor>> fields)
			{
				Definitions = definitions;
				Sections = sections;
				_fields = fields;
			}

			public List<DomDefinition> Definitions { get; }

			public List<SectionDefinition> Sections { get; }

			public IEnumerable<CustomSectionDefinition> SectionsWithFields => _fields.Keys;

			public bool IsEmpty => Definitions.Count == 0 && Sections.Count == 0 && _fields.Count == 0;

			public static LegacyParts Read(ILegacyDomStore store)
			{
				var allSections = store.GetSectionDefinitions().ToList();
				return new LegacyParts(
					store.GetDefinitions().Where(d => KnownIds.RemovedDefinitions.ContainsKey(d.ID.Id)).ToList(),
					allSections.Where(s => KnownIds.RemovedSections.ContainsKey(s.GetID().Id)).ToList(),
					allSections
						.OfType<CustomSectionDefinition>()
						.Where(s => !KnownIds.RemovedSections.ContainsKey(s.GetID().Id))
						.Select(s => new { Section = s, Fields = s.GetAllFieldDescriptors().Where(f => KnownIds.RemovedFields.ContainsKey(f.ID.Id)).ToList() })
						.Where(s => s.Fields.Count > 0)
						.ToDictionary(s => s.Section, s => s.Fields));
			}

			public List<FieldDescriptor> FieldsOf(CustomSectionDefinition section) => _fields[section];

			/// <summary>
			/// Gets the parts still needed by the given instances, including the sections linked to a kept definition.
			/// </summary>
			public LegacyParts KeepUsedBy(List<DomInstance> instances)
			{
				var definitionIds = new HashSet<Guid>(instances.Where(i => i.DomDefinitionId != null).Select(i => i.DomDefinitionId.Id));
				var definitions = Definitions.Where(d => definitionIds.Contains(d.ID.Id)).ToList();

				var sectionIds = new HashSet<Guid>(instances.SelectMany(i => i.Sections).Where(s => s.SectionDefinitionID != null).Select(s => s.SectionDefinitionID.Id));
				sectionIds.UnionWith(definitions.SelectMany(d => d.SectionDefinitionLinks ?? new List<SectionDefinitionLink>()).Where(l => l.SectionDefinitionID != null).Select(l => l.SectionDefinitionID.Id));
				var sections = Sections.Where(s => sectionIds.Contains(s.GetID().Id)).ToList();

				var fieldIds = new HashSet<Guid>(instances.SelectMany(i => i.Sections).SelectMany(s => s.FieldValues).Select(f => f.FieldDescriptorID.Id));
				var fields = _fields
					.Select(p => new { p.Key, Fields = p.Value.Where(f => fieldIds.Contains(f.ID.Id)).ToList() })
					.Where(p => p.Fields.Count > 0)
					.ToDictionary(p => p.Key, p => p.Fields);

				return new LegacyParts(definitions, sections, fields);
			}

			public LegacyParts Except(LegacyParts kept)
			{
				var fields = _fields
					.Select(p => new
					{
						p.Key,
						Fields = p.Value.Where(f => !(kept._fields.TryGetValue(p.Key, out var keptFields) && keptFields.Contains(f))).ToList(),
					})
					.Where(p => p.Fields.Count > 0)
					.ToDictionary(p => p.Key, p => p.Fields);

				return new LegacyParts(Definitions.Except(kept.Definitions).ToList(), Sections.Except(kept.Sections).ToList(), fields);
			}

			public string Describe()
			{
				return $"definitions [{String.Join(", ", Definitions.Select(d => d.Name))}], sections [{String.Join(", ", Sections.Select(s => s.GetName()))}], "
					+ $"fields [{String.Join(", ", _fields.Values.SelectMany(f => f).Select(f => KnownIds.RemovedFields[f.ID.Id]))}]";
			}
		}
	}

	/// <summary>
	/// <see cref="ILegacyDomStore"/> on a <see cref="DomHelper"/>, reading instances page by page.
	/// </summary>
	public sealed class DomHelperLegacyStore : ILegacyDomStore
	{
		private const int PageSize = 500;
		private const int BatchSize = 100;

		private readonly DomHelper _helper;

		public DomHelperLegacyStore(DomHelper helper)
		{
			_helper = helper ?? throw new ArgumentNullException(nameof(helper));
		}

		public string ModuleId => _helper.ModuleId;

		public IEnumerable<DomDefinition> GetDefinitions() => _helper.DomDefinitions.Read(new TRUEFilterElement<DomDefinition>());

		public IEnumerable<SectionDefinition> GetSectionDefinitions() => _helper.SectionDefinitions.Read(new TRUEFilterElement<SectionDefinition>());

		public IEnumerable<DomInstance> GetAllInstances() => ReadPaged(new TRUEFilterElement<DomInstance>());

		public IEnumerable<DomInstance> GetInstancesOfDefinition(Guid definitionId) => ReadPaged(DomInstanceExposers.DomDefinitionId.Equal(definitionId));

		public IEnumerable<DomInstance> GetInstances(IEnumerable<Guid> ids)
		{
			var all = ids.Distinct().ToList();
			for (int i = 0; i < all.Count; i += BatchSize)
			{
				var filter = new ORFilterElement<DomInstance>(all.Skip(i).Take(BatchSize).Select(id => (FilterElement<DomInstance>)DomInstanceExposers.Id.Equal(id)).ToArray());
				foreach (var instance in _helper.DomInstances.Read(filter))
				{
					yield return instance;
				}
			}
		}

		public void UpdateInstance(DomInstance instance) => _helper.DomInstances.Update(instance);

		public void DeleteInstance(DomInstance instance) => _helper.DomInstances.Delete(instance);

		public void UpdateSectionDefinition(SectionDefinition section) => _helper.SectionDefinitions.Update(section);

		public void DeleteSectionDefinition(SectionDefinition section) => _helper.SectionDefinitions.Delete(section);

		public void DeleteDefinition(DomDefinition definition) => _helper.DomDefinitions.Delete(definition);

		private IEnumerable<DomInstance> ReadPaged(FilterElement<DomInstance> filter)
		{
			var paging = _helper.DomInstances.PreparePaging(filter, PageSize);
			while (paging.MoveToNextPage())
			{
				foreach (var instance in paging.GetCurrentPage())
				{
					yield return instance;
				}
			}
		}
	}
}

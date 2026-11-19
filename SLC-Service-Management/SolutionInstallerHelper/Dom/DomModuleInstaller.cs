namespace SLC_ServiceManagement.SolutionInstallerHelper.Dom
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.Apps.Sections.SectionDefinitions;
	using Skyline.DataMiner.Net.Sections;

	/// <summary>
	/// The outcome of a DOM configuration object during <see cref="DomModuleInstaller.Install"/>.
	/// </summary>
	internal enum DomInstallAction
	{
		Unchanged,
		Created,
		Updated,
	}

	/// <summary>
	/// Makes the DOM configuration of a DataMiner System match the shipped <see cref="DomModuleDefinition"/>s, without
	/// external tools: missing objects are created, outdated objects are updated and up-to-date objects are left untouched.
	/// </summary>
	/// <remarks>
	/// <para>Objects are matched on their ID, so re-running the installation never creates duplicates.</para>
	/// <para>Nothing is deleted: field descriptors and section definition links that exist on the system but are not shipped
	/// (for example legacy fields that <c>SLC_SM_Migration_Import</c> still has to clean up) are kept, after the shipped ones.</para>
	/// <para>Order: module settings, section definitions, behavior definitions (parents first) and DOM definitions, so every
	/// object only refers to objects that already exist.</para>
	/// </remarks>
	internal sealed class DomModuleInstaller
	{
		private readonly Action<string> _log;
		private readonly IDomConfigurationStore _store;

		public DomModuleInstaller(IDomConfigurationStore store, Action<string> log)
		{
			_store = store ?? throw new ArgumentNullException(nameof(store));
			_log = log ?? (_ => { });
		}

		/// <summary>
		/// Installs or updates the given DOM modules.
		/// </summary>
		/// <exception cref="InvalidOperationException">Creating or updating a DOM configuration object failed.</exception>
		public DomInstallResult Install(IReadOnlyCollection<DomModuleDefinition> modules)
		{
			if (modules == null)
			{
				throw new ArgumentNullException(nameof(modules));
			}

			var result = new DomInstallResult();

			foreach (var module in modules)
			{
				InstallModuleSettings(module, result);
			}

			foreach (var module in modules)
			{
				InstallSectionDefinitions(module, result);
			}

			foreach (var module in modules)
			{
				InstallBehaviorDefinitions(module, result);
			}

			foreach (var module in modules)
			{
				InstallDomDefinitions(module, result);
			}

			return result;
		}

		/// <summary>
		/// Gets the section definition to store: the shipped one plus the field descriptors that only exist on the system.
		/// </summary>
		internal static SectionDefinition Merge(SectionDefinition shipped, SectionDefinition existing)
		{
			var merged = DomJson.Clone(shipped);
			if (!(merged is CustomSectionDefinition mergedCustom) || !(existing is CustomSectionDefinition existingCustom))
			{
				return merged;
			}

			var shippedFieldIds = new HashSet<Guid>(mergedCustom.GetAllFieldDescriptors().Select(f => f.ID.Id));
			foreach (var field in existingCustom.GetAllFieldDescriptors().Where(f => !shippedFieldIds.Contains(f.ID.Id)))
			{
				mergedCustom.AddOrReplaceFieldDescriptor(field);
			}

			return mergedCustom;
		}

		/// <summary>
		/// Gets the DOM definition to store: the shipped one plus the section definition links that only exist on the system.
		/// </summary>
		internal static DomDefinition Merge(DomDefinition shipped, DomDefinition existing)
		{
			var merged = DomJson.Clone(shipped);
			if (existing?.SectionDefinitionLinks == null)
			{
				return merged;
			}

			merged.SectionDefinitionLinks = merged.SectionDefinitionLinks ?? new List<SectionDefinitionLink>();
			var shippedSectionIds = new HashSet<Guid>(merged.SectionDefinitionLinks.Where(l => l?.SectionDefinitionID != null).Select(l => l.SectionDefinitionID.Id));
			merged.SectionDefinitionLinks.AddRange(existing.SectionDefinitionLinks.Where(l => l?.SectionDefinitionID != null && !shippedSectionIds.Contains(l.SectionDefinitionID.Id)));

			return merged;
		}

		/// <summary>
		/// Orders behavior definitions so that a parent always comes before its children.
		/// </summary>
		internal static IEnumerable<DomBehaviorDefinition> ParentsFirst(IEnumerable<DomBehaviorDefinition> behaviorDefinitions)
		{
			var remaining = behaviorDefinitions.ToList();
			var shippedIds = new HashSet<Guid>(remaining.Select(b => b.ID.Id));
			var done = new HashSet<Guid>();

			while (remaining.Count > 0)
			{
				var ready = remaining
					.Where(b => b.ParentId == null || b.ParentId.Id == Guid.Empty || !shippedIds.Contains(b.ParentId.Id) || done.Contains(b.ParentId.Id))
					.ToList();

				if (ready.Count == 0)
				{
					throw new InvalidOperationException($"The behavior definitions {String.Join(", ", remaining.Select(b => $"'{b.Name}'"))} have a circular parent reference.");
				}

				foreach (var behavior in ready)
				{
					done.Add(behavior.ID.Id);
					remaining.Remove(behavior);
					yield return behavior;
				}
			}
		}

		private void InstallModuleSettings(DomModuleDefinition module, DomInstallResult result)
		{
			var existing = _store.GetModuleSettings(module.ModuleId);
			var desired = DomJson.Clone(module.ModuleSettings);

			Apply(
				result,
				module.ModuleId,
				"module settings",
				module.ModuleId,
				desired,
				existing,
				_store.CreateModuleSettings,
				_store.UpdateModuleSettings);
		}

		private void InstallSectionDefinitions(DomModuleDefinition module, DomInstallResult result)
		{
			var existingById = _store.GetSectionDefinitions(module.ModuleId)
				.Where(s => s?.GetID() != null)
				.GroupBy(s => s.GetID().Id)
				.ToDictionary(g => g.Key, g => g.First());

			foreach (var shipped in module.SectionDefinitions)
			{
				existingById.TryGetValue(shipped.GetID().Id, out var existing);
				var desired = existing == null ? DomJson.Clone(shipped) : Merge(shipped, existing);

				Apply(
					result,
					module.ModuleId,
					"section definition",
					shipped.GetName(),
					desired,
					existing,
					s => _store.CreateSectionDefinition(module.ModuleId, s),
					s => _store.UpdateSectionDefinition(module.ModuleId, s));
			}
		}

		private void InstallBehaviorDefinitions(DomModuleDefinition module, DomInstallResult result)
		{
			var existingById = _store.GetBehaviorDefinitions(module.ModuleId)
				.Where(b => b?.ID != null)
				.GroupBy(b => b.ID.Id)
				.ToDictionary(g => g.Key, g => g.First());

			foreach (var shipped in ParentsFirst(module.BehaviorDefinitions))
			{
				existingById.TryGetValue(shipped.ID.Id, out var existing);

				Apply(
					result,
					module.ModuleId,
					"behavior definition",
					shipped.Name,
					DomJson.Clone(shipped),
					existing,
					b => _store.CreateBehaviorDefinition(module.ModuleId, b),
					b => _store.UpdateBehaviorDefinition(module.ModuleId, b));
			}
		}

		private void InstallDomDefinitions(DomModuleDefinition module, DomInstallResult result)
		{
			var existingById = _store.GetDomDefinitions(module.ModuleId)
				.Where(d => d?.ID != null)
				.GroupBy(d => d.ID.Id)
				.ToDictionary(g => g.Key, g => g.First());

			foreach (var shipped in module.DomDefinitions)
			{
				existingById.TryGetValue(shipped.ID.Id, out var existing);
				var desired = existing == null ? DomJson.Clone(shipped) : Merge(shipped, existing);

				Apply(
					result,
					module.ModuleId,
					"DOM definition",
					shipped.Name,
					desired,
					existing,
					d => _store.CreateDomDefinition(module.ModuleId, d),
					d => _store.UpdateDomDefinition(module.ModuleId, d));
			}
		}

		private void Apply<T>(DomInstallResult result, string moduleId, string kind, string name, T desired, T existing, Action<T> create, Action<T> update)
			where T : class
		{
			DomInstallAction action;
			if (existing == null)
			{
				action = DomInstallAction.Created;
			}
			else if (DomJson.AreEquivalent(desired, existing))
			{
				action = DomInstallAction.Unchanged;
			}
			else
			{
				action = DomInstallAction.Updated;
			}

			try
			{
				switch (action)
				{
					case DomInstallAction.Created:
						create(desired);
						_log($"DOM '{moduleId}': created {kind} '{name}'.");
						break;

					case DomInstallAction.Updated:
						update(desired);
						_log($"DOM '{moduleId}': updated {kind} '{name}'.");
						break;
				}
			}
			catch (Exception ex)
			{
				string verb = action == DomInstallAction.Created ? "create" : "update";
				throw new InvalidOperationException($"DOM '{moduleId}': failed to {verb} {kind} '{name}': {ex.Message}", ex);
			}

			result.Add(action);
		}
	}

	/// <summary>
	/// Counts the DOM configuration objects that were created, updated or left unchanged by <see cref="DomModuleInstaller"/>.
	/// </summary>
	internal sealed class DomInstallResult
	{
		public int Created { get; private set; }

		public int Updated { get; private set; }

		public int Unchanged { get; private set; }

		public bool HasChanges => Created > 0 || Updated > 0;

		public override string ToString()
		{
			return $"{Created} created, {Updated} updated, {Unchanged} unchanged";
		}

		internal void Add(DomInstallAction action)
		{
			switch (action)
			{
				case DomInstallAction.Created:
					Created++;
					break;

				case DomInstallAction.Updated:
					Updated++;
					break;

				default:
					Unchanged++;
					break;
			}
		}
	}
}

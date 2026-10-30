namespace SLCSMMigrationImport.Migration
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.Net.Sections;

	/// <summary>
	/// Removes the legacy DOM model parts (definitions, sections and fields listed in <see cref="KnownIds"/>) that are left behind
	/// when a legacy system is upgraded in place, including the instances and values that use them.
	/// Only needed on such a system; on a clean new system nothing is found. The legacy data itself is in the export file.
	/// </summary>
	public sealed class ObsoleteDomCleanup
	{
		private const int PageSize = 500;

		private readonly Action<string> _log;
		private readonly bool _dryRun;

		public ObsoleteDomCleanup(Action<string> log, bool dryRun)
		{
			_log = log ?? (_ => { });
			_dryRun = dryRun;
		}

		public List<string> Results { get; } = new List<string>();

		public List<string> Failures { get; } = new List<string>();

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

		public void Clean(DomHelper helper)
		{
			string moduleId = helper.ModuleId;
			var definitions = helper.DomDefinitions.Read(new TRUEFilterElement<DomDefinition>())
				.Where(d => KnownIds.RemovedDefinitions.ContainsKey(d.ID.Id))
				.ToList();
			var allSections = helper.SectionDefinitions.Read(new TRUEFilterElement<SectionDefinition>());
			var sections = allSections.Where(s => KnownIds.RemovedSections.ContainsKey(s.GetID().Id)).ToList();
			var sectionsWithFields = allSections
				.OfType<CustomSectionDefinition>()
				.Where(s => !KnownIds.RemovedSections.ContainsKey(s.GetID().Id) && s.GetAllFieldDescriptors().Any(f => KnownIds.RemovedFields.ContainsKey(f.ID.Id)))
				.ToList();

			if (definitions.Count == 0 && sections.Count == 0 && sectionsWithFields.Count == 0)
			{
				Results.Add($"'{moduleId}': no legacy DOM model parts found.");
				return;
			}

			string what = $"definitions [{String.Join(", ", definitions.Select(d => d.Name))}], sections [{String.Join(", ", sections.Select(s => s.GetName()))}], "
				+ $"fields [{String.Join(", ", sectionsWithFields.SelectMany(s => s.GetAllFieldDescriptors()).Where(f => KnownIds.RemovedFields.ContainsKey(f.ID.Id)).Select(f => KnownIds.RemovedFields[f.ID.Id]))}]";
			_log($"Legacy DOM model parts in '{moduleId}': {what}.");

			// 1. Instances: delete those of removed definitions, strip removed sections and fields from the others.
			var toDelete = new List<DomInstance>();
			foreach (var definition in definitions)
			{
				toDelete.AddRange(ReadAll(helper, DomInstanceExposers.DomDefinitionId.Equal(definition.ID.Id)));
			}

			var deleteIds = new HashSet<Guid>(toDelete.Select(i => i.ID.Id));
			int cleaned = 0;
			var paging = helper.DomInstances.PreparePaging(new TRUEFilterElement<DomInstance>(), PageSize);
			while (paging.MoveToNextPage())
			{
				foreach (var instance in paging.GetCurrentPage().Where(i => !deleteIds.Contains(i.ID.Id) && StripRemoved(i)))
				{
					cleaned++;
					if (!_dryRun)
					{
						Try(() => helper.DomInstances.Update(instance), $"remove legacy values from instance {instance.ID.Id} in '{moduleId}'");
					}
				}
			}

			if (_dryRun)
			{
				Results.Add($"'{moduleId}': would delete {toDelete.Count} instance(s) of removed definitions, clean {cleaned} instance(s) and remove {what}.");
				return;
			}

			toDelete.ForEach(i => Try(() => helper.DomInstances.Delete(i), $"delete instance {i.ID.Id} in '{moduleId}'"));

			// 2. Model: removed fields, then definitions, then the sections they used.
			foreach (var section in sectionsWithFields)
			{
				Try(
					() =>
					{
						section.GetAllFieldDescriptors()
							.Where(f => KnownIds.RemovedFields.ContainsKey(f.ID.Id))
							.ToList()
							.ForEach(f => section.RemoveFieldDescriptor(f.ID));
						helper.SectionDefinitions.Update(section);
					},
					$"remove legacy fields from section '{section.GetName()}' in '{moduleId}'");
			}

			definitions.ForEach(d => Try(() => helper.DomDefinitions.Delete(d), $"delete definition '{d.Name}' in '{moduleId}'"));
			sections.ForEach(s => Try(() => helper.SectionDefinitions.Delete(s), $"delete section '{s.GetName()}' in '{moduleId}'"));

			Results.Add($"'{moduleId}': deleted {toDelete.Count} instance(s) of removed definitions, cleaned {cleaned} instance(s), removed {what}.");
		}

		private static IEnumerable<DomInstance> ReadAll(DomHelper helper, FilterElement<DomInstance> filter)
		{
			var paging = helper.DomInstances.PreparePaging(filter, PageSize);
			while (paging.MoveToNextPage())
			{
				foreach (var instance in paging.GetCurrentPage())
				{
					yield return instance;
				}
			}
		}

		private static bool StripRemoved(DomInstance instance)
		{
			bool changed = instance.Sections.RemoveAll(s => s.SectionDefinitionID != null && KnownIds.RemovedSections.ContainsKey(s.SectionDefinitionID.Id)) > 0;
			foreach (var section in instance.Sections)
			{
				foreach (var field in section.FieldValues.Where(f => KnownIds.RemovedFields.ContainsKey(f.FieldDescriptorID.Id)).ToList())
				{
					changed |= section.RemoveFieldValueById(field.FieldDescriptorID);
				}
			}

			return changed;
		}

		private void Try(Action action, string description)
		{
			try
			{
				action();
			}
			catch (Exception e)
			{
				string failure = $"Failed to {description}: {e.Message}";
				Failures.Add(failure);
				_log(failure);
			}
		}
	}
}

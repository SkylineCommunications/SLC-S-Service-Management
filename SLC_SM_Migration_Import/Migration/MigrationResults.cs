namespace SLCSMMigrationImport.Migration
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;

	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel.Status;

	public enum PlannedAction
	{
		Create,
		Update,
		Unchanged,
		Drop,
		Fail,
	}

	/// <summary>
	/// The outcome of planning a single legacy DOM instance.
	/// </summary>
	public sealed class PlannedItem
	{
		public PlannedItem(string moduleId, ExportedInstance source, string definitionName)
		{
			ModuleId = moduleId;
			InstanceId = source.Id;
			Name = source.Name;
			DefinitionId = source.DefinitionId;
			DefinitionName = definitionName;
			SourceStatusId = source.StatusId;
		}

		public string ModuleId { get; }

		public Guid InstanceId { get; }

		public string Name { get; }

		public Guid DefinitionId { get; }

		public string DefinitionName { get; }

		public string SourceStatusId { get; }

		public PlannedAction Action { get; set; }

		/// <summary>
		/// Gets or sets the instance as it must exist on the target system.
		/// </summary>
		public DomInstance Desired { get; set; }

		public DomInstance Existing { get; set; }

		public string TargetStatusId { get; set; }

		public List<DomStatusTransition> Transitions { get; set; } = new List<DomStatusTransition>();

		public string Error { get; set; }

		public List<string> Warnings { get; } = new List<string>();

		public List<DroppedValue> DroppedData { get; } = new List<DroppedValue>();

		/// <summary>
		/// Gets the instances (module, id) this item references, used to warn about references to failed items.
		/// </summary>
		public HashSet<InstanceKey> References { get; } = new HashSet<InstanceKey>();

		public InstanceKey Key => new InstanceKey(ModuleId, InstanceId);

		public string Label => $"{DefinitionName} '{Name}' ({InstanceId})";

		public void Fail(string error)
		{
			Action = PlannedAction.Fail;
			Error = error;
		}
	}

	/// <summary>
	/// Legacy data that could not be mapped onto the new DOM model.
	/// </summary>
	public sealed class DroppedValue
	{
		public string ModuleId { get; set; }

		public Guid InstanceId { get; set; }

		public string InstanceName { get; set; }

		public string Definition { get; set; }

		public string Section { get; set; }

		public string Field { get; set; }

		public string Value { get; set; }

		public string Reason { get; set; }

		public override string ToString()
		{
			string location = String.IsNullOrEmpty(Field) ? Section : $"{Section}.{Field}";
			return $"[{ModuleId}] {Definition} '{InstanceName}' ({InstanceId}) {location} = '{Value}': {Reason}";
		}
	}

	public struct InstanceKey : IEquatable<InstanceKey>
	{
		public InstanceKey(string moduleId, Guid id)
		{
			ModuleId = moduleId ?? String.Empty;
			Id = id;
		}

		public string ModuleId { get; }

		public Guid Id { get; }

		public bool Equals(InstanceKey other)
		{
			return Id == other.Id && String.Equals(ModuleId, other.ModuleId, StringComparison.OrdinalIgnoreCase);
		}

		public override bool Equals(object obj)
		{
			return obj is InstanceKey other && Equals(other);
		}

		public override int GetHashCode()
		{
			return (Id.GetHashCode() * 397) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(ModuleId);
		}

		public override string ToString()
		{
			return $"{ModuleId}/{Id}";
		}
	}

	public sealed class MigrationPlan
	{
		public List<PlannedItem> Items { get; } = new List<PlannedItem>();

		public List<string> Warnings { get; } = new List<string>();

		/// <summary>
		/// Gets errors that prevent the whole import (nothing will be written).
		/// </summary>
		public List<string> FatalErrors { get; } = new List<string>();
	}

	public enum ItemOutcome
	{
		Created,
		Updated,
		Skipped,
		Failed,
	}

	public sealed class ItemResult
	{
		public string ModuleId { get; set; }

		public Guid InstanceId { get; set; }

		public string Definition { get; set; }

		public string Name { get; set; }

		public ItemOutcome Outcome { get; set; }

		public string Message { get; set; }

		public string LegacyStatus { get; set; }

		public List<string> Warnings { get; set; } = new List<string>();

		public static ItemResult For(PlannedItem item, ItemOutcome outcome, string message)
		{
			return new ItemResult
			{
				ModuleId = item.ModuleId,
				InstanceId = item.InstanceId,
				Definition = item.DefinitionName,
				Name = item.Name,
				Outcome = outcome,
				Message = message,
				LegacyStatus = item.SourceStatusId,
				Warnings = item.Warnings.ToList(),
			};
		}

		public override string ToString()
		{
			var sb = new StringBuilder($"{Outcome.ToString().ToUpperInvariant()} [{ModuleId}] {Definition} '{Name}' ({InstanceId}): {Message}");
			foreach (string warning in Warnings)
			{
				sb.Append(Environment.NewLine).Append("    WARNING: ").Append(warning);
			}

			return sb.ToString();
		}
	}

	/// <summary>
	/// Result of an import run: one entry per legacy instance, the data that could not be mapped and a summary.
	/// </summary>
	public sealed class MigrationReport
	{
		public MigrationReport(bool dryRun)
		{
			DryRun = dryRun;
		}

		public bool DryRun { get; }

		public string SourceFile { get; set; }

		public string SourceHost { get; set; }

		public DateTime? ExportedAtUtc { get; set; }

		public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;

		/// <summary>
		/// Gets or sets how long the export took on the legacy system, as written in the export file.
		/// </summary>
		public string ExportDuration { get; set; }

		public TimeSpan ReadDuration { get; set; }

		public TimeSpan CleanupDuration { get; set; }

		public TimeSpan PlanDuration { get; set; }

		public TimeSpan ExecuteDuration { get; set; }

		/// <summary>
		/// Gets or sets the duration of the whole import, excluding writing the report files.
		/// </summary>
		public TimeSpan TotalDuration { get; set; }

		public static string FormatDuration(TimeSpan duration)
		{
			return $"{(int)duration.TotalHours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}.{duration.Milliseconds:D3}";
		}

		public string BuildTiming()
		{
			return $"total {FormatDuration(TotalDuration)} (reading export file {FormatDuration(ReadDuration)}, legacy DOM cleanup {FormatDuration(CleanupDuration)}, mapping {FormatDuration(PlanDuration)}, {(DryRun ? "dry run" : "writing to DOM")} {FormatDuration(ExecuteDuration)}).";
		}

		public List<string> FatalErrors { get; } = new List<string>();

		public List<string> GlobalWarnings { get; } = new List<string>();

		/// <summary>
		/// Gets what <see cref="ObsoleteDomCleanup"/> removed (or would remove in a dry run) per module.
		/// </summary>
		public List<string> ModelCleanup { get; } = new List<string>();

		public List<ItemResult> Items { get; } = new List<ItemResult>();

		public List<DroppedValue> DroppedData { get; } = new List<DroppedValue>();

		public int Count(ItemOutcome outcome) => Items.Count(i => i.Outcome == outcome);

		public int WarningCount => Items.Sum(i => i.Warnings.Count) + GlobalWarnings.Count;

		public bool HasFailures => FatalErrors.Count > 0 || Count(ItemOutcome.Failed) > 0;

		public void Add(PlannedItem item, ItemResult result)
		{
			Items.Add(result);
			DroppedData.AddRange(item.DroppedData);
		}

		/// <summary>
		/// Flags migrated items that reference items which failed to import, so broken links are visible.
		/// </summary>
		public void AddWarningsForReferencesToFailedItems(IEnumerable<PlannedItem> items)
		{
			var plannedItems = items.ToList();
			var failed = new HashSet<InstanceKey>(Items
				.Where(i => i.Outcome == ItemOutcome.Failed)
				.Select(i => new InstanceKey(i.ModuleId, i.InstanceId)));
			if (failed.Count == 0)
			{
				return;
			}

			var byKey = Items.GroupBy(i => new InstanceKey(i.ModuleId, i.InstanceId)).ToDictionary(g => g.Key, g => g.First());
			foreach (var item in plannedItems)
			{
				if (!byKey.TryGetValue(item.Key, out var result) || result.Outcome == ItemOutcome.Failed)
				{
					continue;
				}

				foreach (var reference in item.References.Where(failed.Contains))
				{
					result.Warnings.Add($"References {reference}, which failed to import. Re-run the import after fixing that item.");
				}
			}
		}

		public string BuildSummary()
		{
			var sb = new StringBuilder();
			sb.Append(DryRun ? "SMG migration DRY RUN (nothing was written)" : "SMG migration");
			sb.Append($": {Items.Count} items - created {Count(ItemOutcome.Created)}, updated {Count(ItemOutcome.Updated)}, skipped {Count(ItemOutcome.Skipped)}, failed {Count(ItemOutcome.Failed)}");
			sb.Append($"; {DroppedData.Count} unmapped values; {WarningCount} warnings.");
			if (FatalErrors.Count > 0)
			{
				sb.Append(" ABORTED: ").Append(String.Join(" ", FatalErrors));
			}

			return sb.ToString();
		}

		public void WriteText(TextWriter writer)
		{
			writer.WriteLine(BuildSummary());
			writer.WriteLine($"Source file: {SourceFile}");
			writer.WriteLine($"Exported from: {SourceHost} at {ExportedAtUtc:o} (UTC)" + (String.IsNullOrEmpty(ExportDuration) ? String.Empty : $", export took {ExportDuration}"));
			writer.WriteLine($"Import started: {StartedAtUtc:o} (UTC)");
			writer.WriteLine($"Import duration: {BuildTiming()}");
			writer.WriteLine();

			WriteList(writer, "FATAL ERRORS", FatalErrors.Count, FatalErrors);
			WriteList(writer, "GENERAL WARNINGS", GlobalWarnings.Count, GlobalWarnings);
			WriteList(writer, "LEGACY DOM MODEL CLEANUP", ModelCleanup.Count, ModelCleanup);

			writer.WriteLine("PER DEFINITION");
			foreach (var group in Items.GroupBy(i => $"[{i.ModuleId}] {i.Definition}").OrderBy(g => g.Key, StringComparer.Ordinal))
			{
				writer.WriteLine($"  {group.Key}: total {group.Count()}, created {group.Count(i => i.Outcome == ItemOutcome.Created)}, updated {group.Count(i => i.Outcome == ItemOutcome.Updated)}, skipped {group.Count(i => i.Outcome == ItemOutcome.Skipped)}, failed {group.Count(i => i.Outcome == ItemOutcome.Failed)}");
			}

			writer.WriteLine();
			WriteList(writer, "FAILED ITEMS", Count(ItemOutcome.Failed), Items.Where(i => i.Outcome == ItemOutcome.Failed).Select(i => i.ToString()));
			WriteList(writer, "DATA THAT COULD NOT BE MAPPED (dropped)", DroppedData.Count, DroppedData.Select(d => d.ToString()));
			WriteList(writer, "ALL ITEMS", Items.Count, Items.Select(i => i.ToString()));
		}

		private static void WriteList(TextWriter writer, string title, int count, IEnumerable<string> lines)
		{
			if (count == 0)
			{
				return;
			}

			writer.WriteLine($"{title} ({count})");
			foreach (string line in lines)
			{
				writer.WriteLine("  " + line);
			}

			writer.WriteLine();
		}
	}
}

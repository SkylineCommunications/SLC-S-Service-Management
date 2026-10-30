namespace SLCSMMigrationImport.Migration
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;

	/// <summary>
	/// Applies a <see cref="MigrationPlan"/> on the target system. Every item is written atomically: when a step fails, what was written for that item is reverted.
	/// </summary>
	public sealed class MigrationExecutor
	{
		private readonly IDomWriter _writer;
		private readonly Action<string> _log;

		public MigrationExecutor(IDomWriter writer, Action<string> log)
		{
			_writer = writer;
			_log = log ?? (_ => { });
		}

		public MigrationReport Execute(MigrationPlan plan, bool dryRun)
		{
			if (plan == null)
			{
				throw new ArgumentNullException(nameof(plan));
			}

			if (!dryRun && _writer == null)
			{
				throw new InvalidOperationException("A DOM writer is required when not running in dry-run mode.");
			}

			var report = new MigrationReport(dryRun);
			report.GlobalWarnings.AddRange(plan.Warnings);
			report.FatalErrors.AddRange(plan.FatalErrors);
			if (plan.FatalErrors.Count > 0)
			{
				return report;
			}

			int index = 0;
			foreach (var item in plan.Items)
			{
				index++;
				var result = Execute(item, dryRun);
				report.Add(item, result);
				_log($"[{index}/{plan.Items.Count}] {result}");

				// The instances are no longer needed once written; release them so large imports fit in memory.
				item.Desired = null;
				item.Existing = null;
			}

			report.AddWarningsForReferencesToFailedItems(plan.Items);
			return report;
		}

		private static string StatusSuffix(PlannedItem item)
		{
			return item.Transitions.Count == 0 ? String.Empty : $" Status set to '{item.TargetStatusId}' via {item.Transitions.Count} transition(s).";
		}

		private ItemResult Execute(PlannedItem item, bool dryRun)
		{
			switch (item.Action)
			{
				case PlannedAction.Drop:
					return ItemResult.For(item, ItemOutcome.Skipped, item.Error + " Not migrated; its data is listed in the dropped-data section.");
				case PlannedAction.Fail:
					return ItemResult.For(item, ItemOutcome.Failed, item.Error);
				case PlannedAction.Unchanged:
					return ItemResult.For(item, ItemOutcome.Skipped, "Already up to date on the target.");
				case PlannedAction.Create:
					return dryRun
						? ItemResult.For(item, ItemOutcome.Created, "Would be created." + StatusSuffix(item))
						: Create(item);
				case PlannedAction.Update:
					return dryRun
						? ItemResult.For(item, ItemOutcome.Updated, "Would be updated (overwritten with legacy data)." + StatusSuffix(item))
						: Update(item);
				default:
					return ItemResult.For(item, ItemOutcome.Failed, $"Unsupported action {item.Action}.");
			}
		}

		private ItemResult Create(PlannedItem item)
		{
			DomInstance current;
			try
			{
				current = _writer.Create(item.Desired) ?? item.Desired;
			}
			catch (Exception ex)
			{
				return ItemResult.For(item, ItemOutcome.Failed, "Create failed: " + Flatten(ex));
			}

			if (TryApplyTransitions(item, ref current, out string error))
			{
				return ItemResult.For(item, ItemOutcome.Created, "Created." + StatusSuffix(item));
			}

			try
			{
				_writer.Delete(current);
				return ItemResult.For(item, ItemOutcome.Failed, error + " The created instance was removed again (rollback).");
			}
			catch (Exception ex)
			{
				return ItemResult.For(item, ItemOutcome.Failed, $"{error} ROLLBACK FAILED, the instance remains on the system in status '{current.StatusId}': {Flatten(ex)}");
			}
		}

		private ItemResult Update(PlannedItem item)
		{
			var snapshot = (DomInstance)item.Existing.Clone();
			item.Desired.StatusId = item.Existing.StatusId;

			DomInstance current;
			try
			{
				current = _writer.Update(item.Desired) ?? item.Desired;
			}
			catch (Exception ex)
			{
				return ItemResult.For(item, ItemOutcome.Failed, "Update failed, the existing instance was not changed: " + Flatten(ex));
			}

			if (TryApplyTransitions(item, ref current, out string error))
			{
				return ItemResult.For(item, ItemOutcome.Updated, "Updated." + StatusSuffix(item));
			}

			try
			{
				snapshot.StatusId = current.StatusId;
				_writer.Update(snapshot);
				string statusNote = String.Equals(current.StatusId, item.Existing.StatusId, StringComparison.Ordinal)
					? String.Empty
					: $" Status transitions cannot be reverted; the instance stays in status '{current.StatusId}' (was '{item.Existing.StatusId}').";
				return ItemResult.For(item, ItemOutcome.Failed, error + " The previous field values were restored." + statusNote);
			}
			catch (Exception ex)
			{
				return ItemResult.For(item, ItemOutcome.Failed, $"{error} ROLLBACK FAILED, the instance keeps the new values in status '{current.StatusId}': {Flatten(ex)}");
			}
		}

		private bool TryApplyTransitions(PlannedItem item, ref DomInstance current, out string error)
		{
			error = null;
			foreach (var transition in item.Transitions)
			{
				try
				{
					current = _writer.DoStatusTransition(current, transition.Id) ?? current;
				}
				catch (Exception ex)
				{
					error = $"Status transition '{transition.Id}' ({transition.FromStatusId} -> {transition.ToStatusId}) failed: {Flatten(ex)}";
					return false;
				}
			}

			return true;
		}

		private static string Flatten(Exception ex)
		{
			var messages = new List<string>();
			for (var e = ex; e != null; e = e.InnerException)
			{
				messages.Add(e.Message);
			}

			return String.Join(" -> ", messages.Distinct());
		}
	}
}

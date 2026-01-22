namespace SLC_SM_IAS_Profiles.Presenters
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	public interface IReadOnlyNavigator
	{
		IEnumerable<DataRecordPage> GetPathPages();

		IEnumerable<DataRecord> GetAllRecords();

		DataRecordPage GetCurrentPage();

		bool CanGoBack();

		IEnumerable<DataRecord> GetCurrentSliceRecords();

		bool CanMovePreviousSlice();

		bool CanMoveNextSlice();

		int GetTotalSlicesForCurrentPage();

		int GetCurrentSliceIndex();
	}

	public class PageNavigator : IReadOnlyNavigator
	{
		private readonly Dictionary<DataRecordPage, int> _sliceIndexByPage = new Dictionary<DataRecordPage, int>();

		// Transient (unsaved) records per page; do NOT inject these into DataRecordPage.Records.
		// They are rendered when their assigned slice is visible.
		private readonly Dictionary<DataRecordPage, List<TransientRecord>> _transientRecordsByPage
			= new Dictionary<DataRecordPage, List<TransientRecord>>();

		private int _pageSize = 15;

		private DataRecordPage _root;

		public DataRecordPage CurrentPage { get; private set; }

		public bool CanGoBack => CurrentPage?.Previous != null;

		public int PageSize
		{
			get => _pageSize;
			set
			{
				if (value <= 0)
					throw new ArgumentOutOfRangeException(nameof(value), "PageSize must be > 0");

				if (_pageSize != value)
				{
					_pageSize = value;

					// reset slice indices and transient registrations when page size changes
					_sliceIndexByPage.Clear();
					_transientRecordsByPage.Clear();
				}
			}
		}

		public IEnumerable<DataRecordPage> GetPathPages()
		{
			var visited = new HashSet<DataRecordPage>();
			foreach (var page in Traverse(_root, visited))
			{
				yield return page;
			}
		}

		// Include transient records so StoreModels can see them when the presenter enumerates GetAllRecords().
		public IEnumerable<DataRecord> GetAllRecords()
		{
			foreach (var page in GetPathPages())
			{
				// committed records
				foreach (var r in page.Records)
					yield return r;

				// transient records for the page (in insertion order)
				if (_transientRecordsByPage.TryGetValue(page, out var list))
				{
					foreach (var tr in list)
						yield return tr.Record;
				}
			}
		}

		public DataRecordPage CreateRootPage(IEnumerable<DataRecord> records)
		{
			var root = new RootPage(records);
			_root = root;
			CurrentPage = root;
			EnsureSliceIndex(CurrentPage);
			return root;
		}

		public void AddRecordToCurrentPage(DataRecord record)
		{
			CurrentPage.AddRecord(record);
		}

		// Add transient record to the bottom of the current visible slice.
		// Do NOT modify CurrentPage.Records; keep transient separate.
		public void AddRecordToCurrentSliceEnd(DataRecord record)
		{
			if (CurrentPage == null)
			{
				CreateRootPage(new[] { record });
				return;
			}

			EnsureSliceIndex(CurrentPage);
			var idx = _sliceIndexByPage[CurrentPage];

			// register transient list for this page if absent
			if (!_transientRecordsByPage.TryGetValue(CurrentPage, out var list))
			{
				list = new List<TransientRecord>();
				_transientRecordsByPage[CurrentPage] = list;
			}

			// append transient record with the assigned slice index
			list.Add(new TransientRecord { Record = record, SliceIndex = idx });

			// ensure mappings are cleaned/clamped
			EnsureSliceIndex(CurrentPage);
		}

		public DataRecordPage PushChildPage(ProfileDefinitionDataRecord parentRecord, List<DataRecord> records)
		{
			if (CurrentPage == null)
				return CreateRootPage(records);

			var parentId = parentRecord.ProfileDefinition.ID;
			var child = CurrentPage.Children
				.OfType<ProfilePage>()
				.FirstOrDefault(p => p.ProfileDefinitionRecord.ProfileDefinition.ID == parentId);

			if (child == null)
			{
				child = new ProfilePage(records);
				CurrentPage.AddChild(child);
			}
			else
			{
				child.SetRecords(records);
			}

			child.SetProfileDefinition(parentRecord);
			CurrentPage = child;
			EnsureSliceIndex(CurrentPage);
			return CurrentPage;
		}

		public void GoBack(List<DataRecord> records)
		{
			if (CanGoBack)
				CurrentPage = CurrentPage.Previous;

			CurrentPage.SetRecords(records);
			// transient registrations were for the old page; clear for current page since we replaced records
			_transientRecordsByPage.Remove(CurrentPage);
			EnsureSliceIndex(CurrentPage);
		}

		public DataRecordPage GetCurrentPage()
		{
			return CurrentPage;
		}

		bool IReadOnlyNavigator.CanGoBack()
		{
			return CanGoBack;
		}

		// Rendered slice: take committed records slice (based on PageSize) then append transient records assigned to this slice.
		public IEnumerable<DataRecord> GetCurrentSliceRecords()
		{
			if (CurrentPage == null)
				return Enumerable.Empty<DataRecord>();

			// Remove stale transient entries for this page (e.g. saved or removed)
			CleanTransientMappingsForPage(CurrentPage);

			EnsureSliceIndex(CurrentPage);
			var idx = _sliceIndexByPage[CurrentPage];
			var sliceStart = idx * PageSize;

			var committed = CurrentPage.Records;
			var committedTotal = committed.Count;

			if (sliceStart >= committedTotal)
				// might still have transient records assigned to this slice even if no committed records remaining
				return GetTransientRecordsForSlice(CurrentPage, idx).ToList();

			// collect up to PageSize committed records starting at sliceStart
			var baseCommitted = committed.Skip(sliceStart).Take(PageSize).ToList();

			// append transient records assigned to this slice (in insertion order)
			var transientForSlice = GetTransientRecordsForSlice(CurrentPage, idx).ToList();

			// If there are fewer committed records than PageSize at the end of list, still show those committed,
			// plus transient records for this slice.
			var merged = new List<DataRecord>(baseCommitted.Count + transientForSlice.Count);
			merged.AddRange(baseCommitted);
			merged.AddRange(transientForSlice);

			return merged;
		}

		public int GetCurrentSliceIndex()
		{
			if (CurrentPage == null)
				return 0;

			EnsureSliceIndex(CurrentPage);
			return _sliceIndexByPage[CurrentPage];
		}

		public int GetCurrentSliceCount()
		{
			// total committed records + transient records (for informational uses)
			var committed = CurrentPage?.Records?.Count ?? 0;
			var transient = _transientRecordsByPage.TryGetValue(CurrentPage, out var list) ? list.Count : 0;
			return committed + transient;
		}

		public int GetTotalSlicesForCurrentPage()
		{
			return GetTotalSlicesForPage(CurrentPage);
		}

		public int GetTotalSlicesForPage(DataRecordPage page)
		{
			if (page == null)
				return 0;

			// Use committed records only for slice counting. Transient records are temporary and intentionally
			// do not affect the canonical slice count.
			var committedCount = page.Records?.Count ?? 0;
			return (int)Math.Ceiling(committedCount / (double)PageSize);
		}

		public bool CanMoveNextSlice()
		{
			if (CurrentPage == null)
				return false;

			return GetCurrentSliceIndex() < GetTotalSlicesForCurrentPage() - 1;
		}

		public bool CanMovePreviousSlice()
		{
			if (CurrentPage == null)
				return false;

			return GetCurrentSliceIndex() > 0;
		}

		public void MoveNextSlice()
		{
			if (CurrentPage == null)
				return;

			EnsureSliceIndex(CurrentPage);

			var total = GetTotalSlicesForCurrentPage();

			if (_sliceIndexByPage[CurrentPage] < total - 1)
				_sliceIndexByPage[CurrentPage]++;
		}

		public void MovePreviousSlice()
		{
			if (CurrentPage == null)
				return;

			EnsureSliceIndex(CurrentPage);

			if (_sliceIndexByPage[CurrentPage] > 0)
				_sliceIndexByPage[CurrentPage]--;
		}

		public void SetCurrentSliceIndex(int index)
		{
			if (CurrentPage == null)
				return;

			if (index < 0)
				throw new ArgumentOutOfRangeException(nameof(index), "index must be >= 0");

			var total = GetTotalSlicesForCurrentPage();
			_sliceIndexByPage[CurrentPage] = Math.Min(index, Math.Max(0, total - 1));
		}

		// Helper: transient match (new & unsaved)
		private bool IsNewUnsaved(DataRecord r)
		{
			return r != null && r.RecordType == RecordType.New && r.State == State.Updated;
		}

		private IEnumerable<DataRecord> GetTransientRecordsForSlice(DataRecordPage page, int sliceIndex)
		{
			if (!_transientRecordsByPage.TryGetValue(page, out var list))
				yield break;

			foreach (var tr in list)
			{
				if (tr.SliceIndex == sliceIndex && IsNewUnsaved(tr.Record))
					yield return tr.Record;
			}
		}

		private void CleanTransientMappingsForPage(DataRecordPage page)
		{
			if (page == null)
				return;

			if (!_transientRecordsByPage.TryGetValue(page, out var list))
				return;

			// Remove transient entries that are no longer unsaved new
			list.RemoveAll(tr => !IsNewUnsaved(tr.Record));

			if (list.Count == 0)
				_transientRecordsByPage.Remove(page);
		}

		private void EnsureSliceIndex(DataRecordPage page)
		{
			if (page == null)
				return;

			// Clean transient mappings before we compute totals
			CleanTransientMappingsForPage(page);

			if (!_sliceIndexByPage.ContainsKey(page))
				_sliceIndexByPage[page] = 0;

			var total = GetTotalSlicesForPage(page);
			if (_sliceIndexByPage[page] >= total)
				_sliceIndexByPage[page] = Math.Max(0, total - 1);
		}

		private IEnumerable<DataRecordPage> Traverse(DataRecordPage page, HashSet<DataRecordPage> visited)
		{
			if (page == null || visited.Contains(page))
				yield break;

			visited.Add(page);
			yield return page;

			foreach (var child in page.Children)
			{
				foreach (var c in Traverse(child, visited))
				{
					yield return c;
				}
			}
		}

		// Small private helper type to hold transient record + slice assignment
		private class TransientRecord
		{
			public DataRecord Record { get; set; }

			public int SliceIndex { get; set; }
		}
	}
}

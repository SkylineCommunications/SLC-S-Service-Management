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
					_sliceIndexByPage.Clear();
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

		public IEnumerable<DataRecord> GetAllRecords() => GetPathPages().SelectMany(p => p.Records);

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

		public DataRecordPage PushChildPage(ProfileDataRecord parentRecord, List<DataRecord> records)
		{
			if (CurrentPage == null)
				return CreateRootPage(records);

			var parentId = parentRecord.Profile.ID;
			var child = CurrentPage.Children
				.OfType<ProfilePage>()
				.FirstOrDefault(p => p.ProfileDataRecord.Profile.ID == parentId);

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
			EnsureSliceIndex(CurrentPage);
		}

		public DataRecordPage GetCurrentPage() => CurrentPage;

		bool IReadOnlyNavigator.CanGoBack() => CanGoBack;

		public IEnumerable<DataRecord> GetCurrentSliceRecords()
		{
			if (CurrentPage == null)
				return Enumerable.Empty<DataRecord>();

			EnsureSliceIndex(CurrentPage);
			var idx = _sliceIndexByPage[CurrentPage];
			return CurrentPage.Records.Skip(idx * PageSize).Take(PageSize).ToList();
		}

		public int GetCurrentSliceIndex()
		{
			if (CurrentPage == null)
				return 0;

			EnsureSliceIndex(CurrentPage);
			return _sliceIndexByPage[CurrentPage];
		}

		public int GetTotalSlicesForCurrentPage()
		{
			if (CurrentPage == null)
				return 0;

			var count = CurrentPage.Records?.Count ?? 0;
			return (int)Math.Ceiling(count / (double)PageSize);
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

		private void EnsureSliceIndex(DataRecordPage page)
		{
			if (page == null)
				return;

			if (!_sliceIndexByPage.ContainsKey(page))
				_sliceIndexByPage[page] = 0;

			var total = GetTotalSlicesForCurrentPage();
			if (total > 0 && _sliceIndexByPage[page] >= total)
				_sliceIndexByPage[page] = total - 1;
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
	}
}

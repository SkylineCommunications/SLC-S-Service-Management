namespace SLC_SM_IAS_Profiles.Views
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Helper;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;
	using SLC_SM_IAS_Profiles.Presenters;

	public class ProfileDefinitionView : ConfigurationView
	{
		private Dictionary<Guid, List<Models.ProfileDefinition>> _parentMap;

		public ProfileDefinitionView(
			IEngine engine,
			List<Option<Models.ConfigurationUnit>> cachedUnits,
			EventHandlers callbacks) : base(engine, cachedUnits, callbacks)
		{
		}

		public override void BuildUI(
			IReadOnlyNavigator context,
			IReadOnlyList<Models.ConfigurationParameter> allConfigurationParameters,
			IReadOnlyList<Models.ProfileDefinition> allProfileDefinitions)
		{
			Clear();

			var page = context.GetCurrentPage();
			int row = 0;

			BuildHeader(context, row);

			AddWidget(new WhiteSpace(), ++row, 0);

			var configurationRecords = page.Records
				.Where(x => x.State != State.Removed && x is ConfigurationDataRecord)
				.Cast<ConfigurationDataRecord>();

			if (context.GetCurrentPage() is ProfilePage)
			{
				BuildConfigurationParameterHeader(context, ++row);

				foreach (var record in configurationRecords)
				{
					ConfigurationRowData rowData = BuildRowData(context, allConfigurationParameters, record, ++row);
					BuildRow(rowData);
				}

				AddConfigurationParameterButton(++row);
			}

			AddWidget(new WhiteSpace(), ++row, 0);

			BuildProfileDefinitionHeader(context, ++row);

			var profileDefinitionRecords = page.Records
				.Where(x => x.State != State.Removed && x is ProfileDefinitionDataRecord)
				.Cast<ProfileDefinitionDataRecord>();

			_parentMap = BuildParentMap(allProfileDefinitions);
			foreach (var record in profileDefinitionRecords)
			{
				ProfileDefinitionRowData rowData = BuildRowData(context, record, allProfileDefinitions, ++row);
				BuildRow(rowData);
			}

			AddProfileDefinitionButton(++row);

			BuildFooter(++row, context.CanGoBack());
		}

		private void BuildProfileDefinitionHeader(IReadOnlyNavigator context, int row)
		{
			var lblReference = new Label("PROFILE DEFINITION");
			var lblName = new Label("NAME");
			var lblMultipleAllowed = new Label("ALLOW MULTIPLE");
			var lblMandatory = new Label("MANDATORY");

			AddWidget(lblName, row, 0);
			if (context.GetCurrentPage() is ProfilePage)
			{
				AddWidget(lblReference, row, 1);
				AddWidget(lblMultipleAllowed, row, 2);
				AddWidget(lblMandatory, row, 3);
			}
		}

		private void AddProfileDefinitionButton(int row)
		{
			var btnAddProfileDefinition = new Button("➕ Profile Definition");
			btnAddProfileDefinition.Pressed += (sender, args) => Callbacks.Common.Handle_Add_ProfileDefinition_Pressed();
			AddWidget(btnAddProfileDefinition, row, 0);
		}

		private ProfileDefinitionRowData BuildRowData(
			IReadOnlyNavigator context,
			ProfileDefinitionDataRecord record,
			IReadOnlyList<Models.ProfileDefinition> allProfileDefinitions,
			int row)
		{
			var options = allProfileDefinitions.ToArray();
			if (context.GetCurrentPage() is ProfilePage profilePage)
			{
				var parentProfileDefinition = profilePage.ProfileDefinitionRecord.ProfileDefinition;

				var siblings = profilePage.Records
					.OfType<ProfileDefinitionDataRecord>()
					.Where(r => r.State != State.Removed)
					.Select(r => r.ProfileDefinition);

				var ancestors = GetAllAncestors(parentProfileDefinition);

				options = allProfileDefinitions
					.Except(new[] { parentProfileDefinition }, ProfileDefinitionIdComparer.Instance)
					.Except(ancestors, ProfileDefinitionIdComparer.Instance)
					.Except(siblings, ProfileDefinitionIdComparer.Instance)
					.DistinctBy(p => p.ID)
					.ToArray();
			}

			return new ProfileDefinitionRowData
			{
				Record = record,
				Page = context.GetCurrentPage(),
				CachedUnits = CachedUnits,
				ReferenceOptions = options,
				Callbacks = Callbacks,
				RowIndex = row,
			};
		}

		private Dictionary<Guid, List<Models.ProfileDefinition>> BuildParentMap(
			IEnumerable<Models.ProfileDefinition> allDefinitions)
		{
			var parents = new Dictionary<Guid, List<Models.ProfileDefinition>>();

			foreach (var p in allDefinitions)
			{
				foreach (var childRef in p.ProfileDefinitions)
				{
					if (!parents.TryGetValue(childRef.ProfileDefinitionReference, out var list))
						parents[childRef.ProfileDefinitionReference] = list = new List<Models.ProfileDefinition>();

					list.Add(p);
				}
			}

			return parents;
		}

		private List<Models.ProfileDefinition> GetAllAncestors(Models.ProfileDefinition profileDefinition)
		{
			var ancestors = new List<Models.ProfileDefinition>();
			var visited = new HashSet<Guid>();
			var queue = new Queue<Models.ProfileDefinition>();

			if (!_parentMap.TryGetValue(profileDefinition.ID, out var directParents))
				return ancestors;

			foreach (var p in directParents)
			{
				visited.Add(p.ID);
				queue.Enqueue(p);
			}

			while (queue.Count > 0)
			{
				var current = queue.Dequeue();
				ancestors.Add(current);

				if (_parentMap.TryGetValue(current.ID, out var parentList))
				{
					foreach (var parent in parentList)
					{
						if (visited.Add(parent.ID))
							queue.Enqueue(parent);
					}
				}
			}

			return ancestors;
		}
	}

	public sealed class ProfileDefinitionIdComparer : IEqualityComparer<Models.ProfileDefinition>
	{
		public static readonly ProfileDefinitionIdComparer Instance = new ProfileDefinitionIdComparer();

		public bool Equals(Models.ProfileDefinition x, Models.ProfileDefinition y)
			=> x?.ID == y?.ID;

		public int GetHashCode(Models.ProfileDefinition obj)
			=> obj.ID.GetHashCode();
	}
}
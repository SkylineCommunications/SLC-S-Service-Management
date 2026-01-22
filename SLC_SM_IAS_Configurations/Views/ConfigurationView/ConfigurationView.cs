namespace SLC_SM_IAS_Profiles.Views
{
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using Library;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Helper;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;
	using SLC_SM_IAS_Profiles.Presenters;

	public class ConfigurationView : Dialog
	{
		private const int DEFAULT_BUTTON_WIDTH = 100;

		public ConfigurationView(
			IEngine engine,
			List<Option<Models.ConfigurationUnit>> cachedUnits,
			EventHandlers callbacks) : base(engine)
		{
			Callbacks = callbacks;
			CachedUnits = cachedUnits;

			Title = "Manage Configurations";
			MinWidth = Defaults.DialogMinWidth;

			BtnCancel.Pressed += (sender, args) => throw new ScriptAbortException("OK");
			BtnSave.Pressed += (sender, args) => callbacks.Common.Handle_Update_Pressed();
			BtnBack.Pressed += (sender, args) => callbacks.Common.Handle_GoBack_Pressed();

			BtnPrevious.Pressed += (sender, args) => callbacks.Common.Handle_GoTo_Previous_Page_Pressed();
			BtnNext.Pressed += (sender, args) => callbacks.Common.Handle_GoTo_Next_Page_Pressed();
		}

		public Button BtnSave { get; } = new Button("Save") { Style = ButtonStyle.CallToAction, Width = DEFAULT_BUTTON_WIDTH };

		public Button BtnCancel { get; } = new Button("Cancel") { Width = DEFAULT_BUTTON_WIDTH};

		public Button BtnBack { get; } = new Button("Back") { Width = DEFAULT_BUTTON_WIDTH };

		public Button BtnPrevious { get; } = new Button("Previous") { Width = DEFAULT_BUTTON_WIDTH };

		public Button BtnNext { get; } = new Button("Next") { Width = DEFAULT_BUTTON_WIDTH };

		protected List<Option<Models.ConfigurationUnit>> CachedUnits { get; }

		protected EventHandlers Callbacks { get; }

		public virtual void BuildUI(
			IReadOnlyNavigator context,
			IReadOnlyList<Models.ConfigurationParameter> allConfigurationParameters,
			IReadOnlyList<Models.ProfileDefinition> allProfileDefinitions)
		{
			Clear();

			var page = context.GetCurrentPage();
			int row = 0;

			BuildHeader(context, row);

			AddWidget(new WhiteSpace(), ++row, 0);

			BuildConfigurationParameterHeader(context, ++row);

			var configurationRecords = context.GetCurrentSliceRecords()
				.Where(x => x.State != State.Removed && x is ConfigurationDataRecord)
				.Cast<ConfigurationDataRecord>();

			foreach (var record in configurationRecords)
			{
				ConfigurationRowData rowData = BuildRowData(context, allConfigurationParameters, record, ++row);
				BuildRow(rowData);
			}

			AddConfigurationParameterButton(++row);

			BuildFooter(++row, context.CanGoBack());
		}

		protected void BuildRow(RowData rowData)
		{
			var row = RowFactory.Create(rowData);
			row.Configure();
			row.BuildRow(this);
		}

		protected void BuildHeader(IReadOnlyNavigator context, int row)
		{
			var lblTitle = new Label();
			lblTitle.Style = TextStyle.Heading;

			var names = new List<string>();

			for (var page = context.GetCurrentPage();
				page is ProfilePage profilePage;
				page = page.Previous)
			{
				names.Add(profilePage.ProfileDefinitionRecord.ProfileDefinition.Name);
			}

			names.Reverse();

			var path = "Home" + (names.Count > 0 ? " > " + string.Join(" > ", names) : "");

			lblTitle.Text = path;

			AddWidget(lblTitle, row, 0);

			if (!(this is ProfileDefinitionView))
			{
				var pageLabel = new Label();
				pageLabel.Text = $"Page {context.GetCurrentSliceIndex() + 1} of {context.GetTotalSlicesForCurrentPage()}";

				AddWidget(pageLabel, row, 10, HorizontalAlignment.Right);

				BtnPrevious.IsEnabled = context.CanMovePreviousSlice();
				BtnNext.IsEnabled = context.CanMoveNextSlice();

				AddWidget(BtnPrevious, row, 11);
				AddWidget(BtnNext, row, 12);
			}
		}

		protected void BuildConfigurationParameterHeader(IReadOnlyNavigator context, int row)
		{
			var lblName = new Label("NAME");
			var lblReference = new Label("CONFIGURATION PARAMETER");
			var lblMultipleAllowed = new Label("ALLOW MULTIPLE");
			var lblMandatory = new Label("MANDATORY");
			var lblType = new Label("TYPE");
			var lblUnit = new Label("UNIT");
			var lblStart = new Label("START");
			var lblEnd = new Label("END");
			var lblStop = new Label("STEP SIZE");
			var lblDecimals = new Label("DECIMALS");
			var lblDefault = new Label("DEFAULT VALUE");
			var lblValues = new Label("SETTINGS");

			AddWidget(lblName, row, 0);

			if (context.GetCurrentPage() is ProfilePage)
			{
				AddWidget(lblReference, row, 1);
				AddWidget(lblMultipleAllowed, row, 2);
				AddWidget(lblMandatory, row, 3);
			}

			AddWidget(lblType, row, 4);
			AddWidget(lblUnit, row, 5);
			AddWidget(lblStart, row, 6);
			AddWidget(lblEnd, row, 7);
			AddWidget(lblStop, row, 8);
			AddWidget(lblDecimals, row, 9);
			AddWidget(lblDefault, row, 10);
			AddWidget(lblValues, row, 11);
		}

		protected ConfigurationRowData BuildRowData(
			IReadOnlyNavigator context,
			IReadOnlyList<Models.ConfigurationParameter> allConfigurationParameters,
			ConfigurationDataRecord record,
			int row)
		{
			IEnumerable<Models.ConfigurationParameter> options = allConfigurationParameters;
			if (context.GetCurrentPage() is ProfilePage profilePage)
			{
				var siblings = profilePage.Records
					.OfType<ConfigurationDataRecord>()
					.Where(r => r.State != State.Removed)
					.Select(r => r.ConfigurationParameter);

				options = allConfigurationParameters
					.Except(new[] { record.ConfigurationParameter }, ConfigurationParameterIdComparer.Instance)
					.Except(siblings, ConfigurationParameterIdComparer.Instance)
					.DistinctBy(c => c.ID);
			}

			return new ConfigurationRowData
			{
				Record = record,
				Page = context.GetCurrentPage(),
				CachedUnits = CachedUnits,
				ReferenceOptions = options,
				Callbacks = Callbacks,
				RowIndex = row,
			};
		}

		protected int AddConfigurationParameterButton(int row)
		{
			var btnAddConfiguration = new Button("➕ Parameter");
			btnAddConfiguration.Pressed += (sender, args) => Callbacks.Common.Handle_Add_Configuration_Pressed();
			AddWidget(btnAddConfiguration, row, 0);
			return row;
		}

		protected void BuildFooter(int row, bool canGoBack)
		{
			BtnBack.IsVisible = canGoBack;

			AddWidget(new WhiteSpace(), ++row, 0);

			AddWidget(BtnBack, ++row, 0);
			AddWidget(BtnCancel, row, 11);
			AddWidget(BtnSave, row, 12);
		}
	}

	public sealed class ConfigurationParameterIdComparer : IEqualityComparer<Models.ConfigurationParameter>
	{
		public static readonly ConfigurationParameterIdComparer Instance = new ConfigurationParameterIdComparer();

		public bool Equals(Models.ConfigurationParameter x, Models.ConfigurationParameter y)
			=> x?.ID == y?.ID;

		public int GetHashCode(Models.ConfigurationParameter obj)
			=> obj.ID.GetHashCode();
	}
}
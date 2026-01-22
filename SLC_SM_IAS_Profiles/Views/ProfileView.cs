namespace SLC_SM_IAS_Profiles.Views
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Runtime.Remoting.Contexts;
	using Library;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;
	using SLC_SM_IAS_Profiles.Presenters;

	public class ProfileView : Dialog
	{
		private const int DEFAULT_BUTTON_WIDTH = 100;

		public ProfileView(
			IEngine engine,
			List<Option<Models.ConfigurationUnit>> cachedUnits,
			EventHandlers callbacks) : base(engine)
		{
			Callbacks = callbacks;
			CachedUnits = cachedUnits;

			Title = "Manage Configurations";
			MinWidth = Defaults.DialogMinWidth;

			BtnCancel.Pressed += (sender, args) => throw new ScriptAbortException("OK");
			BtnUpdate.Pressed += (sender, args) => callbacks.Common.Handle_Update_Pressed();
			BtnBack.Pressed += (sender, args) => callbacks.Common.Handle_GoBack_Pressed();
		}

		public Button BtnUpdate { get; } = new Button("Save") { Style = ButtonStyle.CallToAction, Width = DEFAULT_BUTTON_WIDTH };

		public Button BtnCancel { get; } = new Button("Cancel") { Width = DEFAULT_BUTTON_WIDTH };

		public Button BtnBack { get; } = new Button("Back") { Width = DEFAULT_BUTTON_WIDTH };

		private List<Option<Models.ConfigurationUnit>> CachedUnits { get; }

		private EventHandlers Callbacks { get; }

		public void BuildUI(
			IReadOnlyNavigator context,
			IReadOnlyList<Models.ConfigurationParameter> allConfigurationParameters,
			IReadOnlyList<Models.ProfileDefinition> allProfileDefinitions)
		{
			Clear();

			var page = context.GetCurrentPage();
			int row = 0;

			BuildTitle(context, row);

			if (context.GetCurrentPage() is ProfilePage)
			{
				BuildConfigurationParameterHeader(++row);

				var configurationRecords = page.Records
					.Where(x => x.State != State.Removed && x is ConfigurationDataRecord)
					.Cast<ConfigurationDataRecord>();

				foreach (var record in configurationRecords)
				{
					ConfigurationRowData rowData = BuildConfigurationRowData(context, record, ++row);
					BuildRow(rowData);
				}

				AddNewConfigurationValueDropDown(context, allConfigurationParameters, ++row);
			}

			BuildProfileDefinitionHeader(++row);

			var profileRecords = page.Records
				.Where(x => x.State != State.Removed && x is ProfileDataRecord)
				.Cast<ProfileDataRecord>();

			foreach (var record in profileRecords)
			{
				ProfileRowData rowData = BuildProfileRowData(context, record, ++row);
				BuildRow(rowData);
			}

			AddNewProfileDropDown(context, allProfileDefinitions, ++row);

			BuildFooter(++row, context.CanGoBack());
		}

		private void BuildTitle(IReadOnlyNavigator context, int row)
		{
			var lblTitle = new Label();
			lblTitle.Style = TextStyle.Heading;
			var names = new List<string>();

			for (var page = context.GetCurrentPage();
				page is ProfilePage profilePage;
				page = page.Previous)
			{
				names.Add(profilePage.ProfileDataRecord.Profile.Name);
			}

			names.Reverse();

			var path = "Home" + (names.Count > 0 ? " > " + string.Join(" > ", names) : string.Empty);

			lblTitle.Text = path;

			AddWidget(lblTitle, row, 0, 1, 10);
		}

		private void BuildConfigurationParameterHeader(int row)
		{
			var lblLabel = new Label("LABEL");
			var lblReference = new Label("CONFIGURATION PARAMETER");
			var lblType = new Label("TYPE");
			var lblValue = new Label("VALUE");
			var lblUnit = new Label("UNIT");
			var lblStart = new Label("START");
			var lblEnd = new Label("END");
			var lblStop = new Label("STEP SIZE");
			var lblDecimals = new Label("DECIMALS");
			var lblSettings = new Label("SETTINGS");

			AddWidget(lblLabel, row, 0);
			AddWidget(lblReference, row, 1);
			AddWidget(lblType, row, 2);
			AddWidget(lblValue, row, 3);
			AddWidget(lblUnit, row, 4);
			AddWidget(lblStart, row, 5);
			AddWidget(lblEnd, row, 6);
			AddWidget(lblStop, row, 7);
			AddWidget(lblDecimals, row, 8);
			AddWidget(lblSettings, row, 9);
		}

		private void BuildRow(RowData rowData)
		{
			var row = RowFactory.Create(rowData);
			row.Configure();
			row.BuildRow(this);
		}

		private void BuildFooter(int row, bool canGoBack)
		{
			BtnBack.IsVisible = canGoBack;

			AddWidget(new WhiteSpace(), ++row, 0);

			AddWidget(BtnBack, ++row, 0);
			AddWidget(BtnCancel, row, 9);
			AddWidget(BtnUpdate, row, 10);
		}

		private int AddNewConfigurationValueDropDown(
			IReadOnlyNavigator context,
			IReadOnlyList<Models.ConfigurationParameter> allConfigurationParameters,
			int row)
		{
			var dropdownAddParameter = new DropDown<Models.ConfigurationParameter>();

			List<Models.ConfigurationParameter> parameters;
			if (context.GetCurrentPage() is ProfilePage profilePage)
			{
				parameters = GetEligibleConfigurationParameters(context, allConfigurationParameters, profilePage);
			}
			else
			{
				parameters = allConfigurationParameters.ToList();
			}

			var options = parameters.Select(p => new Option<Models.ConfigurationParameter>(p.Name, p)).ToList();
			options.Insert(0, new Option<Models.ConfigurationParameter>("- Add Parameter -", null));

			dropdownAddParameter.Options = options;
			dropdownAddParameter.Changed += (sender, args) => Callbacks.Common.Handle_Add_Configuration_Dropdown_Changed(args);

			AddWidget(dropdownAddParameter, row, 0);
			return row;
		}

		private void BuildProfileDefinitionHeader(int row)
		{
			var lblLabel = new Label("LABEL");
			var lblDefinition = new Label("PROFILE DEFINITION");

			AddWidget(lblLabel, row, 0);
			AddWidget(lblDefinition, row, 1);
			AddWidget(new Label(), row, 2);
			AddWidget(new Label(), row, 3);
			AddWidget(new Label(), row, 4);
			AddWidget(new Label(), row, 5);
			AddWidget(new Label(), row, 6);
			AddWidget(new Label(), row, 7);
			AddWidget(new Label(), row, 8);
			AddWidget(new Label(), row, 9);
		}

		private int AddNewProfileDropDown(
			IReadOnlyNavigator context,
			IReadOnlyList<Models.ProfileDefinition> allProfileDefinitions,
			int row)
		{
			var dropdownAddProfile = new DropDown<Models.ProfileDefinition>();

			List<Models.ProfileDefinition> profileDefinitions;
			if (context.GetCurrentPage() is ProfilePage profilePage)
			{
				profileDefinitions = GetEligibleProfileDefinitions(context, allProfileDefinitions, profilePage);
			}
			else
			{
				profileDefinitions = allProfileDefinitions.ToList();
			}

			var options = profileDefinitions.Select(pd => new Option<Models.ProfileDefinition>(pd.Name, pd)).ToList();
			options.Insert(0, new Option<Models.ProfileDefinition>("- Add Profile -", null));

			dropdownAddProfile.Options = options;
			dropdownAddProfile.Changed += (sender, args) => Callbacks.Common.Handle_Add_Profile_Dropdown_Changed(args);

			AddWidget(dropdownAddProfile, row, 0);

			return row;
		}

		private List<Models.ProfileDefinition> GetEligibleProfileDefinitions(
			IReadOnlyNavigator context,
			IReadOnlyList<Models.ProfileDefinition> allProfileDefinitions,
			ProfilePage profilePage)
		{
			List<Models.ProfileDefinition> options;
			var record = profilePage.ProfileDataRecord;

			var multiples = record.ReferredProfileDefinition.ProfileDefinitions
				.Where(pd => pd.AllowMultiple)
				.Select(pd => pd.ProfileDefinitionReference);

			var siblings = context.GetCurrentPage().Records
				.OfType<ProfileDataRecord>()
				.Where(r => r.State != State.Removed)
				.Select(r => r.ReferredProfileDefinition.ID);

			var singles = record.ReferredProfileDefinition.ProfileDefinitions
				.Where(pd => !pd.AllowMultiple && !pd.Mandatory)
				.Select(pd => pd.ProfileDefinitionReference)
				.Except(siblings);

			var ids = multiples
				.Concat(singles)
				.Distinct();

			options = allProfileDefinitions
				.Where(pd => ids.Contains(pd.ID))
				.ToList();

			return options;
		}

		private List<Models.ConfigurationParameter> GetEligibleConfigurationParameters(
			IReadOnlyNavigator context,
			IReadOnlyList<Models.ConfigurationParameter> allConfigurationParameters,
			ProfilePage profilePage)
		{
			List<Models.ConfigurationParameter> options;
			var record = profilePage.ProfileDataRecord;

			var multiples = record.ReferredProfileDefinition.ConfigurationParameters
				.Where(cp => cp.AllowMultiple)
				.Select(cp => cp.ConfigurationParameter);

			var siblings = context.GetCurrentPage().Records
				.OfType<ConfigurationDataRecord>()
				.Where(r => r.State != State.Removed)
				.Select(r => r.ReferredConfigurationParameter.ID);

			var singles = record.ReferredProfileDefinition.ConfigurationParameters
				.Where(cp => !cp.AllowMultiple && !cp.Mandatory)
				.Select(cp => cp.ConfigurationParameter)
				.Except(siblings);

			var ids = multiples
				.Concat(singles)
				.Distinct();

			options = allConfigurationParameters
				.Where(cp => ids.Contains(cp.ID))
				.ToList();

			return options;
		}

		private ConfigurationRowData BuildConfigurationRowData(
			IReadOnlyNavigator context,
			ConfigurationDataRecord record,
			int row)
		{
			bool canDelete = CanDeleteMandatoryConfiguration(context, record);

			return new ConfigurationRowData
			{
				Record = record,
				Page = context.GetCurrentPage(),
				CachedUnits = CachedUnits,
				ReferenceOptions = new List<Models.ConfigurationParameter>(),
				Callbacks = Callbacks,
				RowIndex = row,
				CanDelete = canDelete,
			};
		}

		private ProfileRowData BuildProfileRowData(
			IReadOnlyNavigator context,
			ProfileDataRecord record,
			int row)
		{
			bool canDelete = CanDeleteMandatoryProfile(context, record);

			return new ProfileRowData
			{
				Record = record,
				Page = context.GetCurrentPage(),
				CachedUnits = CachedUnits,
				ReferenceOptions = new List<Models.ProfileDefinition>(),
				Callbacks = Callbacks,
				RowIndex = row,
				CanDelete = canDelete,
			};
		}

		private bool CanDeleteMandatoryConfiguration(IReadOnlyNavigator context, ConfigurationDataRecord record)
		{
			var page = context.GetCurrentPage() as ProfilePage;
			if (page == null)
				return true;

			var id = record.ReferredConfigurationParameter.ID;

			if (!page.ProfileDataRecord.ReferredProfileDefinition.ConfigurationParameters
					.Any(cp => cp.Mandatory && cp.ConfigurationParameter == id))
				return true;

			return page.Records
				.OfType<ConfigurationDataRecord>()
				.Count(r => r.ReferredConfigurationParameter.ID == id
							&& r.State != State.Removed) > 1;
		}

		private bool CanDeleteMandatoryProfile(IReadOnlyNavigator context, ProfileDataRecord record)
		{
			var page = context.GetCurrentPage() as ProfilePage;
			if (page == null)
				return true;

			var id = record.ReferredProfileDefinition.ID;

			if (!page.ProfileDataRecord.ReferredProfileDefinition.ProfileDefinitions
					.Any(pd => pd.Mandatory && pd.ProfileDefinitionReference == id))
				return true;

			return page.Records
				.OfType<ProfileDataRecord>()
				.Count(r => r.ReferredProfileDefinition.ID == id
							&& r.State != State.Removed) > 1;
		}
	}
}
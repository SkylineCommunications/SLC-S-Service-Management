namespace SLC_SM_IAS_Profiles.Views
{
	using System.Collections.Generic;
	using System.Linq;
	using Library;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;
	using SLC_SM_IAS_Profiles.Presenters;

	public class ProfileDefinitionView : ConfigurationView
	{
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

			if (page is ProfilePage)
			{
				var configurationRecords = page.Records
					.Where(x => x.State != State.Removed && x is ConfigurationDataRecord)
					.Cast<ConfigurationDataRecord>();

				BuildConfigurationParameterHeader(context, ++row);

				foreach (var record in configurationRecords)
				{
					ConfigurationRowData rowData = BuildRowData(context, allConfigurationParameters, record, ++row);
					BuildRow(rowData);
				}

				AddConfigurationParameterButton(++row);
			}
			else
			{
				BuildProfileDefinitionHeader(++row);

				var profileDefinitionRecords = page.Records
					.Where(x => x.State != State.Removed && x is ProfileDefinitionDataRecord)
					.Cast<ProfileDefinitionDataRecord>();

				foreach (var record in profileDefinitionRecords)
				{
					ProfileDefinitionRowData rowData = BuildRowData(context, record, ++row);
					BuildRow(rowData);
				}

				AddProfileDefinitionButton(++row);
			}

			AddWidget(new WhiteSpace(), ++row, 0);

			BuildFooter(++row, context.CanGoBack());
		}

		private void BuildProfileDefinitionHeader(int row)
		{
			AddWidget(new Label("NAME"), row, 0);
		}

		private void AddProfileDefinitionButton(int row)
		{
			var btnAddProfileDefinition = new Button($"{Defaults.SymbolPlus} Profile Definition");
			btnAddProfileDefinition.Pressed += (sender, args) => Callbacks.Common.Handle_Add_ProfileDefinition_Pressed();
			AddWidget(btnAddProfileDefinition, row, 0);
		}

		private ProfileDefinitionRowData BuildRowData(
			IReadOnlyNavigator context,
			ProfileDefinitionDataRecord record,
			int row)
		{
			return new ProfileDefinitionRowData
			{
				Record = record,
				Page = context.GetCurrentPage(),
				CachedUnits = CachedUnits,
				Callbacks = Callbacks,
				RowIndex = row,
			};
		}
	}
}
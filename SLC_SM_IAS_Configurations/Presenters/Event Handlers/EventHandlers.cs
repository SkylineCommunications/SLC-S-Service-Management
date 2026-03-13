namespace SLC_SM_IAS_Profiles.Presenters
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations;

	public class EventHandlers
	{
		public EventHandlers(IEngine engine, ConfigurationPresenter presenter)
		{
			ConfigurationParameter = new ConfigurationParameterEventHandlers(engine, presenter);
			ProfileDefinition = new ProfileDefinitionEventHandlers(engine, presenter);
			Common = new CommonEventHandlers(engine, presenter);
		}

		public ConfigurationParameterEventHandlers ConfigurationParameter { get; set; }

		public ProfileDefinitionEventHandlers ProfileDefinition { get; set; }

		public CommonEventHandlers Common { get; set; }
	}

	public abstract class AbstractEventHandlers
	{
		protected IEngine engine;
		protected ConfigurationPresenter presenter;

		protected AbstractEventHandlers(IEngine engine, ConfigurationPresenter presenter)
		{
			this.engine = engine;
			this.presenter = presenter;
		}

		protected Models.ConfigurationParameter CreateNewConfigurationParameter(PageNavigator navigator)
		{
			var records = navigator.CurrentPage.Records;

			var id = Guid.NewGuid();
			return new Models.ConfigurationParameter
			{
				ID = id,
				Name = $"Parameter #{records.Count(r => r is ConfigurationDataRecord) + 1:000}",
			};
		}

		protected void AddConfigurationParameterReference(ProfileDefinitionDataRecord record, Guid id)
		{
			record.ProfileDefinition.ConfigurationParameters.Add(new Models.ReferencedConfigurationParameters
			{
				AllowMultiple = false,
				Mandatory = false,
				ConfigurationParameter = id,
			});

			record.State = State.Updated;
		}

		protected void AddProfileDefinitionReference(ProfileDefinitionDataRecord record, Guid id)
		{
			record.ProfileDefinition.ProfileDefinitions.Add(new Models.ReferencedProfileDefinitions
			{
				AllowMultiple = false,
				Mandatory = false,
				ProfileDefinitionReference = id,
			});

			record.State = State.Updated;
		}

		protected void RemoveProfileDefinitionReference(ProfileDefinitionDataRecord record, Guid id)
		{
			var refs = record.ProfileDefinition.ProfileDefinitions;

			var toDelete = refs
				.Where(r => r.ProfileDefinitionReference == id)
				.ToList();

			if (toDelete.Count == 0)
				return;

			refs.RemoveAll(r => r.ProfileDefinitionReference == id);

			record.State = State.Updated;

			presenter.Model.TryDelete(toDelete);
		}

		protected Models.ProfileDefinition CreateNewProfileDefinition(PageNavigator navigator)
		{
			var count = navigator.GetAllRecords().Count(r => r is ProfileDefinitionDataRecord) + 1;

			return new Models.ProfileDefinition
			{
				ID = Guid.NewGuid(),
				Name = $"Profile Definition #{count:000}",
				ConfigurationParameters = new List<Models.ReferencedConfigurationParameters>(),
				ProfileDefinitions = new List<Models.ReferencedProfileDefinitions>(),
				Scripts = new List<Models.Script>(),
			};
		}
	}
}

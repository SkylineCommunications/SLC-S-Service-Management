namespace SLC_SM_IAS_Profiles.Presenters
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;
	using SLC_SM_IAS_Profiles.Model;
	using SLC_SM_IAS_Profiles.Views;

	public class EventHandlers
	{
		public EventHandlers(IEngine engine, ProfilePresenter presenter)
		{
			ConfigurationParameter = new ConfigurationParameterEventHandlers(engine, presenter);
			Profile = new ProfileEventHandlers(engine, presenter);
			Common = new CommonEventHandlers(engine, presenter);
		}

		public ConfigurationParameterEventHandlers ConfigurationParameter { get; set; }

		public ProfileEventHandlers Profile { get; set; }

		public CommonEventHandlers Common { get; set; }
	}

	public abstract class AbstractEventHandlers
	{
		protected IEngine engine;
		protected ProfilePresenter presenter;

		protected AbstractEventHandlers(IEngine engine, ProfilePresenter presenter)
		{
			this.engine = engine;
			this.presenter = presenter;
		}

		protected Models.ConfigurationParameterValue CreateNewConfigurationParameterValue(PageNavigator navigator, Models.ConfigurationParameter configurationParameter)
		{
			var records = navigator.CurrentPage.Records;
			var count = records
				.OfType<ConfigurationDataRecord>()
				.Where(r => r.ReferredConfigurationParameter.ID == configurationParameter.ID && r.State != State.Removed)
				.Count();

			var configurationValue = new Models.ConfigurationParameterValue
			{
				Label = count == 0 ? $"{configurationParameter.Name}" : $"{configurationParameter.Name} #{count + 1:000}",
				Type = configurationParameter.Type,
				ConfigurationParameterId = configurationParameter.ID,
				NumberOptions = Clone(configurationParameter.NumberOptions),
				DiscreteOptions = Clone(configurationParameter.DiscreteOptions),
				TextOptions = Clone(configurationParameter.TextOptions),
			};

			return configurationValue;
		}

		protected void AddConfigurationValueToProfile(ProfileDataRecord record, Models.ConfigurationParameterValue configurationValue)
		{
			record.Profile.ConfigurationParameterValues.Add(configurationValue);
			record.State = State.Updated;
		}

		protected void AddSubProfileToProfile(ProfileDataRecord record, Guid id)
		{
			record.Profile.Profiles.Add(id);

			record.State = State.Updated;
		}

		protected void RemoveSubProfileFromProfile(ProfileDataRecord record, Guid id)
		{
			var refs = record.Profile.Profiles;

			var toDelete = refs
				.Where(r => r == id)
				.ToList();

			if (toDelete.Count == 0)
				return;

			refs.RemoveAll(r => r == id);

			record.State = State.Updated;
		}

		protected Models.Profile CreateNewProfile(PageNavigator navigator, Models.ProfileDefinition profileDefinition)
		{
			var count = navigator.GetCurrentPage().Records
				.OfType<ProfileDataRecord>()
				.Where(r => r.ReferredProfileDefinition.ID == profileDefinition.ID && r.State != State.Removed)
				.Count();

			return new Models.Profile
			{
				ID = Guid.NewGuid(),
				Name = count == 0 ? $"{profileDefinition.Name}" : $"{profileDefinition.Name} #{count + 1:000}",
				ProfileDefinitionReference = profileDefinition.ID,
				ConfigurationParameterValues = new List<Models.ConfigurationParameterValue>(),
				Profiles = new List<Guid>(),
				TestedProtocols = new List<Models.ProtocolTest>(),
				IsReusable = true,
			};
		}

		private Models.NumberParameterOptions Clone(Models.NumberParameterOptions numberOptions)
		{
			if (numberOptions == null)
				return null;

			return new Models.NumberParameterOptions
			{
				ID = Guid.NewGuid(),
				Decimals = numberOptions.Decimals,
				DefaultUnit = numberOptions.DefaultUnit,
				DefaultValue = numberOptions.DefaultValue,
				MaxRange = numberOptions.MaxRange,
				MinRange = numberOptions.MinRange,
				StepSize = numberOptions.StepSize,
				Units = numberOptions.Units,
			};
		}

		private Models.DiscreteParameterOptions Clone(Models.DiscreteParameterOptions discreteOptions)
		{
			if (discreteOptions == null)
				return null;

			return new Models.DiscreteParameterOptions
			{
				ID = Guid.NewGuid(),
				Default = discreteOptions.Default,
				DiscreteValues = discreteOptions.DiscreteValues,
			};
		}

		private Models.TextParameterOptions Clone(Models.TextParameterOptions textOptions)
		{
			if (textOptions == null)
				return null;

			return new Models.TextParameterOptions
			{
				ID = Guid.NewGuid(),
				Default = textOptions.Default,
				Regex = textOptions.Regex,
				UserMessage = textOptions.UserMessage,
			};
		}
	}
}

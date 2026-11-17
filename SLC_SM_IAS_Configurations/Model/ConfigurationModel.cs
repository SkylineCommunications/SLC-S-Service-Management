namespace SLC_SM_IAS_Profiles.Model
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM;

	public class ConfigurationModel
	{
		private DataHelpersConfigurations _model;
		private IEngine _engine;

		public ConfigurationModel(IEngine engine)
		{
			_model = new DataHelpersConfigurations(engine.GetUserConnection());
			_engine = engine;
		}

		public List<Models.ConfigurationUnit> ReadConfigurationUnits()
		{
			return _model.ConfigurationUnits.Read();
		}

		public List<Models.ProfileDefinition> ReadProfileDefinitions()
		{
			return _model.ProfileDefinitions.Read();
		}

		public List<Models.ProfileDefinition> ReadProfileDefinitions(IEnumerable<Guid> ids)
		{
			FilterElement<Models.ProfileDefinition> filter = new ORFilterElement<Models.ProfileDefinition>();
			foreach (var id in ids)
			{
				filter = filter.OR(ProfileDefinitionExposers.Guid.Equal(id));
			}

			return _model.ProfileDefinitions.Read(filter);
		}

		public List<Models.ConfigurationParameter> ReadConfigurationParameters()
		{
			return _model.ConfigurationParameters.Read();
		}

		public List<Models.ConfigurationParameter> ReadConfigurationParameters(IEnumerable<Guid> ids)
		{
			FilterElement<Models.ConfigurationParameter> filter = new ORFilterElement<Models.ConfigurationParameter>();
			foreach (var id in ids)
			{
				filter = filter.OR(ConfigurationParameterExposers.Guid.Equal(id));
			}

			return _model.ConfigurationParameters.Read(filter);
		}

		public List<Models.ReferencedConfigurationParameters> ReadReferencedConfigurationParameters(Models.ProfileDefinition profileDefinition)
		{
			FilterElement<Models.ReferencedConfigurationParameters> filter = null;
			if (profileDefinition.ConfigurationParameters.Any())
			{
				filter = profileDefinition.ConfigurationParameters
					.Select(c => (FilterElement<Models.ReferencedConfigurationParameters>)
						ReferencedConfigurationParametersExposers.ID.Equal(c.ID))
					.Aggregate((f1, f2) => f1.OR(f2));
			}
			else
			{
				filter = new FALSEFilterElement<Models.ReferencedConfigurationParameters>();
			}

			return _model.ReferencedConfigurationParameters.Read(filter);
		}

		public bool TryDelete(IEnumerable<Models.ReferencedConfigurationParameters> referencedConfigurationParameters)
		{
			return _model.ReferencedConfigurationParameters.TryDelete(referencedConfigurationParameters);
		}

		public bool TryDelete(Models.ConfigurationParameter configurationParameter)
		{
			return _model.ConfigurationParameters.TryDelete(configurationParameter);
		}

		public Guid CreateOrUpdate(Models.ConfigurationParameter configurationParameter)
		{
			return _model.ConfigurationParameters.CreateOrUpdate(configurationParameter);
		}

		public bool TryDelete(Models.ProfileDefinition profileDefinition)
		{
			return _model.ProfileDefinitions.TryDelete(profileDefinition);
		}

		public Guid CreateOrUpdate(Models.ProfileDefinition profileDefinition)
		{
			return _model.ProfileDefinitions.CreateOrUpdate(profileDefinition);
		}
	}
}

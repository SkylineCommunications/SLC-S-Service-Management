namespace SLC_SM_IAS_Profiles.Presenters
{
	using System;
	using DomHelpers.SlcConfigurations;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations;
	using SLC_SM_IAS_Profiles.Model;

	public class ConfigurationDataRecord : DataRecord
	{
		public ConfigurationDataRecord(Models.ConfigurationParameter configParam, State initialState, RecordType type)
		{
			switch (configParam.Type)
			{
				case SlcConfigurationsIds.Enums.Type.Number:
					if (configParam.NumberOptions == null)
					{
						configParam.NumberOptions = new Models.NumberParameterOptions();
					}

					break;

				case SlcConfigurationsIds.Enums.Type.Discrete:
					if (configParam.DiscreteOptions == null)
					{
						configParam.DiscreteOptions = new Models.DiscreteParameterOptions();
					}

					break;

				case SlcConfigurationsIds.Enums.Type.Text:
					if (configParam.TextOptions == null)
					{
						configParam.TextOptions = new Models.TextParameterOptions();
					}

					break;

				default:
					break;
			}

			State = initialState;
			RecordType = type;
			ConfigurationParameter = configParam;
		}

		public Models.ConfigurationParameter ConfigurationParameter { get; set; }

		public override Guid CreateOrUpdate(ConfigurationModel model)
		{
			return model.CreateOrUpdate(ConfigurationParameter);
		}

		public override void SetName(string name)
		{
			ConfigurationParameter.Name = name;
		}

		public override bool TryDelete(ConfigurationModel model)
		{
			return model.TryDelete(ConfigurationParameter);
		}
	}
}
namespace SLC_SM_IAS_Profiles.Presenters
{
	using System;
	using DomHelpers.SlcConfigurations;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations;
	using SLC_SM_IAS_Profiles.Model;

	public class ConfigurationDataRecord : DataRecord
	{
		public ConfigurationDataRecord(
			Models.ConfigurationParameterValue paramValue,
			Models.ConfigurationParameter configParameter,
			State initialState,
			RecordType type)
		{
			switch (paramValue.Type)
			{
				case SlcConfigurationsIds.Enums.Type.Number:
					if (paramValue.NumberOptions == null)
					{
						paramValue.NumberOptions = new Models.NumberParameterOptions();
					}

					break;

				case SlcConfigurationsIds.Enums.Type.Discrete:
					if (paramValue.DiscreteOptions == null)
					{
						paramValue.DiscreteOptions = new Models.DiscreteParameterOptions();
					}

					break;

				case SlcConfigurationsIds.Enums.Type.Text:
					if (paramValue.TextOptions == null)
					{
						paramValue.TextOptions = new Models.TextParameterOptions();
					}

					break;

				default:
					break;
			}

			State = initialState;
			RecordType = type;
			ReferredConfigurationParameter = configParameter;
			ConfigurationParameterValue = paramValue;
		}

		public Models.ConfigurationParameterValue ConfigurationParameterValue { get; set; }

		public Models.ConfigurationParameter ReferredConfigurationParameter { get; set; }

		public override Guid CreateOrUpdate(ProfileModel model)
		{
			return model.CreateOrUpdateConfigurationValue(ConfigurationParameterValue);
		}

		public override bool TryDelete(ProfileModel model)
		{
			return model.TryDeleteConfigurationValue(ConfigurationParameterValue);
		}

		public override void SetName(string name)
		{
			ConfigurationParameterValue.Label = name;
		}
	}
}
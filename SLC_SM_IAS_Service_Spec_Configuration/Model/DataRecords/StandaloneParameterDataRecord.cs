namespace SLC_SM_IAS_Service_Spec_Configuration.Model.DataRecords
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using Skyline.DataMiner.SDM;

	public partial class ServiceConfigurationPresenter
	{
		internal sealed class StandaloneParameterDataRecord : IParameterDataRecord
		{
			public State State { get; set; }

			public ServiceSpecificationConfigurationValue ServiceConfig { get; set; }

			public ConfigurationParameterValue ConfigurationParamValue { get; set; }

			public ConfigurationParameter ConfigurationParam { get; set; }

			public NumberParameterOptions NumberOptions { get; set; }

			public DiscreteParameterOptions DiscreteOptions { get; set; }

			public TextParameterOptions TextOptions { get; set; }

			public bool NumberOptionsPersisted { get; set; }

			public bool DiscreteOptionsPersisted { get; set; }

			public bool TextOptionsPersisted { get; set; }

			public List<ConfigurationUnit> Units { get; set; } = new List<ConfigurationUnit>();

			public List<DiscreteValue> DiscreteValues { get; set; } = new List<DiscreteValue>();

			internal static StandaloneParameterDataRecord BuildDataRecord(
				ServiceSpecificationConfigurationValue currentConfig,
				ConfigurationParameterValue configurationParameterValue,
				ConfigurationParameter configParam,
				IReadOnlyDictionary<string, NumberParameterOptions> numberOptions,
				IReadOnlyDictionary<string, DiscreteParameterOptions> discreteOptions,
				IReadOnlyDictionary<string, TextParameterOptions> textOptions,
				IReadOnlyDictionary<string, ConfigurationUnit> units,
				IReadOnlyDictionary<string, DiscreteValue> discreteValues)
			{
				var numberOptionsId = FirstIdentifier(configurationParameterValue.NumberOptionsId, configParam.NumberOptionsId);
				var discreteOptionsId = FirstIdentifier(configurationParameterValue.DiscreteOptionsId, configParam.DiscreteOptionsId);
				var textOptionsId = FirstIdentifier(configurationParameterValue.TextOptionsId, configParam.TextOptionsId);
				var dataRecord = new StandaloneParameterDataRecord
				{
					State = State.Update,
					ServiceConfig = currentConfig,
					ConfigurationParamValue = configurationParameterValue,
					ConfigurationParam = configParam,
					NumberOptionsPersisted = !String.IsNullOrEmpty(configurationParameterValue.NumberOptionsId.Identifier),
					DiscreteOptionsPersisted = !String.IsNullOrEmpty(configurationParameterValue.DiscreteOptionsId.Identifier),
					TextOptionsPersisted = !String.IsNullOrEmpty(configurationParameterValue.TextOptionsId.Identifier),
					NumberOptions = GetValue(numberOptions, numberOptionsId),
					DiscreteOptions = GetValue(discreteOptions, discreteOptionsId),
					TextOptions = GetValue(textOptions, textOptionsId),
				};

				if (String.IsNullOrEmpty(configurationParameterValue.NumberOptionsId.Identifier) && dataRecord.NumberOptions != null)
				{
					dataRecord.NumberOptions = Clone(dataRecord.NumberOptions);
					configurationParameterValue.NumberOptionsId = new SdmObjectReference<NumberParameterOptions>(dataRecord.NumberOptions.Identifier);
				}

				if (String.IsNullOrEmpty(configurationParameterValue.DiscreteOptionsId.Identifier) && dataRecord.DiscreteOptions != null)
				{
					dataRecord.DiscreteOptions = Clone(dataRecord.DiscreteOptions);
					configurationParameterValue.DiscreteOptionsId = new SdmObjectReference<DiscreteParameterOptions>(dataRecord.DiscreteOptions.Identifier);
				}

				if (String.IsNullOrEmpty(configurationParameterValue.TextOptionsId.Identifier) && dataRecord.TextOptions != null)
				{
					dataRecord.TextOptions = Clone(dataRecord.TextOptions);
					configurationParameterValue.TextOptionsId = new SdmObjectReference<TextParameterOptions>(dataRecord.TextOptions.Identifier);
				}

				dataRecord.Units = Resolve(dataRecord.NumberOptions?.Units, units);
				dataRecord.DiscreteValues = Resolve(dataRecord.DiscreteOptions?.DiscreteValues, discreteValues);
				return dataRecord;
			}

			private static string FirstIdentifier<T>(SdmObjectReference<T> preferred, SdmObjectReference<T> fallback)
				where T : SdmObject<T>
			{
				return String.IsNullOrEmpty(preferred.Identifier) ? fallback.Identifier : preferred.Identifier;
			}

			private static T GetValue<T>(IReadOnlyDictionary<string, T> values, string identifier)
				where T : class
			{
				return !String.IsNullOrEmpty(identifier) && values.TryGetValue(identifier, out var value) ? value : null;
			}

			private static List<T> Resolve<T>(IEnumerable<SdmObjectReference<T>> references, IReadOnlyDictionary<string, T> values)
				where T : SdmObject<T>
			{
				return references?
					.Select(reference => GetValue(values, reference.Identifier))
					.Where(value => value != null)
					.ToList() ?? new List<T>();
			}

			private static NumberParameterOptions Clone(NumberParameterOptions source)
			{
				return new NumberParameterOptions
				{
					Identifier = Guid.NewGuid().ToString(),
					Units = source.Units?.ToList() ?? new List<SdmObjectReference<ConfigurationUnit>>(),
					DefaultUnitId = source.DefaultUnitId,
					MinRange = source.MinRange,
					MaxRange = source.MaxRange,
					Decimals = source.Decimals,
					StepSize = source.StepSize,
					DefaultValue = source.DefaultValue,
				};
			}

			private static DiscreteParameterOptions Clone(DiscreteParameterOptions source)
			{
				return new DiscreteParameterOptions
				{
					Identifier = Guid.NewGuid().ToString(),
					DiscreteValues = source.DiscreteValues?.ToList() ?? new List<SdmObjectReference<DiscreteValue>>(),
					DefaultDiscreteValueId = source.DefaultDiscreteValueId,
				};
			}

			private static TextParameterOptions Clone(TextParameterOptions source)
			{
				return new TextParameterOptions
				{
					Identifier = Guid.NewGuid().ToString(),
					Regex = source.Regex,
					UserMessage = source.UserMessage,
					Default = source.Default,
				};
			}
		}
	}
}

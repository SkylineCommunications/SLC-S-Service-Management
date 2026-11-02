namespace SLC_SM_IAS_Service_Configuration.Presenters
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;
	using Skyline.DataMiner.SDM;

	public partial class ServiceConfigurationPresenter
	{
		internal sealed class ProfileParameterDataRecord : IParameterDataRecord
		{
			public State State { get; set; }

			public bool Mandatory { get; set; }

			public ConfigurationParameterValue ConfigurationParamValue { get; set; }

			public ConfigurationParameter ConfigurationParam { get; set; }

			public ReferencedConfigurationParameter ReferencedConfiguration { get; set; }

			public NumberParameterOptions NumberOptions { get; set; }

			public DiscreteParameterOptions DiscreteOptions { get; set; }

			public TextParameterOptions TextOptions { get; set; }

			public bool NumberOptionsPersisted { get; set; }

			public bool DiscreteOptionsPersisted { get; set; }

			public bool TextOptionsPersisted { get; set; }

			public List<ConfigurationUnit> Units { get; set; } = new List<ConfigurationUnit>();

			public List<DiscreteValue> DiscreteValues { get; set; } = new List<DiscreteValue>();

			internal static ProfileParameterDataRecord BuildParameterDataRecord(
				ConfigurationParameterValue currentConfig,
				ConfigurationParameter configParam,
				ReferencedConfigurationParameter referencedParam,
				IReadOnlyDictionary<string, NumberParameterOptions> numberOptions,
				IReadOnlyDictionary<string, DiscreteParameterOptions> discreteOptions,
				IReadOnlyDictionary<string, TextParameterOptions> textOptions,
				IReadOnlyDictionary<string, ConfigurationUnit> units,
				IReadOnlyDictionary<string, DiscreteValue> discreteValues,
				State state = State.Update)
			{
				var numberOptionsId = FirstIdentifier(currentConfig.NumberOptionsId, configParam.NumberOptionsId);
				var discreteOptionsId = FirstIdentifier(currentConfig.DiscreteOptionsId, configParam.DiscreteOptionsId);
				var textOptionsId = FirstIdentifier(currentConfig.TextOptionsId, configParam.TextOptionsId);
				var dataRecord = new ProfileParameterDataRecord
				{
					State = state,
					ConfigurationParamValue = currentConfig,
					ConfigurationParam = configParam,
					ReferencedConfiguration = referencedParam,
					Mandatory = referencedParam?.Mandatory ?? false,
					NumberOptionsPersisted = !String.IsNullOrEmpty(currentConfig.NumberOptionsId.Identifier),
					DiscreteOptionsPersisted = !String.IsNullOrEmpty(currentConfig.DiscreteOptionsId.Identifier),
					TextOptionsPersisted = !String.IsNullOrEmpty(currentConfig.TextOptionsId.Identifier),
					NumberOptions = GetValue(numberOptions, numberOptionsId),
					DiscreteOptions = GetValue(discreteOptions, discreteOptionsId),
					TextOptions = GetValue(textOptions, textOptionsId),
				};

				if (String.IsNullOrEmpty(currentConfig.NumberOptionsId.Identifier) && dataRecord.NumberOptions != null)
				{
					dataRecord.NumberOptions = Clone(dataRecord.NumberOptions);
					currentConfig.NumberOptionsId = new SdmObjectReference<NumberParameterOptions>(dataRecord.NumberOptions.Identifier);
				}

				if (String.IsNullOrEmpty(currentConfig.DiscreteOptionsId.Identifier) && dataRecord.DiscreteOptions != null)
				{
					dataRecord.DiscreteOptions = Clone(dataRecord.DiscreteOptions);
					currentConfig.DiscreteOptionsId = new SdmObjectReference<DiscreteParameterOptions>(dataRecord.DiscreteOptions.Identifier);
				}

				if (String.IsNullOrEmpty(currentConfig.TextOptionsId.Identifier) && dataRecord.TextOptions != null)
				{
					dataRecord.TextOptions = Clone(dataRecord.TextOptions);
					currentConfig.TextOptionsId = new SdmObjectReference<TextParameterOptions>(dataRecord.TextOptions.Identifier);
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

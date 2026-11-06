namespace Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions
{
	using System;
	using System.Globalization;

	using DomHelpers.SlcConfigurations;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;
	using Skyline.DataMiner.SDM;

	public static class ConfigurationValueExtensions
	{
		/// <summary>Whether the value already belongs to the given configuration parameter.</summary>
		public static bool IsValueOf(this ConfigurationParameterValue value, ConfigurationParameter definition)
		{
			return value != null
				&& definition != null
				&& !String.IsNullOrWhiteSpace(definition.Identifier)
				&& String.Equals(value.ConfigurationParameterId.Identifier, definition.Identifier, StringComparison.OrdinalIgnoreCase);
		}

		public static void ApplyConfigurationValue(
			this ConfigurationParameterValue value,
			ConfigurationParameter definition,
			string raw,
			bool mandatory)
		{
			if (value == null)
			{
				throw new ArgumentNullException(nameof(value));
			}

			if (definition == null)
			{
				throw new ArgumentNullException(nameof(definition));
			}

			if (mandatory && String.IsNullOrWhiteSpace(raw))
			{
				throw new InvalidOperationException($"A value is required for configuration parameter '{definition.Name}'.");
			}

			// An edited value keeps its own (inherited or customised) options; only a new value or a
			// parameter change takes the options of the parameter template.
			if (!value.IsValueOf(definition))
			{
				value.NumberOptionsId = definition.NumberOptionsId;
				value.DiscreteOptionsId = definition.DiscreteOptionsId;
				value.TextOptionsId = definition.TextOptionsId;
			}

			value.ConfigurationParameterId = new SdmObjectReference<ConfigurationParameter>(definition.Identifier);
			value.Type = definition.Type;

			if (String.IsNullOrWhiteSpace(raw))
			{
				value.DoubleValue = null;
				value.StringValue = definition.Type == SlcConfigurationsIds.Enums.Type.Number ? null : String.Empty;
				return;
			}

			if (definition.Type == SlcConfigurationsIds.Enums.Type.Number)
			{
				if (!Double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
				{
					throw new InvalidOperationException($"Value '{raw}' is not a valid number for '{definition.Name}'.");
				}

				value.DoubleValue = number;
				value.StringValue = null;
			}
			else
			{
				value.StringValue = raw ?? String.Empty;
				value.DoubleValue = null;
			}
		}
	}
}

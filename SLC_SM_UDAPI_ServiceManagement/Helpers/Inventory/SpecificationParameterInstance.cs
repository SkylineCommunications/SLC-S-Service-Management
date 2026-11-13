namespace SLC_SM_UDAPI_ServiceManagement.Helpers.Inventory
{
	using System;
	using System.Collections.Generic;

	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>One parameter reference of a service specification; a parameter can be referenced more than once with different labels.</summary>
	internal sealed class SpecificationParameterInstance
	{
		/// <summary>Gets or sets the referenced ID: a parameter value ID, or the parameter ID itself when the specification has no default.</summary>
		public string ReferenceId { get; set; }

		public string ParameterId { get; set; }

		public string Label { get; set; }

		/// <summary>Gets or sets the specification's parameter value, or <see langword="null"/> when there is no default.</summary>
		public ConfigurationParameterValue SpecificationValue { get; set; }

		public bool MandatoryAtService { get; set; }

		/// <summary>Gets a value indicating whether the specification has a default value for this instance.</summary>
		public bool HasDefault => HasValue(SpecificationValue);

		/// <summary>Creates the instances of the given specification configurations, in order, skipping duplicate and empty references.</summary>
		/// <param name="configurations">The specification configurations, in specification order.</param>
		/// <param name="values">The specification parameter values by ID; references without a value are parameter IDs.</param>
		/// <returns>The specification parameter instances.</returns>
		public static List<SpecificationParameterInstance> Create(
			IEnumerable<ServiceSpecificationConfigurationValue> configurations,
			IReadOnlyDictionary<string, ConfigurationParameterValue> values)
		{
			var result = new List<SpecificationParameterInstance>();
			var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (var configuration in configurations ?? Array.Empty<ServiceSpecificationConfigurationValue>())
			{
				string reference = configuration?.ConfigurationParameterId.Identifier;
				if (String.IsNullOrWhiteSpace(reference) || !seen.Add(configuration.Identifier ?? reference))
				{
					continue;
				}

				ConfigurationParameterValue value = null;
				values?.TryGetValue(reference, out value);
				string parameterId = value?.ConfigurationParameterId.Identifier;
				result.Add(new SpecificationParameterInstance
				{
					ReferenceId = reference,
					ParameterId = String.IsNullOrWhiteSpace(parameterId) ? reference : parameterId,
					Label = value?.Label ?? String.Empty,
					SpecificationValue = value,
					MandatoryAtService = configuration.MandatoryAtService,
				});
			}

			return result;
		}

		/// <summary>Checks whether a configuration parameter value holds a text or numeric value.</summary>
		/// <param name="value">The value, or <see langword="null"/>.</param>
		/// <returns><see langword="true"/> when a value is present.</returns>
		public static bool HasValue(ConfigurationParameterValue value)
		{
			return value != null && (!String.IsNullOrWhiteSpace(value.StringValue) || value.DoubleValue != null);
		}
	}
}

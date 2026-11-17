namespace SLC_SM_UDAPI_ServiceManagement.Helpers.Inventory
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;

	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;

	using SLC_SM_UDAPI_ServiceManagement.Exceptions;
	using SLC_SM_UDAPI_ServiceManagement.Models.Inventory;

	/// <summary>
	/// A single parameter criterion: a configuration parameter (by ID or label) that must have one of the given values.
	/// </summary>
	public sealed class ParameterCriterion
	{
		private const double NumericTolerance = 1e-9;

		/// <summary>
		/// Initializes a new instance of the <see cref="ParameterCriterion"/> class.
		/// </summary>
		/// <param name="parameter">The configuration parameter ID or label.</param>
		/// <param name="values">The accepted values.</param>
		public ParameterCriterion(string parameter, IReadOnlyList<string> values)
		{
			Parameter = parameter;
			Values = values;
		}

		/// <summary>Gets a value indicating whether <see cref="Parameter"/> is a configuration parameter ID.</summary>
		public bool IsIdentifier => Guid.TryParse(Parameter, out _);

		/// <summary>Gets the configuration parameter ID or label.</summary>
		public string Parameter { get; }

		/// <summary>Gets the accepted values (any of them matches).</summary>
		public IReadOnlyList<string> Values { get; }

		/// <summary>
		/// Converts the query entries to criteria. Entries for the same parameter (ID or label, case-insensitive)
		/// are combined into one criterion whose values are OR-ed; different parameters remain AND-ed.
		/// </summary>
		/// <param name="queries">The query entries.</param>
		/// <returns>The criteria.</returns>
		/// <exception cref="InvalidQueryException">An entry has no parameter or value.</exception>
		public static List<ParameterCriterion> FromQuery(IEnumerable<ServiceParameterQuery> queries)
		{
			var result = new List<ParameterCriterion>();
			if (queries == null)
			{
				return result;
			}

			var valuesPerParameter = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
			var order = new List<string>();

			foreach (var query in queries)
			{
				if (query == null || String.IsNullOrWhiteSpace(query.Parameter))
				{
					throw new InvalidQueryException("Every 'parameter' entry must have a 'parameter' ID or label.");
				}

				if (query.Value == null)
				{
					throw new InvalidQueryException($"Parameter '{query.Parameter.Trim()}' has no 'value'.");
				}

				var parameter = query.Parameter.Trim();
				if (!valuesPerParameter.TryGetValue(parameter, out var values))
				{
					values = new List<string>();
					valuesPerParameter[parameter] = values;
					order.Add(parameter);
				}

				values.Add(query.Value);
			}

			foreach (var parameter in order)
			{
				var values = valuesPerParameter[parameter].Distinct(StringComparer.OrdinalIgnoreCase).ToList();
				result.Add(new ParameterCriterion(parameter, values));
			}

			return result;
		}

		/// <summary>
		/// Checks whether a configuration parameter value satisfies this criterion.
		/// </summary>
		/// <param name="parameterValue">The configuration parameter value.</param>
		/// <returns><c>true</c> when parameter and value match.</returns>
		public bool Matches(ConfigurationParameterValue parameterValue)
		{
			if (parameterValue == null || !MatchesParameter(parameterValue))
			{
				return false;
			}

			if (parameterValue.DoubleValue.HasValue)
			{
				return Values.Any(v => Double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)
				 && Math.Abs(parameterValue.DoubleValue.Value - number) < NumericTolerance);
			}

			var actual = (parameterValue.StringValue ?? String.Empty).Trim();
			return Values.Any(v => String.Equals(actual, v, StringComparison.OrdinalIgnoreCase));
		}

		private bool MatchesParameter(ConfigurationParameterValue parameterValue)
		{
			return String.Equals(parameterValue.Label?.Trim(), Parameter, StringComparison.OrdinalIgnoreCase)
			 || String.Equals(parameterValue.ConfigurationParameterId.Identifier, Parameter, StringComparison.OrdinalIgnoreCase);
		}
	}
}
namespace SLC_SM_UDAPI_ServiceManagement.Helpers.Inventory
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;

	internal static class SpecificationParameterInstanceMatcher
	{
		/// <summary>
		/// Matches values to specification parameter instances; each instance is used at most once and matched instances are removed
		/// from <paramref name="available"/>. All values are first matched on the exact reference, then on parameter and label, and only
		/// then on parameter alone, so a loose match cannot take an instance that a later value matches more precisely.
		/// </summary>
		/// <param name="values">The values to match; <see langword="null"/> entries are not matched.</param>
		/// <param name="available">The unmatched specification instances.</param>
		/// <param name="onAmbiguousMatch">Called with the value and candidate count when a parameter-only match had to pick between instances.</param>
		/// <returns>The matched instance per value, in the order of <paramref name="values"/>, or <see langword="null"/> when there is none.</returns>
		public static SpecificationParameterInstance[] MatchAll(
			IReadOnlyList<ConfigurationParameterValue> values,
			IList<SpecificationParameterInstance> available,
			Action<ConfigurationParameterValue, int> onAmbiguousMatch = null)
		{
			var matches = new SpecificationParameterInstance[values?.Count ?? 0];
			if (matches.Length == 0 || available == null)
			{
				return matches;
			}

			MatchPass(values, available, matches, (value, candidate) => Same(candidate.ReferenceId, value.Identifier));
			MatchPass(values, available, matches, (value, candidate) => SameParameter(value, candidate) && HasSameLabel(candidate.Label, value.Label));

			for (int i = 0; i < values.Count; i++)
			{
				var value = values[i];
				if (matches[i] != null || value == null)
				{
					continue;
				}

				var candidates = available.Where(candidate => SameParameter(value, candidate)).ToList();
				if (candidates.Count == 0)
				{
					continue;
				}

				var unlabelled = candidates.Where(candidate => String.IsNullOrWhiteSpace(candidate.Label)).ToList();
				if (candidates.Count > 1 && unlabelled.Count != 1)
				{
					onAmbiguousMatch?.Invoke(value, candidates.Count);
				}

				matches[i] = unlabelled.FirstOrDefault() ?? candidates[0];
				available.Remove(matches[i]);
			}

			return matches;
		}

		private static void MatchPass(
			IReadOnlyList<ConfigurationParameterValue> values,
			IList<SpecificationParameterInstance> available,
			SpecificationParameterInstance[] matches,
			Func<ConfigurationParameterValue, SpecificationParameterInstance, bool> isMatch)
		{
			for (int i = 0; i < values.Count; i++)
			{
				if (matches[i] != null || values[i] == null)
				{
					continue;
				}

				var match = available.FirstOrDefault(candidate => isMatch(values[i], candidate));
				if (match != null)
				{
					matches[i] = match;
					available.Remove(match);
				}
			}
		}

		private static bool SameParameter(ConfigurationParameterValue value, SpecificationParameterInstance candidate)
		{
			return Same(candidate.ParameterId, value.ConfigurationParameterId.Identifier);
		}

		private static bool Same(string left, string right)
		{
			return !String.IsNullOrWhiteSpace(left) && String.Equals(left, right, StringComparison.OrdinalIgnoreCase);
		}

		private static bool HasSameLabel(string left, string right)
		{
			return !String.IsNullOrWhiteSpace(left)
				&& !String.IsNullOrWhiteSpace(right)
				&& String.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
		}
	}
}

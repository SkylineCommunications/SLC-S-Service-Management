namespace SLC_SM_UDAPI_ServiceManagement.Helpers
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;
	using System.Text.RegularExpressions;

	using Skyline.DataMiner.Net.Messages.SLDataGateway;

	using SLC_SM_UDAPI_ServiceManagement.Exceptions;

	/// <summary>
	/// Builds the server-side status, search and sort filters of the cursor list endpoints, so a filtered list never has to be read completely.
	/// </summary>
	internal static class CursorListFilter
	{
		/// <summary>Parses a comma-separated list of status names, such as <c>inProgress,held</c> or <c>In Progress</c>.</summary>
		/// <typeparam name="TEnum">The status enum.</typeparam>
		/// <param name="status">The comma-separated statuses, or <see langword="null"/>.</param>
		/// <returns>The distinct statuses; empty when none were given.</returns>
		/// <exception cref="InvalidQueryException">A status is not known.</exception>
		public static List<TEnum> ParseStatuses<TEnum>(string status)
			where TEnum : struct
		{
			var result = new List<TEnum>();
			foreach (var value in Split(status))
			{
				string key = value.Replace(" ", String.Empty).Replace("_", String.Empty);
				if (!Enum.TryParse(key, true, out TEnum parsed) || !Enum.IsDefined(typeof(TEnum), parsed) || key.All(Char.IsDigit))
				{
					throw new InvalidQueryException($"Unsupported 'status' value '{value}'. Allowed values: {String.Join(", ", Enum.GetNames(typeof(TEnum)).Select(ToCamelCase))}.");
				}

				if (!result.Contains(parsed))
				{
					result.Add(parsed);
				}
			}

			return result;
		}

		/// <summary>Gets the statuses whose name contains the search text, for example <c>prog</c> matches <c>InProgress</c>.</summary>
		/// <typeparam name="TEnum">The status enum.</typeparam>
		/// <param name="search">The search text.</param>
		/// <returns>The matching statuses.</returns>
		public static List<TEnum> StatusesMatching<TEnum>(string search)
			where TEnum : struct
		{
			return Enum.GetValues(typeof(TEnum))
				.Cast<TEnum>()
				.Where(value => TextMatches(value.ToString(), search) || TextMatches(ToDisplayName(value.ToString()), search))
				.ToList();
		}

		/// <summary>Gets the casings of a search term to look for.</summary>
		/// <remarks>Depending on the storage, the DOM <c>Contains</c> comparison can be case-sensitive, so the common casings are searched as well.</remarks>
		/// <param name="search">The search text.</param>
		/// <returns>The distinct variants; empty when there is no search text.</returns>
		public static List<string> SearchVariants(string search)
		{
			string term = search?.Trim();
			if (String.IsNullOrEmpty(term))
			{
				return new List<string>();
			}

			return new[]
				{
					term,
					term.ToLowerInvariant(),
					term.ToUpperInvariant(),
					CultureInfo.InvariantCulture.TextInfo.ToTitleCase(term.ToLowerInvariant()),
				}
				.Distinct(StringComparer.Ordinal)
				.ToList();
		}

		/// <summary>Checks whether a value contains the search text, ignoring case.</summary>
		/// <param name="value">The value.</param>
		/// <param name="search">The search text.</param>
		/// <returns><see langword="true"/> when the value contains the text.</returns>
		public static bool TextMatches(string value, string search)
		{
			string term = search?.Trim();
			return !String.IsNullOrEmpty(term) && value != null && value.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
		}

		/// <summary>Reads the identifiers of the objects with a text that contains the search text.</summary>
		/// <remarks>
		/// The storage filter can return near matches, so the matches are checked again. Every match is returned: truncating them would
		/// silently drop the objects that reference the remaining matches from all pages and from the total count.
		/// </remarks>
		/// <typeparam name="T">The object type.</typeparam>
		/// <param name="read">Reads the objects matching a filter.</param>
		/// <param name="filter">The storage filter, or <see langword="null"/> when there is nothing to search.</param>
		/// <param name="search">The search text.</param>
		/// <param name="identifier">Gets the identifier of an object.</param>
		/// <param name="texts">Gets the searchable texts of an object.</param>
		/// <returns>The identifiers.</returns>
		public static List<string> MatchingIds<T>(
			Func<FilterElement<T>, IEnumerable<T>> read,
			FilterElement<T> filter,
			string search,
			Func<T, string> identifier,
			params Func<T, string>[] texts)
		{
			if (filter == null)
			{
				return new List<string>();
			}

			return (read(filter) ?? Enumerable.Empty<T>())
				.Where(o => o != null && texts.Any(text => TextMatches(text(o), search)))
				.Select(identifier)
				.Where(id => !String.IsNullOrEmpty(id))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();
		}

		/// <summary>Combines filters with OR.</summary>
		/// <typeparam name="T">The filtered type.</typeparam>
		/// <param name="filters">The filters; <see langword="null"/> entries are ignored.</param>
		/// <returns>The combined filter, or <see langword="null"/> when there are no filters.</returns>
		public static FilterElement<T> Any<T>(IEnumerable<FilterElement<T>> filters)
		{
			var list = (filters ?? Enumerable.Empty<FilterElement<T>>()).Where(f => f != null).ToArray();
			return list.Length == 0 ? null : list.Length == 1 ? list[0] : new ORFilterElement<T>(list);
		}

		/// <summary>Combines filters with AND.</summary>
		/// <typeparam name="T">The filtered type.</typeparam>
		/// <param name="filters">The filters; <see langword="null"/> entries are ignored.</param>
		/// <returns>The combined filter, or <see langword="null"/> when there are no filters.</returns>
		public static FilterElement<T> All<T>(params FilterElement<T>[] filters)
		{
			var list = (filters ?? new FilterElement<T>[0]).Where(f => f != null).ToArray();
			return list.Length == 0 ? null : list.Length == 1 ? list[0] : new ANDFilterElement<T>(list);
		}

		/// <summary>Parses a sort expression such as <c>name</c> or <c>-serviceId</c>.</summary>
		/// <param name="sort">The sort expression, or <see langword="null"/>.</param>
		/// <param name="allowed">The supported keys.</param>
		/// <returns>The matching allowed key (or <see langword="null"/> when no sort was given) and whether it is descending.</returns>
		/// <exception cref="InvalidQueryException">The key is not supported.</exception>
		public static CursorSort ParseSort(string sort, params string[] allowed)
		{
			if (String.IsNullOrWhiteSpace(sort))
			{
				return new CursorSort(null, false);
			}

			string key = sort.Trim();
			bool descending = key.StartsWith("-", StringComparison.Ordinal);
			key = key.TrimStart('-', '+').Trim();
			string match = allowed.FirstOrDefault(a => String.Equals(a, key, StringComparison.OrdinalIgnoreCase));
			if (match == null)
			{
				throw new InvalidQueryException($"Unsupported 'sort' value '{key}' for cursor pagination. Use {String.Join(" or ", allowed)}, optionally prefixed with '-'.");
			}

			return new CursorSort(match, descending);
		}

		private static IEnumerable<string> Split(string values)
		{
			return String.IsNullOrWhiteSpace(values)
				? Enumerable.Empty<string>()
				: values.Split(',').Select(v => v.Trim()).Where(v => v.Length > 0);
		}

		private static string ToCamelCase(string value)
		{
			return String.IsNullOrEmpty(value) ? value : Char.ToLowerInvariant(value[0]) + value.Substring(1);
		}

		private static string ToDisplayName(string value)
		{
			return Regex.Replace(value ?? String.Empty, "([a-z])([A-Z])", "$1 $2");
		}
	}

	/// <summary>A parsed cursor sort.</summary>
	internal sealed class CursorSort
	{
		public CursorSort(string key, bool descending)
		{
			Key = key;
			Descending = descending;
		}

		/// <summary>Gets the sort key, or <see langword="null"/> for the default order.</summary>
		public string Key { get; }

		/// <summary>Gets a value indicating whether the order is descending.</summary>
		public bool Descending { get; }
	}
}

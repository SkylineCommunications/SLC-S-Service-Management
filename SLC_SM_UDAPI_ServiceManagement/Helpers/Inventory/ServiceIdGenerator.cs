namespace SLC_SM_UDAPI_ServiceManagement.Helpers.Inventory
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	internal static class ServiceIdGenerator
	{
		/// <summary>
		/// Returns the <c>SERVICE-nnnnn</c> identifier one above the highest numeric suffix of the given service IDs.
		/// </summary>
		/// <param name="existingServiceIds">All existing service IDs. Must be complete (use a paged read), otherwise an ID can be reused.</param>
		public static string Next(IEnumerable<string> existingServiceIds)
		{
			long highest = (existingServiceIds ?? Enumerable.Empty<string>())
				.Where(id => !String.IsNullOrWhiteSpace(id))
				.Select(id => Int64.TryParse(id.Trim().Split('-').Last(), out long number) && number > 0 ? number : 0)
				.DefaultIfEmpty(0)
				.Max();

			return $"SERVICE-{highest + 1:00000}";
		}
	}
}

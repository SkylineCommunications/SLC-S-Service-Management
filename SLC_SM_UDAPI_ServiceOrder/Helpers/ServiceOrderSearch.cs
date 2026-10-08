namespace SLC_SM_UDAPI_ServiceOrder.Helpers
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	using SLC_SM_UDAPI_ServiceOrder.Exceptions;
	using SLC_SM_UDAPI_ServiceOrder.Models;

	/// <summary>
	/// Finds the service orders matching a <see cref="ListServiceOrdersQuery"/>.
	/// The requested start date of an order is the earliest start time of its order items.
	/// </summary>
	public sealed class ServiceOrderSearch
	{
		private static readonly string[] AllowedStatuses =
		{
			"new", "acknowledged", "inProgress", "completed", "rejected", "failed",
			"partiallyFailed", "held", "pending", "assessCancellation", "pendingCancellation", "cancelled",
		};

		private static readonly string[] AllowedPriorities = { "high", "medium", "low" };

		private readonly IServiceOrderSearchDataSource dataSource;

		/// <summary>
		/// Initializes a new instance of the <see cref="ServiceOrderSearch"/> class.
		/// </summary>
		/// <param name="dataSource">The data source.</param>
		public ServiceOrderSearch(IServiceOrderSearchDataSource dataSource)
		{
			this.dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
		}

		/// <summary>
		/// Finds the service orders matching the query. <c>fields</c>, <c>limit</c> and <c>offset</c> are not applied.
		/// </summary>
		/// <param name="query">The query.</param>
		/// <returns>The matching service orders, sorted when requested.</returns>
		/// <exception cref="InvalidQueryException">The query is invalid.</exception>
		public List<ServiceOrder> Find(ListServiceOrdersQuery query)
		{
			if (query == null)
			{
				throw new ArgumentNullException(nameof(query));
			}

			Validate(query);
			var comparer = CreateSortComparer(query.Sort);

			var orders = dataSource.GetServiceOrders()
				.Where(o => o != null && MatchesOrderFields(o, query))
				.ToList();

			var needsStartDates = query.RequestedStartDateGte.HasValue
				|| query.RequestedStartDateLte.HasValue
				|| IsSortKey(query.Sort, "requestedstartdate");

			if (needsStartDates)
			{
				var startDates = GetRequestedStartDates(orders);

				orders = orders.Where(o => MatchesStartDate(startDates.TryGetValue(o.Identifier, out var s) ? s : null, query)).ToList();

				if (IsSortKey(query.Sort, "requestedstartdate"))
				{
					bool descending = query.Sort.Trim().StartsWith("-", StringComparison.Ordinal);
					Comparison<ServiceOrder> byDate = (a, b) => Nullable.Compare(
						startDates.TryGetValue(a.Identifier, out var sa) ? sa : null,
						startDates.TryGetValue(b.Identifier, out var sb) ? sb : null);
					comparer = descending ? (a, b) => byDate(b, a) : byDate;
				}
			}

			if (comparer != null)
			{
				orders.Sort(comparer);
			}

			return orders;
		}

		private static void Validate(ListServiceOrdersQuery query)
		{
			if (query.Offset < 0)
			{
				throw new InvalidQueryException("'offset' must be greater than or equal to 0.");
			}

			if (query.Limit < 1 || query.Limit > 500)
			{
				throw new InvalidQueryException("'limit' must be between 1 and 500.");
			}

			foreach (var status in query.Statuses ?? new List<string>())
			{
				if (!AllowedStatuses.Contains(status, StringComparer.OrdinalIgnoreCase))
				{
					throw new InvalidQueryException($"Unsupported 'status' value '{status}'. Allowed values: {String.Join(", ", AllowedStatuses)}.");
				}
			}

			if (!String.IsNullOrWhiteSpace(query.Priority) && !AllowedPriorities.Contains(query.Priority.Trim(), StringComparer.OrdinalIgnoreCase))
			{
				throw new InvalidQueryException($"Unsupported 'priority' value '{query.Priority}'. Allowed values: {String.Join(", ", AllowedPriorities)}.");
			}

			if (query.RequestedStartDateGte.HasValue && query.RequestedStartDateLte.HasValue
				&& query.RequestedStartDateGte.Value > query.RequestedStartDateLte.Value)
			{
				throw new InvalidQueryException("'requestedStartDate.gte' must be earlier than or equal to 'requestedStartDate.lte'.");
			}
		}

		private static bool MatchesOrderFields(ServiceOrder order, ListServiceOrdersQuery query)
		{
			var statuses = query.Statuses ?? new List<string>();
			if (statuses.Count > 0
				&& !statuses.Any(s => String.Equals(s.Trim(), order.Status.ToString(), StringComparison.OrdinalIgnoreCase)))
			{
				return false;
			}

			if (!String.IsNullOrWhiteSpace(query.Priority)
				&& !String.Equals(query.Priority.Trim(), order.Priority?.ToString(), StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}

			if (!String.IsNullOrWhiteSpace(query.ExternalId)
				&& !String.Equals(query.ExternalId.Trim(), order.ExternalID?.Trim(), StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}

			if (query.OrganizationId.HasValue && order.OrganizationId != query.OrganizationId)
			{
				return false;
			}

			return true;
		}

		private static bool MatchesStartDate(DateTime? requestedStartDate, ListServiceOrdersQuery query)
		{
			if (!requestedStartDate.HasValue)
			{
				return false;
			}

			var value = requestedStartDate.Value.ToUniversalTime();

			if (query.RequestedStartDateGte.HasValue && value < FromUnixSeconds(query.RequestedStartDateGte.Value))
			{
				return false;
			}

			if (query.RequestedStartDateLte.HasValue && value > FromUnixSeconds(query.RequestedStartDateLte.Value))
			{
				return false;
			}

			return true;
		}

		private static DateTime FromUnixSeconds(long seconds)
		{
			try
			{
				return DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime;
			}
			catch (ArgumentOutOfRangeException)
			{
				throw new InvalidQueryException("'requestedStartDate.gte' and 'requestedStartDate.lte' must be valid Unix timestamps in seconds.");
			}
		}

		private static bool IsSortKey(string sort, string key)
		{
			return !String.IsNullOrWhiteSpace(sort)
				&& String.Equals(sort.Trim().TrimStart('-', '+'), key, StringComparison.OrdinalIgnoreCase);
		}

		private static Comparison<ServiceOrder> CreateSortComparer(string sort)
		{
			if (String.IsNullOrWhiteSpace(sort))
			{
				return null;
			}

			var key = sort.Trim();
			bool descending = key.StartsWith("-", StringComparison.Ordinal);
			key = key.TrimStart('-', '+');

			Comparison<ServiceOrder> comparison;
			switch (key.ToLowerInvariant())
			{
				case "name":
					comparison = (a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name);
					break;

				case "id":
					comparison = (a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Identifier, b.Identifier);
					break;

				case "orderid":
					comparison = (a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.OrderId, b.OrderId);
					break;

				case "externalid":
					comparison = (a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.ExternalID, b.ExternalID);
					break;

				case "status":
					comparison = (a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Status.ToString(), b.Status.ToString());
					break;

				case "priority":
					comparison = (a, b) => Nullable.Compare(a.Priority, b.Priority);
					break;

				case "requestedstartdate":
					// Applied in Find, because it requires the order items.
					return null;

				default:
					throw new InvalidQueryException($"Unsupported 'sort' value '{key}'. Use name, id, orderId, externalId, status, priority or requestedStartDate, optionally prefixed with '-'.");
			}

			return descending ? (a, b) => comparison(b, a) : comparison;
		}

		private Dictionary<string, DateTime?> GetRequestedStartDates(IReadOnlyCollection<ServiceOrder> orders)
		{
			var itemIds = orders
				.SelectMany(o => o.OrderItems ?? new List<ServiceOrderEntry>())
				.Select(e => e?.ServiceOrderItemId.Identifier)
				.Where(id => !String.IsNullOrEmpty(id))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();

			var items = itemIds.Count > 0
				? dataSource.GetServiceOrderItems(itemIds).ToDictionary(i => i.Identifier, StringComparer.OrdinalIgnoreCase)
				: new Dictionary<string, ServiceOrderItem>(StringComparer.OrdinalIgnoreCase);

			var result = new Dictionary<string, DateTime?>(StringComparer.OrdinalIgnoreCase);
			foreach (var order in orders)
			{
				result[order.Identifier] = (order.OrderItems ?? new List<ServiceOrderEntry>())
					.Select(e => e?.ServiceOrderItemId.Identifier)
					.Where(id => !String.IsNullOrEmpty(id) && items.ContainsKey(id))
					.Select(id => items[id].StartTime)
					.Where(t => t.HasValue)
					.Select(t => (DateTime?)t.Value.ToUniversalTime())
					.DefaultIfEmpty(null)
					.Min();
			}

			return result;
		}
	}
}

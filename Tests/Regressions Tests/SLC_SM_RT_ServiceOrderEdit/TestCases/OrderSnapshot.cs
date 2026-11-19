namespace SLC_SM_RT_ServiceOrderEdit.TestCases
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// The order-level fields of a service order that the edit can change, or must leave untouched.
	/// </summary>
	internal sealed class OrderSnapshot
	{
		private static readonly TimeSpan DateTolerance = TimeSpan.FromSeconds(1);

		public string Name { get; set; }

		public string Description { get; set; }

		public string ExternalId { get; set; }

		public string Priority { get; set; }

		public Guid? OrganizationId { get; set; }

		public DateTime? RequestedStart { get; set; }

		public DateTime? RequestedEnd { get; set; }

		public string OrderNumber { get; set; }

		public string Status { get; set; }

		public List<string> OrderItemIds { get; set; } = new List<string>();

		public static OrderSnapshot From(Models.ServiceOrder order)
		{
			if (order == null)
			{
				throw new ArgumentNullException(nameof(order));
			}

			return new OrderSnapshot
			{
				Name = order.Name,
				Description = order.Description ?? String.Empty,
				ExternalId = order.ExternalID ?? String.Empty,
				Priority = order.Priority?.ToString() ?? "<none>",
				OrganizationId = order.OrganizationId,
				RequestedStart = order.CompletionInfo?.RequestedStartDate,
				RequestedEnd = order.CompletionInfo?.RequestedCompletedDate,
				OrderNumber = order.OrderId,
				Status = order.Status.ToString(),
				OrderItemIds = (order.OrderItems ?? new List<Models.ServiceOrderEntry>())
					.Select(entry => entry.ServiceOrderItemId.Identifier)
					.Where(id => !String.IsNullOrWhiteSpace(id))
					.OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
					.ToList(),
			};
		}

		public OrderSnapshot Clone()
		{
			var clone = (OrderSnapshot)MemberwiseClone();
			clone.OrderItemIds = new List<string>(OrderItemIds);
			return clone;
		}

		/// <summary>
		/// Lists every field of this (actual) snapshot that differs from the expected one.
		/// </summary>
		/// <param name="expected">The expected snapshot.</param>
		/// <returns>One line per difference; empty when both match.</returns>
		public List<string> DifferencesFrom(OrderSnapshot expected)
		{
			var differences = new List<string>();
			Compare(differences, "Name", expected.Name, Name);
			Compare(differences, "Description", expected.Description, Description);
			Compare(differences, "ExternalID", expected.ExternalId, ExternalId);
			Compare(differences, "Priority", expected.Priority, Priority);
			Compare(differences, "OrganizationId", Format(expected.OrganizationId), Format(OrganizationId));
			CompareDate(differences, "CompletionInfo.RequestedStartDate", expected.RequestedStart, RequestedStart);
			CompareDate(differences, "CompletionInfo.RequestedCompletedDate", expected.RequestedEnd, RequestedEnd);
			Compare(differences, "OrderId", expected.OrderNumber, OrderNumber);
			Compare(differences, "Status", expected.Status, Status);
			Compare(differences, "OrderItems", String.Join(", ", expected.OrderItemIds), String.Join(", ", OrderItemIds));
			return differences;
		}

		private static void Compare(List<string> differences, string field, string expected, string actual)
		{
			if (!String.Equals(expected, actual, StringComparison.Ordinal))
			{
				differences.Add($"{field}: expected '{expected}', actual '{actual}'");
			}
		}

		private static void CompareDate(List<string> differences, string field, DateTime? expected, DateTime? actual)
		{
			bool match = expected.HasValue == actual.HasValue
				&& (!expected.HasValue || (ToUtc(expected.Value) - ToUtc(actual.Value)).Duration() <= DateTolerance);
			if (!match)
			{
				differences.Add($"{field}: expected '{Format(expected)}', actual '{Format(actual)}'");
			}
		}

		private static DateTime ToUtc(DateTime value)
		{
			return value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc);
		}

		private static string Format(Guid? value)
		{
			return value.HasValue ? value.Value.ToString() : "<null>";
		}

		private static string Format(DateTime? value)
		{
			return value.HasValue ? ToUtc(value.Value).ToString("o") : "<null>";
		}
	}
}

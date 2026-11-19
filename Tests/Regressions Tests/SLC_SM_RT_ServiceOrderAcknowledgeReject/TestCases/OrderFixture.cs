namespace SLC_SM_RT_ServiceOrderAcknowledgeReject.TestCases
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// A service order fixture and the order items it contains.
	/// </summary>
	internal sealed class OrderFixture
	{
		public OrderFixture(string label)
		{
			if (String.IsNullOrWhiteSpace(label))
			{
				throw new ArgumentException("Label should not be empty", nameof(label));
			}

			Label = label;
			RejectionReason = $"RT rejection reason for order {label} {Guid.NewGuid()}";
		}

		/// <summary>
		/// Gets the short label used in test case names and failure messages, for example "A".
		/// </summary>
		public string Label { get; }

		/// <summary>
		/// Gets the reason passed to the script under test when the order is rejected.
		/// </summary>
		public string RejectionReason { get; }

		public string OrderId { get; set; } = String.Empty;

		public List<string> OrderItemIds { get; } = new List<string>();

		public List<string> OrderItemNames { get; } = new List<string>();
	}
}

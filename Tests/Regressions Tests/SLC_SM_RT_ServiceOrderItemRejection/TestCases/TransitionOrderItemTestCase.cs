namespace SLC_SM_RT_ServiceOrderItemRejection.TestCases
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	/// <summary>
	/// Runs a single order item state transition and verifies the resulting status of the order item and of its parent
	/// order. The other order items of the same order must not change.
	/// </summary>
	internal sealed class TransitionOrderItemTestCase : TestCase
	{
		private readonly ServiceOrderItemRejectionTestData testData;
		private readonly OrderFixture fixture;
		private readonly int orderItemIndex;
		private readonly string previousState;
		private readonly string nextState;
		private readonly string expectedOrderItemStatus;
		private readonly string expectedOrderStatus;

		public TransitionOrderItemTestCase(
			ServiceOrderItemRejectionTestData testData,
			OrderFixture fixture,
			int orderItemIndex,
			string previousState,
			string nextState,
			string expectedOrderItemStatus,
			string expectedOrderStatus)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
			this.fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
			this.orderItemIndex = orderItemIndex;
			this.previousState = previousState ?? throw new ArgumentNullException(nameof(previousState));
			this.nextState = nextState ?? throw new ArgumentNullException(nameof(nextState));
			this.expectedOrderItemStatus = expectedOrderItemStatus ?? throw new ArgumentNullException(nameof(expectedOrderItemStatus));
			this.expectedOrderStatus = expectedOrderStatus ?? throw new ArgumentNullException(nameof(expectedOrderStatus));
		}

		/// <inheritdoc/>
		public override string Name => $"Order{fixture.Label}_Item{orderItemIndex + 1}_{previousState}_To_{nextState}";

		public override void Execute(IEngine engine)
		{
			string orderItemId = fixture.OrderItemIds[orderItemIndex];
			Dictionary<string, string> before = testData.ReadStatuses(fixture);

			SubScriptOutcome outcome = testData.InvokeOrderItemTransition(orderItemId, previousState, nextState);

			Dictionary<string, string> after = testData.ReadStatuses(fixture);

			var problems = new List<string>();
			if (outcome.HadError)
			{
				problems.Add($"the {outcome.Describe()}");
			}

			if (!String.Equals(expectedOrderItemStatus, after[orderItemId], StringComparison.Ordinal))
			{
				problems.Add($"order item '{orderItemId}' status expected '{expectedOrderItemStatus}', got '{after[orderItemId]}'");
			}

			if (!String.Equals(expectedOrderStatus, after["Order"], StringComparison.Ordinal))
			{
				problems.Add($"parent order '{fixture.OrderId}' status expected '{expectedOrderStatus}' (was '{before["Order"]}'), got '{after["Order"]}'");
			}

			problems.AddRange(fixture.OrderItemIds
				.Where(id => id != orderItemId && !String.Equals(before[id], after[id], StringComparison.Ordinal))
				.Select(id => $"sibling order item '{id}' changed from '{before[id]}' to '{after[id]}'"));

			TestTools.Assert(
				problems.Count == 0,
				$"Order {fixture.Label} item {orderItemIndex + 1} transition '{previousState}' -> '{nextState}' did not have the expected result: {String.Join("; ", problems)}.");
		}

		public override void PerformCleanup(IEngine engine)
		{
			// Cleanup of every fixture is owned by the test data.
		}
	}
}

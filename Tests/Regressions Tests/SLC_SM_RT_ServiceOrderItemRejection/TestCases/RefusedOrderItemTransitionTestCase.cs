namespace SLC_SM_RT_ServiceOrderItemRejection.TestCases
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	/// <summary>
	/// Requests an unsupported transition for an order item and verifies the script reports an error and that neither
	/// the order item, its siblings nor the parent order changed status.
	/// </summary>
	internal sealed class RefusedOrderItemTransitionTestCase : TestCase
	{
		private readonly ServiceOrderItemRejectionTestData testData;
		private readonly OrderFixture fixture;
		private readonly int orderItemIndex;
		private readonly string previousState;
		private readonly string nextState;

		public RefusedOrderItemTransitionTestCase(
			ServiceOrderItemRejectionTestData testData,
			OrderFixture fixture,
			int orderItemIndex,
			string previousState,
			string nextState)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
			this.fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
			this.orderItemIndex = orderItemIndex;
			this.previousState = previousState ?? throw new ArgumentNullException(nameof(previousState));
			this.nextState = nextState ?? throw new ArgumentNullException(nameof(nextState));
		}

		/// <inheritdoc/>
		public override string Name => $"Order{fixture.Label}_Item{orderItemIndex + 1}_{previousState}_To_{nextState}_IsRefused";

		public override void Execute(IEngine engine)
		{
			string orderItemId = fixture.OrderItemIds[orderItemIndex];
			Dictionary<string, string> before = testData.ReadStatuses(fixture);

			SubScriptOutcome outcome = testData.InvokeOrderItemTransition(orderItemId, previousState, nextState);

			Dictionary<string, string> after = testData.ReadStatuses(fixture);

			var problems = before
				.Where(pair => !String.Equals(pair.Value, after[pair.Key], StringComparison.Ordinal))
				.Select(pair => pair.Key == "Order"
					? $"parent order '{fixture.OrderId}' changed from '{pair.Value}' to '{after[pair.Key]}'"
					: $"order item '{pair.Key}' changed from '{pair.Value}' to '{after[pair.Key]}'")
				.ToList();

			if (!outcome.HadError)
			{
				problems.Add("the script reported no error while the transition is not supported");
			}

			TestTools.Assert(
				problems.Count == 0,
				$"Order {fixture.Label} item {orderItemIndex + 1} ('{orderItemId}') transition '{previousState}' -> '{nextState}' should have been refused, but {String.Join("; ", problems)} ({outcome.Describe()}).");
		}

		public override void PerformCleanup(IEngine engine)
		{
			// Cleanup of every fixture is owned by the test data.
		}
	}
}

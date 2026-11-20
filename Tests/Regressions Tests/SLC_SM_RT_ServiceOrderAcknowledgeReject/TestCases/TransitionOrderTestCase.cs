namespace SLC_SM_RT_ServiceOrderAcknowledgeReject.TestCases
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	/// <summary>
	/// Runs a single order-level state transition and verifies the resulting status of the order and of all its order items.
	/// </summary>
	internal sealed class TransitionOrderTestCase : TestCase
	{
		private readonly ServiceOrderAcknowledgeRejectTestData testData;
		private readonly OrderFixture fixture;
		private readonly string previousState;
		private readonly string nextState;
		private readonly string expectedOrderStatus;
		private readonly string expectedOrderItemStatus;

		public TransitionOrderTestCase(
			ServiceOrderAcknowledgeRejectTestData testData,
			OrderFixture fixture,
			string previousState,
			string nextState,
			string expectedOrderStatus,
			string expectedOrderItemStatus)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
			this.fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
			this.previousState = previousState ?? throw new ArgumentNullException(nameof(previousState));
			this.nextState = nextState ?? throw new ArgumentNullException(nameof(nextState));
			this.expectedOrderStatus = expectedOrderStatus ?? throw new ArgumentNullException(nameof(expectedOrderStatus));
			this.expectedOrderItemStatus = expectedOrderItemStatus ?? throw new ArgumentNullException(nameof(expectedOrderItemStatus));
		}

		/// <inheritdoc/>
		public override string Name => $"Order{fixture.Label}_{previousState}_To_{nextState}";

		public override void Execute(IEngine engine)
		{
			SubScriptOutcome outcome = testData.InvokeOrderTransition(fixture, previousState, nextState);
			Dictionary<string, string> statuses = testData.ReadStatuses(fixture);

			var problems = new List<string>();
			if (outcome.HadError)
			{
				problems.Add($"the {outcome.Describe()}");
			}

			if (!String.Equals(expectedOrderStatus, statuses["Order"], StringComparison.Ordinal))
			{
				problems.Add($"order '{fixture.OrderId}' status expected '{expectedOrderStatus}', got '{statuses["Order"]}'");
			}

			problems.AddRange(fixture.OrderItemIds
				.Where(id => !String.Equals(expectedOrderItemStatus, statuses[id], StringComparison.Ordinal))
				.Select(id => $"order item '{id}' status expected '{expectedOrderItemStatus}', got '{statuses[id]}'"));

			TestTools.Assert(
				problems.Count == 0,
				$"Order {fixture.Label} transition '{previousState}' -> '{nextState}' did not have the expected result: {String.Join("; ", problems)}.");
		}

		public override void PerformCleanup(IEngine engine)
		{
			// Cleanup of every fixture is owned by the test data.
		}
	}
}

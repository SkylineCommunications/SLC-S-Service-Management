namespace SLC_SM_RT_ServiceOrderToServiceLink.TestCases
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
		private readonly ServiceOrderToServiceLinkTestData testData;
		private readonly OrderFixture fixture;
		private readonly string previousState;
		private readonly string nextState;
		private readonly string expectedOrderStatus;
		private readonly string expectedOrderItemStatus;

		/// <summary>
		/// Initializes a new instance of the <see cref="TransitionOrderTestCase"/> class.
		/// </summary>
		/// <param name="testData">The fixture owner.</param>
		/// <param name="fixture">The order to transition.</param>
		/// <param name="previousState">The previous state passed to the script.</param>
		/// <param name="nextState">The next state passed to the script.</param>
		/// <param name="expectedOrderStatus">The status the order must have afterwards.</param>
		/// <param name="expectedOrderItemStatus">The status every order item must have afterwards.</param>
		/// <remarks>
		/// A script error always fails the test case. The silent service creation ("SLC_SM_Create Service Inventory Item" with
		/// Action AddItemSilent) does not end with ScriptAbortException("OK"), and any sub script error is rethrown by the script
		/// under test as a generic "Script failed" that stops the remaining order items, so no error can be safely tolerated.
		/// </remarks>
		public TransitionOrderTestCase(
			ServiceOrderToServiceLinkTestData testData,
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

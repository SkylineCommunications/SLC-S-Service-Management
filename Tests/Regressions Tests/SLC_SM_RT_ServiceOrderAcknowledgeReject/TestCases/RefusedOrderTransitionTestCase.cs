namespace SLC_SM_RT_ServiceOrderAcknowledgeReject.TestCases
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	/// <summary>
	/// Requests an order-level transition that is not allowed from the current state and verifies that neither the order
	/// nor any of its order items changed status.
	/// </summary>
	internal sealed class RefusedOrderTransitionTestCase : TestCase
	{
		private readonly ServiceOrderAcknowledgeRejectTestData testData;
		private readonly OrderFixture fixture;
		private readonly string previousState;
		private readonly string nextState;
		private readonly bool expectScriptError;

		/// <summary>
		/// Initializes a new instance of the <see cref="RefusedOrderTransitionTestCase"/> class.
		/// </summary>
		/// <param name="testData">The fixture owner.</param>
		/// <param name="fixture">The order to transition.</param>
		/// <param name="previousState">The previous state passed to the script.</param>
		/// <param name="nextState">The next state passed to the script.</param>
		/// <param name="expectScriptError">
		/// <c>true</c> when the script must report an error (unsupported transition);
		/// <c>false</c> when the script silently ignores the request (supported transition with a stale previous state).
		/// </param>
		public RefusedOrderTransitionTestCase(
			ServiceOrderAcknowledgeRejectTestData testData,
			OrderFixture fixture,
			string previousState,
			string nextState,
			bool expectScriptError)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
			this.fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
			this.previousState = previousState ?? throw new ArgumentNullException(nameof(previousState));
			this.nextState = nextState ?? throw new ArgumentNullException(nameof(nextState));
			this.expectScriptError = expectScriptError;
		}

		/// <inheritdoc/>
		public override string Name => $"Order{fixture.Label}_{previousState}_To_{nextState}_IsRefused";

		public override void Execute(IEngine engine)
		{
			Dictionary<string, string> before = testData.ReadStatuses(fixture);

			SubScriptOutcome outcome = testData.InvokeOrderTransition(fixture, previousState, nextState);

			Dictionary<string, string> after = testData.ReadStatuses(fixture);

			var problems = before
				.Where(pair => !String.Equals(pair.Value, after[pair.Key], StringComparison.Ordinal))
				.Select(pair => pair.Key == "Order"
					? $"order '{fixture.OrderId}' changed from '{pair.Value}' to '{after[pair.Key]}'"
					: $"order item '{pair.Key}' changed from '{pair.Value}' to '{after[pair.Key]}'")
				.ToList();

			if (expectScriptError && !outcome.HadError)
			{
				problems.Add("the script reported no error while the transition is not supported");
			}

			TestTools.Assert(
				problems.Count == 0,
				$"Order {fixture.Label} transition '{previousState}' -> '{nextState}' should have been refused, but {String.Join("; ", problems)} ({outcome.Describe()}).");
		}

		public override void PerformCleanup(IEngine engine)
		{
			// Cleanup of every fixture is owned by the test data.
		}
	}
}

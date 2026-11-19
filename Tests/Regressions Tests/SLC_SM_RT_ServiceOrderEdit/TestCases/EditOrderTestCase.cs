namespace SLC_SM_RT_ServiceOrderEdit.TestCases
{
	using System;
	using System.Collections.Generic;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	/// <summary>
	/// Edits the order through the script under test and verifies exactly the expected order-level fields changed,
	/// while the order number, status and order items stay as they were.
	/// </summary>
	internal sealed class EditOrderTestCase : TestCase
	{
		private readonly ServiceOrderEditTestData testData;
		private readonly string label;
		private readonly Func<OrderEditPayload> createPayload;
		private readonly Action<OrderSnapshot> applyExpectedChanges;

		/// <summary>
		/// Initializes a new instance of the <see cref="EditOrderTestCase"/> class.
		/// </summary>
		/// <param name="testData">The shared test data.</param>
		/// <param name="label">Describes the edit in the test case name.</param>
		/// <param name="createPayload">Creates the edit request; called when the test case runs, so it can use the fixtures.</param>
		/// <param name="applyExpectedChanges">Applies the expected changes to a snapshot of the order taken before the edit.</param>
		public EditOrderTestCase(ServiceOrderEditTestData testData, string label, Func<OrderEditPayload> createPayload, Action<OrderSnapshot> applyExpectedChanges)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
			this.label = label ?? throw new ArgumentNullException(nameof(label));
			this.createPayload = createPayload ?? throw new ArgumentNullException(nameof(createPayload));
			this.applyExpectedChanges = applyExpectedChanges ?? throw new ArgumentNullException(nameof(applyExpectedChanges));
		}

		public override string Name => $"Edit order: {label}";

		public override void Execute(IEngine engine)
		{
			OrderSnapshot before = testData.ReadOrderSnapshot();
			OrderSnapshot expected = before.Clone();
			applyExpectedChanges(expected);

			SubScriptOutcome outcome = testData.InvokeEdit(createPayload());
			TestTools.Assert(!outcome.HadError, $"Editing order '{testData.OrderId}' ({label}) failed: {outcome.Describe()}.");

			List<string> differences = testData.ReadOrderSnapshot().DifferencesFrom(expected);
			TestTools.Assert(
				differences.Count == 0,
				$"Order '{testData.OrderId}' after edit ({label}) does not match the expectation ({outcome.Describe()}): {String.Join("; ", differences)}.");

			foreach (string orderItemId in testData.OrderItemIds)
			{
				string actual = testData.DescribeOrderItem(testData.ReadOrderItem(orderItemId));
				TestTools.AssertEqual(
					testData.InitialOrderItemStates[orderItemId],
					actual,
					$"order item '{orderItemId}' of order '{testData.OrderId}' after edit ({label})");
			}
		}

		public override void PerformCleanup(IEngine engine)
		{
			// The fixtures are removed by CreateFixturesTestCase.
		}
	}
}

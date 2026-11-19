namespace SLC_SM_RT_ServiceOrderEdit.TestCases
{
	using System;
	using System.Collections.Generic;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	/// <summary>
	/// Sends an invalid edit request and verifies the script under test refuses it without changing the order.
	/// </summary>
	internal sealed class RefusedEditOrderTestCase : TestCase
	{
		private readonly ServiceOrderEditTestData testData;
		private readonly string label;
		private readonly Func<OrderEditPayload> createPayload;

		/// <summary>
		/// Initializes a new instance of the <see cref="RefusedEditOrderTestCase"/> class.
		/// </summary>
		/// <param name="testData">The shared test data.</param>
		/// <param name="label">Describes the invalid edit in the test case name.</param>
		/// <param name="createPayload">Creates the invalid edit request; called when the test case runs.</param>
		public RefusedEditOrderTestCase(ServiceOrderEditTestData testData, string label, Func<OrderEditPayload> createPayload)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
			this.label = label ?? throw new ArgumentNullException(nameof(label));
			this.createPayload = createPayload ?? throw new ArgumentNullException(nameof(createPayload));
		}

		public override string Name => $"Refused order edit: {label}";

		public override void Execute(IEngine engine)
		{
			OrderSnapshot before = testData.ReadOrderSnapshot();

			SubScriptOutcome outcome = testData.InvokeEdit(createPayload());
			TestTools.Assert(outcome.HadError, $"Editing order '{testData.OrderId}' with {label} was expected to be refused, but the {outcome.Describe()}.");

			List<string> differences = testData.ReadOrderSnapshot().DifferencesFrom(before);
			TestTools.Assert(
				differences.Count == 0,
				$"Order '{testData.OrderId}' changed after the refused edit ({label}): {String.Join("; ", differences)}.");
		}

		public override void PerformCleanup(IEngine engine)
		{
			// The fixtures are removed by CreateFixturesTestCase.
		}
	}
}

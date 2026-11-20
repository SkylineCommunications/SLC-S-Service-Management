namespace SLC_SM_RT_ServiceOrderAcknowledgeReject.TestCases
{
	using System;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Verifies the rejection reason and date were stored on a rejected order.
	/// </summary>
	internal sealed class ValidateRejectionInfoTestCase : TestCase
	{
		private readonly ServiceOrderAcknowledgeRejectTestData testData;
		private readonly OrderFixture fixture;

		public ValidateRejectionInfoTestCase(ServiceOrderAcknowledgeRejectTestData testData, OrderFixture fixture)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
			this.fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
		}

		/// <inheritdoc/>
		public override string Name => $"Order{fixture.Label}_RejectionInfo";

		public override void Execute(IEngine engine)
		{
			Models.ServiceOrder order = testData.ReadOrder(fixture);

			TestTools.Assert(
				order.CancellationInfo != null,
				$"Rejected order {fixture.Label} '{fixture.OrderId}' has no cancellation info.");
			TestTools.AssertEqual(
				fixture.RejectionReason,
				order.CancellationInfo.Reason,
				$"Rejection reason of order {fixture.Label} '{fixture.OrderId}'");
			TestTools.Assert(
				order.CancellationInfo.CancellationDate.HasValue,
				$"Rejected order {fixture.Label} '{fixture.OrderId}' has no cancellation date.");
		}

		public override void PerformCleanup(IEngine engine)
		{
			// Cleanup of every fixture is owned by the test data.
		}
	}
}

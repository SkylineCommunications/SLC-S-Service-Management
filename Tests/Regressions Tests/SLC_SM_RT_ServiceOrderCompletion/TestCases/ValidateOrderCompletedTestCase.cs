namespace SLC_SM_RT_ServiceOrderCompletion.TestCases
{
	using System;

	using DomHelpers.SlcServicemanagement;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Verifies the service order was completed automatically once its only order item was completed.
	/// Completing an order item also completes the parent order when all of its order items are completed or cancelled.
	/// </summary>
	internal sealed class ValidateOrderCompletedTestCase : TestCase
	{
		private readonly ServiceOrderFlowTestData testData;

		public ValidateOrderCompletedTestCase(ServiceOrderFlowTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			Models.ServiceOrder order = testData.ReadOrder();

			TestTools.Assert(order != null, "Service order was not found.");
			TestTools.AssertEqual(
				SlcServicemanagementIds.Behaviors.Serviceorder_Behavior.StatusesEnum.Completed.ToString(),
				order.Status.ToString(),
				"Service order status after completing its only order item");
		}

		public override void PerformCleanup(IEngine engine)
		{
			testData.CleanUp();
		}
	}
}

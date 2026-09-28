namespace SLC_SM_RT_ServiceOrderItemStateTransition.TestCases
{
	using System;

	using DomHelpers.SlcServicemanagement;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Verifies the order item transition propagated up to the Service Order that contains it.
	/// </summary>
	internal sealed class ValidateOrderPropagationTestCase : TestCase
	{
		private readonly ServiceOrderItemStateTransitionTestData testData;
		private readonly SlcServicemanagementIds.Behaviors.Serviceorder_Behavior.StatusesEnum expectedStatus;

		public ValidateOrderPropagationTestCase(
			ServiceOrderItemStateTransitionTestData testData,
			SlcServicemanagementIds.Behaviors.Serviceorder_Behavior.StatusesEnum expectedStatus)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
			this.expectedStatus = expectedStatus;
		}

		public override void Execute(IEngine engine)
		{
			Models.ServiceOrder order = testData.ReadOrder();

			TestTools.AssertEqual(
				expectedStatus.ToString(),
				order.Status.ToString(),
				"Parent order status propagated from the order item");
		}

		public override void PerformCleanup(IEngine engine)
		{
			testData.CleanUp();
		}
	}
}

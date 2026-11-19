namespace SLC_SM_RT_CreateServiceOrder.TestCases
{
	using System;
	using System.Linq;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Verifies the created Service Order was persisted and references the created Service Order Item.
	/// </summary>
	internal sealed class ValidateOrderTestCase : TestCase
	{
		private readonly CreateServiceOrderTestData testData;

		public ValidateOrderTestCase(CreateServiceOrderTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			Models.ServiceOrder persistedOrder = testData.ReadOrder();

			TestTools.Assert(persistedOrder != null, "Created service order was not found.");

			bool containsOrderItem = persistedOrder.OrderItems != null
				&& persistedOrder.OrderItems.Any(item => item != null && item.ServiceOrderItemId.Identifier == testData.OrderItemId);

			TestTools.Assert(
				containsOrderItem,
				$"Created service order does not reference order item '{testData.OrderItemId}'.");
		}

		public override void PerformCleanup(IEngine engine)
		{
			testData.CleanUp();
		}
	}
}

namespace SLC_SM_RT_ServiceOrderItemConfiguration_Edit.TestCases
{
	using System;
	using System.Linq;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Verifies the order item still references the edited configuration.
	/// </summary>
	internal sealed class ValidateOrderItemLinksTestCase : TestCase
	{
		private readonly ServiceOrderItemConfigurationEditTestData testData;

		public ValidateOrderItemLinksTestCase(ServiceOrderItemConfigurationEditTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			Models.ServiceOrderItem orderItem = testData.ReadOrderItem();

			TestTools.Assert(orderItem != null, "Order item was not found during validation.");

			bool hasConfigurationRef = orderItem.ServiceInfo != null
				&& orderItem.ServiceInfo.Configurations != null
				&& orderItem.ServiceInfo.Configurations.Any(x => x.Identifier == testData.OrderItemConfigurationId);

			TestTools.Assert(hasConfigurationRef, "Order item does not reference the edited configuration.");
		}

		public override void PerformCleanup(IEngine engine)
		{
			// Cleanup of every fixture is owned by the test data and triggered by the last test case.
		}
	}
}

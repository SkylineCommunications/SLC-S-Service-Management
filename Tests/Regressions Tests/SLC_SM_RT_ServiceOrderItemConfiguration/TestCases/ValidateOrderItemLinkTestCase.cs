namespace SLC_SM_RT_ServiceOrderItemConfiguration.TestCases
{
	using System;
	using System.Linq;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Verifies the order item keeps a reference to the created configuration.
	/// </summary>
	internal sealed class ValidateOrderItemLinkTestCase : TestCase
	{
		private readonly ServiceOrderItemConfigurationTestData testData;

		public ValidateOrderItemLinkTestCase(ServiceOrderItemConfigurationTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			Models.ServiceOrderItem persisted = testData.ReadOrderItem();

			TestTools.Assert(persisted != null, "Order item was not found after linking configuration.");

			bool hasConfigurationRef = persisted.ServiceInfo != null
				&& persisted.ServiceInfo.Configurations != null
				&& persisted.ServiceInfo.Configurations.Any(x => x.Identifier == testData.OrderItemConfigurationId);

			TestTools.Assert(hasConfigurationRef, "Order item does not contain the expected configuration reference.");
		}

		public override void PerformCleanup(IEngine engine)
		{
			// Cleanup of every fixture is owned by the test data and triggered by the last test case.
		}
	}
}

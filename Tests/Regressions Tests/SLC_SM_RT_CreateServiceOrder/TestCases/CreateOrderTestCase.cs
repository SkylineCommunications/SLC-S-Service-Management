namespace SLC_SM_RT_CreateServiceOrder.TestCases
{
	using System;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	/// <summary>
	/// Creates the Service Order that contains the created Service Order Item.
	/// </summary>
	internal sealed class CreateOrderTestCase : TestCase
	{
		private readonly CreateServiceOrderTestData testData;

		public CreateOrderTestCase(CreateServiceOrderTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			testData.CreateOrder();
		}

		public override void PerformCleanup(IEngine engine)
		{
			// Cleanup of every fixture is owned by the test data and triggered by the last test case.
		}
	}
}

namespace SLC_SM_RT_CreateServiceInventoryItem.TestCases
{
	using System;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	/// <summary>
	/// Creates the service specification, service order and service order item the test operates on.
	/// </summary>
	internal sealed class CreateFixturesTestCase : TestCase
	{
		private readonly ServiceInventoryItemTestData testData;

		public CreateFixturesTestCase(ServiceInventoryItemTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			testData.CreateFixtures();
		}

		public override void PerformCleanup(IEngine engine)
		{
			// Cleanup of every fixture is owned by the test data and triggered by the last test case.
		}
	}
}

namespace SLC_SM_RT_ServiceOrderItem_Edit.TestCases
{
	using System;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	/// <summary>
	/// Creates the Service Specification and Service Order Item that are edited.
	/// </summary>
	internal sealed class CreateFixturesTestCase : TestCase
	{
		private readonly ServiceOrderItemEditTestData testData;

		public CreateFixturesTestCase(ServiceOrderItemEditTestData testData)
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

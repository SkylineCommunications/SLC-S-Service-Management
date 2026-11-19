namespace SLC_SM_RT_ServiceOrderToServiceLink.TestCases
{
	using System;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	/// <summary>
	/// Creates the fixtures the test needs and removes them again once every test case has run.
	/// </summary>
	internal sealed class CreateFixturesTestCase : TestCase
	{
		private readonly ServiceOrderToServiceLinkTestData testData;

		public CreateFixturesTestCase(ServiceOrderToServiceLinkTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			testData.CreateFixtures();
		}

		public override void PerformCleanup(IEngine engine)
		{
			// The test only starts cleaning up after every test case was executed (or the run stopped on a failure),
			// so the fixture owner can safely remove everything here.
			testData.CleanUp();
		}
	}
}

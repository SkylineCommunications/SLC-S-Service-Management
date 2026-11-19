namespace SLC_SM_RT_DynamicDeleteSpec.TestCases
{
	using System;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	/// <summary>
	/// Creates a Service Specification with two linked service items.
	/// </summary>
	internal sealed class CreateFixturesTestCase : TestCase
	{
		private readonly DynamicDeleteSpecTestData testData;

		public CreateFixturesTestCase(DynamicDeleteSpecTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			testData.CreateFixtures();
		}

		public override void PerformCleanup(IEngine engine)
		{
			// Cleanup of the fixture is owned by the test data and triggered by the last test case.
		}
	}
}

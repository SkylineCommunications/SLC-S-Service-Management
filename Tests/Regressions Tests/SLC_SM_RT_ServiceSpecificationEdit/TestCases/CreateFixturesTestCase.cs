namespace SLC_SM_RT_ServiceSpecificationEdit.TestCases
{
	using System;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	/// <summary>
	/// Creates the configuration parameter, its value, the specification configuration item and the specification.
	/// </summary>
	internal sealed class CreateFixturesTestCase : TestCase
	{
		private readonly ServiceSpecificationEditTestData testData;

		public CreateFixturesTestCase(ServiceSpecificationEditTestData testData)
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

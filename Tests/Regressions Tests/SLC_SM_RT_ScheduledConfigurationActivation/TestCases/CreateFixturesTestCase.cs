namespace SLC_SM_RT_ScheduledConfigurationActivation.TestCases
{
	using System;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	/// <summary>
	/// Creates the service under test and the configuration parameter used by the versions.
	/// </summary>
	internal sealed class CreateFixturesTestCase : TestCase
	{
		private readonly ScheduledConfigurationActivationTestData testData;

		public CreateFixturesTestCase(ScheduledConfigurationActivationTestData testData)
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

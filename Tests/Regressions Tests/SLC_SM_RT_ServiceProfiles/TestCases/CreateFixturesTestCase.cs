namespace SLC_SM_RT_ServiceProfiles.TestCases
{
	using System;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	/// <summary>
	/// Creates the configuration parameter, the profile definition referencing it and the specification.
	/// </summary>
	internal sealed class CreateFixturesTestCase : TestCase
	{
		private readonly ServiceProfilesTestData testData;

		public CreateFixturesTestCase(ServiceProfilesTestData testData)
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

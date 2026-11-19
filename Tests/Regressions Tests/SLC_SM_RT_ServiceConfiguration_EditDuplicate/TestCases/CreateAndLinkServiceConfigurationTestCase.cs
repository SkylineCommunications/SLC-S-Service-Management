namespace SLC_SM_RT_ServiceConfiguration_EditDuplicate.TestCases
{
	using System;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	/// <summary>
	/// Creates the configuration item and version, and activates the version on the service.
	/// </summary>
	internal sealed class CreateAndLinkServiceConfigurationTestCase : TestCase
	{
		private readonly ServiceConfigurationEditDuplicateTestData testData;

		public CreateAndLinkServiceConfigurationTestCase(ServiceConfigurationEditDuplicateTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			testData.CreateAndLinkServiceConfiguration();
		}

		public override void PerformCleanup(IEngine engine)
		{
			// Cleanup of every fixture is owned by the test data and triggered by the last test case.
		}
	}
}

namespace SLC_SM_RT_ServiceConfiguration.TestCases
{
	using System;
	using System.Linq;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Verifies the created configuration version is both referenced by and active on the service.
	/// </summary>
	internal sealed class ValidateActiveConfigurationVersionTestCase : TestCase
	{
		private readonly ServiceConfigurationTestData testData;

		public ValidateActiveConfigurationVersionTestCase(ServiceConfigurationTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			Models.Service service = testData.ReadService();

			TestTools.Assert(service != null, "Service was not found after linking configuration version.");
			TestTools.Assert(
				service.ServiceConfigurationId != null,
				"Service active configuration version is not set as expected.");

			TestTools.AssertEqual(
				testData.ServiceConfigurationVersionId,
				service.ServiceConfigurationId.Identifier,
				"Active service configuration version");

			bool hasVersionRef = service.ConfigurationVersions != null
				&& service.ConfigurationVersions.Any(x => x.Identifier == testData.ServiceConfigurationVersionId);

			TestTools.Assert(hasVersionRef, "Service does not contain the expected configuration version reference.");
		}

		public override void PerformCleanup(IEngine engine)
		{
			// Cleanup of every fixture is owned by the test data and triggered by the last test case.
		}
	}
}

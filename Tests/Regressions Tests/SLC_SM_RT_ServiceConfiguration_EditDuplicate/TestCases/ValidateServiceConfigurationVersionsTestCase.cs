namespace SLC_SM_RT_ServiceConfiguration_EditDuplicate.TestCases
{
	using System;
	using System.Linq;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Verifies the service references both configuration versions and has the duplicate as the active one.
	/// </summary>
	internal sealed class ValidateServiceConfigurationVersionsTestCase : TestCase
	{
		private readonly ServiceConfigurationEditDuplicateTestData testData;

		public ValidateServiceConfigurationVersionsTestCase(ServiceConfigurationEditDuplicateTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			Models.Service service = testData.ReadService();

			TestTools.Assert(service != null, "Service was not found during validation.");
			TestTools.Assert(
				service.ServiceConfigurationId != null,
				"Service should have duplicated configuration as active configuration.");

			TestTools.AssertEqual(
				testData.DuplicatedServiceConfigurationVersionId,
				service.ServiceConfigurationId.Identifier,
				"Active service configuration version");

			bool hasSourceVersion = service.ConfigurationVersions != null
				&& service.ConfigurationVersions.Any(x => x.Identifier == testData.ServiceConfigurationVersionId);
			bool hasDuplicateVersion = service.ConfigurationVersions != null
				&& service.ConfigurationVersions.Any(x => x.Identifier == testData.DuplicatedServiceConfigurationVersionId);

			TestTools.Assert(
				hasSourceVersion && hasDuplicateVersion,
				"Service does not reference both source and duplicate configuration versions.");
		}

		public override void PerformCleanup(IEngine engine)
		{
			// Cleanup of every fixture is owned by the test data and triggered by the last test case.
		}
	}
}

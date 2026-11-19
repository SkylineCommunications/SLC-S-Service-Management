namespace SLC_SM_RT_ServiceConfiguration_EditDuplicate.TestCases
{
	using System;
	using System.Linq;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Verifies the source service keeps its own active version and the duplicated service references only its own copy.
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
			Models.Service source = testData.ReadService();

			TestTools.Assert(source != null, "Source service was not found during validation.");
			TestTools.Assert(source.ServiceConfigurationId != null, "Source service has no active configuration version.");
			TestTools.AssertEqual(
				testData.ServiceConfigurationVersionId,
				source.ServiceConfigurationId.Identifier,
				"Active configuration version of the source service");
			TestTools.Assert(
				source.ConfigurationVersions != null
					&& source.ConfigurationVersions.Count == 1
					&& source.ConfigurationVersions[0].Identifier == testData.ServiceConfigurationVersionId,
				"Duplicating must not change the configuration versions of the source service.");

			Models.Service duplicate = testData.ReadDuplicatedService();

			TestTools.Assert(duplicate != null, "Duplicated service was not found during validation.");
			TestTools.Assert(
				!String.IsNullOrWhiteSpace(testData.DuplicatedServiceConfigurationVersionId),
				"Duplicated service has no active configuration version.");
			TestTools.Assert(
				!String.Equals(testData.DuplicatedServiceConfigurationVersionId, testData.ServiceConfigurationVersionId, StringComparison.OrdinalIgnoreCase),
				"Duplicated service must activate a copied configuration version, not the source version.");
			TestTools.Assert(
				duplicate.ConfigurationVersions != null
					&& duplicate.ConfigurationVersions.Count == 1
					&& duplicate.ConfigurationVersions.Any(x => x.Identifier == testData.DuplicatedServiceConfigurationVersionId),
				"Duplicated service must reference exactly its own copied configuration version.");
		}

		public override void PerformCleanup(IEngine engine)
		{
			// Cleanup of every fixture is owned by the test data and triggered by the last test case.
		}
	}
}

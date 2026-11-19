namespace SLC_SM_RT_ServiceConfiguration_EditDuplicate.TestCases
{
	using System;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Verifies the duplicated configuration version is a renamed copy of the edited source version with its own configuration item.
	/// </summary>
	internal sealed class ValidateDuplicatedConfigurationVersionTestCase : TestCase
	{
		private readonly ServiceConfigurationEditDuplicateTestData testData;

		public ValidateDuplicatedConfigurationVersionTestCase(ServiceConfigurationEditDuplicateTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			Models.ServiceConfigurationVersion sourceVersion = testData.ReadConfigurationVersion(testData.ServiceConfigurationVersionId);
			Models.ServiceConfigurationVersion duplicateVersion = testData.ReadConfigurationVersion(testData.DuplicatedServiceConfigurationVersionId);

			TestTools.Assert(sourceVersion != null, "Source configuration version was not found during validation.");
			TestTools.Assert(duplicateVersion != null, "Duplicated configuration version was not found during validation.");

			TestTools.AssertEqual($"{testData.EditedVersionName} (Copy)", duplicateVersion.VersionName, "Duplicated configuration version name");
			TestTools.AssertEqual(sourceVersion.Description, duplicateVersion.Description, "Duplicated configuration version description");

			TestTools.Assert(
				duplicateVersion.Parameters != null && duplicateVersion.Parameters.Count == 1,
				$"Duplicated configuration version should hold exactly 1 configuration item, found {duplicateVersion.Parameters?.Count ?? 0}.");
			TestTools.Assert(
				!String.IsNullOrWhiteSpace(testData.DuplicatedServiceConfigurationValueId)
					&& !String.Equals(testData.DuplicatedServiceConfigurationValueId, testData.ServiceConfigurationValueId, StringComparison.OrdinalIgnoreCase),
				"Duplicated configuration version must hold a new configuration item, not the source one.");
		}

		public override void PerformCleanup(IEngine engine)
		{
			// Cleanup of every fixture is owned by the test data and triggered by the last test case.
		}
	}
}

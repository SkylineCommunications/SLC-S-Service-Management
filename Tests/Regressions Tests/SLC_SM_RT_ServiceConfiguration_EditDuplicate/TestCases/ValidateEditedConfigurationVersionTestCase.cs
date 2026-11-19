namespace SLC_SM_RT_ServiceConfiguration_EditDuplicate.TestCases
{
	using System;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Verifies the rename of the source configuration version was persisted.
	/// </summary>
	internal sealed class ValidateEditedConfigurationVersionTestCase : TestCase
	{
		private readonly ServiceConfigurationEditDuplicateTestData testData;

		public ValidateEditedConfigurationVersionTestCase(ServiceConfigurationEditDuplicateTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			Models.ServiceConfigurationVersion sourceVersion = testData.ReadConfigurationVersion(testData.ServiceConfigurationVersionId);

			TestTools.Assert(sourceVersion != null, "Source configuration version was not found during validation.");
			TestTools.AssertEqual(testData.EditedVersionName, sourceVersion.VersionName, "Edited source configuration version name");
		}

		public override void PerformCleanup(IEngine engine)
		{
			// Cleanup of every fixture is owned by the test data and triggered by the last test case.
		}
	}
}

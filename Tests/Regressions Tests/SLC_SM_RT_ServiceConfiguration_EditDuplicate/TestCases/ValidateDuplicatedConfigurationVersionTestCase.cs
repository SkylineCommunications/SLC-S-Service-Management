namespace SLC_SM_RT_ServiceConfiguration_EditDuplicate.TestCases
{
	using System;
	using System.Linq;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Verifies the duplicated configuration version carries its own configuration item.
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
			Models.ServiceConfigurationVersion duplicateVersion = testData.ReadConfigurationVersion(testData.DuplicatedServiceConfigurationVersionId);

			TestTools.Assert(duplicateVersion != null, "Duplicated configuration version was not found during validation.");

			bool hasDuplicateValue = duplicateVersion.Parameters != null
				&& duplicateVersion.Parameters.Any(x => x.Identifier == testData.DuplicatedServiceConfigurationValueId);

			TestTools.Assert(
				hasDuplicateValue,
				"Duplicate validation failed: duplicate version missing duplicated configuration item.");
		}

		public override void PerformCleanup(IEngine engine)
		{
			// Cleanup of every fixture is owned by the test data and triggered by the last test case.
		}
	}
}

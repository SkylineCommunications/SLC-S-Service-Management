namespace SLC_SM_RT_ServiceConfigurationItem_EditDuplicate.TestCases
{
	using System;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Verifies the edit on the source Service Configuration Item was persisted.
	/// </summary>
	internal sealed class ValidateEditedConfigurationItemTestCase : TestCase
	{
		private readonly ServiceConfigurationItemEditDuplicateTestData testData;

		public ValidateEditedConfigurationItemTestCase(ServiceConfigurationItemEditDuplicateTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			Models.ServiceConfigurationValue source = testData.ReadServiceConfigurationValue(testData.ServiceConfigurationValueId);

			TestTools.Assert(source != null, "Source service configuration item was not found during validation.");
			TestTools.Assert(source.Mandatory, "Edit validation failed: source Mandatory flag should be true.");
			TestTools.Assert(
				source.ConfigurationParameterId != null,
				"Source service configuration item has no linked parameter value.");

			TestTools.AssertEqual(
				testData.ParameterValueId,
				source.ConfigurationParameterId.Identifier,
				"Source linked configuration parameter value");
		}

		public override void PerformCleanup(IEngine engine)
		{
			// Cleanup of every fixture is owned by the test data and triggered by the last test case.
		}
	}
}

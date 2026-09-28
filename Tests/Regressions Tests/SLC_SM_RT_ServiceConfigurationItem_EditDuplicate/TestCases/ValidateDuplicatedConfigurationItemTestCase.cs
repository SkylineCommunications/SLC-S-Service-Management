namespace SLC_SM_RT_ServiceConfigurationItem_EditDuplicate.TestCases
{
	using System;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Verifies the duplicate points to its own parameter value and inherited the edited Mandatory flag.
	/// </summary>
	internal sealed class ValidateDuplicatedConfigurationItemTestCase : TestCase
	{
		private readonly ServiceConfigurationItemEditDuplicateTestData testData;

		public ValidateDuplicatedConfigurationItemTestCase(ServiceConfigurationItemEditDuplicateTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			Models.ServiceConfigurationValue duplicate = testData.ReadServiceConfigurationValue(testData.DuplicatedServiceConfigurationValueId);

			TestTools.Assert(duplicate != null, "Duplicated service configuration item was not found during validation.");
			TestTools.Assert(
				duplicate.ConfigurationParameterId != null,
				"Duplicated service configuration item has no linked parameter value.");

			TestTools.AssertEqual(
				testData.DuplicatedParameterValueId,
				duplicate.ConfigurationParameterId.Identifier,
				"Duplicated linked configuration parameter value");

			TestTools.Assert(
				duplicate.Mandatory,
				"Duplicate validation failed: duplicated service configuration item should inherit Mandatory=true.");
		}

		public override void PerformCleanup(IEngine engine)
		{
			testData.CleanUp();
		}
	}
}

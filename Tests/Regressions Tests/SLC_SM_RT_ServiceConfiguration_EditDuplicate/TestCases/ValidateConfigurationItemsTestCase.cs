namespace SLC_SM_RT_ServiceConfiguration_EditDuplicate.TestCases
{
	using System;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Verifies both the source and duplicated configuration items point to their own parameter value.
	/// </summary>
	internal sealed class ValidateConfigurationItemsTestCase : TestCase
	{
		private readonly ServiceConfigurationEditDuplicateTestData testData;

		public ValidateConfigurationItemsTestCase(ServiceConfigurationEditDuplicateTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			Models.ServiceConfigurationValue sourceValue = testData.ReadConfigurationValue(testData.ServiceConfigurationValueId);

			TestTools.Assert(sourceValue != null, "Source configuration item was not found during validation.");
			TestTools.Assert(
				sourceValue.ConfigurationParameterId != null,
				"Source configuration item has no linked parameter value.");

			TestTools.AssertEqual(
				testData.ParameterValueId,
				sourceValue.ConfigurationParameterId.Identifier,
				"Source linked configuration parameter value");

			Models.ServiceConfigurationValue duplicateValue = testData.ReadConfigurationValue(testData.DuplicatedServiceConfigurationValueId);

			TestTools.Assert(duplicateValue != null, "Duplicated configuration item was not found during validation.");
			TestTools.Assert(
				duplicateValue.ConfigurationParameterId != null,
				"Duplicated configuration item has no linked parameter value.");

			TestTools.AssertEqual(
				testData.DuplicatedParameterValueId,
				duplicateValue.ConfigurationParameterId.Identifier,
				"Duplicated linked configuration parameter value");
		}

		public override void PerformCleanup(IEngine engine)
		{
			testData.CleanUp();
		}
	}
}

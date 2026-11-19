namespace SLC_SM_RT_ServiceConfiguration_EditDuplicate.TestCases
{
	using System;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using ConfigModels = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;
	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Verifies the source and duplicated configuration items point to their own parameter value,
	/// and that the duplicated parameter value is a copy of the source value.
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
			TestTools.Assert(sourceValue.Mandatory == duplicateValue.Mandatory, "Duplicated configuration item must keep the mandatory flag of the source item.");
			TestTools.Assert(
				!String.Equals(testData.ParameterValueId, duplicateValue.ConfigurationParameterId.Identifier, StringComparison.OrdinalIgnoreCase),
				"Duplicated configuration item must point to a copied parameter value, not the source one.");

			ConfigModels.ConfigurationParameterValue sourceParameterValue = testData.ReadParameterValue(testData.ParameterValueId);
			ConfigModels.ConfigurationParameterValue duplicateParameterValue = testData.ReadParameterValue(duplicateValue.ConfigurationParameterId.Identifier);

			TestTools.Assert(sourceParameterValue != null, "Source parameter value was not found during validation.");
			TestTools.Assert(duplicateParameterValue != null, "Duplicated parameter value was not found during validation.");
			TestTools.AssertEqual(sourceParameterValue.Label, duplicateParameterValue.Label, "Duplicated parameter value label");
			TestTools.Assert(Equals(sourceParameterValue.Type, duplicateParameterValue.Type), "Duplicated parameter value must keep the type of the source value.");
			TestTools.AssertEqual(sourceParameterValue.StringValue, duplicateParameterValue.StringValue, "Duplicated parameter string value");
			TestTools.Assert(Equals(sourceParameterValue.DoubleValue, duplicateParameterValue.DoubleValue), "Duplicated parameter value must keep the double value of the source value.");
			TestTools.AssertEqual(
				sourceParameterValue.ConfigurationParameterId.Identifier,
				duplicateParameterValue.ConfigurationParameterId.Identifier,
				"Duplicated parameter value configuration parameter");
			TestTools.Assert(
				ServiceConfigurationEditDuplicateTestData.HasValue(duplicateParameterValue),
				"The mandatory value must be copied so that the duplicated active configuration version stays valid.");
		}

		public override void PerformCleanup(IEngine engine)
		{
			testData.CleanUp();
		}
	}
}

namespace SLC_SM_RT_ServiceSpecificationConfiguration_Edit.TestCases
{
	using System;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using ConfigModels = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;
	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Verifies the edits on the source parameter value and specification configuration were persisted.
	/// </summary>
	internal sealed class ValidateEditedConfigurationTestCase : TestCase
	{
		private readonly ServiceSpecificationConfigurationEditTestData testData;

		public ValidateEditedConfigurationTestCase(ServiceSpecificationConfigurationEditTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			ConfigModels.ConfigurationParameterValue sourceParameterValue = testData.ReadParameterValue(testData.ParameterValueId);

			TestTools.Assert(sourceParameterValue != null, "Edited source parameter value was not found.");
			TestTools.AssertEqual(testData.EditedLabel, sourceParameterValue.Label, "Edited source parameter value label");

			Models.ServiceSpecificationConfigurationValue sourceConfiguration = testData.ReadSpecificationConfiguration(testData.SpecificationConfigurationId);

			TestTools.Assert(sourceConfiguration != null, "Edited source specification configuration was not found.");
			TestTools.Assert(
				!sourceConfiguration.ExposeAtServiceOrder && sourceConfiguration.MandatoryAtServiceOrder && sourceConfiguration.MandatoryAtService,
				"Edit validation failed: source specification configuration flags mismatch.");
		}

		public override void PerformCleanup(IEngine engine)
		{
			testData.CleanUp();
		}
	}
}

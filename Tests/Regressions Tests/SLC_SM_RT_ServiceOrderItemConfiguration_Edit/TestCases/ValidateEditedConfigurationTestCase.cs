namespace SLC_SM_RT_ServiceOrderItemConfiguration_Edit.TestCases
{
	using System;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using ConfigModels = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;
	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Verifies the edits on the order item configuration and parameter value were persisted.
	/// </summary>
	internal sealed class ValidateEditedConfigurationTestCase : TestCase
	{
		private readonly ServiceOrderItemConfigurationEditTestData testData;

		public ValidateEditedConfigurationTestCase(ServiceOrderItemConfigurationEditTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			Models.ServiceOrderItemConfigurationValue sourceConfiguration = testData.ReadOrderItemConfiguration(testData.OrderItemConfigurationId);

			TestTools.Assert(sourceConfiguration != null, "Source configuration was not found during validation.");
			TestTools.Assert(
				!sourceConfiguration.Mandatory,
				"Edit validation failed: source configuration Mandatory should be false.");

			ConfigModels.ConfigurationParameterValue sourceParameterValue = testData.ReadParameterValue(testData.ParameterValueId);

			TestTools.Assert(sourceParameterValue != null, "Source configuration parameter value was not found during validation.");
			TestTools.AssertEqual(testData.EditedLabel, sourceParameterValue.Label, "Edited source parameter value label");
		}

		public override void PerformCleanup(IEngine engine)
		{
			testData.CleanUp();
		}
	}
}

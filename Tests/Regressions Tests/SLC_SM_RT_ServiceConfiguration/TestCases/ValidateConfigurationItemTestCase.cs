namespace SLC_SM_RT_ServiceConfiguration.TestCases
{
	using System;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Verifies the created configuration item points to the created configuration parameter value.
	/// </summary>
	internal sealed class ValidateConfigurationItemTestCase : TestCase
	{
		private readonly ServiceConfigurationTestData testData;

		public ValidateConfigurationItemTestCase(ServiceConfigurationTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			Models.ServiceConfigurationValue value = testData.ReadConfigurationValue();

			TestTools.Assert(value != null, "Service configuration item was not found.");
			TestTools.Assert(
				value.ConfigurationParameterId != null,
				"Service configuration item has no linked parameter value.");

			TestTools.AssertEqual(
				testData.ParameterValueId,
				value.ConfigurationParameterId.Identifier,
				"Linked configuration parameter value");
		}

		public override void PerformCleanup(IEngine engine)
		{
			testData.CleanUp();
		}
	}
}

namespace SLC_SM_RT_ServiceOrderItemConfiguration.TestCases
{
	using System;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Verifies the order item configuration points to the created configuration parameter value.
	/// </summary>
	internal sealed class ValidateConfigurationParameterValueTestCase : TestCase
	{
		private readonly ServiceOrderItemConfigurationTestData testData;

		public ValidateConfigurationParameterValueTestCase(ServiceOrderItemConfigurationTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			Models.ServiceOrderItemConfigurationValue persisted = testData.ReadOrderItemConfiguration();

			TestTools.Assert(persisted != null, "Order item configuration value was not found.");
			TestTools.Assert(
				persisted.ConfigurationParameterValueId != null,
				"Order item configuration has no linked parameter value.");

			TestTools.AssertEqual(
				testData.ParameterValueId,
				persisted.ConfigurationParameterValueId.Identifier,
				"Linked configuration parameter value");
		}

		public override void PerformCleanup(IEngine engine)
		{
			testData.CleanUp();
		}
	}
}

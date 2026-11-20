namespace SLC_SM_RT_ServiceSpecificationConfiguration.TestCases
{
	using System;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Verifies the specification configuration points to the created configuration parameter value.
	/// </summary>
	internal sealed class ValidateConfigurationParameterValueTestCase : TestCase
	{
		private readonly ServiceSpecificationConfigurationTestData testData;

		public ValidateConfigurationParameterValueTestCase(ServiceSpecificationConfigurationTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			Models.ServiceSpecificationConfigurationValue persisted = testData.ReadSpecificationConfiguration();

			TestTools.Assert(persisted != null, "Service specification configuration value was not found.");
			TestTools.Assert(
				persisted.ConfigurationParameterId != null,
				"Specification configuration has no linked parameter value.");

			TestTools.AssertEqual(
				testData.ParameterValueId,
				persisted.ConfigurationParameterId.Identifier,
				"Linked configuration parameter value");
		}

		public override void PerformCleanup(IEngine engine)
		{
			testData.CleanUp();
		}
	}
}

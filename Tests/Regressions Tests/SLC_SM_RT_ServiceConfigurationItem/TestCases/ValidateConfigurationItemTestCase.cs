namespace SLC_SM_RT_ServiceConfigurationItem.TestCases
{
	using System;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Verifies the updated Service Configuration Item was persisted and still points to its parameter value.
	/// </summary>
	internal sealed class ValidateConfigurationItemTestCase : TestCase
	{
		private readonly ServiceConfigurationItemTestData testData;

		public ValidateConfigurationItemTestCase(ServiceConfigurationItemTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			Models.ServiceConfigurationValue persisted = testData.ReadServiceConfigurationValue();

			TestTools.Assert(persisted != null, "Service configuration item was not found after update.");
			TestTools.Assert(persisted.Mandatory, "Service configuration item Mandatory flag was not updated.");
			TestTools.Assert(
				persisted.ConfigurationParameterId != null,
				"Service configuration item has no linked parameter value.");

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

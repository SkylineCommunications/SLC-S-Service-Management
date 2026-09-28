namespace SLC_SM_RT_ServiceConfiguration.TestCases
{
	using System;
	using System.Linq;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Verifies the configuration version holds a reference to the created configuration item.
	/// </summary>
	internal sealed class ValidateConfigurationVersionParametersTestCase : TestCase
	{
		private readonly ServiceConfigurationTestData testData;

		public ValidateConfigurationVersionParametersTestCase(ServiceConfigurationTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			Models.ServiceConfigurationVersion version = testData.ReadConfigurationVersion();

			TestTools.Assert(version != null, "Service configuration version was not found.");

			bool hasValueRef = version.Parameters != null
				&& version.Parameters.Any(x => x.Identifier == testData.ServiceConfigurationValueId);

			TestTools.Assert(hasValueRef, "Configuration version does not contain the expected configuration item reference.");
		}

		public override void PerformCleanup(IEngine engine)
		{
			// Cleanup of every fixture is owned by the test data and triggered by the last test case.
		}
	}
}

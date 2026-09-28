namespace SLC_SM_RT_ServiceSpecificationConfiguration.TestCases
{
	using System;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	/// <summary>
	/// Creates the configuration parameter value and specification configuration, and links them to the specification.
	/// </summary>
	internal sealed class CreateAndLinkSpecificationConfigurationTestCase : TestCase
	{
		private readonly ServiceSpecificationConfigurationTestData testData;

		public CreateAndLinkSpecificationConfigurationTestCase(ServiceSpecificationConfigurationTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			testData.CreateAndLinkSpecificationConfiguration();
		}

		public override void PerformCleanup(IEngine engine)
		{
			// Cleanup of every fixture is owned by the test data and triggered by the last test case.
		}
	}
}

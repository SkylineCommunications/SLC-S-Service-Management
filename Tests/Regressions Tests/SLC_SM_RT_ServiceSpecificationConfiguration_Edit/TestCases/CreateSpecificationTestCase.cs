namespace SLC_SM_RT_ServiceSpecificationConfiguration_Edit.TestCases
{
	using System;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	/// <summary>
	/// Creates the Service Specification the configurations are linked to.
	/// </summary>
	internal sealed class CreateSpecificationTestCase : TestCase
	{
		private readonly ServiceSpecificationConfigurationEditTestData testData;

		public CreateSpecificationTestCase(ServiceSpecificationConfigurationEditTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			testData.CreateSpecification();
		}

		public override void PerformCleanup(IEngine engine)
		{
			// Cleanup of every fixture is owned by the test data and triggered by the last test case.
		}
	}
}

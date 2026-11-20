namespace SLC_SM_RT_ServiceSpecificationConfiguration_Edit.TestCases
{
	using System;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	/// <summary>
	/// Edits the parameter value label and the flags of the specification configuration.
	/// </summary>
	internal sealed class EditConfigurationTestCase : TestCase
	{
		private readonly ServiceSpecificationConfigurationEditTestData testData;

		public EditConfigurationTestCase(ServiceSpecificationConfigurationEditTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			testData.EditConfiguration();
		}

		public override void PerformCleanup(IEngine engine)
		{
			// Cleanup of every fixture is owned by the test data and triggered by the last test case.
		}
	}
}

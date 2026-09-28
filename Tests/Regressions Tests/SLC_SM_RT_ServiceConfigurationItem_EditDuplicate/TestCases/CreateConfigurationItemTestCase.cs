namespace SLC_SM_RT_ServiceConfigurationItem_EditDuplicate.TestCases
{
	using System;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	/// <summary>
	/// Creates the Configuration Parameter Value and the Service Configuration Item under test.
	/// </summary>
	internal sealed class CreateConfigurationItemTestCase : TestCase
	{
		private readonly ServiceConfigurationItemEditDuplicateTestData testData;

		public CreateConfigurationItemTestCase(ServiceConfigurationItemEditDuplicateTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			testData.CreateConfigurationItem();
		}

		public override void PerformCleanup(IEngine engine)
		{
			// Cleanup of every fixture is owned by the test data and triggered by the last test case.
		}
	}
}

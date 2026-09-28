namespace SLC_SM_RT_ServiceConfigurationItem_EditDuplicate.TestCases
{
	using System;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	/// <summary>
	/// Edits the Mandatory flag of the source Service Configuration Item.
	/// </summary>
	internal sealed class EditConfigurationItemTestCase : TestCase
	{
		private readonly ServiceConfigurationItemEditDuplicateTestData testData;

		public EditConfigurationItemTestCase(ServiceConfigurationItemEditDuplicateTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			testData.EditConfigurationItem();
		}

		public override void PerformCleanup(IEngine engine)
		{
			// Cleanup of every fixture is owned by the test data and triggered by the last test case.
		}
	}
}

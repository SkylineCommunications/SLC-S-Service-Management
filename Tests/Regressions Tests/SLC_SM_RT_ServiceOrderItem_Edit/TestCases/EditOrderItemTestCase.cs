namespace SLC_SM_RT_ServiceOrderItem_Edit.TestCases
{
	using System;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	/// <summary>
	/// Edits the name and end time of the Service Order Item.
	/// </summary>
	internal sealed class EditOrderItemTestCase : TestCase
	{
		private readonly ServiceOrderItemEditTestData testData;

		public EditOrderItemTestCase(ServiceOrderItemEditTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			testData.EditOrderItem();
		}

		public override void PerformCleanup(IEngine engine)
		{
			// Cleanup of every fixture is owned by the test data and triggered by the last test case.
		}
	}
}

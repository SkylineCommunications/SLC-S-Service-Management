namespace SLC_SM_RT_ServiceOrderItem_Edit.TestCases
{
	using System;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Verifies the edit on the Service Order Item was persisted.
	/// </summary>
	internal sealed class ValidateEditedOrderItemTestCase : TestCase
	{
		private readonly ServiceOrderItemEditTestData testData;

		public ValidateEditedOrderItemTestCase(ServiceOrderItemEditTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			Models.ServiceOrderItem edited = testData.ReadOrderItem(testData.OrderItemId);

			TestTools.Assert(edited != null, "Edited order item was not found.");
			TestTools.AssertEqual(testData.EditedName, edited.Name, "Edited order item name");
		}

		public override void PerformCleanup(IEngine engine)
		{
			testData.CleanUp();
		}
	}
}

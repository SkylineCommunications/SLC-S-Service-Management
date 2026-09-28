namespace SLC_SM_RT_ServiceOrderItem.TestCases
{
	using System;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Verifies the created Service Order Item was persisted and stayed linked to its Service Specification.
	/// </summary>
	internal sealed class ValidateOrderItemTestCase : TestCase
	{
		private readonly ServiceOrderItemTestData testData;

		public ValidateOrderItemTestCase(ServiceOrderItemTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			Models.ServiceOrderItem persisted = testData.ReadOrderItem();

			TestTools.Assert(persisted != null, "Created service order item was not found.");
			TestTools.Assert(
				persisted.ServiceInfo != null && persisted.ServiceInfo.SpecificationId != null,
				"Created service order item has no linked specification.");

			TestTools.AssertEqual(
				testData.SpecificationId,
				persisted.ServiceInfo.SpecificationId.Identifier,
				"Linked service specification");
		}

		public override void PerformCleanup(IEngine engine)
		{
			testData.CleanUp();
		}
	}
}

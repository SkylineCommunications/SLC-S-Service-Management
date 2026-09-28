namespace SLC_SM_RT_CreateServiceInventoryItem.TestCases
{
	using System;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Verifies that the script under test linked a correctly configured service to the service order item.
	/// </summary>
	internal sealed class ValidateServiceInventoryItemTestCase : TestCase
	{
		private readonly ServiceInventoryItemTestData testData;

		public ValidateServiceInventoryItemTestCase(ServiceInventoryItemTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			Models.Service service = testData.ReadLinkedService();

			TestTools.Assert(service != null, "No service was created and linked to the service order item.");
			TestTools.AssertEqual(testData.SpecificationId, service.ServiceSpecificationId.Identifier, "Linked service specification");
			TestTools.AssertEqual(testData.ExpectedServiceName, service.Name, "Linked service name");
		}

		public override void PerformCleanup(IEngine engine)
		{
			testData.CleanUp();
		}
	}
}

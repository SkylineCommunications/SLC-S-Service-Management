namespace SLC_SM_RT_AddServiceItem.TestCases
{
	using System;

	using DomHelpers.SlcServicemanagement;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Verifies the added service item exists and carries the requested type and label.
	/// </summary>
	internal sealed class ValidateAddedServiceItemTestCase : TestCase
	{
		private readonly AddServiceItemTestData testData;

		public ValidateAddedServiceItemTestCase(AddServiceItemTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			Models.ServiceItem addedItem = testData.ReadAddedServiceItem();

			TestTools.Assert(
				addedItem != null,
				$"No service item with definition reference '{testData.DefinitionReference}' was added.");

			TestTools.AssertEqual(
				SlcServicemanagementIds.Enums.ServiceitemtypesEnum.Service.ToString(),
				addedItem.Type.ToString(),
				"Added service item type");

			TestTools.AssertEqual(
				testData.DefinitionReference,
				addedItem.Label,
				"Added service item label");
		}

		public override void PerformCleanup(IEngine engine)
		{
			testData.CleanUp();
		}
	}
}

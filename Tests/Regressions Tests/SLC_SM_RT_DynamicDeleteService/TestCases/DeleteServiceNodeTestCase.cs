namespace SLC_SM_RT_DynamicDeleteService.TestCases
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Removes service item "0" from the service and verifies that the item and every relationship
	/// that referenced it are gone, while the other service items survive.
	/// </summary>
	internal sealed class DeleteServiceNodeTestCase : TestCase
	{
		private readonly DynamicDeleteServiceTestData testData;

		public DeleteServiceNodeTestCase(DynamicDeleteServiceTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			var scriptParameters = new Dictionary<string, string>
			{
				{ "DomId", testData.ServiceId },
				{ "NodeIds", "0" },
				{ "ConnectionIds", String.Empty },
			};

			TestTools.ExecuteSubScript(this, engine, Script.ScriptUnderTest, scriptParameters);

			Models.Service service = testData.ReadServiceOrThrow();

			TestTools.Assert(
				!HasServiceItem(service, 0),
				"Service item '0' still exists after it was deleted.");

			TestTools.Assert(
				HasServiceItem(service, 1) && HasServiceItem(service, 2),
				"Service items '1' and '2' should have survived the deletion of service item '0'.");

			bool referencesDeletedNode = (service.ServiceItemsRelationships ?? new List<Models.ServiceItemRelationship>())
				.Any(relationship => relationship.ParentServiceItem == "0" || relationship.ChildServiceItem == "0");

			TestTools.Assert(
				!referencesDeletedNode,
				"A service item relationship still references the deleted service item '0'.");

			TestTools.AssertEqual(
				"1",
				(service.ServiceItemsRelationships?.Count ?? 0).ToString(),
				"Number of remaining service item relationships");
		}

		public override void PerformCleanup(IEngine engine)
		{
			// Cleanup of every fixture is owned by the test data and triggered by the last test case.
		}

		private static bool HasServiceItem(Models.Service service, int serviceItemId)
		{
			return (service.ServiceItems ?? new List<Models.ServiceItem>())
				.Any(item => item.ServiceItemID.HasValue && item.ServiceItemID.Value == serviceItemId);
		}
	}
}

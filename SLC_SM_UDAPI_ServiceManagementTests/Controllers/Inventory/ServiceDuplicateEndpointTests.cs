namespace SLC_SM_UDAPI_ServiceManagement.Tests.Controllers
{
	using System.Collections.Generic;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	using ItemType = DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Enums.ServiceItemTypesEnum;

	[TestClass]
	public class ServiceDuplicateEndpointTests : ServiceInventoryControllerTestBase
	{
		[TestMethod]
		public void DuplicateService_CopiesSourceItemsAndRelationshipsAndClearsMonitoringLink()
		{
			var source = AddService();
			source.GenerateMonitoringService = true;
			source.MonitoringService = "MONITORING-1";
			source.ServiceItems = new List<ServiceItem>
			{
				new ServiceItem
				{
					ServiceItemID = 3,
					Label = "Workflow",
					Type = ItemType.Workflow,
					DefinitionReference = "definition",
					ImplementationReference = "stale-implementation",
					Script = "script",
					Icon = "icon",
				},
			};
			source.ServiceItemsRelationships = new List<ServiceItemRelationship>
			{
				new ServiceItemRelationship { ParentServiceItem = "3", ChildServiceItem = "4" },
			};

			var result = Controller.DuplicateService(source.ServiceID);

			Assert.AreEqual(201, StatusCode(result));
			var duplicate = StoredServices.Single(service => !ReferenceEquals(service, source));
			Assert.AreEqual("SERVICE-00002", duplicate.ServiceID);
			Assert.IsFalse(duplicate.GenerateMonitoringService);
			Assert.AreEqual(String.Empty, duplicate.MonitoringService);
			Assert.AreEqual(1, duplicate.ServiceItems.Count);
			Assert.AreEqual(3L, duplicate.ServiceItems[0].ServiceItemID);
			Assert.AreEqual(ItemType.Workflow, duplicate.ServiceItems[0].Type);
			Assert.AreEqual("definition", duplicate.ServiceItems[0].DefinitionReference);
			Assert.AreEqual(String.Empty, duplicate.ServiceItems[0].ImplementationReference);
			Assert.AreEqual("script", duplicate.ServiceItems[0].Script);
			Assert.AreEqual("icon", duplicate.ServiceItems[0].Icon);
			Assert.AreEqual("3", duplicate.ServiceItemsRelationships.Single().ParentServiceItem);
			Assert.AreEqual("4", duplicate.ServiceItemsRelationships.Single().ChildServiceItem);
		}
	}
}

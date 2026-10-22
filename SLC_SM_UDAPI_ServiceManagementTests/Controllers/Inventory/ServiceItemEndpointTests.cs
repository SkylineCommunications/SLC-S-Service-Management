namespace SLC_SM_UDAPI_ServiceManagement.Tests.Controllers
{
	using System.Collections.Generic;
	using Moq;
	using Newtonsoft.Json.Linq;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using SLC_SM_UDAPI_ServiceManagement.Helpers.Inventory;
	using SLC_SM_UDAPI_ServiceManagement.Models.Inventory;

	using ItemType = DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Enums.ServiceItemTypesEnum;

	[TestClass]
	public class ServiceItemEndpointTests : ServiceInventoryControllerTestBase
	{
		// ---- GET /service/{id}/item ----
		[TestMethod]
		public void ListServiceItems_ReturnsItemsSortedByIdWithImplementation()
		{
			var linked = AddService("Linked", number: 2);
			var service = AddService();
			var job = AddItem(service, 3, "Job", ItemType.Workflow, Guid.NewGuid().ToString());
			AddItem(service, 1, "Svc", ItemType.Service, linked.Identifier);
			ItemImplementations.Setup(i => i.Describe(job)).Returns(new ServiceItemImplementation { Name = "Job 1", State = "Running", MonitoringServiceState = "Normal" });

			var items = Value<List<JObject>>(Controller.ListServiceItems(service.ServiceID));

			Assert.AreEqual(2, items.Count);
			Assert.AreEqual("Svc", (string)items[0]["Label"]);
			Assert.AreEqual("Linked", (string)items[0]["ImplementationName"]);
			Assert.AreEqual(String.Empty, (string)items[0]["ImplementationState"]);
			Assert.AreEqual("Job 1", (string)items[1]["ImplementationName"]);
			Assert.AreEqual("Running", (string)items[1]["ImplementationState"]);
			Assert.AreEqual("Normal", (string)items[1]["MonitoringServiceState"]);
		}

		[TestMethod]
		public void ListServiceItems_DescribeFails_ReturnsItemWithEmptyImplementation()
		{
			var service = AddService();
			AddItem(service, 0, "Booking", ItemType.SRMBooking, Guid.NewGuid().ToString());
			ItemImplementations.Setup(i => i.Describe(It.IsAny<ServiceItem>())).Throws(new InvalidOperationException("SRM unavailable"));

			var items = Value<List<JObject>>(Controller.ListServiceItems(service.ServiceID));

			Assert.AreEqual(1, items.Count);
			Assert.AreEqual(String.Empty, (string)items[0]["ImplementationName"]);
		}

		[TestMethod]
		public void ListServiceItems_UnknownService_Returns404()
		{
			Assert.AreEqual(404, StatusCode(Controller.ListServiceItems("SERVICE-00099")));
		}

		// ---- POST /service/{id}/item ----
		[TestMethod]
		public void AddServiceItem_Valid_AddsItemWithNextIdAndServiceIcon()
		{
			var service = AddService();
			service.Icon = "logo";
			AddItem(service, 4, "Existing", ItemType.Service, null);

			var result = Controller.AddServiceItem(service.ServiceID, new ServiceRequestItem { Label = " New ", Type = "SRM Booking", DefinitionReference = "def" });

			Assert.AreEqual(201, StatusCode(result));
			var item = service.ServiceItems.Single(i => i.Label == "New");
			Assert.AreEqual(5L, item.ServiceItemID);
			Assert.AreEqual(ItemType.SRMBooking, item.Type);
			Assert.AreEqual("def", item.DefinitionReference);
			Assert.AreEqual("logo", item.Icon);
			Services.Verify(r => r.Update(service), Times.Once);
			ItemImplementations.Verify(i => i.Link(service, item), Times.Once);
		}

		[TestMethod]
		public void AddServiceItem_FirstItemWithoutType_GetsIdZeroAndTypeService()
		{
			var service = AddService();

			Assert.AreEqual(201, StatusCode(Controller.AddServiceItem(service.ServiceID, new ServiceRequestItem { Label = "A" })));

			var item = service.ServiceItems.Single();
			Assert.AreEqual(0L, item.ServiceItemID);
			Assert.AreEqual(ItemType.Service, item.Type);
		}

		[TestMethod]
		public void AddServiceItem_DuplicateLabel_Returns409()
		{
			var service = AddService();
			AddItem(service, 0, "Item", ItemType.Service, null);

			Assert.AreEqual(409, StatusCode(Controller.AddServiceItem(service.ServiceID, new ServiceRequestItem { Label = "ITEM" })));
			Services.Verify(r => r.Update(It.IsAny<Service>()), Times.Never);
		}

		[DataTestMethod]
		[DataRow(null, "Service")]
		[DataRow(" ", "Service")]
		[DataRow("Item", "Unknown")]
		public void AddServiceItem_InvalidRequest_Returns400(string label, string type)
		{
			var service = AddService();

			Assert.AreEqual(400, StatusCode(Controller.AddServiceItem(service.ServiceID, new ServiceRequestItem { Label = label, Type = type })));
			Assert.AreEqual(0, service.ServiceItems.Count);
		}

		[TestMethod]
		public void AddServiceItem_LinkFails_Returns500AfterSaving()
		{
			var service = AddService();
			ItemImplementations.Setup(i => i.Link(It.IsAny<Service>(), It.IsAny<ServiceItem>())).Throws(new InvalidOperationException("boom"));

			Assert.AreEqual(500, StatusCode(Controller.AddServiceItem(service.ServiceID, new ServiceRequestItem { Label = "A" })));
			Assert.AreEqual(1, service.ServiceItems.Count);
		}

		// ---- PATCH /service/{id}/item/{itemId} ----
		[TestMethod]
		public void UpdateServiceItem_PartialPatch_KeepsIdAndOtherFields()
		{
			var service = AddService();
			service.Icon = "logo";
			var item = AddItem(service, 2, "Old", ItemType.Workflow, "ref");
			item.Script = "script";

			var result = Controller.UpdateServiceItem(service.ServiceID, "2", JObject.Parse("{ \"label\": \"New\", \"definitionReference\": \"def\" }"));

			Assert.AreEqual(200, StatusCode(result));
			Assert.AreEqual(2L, item.ServiceItemID);
			Assert.AreEqual("New", item.Label);
			Assert.AreEqual("def", item.DefinitionReference);
			Assert.AreEqual("ref", item.ImplementationReference);
			Assert.AreEqual("script", item.Script);
			Assert.AreEqual(ItemType.Workflow, item.Type);
			Assert.AreEqual("logo", item.Icon);
			ItemImplementations.Verify(i => i.Link(service, item), Times.Once);
		}

		[TestMethod]
		public void UpdateServiceItem_SameLabelDifferentCase_Returns200()
		{
			var service = AddService();
			AddItem(service, 0, "Item", ItemType.Service, null);

			Assert.AreEqual(200, StatusCode(Controller.UpdateServiceItem(service.ServiceID, "0", JObject.Parse("{ \"label\": \"ITEM\" }"))));
		}

		[TestMethod]
		public void UpdateServiceItem_LabelOfOtherItem_Returns409()
		{
			var service = AddService();
			AddItem(service, 0, "A", ItemType.Service, null);
			AddItem(service, 1, "B", ItemType.Service, null);

			Assert.AreEqual(409, StatusCode(Controller.UpdateServiceItem(service.ServiceID, "1", JObject.Parse("{ \"label\": \"a\" }"))));
		}

		[DataTestMethod]
		[DataRow("x")]
		[DataRow("7")]
		public void UpdateServiceItem_UnknownItem_Returns404(string itemId)
		{
			var service = AddService();
			AddItem(service, 0, "A", ItemType.Service, null);

			Assert.AreEqual(404, StatusCode(Controller.UpdateServiceItem(service.ServiceID, itemId, new JObject())));
		}

		// ---- DELETE /service/{id}/item/{itemId} ----
		[TestMethod]
		public void DeleteServiceItem_RemovesItemAndItsRelationships()
		{
			var service = AddService();
			AddItem(service, 0, "A", ItemType.Service, null);
			AddItem(service, 1, "B", ItemType.Workflow, Guid.NewGuid().ToString());
			AddItem(service, 2, "C", ItemType.Service, null);
			service.ServiceItemsRelationships.Add(new ServiceItemRelationship { ParentServiceItem = "0", ChildServiceItem = "1" });
			service.ServiceItemsRelationships.Add(new ServiceItemRelationship { ParentServiceItem = "0", ChildServiceItem = "2" });

			Assert.AreEqual(204, StatusCode(Controller.DeleteServiceItem(service.ServiceID, "1")));

			CollectionAssert.AreEqual(new[] { "A", "C" }, service.ServiceItems.Select(i => i.Label).ToArray());
			Assert.AreEqual("2", service.ServiceItemsRelationships.Single().ChildServiceItem);
			Services.Verify(r => r.Update(service), Times.Once);
		}

		[TestMethod]
		public void DeleteServiceItem_LinkedServiceStillExists_Returns409()
		{
			var linked = AddService("Linked", number: 2);
			var service = AddService();
			AddItem(service, 0, "A", ItemType.Service, linked.Identifier);

			Assert.AreEqual(409, StatusCode(Controller.DeleteServiceItem(service.ServiceID, "0")));
			Assert.AreEqual(1, service.ServiceItems.Count);
		}

		[TestMethod]
		public void DeleteServiceItem_UnknownItem_Returns404()
		{
			var service = AddService();

			Assert.AreEqual(404, StatusCode(Controller.DeleteServiceItem(service.ServiceID, "0")));
		}

		private static ServiceItem AddItem(Service service, long id, string label, ItemType type, string implementationReference)
		{
			var item = new ServiceItem { ServiceItemID = id, Label = label, Type = type, ImplementationReference = implementationReference };
			service.ServiceItems.Add(item);
			return item;
		}
	}
}

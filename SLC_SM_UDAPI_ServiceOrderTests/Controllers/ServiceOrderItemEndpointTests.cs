namespace SLC_SM_UDAPI_ServiceOrder.Tests.Controllers
{
	using System.Collections.Generic;
	using Moq;
	using Newtonsoft.Json.Linq;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using SLC_SM_UDAPI_ServiceOrder.Models;

	using ItemTransition = DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Serviceorderitem_Behavior.TransitionsEnum;

	[TestClass]
	public class ServiceOrderItemEndpointTests : ServiceOrderControllerTestBase
	{
		#region GET /serviceOrder/{id}/item

		// ---- GET /serviceOrder/{id}/item ----
		[TestMethod]
		public void ListServiceOrderItems_ReturnsItemsOrderedByPriority()
		{
			var first = AddItem();
			var second = AddItem();
			var order = AddOrder("New", first, second);
			order.OrderItems[0].Priority = 5;
			order.OrderItems[1].Priority = 1;

			var value = Value<List<ServiceOrderItem>>(Controller.ListServiceOrderItems(OrderId));

			CollectionAssert.AreEqual(new[] { second, first }, value);
		}

		[TestMethod]
		public void ListServiceOrderItems_NoItems_ReturnsEmpty()
		{
			AddOrder();
			Assert.AreEqual(0, Value<List<ServiceOrderItem>>(Controller.ListServiceOrderItems(OrderId)).Count);
		}

		[TestMethod]
		public void ListServiceOrderItems_Unknown_Returns404()
		{
			Assert.AreEqual(404, StatusCode(Controller.ListServiceOrderItems(OrderId)));
		}

		[TestMethod]
		public void ListServiceOrderItems_MissingId_Returns400()
		{
			Assert.AreEqual(400, StatusCode(Controller.ListServiceOrderItems("")));
		}

		#endregion GET /serviceOrder/{id}/item

		#region GET /serviceOrder/{id}/item/{itemId}

		// ---- GET /serviceOrder/{id}/item/{itemId} ----
		[TestMethod]
		public void GetServiceOrderItem_Existing_Returns200WithPriority()
		{
			var item = AddItem();
			AddOrder("New", item);

			var value = Value<JObject>(Controller.GetServiceOrderItem(OrderId, item.Identifier));

			Assert.AreEqual(0, (long)value["priority"]);
		}

		[TestMethod]
		public void GetServiceOrderItem_InvalidItemId_Returns400()
		{
			Assert.AreEqual(400, StatusCode(Controller.GetServiceOrderItem(OrderId, "abc")));
		}

		[TestMethod]
		public void GetServiceOrderItem_ItemNotInOrder_Returns404()
		{
			AddItem();
			AddOrder();
			Assert.AreEqual(404, StatusCode(Controller.GetServiceOrderItem(OrderId, Guid.NewGuid().ToString())));
		}

		[TestMethod]
		public void GetServiceOrderItem_UnknownOrder_Returns404()
		{
			Assert.AreEqual(404, StatusCode(Controller.GetServiceOrderItem(OrderId, Guid.NewGuid().ToString())));
		}

		#endregion GET /serviceOrder/{id}/item/{itemId}

		#region POST /serviceOrder/{id}/item

		// ---- POST /serviceOrder/{id}/item ----
		[TestMethod]
		public void AddServiceOrderItem_Valid_CreatesItemLinksOrderAndReturns201()
		{
			var order = AddOrder();
			var request = new CreateServiceOrderItemRequest { Action = "add", ServiceSpecificationId = Guid.NewGuid(), Priority = 3 };

			var result = Controller.AddServiceOrderItem(OrderId, request);

			Assert.AreEqual(201, StatusCode(result));
			Items.Verify(r => r.Create(It.Is<ServiceOrderItem>(i => i.Action == "add")), Times.Once);
			Orders.Verify(r => r.Update(order), Times.Once);
			Assert.AreEqual(1, order.OrderItems.Count);
			Assert.AreEqual(3L, order.OrderItems[0].Priority);
		}

		[DataTestMethod]
		[DataRow(null, false, false)]
		[DataRow("invalid", false, false)]
		[DataRow("add", false, false)]
		[DataRow("modify", true, false)]
		[DataRow("delete", true, false)]
		public void AddServiceOrderItem_InvalidRequest_Returns400(string action, bool withSpec, bool withService)
		{
			AddOrder();
			var request = new CreateServiceOrderItemRequest
			{
				Action = action,
				ServiceSpecificationId = withSpec ? Guid.NewGuid() : default(Guid?),
				ServiceId = withService ? Guid.NewGuid() : default(Guid?),
			};

			Assert.AreEqual(400, StatusCode(Controller.AddServiceOrderItem(OrderId, request)));
			Items.Verify(r => r.Create(It.IsAny<ServiceOrderItem>()), Times.Never);
		}

		[TestMethod]
		public void AddServiceOrderItem_EndBeforeStart_Returns400()
		{
			AddOrder();
			var request = new CreateServiceOrderItemRequest { Action = "add", ServiceSpecificationId = Guid.NewGuid(), StartTime = DateTime.UtcNow, EndTime = DateTime.UtcNow.AddHours(-1) };
			Assert.AreEqual(400, StatusCode(Controller.AddServiceOrderItem(OrderId, request)));
		}

		[TestMethod]
		public void AddServiceOrderItem_Unknown_Returns404()
		{
			Assert.AreEqual(404, StatusCode(Controller.AddServiceOrderItem(OrderId, new CreateServiceOrderItemRequest { Action = "noChange" })));
		}

		[TestMethod]
		public void AddServiceOrderItem_OrderInProgress_Returns409()
		{
			AddOrder("InProgress");
			Assert.AreEqual(409, StatusCode(Controller.AddServiceOrderItem(OrderId, new CreateServiceOrderItemRequest { Action = "noChange" })));
		}

		#endregion POST /serviceOrder/{id}/item

		#region PATCH /serviceOrder/{id}/item/{itemId}

		// ---- PATCH /serviceOrder/{id}/item/{itemId} ----
		[TestMethod]
		public void UpdateServiceOrderItem_Valid_UpdatesItemAndPriority()
		{
			var item = AddItem();
			var order = AddOrder("New", item);

			var result = Controller.UpdateServiceOrderItem(OrderId, item.Identifier, new JObject { ["name"] = "Renamed", ["priority"] = 7 });

			Assert.AreEqual(200, StatusCode(result));
			Items.Verify(r => r.Update(It.Is<ServiceOrderItem>(i => i.Name == "Renamed")), Times.Once);
			Orders.Verify(r => r.Update(order), Times.Once);
			Assert.AreEqual(7L, (long)Value<JObject>(result)["priority"]);
		}

		[TestMethod]
		public void UpdateServiceOrderItem_WithoutPriority_DoesNotUpdateOrder()
		{
			var item = AddItem();
			AddOrder("New", item);

			Controller.UpdateServiceOrderItem(OrderId, item.Identifier, new JObject { ["description"] = "d" });

			Orders.Verify(r => r.Update(It.IsAny<ServiceOrder>()), Times.Never);
		}

		[TestMethod]
		public void UpdateServiceOrderItem_MergedResultInvalid_Returns400()
		{
			var item = AddItem();
			AddOrder("New", item);

			Assert.AreEqual(400, StatusCode(Controller.UpdateServiceOrderItem(OrderId, item.Identifier, new JObject { ["action"] = "modify" })));
			Items.Verify(r => r.Update(It.IsAny<ServiceOrderItem>()), Times.Never);
		}

		[TestMethod]
		public void UpdateServiceOrderItem_InvalidItemId_Returns400()
		{
			Assert.AreEqual(400, StatusCode(Controller.UpdateServiceOrderItem(OrderId, "x", new JObject())));
		}

		[TestMethod]
		public void UpdateServiceOrderItem_NullBody_Returns400()
		{
			Assert.AreEqual(400, StatusCode(Controller.UpdateServiceOrderItem(OrderId, Guid.NewGuid().ToString(), null)));
		}

		[TestMethod]
		public void UpdateServiceOrderItem_ItemNotInOrder_Returns404()
		{
			AddOrder();
			Assert.AreEqual(404, StatusCode(Controller.UpdateServiceOrderItem(OrderId, Guid.NewGuid().ToString(), new JObject())));
		}

		[TestMethod]
		public void UpdateServiceOrderItem_ItemNotNew_Returns409()
		{
			var item = AddItem("Completed");
			AddOrder("New", item);
			Assert.AreEqual(409, StatusCode(Controller.UpdateServiceOrderItem(OrderId, item.Identifier, new JObject { ["name"] = "x" })));
		}

		#endregion PATCH /serviceOrder/{id}/item/{itemId}

		#region DELETE /serviceOrder/{id}/item/{itemId}

		// ---- DELETE /serviceOrder/{id}/item/{itemId} ----
		[TestMethod]
		public void DeleteServiceOrderItem_New_UnlinksThenDeletes()
		{
			var item = AddItem();
			var order = AddOrder("New", item);
			var sequence = new MockSequence();
			Orders.InSequence(sequence).Setup(r => r.Update(order)).Returns(order);
			Items.InSequence(sequence).Setup(r => r.Delete(item));

			Assert.AreEqual(204, StatusCode(Controller.DeleteServiceOrderItem(OrderId, item.Identifier)));
			Assert.AreEqual(0, order.OrderItems.Count);
			Items.Verify(r => r.Delete(item), Times.Once);
		}

		[TestMethod]
		public void DeleteServiceOrderItem_InvalidItemId_Returns400()
		{
			Assert.AreEqual(400, StatusCode(Controller.DeleteServiceOrderItem(OrderId, "x")));
		}

		[TestMethod]
		public void DeleteServiceOrderItem_UnknownOrder_Returns404()
		{
			Assert.AreEqual(404, StatusCode(Controller.DeleteServiceOrderItem(OrderId, Guid.NewGuid().ToString())));
		}

		[TestMethod]
		public void DeleteServiceOrderItem_ItemNotNew_Returns409()
		{
			var item = AddItem("Completed");
			AddOrder("New", item);
			Assert.AreEqual(409, StatusCode(Controller.DeleteServiceOrderItem(OrderId, item.Identifier)));
			Items.Verify(r => r.Delete(It.IsAny<ServiceOrderItem>()), Times.Never);
		}

		#endregion DELETE /serviceOrder/{id}/item/{itemId}

		#region POST /serviceOrder/{id}/item/{itemId}/transition

		// ---- POST /serviceOrder/{id}/item/{itemId}/transition ----
		[TestMethod]
		public void TransitionServiceOrderItem_AllowedTransition_Returns200()
		{
			var parts = FirstTransition(typeof(ItemTransition));
			var item = AddItem(parts[0]);
			AddOrder("New", item);
			var expected = (ItemTransition)Enum.Parse(typeof(ItemTransition), parts[0] + "_To_" + parts[1]);

			var result = Controller.TransitionServiceOrderItem(OrderId, item.Identifier, new TransitionServiceOrderRequest { TargetStatus = parts[1] });

			Assert.AreEqual(200, StatusCode(result));
			Items.Verify(r => r.TransitionStatus(item, expected), Times.Once);
		}

		[TestMethod]
		public void TransitionServiceOrderItem_MissingTargetStatus_Returns400()
		{
			Assert.AreEqual(400, StatusCode(Controller.TransitionServiceOrderItem(OrderId, Guid.NewGuid().ToString(), new TransitionServiceOrderRequest())));
		}

		[TestMethod]
		public void TransitionServiceOrderItem_InvalidItemId_Returns400()
		{
			Assert.AreEqual(400, StatusCode(Controller.TransitionServiceOrderItem(OrderId, "x", new TransitionServiceOrderRequest { TargetStatus = "new" })));
		}

		[TestMethod]
		public void TransitionServiceOrderItem_ItemNotInOrder_Returns404()
		{
			AddOrder();
			Assert.AreEqual(404, StatusCode(Controller.TransitionServiceOrderItem(OrderId, Guid.NewGuid().ToString(), new TransitionServiceOrderRequest { TargetStatus = "acknowledged" })));
		}

		[TestMethod]
		public void TransitionServiceOrderItem_DisallowedTransition_Returns409()
		{
			var item = AddItem("New");
			AddOrder("New", item);
			Assert.AreEqual(409, StatusCode(Controller.TransitionServiceOrderItem(OrderId, item.Identifier, new TransitionServiceOrderRequest { TargetStatus = "new" })));
		}

		#endregion POST /serviceOrder/{id}/item/{itemId}/transition
	}
}
namespace SLC_SM_UDAPI_ServiceOrder.Tests.Controllers
{
	using System.Collections.Generic;
	using Moq;
	using Newtonsoft.Json.Linq;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using SLC_SM_UDAPI_ServiceOrder.Models;

	using OrderTransition = DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Serviceorder_Behavior.TransitionsEnum;

	[TestClass]
	public class ServiceOrderEndpointTests : ServiceOrderControllerTestBase
	{
		// ---- GET /serviceOrder ----
		[TestMethod]
		public void ListServiceOrders_NoFilters_ReturnsAllOrders()
		{
			AddOrder();
			var result = Controller.ListServiceOrders();
			Assert.AreEqual(200, StatusCode(result));
		}

		[TestMethod]
		public void ListServiceOrders_InvalidOrganizationId_Returns400()
		{
			Assert.AreEqual(400, StatusCode(Controller.ListServiceOrders(organizationId: "not-a-guid")));
		}

		[TestMethod]
		public void ListServiceOrders_RepositoryThrows_Returns500()
		{
			Orders.Setup(r => r.Read(It.IsAny<FilterElement<ServiceOrder>>())).Throws(new InvalidOperationException());
			Assert.AreEqual(500, StatusCode(Controller.ListServiceOrders()));
		}

		// ---- GET /serviceOrder/{id} ----
		[TestMethod]
		public void GetServiceOrder_Existing_Returns200WithOrder()
		{
			var order = AddOrder();
			var result = Controller.GetServiceOrder(OrderId);
			Assert.AreEqual(200, StatusCode(result));
			Assert.AreSame(order, Value<ServiceOrder>(result));
		}

		[TestMethod]
		public void GetServiceOrder_WithFields_ReturnsOnlyRequestedFields()
		{
			AddOrder();
			var value = Value<JObject>(Controller.GetServiceOrder(OrderId, "name"));
			Assert.AreEqual(1, value.Count);
			Assert.AreEqual("Order", (string)value["Name"]);
		}

		[DataTestMethod]
		[DataRow(null)]
		[DataRow(" ")]
		public void GetServiceOrder_MissingId_Returns400(string id)
		{
			Assert.AreEqual(400, StatusCode(Controller.GetServiceOrder(id)));
		}

		[TestMethod]
		public void GetServiceOrder_Unknown_Returns404()
		{
			Assert.AreEqual(404, StatusCode(Controller.GetServiceOrder(OrderId)));
		}

		// ---- POST /serviceOrder ----
		[TestMethod]
		public void CreateServiceOrder_Valid_CreatesItemsAndOrderAndReturns201()
		{
			var request = new CreateServiceOrderRequest
			{
				Name = " New order ",
				Priority = "high",
				OrderItems = new List<CreateServiceOrderItemRequest> { new CreateServiceOrderItemRequest { Action = "add", ServiceSpecificationId = Guid.NewGuid() } },
			};

			var result = Controller.CreateServiceOrder(request);

			Assert.AreEqual(201, StatusCode(result));
			Items.Verify(r => r.Create(It.IsAny<ServiceOrderItem>()), Times.Once);
			Orders.Verify(r => r.Create(It.Is<IEnumerable<ServiceOrder>>(o => o.Single().Name == "New order" && o.Single().OrderId == "ORDER-000001" && o.Single().OrderItems.Count == 1)), Times.Once);
		}

		[TestMethod]
		public void CreateServiceOrder_GeneratesNextOrderId()
		{
			AddOrder().OrderId = "ORDER-000041";
			var request = new CreateServiceOrderRequest { Name = "x", OrderItems = new List<CreateServiceOrderItemRequest> { new CreateServiceOrderItemRequest() } };

			Controller.CreateServiceOrder(request);

			Orders.Verify(r => r.Create(It.Is<IEnumerable<ServiceOrder>>(o => o.Single().OrderId == "ORDER-000042")), Times.Once);
		}

		[TestMethod]
		public void CreateServiceOrder_NullBody_Returns400()
		{
			Assert.AreEqual(400, StatusCode(Controller.CreateServiceOrder(null)));
		}

		[TestMethod]
		public void CreateServiceOrder_MissingName_Returns400()
		{
			var request = new CreateServiceOrderRequest { OrderItems = new List<CreateServiceOrderItemRequest> { new CreateServiceOrderItemRequest() } };
			Assert.AreEqual(400, StatusCode(Controller.CreateServiceOrder(request)));
			Orders.Verify(r => r.Create(It.IsAny<IEnumerable<ServiceOrder>>()), Times.Never);
		}

		[TestMethod]
		public void CreateServiceOrder_NoItems_Returns400()
		{
			var request = new CreateServiceOrderRequest { Name = "x", OrderItems = new List<CreateServiceOrderItemRequest>() };
			Assert.AreEqual(400, StatusCode(Controller.CreateServiceOrder(request)));
		}

		[TestMethod]
		public void CreateServiceOrder_InvalidPriority_Returns400()
		{
			var request = new CreateServiceOrderRequest { Name = "x", Priority = "urgent", OrderItems = new List<CreateServiceOrderItemRequest> { new CreateServiceOrderItemRequest() } };
			Assert.AreEqual(400, StatusCode(Controller.CreateServiceOrder(request)));
		}

		// ---- PATCH /serviceOrder/{id} ----
		[TestMethod]
		public void UpdateServiceOrder_Valid_UpdatesOnlyProvidedFields()
		{
			var order = AddOrder();
			order.Description = "keep";

			var result = Controller.UpdateServiceOrder(OrderId, new JObject { ["name"] = "Renamed" });

			Assert.AreEqual(200, StatusCode(result));
			Orders.Verify(r => r.Update(It.Is<ServiceOrder>(o => o.Name == "Renamed" && o.Description == "keep")), Times.Once);
		}

		[TestMethod]
		public void UpdateServiceOrder_ReplacesOrderItems()
		{
			AddOrder("New", AddItem());
			var patch = JObject.FromObject(new { orderItems = new[] { new { action = "add" }, new { action = "add" } } });

			Assert.AreEqual(200, StatusCode(Controller.UpdateServiceOrder(OrderId, patch)));
			Items.Verify(r => r.Create(It.IsAny<ServiceOrderItem>()), Times.Exactly(2));
			Orders.Verify(r => r.Update(It.Is<ServiceOrder>(o => o.OrderItems.Count == 2)), Times.Once);
		}

		[TestMethod]
		public void UpdateServiceOrder_NullBody_Returns400()
		{
			Assert.AreEqual(400, StatusCode(Controller.UpdateServiceOrder(OrderId, null)));
		}

		[TestMethod]
		public void UpdateServiceOrder_EmptyName_Returns400()
		{
			AddOrder();
			Assert.AreEqual(400, StatusCode(Controller.UpdateServiceOrder(OrderId, new JObject { ["name"] = " " })));
			Orders.Verify(r => r.Update(It.IsAny<ServiceOrder>()), Times.Never);
		}

		[TestMethod]
		public void UpdateServiceOrder_EmptyOrderItems_Returns400()
		{
			AddOrder();
			Assert.AreEqual(400, StatusCode(Controller.UpdateServiceOrder(OrderId, new JObject { ["orderItems"] = new JArray() })));
		}

		[TestMethod]
		public void UpdateServiceOrder_Unknown_Returns404()
		{
			Assert.AreEqual(404, StatusCode(Controller.UpdateServiceOrder(OrderId, new JObject())));
		}

		[TestMethod]
		public void UpdateServiceOrder_NonUpdatableStatus_Returns409()
		{
			AddOrder("Completed");
			Assert.AreEqual(409, StatusCode(Controller.UpdateServiceOrder(OrderId, new JObject { ["name"] = "x" })));
			Orders.Verify(r => r.Update(It.IsAny<ServiceOrder>()), Times.Never);
		}

		// ---- DELETE /serviceOrder/{id} ----
		[TestMethod]
		public void DeleteServiceOrder_New_DeletesAndReturns204()
		{
			var order = AddOrder();
			Assert.AreEqual(204, StatusCode(Controller.DeleteServiceOrder(OrderId)));
			Orders.Verify(r => r.Delete(order), Times.Once);
		}

		[TestMethod]
		public void DeleteServiceOrder_MissingId_Returns400()
		{
			Assert.AreEqual(400, StatusCode(Controller.DeleteServiceOrder(" ")));
		}

		[TestMethod]
		public void DeleteServiceOrder_Unknown_Returns404()
		{
			Assert.AreEqual(404, StatusCode(Controller.DeleteServiceOrder(OrderId)));
		}

		[TestMethod]
		public void DeleteServiceOrder_NotNew_Returns409()
		{
			AddOrder("Acknowledged");
			Assert.AreEqual(409, StatusCode(Controller.DeleteServiceOrder(OrderId)));
			Orders.Verify(r => r.Delete(It.IsAny<ServiceOrder>()), Times.Never);
		}

		// ---- GET /serviceOrder/{id}/transition ----
		[TestMethod]
		public void GetServiceOrderTransitions_ReturnsCurrentAndAllowedStatuses()
		{
			var parts = FirstTransition(typeof(OrderTransition));
			AddOrder(parts[0]);

			var value = Value<JObject>(Controller.GetServiceOrderTransitions(OrderId));

			Assert.IsNotNull(value["currentStatus"]);
			Assert.IsTrue(value["allowedStatuses"].Any());
		}

		[TestMethod]
		public void GetServiceOrderTransitions_Unknown_Returns404()
		{
			Assert.AreEqual(404, StatusCode(Controller.GetServiceOrderTransitions(OrderId)));
		}

		[TestMethod]
		public void GetServiceOrderTransitions_MissingId_Returns400()
		{
			Assert.AreEqual(400, StatusCode(Controller.GetServiceOrderTransitions(null)));
		}

		// ---- POST /serviceOrder/{id}/transition ----
		[TestMethod]
		public void TransitionServiceOrder_AllowedTransition_CallsRepositoryAndReturns200()
		{
			var parts = FirstTransition(typeof(OrderTransition));
			AddOrder(parts[0]);
			var expected = (OrderTransition)Enum.Parse(typeof(OrderTransition), parts[0] + "_To_" + parts[1]);

			var result = Controller.TransitionServiceOrder(OrderId, new TransitionServiceOrderRequest { TargetStatus = parts[1], CancellationInfo = new CancellationInfoRequest { Reason = "r" } });

			Assert.AreEqual(200, StatusCode(result));
			Orders.Verify(r => r.TransitionStatus(It.IsAny<ServiceOrder>(), expected), Times.Once);
		}

		[TestMethod]
		public void TransitionServiceOrder_MissingTargetStatus_Returns400()
		{
			Assert.AreEqual(400, StatusCode(Controller.TransitionServiceOrder(OrderId, new TransitionServiceOrderRequest())));
		}

		[TestMethod]
		public void TransitionServiceOrder_InvalidTargetStatus_Returns400()
		{
			Assert.AreEqual(400, StatusCode(Controller.TransitionServiceOrder(OrderId, new TransitionServiceOrderRequest { TargetStatus = "bogus" })));
		}

		[TestMethod]
		public void TransitionServiceOrder_AssessCancellationWithoutReason_Returns400()
		{
			AddOrder();
			Assert.AreEqual(400, StatusCode(Controller.TransitionServiceOrder(OrderId, new TransitionServiceOrderRequest { TargetStatus = "assessCancellation" })));
		}

		[TestMethod]
		public void TransitionServiceOrder_Unknown_Returns404()
		{
			Assert.AreEqual(404, StatusCode(Controller.TransitionServiceOrder(OrderId, new TransitionServiceOrderRequest { TargetStatus = "acknowledged" })));
		}

		[TestMethod]
		public void TransitionServiceOrder_DisallowedTransition_Returns409()
		{
			AddOrder("New");
			Assert.AreEqual(409, StatusCode(Controller.TransitionServiceOrder(OrderId, new TransitionServiceOrderRequest { TargetStatus = "new" })));
			Orders.Verify(r => r.TransitionStatus(It.IsAny<ServiceOrder>(), It.IsAny<OrderTransition>()), Times.Never);
		}
	}
}

namespace SLC_SM_UDAPI_ServiceManagement.Tests.Controllers
{
	using System.Collections.Generic;
	using DomHelpers.SlcPeople_Organizations;
	using Microsoft.Extensions.Logging;
	using Moq;
	using Newtonsoft.Json.Linq;
	using Skyline.DataMiner.Net;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.ManagerStore;
	using Skyline.DataMiner.Net.Messages;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using Skyline.DataMiner.Utils.UserDefinedApiToolkit;
	using SLC_SM_UDAPI_ServiceManagement.Controllers;
	using SLC_SM_UDAPI_ServiceManagement.Models;
	using SLC_SM_UDAPI_ServiceManagement.Models.Order;
	using SLDataGateway.API.Types.Querying;

	using IEngine = Skyline.DataMiner.Automation.IEngine;
	using Cfg = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;
	using OrderTransition = DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Serviceorder_Behavior.TransitionsEnum;
	using ItemTransition = DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Serviceorderitem_Behavior.TransitionsEnum;
	using ServiceStatus = DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Service_Behavior.StatusesEnum;
	using ServiceTransition = DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Service_Behavior.TransitionsEnum;

	[TestClass]
	public class ServiceOrderEndpointTests : ServiceOrderControllerTestBase
	{
		// ---- GET /serviceOrder ----
		[TestMethod]
		public void ListServiceOrders_NoFilters_ReturnsAllOrders()
		{
			var order = AddOrder("InProgress");
			var value = Value<List<JObject>>(Controller.ListServiceOrders());
			Assert.AreEqual(1, value.Count);
			Assert.AreEqual(order.Identifier, (string)value[0]["Identifier"]);
			Assert.AreEqual("inProgress", (string)value[0]["StatusName"]);
		}

		[TestMethod]
		public void ListServiceOrders_OrganizationUnreadable_ReturnsOrdersWithoutName()
		{
			var order = AddOrder();
			order.OrganizationId = Guid.NewGuid();
			var value = Value<List<JObject>>(Controller.ListServiceOrders());
			Assert.IsNull((string)value[0]["OrganizationName"]);
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
			Orders.Setup(r => r.Read(It.IsAny<IQuery<ServiceOrder>>())).Throws(new InvalidOperationException());
			Orders.Setup(r => r.ReadPaged(It.IsAny<IQuery<ServiceOrder>>(), It.IsAny<int>())).Throws(new InvalidOperationException());
			Assert.AreEqual(500, StatusCode(Controller.ListServiceOrders()));
		}

		[TestMethod]
		public void ListServiceOrdersCursor_StatusFilter_ReturnsMatchingOrdersAndFilteredCount()
		{
			AddOrder("New");
			var held = AddOrder("Held");
			var failed = AddOrder("Failed");

			var page = Value<CursorPageResponse<JObject>>(Controller.ListServiceOrders(pagination: "cursor", status: "held,failed"));

			CollectionAssert.AreEquivalent(new[] { held.Identifier, failed.Identifier }, page.Items.Select(i => (string)i["Identifier"]).ToList());
			Assert.AreEqual(2L, page.TotalCount);
		}

		[TestMethod]
		public void ListServiceOrdersCursor_Search_MatchesDescriptionAndPriority()
		{
			var described = AddOrder();
			described.Description = "Urgent uplink";
			var high = AddOrder();
			high.Priority = DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Enums.ServiceOrderPriorityEnum.High;
			AddOrder();

			var byDescription = Value<CursorPageResponse<JObject>>(Controller.ListServiceOrders(pagination: "cursor", search: "urgent"));
			var byPriority = Value<CursorPageResponse<JObject>>(Controller.ListServiceOrders(pagination: "cursor", search: "high"));

			Assert.AreEqual(described.Identifier, (string)byDescription.Items.Single()["Identifier"]);
			Assert.AreEqual(high.Identifier, (string)byPriority.Items.Single()["Identifier"]);
		}

		[TestMethod]
		public void ListServiceOrdersCursor_SearchMatchingManyOrganizations_ReturnsOrdersOfEveryMatchingOrganization()
		{
			var organizations = Enumerable.Range(1, 150).Select(i => new OrganizationsInstance()).ToList();
			for (int i = 0; i < organizations.Count; i++)
			{
				organizations[i].OrganizationInformation.OrganizationName = $"Acme {i + 1}";
				organizations[i].OrganizationInformation.Category = Guid.NewGuid();
			}

			var first = AddOrder();
			first.OrganizationId = organizations.First().ID.Id;
			var last = AddOrder();
			last.OrganizationId = organizations.Last().ID.Id;
			AddOrder();

			var connection = new Mock<IConnection>();
			connection.Setup(c => c.HandleMessages(It.IsAny<DMSMessage[]>()))
				.Returns<DMSMessage[]>(messages => messages.Select(m => RespondWith(m, organizations.Select(o => o.ToInstance()).ToList())).ToArray());
			var controller = new ServiceOrderController(
				Mock.Of<ILogger<ServiceOrderController>>(),
				Helper,
				Mock.Of<IAccessor<IEngine>>(a => a.Value == Mock.Of<IEngine>(e => e.GetUserConnection() == connection.Object)));
			AttachContext(controller);

			var page = Value<CursorPageResponse<JObject>>(controller.ListServiceOrders(pagination: "cursor", search: "acme", includeTotalCount: true));

			CollectionAssert.AreEquivalent(new[] { first.Identifier, last.Identifier }, page.Items.Select(i => (string)i["Identifier"]).ToList());
			Assert.AreEqual(2L, page.TotalCount);
		}

		[TestMethod]
		public void ListServiceOrdersCursor_InvalidStatusOrSort_Returns400()
		{
			Assert.AreEqual(400, StatusCode(Controller.ListServiceOrders(pagination: "cursor", status: "unknown")));
			Assert.AreEqual(400, StatusCode(Controller.ListServiceOrders(pagination: "cursor", sort: "organization")));
		}

		// ---- GET /serviceOrder/{id} ----
		[TestMethod]
		public void GetServiceOrder_Existing_Returns200WithOrder()
		{
			var order = AddOrder();
			var result = Controller.GetServiceOrder(OrderId);
			Assert.AreEqual(200, StatusCode(result));
			var value = Value<JObject>(result);
			Assert.AreEqual(order.Identifier, (string)value["Identifier"]);
			Assert.AreEqual("new", (string)value["StatusName"]);
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
				OrderItems = new List<CreateServiceOrderItemRequest> { new CreateServiceOrderItemRequest { Action = "add", ServiceSpecificationId = AddSpecification() } },
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
		public void CreateServiceOrder_MissingName_UsesGeneratedOrderIdAsName()
		{
			AddOrder().OrderId = "ORDER-000041";
			var request = new CreateServiceOrderRequest { Name = " ", OrderItems = new List<CreateServiceOrderItemRequest>() };

			Assert.AreEqual(201, StatusCode(Controller.CreateServiceOrder(request)));
			Orders.Verify(r => r.Create(It.Is<IEnumerable<ServiceOrder>>(o => o.Single().OrderId == "ORDER-000042" && o.Single().Name == "ORDER-000042")), Times.Once);
		}

		[TestMethod]
		public void CreateServiceOrder_NoItems_CreatesEmptyOrder()
		{
			var request = new CreateServiceOrderRequest { Name = "x", OrderItems = new List<CreateServiceOrderItemRequest>() };

			Assert.AreEqual(201, StatusCode(Controller.CreateServiceOrder(request)));
			Orders.Verify(r => r.Create(It.Is<IEnumerable<ServiceOrder>>(orders => orders.Single().OrderItems.Count == 0)), Times.Once);
		}

		[TestMethod]
		public void CreateServiceOrder_InvalidPriority_Returns400()
		{
			var request = new CreateServiceOrderRequest { Name = "x", Priority = "urgent", OrderItems = new List<CreateServiceOrderItemRequest> { new CreateServiceOrderItemRequest() } };
			Assert.AreEqual(400, StatusCode(Controller.CreateServiceOrder(request)));
		}

		[TestMethod]
		public void CreateServiceOrder_MandatoryParameterWithoutValue_ErrorShowsParameterName()
		{
			var parameter = new Cfg.ConfigurationParameter { Identifier = Guid.NewGuid().ToString(), Name = "Bitrate" };
			SetupMandatorySpecificationRepositories(new List<Cfg.ConfigurationParameterValue>(), parameter.Identifier, out var specificationId);
			Mock.Get(Helper.ServiceCatalog).SetupGet(api => api.ConfigurationParameters)
				.Returns(InitializationRepository(new List<Cfg.ConfigurationParameter> { parameter }).Object);
			var request = new CreateServiceOrderRequest
			{
				Name = "x",
				OrderItems = new List<CreateServiceOrderItemRequest> { new CreateServiceOrderItemRequest { Action = "add", ServiceSpecificationId = specificationId } },
			};

			var result = Controller.CreateServiceOrder(request);

			Assert.AreEqual(400, StatusCode(result));
			StringAssert.EndsWith(ErrorDetail(result), "without a value: Bitrate.");
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
		[DataTestMethod]
		[DataRow("New")]
		[DataRow("Cancelled")]
		[DataRow("Completed")]
		[DataRow("Rejected")]
		[DataRow("Failed")]
		[DataRow("PartiallyFailed")]
		public void DeleteServiceOrder_DeletableStatus_DeletesAndReturns204(string status)
		{
			var order = AddOrder(status);
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

		[DataTestMethod]
		[DataRow("Acknowledged")]
		[DataRow("InProgress")]
		[DataRow("Pending")]
		[DataRow("Held")]
		[DataRow("AssessCancellation")]
		[DataRow("PendingCancellation")]
		public void DeleteServiceOrder_NonDeletableStatus_Returns409(string status)
		{
			AddOrder(status);
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

		[TestMethod]
		public void TransitionServiceOrder_Acknowledge_AcknowledgesNewItems()
		{
			var newItem = AddItem("New");
			var acknowledgedItem = AddItem("Acknowledged");
			AddOrder("New", newItem, acknowledgedItem);

			Assert.AreEqual(200, StatusCode(Controller.TransitionServiceOrder(OrderId, new TransitionServiceOrderRequest { TargetStatus = "acknowledged" })));

			Items.Verify(r => r.TransitionStatus(newItem, ItemTransition.New_To_Acknowledged), Times.Once);
			Items.Verify(r => r.TransitionStatus(acknowledgedItem, It.IsAny<ItemTransition>()), Times.Never);
			Orders.Verify(r => r.TransitionStatus(It.IsAny<ServiceOrder>(), OrderTransition.New_To_Acknowledged), Times.Once);
		}

		[TestMethod]
		public void TransitionServiceOrder_Reject_RejectsItemsAndRetiresTerminatedService()
		{
			var service = new Service { Identifier = Guid.NewGuid().ToString(), Name = "S" };
			SetStatus(service, ServiceStatus.Terminated);
			StoredServices.Add(service);
			var linkedItem = AddItem("Acknowledged");
			linkedItem.ServiceInfo.ServiceId = new Skyline.DataMiner.SDM.SdmObjectReference<Service>(service.Identifier);
			var newItem = AddItem("New");
			AddOrder("Acknowledged", linkedItem, newItem);

			var result = Controller.TransitionServiceOrder(OrderId, new TransitionServiceOrderRequest { TargetStatus = "rejected", CancellationInfo = new CancellationInfoRequest { Reason = "r" } });

			Assert.AreEqual(200, StatusCode(result));
			Items.Verify(r => r.TransitionStatus(linkedItem, ItemTransition.Acknowledged_To_Rejected), Times.Once);
			Items.Verify(r => r.TransitionStatus(newItem, ItemTransition.New_To_Rejected), Times.Once);
			Services.Verify(r => r.TransitionStatus(service, ServiceTransition.Terminated_To_Retired), Times.Once);
			Orders.Verify(r => r.TransitionStatus(It.IsAny<ServiceOrder>(), OrderTransition.Acknowledged_To_Rejected), Times.Once);
		}

		[TestMethod]
		public void TransitionServiceOrder_Start_StartsItemsAndInitializesServices()
		{
			var item = AddItem("Acknowledged");
			item.ServiceInfo.SpecificationId = new Skyline.DataMiner.SDM.SdmObjectReference<ServiceSpecification>(AddSpecification().ToString());
			AddOrder("Acknowledged", item);

			Assert.AreEqual(200, StatusCode(Controller.TransitionServiceOrder(OrderId, new TransitionServiceOrderRequest { TargetStatus = "inProgress" })));

			Items.Verify(r => r.TransitionStatus(item, ItemTransition.Acknowledged_To_Inprogress), Times.Once);
			Assert.AreEqual(1, StoredServices.Count);
			Assert.AreEqual(StoredServices[0].Identifier, item.ServiceInfo.ServiceId.Identifier);
			Orders.Verify(r => r.TransitionStatus(It.IsAny<ServiceOrder>(), OrderTransition.Acknowledged_To_Inprogress), Times.Once);
		}

		[TestMethod]
		public void TransitionServiceOrder_StartWithEmptyMandatoryValue_Returns409NamingItemAndParameter()
		{
			var item = AddItem("Acknowledged");
			item.ServiceInfo.SpecificationId = new Skyline.DataMiner.SDM.SdmObjectReference<ServiceSpecification>(AddSpecification().ToString());
			AddOrder("Acknowledged", item);
			AddEmptyMandatoryItemConfiguration(item, "Bitrate");

			var result = Controller.TransitionServiceOrder(OrderId, new TransitionServiceOrderRequest { TargetStatus = "inProgress" });

			Assert.AreEqual(409, StatusCode(result));
			StringAssert.Contains(ErrorDetail(result), "(Bitrate)");
			Items.Verify(r => r.TransitionStatus(It.IsAny<ServiceOrderItem>(), It.IsAny<ItemTransition>()), Times.Never);
			Assert.AreEqual(0, StoredServices.Count);
		}

		[TestMethod]
		public void TransitionServiceOrder_StartWithServiceMandatoryEmptyValue_Returns409NamingItemAndParameter()
		{
			var item = AddItem("Acknowledged");
			var specification = AddSpecification();
			item.ServiceInfo.SpecificationId = new Skyline.DataMiner.SDM.SdmObjectReference<ServiceSpecification>(specification.ToString());
			AddOrder("Acknowledged", item);

			var parameter = new Cfg.ConfigurationParameter { Identifier = Guid.NewGuid().ToString(), Name = "Bitrate" };
			var value = new Cfg.ConfigurationParameterValue
			{
				Identifier = Guid.NewGuid().ToString(),
				ConfigurationParameterId = new Skyline.DataMiner.SDM.SdmObjectReference<Cfg.ConfigurationParameter>(parameter.Identifier),
			};
			var specificationConfiguration = new ServiceSpecificationConfigurationValue
			{
				Identifier = Guid.NewGuid().ToString(),
				ConfigurationParameterId = new Skyline.DataMiner.SDM.SdmObjectReference<Cfg.ConfigurationParameter>(parameter.Identifier),
				MandatoryAtService = true,
			};
			var itemConfiguration = new ServiceOrderItemConfigurationValue
			{
				Identifier = Guid.NewGuid().ToString(),
				ConfigurationParameterValueId = new Skyline.DataMiner.SDM.SdmObjectReference<Cfg.ConfigurationParameterValue>(value.Identifier),
				Mandatory = false,
			};
			StoredSpecifications.Single(candidate => candidate.Identifier == specification.ToString()).ConfigurationParameters =
				new List<Skyline.DataMiner.SDM.SdmObjectReference<ServiceSpecificationConfigurationValue>>
				{
					new Skyline.DataMiner.SDM.SdmObjectReference<ServiceSpecificationConfigurationValue>(specificationConfiguration.Identifier),
				};
			item.ServiceInfo.Configurations = new List<Skyline.DataMiner.SDM.SdmObjectReference<ServiceOrderItemConfigurationValue>>
			{
				new Skyline.DataMiner.SDM.SdmObjectReference<ServiceOrderItemConfigurationValue>(itemConfiguration.Identifier),
			};

			Mock.Get(Helper.ServiceCatalog).SetupGet(api => api.ServiceSpecificationConfigurationValues)
				.Returns(InitializationRepository(new List<ServiceSpecificationConfigurationValue> { specificationConfiguration }).Object);
			Mock.Get(Helper.ServiceCatalog).SetupGet(api => api.ConfigurationParameters)
				.Returns(InitializationRepository(new List<Cfg.ConfigurationParameter> { parameter }).Object);
			Mock.Get(Helper.ServiceCatalog).SetupGet(api => api.ConfigurationParameterValues)
				.Returns(InitializationRepository(new List<Cfg.ConfigurationParameterValue> { value }).Object);
			Mock.Get(Helper.ServiceOrder).SetupGet(api => api.ServiceOrderItemConfigurationValues)
				.Returns(InitializationRepository(new List<ServiceOrderItemConfigurationValue> { itemConfiguration }).Object);

			var result = Controller.TransitionServiceOrder(OrderId, new TransitionServiceOrderRequest { TargetStatus = "inProgress" });

			Assert.AreEqual(409, StatusCode(result));
			StringAssert.Contains(ErrorDetail(result), "(Bitrate)");
			Items.Verify(r => r.TransitionStatus(It.IsAny<ServiceOrderItem>(), It.IsAny<ItemTransition>()), Times.Never);
			Assert.AreEqual(0, StoredServices.Count);
		}

		[TestMethod]
		public void TransitionServiceOrder_StartWithServiceMandatoryDefaultNotOnItem_StartsAndCopiesDefault()
		{
			var item = AddItem("Acknowledged");
			var specification = AddSpecification();
			item.ServiceInfo.SpecificationId = new Skyline.DataMiner.SDM.SdmObjectReference<ServiceSpecification>(specification.ToString());
			AddOrder("Acknowledged", item);

			var parameter = new Cfg.ConfigurationParameter { Identifier = Guid.NewGuid().ToString(), Name = "Protocol" };
			var defaultValue = new Cfg.ConfigurationParameterValue
			{
				Identifier = Guid.NewGuid().ToString(),
				ConfigurationParameterId = new Skyline.DataMiner.SDM.SdmObjectReference<Cfg.ConfigurationParameter>(parameter.Identifier),
				StringValue = "SRT",
			};
			var specificationConfiguration = new ServiceSpecificationConfigurationValue
			{
				Identifier = Guid.NewGuid().ToString(),
				ConfigurationParameterId = new Skyline.DataMiner.SDM.SdmObjectReference<Cfg.ConfigurationParameter>(defaultValue.Identifier),
				MandatoryAtService = true,
			};
			StoredSpecifications.Single(candidate => candidate.Identifier == specification.ToString()).ConfigurationParameters =
				new List<Skyline.DataMiner.SDM.SdmObjectReference<ServiceSpecificationConfigurationValue>>
				{
					new Skyline.DataMiner.SDM.SdmObjectReference<ServiceSpecificationConfigurationValue>(specificationConfiguration.Identifier),
				};

			var parameterValues = new List<Cfg.ConfigurationParameterValue> { defaultValue };
			var configurations = new List<ServiceConfigurationValue>();
			Mock.Get(Helper.ServiceCatalog).SetupGet(api => api.ServiceSpecificationConfigurationValues)
				.Returns(InitializationRepository(new List<ServiceSpecificationConfigurationValue> { specificationConfiguration }).Object);
			Mock.Get(Helper.ServiceCatalog).SetupGet(api => api.ConfigurationParameters)
				.Returns(InitializationRepository(new List<Cfg.ConfigurationParameter> { parameter }).Object);
			Mock.Get(Helper.ServiceCatalog).SetupGet(api => api.ConfigurationParameterValues)
				.Returns(InitializationRepository(parameterValues).Object);
			Mock.Get(Helper.ServiceOrder).SetupGet(api => api.ServiceOrderItemConfigurationValues)
				.Returns(InitializationRepository(new List<ServiceOrderItemConfigurationValue>()).Object);
			Mock.Get(Helper.ServiceInventory).SetupGet(api => api.ServiceConfigurationValues).Returns(InitializationRepository(configurations).Object);
			Mock.Get(Helper.ServiceInventory).SetupGet(api => api.ServiceConfigurationVersions)
				.Returns(InitializationRepository(new List<ServiceConfigurationVersion>()).Object);

			var result = Controller.TransitionServiceOrder(OrderId, new TransitionServiceOrderRequest { TargetStatus = "inProgress" });

			Assert.AreEqual(200, StatusCode(result));
			Assert.AreEqual(1, StoredServices.Count);
			var copied = configurations.Single();
			Assert.IsTrue(copied.Mandatory);
			var copiedValue = parameterValues.Single(value => value.Identifier == copied.ConfigurationParameterId.Identifier);
			Assert.AreNotEqual(defaultValue.Identifier, copiedValue.Identifier);
			Assert.AreEqual("SRT", copiedValue.StringValue);
		}

		[TestMethod]
		public void TransitionServiceOrder_StartWithEmptyOptionalRepeatedInstance_Starts()
		{
			var scenario = SetupRepeatedSpecificationScenario(
				"Acknowledged",
				"Acknowledged",
				mainSpecificationHasDefault: true,
				mainItemPresent: true,
				mainItemHasValue: true,
				backupItemHasValue: false);

			var result = Controller.TransitionServiceOrder(OrderId, new TransitionServiceOrderRequest { TargetStatus = "inProgress" });

			Assert.AreEqual(200, StatusCode(result));
			Assert.AreEqual(1, StoredServices.Count);
			Assert.AreEqual(1, scenario.Versions.Count);
		}

		[TestMethod]
		public void TransitionServiceOrder_StartWithMissingMandatoryRepeatedInstanceIsNotMaskedByOptionalSibling()
		{
			var scenario = SetupRepeatedSpecificationScenario(
				"Acknowledged",
				"Acknowledged",
				mainSpecificationHasDefault: false,
				mainItemPresent: false,
				mainItemHasValue: false,
				backupItemHasValue: true);

			var result = Controller.TransitionServiceOrder(OrderId, new TransitionServiceOrderRequest { TargetStatus = "inProgress" });

			Assert.AreEqual(409, StatusCode(result));
			StringAssert.Contains(ErrorDetail(result), "(Transport)");
			Items.Verify(r => r.TransitionStatus(It.IsAny<ServiceOrderItem>(), It.IsAny<ItemTransition>()), Times.Never);
			Assert.AreEqual(0, StoredServices.Count);
		}

		[DataTestMethod]
		[DataRow("New", "Rejected", "acknowledged", "")]
		[DataRow("Acknowledged", "Acknowledged", "rejected", "Active")]
		[DataRow("Acknowledged", "Acknowledged", "inProgress", "")]
		[DataRow("InProgress", "InProgress", "completed", "")]
		[DataRow("PendingCancellation", "Completed", "cancelled", "")]
		public void TransitionServiceOrder_ItemsBlockTransition_Returns409WithoutChanges(string orderStatus, string itemStatus, string target, string linkedServiceStatus)
		{
			var item = AddItem(itemStatus);
			if (!String.IsNullOrEmpty(linkedServiceStatus))
			{
				var service = new Service { Identifier = Guid.NewGuid().ToString(), Name = "S" };
				SetStatus(service, Enum.Parse(typeof(ServiceStatus), linkedServiceStatus));
				StoredServices.Add(service);
				item.ServiceInfo.ServiceId = new Skyline.DataMiner.SDM.SdmObjectReference<Service>(service.Identifier);
			}

			AddOrder(orderStatus, item);

			var result = Controller.TransitionServiceOrder(OrderId, new TransitionServiceOrderRequest { TargetStatus = target, CancellationInfo = new CancellationInfoRequest { Reason = "r" } });

			Assert.AreEqual(409, StatusCode(result));
			Items.Verify(r => r.TransitionStatus(It.IsAny<ServiceOrderItem>(), It.IsAny<ItemTransition>()), Times.Never);
			Orders.Verify(r => r.TransitionStatus(It.IsAny<ServiceOrder>(), It.IsAny<OrderTransition>()), Times.Never);
			Orders.Verify(r => r.Update(It.IsAny<ServiceOrder>()), Times.Never);
		}

		private static DMSMessage RespondWith(DMSMessage request, List<DomInstance> instances)
		{
			switch (request)
			{
				case ManagerStoreStartPagingRequest<DomInstance> _:
					return new ManagerStorePagingResponse<DomInstance>(instances, true, null);
				case ManagerStoreCountRequest<DomInstance> _:
					return new ManagerStoreCountResponse<DomInstance>(instances.Count);
				case ManagerStoreReadRequest<DomInstance> _:
					return new ManagerStoreCrudResponse<DomInstance>(instances);
				default:
					throw new NotSupportedException($"Unexpected request {request.GetType().FullName}.");
			}
		}
	}
}

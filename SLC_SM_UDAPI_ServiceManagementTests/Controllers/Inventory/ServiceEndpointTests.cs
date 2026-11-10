namespace SLC_SM_UDAPI_ServiceManagement.Tests.Controllers
{
	using System.Collections.Generic;
	using Microsoft.Extensions.Logging;
	using Moq;
	using Newtonsoft.Json.Linq;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using Skyline.DataMiner.SDM;
	using SLDataGateway.API.Types.Querying;
	using SLC_SM_UDAPI_ServiceManagement.Models;
	using SLC_SM_UDAPI_ServiceManagement.Models.Inventory;

	using Cfg = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;
	using ServiceStatus = DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Service_Behavior.StatusesEnum;
	using ServiceTransition = DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Service_Behavior.TransitionsEnum;

	[TestClass]
	public class ServiceEndpointTests : ServiceInventoryControllerTestBase
	{
		[TestMethod]
		public void RelatedOrders_WhenUnpagedOrderReadIsIncomplete_FindsOrderThroughPagedRead()
		{
			var service = AddService();
			var item = new ServiceOrderItem
			{
				Identifier = Guid.NewGuid().ToString(),
				Action = "Add",
				ServiceInfo = new ServiceOrderItemServiceInfo { ServiceId = new SdmObjectReference<Service>(service.Identifier) },
			};
			StoredOrderItems.Add(item);
			var order = new ServiceOrder
			{
				Identifier = Guid.NewGuid().ToString(),
				OrderId = "ORDER-1",
				Name = "Related order",
				OrderItems = new List<ServiceOrderEntry> { new ServiceOrderEntry { ServiceOrderItemId = new SdmObjectReference<ServiceOrderItem>(item.Identifier) } },
			};
			var orders = new Mock<IServiceOrderRepository>();
			orders.Setup(repository => repository.Read(It.IsAny<FilterElement<ServiceOrder>>())).Returns(new ServiceOrder[0]);
			orders.Setup(repository => repository.ReadPaged(It.IsAny<IQuery<ServiceOrder>>(), It.IsAny<int>()))
				.Returns<IQuery<ServiceOrder>, int>((query, size) => OnePage(new[] { order }, query));
			Mock.Get(ApiHelper.Object.ServiceOrder).SetupGet(api => api.ServiceOrders).Returns(orders.Object);

			var rows = JArray.FromObject(Value<object>(Controller.ListRelatedServiceOrders(service.Identifier)));

			Assert.AreEqual(1, rows.Count);
			Assert.AreEqual("Related order", (string)rows[0]["OrderName"]);
		}

		[TestMethod]
		public void RelatedOrders_ReturnsCurrentItemStatusInsteadOfOrderStatus()
		{
			var service = AddService();
			var item = new ServiceOrderItem
			{
				Identifier = Guid.NewGuid().ToString(),
				Action = "Add",
				ServiceInfo = new ServiceOrderItemServiceInfo
				{
					ServiceId = new SdmObjectReference<Service>(service.Identifier),
				},
			};
			SetStatus(item, DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Serviceorderitem_Behavior.StatusesEnum.New);
			StoredOrderItems.Add(item);
			var unrelated = new ServiceOrderItem
			{
				Identifier = Guid.NewGuid().ToString(),
				ServiceInfo = new ServiceOrderItemServiceInfo
				{
					ServiceId = new SdmObjectReference<Service>(Guid.NewGuid().ToString()),
				},
			};
			StoredOrderItems.Add(unrelated);
			var order = new ServiceOrder
			{
				Identifier = Guid.NewGuid().ToString(),
				OrderId = "ORDER-1",
				Name = "Related order",
				OrderItems = new List<ServiceOrderEntry>
				{
					new ServiceOrderEntry { ServiceOrderItemId = new SdmObjectReference<ServiceOrderItem>(item.Identifier) },
					new ServiceOrderEntry { ServiceOrderItemId = new SdmObjectReference<ServiceOrderItem>(unrelated.Identifier) },
					new ServiceOrderEntry { ServiceOrderItemId = new SdmObjectReference<ServiceOrderItem>(Guid.NewGuid().ToString()) },
					null,
				},
			};
			var orders = new Mock<IServiceOrderRepository>();
			orders.Setup(repository => repository.Read(It.IsAny<FilterElement<ServiceOrder>>())).Returns(() => new[] { order });
			orders.Setup(repository => repository.ReadPaged(It.IsAny<IQuery<ServiceOrder>>(), It.IsAny<int>()))
				.Returns<IQuery<ServiceOrder>, int>((query, size) => OnePage(new[] { order }, query));
			Mock.Get(ApiHelper.Object.ServiceOrder).SetupGet(api => api.ServiceOrders).Returns(orders.Object);

			SetStatus(order, DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Serviceorder_Behavior.StatusesEnum.InProgress);
			var result = Controller.ListRelatedServiceOrders(service.ServiceID);
			Assert.AreEqual(200, StatusCode(result));
			var rows = JArray.FromObject(Value<object>(result));
			Assert.AreEqual(1, rows.Count);
			Assert.AreEqual("New", (string)rows[0]["State"]);
			Assert.AreEqual(item.Identifier, (string)rows[0]["ServiceOrderItemId"]);
			Assert.AreEqual("Add", (string)rows[0]["Action"]);

			SetStatus(order, DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Serviceorder_Behavior.StatusesEnum.Completed);
			SetStatus(item, DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Serviceorderitem_Behavior.StatusesEnum.InProgress);
			rows = JArray.FromObject(Value<object>(Controller.ListRelatedServiceOrders(service.ServiceID)));
			Assert.AreEqual("InProgress", (string)rows[0]["State"]);

			var other = AddService("Same public ID");
			StoredServices.Remove(other);
			StoredServices.Insert(0, other);
			rows = JArray.FromObject(Value<object>(Controller.ListRelatedServiceOrders(service.Identifier)));
			Assert.AreEqual(1, rows.Count);
			Assert.AreEqual(item.Identifier, (string)rows[0]["ServiceOrderItemId"]);
		}

		// ---- GET /service/{id} ----
		[TestMethod]
		public void GetService_Existing_Returns200WithService()
		{
			var service = AddService();
			var result = Controller.GetService(service.ServiceID);
			Assert.AreEqual(200, StatusCode(result));
			Assert.AreSame(service, Value<Service>(result));
		}

		[TestMethod]
		public void GetService_WithFields_ReturnsOnlyRequestedFields()
		{
			var service = AddService("Svc");
			var value = Value<JObject>(Controller.GetService(service.ServiceID, "name"));
			Assert.AreEqual(1, value.Count);
			Assert.AreEqual("Svc", (string)value["Name"]);
		}

		[TestMethod]
		public void GetService_WithOrganizationFields_ReadsOrganizationForSelectedService()
		{
			var service = AddService("Svc");
			var serviceIdentifier = Guid.Parse(service.Identifier);
			var organizationId = Guid.NewGuid();
			OrganizationNames[organizationId] = "Skyline";
			ServiceOrganizations[serviceIdentifier] = organizationId;

			var value = Value<JObject>(Controller.GetService(service.ServiceID, "OrganizationId,OrganizationName"));

			Assert.AreEqual(2, value.Count);
			Assert.AreEqual(organizationId, (Guid)value["OrganizationId"]);
			Assert.AreEqual("Skyline", (string)value["OrganizationName"]);
			Organizations.Verify(o => o.ReadServiceOrganizationIds(It.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { serviceIdentifier }))), Times.Once);
			Organizations.Verify(o => o.ReadOrganizationNames(), Times.Once);
		}

		[TestMethod]
		public void GetService_ByDomIdentifier_Returns200WithService()
		{
			AddService("Other");
			var service = AddService("Svc");
			var result = Controller.GetService(service.Identifier, "name");
			Assert.AreEqual(200, StatusCode(result));
			Assert.AreEqual("Svc", (string)Value<JObject>(result)["Name"]);
		}

		[DataTestMethod]
		[DataRow(null)]
		[DataRow(" ")]
		[DataRow("not-a-service-id")]
		[DataRow("SERVICE-00099")]
		[DataRow("00000000-0000-0000-0000-000000000000")]
		[DataRow("6f1c2b8e-4a1d-4c5e-9b7a-2d3e4f5a6b7c")]
		public void GetService_MissingOrUnknown_Returns404(string id)
		{
			AddService();
			Assert.AreEqual(404, StatusCode(Controller.GetService(id)));
		}

		// ---- POST /service ----
		[TestMethod]
		public void CreateService_Valid_StoresServiceWithNextIdAndReturns201()
		{
			AddService("Existing", number: 7);

			var result = Controller.CreateService(new CreateServiceRequest { Name = " New ", Description = "d" });

			Assert.AreEqual(201, StatusCode(result));
			var created = StoredServices.Single(s => s.Name == "New");
			Assert.AreEqual("SERVICE-00008", created.ServiceID);
			Assert.AreEqual("d", created.Description);
			Services.Verify(r => r.Create(It.IsAny<Service>()), Times.Once);
		}

		[TestMethod]
		public void CreateService_MissingBody_Returns400()
		{
			Assert.AreEqual(400, StatusCode(Controller.CreateService(null)));
		}

		[DataTestMethod]
		[DataRow(null)]
		[DataRow("  ")]
		public void CreateService_MissingName_Returns400(string name)
		{
			var result = Controller.CreateService(new CreateServiceRequest { Name = name });
			Assert.AreEqual(400, StatusCode(result));
			StringAssert.Contains(ErrorDetail(result), "'name'");
		}

		[TestMethod]
		public void CreateService_EndBeforeStart_Returns400()
		{
			var request = new CreateServiceRequest { Name = "x", StartTime = DateTime.UtcNow, EndTime = DateTime.UtcNow.AddHours(-1) };
			Assert.AreEqual(400, StatusCode(Controller.CreateService(request)));
		}

		[TestMethod]
		public void CreateService_DuplicateName_Returns409()
		{
			AddService("Dup");
			Assert.AreEqual(409, StatusCode(Controller.CreateService(new CreateServiceRequest { Name = "Dup" })));
			Services.Verify(r => r.Create(It.IsAny<Service>()), Times.Never);
		}

		[TestMethod]
		public void CreateService_UnknownSpecification_Returns400()
		{
			var request = new CreateServiceRequest { Name = "x", ServiceSpecificationId = Guid.NewGuid() };
			Assert.AreEqual(400, StatusCode(Controller.CreateService(request)));
		}

		[TestMethod]
		public void CreateService_UnknownCategory_Returns400()
		{
			var request = new CreateServiceRequest { Name = "x", Category = new ServiceRequestCategory { Name = "nope" } };
			Assert.AreEqual(400, StatusCode(Controller.CreateService(request)));
		}

		[TestMethod]
		public void CreateService_CategoryWithoutIdOrName_Returns400()
		{
			var request = new CreateServiceRequest { Name = "x", Category = new ServiceRequestCategory() };
			Assert.AreEqual(400, StatusCode(Controller.CreateService(request)));
		}

		[TestMethod]
		public void CreateService_InvalidServiceItemType_Returns400()
		{
			var request = new CreateServiceRequest
			{
				Name = "x",
				ServiceItems = new List<ServiceRequestItem> { new ServiceRequestItem { Label = "a", Type = "bogus" } },
			};
			Assert.AreEqual(400, StatusCode(Controller.CreateService(request)));
		}

		[TestMethod]
		public void CreateService_RepositoryThrows_Returns500()
		{
			Services.Setup(r => r.Create(It.IsAny<Service>())).Throws(new InvalidOperationException());
			Assert.AreEqual(500, StatusCode(Controller.CreateService(new CreateServiceRequest { Name = "x" })));
		}

		[TestMethod]
		public void CreateService_NextIdCreatedAfterFetch_SkipsToFreeId()
		{
			AddService("Existing", number: 7);
			var fetched = StoredServices.ToList();
			Services.Setup(r => r.ReadPaged(It.IsAny<IQuery<Service>>(), It.IsAny<int>())).Returns<IQuery<Service>, int>((q, size) => OnePage(fetched, q));
			AddService("Created meanwhile", number: 8);

			var result = Controller.CreateService(new CreateServiceRequest { Name = "New" });

			Assert.AreEqual(201, StatusCode(result));
			Assert.AreEqual("SERVICE-00009", StoredServices.Single(s => s.Name == "New").ServiceID);
		}

		// ---- PATCH /service/{id} ----
		[TestMethod]
		public void UpdateService_ByServiceIdTrimmed_UpdatesOnlyProvidedFields()
		{
			var service = AddService("Old");
			service.Description = "keep";

			var result = Controller.UpdateService($" {service.ServiceID} ", new JObject { ["name"] = " Renamed " });

			Assert.AreEqual(200, StatusCode(result));
			Assert.AreEqual("Renamed", service.Name);
			Assert.AreEqual("keep", service.Description);
			Services.Verify(r => r.Update(service), Times.Once);
		}

		[TestMethod]
		public void UpdateService_ByServiceId_Returns200()
		{
			var service = AddService();
			var result = Controller.UpdateService(service.ServiceID, new JObject { ["description"] = "new" });
			Assert.AreEqual(200, StatusCode(result));
			Assert.AreEqual("new", service.Description);
		}

		[TestMethod]
		public void UpdateService_Unknown_Returns404()
		{
			Assert.AreEqual(404, StatusCode(Controller.UpdateService("SERVICE-00042", new JObject())));
		}

		[TestMethod]
		public void UpdateService_ByInstanceId_ChangesOnlySelectedServiceWithDuplicatePublicId()
		{
			var other = AddService("Other service");
			var selected = AddService("Selected service");

			var result = Controller.UpdateService(selected.Identifier, JObject.FromObject(new { description = "Selected description" }));

			Assert.AreEqual(200, StatusCode(result));
			Assert.AreEqual("Selected description", selected.Description);
			Assert.AreNotEqual("Selected description", other.Description);
		}

		[TestMethod]
		public void UpdateService_NullBody_Returns400()
		{
			var service = AddService();
			Assert.AreEqual(400, StatusCode(Controller.UpdateService(service.ServiceID, null)));
		}

		[TestMethod]
		public void UpdateService_EmptyName_Returns400()
		{
			var service = AddService();
			Assert.AreEqual(400, StatusCode(Controller.UpdateService(service.ServiceID, new JObject { ["name"] = "" })));
		}

		[TestMethod]
		public void UpdateService_NameUsedByOtherService_Returns400()
		{
			AddService("Taken", number: 1);
			var service = AddService("Mine", number: 2);
			Assert.AreEqual(400, StatusCode(Controller.UpdateService(service.ServiceID, new JObject { ["name"] = "Taken" })));
			Services.Verify(r => r.Update(It.IsAny<Service>()), Times.Never);
		}

		[TestMethod]
		public void UpdateService_EndBeforeStart_Returns400()
		{
			var service = AddService();
			var patch = new JObject { ["startTime"] = DateTime.UtcNow, ["endTime"] = DateTime.UtcNow.AddDays(-1) };
			Assert.AreEqual(400, StatusCode(Controller.UpdateService(service.ServiceID, patch)));
		}

		[TestMethod]
		public void UpdateService_InvalidValueType_Returns400()
		{
			var service = AddService();
			Assert.AreEqual(400, StatusCode(Controller.UpdateService(service.ServiceID, new JObject { ["startTime"] = "not-a-date" })));
		}

		// ---- DELETE /service/{id} ----
		[DataTestMethod]
		[DataRow(ServiceStatus.New)]
		[DataRow(ServiceStatus.Retired)]
		public void DeleteService_DeletableStatus_Returns204(ServiceStatus status)
		{
			var service = AddService(status: status);
			Assert.AreEqual(204, StatusCode(Controller.DeleteService(service.ServiceID)));
			Services.Verify(r => r.Delete(service), Times.Once);
		}

		[DataTestMethod]
		[DataRow(ServiceStatus.Designed)]
		[DataRow(ServiceStatus.Reserved)]
		[DataRow(ServiceStatus.Active)]
		[DataRow(ServiceStatus.Terminated)]
		public void DeleteService_OtherStatus_Returns409(ServiceStatus status)
		{
			var service = AddService(status: status);
			Assert.AreEqual(409, StatusCode(Controller.DeleteService(service.ServiceID)));
			Services.Verify(r => r.Delete(It.IsAny<Service>()), Times.Never);
		}

		[TestMethod]
		public void DeleteService_Unknown_Returns404()
		{
			Assert.AreEqual(404, StatusCode(Controller.DeleteService(Guid.NewGuid().ToString())));
		}

		[TestMethod]
		public void DeleteService_RepositoryThrows_Returns500()
		{
			var service = AddService();
			Services.Setup(r => r.Delete(It.IsAny<Service>())).Throws(new InvalidOperationException());
			Assert.AreEqual(500, StatusCode(Controller.DeleteService(service.ServiceID)));
		}

		// ---- GET /service/{id}/transition ----
		[TestMethod]
		public void GetServiceTransitions_New_ReturnsAllowedStatuses()
		{
			var service = AddService(status: ServiceStatus.New);
			var value = Value<JObject>(Controller.GetServiceTransitions(service.ServiceID));
			Assert.AreEqual("new", (string)value["currentStatus"]);
			CollectionAssert.AreEquivalent(new[] { "designed" }, value["allowedStatuses"].ToObject<string[]>());
		}

		[TestMethod]
		public void GetServiceTransitions_Retired_ReturnsEmptyList()
		{
			var service = AddService(status: ServiceStatus.Retired);
			var value = Value<JObject>(Controller.GetServiceTransitions(service.ServiceID));
			Assert.AreEqual(0, ((JArray)value["allowedStatuses"]).Count);
		}

		[TestMethod]
		public void GetServiceTransitions_Unknown_Returns404()
		{
			Assert.AreEqual(404, StatusCode(Controller.GetServiceTransitions("SERVICE-00001")));
		}

		// ---- POST /service/{id}/transition ----
		[TestMethod]
		public void TransitionService_Allowed_CallsTransitionAndReturns200()
		{
			// Reserved->Active and Active->Terminated run shared logic that needs a live engine; covered by integration tests.
			var service = AddService(status: ServiceStatus.New);
			var result = Controller.TransitionService(service.ServiceID, new TransitionServiceRequest { TargetStatus = " Designed " });
			Assert.AreEqual(200, StatusCode(result));
			Services.Verify(r => r.TransitionStatus(service, ServiceTransition.New_To_Designed), Times.Once);
		}

		[TestMethod]
		public void TransitionService_NotAllowed_Returns409()
		{
			var service = AddService(status: ServiceStatus.New);
			var result = Controller.TransitionService(service.ServiceID, new TransitionServiceRequest { TargetStatus = "active" });
			Assert.AreEqual(409, StatusCode(result));
			Services.Verify(r => r.TransitionStatus(It.IsAny<Service>(), It.IsAny<ServiceTransition>()), Times.Never);
		}

		[DataTestMethod]
		[DataRow(null)]
		[DataRow(" ")]
		[DataRow("bogus")]
		public void TransitionService_InvalidTarget_Returns400(string target)
		{
			var service = AddService();
			Assert.AreEqual(400, StatusCode(Controller.TransitionService(service.ServiceID, new TransitionServiceRequest { TargetStatus = target })));
		}

		[TestMethod]
		public void TransitionService_NullBody_Returns400()
		{
			var service = AddService();
			Assert.AreEqual(400, StatusCode(Controller.TransitionService(service.ServiceID, null)));
		}

		[TestMethod]
		public void TransitionService_Unknown_Returns404()
		{
			Assert.AreEqual(404, StatusCode(Controller.TransitionService("SERVICE-00009", new TransitionServiceRequest { TargetStatus = "designed" })));
		}

		[TestMethod]
		public void TransitionService_RepositoryThrows_Returns500()
		{
			var service = AddService(status: ServiceStatus.New);
			Services.Setup(r => r.TransitionStatus(It.IsAny<Service>(), It.IsAny<ServiceTransition>())).Throws(new ApplicationException());
			Assert.AreEqual(500, StatusCode(Controller.TransitionService(service.ServiceID, new TransitionServiceRequest { TargetStatus = "designed" })));
		}

		[TestMethod]
		public void TransitionService_BlockedByLinkedWork_Returns409()
		{
			var service = AddService(status: ServiceStatus.New);
			Services.Setup(r => r.TransitionStatus(It.IsAny<Service>(), It.IsAny<ServiceTransition>())).Throws(new InvalidOperationException("busy"));
			var result = Controller.TransitionService(service.ServiceID, new TransitionServiceRequest { TargetStatus = "designed" });
			Assert.AreEqual(409, StatusCode(result));
			Assert.AreEqual("busy", ErrorDetail(result));
		}

		// ---- POST /service: script parity ----
		[TestMethod]
		public void CreateService_CustomServiceId_UsesIt()
		{
			AddService("Existing", number: 7);
			Assert.AreEqual(201, StatusCode(Controller.CreateService(new CreateServiceRequest { Name = "New", ServiceId = " SERVICE-00100 " })));
			Assert.AreEqual("SERVICE-00100", StoredServices.Single(s => s.Name == "New").ServiceID);
		}

		[TestMethod]
		public void CreateService_InvalidServiceId_Returns400()
		{
			var result = Controller.CreateService(new CreateServiceRequest { Name = "New", ServiceId = "SVC-1" });
			Assert.AreEqual(400, StatusCode(result));
			StringAssert.Contains(ErrorDetail(result), "'serviceId'");
		}

		[TestMethod]
		public void CreateService_ServiceIdInUse_Returns409()
		{
			var existing = AddService("Existing", number: 3);
			Assert.AreEqual(409, StatusCode(Controller.CreateService(new CreateServiceRequest { Name = "New", ServiceId = existing.ServiceID })));
			Services.Verify(r => r.Create(It.IsAny<Service>()), Times.Never);
		}

		[TestMethod]
		public void CreateService_GenerateMonitoringService_CreatesDataMinerService()
		{
			StoredCategories.Add(new ServiceCategory { Identifier = Guid.NewGuid().ToString(), Name = "Video", Icon = "video.svg" });
			var request = new CreateServiceRequest
			{
				Name = "New",
				GenerateMonitoringService = true,
				MonitoringService = " Linked ",
				Category = new ServiceRequestCategory { Name = "Video" },
			};

			Assert.AreEqual(201, StatusCode(Controller.CreateService(request)));

			var created = StoredServices.Single(s => s.Name == "New");
			Assert.AreEqual(true, created.GenerateMonitoringService);
			Assert.AreEqual("Linked", created.MonitoringService);
			MonitoringServices.Verify(m => m.Create("New", "video.svg"), Times.Once);
		}

		[TestMethod]
		public void CreateService_DataMinerServiceExists_Returns409AndWritesNothing()
		{
			MonitoringServices.Setup(m => m.Exists("New")).Returns(true);
			Assert.AreEqual(409, StatusCode(Controller.CreateService(new CreateServiceRequest { Name = "New", GenerateMonitoringService = true })));
			Services.Verify(r => r.Create(It.IsAny<Service>()), Times.Never);
			MonitoringServices.Verify(m => m.Create(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
		}

		[TestMethod]
		public void CreateService_DataMinerServiceFails_Returns500AndKeepsService()
		{
			MonitoringServices.Setup(m => m.Create(It.IsAny<string>(), It.IsAny<string>())).Throws(new TimeoutException("dms details"));

			var result = Controller.CreateService(new CreateServiceRequest { Name = "New", GenerateMonitoringService = true });

			Assert.AreEqual(500, StatusCode(result));
			StringAssert.Contains(ErrorDetail(result), "DataMiner monitoring service");
			Assert.IsFalse(ErrorDetail(result).Contains("dms details"));
			Assert.AreEqual(1, StoredServices.Count(s => s.Name == "New"));
		}

		[TestMethod]
		public void CreateService_UnknownOrganization_Returns400()
		{
			Assert.AreEqual(400, StatusCode(Controller.CreateService(new CreateServiceRequest { Name = "New", OrganizationId = Guid.NewGuid() })));
			Services.Verify(r => r.Create(It.IsAny<Service>()), Times.Never);
		}

		[TestMethod]
		public void CreateService_Organization_SavesOrganization()
		{
			var organizationId = Guid.NewGuid();
			OrganizationNames[organizationId] = "Skyline";

			Assert.AreEqual(201, StatusCode(Controller.CreateService(new CreateServiceRequest { Name = "New", OrganizationId = organizationId })));

			var created = StoredServices.Single(s => s.Name == "New");
			Organizations.Verify(o => o.SetOrganization(Guid.Parse(created.Identifier), organizationId), Times.Once);
		}

		[TestMethod]
		public void CreateService_FromSpecification_CopiesDescriptionAndClearsItemType()
		{
			var specification = new ServiceSpecification
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = "Spec",
				Description = "From spec",
				ServiceItems = new List<ServiceItem>
				{
					new ServiceItem { ServiceItemID = 1, Label = "Item", Type = Enum.GetValues(typeof(DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Enums.ServiceItemTypesEnum)).Cast<DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Enums.ServiceItemTypesEnum>().First() },
				},
				ServiceItemsRelationships = new List<ServiceItemRelationship>(),
			};
			StoredSpecifications.Add(specification);

			var request = new CreateServiceRequest { Name = "New", ServiceSpecificationId = Guid.Parse(specification.Identifier) };
			Assert.AreEqual(201, StatusCode(Controller.CreateService(request)));

			var created = StoredServices.Single(s => s.Name == "New");
			Assert.AreEqual("From spec", created.Description);
			Assert.AreEqual("Item", created.ServiceItems.Single().Label);
			Assert.IsNull(created.ServiceItems.Single().Type);
			Assert.AreEqual(0, StoredVersions.Count, "A specification without configuration gives no configuration version.");
		}

		[TestMethod]
		public void CreateService_FromSpecificationWithConfiguration_CreatesNoConfigurationVersion()
		{
			var specification = AddSpecificationWithConfiguration(out _, out _, out _);

			var request = new CreateServiceRequest { Name = "New", ServiceSpecificationId = Guid.Parse(specification.Identifier) };
			Assert.AreEqual(201, StatusCode(Controller.CreateService(request)));

			var created = StoredServices.Single(s => s.Name == "New");
			Assert.AreEqual(0, StoredVersions.Count);
			Assert.AreEqual(0, created.ConfigurationVersions.Count);
			Assert.IsTrue(String.IsNullOrEmpty(created.ServiceConfigurationId.Identifier));
		}

		[TestMethod]
		public void UpdateService_NewSpecification_CreatesNoConfigurationVersion()
		{
			var service = AddService();
			var specification = AddSpecificationWithConfiguration(out _, out _, out _);

			var patch = new JObject { ["serviceSpecificationId"] = specification.Identifier };
			Assert.AreEqual(200, StatusCode(Controller.UpdateService(service.ServiceID, patch)));

			Assert.AreEqual(specification.Identifier, service.ServiceSpecificationId.Identifier);
			Assert.AreEqual(0, StoredVersions.Count);
		}

		[TestMethod]
		public void CreateConfigurationVersion_WithoutContent_PrefillsFromSpecification()
		{
			var specification = AddSpecificationWithConfiguration(out var defaultValue, out var parameterWithoutDefault, out var profile);
			var service = AddConfigurableService(specification);

			Assert.AreEqual(201, StatusCode(Controller.CreateConfigurationVersion(service.ServiceID, new CreateConfigurationVersionRequest { VersionName = "v2" })));

			var version = StoredVersions.Single();
			Assert.AreEqual("v2", version.VersionName);
			Assert.AreEqual(version.Identifier, service.ConfigurationVersions.Single().Identifier);

			var configurations = version.Parameters.Select(reference => StoredConfigurationValues.Single(value => value.Identifier == reference.Identifier)).ToList();
			Assert.AreEqual(2, configurations.Count);
			var copiedDefault = StoredParameterValues.Single(value => value.Identifier == configurations[0].ConfigurationParameterId.Identifier);
			Assert.AreNotEqual(defaultValue.Identifier, copiedDefault.Identifier);
			Assert.AreEqual("SRT", copiedDefault.StringValue);
			Assert.IsTrue(configurations[0].Mandatory);
			var emptyValue = StoredParameterValues.Single(value => value.Identifier == configurations[1].ConfigurationParameterId.Identifier);
			Assert.AreEqual(parameterWithoutDefault.Identifier, emptyValue.ConfigurationParameterId.Identifier);
			Assert.IsNull(emptyValue.StringValue);
			Assert.IsFalse(configurations[1].Mandatory);

			var serviceProfile = StoredServiceProfiles.Single(p => p.Identifier == version.Profiles.Single().Identifier);
			Assert.IsTrue(serviceProfile.Mandatory);
			var copiedProfile = StoredProfiles.Single(p => p.Identifier == serviceProfile.ProfileId.Identifier);
			Assert.AreNotEqual(profile.Identifier, copiedProfile.Identifier);
			Assert.AreEqual("Main", StoredParameterValues.Single(value => value.Identifier == copiedProfile.ConfigurationParameterValues.Single().Identifier).StringValue);
		}

		[TestMethod]
		public void CreateConfigurationVersion_PrefillWithMandatoryParameterWithoutDefault_ActivateReturns400AndCleansUp()
		{
			var specification = AddSpecificationWithConfiguration(out _, out var parameterWithoutDefault, out _);
			StoredSpecificationConfigurations
				.Single(configuration => configuration.ConfigurationParameterId.Identifier == parameterWithoutDefault.Identifier)
				.MandatoryAtService = true;
			var service = AddConfigurableService(specification);
			string activeVersionId = service.ServiceConfigurationId.Identifier;
			int parameterValueCount = StoredParameterValues.Count;
			int profileCount = StoredProfiles.Count;

			var result = Controller.CreateConfigurationVersion(
				service.ServiceID,
				new CreateConfigurationVersionRequest { VersionName = "invalid active version" },
				activate: true);

			Assert.AreEqual(400, StatusCode(result));
			StringAssert.Contains(ErrorDetail(result), "Bitrate");
			Assert.AreEqual(parameterValueCount, StoredParameterValues.Count);
			Assert.AreEqual(profileCount, StoredProfiles.Count);
			Assert.AreEqual(0, StoredConfigurationValues.Count);
			Assert.AreEqual(0, StoredServiceProfiles.Count);
			Assert.AreEqual(0, StoredVersions.Count);
			Assert.AreEqual(0, service.ConfigurationVersions.Count);
			Assert.AreEqual(activeVersionId, service.ServiceConfigurationId.Identifier);
		}

		[TestMethod]
		public void CreateConfigurationVersion_ReusableSpecificationProfile_IsReferencedNotCopied()
		{
			var specification = AddSpecificationWithConfiguration(out _, out _, out var profile);
			profile.IsReusable = true;
			var service = AddConfigurableService(specification);

			Assert.AreEqual(201, StatusCode(Controller.CreateConfigurationVersion(service.ServiceID, new CreateConfigurationVersionRequest())));

			var serviceProfile = StoredServiceProfiles.Single(p => p.Identifier == StoredVersions.Single().Profiles.Single().Identifier);
			Assert.AreEqual(profile.Identifier, serviceProfile.ProfileId.Identifier);
			Assert.AreSame(profile, StoredProfiles.Single());
		}

		[TestMethod]
		public void CreateConfigurationVersion_WithEmptyContent_DoesNotPrefill()
		{
			var service = AddConfigurableService(AddSpecificationWithConfiguration(out _, out _, out _));
			var request = new CreateConfigurationVersionRequest
			{
				Parameters = new List<ServiceConfigurationValueRequest>(),
				Profiles = new List<ServiceProfileRequest>(),
			};

			Assert.AreEqual(201, StatusCode(Controller.CreateConfigurationVersion(service.ServiceID, request)));

			Assert.AreEqual(0, StoredVersions.Single().Parameters.Count);
			Assert.AreEqual(0, StoredVersions.Single().Profiles.Count);
		}

		[TestMethod]
		public void GetServiceConfiguration_WithSpecification_ReturnsTemplateWithoutSaving()
		{
			var specification = AddSpecificationWithConfiguration(out var defaultValue, out var parameterWithoutDefault, out var profile);
			var service = AddConfigurableService(specification);
			int valueCount = StoredParameterValues.Count;
			int profileCount = StoredProfiles.Count;

			var result = Controller.GetServiceConfiguration(service.ServiceID);

			Assert.AreEqual(200, StatusCode(result));
			var template = Value<List<ConfigurationRecord>>(result).Where(row => row.VersionId == ConfigurationRecord.SpecificationTemplateVersionId).ToList();
			var parameters = template.Where(row => row.RecordType == "Parameter").ToList();
			Assert.AreEqual(2, parameters.Count);
			Assert.AreEqual(defaultValue.Identifier, parameters[0].ParameterValueId);
			Assert.AreEqual("SRT", parameters[0].ParameterValue);
			Assert.IsTrue(parameters[0].Mandatory);
			Assert.AreEqual(parameterWithoutDefault.Identifier, parameters[1].ParameterId);
			Assert.AreEqual(String.Empty, parameters[1].ParameterValueId);
			Assert.IsFalse(parameters[1].Mandatory);
			var profileRow = template.Single(row => row.RecordType == "ProfileParameter");
			Assert.AreEqual(StoredSpecificationProfiles.Single().Identifier, profileRow.ProfileId);
			Assert.AreEqual(profile.Identifier, profileRow.ProfileReferenceId);
			Assert.IsTrue(profileRow.ProfileMandatory);
			Assert.IsFalse(template.Any(row => row.IsActive));

			Assert.AreEqual(0, StoredVersions.Count);
			Assert.AreEqual(0, StoredConfigurationValues.Count);
			Assert.AreEqual(0, StoredServiceProfiles.Count);
			Assert.AreEqual(valueCount, StoredParameterValues.Count);
			Assert.AreEqual(profileCount, StoredProfiles.Count);
		}

		[TestMethod]
		public void GetServiceConfiguration_WithReusableProfileAtTopLevelAndNested_PreservesBothAssignments()
		{
			var definition = new Cfg.ProfileDefinition { Identifier = Guid.NewGuid().ToString(), Name = "Transport" };
			StoredProfileDefinitions.Add(definition);

			var reusable = new Cfg.Profile
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = "Shared",
				IsReusable = true,
				ProfileDefinitionId = new SdmObjectReference<Cfg.ProfileDefinition>(definition.Identifier),
				Profiles = new List<SdmObjectReference<Cfg.Profile>>(),
			};
			var parent = new Cfg.Profile
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = "Parent",
				ProfileDefinitionId = new SdmObjectReference<Cfg.ProfileDefinition>(definition.Identifier),
				Profiles = new List<SdmObjectReference<Cfg.Profile>> { new SdmObjectReference<Cfg.Profile>(reusable.Identifier) },
			};
			StoredProfiles.Add(parent);
			StoredProfiles.Add(reusable);

			var nestedAssignment = new ServiceSpecificationProfile
			{
				Identifier = Guid.NewGuid().ToString(),
				ProfileId = new SdmObjectReference<Cfg.Profile>(parent.Identifier),
			};
			var topLevelAssignment = new ServiceSpecificationProfile
			{
				Identifier = Guid.NewGuid().ToString(),
				ProfileId = new SdmObjectReference<Cfg.Profile>(reusable.Identifier),
			};
			StoredSpecificationProfiles.Add(nestedAssignment);
			StoredSpecificationProfiles.Add(topLevelAssignment);
			var specification = new ServiceSpecification
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = "Reusable collision spec",
				ConfigurationProfiles = new List<SdmObjectReference<ServiceSpecificationProfile>>
				{
					new SdmObjectReference<ServiceSpecificationProfile>(nestedAssignment.Identifier),
					new SdmObjectReference<ServiceSpecificationProfile>(topLevelAssignment.Identifier),
				},
			};
			StoredSpecifications.Add(specification);
			var service = AddConfigurableService(specification);

			var result = Controller.GetServiceConfiguration(service.ServiceID);

			Assert.AreEqual(200, StatusCode(result));
			var template = Value<List<ConfigurationRecord>>(result)
				.Where(row => row.VersionId == ConfigurationRecord.SpecificationTemplateVersionId)
				.ToList();
			Assert.IsTrue(template.Any(row => row.RecordType == "Profile"
				&& row.ProfileId == topLevelAssignment.Identifier
				&& row.ProfileReferenceId == reusable.Identifier));
			Assert.IsTrue(template.Any(row => row.RecordType == "NestedProfile"
				&& row.ProfileId == nestedAssignment.Identifier
				&& row.NestedProfileId == reusable.Identifier));
		}

		[TestMethod]
		public void SaveServiceConfiguration_NewReusableProfile_IsReferencedNotModified()
		{
			var service = AddConfigurableService(AddSpecificationWithConfiguration(out _, out _, out var profile));
			var definition = new Cfg.ProfileDefinition { Identifier = Guid.NewGuid().ToString(), Name = "Transport" };
			StoredProfileDefinitions.Add(definition);
			profile.IsReusable = true;
			profile.ProfileDefinitionId = new SdmObjectReference<Cfg.ProfileDefinition>(definition.Identifier);
			var request = new ServiceConfigurationRequest
			{
				VersionName = "Initial version",
				ActivateVersion = true,
				Profiles = new List<ServiceConfigurationProfileRequest>
				{
					new ServiceConfigurationProfileRequest { Id = profile.Identifier, Name = "Renamed", DefinitionId = definition.Identifier, Reusable = true, Mandatory = true },
				},
			};

			var result = Controller.SaveServiceConfiguration(service.ServiceID, JObject.FromObject(request));

			int statusCode = StatusCode(result);
			Assert.AreEqual(200, statusCode, statusCode == 200 ? String.Empty : ErrorDetail(result));
			var serviceProfile = StoredServiceProfiles.Single();
			Assert.AreEqual(serviceProfile.Identifier, StoredVersions.Single().Profiles.Single().Identifier);
			Assert.AreEqual(profile.Identifier, serviceProfile.ProfileId.Identifier);
			Assert.IsTrue(serviceProfile.Mandatory);
			Assert.AreSame(profile, StoredProfiles.Single());
			Assert.AreEqual("Transport", profile.Name);
		}

		[TestMethod]
		public void SaveServiceConfiguration_ProfileOwnershipError_UsesProfileNameWithoutGuid()
		{
			var service = AddConfigurableService(AddSpecificationWithConfiguration(out _, out _, out _));
			string invalidProfileId = Guid.NewGuid().ToString();
			var request = new ServiceConfigurationRequest
			{
				Profiles = new List<ServiceConfigurationProfileRequest>
				{
					new ServiceConfigurationProfileRequest
					{
						Id = invalidProfileId,
						Name = "Shared transport",
						DefinitionId = Guid.NewGuid().ToString(),
					},
				},
			};

			var result = Controller.SaveServiceConfiguration(service.ServiceID, JObject.FromObject(request));

			Assert.AreEqual(400, StatusCode(result));
			StringAssert.Contains(ErrorDetail(result), "Shared transport");
			Assert.IsFalse(ErrorDetail(result).Contains(invalidProfileId));
		}

		private Service AddConfigurableService(ServiceSpecification specification)
		{
			var service = AddService();

			// FullServiceBuilder dereferences these references, so give them identifiers.
			service.CategoryId = new SdmObjectReference<ServiceCategory>(Guid.NewGuid().ToString());
			service.ServiceSpecificationId = new SdmObjectReference<ServiceSpecification>(specification.Identifier);
			service.ServiceConfigurationId = new SdmObjectReference<ServiceConfigurationVersion>(Guid.NewGuid().ToString());
			return service;
		}

		/// <summary>
		/// Adds a specification with a "Protocol" parameter (default "SRT", mandatory at service), a "Bitrate" parameter without
		/// a default and a profile holding the value "Main" (mandatory at service).
		/// </summary>
		private ServiceSpecification AddSpecificationWithConfiguration(out Cfg.ConfigurationParameterValue defaultValue, out Cfg.ConfigurationParameter parameterWithoutDefault, out Cfg.Profile profile)
		{
			var protocol = new Cfg.ConfigurationParameter { Identifier = Guid.NewGuid().ToString(), Name = "Protocol" };
			parameterWithoutDefault = new Cfg.ConfigurationParameter { Identifier = Guid.NewGuid().ToString(), Name = "Bitrate" };
			StoredParameters.Add(protocol);
			StoredParameters.Add(parameterWithoutDefault);
			defaultValue = new Cfg.ConfigurationParameterValue
			{
				Identifier = Guid.NewGuid().ToString(),
				ConfigurationParameterId = new SdmObjectReference<Cfg.ConfigurationParameter>(protocol.Identifier),
				StringValue = "SRT",
			};
			var profileValue = new Cfg.ConfigurationParameterValue { Identifier = Guid.NewGuid().ToString(), StringValue = "Main" };
			StoredParameterValues.Add(defaultValue);
			StoredParameterValues.Add(profileValue);
			profile = new Cfg.Profile
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = "Transport",
				Profiles = new List<SdmObjectReference<Cfg.Profile>>(),
				ConfigurationParameterValues = new List<SdmObjectReference<Cfg.ConfigurationParameterValue>> { new SdmObjectReference<Cfg.ConfigurationParameterValue>(profileValue.Identifier) },
			};
			StoredProfiles.Add(profile);

			var withDefault = new ServiceSpecificationConfigurationValue
			{
				Identifier = Guid.NewGuid().ToString(),
				ConfigurationParameterId = new SdmObjectReference<Cfg.ConfigurationParameter>(defaultValue.Identifier),
				MandatoryAtService = true,
			};
			var withoutDefault = new ServiceSpecificationConfigurationValue
			{
				Identifier = Guid.NewGuid().ToString(),
				ConfigurationParameterId = new SdmObjectReference<Cfg.ConfigurationParameter>(parameterWithoutDefault.Identifier),
			};
			var specificationProfile = new ServiceSpecificationProfile
			{
				Identifier = Guid.NewGuid().ToString(),
				ProfileId = new SdmObjectReference<Cfg.Profile>(profile.Identifier),
				MandatoryAtService = true,
			};
			StoredSpecificationConfigurations.Add(withDefault);
			StoredSpecificationConfigurations.Add(withoutDefault);
			StoredSpecificationProfiles.Add(specificationProfile);

			var specification = new ServiceSpecification
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = "Configured spec",
				ConfigurationParameters = new List<SdmObjectReference<ServiceSpecificationConfigurationValue>>
				{
					new SdmObjectReference<ServiceSpecificationConfigurationValue>(withDefault.Identifier),
					new SdmObjectReference<ServiceSpecificationConfigurationValue>(withoutDefault.Identifier),
				},
				ConfigurationProfiles = new List<SdmObjectReference<ServiceSpecificationProfile>>
				{
					new SdmObjectReference<ServiceSpecificationProfile>(specificationProfile.Identifier),
				},
			};
			StoredSpecifications.Add(specification);
			return specification;
		}

		// ---- PATCH /service/{id}: script parity ----
		[TestMethod]
		public void UpdateService_MonitoringServiceAndOrganization_AreSaved()
		{
			var service = AddService();
			var organizationId = Guid.NewGuid();
			OrganizationNames[organizationId] = "Skyline";

			var patch = new JObject { ["monitoringService"] = " Linked ", ["organizationId"] = organizationId.ToString() };
			Assert.AreEqual(200, StatusCode(Controller.UpdateService(service.ServiceID, patch)));

			Assert.AreEqual("Linked", service.MonitoringService);
			Organizations.Verify(o => o.SetOrganization(Guid.Parse(service.Identifier), organizationId), Times.Once);
		}

		[TestMethod]
		public void UpdateService_NullOrganization_ClearsOrganization()
		{
			var service = AddService();
			Assert.AreEqual(200, StatusCode(Controller.UpdateService(service.ServiceID, new JObject { ["organizationId"] = null })));
			Organizations.Verify(o => o.SetOrganization(Guid.Parse(service.Identifier), null), Times.Once);
		}

		[TestMethod]
		public void UpdateService_UnknownOrganization_Returns400AndWritesNothing()
		{
			var service = AddService();
			Assert.AreEqual(400, StatusCode(Controller.UpdateService(service.ServiceID, new JObject { ["organizationId"] = Guid.NewGuid().ToString() })));
			Services.Verify(r => r.Update(It.IsAny<Service>()), Times.Never);
			Organizations.Verify(o => o.SetOrganization(It.IsAny<Guid>(), It.IsAny<Guid?>()), Times.Never);
		}

		[TestMethod]
		public void UpdateService_CategoryWithoutIcon_UsesCategoryIcon()
		{
			var service = AddService();
			service.Icon = "old.svg";
			StoredCategories.Add(new ServiceCategory { Identifier = Guid.NewGuid().ToString(), Name = "Video", Icon = "video.svg" });

			Assert.AreEqual(200, StatusCode(Controller.UpdateService(service.ServiceID, new JObject { ["category"] = new JObject { ["name"] = "Video" } })));
			Assert.AreEqual("video.svg", service.Icon);

			Assert.AreEqual(200, StatusCode(Controller.UpdateService(service.ServiceID, new JObject { ["category"] = null })));
			Assert.AreEqual(String.Empty, service.Icon);
		}

		// ---- GET /service ----
		[TestMethod]
		public void ListServices_AddsStatusCatalogAndOrganizationNames()
		{
			var category = new ServiceCategory { Identifier = Guid.NewGuid().ToString(), Name = "Video", Type = "Broadcast" };
			var specification = new ServiceSpecification { Identifier = Guid.NewGuid().ToString(), Name = "Spec" };
			StoredCategories.Add(category);
			StoredSpecifications.Add(specification);

			var service = AddService("Svc", ServiceStatus.Active);
			service.CategoryId = new SdmObjectReference<ServiceCategory>(category.Identifier);
			service.ServiceSpecificationId = new SdmObjectReference<ServiceSpecification>(specification.Identifier);
			var organizationId = Guid.NewGuid();
			OrganizationNames[organizationId] = "Skyline";
			ServiceOrganizations[Guid.Parse(service.Identifier)] = organizationId;

			var item = Value<List<JObject>>(Controller.ListServices()).Single();

			Assert.AreEqual(service.ServiceID, (string)item["ServiceID"]);
			Assert.AreEqual("active", (string)item["StatusName"]);
			Assert.AreEqual("Video", (string)item["CategoryName"]);
			Assert.AreEqual("Broadcast", (string)item["CategoryType"]);
			Assert.AreEqual("Spec", (string)item["ServiceSpecificationName"]);
			Assert.AreEqual(organizationId, (Guid)item["OrganizationId"]);
			Assert.AreEqual("Skyline", (string)item["OrganizationName"]);
		}

		[TestMethod]
		public void ListServices_OrganizationLookupFails_StillReturnsServices()
		{
			AddService("Svc");
			Organizations.Setup(o => o.ReadServiceOrganizationIds(It.IsAny<IEnumerable<Guid>>())).Throws(new InvalidOperationException());

			var item = Value<List<JObject>>(Controller.ListServices()).Single();

			Assert.AreEqual("Svc", (string)item["Name"]);
			Assert.AreEqual(JTokenType.Null, item["OrganizationId"].Type);
		}

		[TestMethod]
		public void ListServices_WithFields_ReturnsOnlyRequestedFields()
		{
			AddService("Svc");
			var item = Value<List<object>>(Controller.ListServices(fields: "name,statusName")).Cast<JObject>().Single();
			Assert.AreEqual(2, item.Count);
			Assert.AreEqual("new", (string)item["StatusName"]);
		}

		[TestMethod]
		public void ListServices_WithSummaryFields_SkipsOrganizationLookupsAndHeavyProperties()
		{
			var category = new ServiceCategory { Identifier = Guid.NewGuid().ToString(), Name = "Video", Type = "Broadcast" };
			var specification = new ServiceSpecification { Identifier = Guid.NewGuid().ToString(), Name = "Spec" };
			StoredCategories.Add(category);
			StoredSpecifications.Add(specification);
			var service = AddService("Svc", ServiceStatus.Active);
			service.CategoryId = new SdmObjectReference<ServiceCategory>(category.Identifier);
			service.ServiceSpecificationId = new SdmObjectReference<ServiceSpecification>(specification.Identifier);

			const string fields = "Identifier,ServiceID,Name,StatusName,CategoryName,CategoryType,ServiceSpecificationId,ServiceSpecificationName";
			var item = Value<List<object>>(Controller.ListServices(fields: fields)).Cast<JObject>().Single();

			Assert.AreEqual(fields.Split(',').Length, item.Count);
			Assert.AreEqual("Svc", (string)item["Name"]);
			Assert.AreEqual("active", (string)item["StatusName"]);
			Assert.AreEqual("Video", (string)item["CategoryName"]);
			Assert.AreEqual("Broadcast", (string)item["CategoryType"]);
			Assert.AreEqual("Spec", (string)item["ServiceSpecificationName"]);
			Assert.IsFalse(item.ContainsKey("ServiceItems"));
			Assert.IsFalse(item.ContainsKey("ConfigurationVersions"));
			Assert.IsFalse(item.ContainsKey("ServiceScripts"));
			Organizations.Verify(o => o.ReadServiceOrganizationIds(It.IsAny<IEnumerable<Guid>>()), Times.Never);
			Organizations.Verify(o => o.ReadOrganizationNames(), Times.Never);
		}

		[TestMethod]
		public void ListServicesCursor_StatusFilter_ReturnsMatchingServicesAndFilteredCount()
		{
			var active = AddService("Active", ServiceStatus.Active, 1);
			var designed = AddService("Designed", ServiceStatus.Designed, 2);
			AddService("Retired", ServiceStatus.Retired, 3);

			var page = Value<CursorPageResponse<JObject>>(Controller.ListServices(pagination: "cursor", status: "active,designed"));

			CollectionAssert.AreEquivalent(new[] { active.Identifier, designed.Identifier }, page.Items.Select(i => (string)i["Identifier"]).ToList());
			Assert.AreEqual(2L, page.TotalCount);
		}

		[TestMethod]
		public void ListServicesCursor_Search_MatchesNameInAnyCaseAndCategoryName()
		{
			var category = new ServiceCategory { Identifier = Guid.NewGuid().ToString(), Name = "Video", Type = "Broadcast" };
			StoredCategories.Add(category);
			var alpha = AddService("Alpha", ServiceStatus.Active, 1);
			var beta = AddService("Beta", ServiceStatus.Active, 2);
			beta.CategoryId = new SdmObjectReference<ServiceCategory>(category.Identifier);

			var byName = Value<CursorPageResponse<JObject>>(Controller.ListServices(pagination: "cursor", search: "alp"));
			var byCategory = Value<CursorPageResponse<JObject>>(Controller.ListServices(pagination: "cursor", search: "video"));

			Assert.AreEqual(alpha.Identifier, (string)byName.Items.Single()["Identifier"]);
			Assert.AreEqual(beta.Identifier, (string)byCategory.Items.Single()["Identifier"]);
		}

		[TestMethod]
		public void ListServicesCursor_SearchMatchingManyReferences_ReturnsServicesOfEveryMatchingSpecificationAndCategory()
		{
			for (int i = 1; i <= 150; i++)
			{
				StoredSpecifications.Add(new ServiceSpecification { Identifier = Guid.NewGuid().ToString(), Name = $"Uplink spec {i}" });
				StoredCategories.Add(new ServiceCategory { Identifier = Guid.NewGuid().ToString(), Name = $"Uplink category {i}" });
			}

			var bySpecification = AddService("Alpha", ServiceStatus.Active, 1);
			bySpecification.ServiceSpecificationId = new SdmObjectReference<ServiceSpecification>(StoredSpecifications.Last().Identifier);
			var byCategory = AddService("Beta", ServiceStatus.Active, 2);
			byCategory.CategoryId = new SdmObjectReference<ServiceCategory>(StoredCategories.Last().Identifier);
			AddService("Gamma", ServiceStatus.Active, 3);

			var page = Value<CursorPageResponse<JObject>>(Controller.ListServices(pagination: "cursor", search: "uplink", includeTotalCount: true));

			CollectionAssert.AreEquivalent(new[] { bySpecification.Identifier, byCategory.Identifier }, page.Items.Select(i => (string)i["Identifier"]).ToList());
			Assert.AreEqual(2L, page.TotalCount);
		}

		[TestMethod]
		public void ListServicesCursor_RepositoryError_LogsFullExceptionAndReturnsGeneric500()
		{
			var exception = new NullReferenceException("search diagnostic");
			Services.Setup(r => r.ReadPaged(It.IsAny<IQuery<Service>>(), It.IsAny<int>()))
				.Throws(exception);

			var result = Controller.ListServices(pagination: "cursor", search: "e");

			Assert.AreEqual(500, StatusCode(result));
			Assert.IsFalse(ErrorDetail(result).Contains("search diagnostic"));
			var errorLog = ControllerLogger.Invocations.Single(call =>
				call.Method.Name == nameof(ILogger.Log) && (LogLevel)call.Arguments[0] == LogLevel.Error);
			Assert.AreSame(exception, errorLog.Arguments[3]);
			Assert.IsFalse(String.IsNullOrWhiteSpace(exception.StackTrace));
		}

		[TestMethod]
		public void ListServicesCursor_InvalidStatusOrSort_Returns400()
		{
			Assert.AreEqual(400, StatusCode(Controller.ListServices(pagination: "cursor", status: "unknown")));
			Assert.AreEqual(400, StatusCode(Controller.ListServices(pagination: "cursor", sort: "category")));
		}
	}
}

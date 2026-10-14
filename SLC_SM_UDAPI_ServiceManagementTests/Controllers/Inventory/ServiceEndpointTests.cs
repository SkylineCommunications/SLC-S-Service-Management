namespace SLC_SM_UDAPI_ServiceManagement.Tests.Controllers
{
	using System.Collections.Generic;
	using Moq;
	using Newtonsoft.Json.Linq;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using SLC_SM_UDAPI_ServiceManagement.Models;
	using SLC_SM_UDAPI_ServiceManagement.Models.Inventory;

	using ServiceStatus = DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Service_Behavior.StatusesEnum;
	using ServiceTransition = DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Service_Behavior.TransitionsEnum;

	[TestClass]
	public class ServiceEndpointTests : ServiceInventoryControllerTestBase
	{
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

		[DataTestMethod]
		[DataRow(null)]
		[DataRow(" ")]
		[DataRow("not-a-service-id")]
		[DataRow("SERVICE-00099")]
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
	}
}

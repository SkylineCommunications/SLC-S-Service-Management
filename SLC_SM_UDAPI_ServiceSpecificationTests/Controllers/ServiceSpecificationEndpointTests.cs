namespace SLC_SM_UDAPI_ServiceSpecification.Tests.Controllers
{
	using System;
	using System.Collections.Generic;
	using Microsoft.Extensions.Logging;
	using Moq;
	using Newtonsoft.Json.Linq;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using Skyline.DataMiner.SDM;
	using SLC_SM_UDAPI_ServiceSpecification.Controllers;
	using SLC_SM_UDAPI_ServiceSpecification.Models;

	/// <summary>
	/// Tests for GET {id}, POST, PATCH and DELETE of the service specification controller.
	/// </summary>
	[TestClass]
	public class ServiceSpecificationEndpointTests : ServiceCatalogTestBase
	{
		private ServiceSpecificationController controller;

		[TestInitialize]
		public void Initialize()
		{
			controller = new ServiceSpecificationController(Mock.Of<ILogger<ServiceSpecificationController>>(), Helper.Object);
			AttachContext(controller);
		}

		// ---- Get by id ----
		[TestMethod]
		public void Get_Existing_Returns200()
		{
			var spec = AddSpec("A");
			var result = controller.GetServiceSpecification(spec.Identifier);
			Assert.AreEqual(200, StatusCode(result));
			Assert.AreEqual("A", Value<ServiceSpecification>(result).Name);
		}

		[TestMethod]
		public void Get_Fields_ReturnsOnlyRequested()
		{
			var spec = AddSpec("A");
			var json = Value<JObject>(controller.GetServiceSpecification(spec.Identifier, "name"));
			CollectionAssert.AreEqual(new[] { "Name" }, json.Properties().Select(p => p.Name).ToArray());
		}

		[TestMethod]
		public void Get_Unknown_Returns404()
		{
			Assert.AreEqual(404, StatusCode(controller.GetServiceSpecification(Guid.NewGuid().ToString())));
		}

		[TestMethod]
		public void Get_InvalidId_Returns400()
		{
			Assert.AreEqual(400, StatusCode(controller.GetServiceSpecification("nope")));
		}

		// ---- Create ----
		[TestMethod]
		public void Create_Minimal_Returns201()
		{
			var result = controller.CreateServiceSpecification(new ServiceSpecificationRequest { Name = " Feed " });

			Assert.AreEqual(201, StatusCode(result));
			Assert.AreEqual(1, Specifications.Count);
			Assert.AreEqual("Feed", Specifications[0].Name);
			Assert.AreEqual(0, Specifications[0].ServiceItems.Count);
		}

		[TestMethod]
		public void Create_WithConfiguration_CreatesOwnedObjects()
		{
			var parameter = AddWithId(ConfigurationParameters, new ConfigurationParameter());
			var profile = AddWithId(Profiles, new Profile());
			var definition = AddWithId(ProfileDefinitions, new ProfileDefinition());

			var request = new ServiceSpecificationRequest
			{
				Name = "Feed",
				ConfigurationParameters = new List<ConfigurationParameterRequest>
				{
					new ConfigurationParameterRequest { ConfigurationParameterId = Guid.Parse(parameter.Identifier), MandatoryAtService = true },
				},
				ConfigurationProfiles = new List<ConfigurationProfileRequest>
				{
					new ConfigurationProfileRequest { ProfileId = Guid.Parse(profile.Identifier), ProfileDefinitionId = Guid.Parse(definition.Identifier) },
				},
			};

			Assert.AreEqual(201, StatusCode(controller.CreateServiceSpecification(request)));
			Assert.AreEqual(1, ConfigValues.Count);
			Assert.IsTrue(ConfigValues[0].MandatoryAtService);
			Assert.IsTrue(ConfigValues[0].ExposeAtServiceOrder, "exposeAtServiceOrder defaults to true");
			Assert.AreEqual(1, SpecProfiles.Count);
			Assert.AreEqual(ConfigValues[0].Identifier, Specifications[0].ConfigurationParameters.Single().Identifier);
			Assert.AreEqual(SpecProfiles[0].Identifier, Specifications[0].ConfigurationProfiles.Single().Identifier);
		}

		[TestMethod]
		public void Create_WithItemsAndRelationship_Returns201()
		{
			var request = new ServiceSpecificationRequest
			{
				Name = "Feed",
				ServiceItems = new List<ServiceItem> { new ServiceItem { ServiceItemID = 0 }, new ServiceItem { ServiceItemID = 1 } },
				ServiceItemRelationships = new List<ServiceItemRelationship> { new ServiceItemRelationship { ParentServiceItem = "0", ChildServiceItem = "1" } },
			};
			Assert.AreEqual(201, StatusCode(controller.CreateServiceSpecification(request)));
			Assert.AreEqual(2, Specifications[0].ServiceItems.Count);
		}

		[TestMethod]
		public void Create_NullBody_Returns400()
		{
			Assert.AreEqual(400, StatusCode(controller.CreateServiceSpecification(null)));
		}

		[TestMethod]
		public void Create_MissingName_Returns400()
		{
			Assert.AreEqual(400, StatusCode(controller.CreateServiceSpecification(new ServiceSpecificationRequest { Name = " " })));
		}

		[TestMethod]
		public void Create_DuplicateName_Returns409()
		{
			AddSpec("Feed");
			Assert.AreEqual(409, StatusCode(controller.CreateServiceSpecification(new ServiceSpecificationRequest { Name = "FEED" })));
			Assert.AreEqual(1, Specifications.Count);
		}

		[TestMethod]
		public void Create_UnknownConfigurationParameter_Returns400()
		{
			var request = new ServiceSpecificationRequest
			{
				Name = "Feed",
				ConfigurationParameters = new List<ConfigurationParameterRequest> { new ConfigurationParameterRequest { ConfigurationParameterId = Guid.NewGuid() } },
			};
			var result = controller.CreateServiceSpecification(request);
			Assert.AreEqual(400, StatusCode(result));
			StringAssert.Contains(ErrorDetail(result), "Configuration parameter");
			Assert.AreEqual(0, ConfigValues.Count);
		}

		[TestMethod]
		public void Create_ConfigurationParameterWithoutId_Returns400()
		{
			var request = new ServiceSpecificationRequest { Name = "Feed", ConfigurationParameters = new List<ConfigurationParameterRequest> { new ConfigurationParameterRequest() } };
			Assert.AreEqual(400, StatusCode(controller.CreateServiceSpecification(request)));
		}

		[TestMethod]
		public void Create_UnknownProfile_Returns400()
		{
			var request = new ServiceSpecificationRequest
			{
				Name = "Feed",
				ConfigurationProfiles = new List<ConfigurationProfileRequest> { new ConfigurationProfileRequest { ProfileId = Guid.NewGuid() } },
			};
			Assert.AreEqual(400, StatusCode(controller.CreateServiceSpecification(request)));
		}

		[TestMethod]
		public void Create_UnknownProfileDefinition_Returns400()
		{
			var profile = AddWithId(Profiles, new Profile());
			var request = new ServiceSpecificationRequest
			{
				Name = "Feed",
				ConfigurationProfiles = new List<ConfigurationProfileRequest>
				{
					new ConfigurationProfileRequest { ProfileId = Guid.Parse(profile.Identifier), ProfileDefinitionId = Guid.NewGuid() },
				},
			};
			Assert.AreEqual(400, StatusCode(controller.CreateServiceSpecification(request)));
		}

		[TestMethod]
		public void Create_DuplicateServiceItemIds_Returns400()
		{
			var request = new ServiceSpecificationRequest
			{
				Name = "Feed",
				ServiceItems = new List<ServiceItem> { new ServiceItem { ServiceItemID = 1 }, new ServiceItem { ServiceItemID = 1 } },
			};
			Assert.AreEqual(400, StatusCode(controller.CreateServiceSpecification(request)));
		}

		[TestMethod]
		public void Create_RelationshipToUnknownItem_Returns400()
		{
			var request = new ServiceSpecificationRequest
			{
				Name = "Feed",
				ServiceItems = new List<ServiceItem> { new ServiceItem { ServiceItemID = 0 } },
				ServiceItemRelationships = new List<ServiceItemRelationship> { new ServiceItemRelationship { ParentServiceItem = "0", ChildServiceItem = "7" } },
			};
			Assert.AreEqual(400, StatusCode(controller.CreateServiceSpecification(request)));
		}

		[TestMethod]
		public void Create_SaveFails_Returns500AndRemovesOwnedObjects()
		{
			var parameter = AddWithId(ConfigurationParameters, new ConfigurationParameter());
			var specs = Repository(Specifications);
			specs.Setup(r => r.Create(It.IsAny<ServiceSpecification>())).Throws(new InvalidOperationException("boom"));
			Catalog.SetupGet(c => c.ServiceSpecifications).Returns(specs.Object);

			var request = new ServiceSpecificationRequest
			{
				Name = "Feed",
				ConfigurationParameters = new List<ConfigurationParameterRequest> { new ConfigurationParameterRequest { ConfigurationParameterId = Guid.Parse(parameter.Identifier) } },
			};

			Assert.AreEqual(500, StatusCode(controller.CreateServiceSpecification(request)));
			Assert.AreEqual(0, ConfigValues.Count);
		}

		// ---- Patch ----
		[TestMethod]
		public void Patch_Name_UpdatesOnlyName()
		{
			var spec = AddSpec("Old", "keep");
			var result = controller.UpdateServiceSpecification(spec.Identifier, new JObject { ["name"] = "New" });

			Assert.AreEqual(200, StatusCode(result));
			Assert.AreEqual("New", spec.Name);
			Assert.AreEqual("keep", spec.Description);
		}

		[TestMethod]
		public void Patch_DescriptionNull_ClearsDescription()
		{
			var spec = AddSpec("A", "desc");
			Assert.AreEqual(200, StatusCode(controller.UpdateServiceSpecification(spec.Identifier, new JObject { ["description"] = null })));
			Assert.IsNull(spec.Description);
		}

		[TestMethod]
		public void Patch_ServiceItems_ReplacesArray()
		{
			var spec = AddSpec("A");
			spec.ServiceItems = new List<ServiceItem> { new ServiceItem { ServiceItemID = 5 } };
			var patch = new JObject { ["serviceItems"] = new JArray(new JObject { ["ServiceItemID"] = 1 }, new JObject { ["ServiceItemID"] = 2 }) };

			Assert.AreEqual(200, StatusCode(controller.UpdateServiceSpecification(spec.Identifier, patch)));
			CollectionAssert.AreEqual(new long?[] { 1, 2 }, spec.ServiceItems.Select(i => i.ServiceItemID).ToArray());
		}

		[TestMethod]
		public void Patch_ConfigurationParameters_ReplacesAndDeletesOld()
		{
			var parameter = AddWithId(ConfigurationParameters, new ConfigurationParameter());
			var old = AddWithId(ConfigValues, new ServiceSpecificationConfigurationValue());
			var spec = AddSpec("A");
			spec.ConfigurationParameters = new List<SdmObjectReference<ServiceSpecificationConfigurationValue>> { new SdmObjectReference<ServiceSpecificationConfigurationValue>(old.Identifier) };

			var patch = new JObject { ["configurationParameters"] = new JArray(new JObject { ["configurationParameterId"] = parameter.Identifier }) };

			Assert.AreEqual(200, StatusCode(controller.UpdateServiceSpecification(spec.Identifier, patch)));
			Assert.AreEqual(1, ConfigValues.Count);
			Assert.AreNotEqual(old.Identifier, ConfigValues[0].Identifier);
			Assert.AreEqual(ConfigValues[0].Identifier, spec.ConfigurationParameters.Single().Identifier);
		}

		[TestMethod]
		public void Patch_EmptyName_Returns400()
		{
			var spec = AddSpec("A");
			Assert.AreEqual(400, StatusCode(controller.UpdateServiceSpecification(spec.Identifier, new JObject { ["name"] = "" })));
			Assert.AreEqual("A", spec.Name);
		}

		[TestMethod]
		public void Patch_NameOfOtherSpec_Returns409()
		{
			AddSpec("Taken");
			var spec = AddSpec("A");
			Assert.AreEqual(409, StatusCode(controller.UpdateServiceSpecification(spec.Identifier, new JObject { ["name"] = "taken" })));
		}

		[TestMethod]
		public void Patch_SameName_Returns200()
		{
			var spec = AddSpec("A");
			Assert.AreEqual(200, StatusCode(controller.UpdateServiceSpecification(spec.Identifier, new JObject { ["name"] = "A" })));
		}

		[TestMethod]
		public void Patch_UnknownProfile_Returns400()
		{
			var spec = AddSpec("A");
			var patch = new JObject { ["configurationProfiles"] = new JArray(new JObject { ["profileId"] = Guid.NewGuid().ToString() }) };
			Assert.AreEqual(400, StatusCode(controller.UpdateServiceSpecification(spec.Identifier, patch)));
		}

		[TestMethod]
		public void Patch_Unknown_Returns404()
		{
			Assert.AreEqual(404, StatusCode(controller.UpdateServiceSpecification(Guid.NewGuid().ToString(), new JObject())));
		}

		[TestMethod]
		public void Patch_InvalidIdOrBody_Returns400()
		{
			var spec = AddSpec("A");
			Assert.AreEqual(400, StatusCode(controller.UpdateServiceSpecification("x", new JObject())));
			Assert.AreEqual(400, StatusCode(controller.UpdateServiceSpecification(spec.Identifier, null)));
		}

		// ---- Delete ----
		[TestMethod]
		public void Delete_Unreferenced_Returns204AndRemovesOwnedObjects()
		{
			var config = AddWithId(ConfigValues, new ServiceSpecificationConfigurationValue());
			var profile = AddWithId(SpecProfiles, new ServiceSpecificationProfile());
			var spec = AddSpec("A");
			spec.ConfigurationParameters = new List<SdmObjectReference<ServiceSpecificationConfigurationValue>> { new SdmObjectReference<ServiceSpecificationConfigurationValue>(config.Identifier) };
			spec.ConfigurationProfiles = new List<SdmObjectReference<ServiceSpecificationProfile>> { new SdmObjectReference<ServiceSpecificationProfile>(profile.Identifier) };

			Assert.AreEqual(204, StatusCode(controller.DeleteServiceSpecification(spec.Identifier)));
			Assert.AreEqual(0, Specifications.Count);
			Assert.AreEqual(0, ConfigValues.Count);
			Assert.AreEqual(0, SpecProfiles.Count);
		}

		[TestMethod]
		public void Delete_ReferencedByService_Returns409()
		{
			var spec = AddSpec("A");
			Services.Add(new Service { Identifier = Guid.NewGuid().ToString(), ServiceSpecificationId = new SdmObjectReference<ServiceSpecification>(spec.Identifier) });
			Assert.AreEqual(409, StatusCode(controller.DeleteServiceSpecification(spec.Identifier)));
			Assert.AreEqual(1, Specifications.Count);
			Assert.AreEqual(0, OrderItemReads, "Order items must not be read when a service already blocks the delete.");
		}

		[TestMethod]
		public void Delete_ReferencedByOpenOrderItem_Returns409()
		{
			var spec = AddSpec("A");
			OrderItems.Add(OrderItem(spec, "New"));
			Assert.AreEqual(409, StatusCode(controller.DeleteServiceSpecification(spec.Identifier)));
		}

		[DataTestMethod]
		[DataRow("Completed")]
		[DataRow("Cancelled")]
		[DataRow("Rejected")]
		[DataRow("Failed")]
		public void Delete_ReferencedOnlyByClosedOrderItem_Returns204(string status)
		{
			var spec = AddSpec("A");
			OrderItems.Add(OrderItem(spec, status));
			Assert.AreEqual(204, StatusCode(controller.DeleteServiceSpecification(spec.Identifier)));
		}

		[TestMethod]
		public void Delete_Unknown_Returns404()
		{
			Assert.AreEqual(404, StatusCode(controller.DeleteServiceSpecification(Guid.NewGuid().ToString())));
		}

		[TestMethod]
		public void Delete_InvalidId_Returns400()
		{
			Assert.AreEqual(400, StatusCode(controller.DeleteServiceSpecification("x")));
		}

		private ServiceSpecification AddSpec(string name, string description = null)
		{
			return AddWithId(Specifications, new ServiceSpecification { Name = name, Description = description });
		}

		private static ServiceOrderItem OrderItem(ServiceSpecification spec, string status)
		{
			var item = new ServiceOrderItem
			{
				Identifier = Guid.NewGuid().ToString(),
				ServiceInfo = new ServiceOrderItemServiceInfo { SpecificationId = new SdmObjectReference<ServiceSpecification>(spec.Identifier) },
			};
			SetItemStatus(item, status);
			return item;
		}
	}
}

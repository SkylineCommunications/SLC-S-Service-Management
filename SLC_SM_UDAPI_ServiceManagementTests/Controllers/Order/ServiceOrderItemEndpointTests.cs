namespace SLC_SM_UDAPI_ServiceManagement.Tests.Controllers
{
	using System.Collections.Generic;
	using Moq;
	using Newtonsoft.Json.Linq;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.SDM;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using SLC_SM_UDAPI_ServiceManagement.Models.Order;
	using SLC_SM_UDAPI_ServiceManagement.Models.Inventory;
	using SLC_SM_UDAPI_ServiceManagement.Helpers.Inventory;

	using ItemTransition = DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Serviceorderitem_Behavior.TransitionsEnum;
	using Cfg = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;

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

			var value = Value<List<JObject>>(Controller.ListServiceOrderItems(OrderId));

			CollectionAssert.AreEqual(new[] { second.Identifier, first.Identifier }, value.Select(v => (string)v["Identifier"]).ToArray());
		}

		[TestMethod]
		public void ListServiceOrderItems_AddsStatusAndSpecificationName()
		{
			var item = AddItem("InProgress");
			item.ServiceInfo.SpecificationId = new Skyline.DataMiner.SDM.SdmObjectReference<ServiceSpecification>(AddSpecification().ToString());
			AddOrder("New", item);

			var value = Value<List<JObject>>(Controller.ListServiceOrderItems(OrderId)).Single();

			Assert.AreEqual("inProgress", (string)value["StatusName"]);
			Assert.AreEqual("Spec", (string)value["ServiceSpecificationName"]);
			Assert.IsNull((string)value["ServiceCategoryName"]);
			Assert.IsNull((string)value["ServiceName"]);
		}

		[TestMethod]
		public void ListServiceOrderItems_NoItems_ReturnsEmpty()
		{
			AddOrder();
			Assert.AreEqual(0, Value<List<JObject>>(Controller.ListServiceOrderItems(OrderId)).Count);
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

		#region Item configuration and initialization

		[TestMethod]
		public void GetServiceOrderItemConfiguration_ItemNotInOrder_Returns404()
		{
			AddOrder();
			Assert.AreEqual(404, StatusCode(Controller.GetServiceOrderItemConfiguration(OrderId, Guid.NewGuid().ToString())));
		}

		[TestMethod]
		public void SaveServiceOrderItemConfiguration_ForeignConfiguration_Returns400WithoutUpdatingItem()
		{
			var item = AddItem();
			AddOrder("New", item);
			var body = new JObject
			{
				["DeleteConfigurationIds"] = new JArray(Guid.NewGuid().ToString()),
			};

			Assert.AreEqual(400, StatusCode(Controller.SaveServiceOrderItemConfiguration(OrderId, item.Identifier, body)));
			Items.Verify(r => r.Update(It.IsAny<ServiceOrderItem>()), Times.Never);
		}

		[TestMethod]
		public void SaveServiceOrderItemConfiguration_EditValue_KeepsValueOptionsAndNewValueUsesTemplate()
		{
			var definition = new Cfg.ConfigurationParameter
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = "Transport",
				Type = DomHelpers.SlcConfigurations.SlcConfigurationsIds.Enums.Type.Text,
				TextOptionsId = new SdmObjectReference<Cfg.TextParameterOptions>("template-text"),
				DiscreteOptionsId = new SdmObjectReference<Cfg.DiscreteParameterOptions>("template-discrete"),
				NumberOptionsId = new SdmObjectReference<Cfg.NumberParameterOptions>("template-number"),
			};
			var existing = new Cfg.ConfigurationParameterValue
			{
				Identifier = Guid.NewGuid().ToString(),
				ConfigurationParameterId = new SdmObjectReference<Cfg.ConfigurationParameter>(definition.Identifier),
				StringValue = "SRT",
				TextOptionsId = new SdmObjectReference<Cfg.TextParameterOptions>("custom-text"),
				DiscreteOptionsId = new SdmObjectReference<Cfg.DiscreteParameterOptions>("custom-discrete"),
				NumberOptionsId = new SdmObjectReference<Cfg.NumberParameterOptions>("custom-number"),
			};
			var configuration = new ServiceOrderItemConfigurationValue
			{
				Identifier = Guid.NewGuid().ToString(),
				ConfigurationParameterValueId = new SdmObjectReference<Cfg.ConfigurationParameterValue>(existing.Identifier),
			};
			var values = new List<Cfg.ConfigurationParameterValue> { existing };
			var item = AddItem();
			item.ServiceInfo.Configurations = new List<SdmObjectReference<ServiceOrderItemConfigurationValue>> { new SdmObjectReference<ServiceOrderItemConfigurationValue>(configuration.Identifier) };
			AddOrder("New", item);
			var catalog = Mock.Get(Helper.ServiceCatalog);
			catalog.SetupGet(api => api.ConfigurationParameters).Returns(InitializationRepository(new List<Cfg.ConfigurationParameter> { definition }).Object);
			catalog.SetupGet(api => api.ConfigurationParameterValues).Returns(InitializationRepository(values).Object);
			catalog.SetupGet(api => api.ProfileDefinitions).Returns(InitializationRepository(new List<Cfg.ProfileDefinition>()).Object);
			catalog.SetupGet(api => api.Profiles).Returns(InitializationRepository(new List<Cfg.Profile>()).Object);
			catalog.SetupGet(api => api.DiscreteParameterOptions).Returns(InitializationRepository(new List<Cfg.DiscreteParameterOptions>()).Object);
			catalog.SetupGet(api => api.DiscreteValues).Returns(InitializationRepository(new List<Cfg.DiscreteValue>()).Object);
			catalog.SetupGet(api => api.NumberParameterOptions).Returns(InitializationRepository(new List<Cfg.NumberParameterOptions>()).Object);
			catalog.SetupGet(api => api.ConfigurationUnits).Returns(InitializationRepository(new List<Cfg.ConfigurationUnit>()).Object);
			Mock.Get(Helper.ServiceOrder).SetupGet(api => api.ServiceOrderItemConfigurationValues)
				.Returns(InitializationRepository(new List<ServiceOrderItemConfigurationValue> { configuration }).Object);
			var body = new JObject
			{
				["Parameters"] = new JArray(
					new JObject { ["ParameterId"] = definition.Identifier, ["ConfigurationId"] = configuration.Identifier, ["ParameterValueId"] = existing.Identifier, ["Value"] = "RTP" },
					new JObject { ["ParameterId"] = definition.Identifier, ["Value"] = "UDP" }),
			};

			Assert.AreEqual(200, StatusCode(Controller.SaveServiceOrderItemConfiguration(OrderId, item.Identifier, body)));

			var edited = values.Single(value => value.Identifier == existing.Identifier);
			Assert.AreEqual("RTP", edited.StringValue);
			Assert.AreEqual("custom-text", edited.TextOptionsId.Identifier);
			Assert.AreEqual("custom-discrete", edited.DiscreteOptionsId.Identifier);
			Assert.AreEqual("custom-number", edited.NumberOptionsId.Identifier);
			var added = values.Single(value => value.Identifier != existing.Identifier);
			Assert.AreEqual("template-text", added.TextOptionsId.Identifier);
			Assert.AreEqual("template-discrete", added.DiscreteOptionsId.Identifier);
			Assert.AreEqual("template-number", added.NumberOptionsId.Identifier);
		}

		[TestMethod]
		public void InitializeServiceFromOrderItem_WithoutSpecification_Returns400()
		{
			var item = AddItem();
			item.ServiceInfo.SpecificationId = null;
			AddOrder("New", item);

			Assert.AreEqual(400, StatusCode(Controller.InitializeServiceFromOrderItem(OrderId, item.Identifier)));
		}

		[TestMethod]
		public void InitializeServiceFromOrderItem_WhenItemNameIsUnique_KeepsOriginalName()
		{
			var specificationId = AddSpecification();
			var item = AddItem();
			item.ServiceInfo.SpecificationId = new Skyline.DataMiner.SDM.SdmObjectReference<ServiceSpecification>(specificationId.ToString());
			AddOrder("New", item);

			var result = Controller.InitializeServiceFromOrderItem(OrderId, item.Identifier);

			Assert.AreEqual(201, StatusCode(result));
			var service = Value<Service>(result);
			Assert.AreEqual("Item", service.Name);
			Assert.IsFalse(String.IsNullOrWhiteSpace(service.ServiceConfigurationId.Identifier));
			Assert.AreEqual(service.ServiceConfigurationId.Identifier, service.ConfigurationVersions.Single().Identifier);
			Mock.Get(Helper.ServiceInventory.ServiceConfigurationVersions).Verify(repository => repository.Create(
				It.Is<ServiceConfigurationVersion>(version => version.Identifier == service.ServiceConfigurationId.Identifier
					&& version.Parameters.Count == 0 && version.Profiles.Count == 0)), Times.Once);
		}

		[TestMethod]
		public void InitializeServiceFromOrderItem_WhenItemNameAlreadyExists_AddsUniqueSuffix()
		{
			var specificationId = AddSpecification();
			var item = AddItem();
			item.ServiceInfo.SpecificationId = new Skyline.DataMiner.SDM.SdmObjectReference<ServiceSpecification>(specificationId.ToString());
			AddOrder("New", item);
			StoredServices.Add(new Service
			{
				Identifier = Guid.NewGuid().ToString(),
				ServiceID = "SERVICE-00001",
				Name = "item",
			});
			StoredServices.Add(new Service
			{
				Identifier = Guid.NewGuid().ToString(),
				ServiceID = "SERVICE-00002",
				Name = "Item (2)",
			});

			var result = Controller.InitializeServiceFromOrderItem(OrderId, item.Identifier);

			Assert.AreEqual(201, StatusCode(result));
			var service = Value<Service>(result);
			Assert.AreEqual("Item (3)", service.Name);
			Assert.AreEqual(service.Identifier, item.ServiceInfo.ServiceId.Identifier);
			Services.Verify(repository => repository.Create(It.Is<Service>(created => created.Name == "Item (3)")), Times.Once);
		}

		[TestMethod]
		public void InitializeServiceFromOrderItem_WithDuplicateAndLargeServiceIds_GeneratesUnusedServiceId()
		{
			var item = AddItem();
			item.ServiceInfo.SpecificationId = new Skyline.DataMiner.SDM.SdmObjectReference<ServiceSpecification>(AddSpecification().ToString());
			AddOrder("New", item);
			foreach (string serviceId in new[] { "SERVICE-845525618", "SERVICE-845525618", "SERVICE-3000000000", "SERVICE-3000000001x" })
			{
				StoredServices.Add(new Service { Identifier = Guid.NewGuid().ToString(), ServiceID = serviceId, Name = Guid.NewGuid().ToString() });
			}

			var result = Controller.InitializeServiceFromOrderItem(OrderId, item.Identifier);

			Assert.AreEqual(201, StatusCode(result));
			Assert.AreEqual("SERVICE-3000000001", Value<Service>(result).ServiceID);
		}


		#endregion Item configuration and initialization

		[DataTestMethod]
		[DataRow(null)]
		[DataRow(true)]
		[DataRow(false)]
		public void InitializeService_CopiesDetailsAndOptionallyIndependentConfiguration(bool? copyConfiguration)
		{
			var item = AddItem();
			item.Description = "Configured service description";
			item.ServiceInfo.SpecificationId = new SdmObjectReference<ServiceSpecification>(AddSpecification().ToString());
			AddOrder("New", item);
			var sourceValue = new Cfg.ConfigurationParameterValue
			{
				Identifier = Guid.NewGuid().ToString(),
				ConfigurationParameterId = new SdmObjectReference<Cfg.ConfigurationParameter>(Guid.NewGuid().ToString()),
				StringValue = "SRT",
				ValueFixed = true,
				Label = "Transport",
			};
			var values = new List<Cfg.ConfigurationParameterValue> { sourceValue };
			var sourceConfiguration = new ServiceOrderItemConfigurationValue
			{
				Identifier = Guid.NewGuid().ToString(),
				ConfigurationParameterValueId = new SdmObjectReference<Cfg.ConfigurationParameterValue>(sourceValue.Identifier),
				Mandatory = true,
			};
			item.ServiceInfo.Configurations = new List<SdmObjectReference<ServiceOrderItemConfigurationValue>> { sourceConfiguration };
			var configurations = new List<ServiceConfigurationValue>();
			var versions = new List<ServiceConfigurationVersion>();
			Mock.Get(Helper.ServiceOrder).SetupGet(api => api.ServiceOrderItemConfigurationValues).Returns(InitializationRepository(new List<ServiceOrderItemConfigurationValue> { sourceConfiguration }).Object);
			Mock.Get(Helper.ServiceCatalog).SetupGet(api => api.ConfigurationParameterValues).Returns(InitializationRepository(values).Object);
			Mock.Get(Helper.ServiceInventory).SetupGet(api => api.ServiceConfigurationValues).Returns(InitializationRepository(configurations).Object);
			Mock.Get(Helper.ServiceInventory).SetupGet(api => api.ServiceConfigurationVersions).Returns(InitializationRepository(versions).Object);

			var result = Controller.InitializeServiceFromOrderItem(OrderId, item.Identifier,
				copyConfiguration.HasValue ? new InitializeServiceRequest { CopyConfiguration = copyConfiguration.Value } : null);

			Assert.AreEqual(201, StatusCode(result));
			var service = Value<Service>(result);
			Assert.AreEqual(item.Description, service.Description);
			Assert.AreEqual(item.StartTime, service.StartTime);
			Assert.AreEqual(item.EndTime, service.EndTime);
			Assert.AreEqual(item.ServiceInfo.SpecificationId.Identifier, service.ServiceSpecificationId.Identifier);
			Assert.AreEqual(service.Identifier, item.ServiceInfo.ServiceId.Identifier);
			if (copyConfiguration != false)
			{
				Assert.AreEqual(1, versions.Count);
				Assert.AreEqual(versions[0].Identifier, service.ServiceConfigurationId.Identifier);
				Assert.AreEqual(versions[0].Identifier, service.ConfigurationVersions.Single().Identifier);
				Assert.AreEqual(configurations.Single().Identifier, versions[0].Parameters.Single().Identifier);
				Assert.IsTrue(configurations[0].Mandatory);
				var copied = values.Single(value => value.Identifier != sourceValue.Identifier);
				Assert.AreEqual(copied.Identifier, configurations[0].ConfigurationParameterId.Identifier);
				Assert.AreEqual("SRT", copied.StringValue);
				Assert.IsTrue(copied.ValueFixed);
				Assert.AreEqual("Transport", copied.Label);
				Assert.AreEqual(sourceValue.ConfigurationParameterId.Identifier, copied.ConfigurationParameterId.Identifier);
				copied.StringValue = "Changed";
				Assert.AreEqual("SRT", sourceValue.StringValue);
				Assert.AreEqual(sourceConfiguration.Identifier, item.ServiceInfo.Configurations.Single().Identifier);
			}
			else
			{
				Assert.AreEqual(0, versions.Count);
				Assert.AreEqual(0, configurations.Count);
				Assert.AreEqual(1, values.Count);
			}

			Assert.AreEqual(200, StatusCode(Controller.InitializeServiceFromOrderItem(OrderId, item.Identifier)));
			Assert.AreEqual(1, StoredServices.Count, "Repeated initialization must not create another service.");
		}

		[TestMethod]
		public void InitializeService_LinkFailure_RemovesCopiedConfigurationAndService()
		{
			var item = AddItem();
			item.ServiceInfo.SpecificationId = new SdmObjectReference<ServiceSpecification>(AddSpecification().ToString());
			AddOrder("New", item);
			var value = new Cfg.ConfigurationParameterValue { Identifier = Guid.NewGuid().ToString(), StringValue = "Original" };
			var configuration = new ServiceOrderItemConfigurationValue
			{
				Identifier = Guid.NewGuid().ToString(),
				ConfigurationParameterValueId = new SdmObjectReference<Cfg.ConfigurationParameterValue>(value.Identifier),
			};
			item.ServiceInfo.Configurations = new List<SdmObjectReference<ServiceOrderItemConfigurationValue>> { configuration };
			var values = new List<Cfg.ConfigurationParameterValue> { value };
			var configurations = new List<ServiceConfigurationValue>();
			var versions = new List<ServiceConfigurationVersion>();
			Mock.Get(Helper.ServiceOrder).SetupGet(api => api.ServiceOrderItemConfigurationValues).Returns(InitializationRepository(new List<ServiceOrderItemConfigurationValue> { configuration }).Object);
			Mock.Get(Helper.ServiceCatalog).SetupGet(api => api.ConfigurationParameterValues).Returns(InitializationRepository(values).Object);
			Mock.Get(Helper.ServiceInventory).SetupGet(api => api.ServiceConfigurationValues).Returns(InitializationRepository(configurations).Object);
			Mock.Get(Helper.ServiceInventory).SetupGet(api => api.ServiceConfigurationVersions).Returns(InitializationRepository(versions).Object);
			Items.Setup(repository => repository.Update(item)).Throws(new InvalidOperationException("Link failed"));

			Assert.AreEqual(500, StatusCode(Controller.InitializeServiceFromOrderItem(OrderId, item.Identifier)));
			Assert.AreEqual(0, StoredServices.Count);
			Assert.AreEqual(0, versions.Count);
			Assert.AreEqual(0, configurations.Count);
			CollectionAssert.AreEqual(new[] { value }, values);
		}

		[TestMethod]
		public void InitializeService_MissingConfiguration_Returns400WithoutCreatingService()
		{
			var item = AddItem();
			item.ServiceInfo.SpecificationId = new SdmObjectReference<ServiceSpecification>(AddSpecification().ToString());
			item.ServiceInfo.Configurations = new List<SdmObjectReference<ServiceOrderItemConfigurationValue>>
			{
				new SdmObjectReference<ServiceOrderItemConfigurationValue>(Guid.NewGuid().ToString()),
			};
			AddOrder("New", item);

			Assert.AreEqual(400, StatusCode(Controller.InitializeServiceFromOrderItem(OrderId, item.Identifier)));
			Services.Verify(repository => repository.Create(It.IsAny<Service>()), Times.Never);
			Items.Verify(repository => repository.Update(It.IsAny<ServiceOrderItem>()), Times.Never);
		}

		[TestMethod]
		public void SaveConfiguration_ReloadAndInitialize_PreservesLabelsAndCreatesActiveVersionWithParameters()
		{
			var item = AddItem();
			item.ServiceInfo.SpecificationId = new SdmObjectReference<ServiceSpecification>(AddSpecification().ToString());
			AddOrder("New", item);
			var definition = new Cfg.ConfigurationParameter
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = "Transport",
				Type = DomHelpers.SlcConfigurations.SlcConfigurationsIds.Enums.Type.Text,
			};
			var values = new List<Cfg.ConfigurationParameterValue>();
			var itemConfigurations = new List<ServiceOrderItemConfigurationValue>();
			var configurations = new List<ServiceConfigurationValue>();
			var versions = new List<ServiceConfigurationVersion>();
			var catalog = Mock.Get(Helper.ServiceCatalog);
			catalog.SetupGet(api => api.ConfigurationParameters).Returns(InitializationRepository(new List<Cfg.ConfigurationParameter> { definition }).Object);
			catalog.SetupGet(api => api.ConfigurationParameterValues).Returns(InitializationRepository(values).Object);
			catalog.SetupGet(api => api.ProfileDefinitions).Returns(InitializationRepository(new List<Cfg.ProfileDefinition>()).Object);
			catalog.SetupGet(api => api.Profiles).Returns(InitializationRepository(new List<Cfg.Profile>()).Object);
			Mock.Get(Helper.ServiceOrder).SetupGet(api => api.ServiceOrderItemConfigurationValues).Returns(InitializationRepository(itemConfigurations).Object);
			Mock.Get(Helper.ServiceInventory).SetupGet(api => api.ServiceConfigurationValues).Returns(InitializationRepository(configurations).Object);
			Mock.Get(Helper.ServiceInventory).SetupGet(api => api.ServiceConfigurationVersions).Returns(InitializationRepository(versions).Object);
			Items.Setup(api => api.Update(It.IsAny<ServiceOrderItem>())).Returns<ServiceOrderItem>(updated =>
			{
				var stored = new ServiceOrderItem
				{
					Identifier = updated.Identifier,
					Name = updated.Name,
					Description = updated.Description,
					ServiceInfo = new ServiceOrderItemServiceInfo
					{
						SpecificationId = updated.ServiceInfo.SpecificationId,
						ServiceCategoryId = updated.ServiceInfo.ServiceCategoryId,
						ServiceId = updated.ServiceInfo.ServiceId,
						Configurations = updated.ServiceInfo.Configurations.ToList(),
					},
				};
				StoredItems[StoredItems.FindIndex(candidate => candidate.Identifier == stored.Identifier)] = stored;
				return stored;
			});
			Services.Setup(api => api.Create(It.IsAny<Service>())).Returns<Service>(created =>
			{
				var stored = new Service
				{
					Identifier = created.Identifier,
					ServiceID = created.ServiceID,
					ServiceConfigurationId = created.ServiceConfigurationId,
					ConfigurationVersions = created.ConfigurationVersions.ToList(),
				};
				StoredServices.Add(stored);
				return stored;
			});

			var body = new JObject
			{
				["Parameters"] = new JArray(
					new JObject { ["ParameterId"] = definition.Identifier, ["Value"] = "SRT", ["Label"] = "Primary", ["IsLinked"] = true },
					new JObject { ["ParameterId"] = definition.Identifier, ["Value"] = "RTP", ["Label"] = "Backup" }),
			};
			Assert.AreEqual(200, StatusCode(Controller.SaveServiceOrderItemConfiguration(OrderId, item.Identifier, body)));
			Assert.AreEqual(2, values.Count);
			Assert.AreEqual(2, itemConfigurations.Count);
			Assert.AreEqual(2, StoredItems.Single().ServiceInfo.Configurations.Count);
			Assert.AreEqual(itemConfigurations[0].Identifier, StoredItems.Single().ServiceInfo.Configurations[0].Identifier);
			var savedRows = Value<List<ConfigurationRecord>>(Controller.GetServiceOrderItemConfiguration(OrderId, item.Identifier))
				.Where(row => row.RecordType == "Parameter").ToList();
			CollectionAssert.AreEquivalent(new[] { "Primary", "Backup" }, savedRows.Select(row => row.ParameterLabel).ToArray());
			Assert.IsTrue(values.Single(value => value.Label == "Primary").IsLinked);
			Assert.AreEqual(2, StoredItems.Single().ServiceInfo.Configurations.Count);

			Assert.AreEqual(201, StatusCode(Controller.InitializeServiceFromOrderItem(OrderId, item.Identifier)));
			var service = StoredServices.Single();
			Assert.AreEqual(versions.Single().Identifier, service.ServiceConfigurationId.Identifier);
			Assert.AreEqual(versions[0].Identifier, service.ConfigurationVersions.Single().Identifier);
			var fullVersion = new FullServiceBuilder(Helper).BuildVersion(versions[0]);
			Assert.AreEqual(2, fullVersion.Parameters.Count);
			CollectionAssert.AreEquivalent(new[] { "Primary", "Backup" }, fullVersion.Parameters.Select(parameter => parameter.ConfigurationParameter.Label).ToArray());
			CollectionAssert.AreEquivalent(new[] { "SRT", "RTP" }, fullVersion.Parameters.Select(parameter => parameter.ConfigurationParameter.StringValue).ToArray());
			Assert.IsFalse(fullVersion.Parameters.Any(parameter => itemConfigurations.Any(source => source.ConfigurationParameterValueId.Identifier == parameter.ConfigurationParameter.Identifier)));
		}

		[DataTestMethod]
		[DataRow("copy")]
		[DataRow("skip")]
		[DataRow("missing")]
		[DataRow("cycle")]
		[DataRow("linkFailure")]
		public void InitializeService_Profiles_CopiesNestedValuesIndependentlyAndCleansUpFailures(string scenario)
		{
			var item = AddItem();
			string specificationId = AddSpecification().ToString();
			item.ServiceInfo.SpecificationId = new SdmObjectReference<ServiceSpecification>(specificationId);
			item.ServiceInfo.Configurations = new List<SdmObjectReference<ServiceOrderItemConfigurationValue>>();
			AddOrder("New", item);
			var value = new Cfg.ConfigurationParameterValue { Identifier = Guid.NewGuid().ToString(), StringValue = "Profile value", ValueFixed = true };
			var nested = new Cfg.Profile
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = "Nested",
				IsReusable = true,
				ConfigurationParameterValues = new List<SdmObjectReference<Cfg.ConfigurationParameterValue>> { value },
			};
			var root = new Cfg.Profile
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = "Root",
				IsReusable = true,
				Profiles = new List<SdmObjectReference<Cfg.Profile>> { nested },
			};
			var specificationProfile = new ServiceSpecificationProfile
			{
				Identifier = Guid.NewGuid().ToString(),
				ProfileId = new SdmObjectReference<Cfg.Profile>(root.Identifier),
				ProfileDefinitionId = new SdmObjectReference<Cfg.ProfileDefinition>(Guid.NewGuid().ToString()),
				MandatoryAtService = true,
			};
			StoredSpecifications.Single(specification => specification.Identifier == specificationId).ConfigurationProfiles =
				new List<SdmObjectReference<ServiceSpecificationProfile>> { specificationProfile };
			var profiles = new List<Cfg.Profile> { root, nested };
			var values = new List<Cfg.ConfigurationParameterValue> { value };
			var serviceProfiles = new List<ServiceProfile>();
			var versions = new List<ServiceConfigurationVersion>();
			var catalog = Mock.Get(Helper.ServiceCatalog);
			catalog.SetupGet(api => api.ServiceSpecificationProfiles).Returns(InitializationRepository(new List<ServiceSpecificationProfile> { specificationProfile }).Object);
			catalog.SetupGet(api => api.Profiles).Returns(InitializationRepository(profiles).Object);
			catalog.SetupGet(api => api.ConfigurationParameterValues).Returns(InitializationRepository(values).Object);
			Mock.Get(Helper.ServiceInventory).SetupGet(api => api.ServiceProfiles).Returns(InitializationRepository(serviceProfiles).Object);
			Mock.Get(Helper.ServiceInventory).SetupGet(api => api.ServiceConfigurationVersions).Returns(InitializationRepository(versions).Object);
			if (scenario == "missing") values.Clear();
			if (scenario == "cycle") nested.Profiles = new List<SdmObjectReference<Cfg.Profile>> { root };
			if (scenario == "linkFailure") Items.Setup(repository => repository.Update(item)).Throws(new InvalidOperationException("Link failed"));

			var result = Controller.InitializeServiceFromOrderItem(OrderId, item.Identifier, new InitializeServiceRequest { CopyConfiguration = scenario != "skip" });

			if (scenario == "copy")
			{
				Assert.AreEqual(201, StatusCode(result));
				var service = Value<Service>(result);
				Assert.AreEqual(versions.Single().Identifier, service.ServiceConfigurationId.Identifier);
				Assert.AreEqual(serviceProfiles.Single().Identifier, versions[0].Profiles.Single().Identifier);
				Assert.IsTrue(serviceProfiles[0].Mandatory);
				Assert.AreEqual(specificationProfile.ProfileDefinitionId.Identifier, serviceProfiles[0].ProfileDefinitionId.Identifier);
				var copiedRoot = profiles.Single(profile => profile.Identifier == serviceProfiles[0].ProfileId.Identifier);
				var copiedNested = profiles.Single(profile => profile.Identifier == copiedRoot.Profiles.Single().Identifier);
				Assert.AreNotEqual(root.Identifier, copiedRoot.Identifier);
				Assert.AreNotEqual(nested.Identifier, copiedNested.Identifier);
				Assert.IsFalse(copiedRoot.IsReusable);
				Assert.IsFalse(copiedNested.IsReusable);
				var copiedValue = values.Single(parameter => parameter.Identifier == copiedNested.ConfigurationParameterValues.Single().Identifier);
				Assert.AreNotEqual(value.Identifier, copiedValue.Identifier);
				Assert.AreEqual(value.StringValue, copiedValue.StringValue);
				Assert.IsTrue(copiedValue.ValueFixed);
				copiedValue.StringValue = "Changed";
				Assert.AreEqual("Profile value", value.StringValue);
			}
			else
			{
				Assert.AreEqual(scenario == "skip" ? 201 : scenario == "linkFailure" ? 500 : 400, StatusCode(result));
				Assert.AreEqual(2, profiles.Count);
				Assert.AreEqual(scenario == "missing" ? 0 : 1, values.Count);
				Assert.AreEqual(0, serviceProfiles.Count);
				Assert.AreEqual(0, versions.Count);
				Assert.AreEqual(scenario == "skip" ? 1 : 0, StoredServices.Count);
			}
		}

		private static Mock<IBulkRepository<T>> InitializationRepository<T>(List<T> store)
			where T : SdmObject<T>
		{
			var repository = new Mock<IBulkRepository<T>>();
			repository.Setup(api => api.Read(It.IsAny<FilterElement<T>>())).Returns<FilterElement<T>>(filter => store.Where(filter.getLambda()).ToList());
			repository.Setup(api => api.Create(It.IsAny<T>())).Callback<T>(store.Add).Returns<T>(value => value);
			repository.Setup(api => api.Delete(It.IsAny<T>())).Callback<T>(value => store.Remove(value));
			repository.Setup(api => api.CreateOrUpdate(It.IsAny<IEnumerable<T>>())).Callback<IEnumerable<T>>(values =>
			{
				foreach (var value in values)
				{
					store.RemoveAll(stored => stored.Identifier == value.Identifier);
					store.Add(value);
				}
			});
			return repository;
		}

		#region POST /serviceOrder/{id}/item

		// ---- POST /serviceOrder/{id}/item ----
		[TestMethod]
		public void AddServiceOrderItem_Valid_CreatesItemLinksOrderAndReturns201()
		{
			var order = AddOrder();
			var request = new CreateServiceOrderItemRequest { Action = "add", ServiceSpecificationId = AddSpecification(), Priority = 3 };

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
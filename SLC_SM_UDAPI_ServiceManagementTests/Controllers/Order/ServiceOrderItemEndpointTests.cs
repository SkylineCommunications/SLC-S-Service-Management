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
		public void InitializeService_MandatoryFlagComesFromSpecificationMandatoryAtService()
		{
			var item = AddItem();
			var parameterId = Guid.NewGuid().ToString();
			var specificationConfiguration = new ServiceSpecificationConfigurationValue
			{
				Identifier = Guid.NewGuid().ToString(),
				ConfigurationParameterId = new SdmObjectReference<Cfg.ConfigurationParameter>(parameterId),
				MandatoryAtService = true,
			};
			item.ServiceInfo.SpecificationId = new SdmObjectReference<ServiceSpecification>(AddSpecification().ToString());
			StoredSpecifications.Last().ConfigurationParameters = new List<SdmObjectReference<ServiceSpecificationConfigurationValue>>
			{
				new SdmObjectReference<ServiceSpecificationConfigurationValue>(specificationConfiguration.Identifier),
			};
			AddOrder("New", item);
			var value = new Cfg.ConfigurationParameterValue
			{
				Identifier = Guid.NewGuid().ToString(),
				ConfigurationParameterId = new SdmObjectReference<Cfg.ConfigurationParameter>(parameterId),
				StringValue = "SRT",
			};
			var itemConfiguration = new ServiceOrderItemConfigurationValue
			{
				Identifier = Guid.NewGuid().ToString(),
				ConfigurationParameterValueId = new SdmObjectReference<Cfg.ConfigurationParameterValue>(value.Identifier),
				Mandatory = false,
			};
			item.ServiceInfo.Configurations = new List<SdmObjectReference<ServiceOrderItemConfigurationValue>> { itemConfiguration };
			var configurations = new List<ServiceConfigurationValue>();
			Mock.Get(Helper.ServiceCatalog).SetupGet(api => api.ServiceSpecificationConfigurationValues)
				.Returns(InitializationRepository(new List<ServiceSpecificationConfigurationValue> { specificationConfiguration }).Object);
			Mock.Get(Helper.ServiceOrder).SetupGet(api => api.ServiceOrderItemConfigurationValues).Returns(InitializationRepository(new List<ServiceOrderItemConfigurationValue> { itemConfiguration }).Object);
			Mock.Get(Helper.ServiceCatalog).SetupGet(api => api.ConfigurationParameterValues).Returns(InitializationRepository(new List<Cfg.ConfigurationParameterValue> { value }).Object);
			Mock.Get(Helper.ServiceInventory).SetupGet(api => api.ServiceConfigurationValues).Returns(InitializationRepository(configurations).Object);
			Mock.Get(Helper.ServiceInventory).SetupGet(api => api.ServiceConfigurationVersions).Returns(InitializationRepository(new List<ServiceConfigurationVersion>()).Object);

			Assert.AreEqual(201, StatusCode(Controller.InitializeServiceFromOrderItem(OrderId, item.Identifier)));
			Assert.IsTrue(configurations.Single().Mandatory);
		}

		[TestMethod]
		public void InitializeService_RepeatedSpecificationParametersKeepMandatoryFlagPerInstance()
		{
			var item = AddItem();
			Guid specificationId = AddSpecification();
			item.ServiceInfo.SpecificationId = new SdmObjectReference<ServiceSpecification>(specificationId.ToString());
			AddOrder("New", item);

			var parameter = new Cfg.ConfigurationParameter { Identifier = Guid.NewGuid().ToString(), Name = "Transport" };
			var mainSpecificationValue = new Cfg.ConfigurationParameterValue
			{
				Identifier = Guid.NewGuid().ToString(),
				Label = "Main",
				ConfigurationParameterId = new SdmObjectReference<Cfg.ConfigurationParameter>(parameter.Identifier),
				StringValue = "SRT",
			};
			var backupSpecificationValue = new Cfg.ConfigurationParameterValue
			{
				Identifier = Guid.NewGuid().ToString(),
				Label = "Backup",
				ConfigurationParameterId = new SdmObjectReference<Cfg.ConfigurationParameter>(parameter.Identifier),
				StringValue = "RTP",
			};
			var mainSpecificationConfiguration = new ServiceSpecificationConfigurationValue
			{
				Identifier = Guid.NewGuid().ToString(),
				ConfigurationParameterId = new SdmObjectReference<Cfg.ConfigurationParameter>(mainSpecificationValue.Identifier),
				MandatoryAtService = true,
			};
			var backupSpecificationConfiguration = new ServiceSpecificationConfigurationValue
			{
				Identifier = Guid.NewGuid().ToString(),
				ConfigurationParameterId = new SdmObjectReference<Cfg.ConfigurationParameter>(backupSpecificationValue.Identifier),
				MandatoryAtService = false,
			};
			StoredSpecifications.Single(specification => specification.Identifier == specificationId.ToString()).ConfigurationParameters =
				new List<SdmObjectReference<ServiceSpecificationConfigurationValue>>
				{
					new SdmObjectReference<ServiceSpecificationConfigurationValue>(mainSpecificationConfiguration.Identifier),
					new SdmObjectReference<ServiceSpecificationConfigurationValue>(backupSpecificationConfiguration.Identifier),
				};

			var mainOrderValue = new Cfg.ConfigurationParameterValue
			{
				Identifier = Guid.NewGuid().ToString(),
				Label = "Main",
				ConfigurationParameterId = new SdmObjectReference<Cfg.ConfigurationParameter>(parameter.Identifier),
				StringValue = "SRT",
			};
			var backupOrderValue = new Cfg.ConfigurationParameterValue
			{
				Identifier = Guid.NewGuid().ToString(),
				Label = "Backup",
				ConfigurationParameterId = new SdmObjectReference<Cfg.ConfigurationParameter>(parameter.Identifier),
				StringValue = "RTP",
			};
			var mainItemConfiguration = new ServiceOrderItemConfigurationValue
			{
				Identifier = Guid.NewGuid().ToString(),
				ConfigurationParameterValueId = new SdmObjectReference<Cfg.ConfigurationParameterValue>(mainOrderValue.Identifier),
				Mandatory = false,
			};
			var backupItemConfiguration = new ServiceOrderItemConfigurationValue
			{
				Identifier = Guid.NewGuid().ToString(),
				ConfigurationParameterValueId = new SdmObjectReference<Cfg.ConfigurationParameterValue>(backupOrderValue.Identifier),
				Mandatory = false,
			};
			item.ServiceInfo.Configurations = new List<SdmObjectReference<ServiceOrderItemConfigurationValue>>
			{
				new SdmObjectReference<ServiceOrderItemConfigurationValue>(mainItemConfiguration.Identifier),
				new SdmObjectReference<ServiceOrderItemConfigurationValue>(backupItemConfiguration.Identifier),
			};

			var specificationConfigurations = new List<ServiceSpecificationConfigurationValue>
			{
				mainSpecificationConfiguration,
				backupSpecificationConfiguration,
			};
			var parameterValues = new List<Cfg.ConfigurationParameterValue>
			{
				mainSpecificationValue,
				backupSpecificationValue,
				mainOrderValue,
				backupOrderValue,
			};
			var itemConfigurations = new List<ServiceOrderItemConfigurationValue> { mainItemConfiguration, backupItemConfiguration };
			var configurations = new List<ServiceConfigurationValue>();
			var versions = new List<ServiceConfigurationVersion>();
			Mock.Get(Helper.ServiceCatalog).SetupGet(api => api.ConfigurationParameters)
				.Returns(InitializationRepository(new List<Cfg.ConfigurationParameter> { parameter }).Object);
			Mock.Get(Helper.ServiceCatalog).SetupGet(api => api.ServiceSpecificationConfigurationValues)
				.Returns(InitializationRepository(specificationConfigurations).Object);
			Mock.Get(Helper.ServiceCatalog).SetupGet(api => api.ConfigurationParameterValues)
				.Returns(InitializationRepository(parameterValues).Object);
			Mock.Get(Helper.ServiceOrder).SetupGet(api => api.ServiceOrderItemConfigurationValues)
				.Returns(InitializationRepository(itemConfigurations).Object);
			Mock.Get(Helper.ServiceInventory).SetupGet(api => api.ServiceConfigurationValues)
				.Returns(InitializationRepository(configurations).Object);
			Mock.Get(Helper.ServiceInventory).SetupGet(api => api.ServiceConfigurationVersions)
				.Returns(InitializationRepository(versions).Object);

			Assert.AreEqual(201, StatusCode(Controller.InitializeServiceFromOrderItem(OrderId, item.Identifier)));

			var fullVersion = new FullServiceBuilder(Helper).BuildVersion(versions.Single());
			var main = fullVersion.Parameters.Single(value => value.ConfigurationParameter.Label == "Main");
			var backup = fullVersion.Parameters.Single(value => value.ConfigurationParameter.Label == "Backup");
			Assert.IsTrue(main.Mandatory);
			Assert.IsFalse(backup.Mandatory);
		}

		[TestMethod]
		public void InitializeService_EmptyMandatoryValue_Returns400NamingParameterWithoutCreatingService()
		{
			var item = AddItem();
			item.ServiceInfo.SpecificationId = new SdmObjectReference<ServiceSpecification>(AddSpecification().ToString());
			AddOrder("New", item);
			AddEmptyMandatoryItemConfiguration(item, "Bitrate");

			var result = Controller.InitializeServiceFromOrderItem(OrderId, item.Identifier);

			Assert.AreEqual(400, StatusCode(result));
			StringAssert.Contains(ErrorDetail(result), "without a value: Bitrate.");
			Services.Verify(repository => repository.Create(It.IsAny<Service>()), Times.Never);
		}

		[TestMethod]
		public void InitializeService_ServiceMandatoryEmptyValue_Returns400WithoutCreatingService()
		{
			var item = AddItem();
			var specification = AddSpecification();
			item.ServiceInfo.SpecificationId = new SdmObjectReference<ServiceSpecification>(specification.ToString());
			AddOrder("New", item);

			var parameter = new Cfg.ConfigurationParameter { Identifier = Guid.NewGuid().ToString(), Name = "Bitrate" };
			var value = new Cfg.ConfigurationParameterValue
			{
				Identifier = Guid.NewGuid().ToString(),
				ConfigurationParameterId = new SdmObjectReference<Cfg.ConfigurationParameter>(parameter.Identifier),
			};
			var specificationConfiguration = new ServiceSpecificationConfigurationValue
			{
				Identifier = Guid.NewGuid().ToString(),
				ConfigurationParameterId = new SdmObjectReference<Cfg.ConfigurationParameter>(parameter.Identifier),
				MandatoryAtService = true,
			};
			var itemConfiguration = new ServiceOrderItemConfigurationValue
			{
				Identifier = Guid.NewGuid().ToString(),
				ConfigurationParameterValueId = new SdmObjectReference<Cfg.ConfigurationParameterValue>(value.Identifier),
				Mandatory = false,
			};
			StoredSpecifications.Single(candidate => candidate.Identifier == specification.ToString()).ConfigurationParameters =
				new List<SdmObjectReference<ServiceSpecificationConfigurationValue>>
				{
					new SdmObjectReference<ServiceSpecificationConfigurationValue>(specificationConfiguration.Identifier),
				};
			item.ServiceInfo.Configurations = new List<SdmObjectReference<ServiceOrderItemConfigurationValue>>
			{
				new SdmObjectReference<ServiceOrderItemConfigurationValue>(itemConfiguration.Identifier),
			};

			Mock.Get(Helper.ServiceCatalog).SetupGet(api => api.ServiceSpecificationConfigurationValues)
				.Returns(InitializationRepository(new List<ServiceSpecificationConfigurationValue> { specificationConfiguration }).Object);
			Mock.Get(Helper.ServiceCatalog).SetupGet(api => api.ConfigurationParameters)
				.Returns(InitializationRepository(new List<Cfg.ConfigurationParameter> { parameter }).Object);
			Mock.Get(Helper.ServiceCatalog).SetupGet(api => api.ConfigurationParameterValues)
				.Returns(InitializationRepository(new List<Cfg.ConfigurationParameterValue> { value }).Object);
			Mock.Get(Helper.ServiceOrder).SetupGet(api => api.ServiceOrderItemConfigurationValues)
				.Returns(InitializationRepository(new List<ServiceOrderItemConfigurationValue> { itemConfiguration }).Object);

			var result = Controller.InitializeServiceFromOrderItem(OrderId, item.Identifier);

			Assert.AreEqual(400, StatusCode(result));
			StringAssert.Contains(ErrorDetail(result), "without a value: Bitrate.");
			Services.Verify(repository => repository.Create(It.IsAny<Service>()), Times.Never);
			Items.Verify(repository => repository.Update(It.IsAny<ServiceOrderItem>()), Times.Never);
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
		[DataRow(false)]
		[DataRow(true)]
		public void InitializeService_DoesNotCopySpecificationProfiles(bool brokenProfile)
		{
			var item = AddItem();
			string specificationId = AddSpecification().ToString();
			item.ServiceInfo.SpecificationId = new SdmObjectReference<ServiceSpecification>(specificationId);
			item.ServiceInfo.Configurations = new List<SdmObjectReference<ServiceOrderItemConfigurationValue>>();
			AddOrder("New", item);
			var value = new Cfg.ConfigurationParameterValue { Identifier = Guid.NewGuid().ToString(), StringValue = "Profile value" };
			var root = new Cfg.Profile
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = "Root",
				ConfigurationParameterValues = new List<SdmObjectReference<Cfg.ConfigurationParameterValue>> { value },
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
			var profiles = new List<Cfg.Profile> { root };
			var values = brokenProfile ? new List<Cfg.ConfigurationParameterValue>() : new List<Cfg.ConfigurationParameterValue> { value };
			var serviceProfiles = new List<ServiceProfile>();
			var versions = new List<ServiceConfigurationVersion>();
			var catalog = Mock.Get(Helper.ServiceCatalog);
			catalog.SetupGet(api => api.ServiceSpecificationProfiles).Returns(InitializationRepository(new List<ServiceSpecificationProfile> { specificationProfile }).Object);
			catalog.SetupGet(api => api.Profiles).Returns(InitializationRepository(profiles).Object);
			catalog.SetupGet(api => api.ConfigurationParameterValues).Returns(InitializationRepository(values).Object);
			Mock.Get(Helper.ServiceInventory).SetupGet(api => api.ServiceProfiles).Returns(InitializationRepository(serviceProfiles).Object);
			Mock.Get(Helper.ServiceInventory).SetupGet(api => api.ServiceConfigurationVersions).Returns(InitializationRepository(versions).Object);

			var result = Controller.InitializeServiceFromOrderItem(OrderId, item.Identifier, new InitializeServiceRequest { CopyConfiguration = true });

			Assert.AreEqual(201, StatusCode(result));
			Assert.AreEqual(versions.Single().Identifier, Value<Service>(result).ServiceConfigurationId.Identifier);
			Assert.AreEqual(0, versions[0].Profiles.Count);
			Assert.AreEqual(0, serviceProfiles.Count);
			Assert.AreEqual(1, profiles.Count);
			Assert.AreEqual(brokenProfile ? 0 : 1, values.Count);
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

		[TestMethod]
		public void AddServiceOrderItem_MandatoryParameterWithoutValue_CreatesEmptyMandatoryConfiguration()
		{
			var order = AddOrder();
			string parameterId = Guid.NewGuid().ToString();
			var values = new List<Cfg.ConfigurationParameterValue>();
			var itemConfigurations = SetupMandatorySpecificationRepositories(values, parameterId, out var specificationId);

			var result = Controller.AddServiceOrderItem(OrderId, new CreateServiceOrderItemRequest { Action = "add", ServiceSpecificationId = specificationId });

			Assert.AreEqual(201, StatusCode(result));
			Assert.AreEqual(1, order.OrderItems.Count);
			Assert.IsTrue(itemConfigurations.Single().Mandatory);
			var created = values.Single();
			Assert.AreEqual(parameterId, created.ConfigurationParameterId.Identifier);
			Assert.IsTrue(String.IsNullOrEmpty(created.StringValue) && created.DoubleValue == null);
		}

		[TestMethod]
		public void AddServiceOrderItem_SpecificationReferencesValueId_CopiesSpecificationValue()
		{
			AddOrder();
			string parameterId = Guid.NewGuid().ToString();
			var specificationValue = new Cfg.ConfigurationParameterValue
			{
				Identifier = Guid.NewGuid().ToString(),
				ConfigurationParameterId = new SdmObjectReference<Cfg.ConfigurationParameter>(parameterId),
				StringValue = "SRT",
			};
			var values = new List<Cfg.ConfigurationParameterValue> { specificationValue };
			var itemConfigurations = SetupMandatorySpecificationRepositories(values, specificationValue.Identifier, out var specificationId);

			var result = Controller.AddServiceOrderItem(OrderId, new CreateServiceOrderItemRequest { Action = "add", ServiceSpecificationId = specificationId });

			Assert.AreEqual(201, StatusCode(result));
			var copied = values.Single(value => value.Identifier != specificationValue.Identifier);
			Assert.AreEqual(itemConfigurations.Single().ConfigurationParameterValueId.Identifier, copied.Identifier);
			Assert.AreEqual(parameterId, copied.ConfigurationParameterId.Identifier);
			Assert.AreEqual("SRT", copied.StringValue);
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
		public void UpdateServiceOrderItem_NewSpecificationWithMandatoryParameterWithoutValue_Returns200()
		{
			var item = AddItem();
			AddOrder("New", item);
			var values = new List<Cfg.ConfigurationParameterValue>();
			var itemConfigurations = SetupMandatorySpecificationRepositories(values, Guid.NewGuid().ToString(), out var specificationId);

			var result = Controller.UpdateServiceOrderItem(OrderId, item.Identifier, new JObject { ["serviceSpecificationId"] = specificationId.ToString() });

			Assert.AreEqual(200, StatusCode(result));
			Assert.IsTrue(itemConfigurations.Single().Mandatory);
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
namespace SLC_SM_UDAPI_ServiceManagement.Tests.Controllers
{
	using System.Collections.Generic;
	using Microsoft.Extensions.Logging;
	using Moq;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using Skyline.DataMiner.SDM;
	using Skyline.DataMiner.Utils.UserDefinedApiToolkit;
	using SLDataGateway.API.Types.Querying;
	using SLC_SM_UDAPI_ServiceManagement.Controllers;
	using Cfg = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;

	using OrderStatus = DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Serviceorder_Behavior.StatusesEnum;
	using OrderTransition = DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Serviceorder_Behavior.TransitionsEnum;
	using ItemStatus = DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Serviceorderitem_Behavior.StatusesEnum;
	using ItemTransition = DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Serviceorderitem_Behavior.TransitionsEnum;

	/// <summary>
	/// Shared fixture: mocks the Service Management API so the controller can be exercised without DataMiner.
	/// </summary>
	public abstract class ServiceOrderControllerTestBase : ControllerTestBase
	{
		protected const string OrderId = "ORDER-000001";

		protected Mock<IServiceOrderRepository> Orders { get; private set; }

		protected Mock<IServiceOrderItemRepository> Items { get; private set; }

		protected Mock<IServiceRepository> Services { get; private set; }

		protected List<ServiceOrder> StoredOrders { get; private set; }

		protected List<ServiceOrderItem> StoredItems { get; private set; }

		protected List<Service> StoredServices { get; private set; }

		protected List<ServiceSpecification> StoredSpecifications { get; private set; }

		protected ServiceOrderController Controller { get; private set; }

		protected IServiceManagementApiHelper Helper { get; private set; }

		[TestInitialize]
		public void BaseInitialize()
		{
			StoredOrders = new List<ServiceOrder>();
			StoredItems = new List<ServiceOrderItem>();
			StoredServices = new List<Service>();

			Orders = new Mock<IServiceOrderRepository>();
			Orders.Setup(r => r.Read(It.IsAny<FilterElement<ServiceOrder>>())).Returns(() => StoredOrders.ToList());
			Orders.Setup(r => r.Read(It.IsAny<IQuery<ServiceOrder>>())).Returns<IQuery<ServiceOrder>>(q => StoredOrders.Where(q.Filter.getLambda()).ToList());
			Orders.Setup(r => r.ReadPaged(It.IsAny<IQuery<ServiceOrder>>(), It.IsAny<int>())).Returns<IQuery<ServiceOrder>, int>((q, size) => OnePage(StoredOrders, q));
			Orders.Setup(r => r.Count(It.IsAny<FilterElement<ServiceOrder>>())).Returns<FilterElement<ServiceOrder>>(f => StoredOrders.Count(f.getLambda()));
			Orders.Setup(r => r.Update(It.IsAny<ServiceOrder>())).Returns<ServiceOrder>(o => o);
			Orders.Setup(r => r.Create(It.IsAny<IEnumerable<ServiceOrder>>())).Returns<IEnumerable<ServiceOrder>>(o => o.ToList());
			Orders.Setup(r => r.TransitionStatus(It.IsAny<ServiceOrder>(), It.IsAny<OrderTransition>())).Returns<ServiceOrder, OrderTransition>((o, t) => o);

			Items = new Mock<IServiceOrderItemRepository>();
			Items.Setup(r => r.Read(It.IsAny<FilterElement<ServiceOrderItem>>())).Returns<FilterElement<ServiceOrderItem>>(f => StoredItems.Where(f.getLambda()).ToList());
			Items.Setup(r => r.ReadPaged(It.IsAny<IQuery<ServiceOrderItem>>(), It.IsAny<int>())).Returns<IQuery<ServiceOrderItem>, int>((q, size) => OnePage(StoredItems, q));
			Items.Setup(r => r.Create(It.IsAny<ServiceOrderItem>())).Returns<ServiceOrderItem>(i => i);
			Items.Setup(r => r.Update(It.IsAny<ServiceOrderItem>())).Returns<ServiceOrderItem>(i => i);
			Items.Setup(r => r.TransitionStatus(It.IsAny<ServiceOrderItem>(), It.IsAny<ItemTransition>())).Returns<ServiceOrderItem, ItemTransition>((i, t) => i);

			var orderApi = new Mock<IServiceOrderApi>();
			orderApi.SetupGet(a => a.ServiceOrders).Returns(Orders.Object);
			orderApi.SetupGet(a => a.ServiceOrderItems).Returns(Items.Object);
			var itemConfigurations = new Mock<IBulkRepository<ServiceOrderItemConfigurationValue>>();
			itemConfigurations.Setup(r => r.Read(It.IsAny<FilterElement<ServiceOrderItemConfigurationValue>>()))
				.Returns(new List<ServiceOrderItemConfigurationValue>());
			itemConfigurations.Setup(r => r.ReadPaged(It.IsAny<IQuery<ServiceOrderItemConfigurationValue>>(), It.IsAny<int>()))
				.Returns<IQuery<ServiceOrderItemConfigurationValue>, int>((q, size) => OnePage(new List<ServiceOrderItemConfigurationValue>(), q));
			orderApi.SetupGet(a => a.ServiceOrderItemConfigurationValues).Returns(itemConfigurations.Object);

			StoredSpecifications = new List<ServiceSpecification>();
			var specifications = new Mock<IBulkRepository<ServiceSpecification>>();
			specifications.Setup(r => r.Read(It.IsAny<FilterElement<ServiceSpecification>>()))
				.Returns<FilterElement<ServiceSpecification>>(f => StoredSpecifications.Where(f.getLambda()).ToList());
			var configurationParameters = new Mock<IBulkRepository<Cfg.ConfigurationParameter>>();
			configurationParameters.Setup(r => r.Read(It.IsAny<FilterElement<Cfg.ConfigurationParameter>>()))
				.Returns(new List<Cfg.ConfigurationParameter>());
			var parameterValues = new Mock<IBulkRepository<Cfg.ConfigurationParameterValue>>();
			parameterValues.Setup(r => r.Read(It.IsAny<FilterElement<Cfg.ConfigurationParameterValue>>()))
				.Returns(new List<Cfg.ConfigurationParameterValue>());

			Services = new Mock<IServiceRepository>();
			Services.Setup(r => r.Read(It.IsAny<FilterElement<Service>>()))
				.Returns<FilterElement<Service>>(f => StoredServices.Where(f.getLambda()).ToList());
			Services.Setup(r => r.ReadPaged(It.IsAny<IQuery<Service>>(), It.IsAny<int>()))
				.Returns<IQuery<Service>, int>((q, size) => OnePage(StoredServices, q));
			Services.Setup(r => r.Create(It.IsAny<Service>()))
				.Callback<Service>(service => StoredServices.Add(service))
				.Returns<Service>(service => service);
			Services.Setup(r => r.Delete(It.IsAny<Service>()))
				.Callback<Service>(service => StoredServices.Remove(service));
			var inventory = new Mock<IServiceInventoryApi>();
			inventory.SetupGet(a => a.Services).Returns(Services.Object);
			var versions = new Mock<IBulkRepository<ServiceConfigurationVersion>>();
			versions.Setup(repository => repository.Create(It.IsAny<ServiceConfigurationVersion>()))
				.Returns<ServiceConfigurationVersion>(version => version);
			inventory.SetupGet(a => a.ServiceConfigurationVersions).Returns(versions.Object);

			var catalog = new Mock<IServiceCatalogApi>();
			catalog.SetupGet(c => c.ServiceSpecifications).Returns(specifications.Object);
			catalog.SetupGet(c => c.ConfigurationParameters).Returns(configurationParameters.Object);
			catalog.SetupGet(c => c.ConfigurationParameterValues).Returns(parameterValues.Object);

			var helper = new Mock<IServiceManagementApiHelper>();
			helper.SetupGet(h => h.ServiceOrder).Returns(orderApi.Object);
			helper.SetupGet(h => h.ServiceInventory).Returns(inventory.Object);
			helper.SetupGet(h => h.ServiceCatalog).Returns(catalog.Object);

			Controller = new ServiceOrderController(
				Mock.Of<ILogger<ServiceOrderController>>(),
				helper.Object,
				Mock.Of<IAccessor<Skyline.DataMiner.Automation.IEngine>>(a => a.Value == Mock.Of<Skyline.DataMiner.Automation.IEngine>()));
			Helper = helper.Object;

			AttachContext(Controller);
		}

		protected static T Value<T>(IApiResult result)
		{
			Assert.IsInstanceOfType(result, typeof(ObjectResult<T>));
			return ((ObjectResult<T>)result).Value;
		}

		protected static OrderStatus OrderStatusFromName(string name)
		{
			return (OrderStatus)Enum.Parse(typeof(OrderStatus), name, true);
		}

		protected static ItemStatus ItemStatusFromName(string name)
		{
			return (ItemStatus)Enum.Parse(typeof(ItemStatus), name, true);
		}

		/// <summary>Returns the first defined "From_To_To" transition so tests don't hard-code the DOM behavior.</summary>
		protected static string[] FirstTransition(Type transitionEnum)
		{
			return Enum.GetNames(transitionEnum)
				.Select(n => n.Split(new[] { "_To_" }, StringSplitOptions.None))
				.First(p => p.Length == 2);
		}

		protected Guid AddSpecification()
		{
			var id = Guid.NewGuid();
			StoredSpecifications.Add(new ServiceSpecification { Identifier = id.ToString(), Name = "Spec" });
			return id;
		}

		protected static Mock<IBulkRepository<T>> InitializationRepository<T>(List<T> store)
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

		/// <summary>Gives the item one mandatory configuration parameter without a value.</summary>
		/// <param name="item">The order item.</param>
		/// <param name="parameterName">Name of the configuration parameter.</param>
		protected void AddEmptyMandatoryItemConfiguration(ServiceOrderItem item, string parameterName)
		{
			var parameter = new Cfg.ConfigurationParameter { Identifier = Guid.NewGuid().ToString(), Name = parameterName };
			var value = new Cfg.ConfigurationParameterValue
			{
				Identifier = Guid.NewGuid().ToString(),
				ConfigurationParameterId = new SdmObjectReference<Cfg.ConfigurationParameter>(parameter.Identifier),
			};
			var configuration = new ServiceOrderItemConfigurationValue
			{
				Identifier = Guid.NewGuid().ToString(),
				ConfigurationParameterValueId = new SdmObjectReference<Cfg.ConfigurationParameterValue>(value.Identifier),
				Mandatory = true,
			};
			item.ServiceInfo.Configurations = new List<SdmObjectReference<ServiceOrderItemConfigurationValue>> { configuration };
			Mock.Get(Helper.ServiceOrder).SetupGet(api => api.ServiceOrderItemConfigurationValues)
				.Returns(InitializationRepository(new List<ServiceOrderItemConfigurationValue> { configuration }).Object);
			Mock.Get(Helper.ServiceCatalog).SetupGet(api => api.ConfigurationParameterValues)
				.Returns(InitializationRepository(new List<Cfg.ConfigurationParameterValue> { value }).Object);
			Mock.Get(Helper.ServiceCatalog).SetupGet(api => api.ConfigurationParameters)
				.Returns(InitializationRepository(new List<Cfg.ConfigurationParameter> { parameter }).Object);
		}

		/// <summary>Sets up a specification with one parameter that is mandatory at service order.</summary>
		/// <param name="values">Backing store of the configuration parameter values.</param>
		/// <param name="referenceId">The parameter value ID or parameter ID the specification configuration refers to.</param>
		/// <param name="specificationId">The created specification ID.</param>
		/// <returns>Backing store of the created order item configuration values.</returns>
		protected List<ServiceOrderItemConfigurationValue> SetupMandatorySpecificationRepositories(List<Cfg.ConfigurationParameterValue> values, string referenceId, out Guid specificationId)
		{
			specificationId = AddSpecification();
			var specificationConfiguration = new ServiceSpecificationConfigurationValue
			{
				Identifier = Guid.NewGuid().ToString(),
				ConfigurationParameterId = new SdmObjectReference<Cfg.ConfigurationParameter>(referenceId),
				MandatoryAtServiceOrder = true,
			};
			StoredSpecifications.Last().ConfigurationParameters = new List<SdmObjectReference<ServiceSpecificationConfigurationValue>>
			{
				new SdmObjectReference<ServiceSpecificationConfigurationValue>(specificationConfiguration.Identifier),
			};

			var itemConfigurations = new List<ServiceOrderItemConfigurationValue>();
			Mock.Get(Helper.ServiceCatalog).SetupGet(api => api.ServiceSpecificationConfigurationValues)
				.Returns(InitializationRepository(new List<ServiceSpecificationConfigurationValue> { specificationConfiguration }).Object);
			Mock.Get(Helper.ServiceCatalog).SetupGet(api => api.ConfigurationParameterValues).Returns(InitializationRepository(values).Object);
			Mock.Get(Helper.ServiceOrder).SetupGet(api => api.ServiceOrderItemConfigurationValues).Returns(InitializationRepository(itemConfigurations).Object);
			return itemConfigurations;
		}

		protected RepeatedParameterScenario SetupRepeatedSpecificationScenario(
			string orderStatus,
			string itemStatus,
			bool mainSpecificationHasDefault,
			bool mainItemPresent,
			bool mainItemHasValue,
			bool backupItemHasValue)
		{
			var item = AddItem(itemStatus);
			Guid specificationId = AddSpecification();
			item.ServiceInfo.SpecificationId = new SdmObjectReference<ServiceSpecification>(specificationId.ToString());
			AddOrder(orderStatus, item);

			var parameter = new Cfg.ConfigurationParameter
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = "Transport",
			};
			var mainSpecificationValue = mainSpecificationHasDefault
				? new Cfg.ConfigurationParameterValue
				{
					Identifier = Guid.NewGuid().ToString(),
					Label = "Main",
					ConfigurationParameterId = new SdmObjectReference<Cfg.ConfigurationParameter>(parameter.Identifier),
					StringValue = "SRT",
				}
				: null;
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
				ConfigurationParameterId = new SdmObjectReference<Cfg.ConfigurationParameter>(
					mainSpecificationValue?.Identifier ?? parameter.Identifier),
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

			var mainOrderValue = mainItemPresent
				? new Cfg.ConfigurationParameterValue
				{
					Identifier = Guid.NewGuid().ToString(),
					Label = "Main",
					ConfigurationParameterId = new SdmObjectReference<Cfg.ConfigurationParameter>(parameter.Identifier),
					StringValue = mainItemHasValue ? "SRT" : null,
				}
				: null;
			var backupOrderValue = new Cfg.ConfigurationParameterValue
			{
				Identifier = Guid.NewGuid().ToString(),
				Label = "Backup",
				ConfigurationParameterId = new SdmObjectReference<Cfg.ConfigurationParameter>(parameter.Identifier),
				StringValue = backupItemHasValue ? "RTP" : null,
			};
			var mainItemConfiguration = mainOrderValue == null
				? null
				: new ServiceOrderItemConfigurationValue
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
			var itemConfigurations = new List<ServiceOrderItemConfigurationValue>();
			var configurationReferences = new List<SdmObjectReference<ServiceOrderItemConfigurationValue>>();
			if (mainItemConfiguration != null)
			{
				itemConfigurations.Add(mainItemConfiguration);
				configurationReferences.Add(new SdmObjectReference<ServiceOrderItemConfigurationValue>(mainItemConfiguration.Identifier));
			}

			itemConfigurations.Add(backupItemConfiguration);
			configurationReferences.Add(new SdmObjectReference<ServiceOrderItemConfigurationValue>(backupItemConfiguration.Identifier));
			item.ServiceInfo.Configurations = configurationReferences;

			var specificationConfigurations = new List<ServiceSpecificationConfigurationValue>
			{
				mainSpecificationConfiguration,
				backupSpecificationConfiguration,
			};
			var parameterValues = new List<Cfg.ConfigurationParameterValue>();
			if (mainSpecificationValue != null)
			{
				parameterValues.Add(mainSpecificationValue);
			}

			parameterValues.Add(backupSpecificationValue);
			if (mainOrderValue != null)
			{
				parameterValues.Add(mainOrderValue);
			}

			parameterValues.Add(backupOrderValue);
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

			return new RepeatedParameterScenario
			{
				Item = item,
				Configurations = configurations,
				Versions = versions,
			};
		}

		protected ServiceOrder AddOrder(string status = "New", params ServiceOrderItem[] items)
		{
			var order = new ServiceOrder
			{
				Identifier = Guid.NewGuid().ToString(),
				OrderId = OrderId,
				Name = "Order",
				OrderItems = items.Select((item, i) => new ServiceOrderEntry { ServiceOrderItemId = item, Priority = i }).ToList(),
			};
			SetStatus(order, OrderStatusFromName(status));
			StoredOrders.Add(order);
			return order;
		}

		protected ServiceOrderItem AddItem(string status = "New", string action = "add")
		{
			var item = new ServiceOrderItem
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = "Item",
				Action = action,
				ServiceInfo = new ServiceOrderItemServiceInfo
				{
					SpecificationId = new Skyline.DataMiner.SDM.SdmObjectReference<ServiceSpecification>(Guid.NewGuid().ToString()),
					Configurations = new List<Skyline.DataMiner.SDM.SdmObjectReference<ServiceOrderItemConfigurationValue>>(),
				},
			};
			SetStatus(item, ItemStatusFromName(status));
			StoredItems.Add(item);
			return item;
		}

		protected sealed class RepeatedParameterScenario
		{
			public ServiceOrderItem Item { get; set; }

			public List<ServiceConfigurationValue> Configurations { get; set; }

			public List<ServiceConfigurationVersion> Versions { get; set; }
		}
	}
}

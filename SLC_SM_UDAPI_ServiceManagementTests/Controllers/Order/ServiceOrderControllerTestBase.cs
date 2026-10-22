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

		[TestInitialize]
		public void BaseInitialize()
		{
			StoredOrders = new List<ServiceOrder>();
			StoredItems = new List<ServiceOrderItem>();
			StoredServices = new List<Service>();

			Orders = new Mock<IServiceOrderRepository>();
			Orders.Setup(r => r.Read(It.IsAny<FilterElement<ServiceOrder>>())).Returns(() => StoredOrders.ToList());
			Orders.Setup(r => r.Update(It.IsAny<ServiceOrder>())).Returns<ServiceOrder>(o => o);
			Orders.Setup(r => r.Create(It.IsAny<IEnumerable<ServiceOrder>>())).Returns<IEnumerable<ServiceOrder>>(o => o.ToList());
			Orders.Setup(r => r.TransitionStatus(It.IsAny<ServiceOrder>(), It.IsAny<OrderTransition>())).Returns<ServiceOrder, OrderTransition>((o, t) => o);

			Items = new Mock<IServiceOrderItemRepository>();
			Items.Setup(r => r.Read(It.IsAny<FilterElement<ServiceOrderItem>>())).Returns(() => StoredItems.ToList());
			Items.Setup(r => r.Create(It.IsAny<ServiceOrderItem>())).Returns<ServiceOrderItem>(i => i);
			Items.Setup(r => r.Update(It.IsAny<ServiceOrderItem>())).Returns<ServiceOrderItem>(i => i);
			Items.Setup(r => r.TransitionStatus(It.IsAny<ServiceOrderItem>(), It.IsAny<ItemTransition>())).Returns<ServiceOrderItem, ItemTransition>((i, t) => i);

			var orderApi = new Mock<IServiceOrderApi>();
			orderApi.SetupGet(a => a.ServiceOrders).Returns(Orders.Object);
			orderApi.SetupGet(a => a.ServiceOrderItems).Returns(Items.Object);
			var itemConfigurations = new Mock<IBulkRepository<ServiceOrderItemConfigurationValue>>();
			itemConfigurations.Setup(r => r.Read(It.IsAny<FilterElement<ServiceOrderItemConfigurationValue>>()))
				.Returns(new List<ServiceOrderItemConfigurationValue>());
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
			Services.Setup(r => r.Create(It.IsAny<Service>()))
				.Callback<Service>(service => StoredServices.Add(service))
				.Returns<Service>(service => service);
			Services.Setup(r => r.Delete(It.IsAny<Service>()))
				.Callback<Service>(service => StoredServices.Remove(service));
			var inventory = new Mock<IServiceInventoryApi>();
			inventory.SetupGet(a => a.Services).Returns(Services.Object);

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
	}
}

namespace SLC_SM_UDAPI_ServiceOrder.Tests.Controllers
{
	using System.Collections.Generic;
	using System.Reflection;
	using DomHelpers.SlcServicemanagement;
	using Microsoft.Extensions.Logging;
	using Moq;
	using Newtonsoft.Json.Linq;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using Skyline.DataMiner.Utils.UserDefinedApiToolkit;
	using SLC_SM_UDAPI_ServiceOrder.Controllers;

	using OrderStatus = DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Serviceorder_Behavior.StatusesEnum;
	using OrderTransition = DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Serviceorder_Behavior.TransitionsEnum;
	using ItemStatus = DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Serviceorderitem_Behavior.StatusesEnum;
	using ItemTransition = DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Serviceorderitem_Behavior.TransitionsEnum;

	/// <summary>
	/// Shared fixture: mocks the Service Management API so the controller can be exercised without DataMiner.
	/// </summary>
	public abstract class ServiceOrderControllerTestBase
	{
		protected const string OrderId = "ORDER-000001";

		protected Mock<IServiceOrderRepository> Orders { get; private set; }

		protected Mock<IServiceOrderItemRepository> Items { get; private set; }

		protected List<ServiceOrder> StoredOrders { get; private set; }

		protected List<ServiceOrderItem> StoredItems { get; private set; }

		protected ServiceOrderController Controller { get; private set; }

		[TestInitialize]
		public void BaseInitialize()
		{
			StoredOrders = new List<ServiceOrder>();
			StoredItems = new List<ServiceOrderItem>();

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

			var helper = new Mock<IServiceManagementApiHelper>();
			helper.SetupGet(h => h.ServiceOrder).Returns(orderApi.Object);

			Controller = new ServiceOrderController(Mock.Of<ILogger<ServiceOrderController>>(), helper.Object);

			// The UDAPI pipeline normally supplies the context; result helpers need a default output converter.
			// The setters are internal to the toolkit, so they are assigned through reflection.
			var context = new ApiContext();
			SetProperty(context, nameof(ApiContext.InputConverters), new List<IInputConverter> { new NewtonsoftConverter() });
			SetProperty(context, nameof(ApiContext.OutputConverters), new List<IOutputConverter> { new NewtonsoftConverter() });
			SetProperty(Controller, nameof(ServiceOrderController.ApiContext), context);
		}

		private static void SetProperty(object target, string name, object value)
		{
			var property = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			Assert.IsNotNull(property, $"Property '{name}' not found; toolkit layout changed.");
			property.SetValue(target, value);
		}

		protected static int StatusCode(IApiResult result)
		{
			Assert.IsInstanceOfType(result, typeof(StatusCodeResult));
			return ((StatusCodeResult)result).StatusCode;
		}

		protected static T Value<T>(IApiResult result)
		{
			Assert.IsInstanceOfType(result, typeof(ObjectResult<T>));
			return ((ObjectResult<T>)result).Value;
		}

		protected static string ErrorDetail(IApiResult result)
		{
			var value = result.GetType().GetProperty("Value").GetValue(result);
			return (string)JObject.FromObject(value)["error"][0]["detail"];
		}

		protected static void SetStatus(object sdmObject, object status)
		{
			var field = sdmObject.GetType().GetField("<Status>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
			Assert.IsNotNull(field, "Status backing field not found; package layout changed.");
			field.SetValue(sdmObject, status);
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

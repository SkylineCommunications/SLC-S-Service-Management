namespace SLC_SM_RT_CreateServiceOrder.TestCases
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;

	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Owns the fixtures of the test and the state shared between its test cases.
	/// </summary>
	internal sealed class CreateServiceOrderTestData
	{
		private readonly IEngine engine;

		public CreateServiceOrderTestData(IEngine engine)
		{
			this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
			Api = engine.GetUserConnection().GetServiceManagementApiHelper("Service Ordering");
		}

		public IServiceManagementApiHelper Api { get; }

		public string SpecificationId { get; private set; } = String.Empty;

		public string OrderId { get; private set; } = String.Empty;

		public string OrderItemId { get; private set; } = String.Empty;

		public string OrderItemName { get; private set; } = String.Empty;

		/// <summary>
		/// Creates the Service Specification the order is built on.
		/// </summary>
		public void CreateSpecification()
		{
			var specification = new Models.ServiceSpecification
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = $"RT_CreateServiceOrder_Spec_{Guid.NewGuid()}",
				Description = "Temporary fixture for regression testing.",
				ServiceItems = new List<Models.ServiceItem> { new Models.ServiceItem() },
				ServiceItemsRelationships = new List<Models.ServiceItemRelationship> { new Models.ServiceItemRelationship() },
			};

			Api.ServiceCatalog.ServiceSpecifications.Create(specification);
			SpecificationId = specification.Identifier;
		}

		/// <summary>
		/// Creates the Service Order Item that references the resolved Service Specification.
		/// </summary>
		public void CreateOrderItem()
		{
			Models.ServiceSpecification specification = ReadSpecification()
				?? throw new InvalidOperationException($"No Service Specification with ID '{SpecificationId}' exists.");

			var orderItem = new Models.ServiceOrderItem
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = $"RT_CreateServiceOrder_OrderItem_{Guid.NewGuid()}",
				ServiceInfo = new Models.ServiceOrderItemServiceInfo
				{
					SpecificationId = specification,
					Configurations = new List<Skyline.DataMiner.SDM.SdmObjectReference<Models.ServiceOrderItemConfigurationValue>>(),
				},
				StartTime = DateTime.UtcNow,
				EndTime = DateTime.UtcNow.AddHours(2),
			};

			Api.ServiceOrder.ServiceOrderItems.Create(orderItem);
			OrderItemId = orderItem.Identifier;
			OrderItemName = orderItem.Name;
		}

		/// <summary>
		/// Creates the Service Order that contains the created Service Order Item.
		/// </summary>
		public void CreateOrder()
		{
			Models.ServiceOrderItem orderItem = Api.ServiceOrder.ServiceOrderItems
				.Read(Models.ServiceOrderItemExposers.Identifier.Equal(OrderItemId))
				.FirstOrDefault()
				?? throw new InvalidOperationException($"No Service Order Item with ID '{OrderItemId}' exists.");

			var order = new Models.ServiceOrder
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = $"RT_CreateServiceOrder_{Guid.NewGuid()}",
				OrderId = $"RT-{Guid.NewGuid()}",
				ContactIds = new List<Guid>(),
				OrderItems = new List<Models.ServiceOrderEntry>
				{
					new Models.ServiceOrderEntry { ServiceOrderItemId = orderItem },
				},
			};

			Api.ServiceOrder.ServiceOrders.Create(new[] { order });
			OrderId = order.Identifier;
		}

		/// <summary>
		/// Reads the created Service Order back from the system.
		/// </summary>
		/// <returns>The order, or <c>null</c> when it was not persisted.</returns>
		public Models.ServiceOrder ReadOrder()
		{
			return Api.ServiceOrder.ServiceOrders
				.Read(Models.ServiceOrderExposers.Identifier.Equal(OrderId))
				.FirstOrDefault();
		}

		/// <summary>
		/// Removes everything this test created, in reverse creation order.
		/// A failing step never blocks the remaining steps.
		/// </summary>
		public void CleanUp()
		{
			TryCleanup(
				"order",
				() =>
				{
					if (String.IsNullOrWhiteSpace(OrderId))
					{
						return;
					}

					Models.ServiceOrder order = ReadOrder();
					if (order != null)
					{
						Api.ServiceOrder.ServiceOrders.Delete(order);
					}
				});

			TryCleanup(
				"order item",
				() =>
				{
					if (String.IsNullOrWhiteSpace(OrderItemId))
					{
						return;
					}

					Models.ServiceOrderItem orderItem = Api.ServiceOrder.ServiceOrderItems
						.Read(Models.ServiceOrderItemExposers.Identifier.Equal(OrderItemId))
						.FirstOrDefault();
					if (orderItem != null)
					{
						Api.ServiceOrder.ServiceOrderItems.Delete(orderItem);
					}
				});

			TryCleanup(
				"specification",
				() =>
				{
					if (String.IsNullOrWhiteSpace(SpecificationId))
					{
						return;
					}

					Models.ServiceSpecification specification = ReadSpecification();
					if (specification != null)
					{
						Api.ServiceCatalog.ServiceSpecifications.Delete(specification);
					}
				});
		}

		private Models.ServiceSpecification ReadSpecification()
		{
			return Api.ServiceCatalog.ServiceSpecifications
				.Read(Models.ServiceSpecificationExposers.Identifier.Equal(SpecificationId))
				.FirstOrDefault();
		}

		private void TryCleanup(string subject, Action action)
		{
			try
			{
				action();
			}
			catch (Exception ex)
			{
				engine.GenerateInformation($"RT_CreateServiceOrder: cleanup failed for {subject}: {ex}");
			}
		}
	}
}

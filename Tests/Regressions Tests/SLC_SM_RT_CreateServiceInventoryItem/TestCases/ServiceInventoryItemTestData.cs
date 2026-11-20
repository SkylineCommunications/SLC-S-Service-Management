namespace SLC_SM_RT_CreateServiceInventoryItem.TestCases
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
	internal sealed class ServiceInventoryItemTestData
	{
		private readonly IEngine engine;

		public ServiceInventoryItemTestData(IEngine engine)
		{
			this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
			Api = engine.GetUserConnection().GetServiceManagementApiHelper("Service Inventory");
		}

		public IServiceManagementApiHelper Api { get; }

		public string SpecificationId { get; private set; } = String.Empty;

		public string OrderId { get; private set; } = String.Empty;

		public string OrderItemId { get; private set; } = String.Empty;

		/// <summary>
		/// Gets the name the script under test is expected to give to the created service.
		/// </summary>
		public string ExpectedServiceName { get; private set; } = String.Empty;

		/// <summary>
		/// Gets the identifier of the service created by the script under test.
		/// </summary>
		public string CreatedServiceId { get; private set; } = String.Empty;

		public void CreateFixtures()
		{
			var specification = new Models.ServiceSpecification
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = $"RT_CreateServiceInventoryItem_Spec_{Guid.NewGuid()}",
				Description = "Temporary fixture for regression testing.",
				ServiceItems = new List<Models.ServiceItem> { new Models.ServiceItem() },
				ServiceItemsRelationships = new List<Models.ServiceItemRelationship> { new Models.ServiceItemRelationship() },
			};

			Api.ServiceCatalog.ServiceSpecifications.Create(specification);
			SpecificationId = specification.Identifier;

			Models.ServiceSpecification persistedSpecification = Api.ServiceCatalog.ServiceSpecifications
				.Read(Models.ServiceSpecificationExposers.Identifier.Equal(SpecificationId))
				.FirstOrDefault()
				?? throw new InvalidOperationException($"No Service Specification with ID '{SpecificationId}' exists.");

			var orderItem = new Models.ServiceOrderItem
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = $"RT_CreateServiceInventoryItem_OrderItem_{Guid.NewGuid()}",
				ServiceInfo = new Models.ServiceOrderItemServiceInfo
				{
					SpecificationId = persistedSpecification,
					Configurations = new List<Skyline.DataMiner.SDM.SdmObjectReference<Models.ServiceOrderItemConfigurationValue>>(),
				},
				StartTime = DateTime.UtcNow,
				EndTime = DateTime.UtcNow.AddHours(2),
			};

			Api.ServiceOrder.ServiceOrderItems.Create(orderItem);
			OrderItemId = orderItem.Identifier;
			ExpectedServiceName = orderItem.Name;

			var order = new Models.ServiceOrder
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = $"RT_CreateServiceInventoryItem_Order_{Guid.NewGuid()}",
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
		/// Reads back the order item and returns the service the script under test linked to it.
		/// </summary>
		/// <returns>The linked service, or <c>null</c> when no service was linked.</returns>
		public Models.Service ReadLinkedService()
		{
			Models.ServiceOrderItem persistedOrderItem = Api.ServiceOrder.ServiceOrderItems
				.Read(Models.ServiceOrderItemExposers.Identifier.Equal(OrderItemId))
				.FirstOrDefault();

			if (persistedOrderItem?.ServiceInfo == null)
			{
				return null;
			}

			string serviceIdentifier = persistedOrderItem.ServiceInfo.ServiceId.Identifier;
			if (String.IsNullOrWhiteSpace(serviceIdentifier))
			{
				return null;
			}

			CreatedServiceId = serviceIdentifier;

			return Api.ServiceInventory.Services
				.Read(Models.ServiceExposers.Identifier.Equal(serviceIdentifier))
				.FirstOrDefault();
		}

		/// <summary>
		/// Removes everything this test created, in reverse creation order.
		/// A failing step never blocks the remaining steps.
		/// </summary>
		public void CleanUp()
		{
			TryCleanup(
				"service",
				() =>
				{
					if (String.IsNullOrWhiteSpace(CreatedServiceId))
					{
						ReadLinkedService();
					}

					if (String.IsNullOrWhiteSpace(CreatedServiceId))
					{
						return;
					}

					Models.Service service = Api.ServiceInventory.Services
						.Read(Models.ServiceExposers.Identifier.Equal(CreatedServiceId))
						.FirstOrDefault();

					if (service != null)
					{
						Api.ServiceInventory.Services.Delete(service);
					}
				});

			TryCleanup(
				"order",
				() =>
				{
					if (String.IsNullOrWhiteSpace(OrderId))
					{
						return;
					}

					Models.ServiceOrder order = Api.ServiceOrder.ServiceOrders
						.Read(Models.ServiceOrderExposers.Identifier.Equal(OrderId))
						.FirstOrDefault();

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

					Models.ServiceSpecification specification = Api.ServiceCatalog.ServiceSpecifications
						.Read(Models.ServiceSpecificationExposers.Identifier.Equal(SpecificationId))
						.FirstOrDefault();

					if (specification != null)
					{
						Api.ServiceCatalog.ServiceSpecifications.Delete(specification);
					}
				});
		}

		private void TryCleanup(string subject, Action action)
		{
			try
			{
				action();
			}
			catch (Exception ex)
			{
				engine.GenerateInformation($"RT_CreateServiceInventoryItem: cleanup failed for {subject}: {ex}");
			}
		}
	}
}

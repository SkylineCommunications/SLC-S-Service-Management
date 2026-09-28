namespace SLC_SM_RT_DynamicDeleteService.TestCases
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using DomHelpers.SlcServicemanagement;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;

	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Owns the fixtures of the test and the state shared between its test cases.
	/// A service is materialised through "SLC_SM_Create Service Inventory Item", which copies the
	/// service items and relationships of the specification onto the created service.
	/// </summary>
	internal sealed class DynamicDeleteServiceTestData
	{
		private const string CreateServiceInventoryItemScript = "SLC_SM_Create Service Inventory Item";

		private readonly IEngine engine;

		public DynamicDeleteServiceTestData(IEngine engine)
		{
			this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
			Api = engine.GetUserConnection().GetServiceManagementApiHelper("Service Inventory");
		}

		public IServiceManagementApiHelper Api { get; }

		public string SpecificationId { get; private set; } = String.Empty;

		public string OrderId { get; private set; } = String.Empty;

		public string OrderItemId { get; private set; } = String.Empty;

		public string ServiceId { get; private set; } = String.Empty;

		public void CreateFixtures()
		{
			CreateSpecification();
			CreateOrderFixtures();
			MaterialiseService();
		}

		/// <summary>
		/// Reads the service under test back from the system.
		/// </summary>
		/// <returns>The service, or <c>null</c> when it no longer exists.</returns>
		public Models.Service ReadService()
		{
			if (String.IsNullOrWhiteSpace(ServiceId))
			{
				return null;
			}

			return Api.ServiceInventory.Services
				.Read(Models.ServiceExposers.Identifier.Equal(ServiceId))
				.FirstOrDefault();
		}

		/// <summary>
		/// Reads the service and fails the calling test case when it no longer exists.
		/// </summary>
		/// <returns>The service under test.</returns>
		public Models.Service ReadServiceOrThrow()
		{
			return ReadService() ?? throw new InvalidOperationException($"Service '{ServiceId}' was not found.");
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
					Models.Service service = ReadService();
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

		private static Models.ServiceItem NewServiceItem(int id, string label)
		{
			return new Models.ServiceItem
			{
				ServiceItemID = id,
				Label = label,
				Type = SlcServicemanagementIds.Enums.ServiceitemtypesEnum.Service,
				DefinitionReference = $"RT-{id}",
			};
		}

		private static Models.ServiceItemRelationship NewRelationship(string parent, string child)
		{
			return new Models.ServiceItemRelationship
			{
				Id = Guid.NewGuid().ToString(),
				ParentServiceItem = parent,
				ParentServiceItemInterfaceId = String.Empty,
				ChildServiceItem = child,
				ChildServiceItemInterfaceId = String.Empty,
			};
		}

		private void CreateSpecification()
		{
			var specification = new Models.ServiceSpecification
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = $"RT_DynamicDeleteService_Spec_{Guid.NewGuid()}",
				Description = "Fixture for the dynamic delete regression test on a service.",
				ServiceItems = new List<Models.ServiceItem>
				{
					NewServiceItem(0, "RT Node A"),
					NewServiceItem(1, "RT Node B"),
					NewServiceItem(2, "RT Node C"),
				},
				ServiceItemsRelationships = new List<Models.ServiceItemRelationship>
				{
					NewRelationship("0", "1"),
					NewRelationship("1", "2"),
				},
			};

			Api.ServiceCatalog.ServiceSpecifications.Create(specification);
			SpecificationId = specification.Identifier;
		}

		private void CreateOrderFixtures()
		{
			Models.ServiceSpecification persistedSpecification = Api.ServiceCatalog.ServiceSpecifications
				.Read(Models.ServiceSpecificationExposers.Identifier.Equal(SpecificationId))
				.FirstOrDefault()
				?? throw new InvalidOperationException($"No Service Specification with ID '{SpecificationId}' exists.");

			var orderItem = new Models.ServiceOrderItem
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = $"RT_DynamicDeleteService_OrderItem_{Guid.NewGuid()}",
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

			var order = new Models.ServiceOrder
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = $"RT_DynamicDeleteService_Order_{Guid.NewGuid()}",
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

		private void MaterialiseService()
		{
			SubScriptOptions subScript = engine.PrepareSubScript(CreateServiceInventoryItemScript);
			subScript.Synchronous = true;
			subScript.ExtendedErrorInfo = true;
			subScript.SelectScriptParam("DOM ID", OrderItemId);
			subScript.SelectScriptParam("Action", "AddItemSilent");
			subScript.StartScript();

			if (subScript.HadError)
			{
				throw new InvalidOperationException(
					$"Could not materialise the service fixture:{Environment.NewLine}{String.Join(Environment.NewLine, subScript.GetErrorMessages() ?? Enumerable.Empty<string>())}");
			}

			Models.ServiceOrderItem persistedOrderItem = Api.ServiceOrder.ServiceOrderItems
				.Read(Models.ServiceOrderItemExposers.Identifier.Equal(OrderItemId))
				.FirstOrDefault();

			if (persistedOrderItem?.ServiceInfo == null)
			{
				throw new InvalidOperationException("The service order item fixture has no service info.");
			}

			ServiceId = persistedOrderItem.ServiceInfo.ServiceId.Identifier ?? String.Empty;

			if (String.IsNullOrWhiteSpace(ServiceId))
			{
				throw new InvalidOperationException("No service was linked to the service order item fixture.");
			}

			Models.Service service = ReadServiceOrThrow();

			if (service.ServiceItems == null || service.ServiceItems.Count != 3)
			{
				throw new InvalidOperationException(
					$"Expected the service fixture to have 3 service items, got {service.ServiceItems?.Count ?? 0}.");
			}

			if (service.ServiceItemsRelationships == null || service.ServiceItemsRelationships.Count != 2)
			{
				throw new InvalidOperationException(
					$"Expected the service fixture to have 2 service item relationships, got {service.ServiceItemsRelationships?.Count ?? 0}.");
			}
		}

		private void TryCleanup(string subject, Action action)
		{
			try
			{
				action();
			}
			catch (Exception ex)
			{
				engine.GenerateInformation($"RT_DynamicDeleteService: cleanup failed for {subject}: {ex}");
			}
		}
	}
}

namespace SLC_SM_RT_ServiceStateTransition.TestCases
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
	internal sealed class ServiceStateTransitionTestData
	{
		private const string CreateServiceInventoryScript = "SLC_SM_Create Service Inventory Item";

		private readonly IEngine engine;

		public ServiceStateTransitionTestData(IEngine engine)
		{
			this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
			InventoryApi = engine.GetUserConnection().GetServiceManagementApiHelper("Service Inventory");
			OrderingApi = engine.GetUserConnection().GetServiceManagementApiHelper("Service Ordering");
		}

		public IServiceManagementApiHelper InventoryApi { get; }

		public IServiceManagementApiHelper OrderingApi { get; }

		public string SpecificationId { get; private set; } = String.Empty;

		public string OrderId { get; private set; } = String.Empty;

		public string OrderItemId { get; private set; } = String.Empty;

		public string ServiceId { get; private set; } = String.Empty;

		public void CreateFixtures()
		{
			var specification = new Models.ServiceSpecification
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = $"RT_ServiceStateTransition_Spec_{Guid.NewGuid()}",
				Description = "Temporary fixture for regression testing.",
				ServiceItems = new List<Models.ServiceItem> { new Models.ServiceItem() },
				ServiceItemsRelationships = new List<Models.ServiceItemRelationship> { new Models.ServiceItemRelationship() },
			};

			OrderingApi.ServiceCatalog.ServiceSpecifications.Create(specification);
			SpecificationId = specification.Identifier;

			Models.ServiceSpecification persistedSpecification = OrderingApi.ServiceCatalog.ServiceSpecifications
				.Read(Models.ServiceSpecificationExposers.Identifier.Equal(SpecificationId))
				.FirstOrDefault()
				?? throw new InvalidOperationException($"No Service Specification with ID '{SpecificationId}' exists.");

			var orderItem = new Models.ServiceOrderItem
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = $"RT_ServiceStateTransition_OrderItem_{Guid.NewGuid()}",
				ServiceInfo = new Models.ServiceOrderItemServiceInfo
				{
					SpecificationId = persistedSpecification,
					Configurations = new List<Skyline.DataMiner.SDM.SdmObjectReference<Models.ServiceOrderItemConfigurationValue>>(),
				},
				StartTime = DateTime.UtcNow,
				EndTime = DateTime.UtcNow.AddHours(2),
			};

			OrderingApi.ServiceOrder.ServiceOrderItems.Create(orderItem);
			OrderItemId = orderItem.Identifier;

			var order = new Models.ServiceOrder
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = $"RT_ServiceStateTransition_Order_{Guid.NewGuid()}",
				OrderId = $"RT-{Guid.NewGuid()}",
				ContactIds = new List<Guid>(),
				OrderItems = new List<Models.ServiceOrderEntry>
				{
					new Models.ServiceOrderEntry { ServiceOrderItemId = orderItem },
				},
			};

			OrderingApi.ServiceOrder.ServiceOrders.Create(new[] { order });
			OrderId = order.Identifier;

			MaterialiseService();
		}

		/// <summary>
		/// Runs the state transition script under test for the service fixture.
		/// </summary>
		/// <param name="previousState">The state the service is currently in.</param>
		/// <param name="nextState">The state the service should move to.</param>
		public void InvokeStateTransition(string previousState, string nextState)
		{
			SubScriptOptions subScript = engine.PrepareSubScript(Script.ScriptUnderTest);
			subScript.Synchronous = true;
			subScript.ExtendedErrorInfo = true;
			subScript.SelectScriptParam("ServiceReference", ServiceId);
			subScript.SelectScriptParam("PreviousState", previousState);
			subScript.SelectScriptParam("NextState", nextState);
			subScript.StartScript();

			if (subScript.HadError)
			{
				throw new InvalidOperationException(
					$"Transition '{previousState}' -> '{nextState}' failed:{Environment.NewLine}{String.Join(Environment.NewLine, subScript.GetErrorMessages() ?? Enumerable.Empty<string>())}");
			}
		}

		/// <summary>
		/// Reads the service under test back from the system.
		/// </summary>
		/// <returns>The service.</returns>
		public Models.Service ReadService()
		{
			return InventoryApi.ServiceInventory.Services
				.Read(Models.ServiceExposers.Identifier.Equal(ServiceId))
				.FirstOrDefault()
				?? throw new InvalidOperationException($"Service '{ServiceId}' was not found.");
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
					if (String.IsNullOrWhiteSpace(ServiceId))
					{
						return;
					}

					Models.Service service = InventoryApi.ServiceInventory.Services
						.Read(Models.ServiceExposers.Identifier.Equal(ServiceId))
						.FirstOrDefault();
					if (service != null)
					{
						InventoryApi.ServiceInventory.Services.Delete(service);
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

					Models.ServiceOrder order = OrderingApi.ServiceOrder.ServiceOrders
						.Read(Models.ServiceOrderExposers.Identifier.Equal(OrderId))
						.FirstOrDefault();
					if (order != null)
					{
						OrderingApi.ServiceOrder.ServiceOrders.Delete(order);
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

					Models.ServiceOrderItem orderItem = OrderingApi.ServiceOrder.ServiceOrderItems
						.Read(Models.ServiceOrderItemExposers.Identifier.Equal(OrderItemId))
						.FirstOrDefault();
					if (orderItem != null)
					{
						OrderingApi.ServiceOrder.ServiceOrderItems.Delete(orderItem);
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

					Models.ServiceSpecification specification = OrderingApi.ServiceCatalog.ServiceSpecifications
						.Read(Models.ServiceSpecificationExposers.Identifier.Equal(SpecificationId))
						.FirstOrDefault();
					if (specification != null)
					{
						OrderingApi.ServiceCatalog.ServiceSpecifications.Delete(specification);
					}
				});
		}

		private void MaterialiseService()
		{
			SubScriptOptions createScript = engine.PrepareSubScript(CreateServiceInventoryScript);
			createScript.Synchronous = true;
			createScript.ExtendedErrorInfo = true;
			createScript.SelectScriptParam("DOM ID", OrderItemId);
			createScript.SelectScriptParam("Action", "AddItemSilent");
			createScript.StartScript();

			if (createScript.HadError)
			{
				throw new InvalidOperationException(
					$"Could not materialise the service fixture:{Environment.NewLine}{String.Join(Environment.NewLine, createScript.GetErrorMessages() ?? Enumerable.Empty<string>())}");
			}

			Models.ServiceOrderItem persistedOrderItem = OrderingApi.ServiceOrder.ServiceOrderItems
				.Read(Models.ServiceOrderItemExposers.Identifier.Equal(OrderItemId))
				.FirstOrDefault();

			if (persistedOrderItem?.ServiceInfo == null)
			{
				throw new InvalidOperationException("The service order item fixture has no service info.");
			}

			ServiceId = persistedOrderItem.ServiceInfo.ServiceId.Identifier;

			if (String.IsNullOrWhiteSpace(ServiceId))
			{
				throw new InvalidOperationException("No linked service was created on the order item.");
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
				engine.GenerateInformation($"RT_ServiceStateTransition: cleanup failed for {subject}: {ex}");
			}
		}
	}
}

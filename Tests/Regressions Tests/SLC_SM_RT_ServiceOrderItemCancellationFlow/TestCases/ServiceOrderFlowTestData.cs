namespace SLC_SM_RT_ServiceOrderItemCancellationFlow.TestCases
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
	internal sealed class ServiceOrderFlowTestData
	{
		private const string OrderItemTransitionScript = "ServiceOrderItem_StateTranstitions";
		private const string OrderTransitionScript = "ServiceOrder_StateTranstitions";

		private readonly IEngine engine;

		public ServiceOrderFlowTestData(IEngine engine)
		{
			this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
			Api = engine.GetUserConnection().GetServiceManagementApiHelper("Service Ordering");
		}

		public IServiceManagementApiHelper Api { get; }

		public string SpecificationId { get; private set; } = String.Empty;

		public string OrderId { get; private set; } = String.Empty;

		public string OrderItemId { get; private set; } = String.Empty;

		public void CreateFixtures()
		{
			var specification = new Models.ServiceSpecification
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = $"RT_OrderItemCancellation_Spec_{Guid.NewGuid()}",
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
				Name = $"RT_OrderItemCancellation_OrderItem_{Guid.NewGuid()}",
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
				Name = $"RT_OrderItemCancellation_Order_{Guid.NewGuid()}",
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
		/// Runs the service order item state transition script.
		/// </summary>
		/// <param name="previousState">The state the order item is currently in.</param>
		/// <param name="nextState">The state the order item should move to.</param>
		public void InvokeOrderItemTransition(string previousState, string nextState)
		{
			SubScriptOptions subScript = engine.PrepareSubScript(OrderItemTransitionScript);
			subScript.Synchronous = true;
			subScript.ExtendedErrorInfo = true;
			subScript.SelectScriptParam("Id", OrderItemId);
			subScript.SelectScriptParam("PreviousState", previousState);
			subScript.SelectScriptParam("NextState", nextState);
			subScript.StartScript();

			if (subScript.HadError)
			{
				throw new InvalidOperationException(
					$"Order item transition '{previousState}' -> '{nextState}' failed:{Environment.NewLine}{String.Join(Environment.NewLine, subScript.GetErrorMessages() ?? Enumerable.Empty<string>())}");
			}
		}

		/// <summary>
		/// Runs the service order state transition script.
		/// </summary>
		/// <param name="previousState">The state the order is currently in.</param>
		/// <param name="nextState">The state the order should move to.</param>
		public void InvokeOrderTransition(string previousState, string nextState)
		{
			SubScriptOptions subScript = engine.PrepareSubScript(OrderTransitionScript);
			subScript.Synchronous = true;
			subScript.ExtendedErrorInfo = true;
			subScript.SelectScriptParam("ServiceOrderReference", OrderId);
			subScript.SelectScriptParam("PreviousState", previousState);
			subScript.SelectScriptParam("NextState", nextState);
			subScript.StartScript();

			if (subScript.HadError)
			{
				throw new InvalidOperationException(
					$"Order transition '{previousState}' -> '{nextState}' failed:{Environment.NewLine}{String.Join(Environment.NewLine, subScript.GetErrorMessages() ?? Enumerable.Empty<string>())}");
			}
		}

		/// <summary>
		/// Reads the service order item under test back from the system.
		/// </summary>
		/// <returns>The service order item.</returns>
		public Models.ServiceOrderItem ReadOrderItem()
		{
			return Api.ServiceOrder.ServiceOrderItems
				.Read(Models.ServiceOrderItemExposers.Identifier.Equal(OrderItemId))
				.FirstOrDefault()
				?? throw new InvalidOperationException($"Service order item '{OrderItemId}' was not found.");
		}

		/// <summary>
		/// Reads the service order under test back from the system.
		/// </summary>
		/// <returns>The service order.</returns>
		public Models.ServiceOrder ReadOrder()
		{
			return Api.ServiceOrder.ServiceOrders
				.Read(Models.ServiceOrderExposers.Identifier.Equal(OrderId))
				.FirstOrDefault()
				?? throw new InvalidOperationException($"Service order '{OrderId}' was not found.");
		}

		/// <summary>
		/// Removes everything this test created, in reverse creation order.
		/// A failing step never blocks the remaining steps.
		/// </summary>
		public void CleanUp()
		{
			TryCleanup(
				"linked service",
				() =>
				{
					if (String.IsNullOrWhiteSpace(OrderItemId))
					{
						return;
					}

					Models.ServiceOrderItem orderItem = Api.ServiceOrder.ServiceOrderItems
						.Read(Models.ServiceOrderItemExposers.Identifier.Equal(OrderItemId))
						.FirstOrDefault();

					if (orderItem?.ServiceInfo == null)
					{
						return;
					}

					string serviceId = orderItem.ServiceInfo.ServiceId.Identifier;
					if (String.IsNullOrWhiteSpace(serviceId))
					{
						return;
					}

					Models.Service service = Api.ServiceInventory.Services
						.Read(Models.ServiceExposers.Identifier.Equal(serviceId))
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
				engine.GenerateInformation($"RT_OrderItemCancellation: cleanup failed for {subject}: {ex}");
			}
		}
	}
}

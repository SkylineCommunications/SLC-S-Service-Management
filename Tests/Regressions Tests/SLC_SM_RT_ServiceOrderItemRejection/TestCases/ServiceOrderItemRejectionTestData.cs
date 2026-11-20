namespace SLC_SM_RT_ServiceOrderItemRejection.TestCases
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
	internal sealed class ServiceOrderItemRejectionTestData
	{
		private const string LogPrefix = "RT_OrderItemRejection";

		private readonly IEngine engine;

		public ServiceOrderItemRejectionTestData(IEngine engine)
		{
			this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
			Api = engine.GetUserConnection().GetServiceManagementApiHelper("Service Ordering");
		}

		public IServiceManagementApiHelper Api { get; }

		public string SpecificationId { get; private set; } = String.Empty;

		/// <summary>
		/// Gets the order with two order items, rejected from New and from Acknowledged respectively.
		/// </summary>
		public OrderFixture OrderA { get; } = new OrderFixture("A");

		/// <summary>
		/// Gets the order with a single order item that is acknowledged and then rejected.
		/// </summary>
		public OrderFixture OrderB { get; } = new OrderFixture("B");

		private IEnumerable<OrderFixture> Orders => new[] { OrderA, OrderB };

		public void CreateFixtures()
		{
			var specification = new Models.ServiceSpecification
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = $"RT_OrderItemRejection_Spec_{Guid.NewGuid()}",
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

			CreateOrder(OrderA, persistedSpecification, 2);
			CreateOrder(OrderB, persistedSpecification, 1);
		}

		/// <summary>
		/// Runs the service order item state transition script under test for the given order item.
		/// </summary>
		/// <param name="orderItemId">The order item to transition.</param>
		/// <param name="previousState">The state the order item is currently in.</param>
		/// <param name="nextState">The state the order item should move to.</param>
		/// <returns>The outcome of the script run.</returns>
		public SubScriptOutcome InvokeOrderItemTransition(string orderItemId, string previousState, string nextState)
		{
			SubScriptOptions subScript = engine.PrepareSubScript(Script.ScriptUnderTest);
			subScript.Synchronous = true;
			subScript.ExtendedErrorInfo = true;

			// Every parameter declared by the script under test must be selected, it throws on missing parameters.
			subScript.SelectScriptParam("Id", orderItemId);
			subScript.SelectScriptParam("PreviousState", previousState);
			subScript.SelectScriptParam("NextState", nextState);
			subScript.SelectScriptParam("Skip Confirmation", Boolean.TrueString);
			subScript.StartScript();

			return new SubScriptOutcome(subScript.HadError, subScript.GetErrorMessages());
		}

		public Models.ServiceOrder ReadOrder(OrderFixture fixture)
		{
			return Api.ServiceOrder.ServiceOrders
				.Read(Models.ServiceOrderExposers.Identifier.Equal(fixture.OrderId))
				.FirstOrDefault()
				?? throw new InvalidOperationException($"Service order {fixture.Label} '{fixture.OrderId}' was not found.");
		}

		public Models.ServiceOrderItem ReadOrderItem(string orderItemId)
		{
			return Api.ServiceOrder.ServiceOrderItems
				.Read(Models.ServiceOrderItemExposers.Identifier.Equal(orderItemId))
				.FirstOrDefault()
				?? throw new InvalidOperationException($"Service order item '{orderItemId}' was not found.");
		}

		/// <summary>
		/// Reads the current status of the order and of each of its order items.
		/// </summary>
		/// <param name="fixture">The order to read.</param>
		/// <returns>The statuses, keyed by "Order" for the order and by order item ID for the items.</returns>
		public Dictionary<string, string> ReadStatuses(OrderFixture fixture)
		{
			var statuses = new Dictionary<string, string>
			{
				{ "Order", ReadOrder(fixture).Status.ToString() },
			};

			foreach (string orderItemId in fixture.OrderItemIds)
			{
				statuses[orderItemId] = ReadOrderItem(orderItemId).Status.ToString();
			}

			return statuses;
		}

		/// <summary>
		/// Removes everything this test created, in reverse creation order.
		/// A failing step never blocks the remaining steps.
		/// </summary>
		public void CleanUp()
		{
			foreach (OrderFixture fixture in Orders)
			{
				CleanUpOrder(fixture);
			}

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

		private void CreateOrder(OrderFixture fixture, Models.ServiceSpecification specification, int orderItemCount)
		{
			var entries = new List<Models.ServiceOrderEntry>();
			for (int i = 1; i <= orderItemCount; i++)
			{
				var orderItem = new Models.ServiceOrderItem
				{
					Identifier = Guid.NewGuid().ToString(),
					Name = $"RT_OrderItemRejection_{fixture.Label}_OrderItem{i}_{Guid.NewGuid()}",
					ServiceInfo = new Models.ServiceOrderItemServiceInfo
					{
						SpecificationId = specification,
						Configurations = new List<Skyline.DataMiner.SDM.SdmObjectReference<Models.ServiceOrderItemConfigurationValue>>(),
					},
					StartTime = DateTime.UtcNow,
					EndTime = DateTime.UtcNow.AddHours(2),
				};

				Api.ServiceOrder.ServiceOrderItems.Create(orderItem);
				fixture.OrderItemIds.Add(orderItem.Identifier);
				fixture.OrderItemNames.Add(orderItem.Name);
				entries.Add(new Models.ServiceOrderEntry { ServiceOrderItemId = orderItem });
			}

			var order = new Models.ServiceOrder
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = $"RT_OrderItemRejection_Order{fixture.Label}_{Guid.NewGuid()}",
				OrderId = $"RT-{Guid.NewGuid()}",
				ContactIds = new List<Guid>(),
				OrderItems = entries,
			};

			Api.ServiceOrder.ServiceOrders.Create(new[] { order });
			fixture.OrderId = order.Identifier;
		}

		private void CleanUpOrder(OrderFixture fixture)
		{
			// Rejecting an order item retires a linked service; delete any service linked to (or named after) the items.
			TryCleanup(
				$"services of order {fixture.Label}",
				() =>
				{
					var serviceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
					foreach (string orderItemId in fixture.OrderItemIds)
					{
						Models.ServiceOrderItem orderItem = Api.ServiceOrder.ServiceOrderItems
							.Read(Models.ServiceOrderItemExposers.Identifier.Equal(orderItemId))
							.FirstOrDefault();
						string serviceId = orderItem?.ServiceInfo == null ? null : orderItem.ServiceInfo.ServiceId.Identifier;
						if (!String.IsNullOrWhiteSpace(serviceId))
						{
							serviceIds.Add(serviceId);
						}
					}

					var itemNames = new HashSet<string>(fixture.OrderItemNames, StringComparer.Ordinal);
					foreach (Models.Service service in Api.ServiceInventory.Services.Read(new TRUEFilterElement<Models.Service>()))
					{
						if (serviceIds.Contains(service.Identifier) || itemNames.Contains(service.Description ?? String.Empty))
						{
							Api.ServiceInventory.Services.Delete(service);
						}
					}
				});

			TryCleanup(
				$"order {fixture.Label}",
				() =>
				{
					if (String.IsNullOrWhiteSpace(fixture.OrderId))
					{
						return;
					}

					Models.ServiceOrder order = Api.ServiceOrder.ServiceOrders
						.Read(Models.ServiceOrderExposers.Identifier.Equal(fixture.OrderId))
						.FirstOrDefault();
					if (order != null)
					{
						Api.ServiceOrder.ServiceOrders.Delete(order);
					}
				});

			foreach (string orderItemId in fixture.OrderItemIds)
			{
				TryCleanup(
					$"order item '{orderItemId}' of order {fixture.Label}",
					() =>
					{
						Models.ServiceOrderItem orderItem = Api.ServiceOrder.ServiceOrderItems
							.Read(Models.ServiceOrderItemExposers.Identifier.Equal(orderItemId))
							.FirstOrDefault();
						if (orderItem != null)
						{
							Api.ServiceOrder.ServiceOrderItems.Delete(orderItem);
						}
					});
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
				engine.GenerateInformation($"{LogPrefix}: cleanup failed for {subject}: {ex}");
			}
		}
	}
}

namespace SLC_SM_RT_ServiceOrderItemStateTransition.TestCases
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
	internal sealed class ServiceOrderItemStateTransitionTestData
	{
		private readonly IEngine engine;

		public ServiceOrderItemStateTransitionTestData(IEngine engine)
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
				Name = $"RT_OrderItemStateTransition_Spec_{Guid.NewGuid()}",
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
				Name = $"RT_OrderItemStateTransition_OrderItem_{Guid.NewGuid()}",
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
				Name = $"RT_OrderItemStateTransition_Order_{Guid.NewGuid()}",
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
		/// Runs the state transition script under test for the Service Order Item fixture.
		/// </summary>
		/// <param name="previousState">The state the order item is currently in.</param>
		/// <param name="nextState">The state the order item should move to.</param>
		public void InvokeStateTransition(string previousState, string nextState)
		{
			SubScriptOptions subScript = engine.PrepareSubScript(Script.ScriptUnderTest);
			subScript.Synchronous = true;
			subScript.ExtendedErrorInfo = true;
			subScript.SelectScriptParam("Id", OrderItemId);
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
		/// Reads the Service Order Item under test back from the system.
		/// </summary>
		/// <returns>The order item.</returns>
		public Models.ServiceOrderItem ReadOrderItem()
		{
			return Api.ServiceOrder.ServiceOrderItems
				.Read(Models.ServiceOrderItemExposers.Identifier.Equal(OrderItemId))
				.FirstOrDefault()
				?? throw new InvalidOperationException("Test order item was not found.");
		}

		/// <summary>
		/// Reads the parent Service Order back from the system.
		/// </summary>
		/// <returns>The order.</returns>
		public Models.ServiceOrder ReadOrder()
		{
			return Api.ServiceOrder.ServiceOrders
				.Read(Models.ServiceOrderExposers.Identifier.Equal(OrderId))
				.FirstOrDefault()
				?? throw new InvalidOperationException("Parent order was not found.");
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
				engine.GenerateInformation($"RT_ServiceOrderItemStateTransition: cleanup failed for {subject}: {ex}");
			}
		}
	}
}

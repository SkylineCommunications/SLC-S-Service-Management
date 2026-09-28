namespace SLC_SM_RT_InvalidServiceTransition.TestCases
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
	internal sealed class InvalidServiceTransitionTestData
	{
		private const string CreateServiceInventoryItemScript = "SLC_SM_Create Service Inventory Item";

		private readonly IEngine engine;

		public InvalidServiceTransitionTestData(IEngine engine)
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
			var specification = new Models.ServiceSpecification
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = $"RT_InvalidServiceTransition_Spec_{Guid.NewGuid()}",
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
				Name = $"RT_InvalidServiceTransition_OrderItem_{Guid.NewGuid()}",
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
				Name = $"RT_InvalidServiceTransition_Order_{Guid.NewGuid()}",
				OrderId = $"RT-{Guid.NewGuid()}",
				ContactIds = new List<Guid>(),
				OrderItems = new List<Models.ServiceOrderEntry>
				{
					new Models.ServiceOrderEntry { ServiceOrderItemId = orderItem },
				},
			};

			Api.ServiceOrder.ServiceOrders.Create(new[] { order });
			OrderId = order.Identifier;

			MaterialiseService();
		}

		/// <summary>
		/// Runs the state transition script without failing the test when the script reports an error.
		/// The script under test swallows its exceptions into an error dialog, so the outcome of the
		/// script is informational only and the real assertion is done on the state of the service.
		/// </summary>
		/// <param name="previousState">The state passed as previous state.</param>
		/// <param name="nextState">The state passed as next state.</param>
		/// <returns><c>true</c> when the script reported an error.</returns>
		public bool TryInvokeStateTransition(string previousState, string nextState)
		{
			SubScriptOptions subScript = engine.PrepareSubScript(Script.ScriptUnderTest);
			subScript.Synchronous = true;
			subScript.ExtendedErrorInfo = true;
			subScript.SelectScriptParam("ServiceReference", ServiceId);
			subScript.SelectScriptParam("PreviousState", previousState);
			subScript.SelectScriptParam("NextState", nextState);

			try
			{
				subScript.StartScript();
			}
			catch (ScriptAbortException)
			{
				throw;
			}
			catch (Exception ex)
			{
				engine.GenerateInformation($"RT_InvalidServiceTransition: the transition script threw: {ex.Message}");
				return true;
			}

			return subScript.HadError;
		}

		/// <summary>
		/// Reads the service under test back from the system.
		/// </summary>
		/// <returns>The service.</returns>
		public Models.Service ReadService()
		{
			return Api.ServiceInventory.Services
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

					Models.Service service = Api.ServiceInventory.Services
						.Read(Models.ServiceExposers.Identifier.Equal(ServiceId))
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

			ServiceId = persistedOrderItem.ServiceInfo.ServiceId.Identifier;

			if (String.IsNullOrWhiteSpace(ServiceId))
			{
				throw new InvalidOperationException("No service was linked to the service order item fixture.");
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
				engine.GenerateInformation($"RT_InvalidServiceTransition: cleanup failed for {subject}: {ex}");
			}
		}
	}
}

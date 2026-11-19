namespace SLC_SM_RT_ServiceOrderItem_Edit.TestCases
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using Skyline.DataMiner.SDM;

	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Owns the fixtures of the test and the state shared between its test cases.
	/// </summary>
	internal sealed class ServiceOrderItemEditTestData
	{
		private readonly IEngine engine;

		public ServiceOrderItemEditTestData(IEngine engine)
		{
			this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
			Api = engine.GetUserConnection().GetServiceManagementApiHelper("Service Ordering");
		}

		public IServiceManagementApiHelper Api { get; }

		public string SpecificationId { get; private set; } = String.Empty;

		public string OrderItemId { get; private set; } = String.Empty;

		public string EditedName { get; private set; } = String.Empty;

		/// <summary>
		/// Creates the Service Specification and the Service Order Item that is edited.
		/// </summary>
		public void CreateFixtures()
		{
			var specification = new Models.ServiceSpecification
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = $"RT_OrderItemEdit_Spec_{Guid.NewGuid()}",
				Description = "Temporary fixture for regression testing.",
				ServiceItems = new List<Models.ServiceItem> { new Models.ServiceItem() },
				ServiceItemsRelationships = new List<Models.ServiceItemRelationship> { new Models.ServiceItemRelationship() },
			};

			Api.ServiceCatalog.ServiceSpecifications.Create(specification);
			SpecificationId = specification.Identifier;

			Models.ServiceSpecification persistedSpecification = ReadSpecification()
				?? throw new InvalidOperationException($"No Service Specification with ID '{SpecificationId}' exists.");

			var orderItem = new Models.ServiceOrderItem
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = $"RT_OrderItemEdit_{Guid.NewGuid()}",
				ServiceInfo = new Models.ServiceOrderItemServiceInfo
				{
					SpecificationId = persistedSpecification,
					Configurations = new List<SdmObjectReference<Models.ServiceOrderItemConfigurationValue>>(),
				},
				StartTime = DateTime.UtcNow,
				EndTime = DateTime.UtcNow.AddHours(1),
			};

			Api.ServiceOrder.ServiceOrderItems.Create(orderItem);
			OrderItemId = orderItem.Identifier;
		}

		/// <summary>
		/// Renames the order item and moves its end time.
		/// </summary>
		public void EditOrderItem()
		{
			Models.ServiceOrderItem orderItem = ReadOrderItem(OrderItemId)
				?? throw new InvalidOperationException("Order item to edit was not found.");

			EditedName = $"RT_OrderItemEdit_Edited_{Guid.NewGuid()}";
			orderItem.Name = EditedName;
			orderItem.EndTime = DateTime.UtcNow.AddHours(3);
			Api.ServiceOrder.ServiceOrderItems.Update(orderItem);
		}

		/// <summary>
		/// Reads the Service Specification back from the system.
		/// </summary>
		/// <returns>The specification, or <c>null</c> when it does not exist.</returns>
		public Models.ServiceSpecification ReadSpecification()
		{
			return Api.ServiceCatalog.ServiceSpecifications
				.Read(Models.ServiceSpecificationExposers.Identifier.Equal(SpecificationId))
				.FirstOrDefault();
		}

		/// <summary>
		/// Reads a Service Order Item back from the system.
		/// </summary>
		/// <param name="identifier">The identifier of the order item.</param>
		/// <returns>The order item, or <c>null</c> when it does not exist.</returns>
		public Models.ServiceOrderItem ReadOrderItem(string identifier)
		{
			if (String.IsNullOrWhiteSpace(identifier))
			{
				return null;
			}

			return Api.ServiceOrder.ServiceOrderItems
				.Read(Models.ServiceOrderItemExposers.Identifier.Equal(identifier))
				.FirstOrDefault();
		}

		/// <summary>
		/// Removes everything this test created, in reverse creation order.
		/// A failing step never blocks the remaining steps.
		/// </summary>
		public void CleanUp()
		{
			TryCleanup("order item", () => DeleteOrderItem(OrderItemId));

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

		private void DeleteOrderItem(string identifier)
		{
			Models.ServiceOrderItem orderItem = ReadOrderItem(identifier);
			if (orderItem != null)
			{
				Api.ServiceOrder.ServiceOrderItems.Delete(orderItem);
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
				engine.GenerateInformation($"RT_ServiceOrderItem_Edit: cleanup failed for {subject}: {ex}");
			}
		}
	}
}

namespace SLC_SM_RT_ServiceOrderEdit.TestCases
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using DomHelpers.SlcServicemanagement;

	using Newtonsoft.Json;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.PeopleAndOrganization;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;

	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using CategoryModel = Skyline.DataMiner.ProjectApi.ServiceManagement.API.PeopleAndOrganization.Models.Category;
	using OrganizationModel = Skyline.DataMiner.ProjectApi.ServiceManagement.API.PeopleAndOrganization.Models.Organization;

	/// <summary>
	/// Owns the fixtures of the test and the state shared between its test cases.
	/// </summary>
	internal sealed class ServiceOrderEditTestData
	{
		private const int OrderItemCount = 2;
		private const string LogPrefix = "RT_ServiceOrderEdit";
		private const string OrganizationsModuleId = "(slc)people_organizations";

		private readonly IEngine engine;
		private readonly DataHelperOrganization organizationHelper;
		private readonly DataHelperCategory categoryHelper;

		public ServiceOrderEditTestData(IEngine engine)
		{
			this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
			Api = engine.GetUserConnection().GetServiceManagementApiHelper("Service Ordering");
			organizationHelper = new DataHelperOrganization(engine.GetUserConnection());
			categoryHelper = new DataHelperCategory(engine.GetUserConnection());
			OrganizationName = $"RT_OrderEdit_Organization_{Guid.NewGuid()}";
			OrganizationCategoryName = $"RT_OrderEdit_OrgCategory_{Guid.NewGuid()}";
		}

		public IServiceManagementApiHelper Api { get; }

		public string SpecificationId { get; private set; } = String.Empty;

		public string OrderId { get; private set; } = String.Empty;

		public List<string> OrderItemIds { get; } = new List<string>();

		/// <summary>
		/// Gets the name of the organization fixture the edit assigns to the order.
		/// </summary>
		public string OrganizationName { get; }

		public Guid OrganizationId { get; private set; }

		public string OrganizationCategoryName { get; }

		public Guid OrganizationCategoryId { get; private set; }

		/// <summary>
		/// Gets a value indicating whether the People &amp; Organizations DOM module is installed. It is a separate app, so a clean system may not have it.
		/// </summary>
		public bool OrganizationsAvailable { get; private set; }

		/// <summary>
		/// Gets the state of the order items right after they were created, keyed by order item ID.
		/// </summary>
		public Dictionary<string, string> InitialOrderItemStates { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		/// <summary>
		/// Formats a UTC time as ISO 8601 with the given UTC offset, as the script under test requires.
		/// </summary>
		/// <param name="utc">The UTC time.</param>
		/// <param name="offset">The offset to express the time in.</param>
		/// <returns>The formatted time.</returns>
		public static string ToIso8601(DateTime utc, TimeSpan offset)
		{
			var value = new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToOffset(offset);
			return offset == TimeSpan.Zero
				? value.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture)
				: value.ToString("yyyy-MM-ddTHH:mm:sszzz", System.Globalization.CultureInfo.InvariantCulture);
		}

		public void CreateFixtures()
		{
			OrganizationsAvailable = IsOrganizationsModuleAvailable();
			if (!OrganizationsAvailable)
			{
				engine.GenerateInformation($"{LogPrefix}: DOM module '{OrganizationsModuleId}' is not installed (People & Organizations is a separate app); the organization part of the edit is skipped.");
			}
			else
			{
				CreateOrganizationFixture();
			}

			var specification = new Models.ServiceSpecification
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = $"RT_OrderEdit_Spec_{Guid.NewGuid()}",
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

			var entries = new List<Models.ServiceOrderEntry>();
			for (int i = 1; i <= OrderItemCount; i++)
			{
				var orderItem = new Models.ServiceOrderItem
				{
					Identifier = Guid.NewGuid().ToString(),
					Name = $"RT_OrderEdit_OrderItem{i}_{Guid.NewGuid()}",
					ServiceInfo = new Models.ServiceOrderItemServiceInfo
					{
						SpecificationId = persistedSpecification,
						Configurations = new List<Skyline.DataMiner.SDM.SdmObjectReference<Models.ServiceOrderItemConfigurationValue>>(),
					},
					StartTime = DateTime.UtcNow,
					EndTime = DateTime.UtcNow.AddHours(2),
				};

				Api.ServiceOrder.ServiceOrderItems.Create(orderItem);
				OrderItemIds.Add(orderItem.Identifier);
				entries.Add(new Models.ServiceOrderEntry { ServiceOrderItemId = orderItem });
			}

			var order = new Models.ServiceOrder
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = $"RT_OrderEdit_Order_{Guid.NewGuid()}",
				OrderId = $"RT-{Guid.NewGuid()}",
				Description = "Initial description",
				ExternalID = $"RT-EXT-INITIAL-{Guid.NewGuid()}",
				Priority = SlcServicemanagementIds.Enums.ServiceOrderPriorityEnum.Low,
				ContactIds = new List<Guid>(),
				OrderItems = entries,
			};

			Api.ServiceOrder.ServiceOrders.Create(new[] { order });
			OrderId = order.Identifier;

			foreach (string orderItemId in OrderItemIds)
			{
				InitialOrderItemStates[orderItemId] = DescribeOrderItem(ReadOrderItem(orderItemId));
			}
		}

		/// <summary>
		/// Runs the script under test in API mode to edit the order fixture.
		/// </summary>
		/// <param name="payload">The edit request.</param>
		/// <returns>The outcome of the script run.</returns>
		public SubScriptOutcome InvokeEdit(OrderEditPayload payload)
		{
			SubScriptOptions subScript = engine.PrepareSubScript(Script.ScriptUnderTest);
			subScript.Synchronous = true;
			subScript.ExtendedErrorInfo = true;

			// Every parameter declared by the script under test must be selected, it throws on missing parameters.
			subScript.SelectScriptParam("DOM ID", OrderId);
			subScript.SelectScriptParam("Action", "Edit");
			subScript.SelectScriptParam("Payload", JsonConvert.SerializeObject(payload));
			subScript.StartScript();

			return new SubScriptOutcome(subScript.HadError, subScript.GetErrorMessages());
		}

		public OrderSnapshot ReadOrderSnapshot()
		{
			Models.ServiceOrder order = Api.ServiceOrder.ServiceOrders
				.Read(Models.ServiceOrderExposers.Identifier.Equal(OrderId))
				.FirstOrDefault()
				?? throw new InvalidOperationException($"Service order '{OrderId}' was not found.");
			return OrderSnapshot.From(order);
		}

		public Models.ServiceOrderItem ReadOrderItem(string orderItemId)
		{
			return Api.ServiceOrder.ServiceOrderItems
				.Read(Models.ServiceOrderItemExposers.Identifier.Equal(orderItemId))
				.FirstOrDefault();
		}

		/// <summary>
		/// Describes the fields of an order item that an order-level edit must not change.
		/// </summary>
		/// <param name="orderItem">The order item, or null when it no longer exists.</param>
		/// <returns>A comparable description.</returns>
		public string DescribeOrderItem(Models.ServiceOrderItem orderItem)
		{
			if (orderItem == null)
			{
				return "<missing>";
			}

			string specificationId = orderItem.ServiceInfo == null ? "<null>" : orderItem.ServiceInfo.SpecificationId.Identifier;
			return $"Name='{orderItem.Name}', Status='{orderItem.Status}', Specification='{specificationId}'";
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

			foreach (string orderItemId in OrderItemIds)
			{
				TryCleanup(
					$"order item '{orderItemId}'",
					() =>
					{
						Models.ServiceOrderItem orderItem = ReadOrderItem(orderItemId);
						if (orderItem != null)
						{
							Api.ServiceOrder.ServiceOrderItems.Delete(orderItem);
						}
					});
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

			if (!OrganizationsAvailable)
			{
				return;
			}

			TryCleanup(
				$"organization '{OrganizationName}'",
				() =>
				{
					List<OrganizationModel> organizations = organizationHelper.Read()
						.Where(o => o.ID == OrganizationId || String.Equals(o.Name, OrganizationName, StringComparison.Ordinal))
						.ToList();
					if (organizations.Count > 0 && !organizationHelper.TryDelete(organizations))
					{
						throw new InvalidOperationException($"Deleting {organizations.Count} organization(s) was refused.");
					}
				});

			TryCleanup(
				$"organization category '{OrganizationCategoryName}'",
				() =>
				{
					if (OrganizationCategoryId != Guid.Empty
						&& !categoryHelper.TryDelete(new[] { new CategoryModel { ID = OrganizationCategoryId, Name = OrganizationCategoryName } }))
					{
						throw new InvalidOperationException($"Deleting organization category '{OrganizationCategoryName}' was refused.");
					}
				});
		}

		private void CreateOrganizationFixture()
		{
			// An organization requires a category (mandatory field in SlcPeople_Organizations).
			OrganizationCategoryId = categoryHelper.CreateOrUpdate(new CategoryModel { Name = OrganizationCategoryName });
			if (OrganizationCategoryId == Guid.Empty)
			{
				throw new InvalidOperationException($"Creating organization category '{OrganizationCategoryName}' did not return an ID.");
			}

			OrganizationId = organizationHelper.CreateOrUpdate(new OrganizationModel { Name = OrganizationName, CategoryId = OrganizationCategoryId });
			if (OrganizationId == Guid.Empty)
			{
				throw new InvalidOperationException($"Creating organization '{OrganizationName}' did not return an ID.");
			}
		}

		private bool IsOrganizationsModuleAvailable()
		{
			try
			{
				organizationHelper.Read();
				return true;
			}
			catch (Exception ex) when (ex.ToString().IndexOf("No settings were found for a module", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return false;
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

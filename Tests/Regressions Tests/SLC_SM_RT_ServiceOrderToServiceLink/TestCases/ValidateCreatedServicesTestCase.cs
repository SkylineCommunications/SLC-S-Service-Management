namespace SLC_SM_RT_ServiceOrderToServiceLink.TestCases
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using DomHelpers.SlcServicemanagement;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Verifies every order item of the order links to its own service that was created from the order item.
	/// A service has no reference to the order item, so the link back is verified through the order item reference
	/// (exactly one order item links to the service) and through the order item name the product copies to the service.
	/// </summary>
	internal sealed class ValidateCreatedServicesTestCase : TestCase
	{
		private static readonly TimeSpan TimeTolerance = TimeSpan.FromSeconds(1);

		private readonly ServiceOrderToServiceLinkTestData testData;
		private readonly string expectedServiceStatus;
		private readonly string nameSuffix;

		public ValidateCreatedServicesTestCase(ServiceOrderToServiceLinkTestData testData, string nameSuffix)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
			this.nameSuffix = nameSuffix ?? String.Empty;

			// A service is created in its initial lifecycle state; the order lifecycle never moves it.
			expectedServiceStatus = SlcServicemanagementIds.Behaviors.Service_Behavior.StatusesEnum.New.ToString();
		}

		/// <inheritdoc/>
		public override string Name => $"Order{testData.Order.Label}_ItemsLinkToCreatedServices{nameSuffix}";

		public override void Execute(IEngine engine)
		{
			OrderFixture fixture = testData.Order;
			var problems = new List<string>();
			var serviceIdsByOrderItem = new Dictionary<string, string>();

			for (int i = 0; i < fixture.OrderItemIds.Count; i++)
			{
				string orderItemId = fixture.OrderItemIds[i];
				string orderItemName = fixture.OrderItemNames[i];
				Models.ServiceOrderItem orderItem = testData.ReadOrderItem(orderItemId);

				List<Models.Service> servicesNamedAfterItem = testData.ReadServicesCreatedFor(orderItemName);
				Models.Service service = testData.ReadLinkedService(orderItem);
				string linkedServiceId = orderItem.ServiceInfo == null ? null : orderItem.ServiceInfo.ServiceId.Identifier;

				if (service == null)
				{
					problems.Add(String.IsNullOrWhiteSpace(linkedServiceId)
						? $"order item '{orderItemId}' (status '{orderItem.Status}') links to no service; services created for it: [{Describe(servicesNamedAfterItem)}]"
						: $"order item '{orderItemId}' links to service '{linkedServiceId}', which does not exist; services created for it: [{Describe(servicesNamedAfterItem)}]");
					continue;
				}

				serviceIdsByOrderItem[orderItemId] = service.Identifier;
				ValidateService(orderItem, service, problems);

				if (testData.FirstLinkedServiceIds.TryGetValue(orderItemId, out string firstServiceId)
					&& !String.Equals(firstServiceId, service.Identifier, StringComparison.OrdinalIgnoreCase))
				{
					problems.Add($"order item '{orderItemId}' links to service '{service.Identifier}', expected the originally created service '{firstServiceId}'");
				}

				if (servicesNamedAfterItem.Count != 1)
				{
					problems.Add($"expected exactly 1 service created for order item '{orderItemId}', found {servicesNamedAfterItem.Count}: [{Describe(servicesNamedAfterItem)}]");
				}
			}

			problems.AddRange(serviceIdsByOrderItem
				.GroupBy(pair => pair.Value, StringComparer.OrdinalIgnoreCase)
				.Where(group => group.Count() > 1)
				.Select(group => $"service '{group.Key}' is linked to more than one order item: [{String.Join(", ", group.Select(pair => pair.Key))}]"));

			if (testData.FirstLinkedServiceIds.Count == 0 && problems.Count == 0)
			{
				foreach (KeyValuePair<string, string> pair in serviceIdsByOrderItem)
				{
					testData.FirstLinkedServiceIds[pair.Key] = pair.Value;
				}
			}

			TestTools.Assert(
				problems.Count == 0,
				$"Order {fixture.Label} '{fixture.OrderId}' items are not linked to correctly created services: {String.Join("; ", problems)}.");
		}

		public override void PerformCleanup(IEngine engine)
		{
			// Cleanup of every fixture, including the created services, is owned by the test data.
		}

		private static string Describe(IEnumerable<Models.Service> services)
		{
			return String.Join(", ", services.Select(s => $"'{s.Identifier}' ({s.ServiceID}, {s.Status})"));
		}

		private static bool AreClose(DateTime? expected, DateTime? actual)
		{
			if (!expected.HasValue || !actual.HasValue)
			{
				return expected.HasValue == actual.HasValue;
			}

			return (expected.Value.ToUniversalTime() - actual.Value.ToUniversalTime()).Duration() <= TimeTolerance;
		}

		private void ValidateService(Models.ServiceOrderItem orderItem, Models.Service service, List<string> problems)
		{
			string subject = $"service '{service.Identifier}' of order item '{orderItem.Identifier}'";
			string itemSpecificationId = orderItem.ServiceInfo == null ? null : orderItem.ServiceInfo.SpecificationId.Identifier;
			string serviceSpecificationId = service.ServiceSpecificationId.Identifier;

			if (!String.Equals(testData.SpecificationId, itemSpecificationId, StringComparison.OrdinalIgnoreCase))
			{
				problems.Add($"order item '{orderItem.Identifier}' specification expected '{testData.SpecificationId}', got '{itemSpecificationId}'");
			}

			if (!String.Equals(itemSpecificationId, serviceSpecificationId, StringComparison.OrdinalIgnoreCase))
			{
				problems.Add($"{subject} specification expected '{itemSpecificationId}' (the order item specification), got '{serviceSpecificationId}'");
			}

			if (!String.Equals(expectedServiceStatus, service.Status.ToString(), StringComparison.Ordinal))
			{
				problems.Add($"{subject} status expected '{expectedServiceStatus}', got '{service.Status}'");
			}

			if (!String.Equals(orderItem.Name, service.Description, StringComparison.Ordinal))
			{
				problems.Add($"{subject} description expected '{orderItem.Name}' (the order item name), got '{service.Description}'");
			}

			if (service.Name == null || !service.Name.StartsWith(orderItem.Name, StringComparison.Ordinal))
			{
				problems.Add($"{subject} name expected to start with '{orderItem.Name}' (the order item name), got '{service.Name}'");
			}

			if (service.ServiceID == null || !service.ServiceID.StartsWith("SERVICE-", StringComparison.Ordinal))
			{
				problems.Add($"{subject} service ID '{service.ServiceID}' does not follow the 'SERVICE-nnnnn' format");
			}

			if (!AreClose(orderItem.StartTime, service.StartTime) || !AreClose(orderItem.EndTime, service.EndTime))
			{
				problems.Add($"{subject} runtime expected '{orderItem.StartTime:o}' - '{orderItem.EndTime:o}' (the order item runtime), got '{service.StartTime:o}' - '{service.EndTime:o}'");
			}

			int serviceItemCount = service.ServiceItems == null ? 0 : service.ServiceItems.Count;
			if (serviceItemCount != testData.SpecificationServiceItemCount)
			{
				problems.Add($"{subject} has {serviceItemCount} service item(s), expected {testData.SpecificationServiceItemCount} (copied from the specification)");
			}
		}
	}
}

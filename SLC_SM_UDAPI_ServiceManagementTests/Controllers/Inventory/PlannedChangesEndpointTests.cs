namespace SLC_SM_UDAPI_ServiceManagement.Tests.Controllers
{
	using System.Collections.Generic;
	using Microsoft.Extensions.Logging;
	using Moq;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using Skyline.DataMiner.SDM;
	using SLC_SM_UDAPI_ServiceManagement.Controllers;
	using SLC_SM_UDAPI_ServiceManagement.Models.Inventory;
	using ServiceStatus = DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Service_Behavior.StatusesEnum;

	[TestClass]
	public class PlannedChangesEndpointTests : ServiceInventoryControllerTestBase
	{
		private static readonly DateTime Reference = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
		private static readonly long ReferenceSeconds = new DateTimeOffset(Reference).ToUnixTimeSeconds();

		private PlannedChangeController plannedChanges;

		[TestInitialize]
		public void InitializePlannedChanges()
		{
			plannedChanges = new PlannedChangeController(Mock.Of<ILogger<PlannedChangeController>>(), ApiHelper.Object);
			AttachContext(plannedChanges);
		}

		// ---- GET /plannedChange ----
		[TestMethod]
		public void ListPlannedChanges_MissingDate_Returns400()
		{
			Assert.AreEqual(400, StatusCode(plannedChanges.ListPlannedChanges()));
		}

		[TestMethod]
		public void ListPlannedChanges_DateOutOfRange_Returns400()
		{
			Assert.AreEqual(400, StatusCode(plannedChanges.ListPlannedChanges(long.MaxValue)));
		}

		[TestMethod]
		public void ListPlannedChanges_Matching_ReturnsServiceWithActiveAndPlannedVersions()
		{
			var service = AddStartedService("S1");
			var active = AddActive(service, "active", Reference.AddDays(-1));
			var next = AddVersion(service, "next", Reference.AddDays(-2));

			var result = plannedChanges.ListPlannedChanges(ReferenceSeconds);

			Assert.AreEqual(200, StatusCode(result));
			var changes = Value<List<PlannedChange>>(result);
			Assert.AreEqual(1, changes.Count);
			Assert.AreEqual("SERVICE-00001", changes[0].ServiceId);
			Assert.AreEqual(active.Identifier, changes[0].ActiveVersion.Id);
			Assert.AreEqual(1, changes[0].PlannedVersions.Count);
			Assert.AreEqual(next.Identifier, changes[0].PlannedVersions[0].Id);
		}

		[DataTestMethod]
		[DataRow(ServiceStatus.Terminated)]
		[DataRow(ServiceStatus.Retired)]
		public void ListPlannedChanges_TerminatedOrRetired_NotReturned(ServiceStatus status)
		{
			var service = AddStartedService(status: status);
			AddActive(service, "active", Reference.AddDays(-1));
			AddVersion(service, "next", Reference.AddDays(-2));

			Assert.AreEqual(0, Matches());
		}

		[TestMethod]
		public void ListPlannedChanges_ServiceStartsAfterDate_NotReturned()
		{
			var service = AddStartedService();
			service.StartTime = Reference.AddDays(1);
			AddActive(service, "active", Reference.AddDays(-1));
			AddVersion(service, "next", Reference.AddDays(-2));

			Assert.AreEqual(0, Matches());
		}

		[TestMethod]
		public void ListPlannedChanges_ServiceStartEqualToDate_NotReturned()
		{
			var service = AddStartedService();
			service.StartTime = Reference;
			AddActive(service, "active", Reference.AddDays(-1));
			AddVersion(service, "next", Reference.AddDays(-2));

			Assert.AreEqual(0, Matches());
		}

		[TestMethod]
		public void ListPlannedChanges_ServiceWithoutStartTime_NotReturned()
		{
			var service = AddStartedService();
			service.StartTime = null;
			AddActive(service, "active", Reference.AddDays(-1));
			AddVersion(service, "next", Reference.AddDays(-2));

			Assert.AreEqual(0, Matches());
		}

		[TestMethod]
		public void ListPlannedChanges_ActiveEndAfterDate_NotReturned()
		{
			var service = AddStartedService();
			AddActive(service, "active", Reference.AddDays(1));
			AddVersion(service, "next", Reference.AddDays(-2));

			Assert.AreEqual(0, Matches());
		}

		[TestMethod]
		public void ListPlannedChanges_DatesEqualToReference_NotReturned()
		{
			var service = AddStartedService();
			AddActive(service, "active", Reference);
			AddVersion(service, "next", Reference);

			Assert.AreEqual(0, Matches());
		}

		[TestMethod]
		public void ListPlannedChanges_NonActiveStartsAfterDate_NotReturned()
		{
			var service = AddStartedService();
			AddActive(service, "active", Reference.AddDays(-1));
			AddVersion(service, "next", Reference.AddDays(1));

			Assert.AreEqual(0, Matches());
		}

		[TestMethod]
		public void ListPlannedChanges_NullDates_NotReturned()
		{
			var service = AddStartedService();
			AddActive(service, "active", null);
			AddVersion(service, "next");

			Assert.AreEqual(0, Matches());
		}

		[TestMethod]
		public void ListPlannedChanges_OnlyActiveVersion_NotReturned()
		{
			var service = AddStartedService();
			AddActive(service, "active", Reference.AddDays(-1));

			Assert.AreEqual(0, Matches());
		}

		[TestMethod]
		public void ListPlannedChanges_NoActiveVersion_NotReturned()
		{
			var service = AddStartedService();
			AddVersion(service, "a", Reference.AddDays(-3));
			AddVersion(service, "b", Reference.AddDays(-2));

			Assert.AreEqual(0, Matches());
		}

		[TestMethod]
		public void ListPlannedChanges_ThreeVersions_ReturnsOnlyStartedNonActiveVersions()
		{
			var service = AddStartedService();
			AddActive(service, "active", Reference.AddDays(-1));
			var started = AddVersion(service, "started", Reference.AddDays(-2));
			AddVersion(service, "future", Reference.AddDays(2));

			var changes = Value<List<PlannedChange>>(plannedChanges.ListPlannedChanges(ReferenceSeconds));

			Assert.AreEqual(1, changes.Count);
			Assert.AreEqual(1, changes[0].PlannedVersions.Count);
			Assert.AreEqual(started.Identifier, changes[0].PlannedVersions[0].Id);
		}

		private int Matches()
		{
			return Value<List<PlannedChange>>(plannedChanges.ListPlannedChanges(ReferenceSeconds)).Count;
		}

		private Service AddStartedService(string name = "Service", ServiceStatus status = ServiceStatus.New)
		{
			var service = AddService(name, status, 1);
			service.StartTime = Reference.AddDays(-10);
			return service;
		}

		private ServiceConfigurationVersion AddActive(Service service, string name, DateTime? endDate)
		{
			var version = AddVersion(service, name);
			version.EndDate = endDate;
			service.ServiceConfigurationId = new SdmObjectReference<ServiceConfigurationVersion>(version.Identifier);
			return version;
		}
	}
}

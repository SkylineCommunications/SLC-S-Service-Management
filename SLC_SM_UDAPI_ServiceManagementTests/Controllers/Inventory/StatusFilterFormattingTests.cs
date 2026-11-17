namespace SLC_SM_UDAPI_ServiceManagement.Tests.Controllers
{
	using System.Collections.Generic;
	using Microsoft.Extensions.Logging;
	using Moq;
	using Skyline.DataMiner.Net;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.ManagerStore;
	using Skyline.DataMiner.Net.Messages;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Auditing;
	using Skyline.DataMiner.SDM;
	using Skyline.DataMiner.Utils.UserDefinedApiToolkit;
	using SLC_SM_UDAPI_ServiceManagement.Controllers;
	using SLC_SM_UDAPI_ServiceManagement.Helpers.Inventory;

	/// <summary>
	/// Runs the cursor list endpoints against the real Service Management API helper. Every SLNet request is formatted, as DataMiner does,
	/// so filters that cannot be formatted (such as a <c>StatusId == null</c> filter for the default status) fail here.
	/// </summary>
	[TestClass]
	public class StatusFilterFormattingTests : ControllerTestBase
	{
		[DataTestMethod]
		[DataRow("w", null)]
		[DataRow("e", null)]
		[DataRow("er", null)]
		[DataRow(null, "new")]
		[DataRow(null, "new,active")]
		public void ListServicesCursor_DefaultStatusInFilter_Returns200(string search, string status)
		{
			using (var api = CreateApiHelper())
			{
				var controller = new ServiceInventoryController(
					Mock.Of<ILogger<ServiceInventoryController>>(),
					api,
					Mock.Of<IAccessor<Skyline.DataMiner.Automation.IEngine>>(),
					Mock.Of<IMonitoringServiceManager>(),
					Mock.Of<IServiceOrganizationRepository>(r => r.ReadOrganizationNames() == new Dictionary<Guid, string>()),
					Mock.Of<IServiceItemImplementations>());
				AttachContext(controller);

				var result = controller.ListServices(pagination: "cursor", limit: 10, search: search, status: status);

				Assert.AreEqual(200, StatusCode(result));
			}
		}

		[DataTestMethod]
		[DataRow("w", null)]
		[DataRow("e", null)]
		[DataRow(null, "new")]
		public void ListServiceOrdersCursor_DefaultStatusInFilter_Returns200(string search, string status)
		{
			using (var api = CreateApiHelper())
			{
				var controller = new ServiceOrderController(
					Mock.Of<ILogger<ServiceOrderController>>(),
					api,
					Mock.Of<IAccessor<Skyline.DataMiner.Automation.IEngine>>());
				AttachContext(controller);

				var result = controller.ListServiceOrders(pagination: "cursor", limit: 10, search: search, status: status);

				Assert.AreEqual(200, StatusCode(result));
			}
		}

		private static ServiceManagementApiHelper CreateApiHelper()
		{
			var connection = new Mock<IConnection>();
			connection.Setup(c => c.HandleMessages(It.IsAny<DMSMessage[]>()))
				.Returns<DMSMessage[]>(messages => messages.Select(Respond).ToArray());
			return new ServiceManagementApiHelper(connection.Object, Mock.Of<ICreatableRepository<AuditLogEntry>>(), "test");
		}

		private static DMSMessage Respond(DMSMessage request)
		{
			_ = request.ToString();
			switch (request)
			{
				case ManagerStoreStartPagingRequest<DomInstance> _:
					return new ManagerStorePagingResponse<DomInstance>(new List<DomInstance>(), true, null);
				case ManagerStoreCountRequest<DomInstance> _:
					return new ManagerStoreCountResponse<DomInstance>(0);
				case ManagerStoreReadRequest<DomInstance> _:
					return new ManagerStoreCrudResponse<DomInstance>(new List<DomInstance>());
				default:
					throw new NotSupportedException($"Unexpected request {request.GetType().FullName}.");
			}
		}
	}
}

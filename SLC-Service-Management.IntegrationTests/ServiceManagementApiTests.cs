namespace SLC_Service_Management.IntegrationTests
{
	using System;
	using System.Net;
	using System.Net.Http;

	using Microsoft.VisualStudio.TestTools.UnitTesting;

	/// <summary>
	/// Read-only smoke tests of the Service Management User-Defined API on a freshly installed DataMiner
	/// (port of <c>tests/k6/udapi-smoke.js</c>). Every test names the request and response in its failure message.
	/// </summary>
	[TestClass]
	[TestCategory("IntegrationTest")]
	[TestCategory("ServiceManagementApi")]
	public class ServiceManagementApiTests
	{
		private const string MissingId = "6f1c2a4e-0000-4000-8000-000000000000";

		private static HttpClient client;

		[ClassInitialize]
		public static void ClassInitialize(TestContext context)
		{
			client = DmsTestSetup.Api.CreateHttpClient(DmsTestSetup.Api.Secret);
		}

		[ClassCleanup]
		public static void ClassCleanup()
		{
			client?.Dispose();
			client = null;
		}

		[TestMethod]
		public void List_ServiceCategory_ReturnsJson() => AssertJson(Get("serviceCategory?limit=5"), HttpStatusCode.OK);

		[TestMethod]
		public void List_ServiceSpecification_ReturnsJson() => AssertJson(Get("serviceSpecification?limit=5"), HttpStatusCode.OK);

		[TestMethod]
		public void List_Service_ReturnsJson() => AssertJson(Get("service?limit=5"), HttpStatusCode.OK);

		[TestMethod]
		public void List_ServiceOrder_ReturnsJson() => AssertJson(Get("serviceOrder?limit=5"), HttpStatusCode.OK);

		[TestMethod]
		public void List_PlannedChange_ForToday_ReturnsJson()
		{
			long unixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
			AssertJson(Get("plannedChange?date=" + unixSeconds), HttpStatusCode.OK);
		}

		[TestMethod]
		public void Validation_ServiceCategory_LimitZero_Returns400() => AssertJson(Get("serviceCategory?limit=0"), HttpStatusCode.BadRequest);

		[TestMethod]
		public void Validation_ServiceCategory_InvalidId_Returns400() => AssertJson(Get("serviceCategory/not-a-uuid"), HttpStatusCode.BadRequest);

		[TestMethod]
		public void Validation_ServiceCategory_UnknownId_Returns404() => AssertJson(Get("serviceCategory/" + MissingId), HttpStatusCode.NotFound);

		[TestMethod]
		public void Validation_PlannedChange_WithoutDate_Returns400() => AssertJson(Get("plannedChange"), HttpStatusCode.BadRequest);

		[TestMethod]
		public void Authentication_WithoutToken_Returns401()
		{
			using (var anonymous = DmsTestSetup.Api.CreateHttpClient(null))
			{
				AssertStatus(ApiResponse.Get(anonymous, "serviceCategory"), HttpStatusCode.Unauthorized);
			}
		}

		[TestMethod]
		public void Authentication_WithInvalidToken_Returns401()
		{
			using (var invalid = DmsTestSetup.Api.CreateHttpClient("invalid-token"))
			{
				AssertStatus(ApiResponse.Get(invalid, "serviceCategory"), HttpStatusCode.Unauthorized);
			}
		}

		private static ApiResponse Get(string relativeUrl) => ApiResponse.Get(client, relativeUrl);

		private static void AssertStatus(ApiResponse response, HttpStatusCode expected)
		{
			Assert.AreEqual(expected, response.StatusCode, $"Unexpected status. {response}. API fixture: {DmsTestSetup.Api.Description}");
		}

		private static void AssertJson(ApiResponse response, HttpStatusCode expected)
		{
			AssertStatus(response, expected);
			Assert.IsNotNull(response.TryParseJson(), $"Body is not valid JSON. {response}");
		}
	}
}
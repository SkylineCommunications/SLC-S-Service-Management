namespace SLC_SM_UDAPI_ServiceManagement.Tests.Helpers
{
	using SLC_SM_UDAPI_ServiceManagement.Helpers;

	[TestClass]
	public class CursorMetricsSerializationTests
	{
		[TestMethod]
		public void ToJObject_PascalCaseMetrics_UsesCamelCaseKeys()
		{
			var result = CursorMetricsSerialization.ToJObject(new PascalCaseMetrics { TotalCount = 12, ActiveCount = 9, RetiredCount = 3 });

			Assert.AreEqual(12, (int)result["totalCount"]);
			Assert.AreEqual(9, (int)result["activeCount"]);
			Assert.AreEqual(3, (int)result["retiredCount"]);
			Assert.IsNull(result["ActiveCount"]);
		}

		[TestMethod]
		public void ToJObject_NullMetrics_ReturnsNull()
		{
			Assert.IsNull(CursorMetricsSerialization.ToJObject(null));
		}

		private sealed class PascalCaseMetrics
		{
			public long TotalCount { get; set; }

			public long ActiveCount { get; set; }

			public long RetiredCount { get; set; }
		}
	}
}

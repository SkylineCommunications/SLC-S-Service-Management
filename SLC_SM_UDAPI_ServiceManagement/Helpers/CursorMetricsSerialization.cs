namespace SLC_SM_UDAPI_ServiceManagement.Helpers
{
	using Newtonsoft.Json;
	using Newtonsoft.Json.Linq;
	using Newtonsoft.Json.Serialization;

	/// <summary>
	/// Serializes cursor page metrics with camelCase keys, matching the rest of the UDAPI response contract.
	/// </summary>
	public static class CursorMetricsSerialization
	{
		private static readonly JsonSerializer CamelCaseSerializer = JsonSerializer.Create(new JsonSerializerSettings
		{
			ContractResolver = new CamelCasePropertyNamesContractResolver(),
		});

		/// <summary>Converts a metrics object to a camelCase <see cref="JObject"/>, or <c>null</c> when absent.</summary>
		public static JObject ToJObject(object metrics)
		{
			return metrics == null ? null : JObject.FromObject(metrics, CamelCaseSerializer);
		}
	}
}

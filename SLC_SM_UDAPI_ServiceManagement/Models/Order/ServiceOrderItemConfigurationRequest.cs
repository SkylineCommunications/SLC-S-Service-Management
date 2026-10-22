namespace SLC_SM_UDAPI_ServiceManagement.Models.Order
{
	using System.Collections.Generic;
	using Newtonsoft.Json;
	using SLC_SM_UDAPI_ServiceManagement.Models.Inventory;

	public sealed class ServiceOrderItemConfigurationRequest
	{
		[JsonProperty("DeleteAll")]
		public bool DeleteAll { get; set; }

		[JsonProperty("DeleteConfigurationIds")]
		public List<string> DeleteConfigurationIds { get; set; } = new List<string>();

		[JsonProperty("Parameters")]
		public List<ServiceConfigurationParameterRequest> Parameters { get; set; } = new List<ServiceConfigurationParameterRequest>();
	}
}

namespace SLC_SM_UDAPI_ServiceManagement.Models.Inventory
{
	using System.Collections.Generic;
	using Newtonsoft.Json;

	public sealed class ServiceItemOptionsResponse
	{
		[JsonProperty("workflowAvailable")]
		public bool WorkflowAvailable { get; set; }

		[JsonProperty("srmAvailable")]
		public bool SrmAvailable { get; set; }

		[JsonProperty("workflows")]
		public List<string> Workflows { get; set; } = new List<string>();

		[JsonProperty("bookingManagers")]
		public List<BookingManagerOption> BookingManagers { get; set; } = new List<BookingManagerOption>();

		[JsonProperty("bookingScripts")]
		public List<string> BookingScripts { get; set; } = new List<string>();
	}

	public sealed class BookingManagerOption
	{
		[JsonProperty("name")]
		public string Name { get; set; }

		[JsonProperty("script")]
		public string Script { get; set; }

		[JsonProperty("bookings")]
		public List<BookingOption> Bookings { get; set; } = new List<BookingOption>();
	}

	public sealed class BookingOption
	{
		[JsonProperty("id")]
		public string Id { get; set; }

		[JsonProperty("name")]
		public string Name { get; set; }
	}
}

namespace SLC_SM_UDAPI_ServiceOrder.Models
{
	using System.Collections.Generic;

	using Newtonsoft.Json;

	internal class ErrorResponse
	{
		[JsonProperty("error")]
		public List<Error> Errors { get; } = new List<Error>();
	}

	internal class Error
	{
		[JsonProperty("title")]
		public string Title { get; set; }

		[JsonProperty("detail")]
		public string Details { get; set; }

		[JsonProperty("errorCode")]
		public int Code { get; set; }

		[JsonProperty("faultingNode")]
		public int FaultingNode { get; set; }
	}
}

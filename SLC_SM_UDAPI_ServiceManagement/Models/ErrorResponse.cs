namespace SLC_SM_UDAPI_ServiceManagement.Models
{
	using System.Collections.Generic;

	using Newtonsoft.Json;

	internal class Error
	{
		[JsonProperty("errorCode")]
		public int Code { get; set; }

		[JsonProperty("detail")]
		public string Details { get; set; }

		[JsonProperty("faultingNode")]
		public int FaultingNode { get; set; }

		[JsonProperty("title")]
		public string Title { get; set; }
	}

	internal class ErrorResponse
	{
		[JsonProperty("error")]
		public List<Error> Errors { get; } = new List<Error>();
	}
}
namespace SLC_SM_GQIDS_Get_Service_Items
{
	// Used to process the Service Items
	using System;

	internal sealed class ImplementationItemInfo
	{
		public string Name { get; set; } = String.Empty;

		public string ServiceId { get; set; } = String.Empty;

		public string State { get; set; } = String.Empty;

		public string CustomLink { get; set; } = String.Empty;

		public string MonServiceState { get; set; } = String.Empty;

		public string MonServiceDmaIdSid { get; set; } = String.Empty;

		public string LogLocation { get; set; } = String.Empty;
	}
}
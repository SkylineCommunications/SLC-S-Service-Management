namespace Skyline.DataMiner.Utils.ServiceManagement.Common.UDAPI
{
	using System;

	using Skyline.DataMiner.Net.Apps.UserDefinableApis;

	public static class Constants
	{
		public static class ApiDefinitions
		{
			public static readonly ApiDefinitionId ServiceInventory_UDAPI_Id = new ApiDefinitionId(Guid.Parse("8b27f8d4-f5d0-4d9f-b7f1-7e5e6db4e5c2"));
			public static readonly ApiDefinitionId ServiceInventory_ById_UDAPI_Id = new ApiDefinitionId(Guid.Parse("5f9c2a71-3c8b-44e9-a6f3-91d7b2c84f6e"));
		}

		public static class ApiScripts
		{
			public static readonly string ServiceInventory_UDAPI = "SLC_SM_UDAPI_ServiceInventory";
		}
	}
}
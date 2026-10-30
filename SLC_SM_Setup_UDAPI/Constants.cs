namespace SLC_SM_Setup_UDAPI
{
	using System;

	using Skyline.DataMiner.Net.Apps.UserDefinableApis;

	public static class Constants
	{
		public static class ApiDefinitions
		{
			// Service Order
			public static readonly ApiDefinitionId ServiceOrder_UDAPI_Id = new ApiDefinitionId(Guid.Parse("5f003c88-b62d-4776-b332-bb060791f8ef"));
			public static readonly ApiDefinitionId ServiceOrder_Overview_UDAPI_Id = new ApiDefinitionId(Guid.Parse("d5f7c2a1-7e44-4a63-9d1b-2f6c8e0b4a17"));

			public static readonly ApiDefinitionId ServiceOrder_ById_UDAPI_Id = new ApiDefinitionId(Guid.Parse("59490290-9655-4295-aa27-55fa680a2c50"));
			public static readonly ApiDefinitionId ServiceOrder_ById_Transitions_UDAPI_Id = new ApiDefinitionId(Guid.Parse("80a19507-a3cf-4820-af0c-6c4e11133ff6"));
			public static readonly ApiDefinitionId ServiceOrder_ById_Item_UDAPI_Id = new ApiDefinitionId(Guid.Parse("54c6bc3e-c9f9-45e9-8c7e-5a94b6833e1f"));
			public static readonly ApiDefinitionId ServiceOrder_ById_ByItemId_UDAPI_Id = new ApiDefinitionId(Guid.Parse("a33f98ad-b17a-49a3-b12d-3298fa01310c"));
			public static readonly ApiDefinitionId ServiceOrder_ById_ByItemId_Transition_UDAPI_Id = new ApiDefinitionId(Guid.Parse("d98c7e8b-7ba0-4587-8c86-0ca9b68e8f20"));
			public static readonly ApiDefinitionId ServiceOrder_ById_ByItemId_Configuration_UDAPI_Id = new ApiDefinitionId(Guid.Parse("bc8158ba-f96e-41f7-98cd-80d0c7c0a12c"));
			public static readonly ApiDefinitionId ServiceOrder_ById_ByItemId_InitializeService_UDAPI_Id = new ApiDefinitionId(Guid.Parse("48369df8-9f6a-48d3-a532-d2a6a7ec5c6d"));

			// Service Inventory
			public static readonly ApiDefinitionId ServiceInventory_UDAPI_Id = new ApiDefinitionId(Guid.Parse("8b27f8d4-f5d0-4d9f-b7f1-7e5e6db4e5c2"));
			public static readonly ApiDefinitionId ServiceInventory_Overview_UDAPI_Id = new ApiDefinitionId(Guid.Parse("c4a81e2f-6b53-4d90-a7c2-1e5f9b3d8a64"));

			public static readonly ApiDefinitionId ServiceInventory_ById_UDAPI_Id = new ApiDefinitionId(Guid.Parse("5f9c2a71-3c8b-44e9-a6f3-91d7b2c84f6e"));
			public static readonly ApiDefinitionId ServiceInventory_ById_Transitions_UDAPI_Id = new ApiDefinitionId(Guid.Parse("8150ce8c-313b-431c-84a1-c7e263c061cb"));
			public static readonly ApiDefinitionId ServiceInventory_ById_Configuration_UDAPI_Id = new ApiDefinitionId(Guid.Parse("77f7aff1-4c22-4ec7-ad8c-a22ee01ed22c"));
			public static readonly ApiDefinitionId ServiceInventory_ById_ByConfigurationId_UDAPI_Id = new ApiDefinitionId(Guid.Parse("00eb0630-6ed1-42f1-a1bc-cace211c4378"));
			public static readonly ApiDefinitionId ServiceInventory_ById_Item_UDAPI_Id = new ApiDefinitionId(Guid.Parse("501d3702-4d36-4d0a-bb92-0a2d12f7fb6f"));
			public static readonly ApiDefinitionId ServiceInventory_ById_ByItemId_UDAPI_Id = new ApiDefinitionId(Guid.Parse("e37fd862-be17-494c-86e9-fefb8b2e7942"));
			public static readonly ApiDefinitionId ServiceInventory_ById_Configuration_Editor_UDAPI_Id = new ApiDefinitionId(Guid.Parse("4985e8ff-f541-4578-bcd7-a04052fd108a"));
			public static readonly ApiDefinitionId ServiceInventory_ById_Duplicate_UDAPI_Id = new ApiDefinitionId(Guid.Parse("d2ab3169-98dd-40a9-82fe-064e38715d41"));
			public static readonly ApiDefinitionId ServiceInventory_ById_ByConfigurationId_Duplicate_UDAPI_Id = new ApiDefinitionId(Guid.Parse("6a3e9c4d-2b71-4f58-9d0e-8c5b1a7f3e26"));
			public static readonly ApiDefinitionId ServiceInventory_ById_RelatedOrder_UDAPI_Id = new ApiDefinitionId(Guid.Parse("f284bf0c-fddf-4f7d-b25f-f68569188de1"));
			public static readonly ApiDefinitionId ServiceInventory_ById_Script_UDAPI_Id = new ApiDefinitionId(Guid.Parse("c821ad77-1746-4e43-a311-449c22478d67"));
			public static readonly ApiDefinitionId MonitoringService_UDAPI_Id = new ApiDefinitionId(Guid.Parse("e1ae1ffe-c8a4-4fb2-8da8-26227dbc49ce"));

			// Service Specification
			public static readonly ApiDefinitionId ServiceSpecification_UDAPI_Id = new ApiDefinitionId(Guid.Parse("f41b17ff-13e6-468a-80cd-2933df07c55a"));
			public static readonly ApiDefinitionId ServiceSpecification_Overview_UDAPI_Id = new ApiDefinitionId(Guid.Parse("a9e3d7b2-5c41-4f86-b0d4-2e7a6c1f9b35"));

			public static readonly ApiDefinitionId ServiceSpecification_ById_UDAPI_Id = new ApiDefinitionId(Guid.Parse("ee87680c-a2b2-47b6-81d4-f20070c5d007"));

			public static readonly ApiDefinitionId ServiceSpecification_ById_Configuration_UDAPI_Id = new ApiDefinitionId(Guid.Parse("15b7ef5b-050c-4b9a-9118-ac9effb76d41"));
			public static readonly ApiDefinitionId ServiceSpecification_ById_Item_UDAPI_Id = new ApiDefinitionId(Guid.Parse("ee34f3a1-b6d7-4a26-98c5-7b4e0e12c184"));
			public static readonly ApiDefinitionId ServiceSpecification_ById_ByItemId_UDAPI_Id = new ApiDefinitionId(Guid.Parse("9eb971d6-af83-430d-978a-35d06d24b083"));

			// Service Category
			public static readonly ApiDefinitionId ServiceCategory_UDAPI_Id = new ApiDefinitionId(Guid.Parse("390e2e04-bea4-4ac0-91dd-b8b12fad4419"));

			public static readonly ApiDefinitionId ServiceCategory_ById_UDAPI_Id = new ApiDefinitionId(Guid.Parse("d1b44755-059a-4f7d-a04b-ff74bbf7a5ae"));

		}

		public static class ApiScripts
		{
			public static readonly string ServiceManagement_UDAPI = "SLC_SM_UDAPI_ServiceManagement";
		}
	}
}
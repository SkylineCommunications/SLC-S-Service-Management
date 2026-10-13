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

			public static readonly ApiDefinitionId ServiceOrder_ById_UDAPI_Id = new ApiDefinitionId(Guid.Parse("59490290-9655-4295-aa27-55fa680a2c50"));
			public static readonly ApiDefinitionId ServiceOrder_ById_Transitions_UDAPI_Id = new ApiDefinitionId(Guid.Parse("80a19507-a3cf-4820-af0c-6c4e11133ff6"));
			public static readonly ApiDefinitionId ServiceOrder_ById_Item_UDAPI_Id = new ApiDefinitionId(Guid.Parse("54c6bc3e-c9f9-45e9-8c7e-5a94b6833e1f"));
			public static readonly ApiDefinitionId ServiceOrder_ById_ByItemId_UDAPI_Id = new ApiDefinitionId(Guid.Parse("a33f98ad-b17a-49a3-b12d-3298fa01310c"));
			public static readonly ApiDefinitionId ServiceOrder_ById_ByItemId_Transition_UDAPI_Id = new ApiDefinitionId(Guid.Parse("d98c7e8b-7ba0-4587-8c86-0ca9b68e8f20"));

			// Service Inventory
			public static readonly ApiDefinitionId ServiceInventory_UDAPI_Id = new ApiDefinitionId(Guid.Parse("8b27f8d4-f5d0-4d9f-b7f1-7e5e6db4e5c2"));

			public static readonly ApiDefinitionId ServiceInventory_ById_UDAPI_Id = new ApiDefinitionId(Guid.Parse("5f9c2a71-3c8b-44e9-a6f3-91d7b2c84f6e"));
			public static readonly ApiDefinitionId ServiceInventory_ById_Transitions_UDAPI_Id = new ApiDefinitionId(Guid.Parse("8150ce8c-313b-431c-84a1-c7e263c061cb"));
			public static readonly ApiDefinitionId ServiceInventory_ById_Configuration_UDAPI_Id = new ApiDefinitionId(Guid.Parse("77f7aff1-4c22-4ec7-ad8c-a22ee01ed22c"));
			public static readonly ApiDefinitionId ServiceInventory_ById_ByConfigurationId_UDAPI_Id = new ApiDefinitionId(Guid.Parse("00eb0630-6ed1-42f1-a1bc-cace211c4378"));

			// Service Specification
			public static readonly ApiDefinitionId ServiceSpecification_UDAPI_Id = new ApiDefinitionId(Guid.Parse("f41b17ff-13e6-468a-80cd-2933df07c55a"));

			public static readonly ApiDefinitionId ServiceSpecification_ById_UDAPI_Id = new ApiDefinitionId(Guid.Parse("ee87680c-a2b2-47b6-81d4-f20070c5d007"));

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
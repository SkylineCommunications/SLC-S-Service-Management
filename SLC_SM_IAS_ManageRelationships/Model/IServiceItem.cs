namespace SLC_SM_IAS_ManageRelationships.Model
{
	using System;
	using System.Collections.Generic;
	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	public interface IServiceItem
	{
		Guid Guid { get; set; }

		List<Models.ServiceItem> ServiceItems { get; set; }

		List<Models.ServiceItemRelationship> ServiceItemRelationships { get; set; }
	}

	public class ScriptServiceItem : IServiceItem
	{
		public Guid Guid { get; set; }

		public List<Models.ServiceItem> ServiceItems { get; set; }

		public List<Models.ServiceItemRelationship> ServiceItemRelationships { get; set; }
	}
}
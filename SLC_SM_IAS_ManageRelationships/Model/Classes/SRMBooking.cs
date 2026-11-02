namespace SLC_SM_IAS_ManageRelationships.Controller
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using DomHelpers.SlcWorkflow;
	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	internal class SRMBooking : IDefinitionObject
	{
		private readonly IList<Models.ServiceItemRelationship> _existingRelationships;
		private readonly Models.ServiceItem _serviceItem;

		public SRMBooking(Models.ServiceItem serviceItem, IList<Models.ServiceItemRelationship> existingRelationships)
		{
			_serviceItem = serviceItem;
			_existingRelationships = existingRelationships;
		}

		IEnumerable<NodesSection> IDefinitionObject.GetAvailableInputs()
		{
			if (!_serviceItem.ServiceItemID.HasValue)
			{
				throw new InvalidOperationException("Cannot resolve SRM booking relationships for a service item without a ServiceItemID.");
			}

			return _existingRelationships.Any(r => r.ChildServiceItem == _serviceItem.ServiceItemID.ToString())
				? Enumerable.Empty<NodesSection>()
				: new[] { new NodesSection { NodeID = "0", NodeAlias = "Default SRM Input" } };
		}

		IEnumerable<NodesSection> IDefinitionObject.GetAvailableOutputs()
		{
			return new[] { new NodesSection { NodeID = "1", NodeAlias = "Default SRM Output" } };
		}
	}
}
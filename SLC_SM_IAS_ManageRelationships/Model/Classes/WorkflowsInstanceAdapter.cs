namespace SLC_SM_IAS_ManageRelationships.Controller
{
	using System.Collections.Generic;
	using DomHelpers.SlcWorkflow;
	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	internal class WorkflowsInstanceAdapter : IDefinitionObject
	{
		private readonly Models.ServiceItem _serviceItem;
		private readonly WorkflowsInstance _instance;
		private readonly IList<Models.ServiceItemRelationship> _existingItemRelationships;

		internal WorkflowsInstanceAdapter(Models.ServiceItem serviceItem, WorkflowsInstance instance, IList<Models.ServiceItemRelationship> existingItemRelationships)
		{
			_instance = instance;
			_existingItemRelationships = existingItemRelationships;
			_serviceItem = serviceItem;
		}

		public IEnumerable<NodesSection> GetAvailableInputs()
		{
			////var inputsInuse = new HashSet<string>(
			////	_existingItemRelationShips
			////		.Where(r => r.ChildServiceItem == _serviceItem.ID.ToString())
			////		.Select(r => r.ChildServiceItemInterfaceId));

			////var availableInputs = _instance.Nodeses
			////	.Where(
			////		n =>
			////			n.NodeType == SlcWorkflowIds.Enums.Nodetype.Source &&
			////			!inputsInuse.Contains(n.NodeID));

			////return availableInputs;
			return new[] { new NodesSection { NodeID = "0", NodeAlias = "Default Workflow Input" } };
		}

		public IEnumerable<NodesSection> GetAvailableOutputs()
		{
			////var availableOutputs = _instance.Nodeses
			////	.Where(
			////		n =>
			////			n.NodeType == SlcWorkflowIds.Enums.Nodetype.Destination
			////		/*&& !outputsInUse.Contains(n.NodeID)*/);

			////return availableOutputs;
			return new[] { new NodesSection { NodeID = "1", NodeAlias = "Default Workflow Output" } };
		}
	}
}
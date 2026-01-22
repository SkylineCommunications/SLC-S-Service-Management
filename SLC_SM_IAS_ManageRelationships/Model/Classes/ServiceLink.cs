namespace SLC_SM_IAS_ManageRelationships.Controller
{
	using System.Collections.Generic;
	using System.Linq;
	using DomHelpers.SlcWorkflow;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.Relationship;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM;
	using SLC_SM_IAS_ManageRelationships.Model;

	internal class ServiceLink : IDefinitionObject
	{
		private readonly IEngine _engine;
		private readonly IServiceItem _serviceItem;

		public ServiceLink(IEngine engine, IServiceItem serviceItem)
		{
			_engine = engine;
			_serviceItem = serviceItem;
		}

		IEnumerable<NodesSection> IDefinitionObject.GetAvailableInputs()
		{
			return new DataHelperLink(_engine.GetUserConnection()).Read(LinkExposers.ChildID.Equal(_serviceItem.Guid.ToString())).Any()
				? Enumerable.Empty<NodesSection>()
				: new[] { new NodesSection { NodeID = "0", NodeAlias = "Default Service Link Input" } };
		}

		IEnumerable<NodesSection> IDefinitionObject.GetAvailableOutputs()
		{
			////return _existingRelationships.Any(r => r.ParentID == _serviceItem.GetId().Id.ToString())
			////	? Enumerable.Empty<NodesSection>()
			////	: new[] { new NodesSection { NodeID = "1", NodeAlias = "Default Service Link Output" } };
			return new[] { new NodesSection { NodeID = "1", NodeAlias = "Default Service Link Output" } };
		}
	}
}
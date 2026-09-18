namespace SLC_SM_IAS_ManageRelationships.Controller
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using DomHelpers.SlcWorkflow;
	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	public class ServiceItemLinkMap
	{
		public Models.ServiceItem SourceNode { get; set; }

		public Models.ServiceItem DestinationNode { get; set; }

		public IEnumerable<NodesSection> AvailableSources { get; set; }

		public IEnumerable<NodesSection> AvailableDestinations { get; set; }

		public List<Models.ServiceItemRelationship> Links { get; set; }

		public bool HasSources => AvailableSources.Any();

		public bool HasDestinations => AvailableDestinations.Any();

		public bool HasSingleSourceInterface => AvailableSources.Count() == 1;

		public bool HasSingleDestinationInterface => AvailableDestinations.Count() == 1;

		public bool IsOneToOne => HasSingleSourceInterface && HasSingleDestinationInterface;

		public bool HasLink(NodesSection source, NodesSection destination)
		{
			return Links.Any(l => l.ParentServiceItemInterfaceId == source.NodeID && l.ChildServiceItemInterfaceId == destination.NodeID);
		}

		public void AddLink(string sourceInterface, string destinationInterface)
		{
			if (!SourceNode.ServiceItemID.HasValue || !DestinationNode.ServiceItemID.HasValue)
			{
				throw new InvalidOperationException("Cannot create a link for service items without a ServiceItemID.");
			}

			Links.Add(new Models.ServiceItemRelationship
			{
				Id = Guid.NewGuid().ToString(),
				Type = "Connection",
				ParentServiceItem = SourceNode.ServiceItemID.Value.ToString(),
				ParentServiceItemInterfaceId = sourceInterface,
				ChildServiceItem = DestinationNode.ServiceItemID.Value.ToString(),
				ChildServiceItemInterfaceId = destinationInterface,
			});
		}

		public Models.ServiceItemRelationship FindLinkBySource(string sourceInterface)
		{
			return Links.FirstOrDefault(l => l.ParentServiceItemInterfaceId == sourceInterface);
		}

		public void RemoveLink(Models.ServiceItemRelationship link)
		{
			Links.Remove(link);
		}

		public void ClearLinks()
		{
			Links.Clear();
		}
	}
}

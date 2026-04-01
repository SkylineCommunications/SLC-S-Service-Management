namespace SLC_SM_IAS_ManageRelationships.Model
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using DomHelpers.SlcServicemanagement;
	using DomHelpers.SlcWorkflow;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;

	using SLC_SM_IAS_ManageRelationships.Controller;
	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement.Models;

	internal class ManageConnectionsModel
	{
		private readonly IEngine _engine;
		private readonly DomHelper _wfDomHelper;
		private bool _workflowAvailable;

		public ManageConnectionsModel(IEngine engine)
		{
			_engine = engine;

			_workflowAvailable = _engine.DomModelExists(SlcWorkflowIds.ModuleId, null);

			if (_workflowAvailable)
			{
				_wfDomHelper = new DomHelper(_engine.SendSLNetMessages, SlcWorkflowIds.ModuleId);
			}
		}

		public WorkflowsInstance GetWorkflowbyId(Guid workflowId)
		{
			if (!_workflowAvailable)
			{
				throw new InvalidOperationException("The Media Ops solution needs to be installed to use this feature. The '(slc)workflow' DOM model is required but not found on the system.");
			}

			var domInstance = _wfDomHelper.DomInstances.Read(DomInstanceExposers.Id.Equal(workflowId)).FirstOrDefault();
			if (domInstance == null)
				throw new InvalidOperationException($"Could not find workflow with id {workflowId}");

			return new WorkflowsInstance(domInstance);
		}

		public WorkflowsInstance GetWorkflowbyName(string workflowName)
		{
			if (!_workflowAvailable)
			{
				throw new InvalidOperationException("The Media Ops solution needs to be installed to use this feature. The '(slc)workflow' DOM model is required but not found on the system.");
			}

			var domInstance = _wfDomHelper.DomInstances.Read(DomInstanceExposers.Name.Equal(workflowName)).FirstOrDefault();
			if (domInstance == null)
				throw new InvalidOperationException($"Could not find workflow with id {workflowName}");

			return new WorkflowsInstance(domInstance);
		}

		/// <summary>
		/// Turns a list of Service Items (A,B,C,D) to a list of pairs (A,B), (B,C), (C,D).
		/// This allows building the relationships between Service Items in the same order the user selected them.
		/// </summary>
		/// <param name="source">A list of Service Items to be connected in sequence.</param>
		/// <returns>A list of Service Item pairs between which a relationship will be built. </returns>
		public IEnumerable<(Models.ServiceItem, Models.ServiceItem)> ToSequentialPairs(IEnumerable<Models.ServiceItem> source)
		{
			using (var enumerator = source.GetEnumerator())
			{
				if (!enumerator.MoveNext())
					yield break;

				var previous = enumerator.Current;
				while (enumerator.MoveNext())
				{
					yield return (previous, enumerator.Current);
					previous = enumerator.Current;
				}
			}
		}

		public List<Models.ServiceItemRelationShip> FindRelationshipsBetweenPair(
			IServiceItem instance,
			(Models.ServiceItem, Models.ServiceItem) pair)
		{
			var relationships = instance.ServiceItemRelationShips;
			var parentId = pair.Item1.ID.ToString();
			var childId = pair.Item2.ID.ToString();

			return relationships.Where(r =>
				r.ParentServiceItem == parentId &&
				r.ChildServiceItem == childId).ToList();
		}

		public IServiceItem GetInstance(Guid domId)
		{
			Models.Service service = new DataHelperService(_engine.GetUserConnection()).Read(ServiceExposers.Guid.Equal(domId)).FirstOrDefault();
			if (service != null)
			{
				return new ScriptServiceItem
				{
					Guid = service.ID,
					ServiceItems = service.ServiceItems,
					ServiceItemRelationShips = service.ServiceItemsRelationships,
				};
			}

			Models.ServiceSpecification spec = new DataHelperServiceSpecification(_engine.GetUserConnection()).Read(ServiceSpecificationExposers.Guid.Equal(domId)).FirstOrDefault();
			if (spec != null)
			{
				return new ScriptServiceItem
				{
					Guid = spec.ID,
					ServiceItems = spec.ServiceItems,
					ServiceItemRelationShips = spec.ServiceItemsRelationships,
				};
			}

			throw new InvalidOperationException($"Could not find the DOM instance with id {domId}");
		}

		public IEnumerable<Models.ServiceItem> GetServiceItems(
			IServiceItem instance,
			IEnumerable<string> serviceItemIds)
		{
			return instance.ServiceItems.Where(x => serviceItemIds.Contains(x.ID.ToString()));
		}

		public void Update(List<ServiceItemLinkMap> linkMap, IServiceItem instance)
		{
			var relationships = instance.ServiceItemRelationShips;

			foreach (var link in linkMap.SelectMany(pair => pair.Links))
			{
				var existing = relationships
					.FirstOrDefault(r => r.ParentServiceItem == link.ParentServiceItem &&
					r.ChildServiceItem == link.ChildServiceItem &&
					r.ParentServiceItemInterfaceId == link.ParentServiceItemInterfaceId);

				if (existing != null)
					relationships.Remove(existing);

				if (!String.IsNullOrEmpty(link.ChildServiceItemInterfaceId))
					relationships.Add(link);
			}

			var dataHelperService = new DataHelperService(_engine.GetUserConnection());
			Models.Service service = dataHelperService.Read(ServiceExposers.Guid.Equal(instance.Guid)).SingleOrDefault();
			if (service != null)
			{
				service.ServiceItemsRelationships = relationships;
				dataHelperService.CreateOrUpdate(service);
				return;
			}

			var dataHelperServiceSpecification = new DataHelperServiceSpecification(_engine.GetUserConnection());
			Models.ServiceSpecification spec = dataHelperServiceSpecification.Read(ServiceSpecificationExposers.Guid.Equal(instance.Guid)).SingleOrDefault();
			if (spec != null)
			{
				spec.ServiceItemsRelationships = relationships;
				dataHelperServiceSpecification.CreateOrUpdate(spec);
			}
		}

		public string CreateServiceItem(Guid domId, string definitionReference, string type)
		{
			var addServiceItemScript = _engine.PrepareSubScript("SLC_SM_AS_AddServiceItem");
			addServiceItemScript.SelectScriptParam("DOM ID", $"[\"{domId}\"]");
			addServiceItemScript.SelectScriptParam("ServiceItemType", type);
			addServiceItemScript.SelectScriptParam("DefinitionReference", definitionReference);
			addServiceItemScript.Synchronous = true;
			addServiceItemScript.InheritScriptOutput = true;
			addServiceItemScript.StartScript();

			if (addServiceItemScript.HadError)
				throw new InvalidOperationException($"Error creating the service item:{addServiceItemScript.GetErrorMessages()}");

			return _engine.GetScriptOutput("ServiceItemId");
		}

		public IDefinitionObject ResolveDefinitionReference(IServiceItem instance, Models.ServiceItem serviceItem)
		{
			if (serviceItem.Type == SlcServicemanagementIds.Enums.ServiceitemtypesEnum.Workflow)
			{
				return new WorkflowsInstanceAdapter(serviceItem, GetWorkflowbyName(serviceItem.DefinitionReference), instance.ServiceItemRelationShips);
			}

			if (serviceItem.Type == SlcServicemanagementIds.Enums.ServiceitemtypesEnum.SRMBooking)
			{
				return new SRMBooking(serviceItem, instance.ServiceItemRelationShips);
			}

			if (serviceItem.Type == SlcServicemanagementIds.Enums.ServiceitemtypesEnum.Service)
			{
				return new ServiceLink(_engine, instance);
			}

			throw new ArgumentException($"Unknown definition reference: {serviceItem.DefinitionReference}");
		}
	}
}
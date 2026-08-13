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
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;

	using SLC_SM_IAS_ManageRelationships.Controller;
	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	internal class ManageConnectionsModel
	{
		private readonly IEngine _engine;
		private readonly DomHelper _wfDomHelper;
		private readonly IServiceManagementApiHelper _serviceManagementApi;
		private bool _workflowAvailable;

		public ManageConnectionsModel(IEngine engine)
		{
			_engine = engine;
			_serviceManagementApi = _engine.GetUserConnection().GetServiceManagementApiHelper("Service Inventory");

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

		public List<Models.ServiceItemRelationship> FindRelationshipsBetweenPair(
			IServiceItem instance,
			(Models.ServiceItem, Models.ServiceItem) pair)
		{
			if (!pair.Item1.ServiceItemID.HasValue || !pair.Item2.ServiceItemID.HasValue)
			{
				throw new InvalidOperationException("Cannot resolve relationships for service items without a ServiceItemID.");
			}

			var relationships = instance.ServiceItemRelationships;
			var parentId = pair.Item1.ServiceItemID.Value.ToString();
			var childId = pair.Item2.ServiceItemID.Value.ToString();

			return relationships.Where(r =>
				r.ParentServiceItem == parentId &&
				r.ChildServiceItem == childId).ToList();
		}

		public IServiceItem GetInstance(Guid domId)
		{
			Models.Service service = _serviceManagementApi.ServiceInventory.Services.Read(ServiceExposers.Identifier.Equal(domId.ToString())).FirstOrDefault();
			if (service != null)
			{
				return new ScriptServiceItem
				{
					Guid = Guid.Parse(service.Identifier),
					ServiceItems = service.ServiceItems,
					ServiceItemRelationships = service.ServiceItemsRelationships ?? new List<Models.ServiceItemRelationship>(),
				};
			}

			Models.ServiceSpecification spec = _serviceManagementApi.ServiceCatalog.ServiceSpecifications.Read(ServiceSpecificationExposers.Identifier.Equal(domId.ToString())).FirstOrDefault();
			if (spec != null)
			{
				return new ScriptServiceItem
				{
					Guid = Guid.Parse(spec.Identifier),
					ServiceItems = spec.ServiceItems,
					ServiceItemRelationships = spec.ServiceItemsRelationships ?? new List<Models.ServiceItemRelationship>(),
				};
			}

			throw new InvalidOperationException($"Could not find the DOM instance with id {domId}");
		}

		public IEnumerable<Models.ServiceItem> GetServiceItems(
			IServiceItem instance,
			IEnumerable<string> serviceItemIds)
		{
			return instance.ServiceItems.Where(x => x.ServiceItemID.HasValue && serviceItemIds.Contains(x.ServiceItemID.Value.ToString()));
		}

		public void Update(List<ServiceItemLinkMap> linkMap, IServiceItem instance)
		{
			var relationships = instance.ServiceItemRelationships;

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

			Models.Service service = _serviceManagementApi.ServiceInventory.Services.Read(ServiceExposers.Identifier.Equal(instance.Guid.ToString())).SingleOrDefault();
			if (service != null)
			{
				service.ServiceItemsRelationships = relationships;
				_serviceManagementApi.ServiceInventory.Services.Update(service);
				return;
			}

			Models.ServiceSpecification spec = _serviceManagementApi.ServiceCatalog.ServiceSpecifications.Read(ServiceSpecificationExposers.Identifier.Equal(instance.Guid.ToString())).SingleOrDefault();
			if (spec != null)
			{
				spec.ServiceItemsRelationships = relationships;
				_serviceManagementApi.ServiceCatalog.ServiceSpecifications.Update(spec);
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
				return new WorkflowsInstanceAdapter(serviceItem, GetWorkflowbyName(serviceItem.DefinitionReference), instance.ServiceItemRelationships);
			}

			if (serviceItem.Type == SlcServicemanagementIds.Enums.ServiceitemtypesEnum.SRMBooking)
			{
				return new SRMBooking(serviceItem, instance.ServiceItemRelationships);
			}

			if (serviceItem.Type == SlcServicemanagementIds.Enums.ServiceitemtypesEnum.Service)
			{
				return new ServiceLink(_engine, instance);
			}

			throw new ArgumentException($"Unknown definition reference: {serviceItem.DefinitionReference}");
		}
	}
}
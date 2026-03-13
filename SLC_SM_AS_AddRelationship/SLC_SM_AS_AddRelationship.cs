/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE		VERSION		AUTHOR			COMMENTS

28/05/2025	1.0.0.1		RCA, Skyline	Initial version
****************************************************************************
*/

namespace SLCSMASAddRelationship
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using DomHelpers.SlcServicemanagement;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;

	/// <summary>
	/// Represents a DataMiner Automation script.
	/// </summary>
	public class Script
	{
		private const string ADD = "add";
		private const string DELETE = "delete";
		private Guid _domId;
		private string _sourceId;
		private string _destinationId;
		private string _sourceInterfaceId;
		private string _destinationInterfaceId;

		private string _action;

		private DomHelper _domHelper;

		/// <summary>
		/// The script entry point.
		/// </summary>
		/// <param name="engine">Link with SLAutomation process.</param>
		public void Run(IEngine engine)
		{
			try
			{
				////ATTENTION: THIS SCRIPT IS CURRENTLY NOT BEING USED.
				////RunSafe(engine);
			}
			catch (Exception e)
			{
				engine.ExitFail("Run|Something went wrong: " + e);
			}
		}

		private void RunSafe(IEngine engine)
		{
			LoadPrameters(engine);

			_domHelper = new DomHelper(engine.SendSLNetMessages, SlcServicemanagementIds.ModuleId);

			var domInstance = GetDomInstanceOrThrow(_domId);

			var relationshipHandler = GetRelationshipHandler();

			ApplyRelationshipUpdate(domInstance, relationshipHandler);
		}

		private DomInstance GetDomInstanceOrThrow(Guid domId)
		{
			var instance = _domHelper.DomInstances.Read(DomInstanceExposers.Id.Equal(domId)).FirstOrDefault();

			if (instance == null)
			{
				throw new InvalidOperationException($"No Service/Service Specification found with ID {domId}");
			}

			return instance;
		}

		private Action<IList<ServiceItemRelationshipSection>> GetRelationshipHandler()
		{
			if (_action == ADD)
			{
				var section = new ServiceItemRelationshipSection
				{
					ID = Guid.NewGuid().ToString(),
					ParentServiceItem = _sourceId,
					ParentServiceItemInterfaceID = _sourceInterfaceId,
					ChildServiceItem = _destinationId,
					ChildServiceItemInterfaceID = _destinationInterfaceId,
				};

				return list => HandleServiceItemRelationshipUpdate(list, section);
			}

			if (_action == DELETE)
			{
				return HandleServiceItemRelationshipDelete;
			}

			throw new InvalidOperationException($"Could not parse the action {_action}");
		}

		private void ApplyRelationshipUpdate(DomInstance domInstance, Action<IList<ServiceItemRelationshipSection>> handler)
		{
			if (domInstance.DomDefinitionId.Id == SlcServicemanagementIds.Definitions.Services.Id)
			{
				var service = new ServicesInstance(domInstance);
				handler(service.ServiceItemRelationships);
				service.Save(_domHelper);
			}
			else if (domInstance.DomDefinitionId.Id == SlcServicemanagementIds.Definitions.ServiceSpecifications.Id)
			{
				var specification = new ServiceSpecificationsInstance(domInstance);
				handler(specification.ServiceItemRelationships);
				specification.Save(_domHelper);
			}
		}

		private void HandleServiceItemRelationshipDelete(IList<ServiceItemRelationshipSection> relationships)
		{
			var existing = FindMatchingRelationship(relationships, _sourceId.ToString(), _sourceInterfaceId, _destinationId.ToString(), _destinationInterfaceId);
			if (existing != null)
			{
				relationships.Remove(existing);
			}
		}

		private void HandleServiceItemRelationshipUpdate(IList<ServiceItemRelationshipSection> relationships, ServiceItemRelationshipSection newRelationship)
		{
			var existing = FindMatchingRelationship(
				relationships,
				newRelationship.ParentServiceItem,
				newRelationship.ParentServiceItemInterfaceID,
				newRelationship.ChildServiceItem,
				newRelationship.ChildServiceItemInterfaceID);

			if (existing == null)
			{
				relationships.Add(newRelationship);
			}
		}

		private ServiceItemRelationshipSection FindMatchingRelationship(
			IList<ServiceItemRelationshipSection> relationships,
			string parentId,
			string parentInterfaceId,
			string childId,
			string childInterfaceId)
		{
			return relationships.FirstOrDefault(x =>
				x.ParentServiceItem == parentId &&
				x.ParentServiceItemInterfaceID == parentInterfaceId &&
				x.ChildServiceItem == childId &&
				x.ChildServiceItemInterfaceID == childInterfaceId);
		}

		private void LoadPrameters(IEngine engine)
		{
			_domId = engine.ReadScriptParamFromApp<Guid>("DomId");

			_action = engine.ReadScriptParamFromApp("Action").ToLower();
			_sourceId = engine.ReadScriptParamFromApp("SourceId");
			_destinationId = engine.ReadScriptParamFromApp("DestinationId");
			_sourceInterfaceId = engine.ReadScriptParamFromApp("SourceInterfaceId");
			_destinationInterfaceId = engine.ReadScriptParamFromApp("DestinationInterfaceId");
		}
	}
}

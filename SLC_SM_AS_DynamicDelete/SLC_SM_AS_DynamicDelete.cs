/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE		VERSION		AUTHOR			COMMENTS

11/06/2025	1.0.0.1		RCA, Skyline	Initial version
****************************************************************************
*/

namespace SLCSMASDynamicDelete
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using DomHelpers;
	using DomHelpers.SlcServicemanagement;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.IAS;
	using SLC_SM_AS_DynamicDelete;

	/// <summary>
	/// Represents a DataMiner Automation script.
	/// </summary>
	public class Script
	{
		private IList<ServiceItemRelationshipSection> _connections;
		private IList<ServiceItemsSection> _nodes;
		private IEngine _engine;

		/// <summary>
		/// The script entry point.
		/// </summary>
		/// <param name="engine">Link with SLAutomation process.</param>
		public void Run(IEngine engine)
		{
			/*
			* Note:
			* Do not remove the commented methods below!
			* The lines are needed to execute an interactive automation script from the non-interactive automation script or from Visio!
			*
			* engine.ShowUI();
			*/
			if (engine.IsInteractive)
			{
				engine.FindInteractiveClient("Failed to run script in interactive mode", 1);
			}

			try
			{
				RunSafe(engine);
			}
			catch (ScriptAbortException)
			{
				// Catch normal abort exceptions (engine.ExitFail or engine.ExitSuccess)
				throw; // Comment if it should be treated as a normal exit of the script.
			}
			catch (ScriptForceAbortException)
			{
				// Catch forced abort exceptions, caused via external maintenance messages.
				throw;
			}
			catch (ScriptTimeoutException)
			{
				// Catch timeout exceptions for when a script has been running for too long.
				throw;
			}
			catch (InteractiveUserDetachedException)
			{
				// Catch a user detaching from the interactive script by closing the window.
				// Only applicable for interactive scripts, can be removed for non-interactive scripts.
				throw;
			}
			catch (Exception e)
			{
				engine.ShowErrorDialog(e);
			}
		}

		private void RunSafe(IEngine engine)
		{
			_engine = engine;

			var scriptData = new ScriptData(engine);
			var domHelper = new DomHelper(engine.SendSLNetMessages, SlcServicemanagementIds.ModuleId);

			var instance = domHelper.DomInstances
				.Read(DomInstanceExposers.Id.Equal(scriptData.DomId))
				.FirstOrDefault();

			if (instance == null)
				throw new InvalidOperationException($"Could not find the DOM instance with id {scriptData.DomId}");

			var domInstanceBase = CreateTypedDomInstance(instance);

			Load(domInstanceBase);
			DeleteNodes(scriptData);
			DeleteRelationships(scriptData);

			domInstanceBase.Save(domHelper);
		}

		private void DeleteNodes(ScriptData scriptData)
		{
			var connections = _connections.ToList(); // cannot iterate mutable collection
			var nodes = _nodes.ToList(); // cannot iterate mutable collection

			foreach (var nodeId in scriptData.NodeIds)
			{
				DeleteNode(connections, nodes, nodeId);
			}
		}

		private void DeleteNode(List<ServiceItemRelationshipSection> connections, List<ServiceItemsSection> nodes, string nodeId)
		{
			var node = nodes.FirstOrDefault(n => n.ServiceItemID.ToString() == nodeId);
			var connectionsToDelete = connections.Where(c => c.ParentServiceItem == node?.ServiceItemID?.ToString() || c.ChildServiceItem == node?.ServiceItemID?.ToString());

			foreach (var connection in connectionsToDelete)
				_connections.Remove(connection);

			_nodes.Remove(node);
		}

		private void DeleteRelationships(ScriptData scriptData)
		{
			var connections = _connections.ToList(); // cannot iterate mutable collection
			foreach (var connectionId in scriptData.ConnectionIds)
			{
				var connection = connections.SingleOrDefault(c => c.ID == connectionId.ToString());
				_connections.Remove(connection);
			}
		}

		private DomInstanceBase CreateTypedDomInstance(DomInstance domInstance)
		{
			if (IsServicesInstance(domInstance))
				return new ServicesInstance(domInstance);

			if (IsServiceSpecificationsInstance(domInstance))
				return new ServiceSpecificationsInstance(domInstance);

			throw new NotSupportedException($"Unsupported DOM definition ID: {domInstance.DomDefinitionId.Id}");
		}

		private bool IsServicesInstance(DomInstance domInstance)
		{
			return domInstance.DomDefinitionId.Id == SlcServicemanagementIds.Definitions.Services.Id;
		}

		private bool IsServiceSpecificationsInstance(DomInstance domInstance)
		{
			return domInstance.DomDefinitionId.Id == SlcServicemanagementIds.Definitions.ServiceSpecifications.Id;
		}

		private void Load(DomInstanceBase domInstance)
		{
			if (domInstance is ServicesInstance services)
			{
				_nodes = services.ServiceItems;
				_connections = services.ServiceItemRelationships;
			}
			else if (domInstance is ServiceSpecificationsInstance specs)
			{
				_nodes = specs.ServiceItems;
				_connections = specs.ServiceItemRelationships;
			}
			else
			{
				throw new InvalidOperationException("Unsupported DomInstance type.");
			}
		}
	}
}

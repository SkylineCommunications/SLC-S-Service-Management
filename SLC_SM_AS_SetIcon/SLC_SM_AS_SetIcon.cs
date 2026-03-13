/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE		VERSION		AUTHOR			COMMENTS

28/08/2025	1.0.0.1		RCA, Skyline	Initial version
****************************************************************************
*/

namespace SLCSMASSetIcon
{
	using System;
	using System.Linq;
	using DomHelpers.SlcServicemanagement;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.IAS;
	using SLC_SM_AS_SetIcon;

	/// <summary>
	/// Represents a DataMiner Automation script.
	/// </summary>
	public class Script
	{
		private ScriptData _scriptData;
		private DomHelper _domHelper;

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
			_scriptData = new ScriptData(engine);
			_domHelper = new DomHelper(engine.SendSLNetMessages, SlcServicemanagementIds.ModuleId);

			SetIcon();
		}

		private void SetIcon()
		{
			switch (_scriptData.Type)
			{
				case ScriptData.ObjectType.ServiceCategory:
					SetServiceCategoryIcon();
					break;
				case ScriptData.ObjectType.Service:
					SetServiceIcon();
					break;
				case ScriptData.ObjectType.ServiceItem:
					SetServiceItemIcon();
					break;
				default:
					break;
			}
		}

		private void SetServiceItemIcon()
		{
			throw new NotImplementedException();
		}

		private void SetServiceIcon()
		{
			var filter = DomInstanceExposers.DomDefinitionId.Equal(SlcServicemanagementIds.Definitions.Services.Id)
				.AND(DomInstanceExposers.Id.Equal(_scriptData.DomId));

			var instance = _domHelper.DomInstances.Read(filter).FirstOrDefault();
			if (instance == null)
			{
				throw new Exception($"Could not find instance with id {_scriptData.DomId}");
			}

			var serviceCategory = new ServicesInstance(instance);

			serviceCategory.ServiceInfo.Icon = _scriptData.Name;
			serviceCategory.Save(_domHelper);
		}

		private void SetServiceCategoryIcon()
		{
			var filter = DomInstanceExposers.DomDefinitionId.Equal(SlcServicemanagementIds.Definitions.ServiceCategory.Id)
				.AND(DomInstanceExposers.Id.Equal(_scriptData.DomId));

			var instance = _domHelper.DomInstances.Read(filter).FirstOrDefault();
			if (instance == null)
			{
				throw new Exception($"Could not find instance with id {_scriptData.DomId}");
			}

			var serviceCategory = new ServiceCategoryInstance(instance);

			serviceCategory.ServiceCategoryInfo.Icon = _scriptData.Name;
			serviceCategory.Save(_domHelper);
		}
	}
}

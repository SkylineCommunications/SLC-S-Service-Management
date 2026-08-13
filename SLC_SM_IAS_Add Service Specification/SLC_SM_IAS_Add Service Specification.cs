/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE        VERSION        AUTHOR            COMMENTS

dd/mm/2025    1.0.0.1        XXX, Skyline    Initial version
****************************************************************************
*/
namespace SLC_SM_IAS_Add_Service_Specification
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.IAS;
	using SLC_SM_IAS_Add_Service_Specification.Presenters;
	using SLC_SM_IAS_Add_Service_Specification.Views;
	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	///     Represents a DataMiner Automation script.
	/// </summary>
	public class Script
	{
		private InteractiveController _controller;
		private IEngine _engine;

		private enum Action
		{
			Add,
			Edit,
		}

		/// <summary>
		///     The script entry point.
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
				_engine = engine;
				_controller = new InteractiveController(engine) { ScriptAbortPopupBehavior = ScriptAbortPopupBehavior.HideAlways };
				RunSafe();
			}
			catch (ScriptAbortException)
			{
				// Catch normal abort exceptions (engine.ExitFail or engine.ExitSuccess)
			}
			catch (ScriptForceAbortException)
			{
				// Catch forced abort exceptions, caused via external maintenance messages.
			}
			catch (ScriptTimeoutException)
			{
				// Catch timeout exceptions for when a script has been running for too long.
			}
			catch (InteractiveUserDetachedException)
			{
				// Catch a user detaching from the interactive script by closing the window.
				// Only applicable for interactive scripts, can be removed for non-interactive scripts.
			}
			catch (Exception e)
			{
				engine.ShowErrorDialog(e);
			}
		}

		private void RunSafe()
		{
			string actionRaw = _engine.ReadScriptParamFromApp("Action");
			if (!Enum.TryParse(actionRaw, true, out Action action))
			{
				throw new InvalidOperationException("No Action provided as input to the script");
			}

			var api = _engine.GetUserConnection().GetServiceManagementApiHelper("Service Catalog");
			List<Models.ServiceSpecification> serviceSpecifications = api.ServiceCatalog.ServiceSpecifications.Read(new TRUEFilterElement<Models.ServiceSpecification>()).ToList();

			var usedOrderItemLabels = serviceSpecifications.Select(x => x.Name).ToList();

			// Init views
			var view = new ServiceSpecView(_engine);
			var presenter = new ServiceSpecPresenter(_engine, view, usedOrderItemLabels);

			// Events
			view.BtnCancel.Pressed += (sender, args) => throw new ScriptAbortException("OK");
			view.BtnAdd.Pressed += (sender, args) =>
			{
				if (presenter.Validate())
				{
					Models.ServiceSpecification specificationToSave = presenter.GetData;
					if (action == Action.Add)
					{
						specificationToSave.Identifier = Guid.NewGuid().ToString();
						api.ServiceCatalog.ServiceSpecifications.Create(specificationToSave);
					}
					else
					{
						api.ServiceCatalog.ServiceSpecifications.Update(specificationToSave);
					}

					throw new ScriptAbortException("OK");
				}
			};

			if (action == Action.Add)
			{
				presenter.LoadFromModel();
			}
			else
			{
				Guid domId = _engine.ReadScriptParamFromApp<Guid>("DOM ID");
				var specification = serviceSpecifications.Find(x => String.Equals(x.Identifier, domId.ToString(), StringComparison.OrdinalIgnoreCase))
				                     ?? throw new InvalidOperationException($"No Service Specification with ID '{domId}' found on the system!");

				var specificationToEdit = api.ServiceCatalog.ServiceSpecifications.Read(ServiceSpecificationExposers.Identifier.Equal(specification.Identifier)).FirstOrDefault();
				presenter.LoadFromModel(specificationToEdit);
			}

			// Run interactive
			_controller.ShowDialog(view);
		}
	}
}

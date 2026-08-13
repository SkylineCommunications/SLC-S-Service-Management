namespace SLCSMTakeOwnership
{
	using System;
	using System.Linq;
	using DomHelpers.SlcPeople_Organizations;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.PeopleAndOrganization;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.IAS;
	using ServiceOrderExposers = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement.ServiceOrderExposers;

	/// <summary>
	/// Represents a DataMiner Automation script.
	/// </summary>
	public class Script
	{
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

			try
			{
				_engine = engine;
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
			Guid domId = _engine.ReadScriptParamFromApp<Guid>("DOM ID");

			var helper = _engine.GetUserConnection().GetServiceManagementApiHelper("Service Ordering");
			var order = helper.ServiceOrder.ServiceOrders.Read(ServiceOrderExposers.Identifier.Equal(domId.ToString())).FirstOrDefault();
			if (order == null)
			{
				throw new NotSupportedException($"No Order exists on the system for the given ID '{domId}'");
			}

			if (!_engine.DomModelExists(SlcPeople_OrganizationsIds.ModuleId, new[] {SlcPeople_OrganizationsIds.Sections.PeopleInformation.Id.Id} ))
			{
				throw new NotSupportedException($"The Media Ops solution needs to be installed to use this feature. The '{SlcPeople_OrganizationsIds.Definitions.People.ModuleId}' DOM model is required but not found on the system.");
			}

			string userName = _engine.UserDisplayName;
			var dataHelperPeople = new DataHelperPeople(_engine.GetUserConnection());
			var person = dataHelperPeople.Read(PeopleExposers.Name.Equal(userName)).FirstOrDefault();
			if (person != null && order.Owner == person.ID)
			{
				// Person already configured as owner - release the ownership
				ReleaseOwnership(helper, order);
				return;
			}

			TakeOwnership(helper, order, person?.ID);
		}

		private void TakeOwnership(IServiceManagementApiHelper helper, ServiceOrder order, Guid? ownerId)
		{
			if (!_engine.ShowConfirmDialog($"Are you sure to you want to take ownership?"))
			{
				return;
			}

			UpdateOwner(helper, order, ownerId);
		}

		private void ReleaseOwnership(IServiceManagementApiHelper helper, ServiceOrder order)
		{
			if (!_engine.ShowConfirmDialog($"Are you sure to you want to release ownership?"))
			{
				return;
			}

			UpdateOwner(helper, order, default(Guid?));
		}

		private void UpdateOwner(IServiceManagementApiHelper helper, ServiceOrder order, Guid? ownerId)
		{
			order.Owner = ownerId;
			helper.ServiceOrder.ServiceOrders.Update(order);
		}
	}
}

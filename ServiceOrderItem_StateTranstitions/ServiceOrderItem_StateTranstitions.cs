namespace ServiceOrderItemStateTranstitions
{
	using System;
	using System.Linq;
	using Library.Dom;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.IAS;
	using static DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Serviceorderitem_Behavior;

	/// <summary>
	///     Represents a DataMiner Automation script.
	/// </summary>
	public class Script
	{
		private IEngine engine;

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
				this.engine = engine;
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

		private static void TransitionOrderToRejected(IEngine engine, Models.ServiceOrderItem orderItem)
		{
			if (!engine.ShowConfirmDialog("Do you wish to reject the current order item?"))
			{
				return;
			}

			orderItem.UpdateStatusToRejected(engine.GetUserConnection());
		}

		private void RunSafe()
		{
			Guid domInstanceId = engine.ReadScriptParamFromApp<Guid>("Id");
			string previousState = engine.ReadScriptParamFromApp("PreviousState").ToLower();
			string nextState = engine.ReadScriptParamFromApp("NextState").ToLower();

			TransitionsEnum transition = Enum.GetValues(typeof(TransitionsEnum))
				.Cast<TransitionsEnum?>()
				.FirstOrDefault(t => t.ToString().Equals($"{previousState}_to_{nextState}", StringComparison.OrdinalIgnoreCase))
				?? throw new NotSupportedException($"The provided previousState '{previousState}' is not supported for nextState '{nextState}'");

			var orderItemHelper = new DataHelperServiceOrderItem(engine.GetUserConnection());
			var orderItem = orderItemHelper.Read(ServiceOrderItemExposers.Guid.Equal(domInstanceId)).FirstOrDefault()
						  ?? throw new NotSupportedException($"No Order Item with ID '{domInstanceId}' exists on the system");

			switch (transition)
			{
				case TransitionsEnum.New_To_Acknowledged:
					// Transition parent order to ACK as well
					orderItem.StatusUpdateToAcknowledged(engine.GetUserConnection());
					break;

				case TransitionsEnum.Pending_To_Inprogress:
				case TransitionsEnum.Acknowledged_To_Inprogress:
					// Transition parent order to In Progress as well
					orderItem.StatusUpdateToInProgress(engine.GetUserConnection());
					break;

				case TransitionsEnum.Inprogress_To_Completed:
					orderItem.UpdateStatusToCompleted(engine.GetUserConnection());
					break;

				case TransitionsEnum.New_To_Rejected:
				case TransitionsEnum.Acknowledged_To_Rejected:
					// Transition linked service items to rejected as well
					TransitionOrderToRejected(engine, orderItem);
					break;

				default:
					engine.GenerateInformation($"[SMS] Status Transition: {orderItem.Name} → {transition}");
					orderItemHelper.UpdateState(orderItem, transition);
					break;
			}
		}
	}
}
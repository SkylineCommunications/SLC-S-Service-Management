namespace SLCSMServiceStateTransitions
{
	using System;
	using DomHelpers.SlcServicemanagement;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel.Actions;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;

	/// <summary>
	///     Represents a DataMiner Automation script.
	/// </summary>
	public class Script
	{
		[AutomationEntryPoint(AutomationEntryPointType.Types.OnDomAction)]
		public void OnDomActionMethod(IEngine engine, ExecuteScriptDomActionContext context)
		{
			// DO NOT REMOVE
			// engine.ShowUI();
			var instanceId = context.ContextId as DomInstanceId;
			var previousState = engine.ReadScriptParamFromApp("PreviousState");
			var nextState = engine.ReadScriptParamFromApp("NextState");

			////engine.GenerateInformation($"EventStateTransition: Input parameters instaceId: {instanceId.ToString()}, PreviousState: {previousState}, NextState: {nextState}");

			////engine.GenerateInformation("Starting DOM Action with script EventStateTransitions");

			////engine.GenerateInformation(previousState);
			////engine.GenerateInformation(nextState);

			var domHelper = new DomHelper(engine.SendSLNetMessages, instanceId.ModuleId);

			////engine.GenerateInformation("Start Event Transition");

			string transitionId = String.Empty;

			switch (previousState)
			{
				case "new":
					switch (nextState)
					{
						case "designed":

							transitionId = SlcServicemanagementIds.Behaviors.Service_Behavior.Transitions.New_To_Designed;
							break;

						default:
							throw new NotSupportedException($"The provided previousState '{previousState}' is not supported for nextState '{nextState}'");
					}

					break;

				case "designed":
					switch (nextState)
					{
						case "reserved":
							transitionId = SlcServicemanagementIds.Behaviors.Service_Behavior.Transitions.Designed_To_Reserved;
							break;

						default:
							throw new NotSupportedException($"The provided previousState '{previousState}' is not supported for nextState '{nextState}'");
					}

					break;

				case "reserved":
					switch (nextState)
					{
						case "active":
							transitionId = SlcServicemanagementIds.Behaviors.Service_Behavior.Transitions.Reserved_To_Active;
							break;

						default:
							throw new NotSupportedException($"The provided previousState '{previousState}' is not supported for nextState '{nextState}'");
					}

					break;

				case "active":
					switch (nextState)
					{
						case "terminated":
							transitionId = SlcServicemanagementIds.Behaviors.Service_Behavior.Transitions.Active_To_Terminated;
							break;

						default:
							throw new NotSupportedException($"The provided previousState '{previousState}' is not supported for nextState '{nextState}'");
					}

					break;

				case "terminated":
					switch (nextState)
					{
						case "retired":
							transitionId = SlcServicemanagementIds.Behaviors.Service_Behavior.Transitions.Terminated_To_Retired;
							break;

						default:
							throw new NotSupportedException($"The provided previousState '{previousState}' is not supported for nextState '{nextState}'");
					}

					break;

				default:
					throw new NotSupportedException($"previousState '{previousState}' is not supported");
			}

			engine.GenerateInformation($"Service Order Status Transition starting: previousState: {previousState}, nextState: {nextState}");
			domHelper.DomInstances.DoStatusTransition(instanceId, transitionId);
		}

		/// <summary>
		///     The Script entry point.
		/// </summary>
		/// <param name="engine">Link with SLAutomation process.</param>
		public void Run(Engine engine)
		{
			// DO NOT REMOVE
			// engine.ShowUI();
			engine.ExitFail("This script should be executed using the 'OnDomAction' entry point");
		}
	}
}
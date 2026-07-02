namespace SLC_SM_IAS_Manage_Service_Scripts.Model
{
	using System;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;

	internal enum ScriptAction
	{
		Add,
		Remove,
		Update,
	}

	internal sealed class ScriptData
	{
		public ScriptData(IEngine engine)
		{
			var actionRaw = engine.ReadScriptParamFromApp("Action");
			if (!Enum.TryParse(actionRaw, true, out ScriptAction action))
			{
				throw new InvalidOperationException($"Unsupported action '{actionRaw}'. Supported actions: Add, Remove, Update.");
			}

			Action = action;
			ServiceName = engine.ReadScriptParamFromApp("Service Name")?.Trim();
			ScriptName = engine.ReadScriptParamFromApp("Script Name")?.Trim();
		}

		public ScriptAction Action { get; }

		public string ServiceName { get; }

		public string ScriptName { get; }

		public void Validate()
		{
			if (String.IsNullOrWhiteSpace(ServiceName))
			{
				throw new InvalidOperationException("No service name was provided.");
			}

			if ((Action == ScriptAction.Remove || Action == ScriptAction.Update) && String.IsNullOrWhiteSpace(ScriptName))
			{
				throw new InvalidOperationException("No script name was provided.");
			}
		}
	}
}

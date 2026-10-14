namespace SLC_SM_IAS_Manage_Service_Scripts.Model
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Messages;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;

	internal enum ScriptAction
	{
		Add,
		Remove,
		Update,
	}

	internal sealed class ScriptData
	{
		private const string ServiceIdParamName = "Service ID";
		private readonly IEngine engine;

		public ScriptData(IEngine engine)
		{
			this.engine = engine;

			var actionRaw = engine.ReadScriptParamFromApp("Action");
			if (!Enum.TryParse(actionRaw, true, out ScriptAction action))
			{
				throw new InvalidOperationException($"Unsupported action '{actionRaw}'. Supported actions: Add, Remove, Update.");
			}

			Action = action;
			ServiceId = engine.ReadScriptParamFromApp("Service Id")?.Trim();
			ScriptName = engine.ReadScriptParamFromApp("Script Name")?.Trim();
			ScriptDescription = engine.ReadScriptParamFromApp("Script Description")?.Trim();
			OriginalScriptDescription = engine.ReadScriptParamFromApp("Original Script Description")?.Trim();
		}

		public ScriptAction Action { get; }

		public string ServiceId { get; }

		public string ScriptDescription { get; }

		public string ScriptName { get; }

		public string OriginalScriptDescription { get; }

		public List<string> GetScriptNamesWithServiceIdParameter()
		{
			try
			{
				var response = engine.SendSLNetSingleResponseMessage(new GetInfoMessage
				{
					DataMinerID = -1,
					HostingDataMinerID = -1,
					Type = InfoType.Scripts,
				}) as GetScriptsResponseMessage;

				if (response == null)
				{
					throw new InvalidOperationException("DataMiner did not return the Automation script list.");
				}

				return response.Scripts
					.Where(HasServiceIdParameter)
					.OrderBy(x => x)
					.ToList();
			}
			catch (Exception exception)
			{
				engine.Log("GetScriptNamesWithServiceIdParameter|Unexpected exception: " + exception);
				throw new InvalidOperationException("The available Automation scripts could not be loaded.", exception);
			}
		}

		public List<string> GetScriptInputParameters(string scriptName)
		{
			try
			{
				var info = engine.SendSLNetSingleResponseMessage(new GetScriptInfoMessage { Name = scriptName }) as GetScriptInfoResponseMessage;
				if (info == null)
				{
					throw new InvalidOperationException($"Automation script '{scriptName}' was not found.");
				}

				return info.Parameters
					.Where(p => !String.Equals(p.Description, ServiceIdParamName, StringComparison.OrdinalIgnoreCase))
					.Select(p => p.Description)
					.ToList();
			}
			catch (Exception exception)
			{
				engine.Log("GetScriptInputParameters|Unexpected exception: " + exception);
				throw new InvalidOperationException($"The input parameters for Automation script '{scriptName}' could not be loaded.", exception);
			}
		}

		public void ValidateInputParameters()
		{
			if (String.IsNullOrWhiteSpace(ServiceId))
			{
				throw new InvalidOperationException("No service Id was provided.");
			}

			bool apiMode = !String.IsNullOrWhiteSpace(engine.ReadScriptParamFromApp("Payload"));
			if ((apiMode || Action == ScriptAction.Remove || Action == ScriptAction.Update) && String.IsNullOrWhiteSpace(ScriptDescription))
			{
				throw new InvalidOperationException("No script description was provided.");
			}

			if (apiMode && (Action == ScriptAction.Add || Action == ScriptAction.Update) && String.IsNullOrWhiteSpace(ScriptName))
			{
				throw new InvalidOperationException("No script name was provided.");
			}
		}

		private bool HasServiceIdParameter(string scriptName)
		{
			var info = engine.SendSLNetSingleResponseMessage(new GetScriptInfoMessage { Name = scriptName }) as GetScriptInfoResponseMessage;
			return info != null && info.Parameters.Any(parameter => String.Equals(parameter.Description, ServiceIdParamName, StringComparison.OrdinalIgnoreCase));
		}
	}
}
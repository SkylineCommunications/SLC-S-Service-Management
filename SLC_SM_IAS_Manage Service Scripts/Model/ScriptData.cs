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
			ScriptDescription = engine.ReadScriptParamFromApp("Script Description")?.Trim();
		}

		public ScriptAction Action { get; }

		public string ServiceId { get; }

		public string ScriptDescription { get; }

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
					return new List<string>();
				}

				return response.Scripts
					.Where(HasServiceIdParameter)
					.OrderBy(x => x)
					.ToList();
			}
			catch (Exception)
			{
				return new List<string>();
			}
		}

		public List<string> GetScriptInputParameters(string scriptName)
		{
			try
			{
				var info = engine.SendSLNetSingleResponseMessage(new GetScriptInfoMessage { Name = scriptName }) as GetScriptInfoResponseMessage;
				if (info == null)
				{
					return new List<string>();
				}

				return info.Parameters
					.Where(p => !String.Equals(p.Description, ServiceIdParamName, StringComparison.OrdinalIgnoreCase))
					.Select(p => p.Description)
					.ToList();
			}
			catch (Exception)
			{
				return new List<string>();
			}
		}

		public void ValidateInputParameters()
		{
			if (String.IsNullOrWhiteSpace(ServiceId))
			{
				throw new InvalidOperationException("No service Id was provided.");
			}

			if ((Action == ScriptAction.Remove || Action == ScriptAction.Update) && String.IsNullOrWhiteSpace(ScriptDescription))
			{
				throw new InvalidOperationException("No script description was provided.");
			}
		}

		private bool HasServiceIdParameter(string scriptName)
		{
			var info = engine.SendSLNetSingleResponseMessage(new GetScriptInfoMessage { Name = scriptName }) as GetScriptInfoResponseMessage;
			return info != null && info.Parameters.Any(parameter => String.Equals(parameter.Description, ServiceIdParamName, StringComparison.OrdinalIgnoreCase));
		}
	}
}
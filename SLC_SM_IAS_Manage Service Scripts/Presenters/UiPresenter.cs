namespace SLC_SM_IAS_Manage_Service_Scripts.Presenters
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement;
	using SLC_SM_IAS_Manage_Service_Scripts.Model;
	using SLC_SM_IAS_Manage_Service_Scripts.Views;

	internal sealed class UiPresenter
	{
		private readonly IEngine engine;
		private readonly ManageServiceScriptsView view;
		private readonly ScriptData data;

		public UiPresenter(IEngine engine, ManageServiceScriptsView view, ScriptData data)
		{
			this.engine = engine;
			this.view = view;
			this.data = data;
		}

		public void Handle()
		{
			var helper = new DataHelperService(engine.GetUserConnection());
			var service = helper.ReadBasicDetails()
				.FirstOrDefault(existingService => String.Equals(existingService.Name, data.ServiceName, StringComparison.OrdinalIgnoreCase));

			if (service == null)
			{
				throw new InvalidOperationException($"No service found with name '{data.ServiceName}'.");
			}

			var serviceModel = service;
			var scripts = ReadScripts(serviceModel);
			var changed = ApplyAction(scripts);
			if (!changed)
			{
				throw new InvalidOperationException($"No changes were applied for service '{data.ServiceName}'.");
			}

			WriteScripts(serviceModel, scripts);
			helper.CreateOrUpdate(service);

			view.ShowSuccess(data.ServiceName, data.Action.ToString(), scripts);
		}

		private List<string> ReadScripts(Models.Service serviceModel)
		{
			var scriptsAsString = serviceModel.Scripts;
			if (String.IsNullOrWhiteSpace(scriptsAsString))
			{
				return new List<string>();
			}

			return scriptsAsString
				.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
				.Select(script => script.Trim())
				.Where(script => !String.IsNullOrWhiteSpace(script))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();
		}

		private void WriteScripts(Models.Service serviceModel, List<string> scripts)
		{
			serviceModel.Scripts = String.Join(",", scripts.Where(script => !String.IsNullOrWhiteSpace(script)).Select(script => script.Trim()));
		}

		private bool ApplyAction(List<string> scripts)
		{
			if (data.Action == ScriptAction.Remove)
			{
				return scripts.RemoveAll(existing => String.Equals(existing, data.ScriptName, StringComparison.OrdinalIgnoreCase)) > 0;
			}

			var enteredScriptName = view.PromptScriptName(data.Action, data.ScriptName);
			if (String.IsNullOrWhiteSpace(enteredScriptName))
			{
				throw new InvalidOperationException("No script name was provided.");
			}

			if (data.Action == ScriptAction.Add)
			{
				if (scripts.Any(existing => String.Equals(existing, enteredScriptName, StringComparison.OrdinalIgnoreCase)))
				{
					return false;
				}

				scripts.Add(enteredScriptName);
				return true;
			}

			var existingIndex = scripts.FindIndex(existing => String.Equals(existing, data.ScriptName, StringComparison.OrdinalIgnoreCase));
			if (existingIndex < 0)
			{
				return false;
			}

			if (scripts.Any(existing =>
				!String.Equals(existing, scripts[existingIndex], StringComparison.OrdinalIgnoreCase) &&
				String.Equals(existing, enteredScriptName, StringComparison.OrdinalIgnoreCase)))
			{
				return false;
			}

			scripts[existingIndex] = enteredScriptName;
			return true;
		}
	}
}

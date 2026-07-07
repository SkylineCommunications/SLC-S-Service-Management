namespace SLC_SM_IAS_Manage_Service_Scripts.Presenters
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using Newtonsoft.Json;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;

	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM;

	using Skyline.DataMiner.Utils.SecureCoding.SecureSerialization.Json.Newtonsoft;

	using SLC_SM_IAS_Manage_Service_Scripts.Model;
	using SLC_SM_IAS_Manage_Service_Scripts.Views;

	using static Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement.Models;
	using static SLC_SM_IAS_Manage_Service_Scripts.Views.ServiceScriptDialog;

	internal sealed class UiPresenter
	{
		private readonly IEngine engine;
		private readonly ScriptData data;

		public UiPresenter(IEngine engine, ScriptData data)
		{
			this.engine = engine;
			this.data = data;
		}

		public void Handle()
		{
			data.ValidateInputParameters();

			var helper = new DataHelperService(engine.GetUserConnection());
			var service = GetService(helper);
			var scripts = service.ServiceScripts ?? new List<ServiceScripts>();
			if (!TryExecuteAction(scripts, service.ServiceID))
			{
				return;
			}

			service.ServiceScripts = scripts;
			helper.CreateOrUpdate(service);
		}

		private static string SerializeInputParameters(Dictionary<string, string> parameters)
		{
			if (parameters == null || parameters.Count == 0)
			{
				return null;
			}

			return JsonConvert.SerializeObject(parameters);
		}

		private static Dictionary<string, string> DeserializeInputParameters(string json)
		{
			if (String.IsNullOrWhiteSpace(json))
			{
				return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			}

			try
			{
				return SecureNewtonsoftDeserialization.DeserializeObject<Dictionary<string, string>>(json) ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			}
			catch (Exception)
			{
				return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			}
		}

		private static string GetUniqueName(List<ServiceScripts> scripts, string baseName)
		{
			if (!scripts.Any(s => String.Equals(s.Name, baseName, StringComparison.OrdinalIgnoreCase)))
			{
				return baseName;
			}

			var counter = 2;
			string candidate;
			do
			{
				candidate = String.Format("{0} ({1})", baseName, counter);
				counter++;
			}
			while (scripts.Any(s => String.Equals(s.Name, candidate, StringComparison.OrdinalIgnoreCase)));

			return candidate;
		}

		private static string GetBaseName(string name)
		{
			if (String.IsNullOrEmpty(name))
			{
				return name;
			}

			var match = System.Text.RegularExpressions.Regex.Match(name, @"^(.*)\s+\(\d+\)$");
			return match.Success ? match.Groups[1].Value : name;
		}

		private static string GetUniqueNameExcluding(List<ServiceScripts> scripts, string baseName, int excludeIndex)
		{
			if (!scripts.Where((s, i) => i != excludeIndex).Any(s => String.Equals(s.Name, baseName, StringComparison.OrdinalIgnoreCase)))
			{
				return baseName;
			}

			var counter = 2;
			string candidate;
			do
			{
				candidate = String.Format("{0} ({1})", baseName, counter);
				counter++;
			}
			while (scripts.Where((s, i) => i != excludeIndex).Any(s => String.Equals(s.Name, candidate, StringComparison.OrdinalIgnoreCase)));

			return candidate;
		}

		private static bool HasDuplicateScriptName(List<ServiceScripts> scripts, string enteredName, int currentIndex)
		{
			for (var i = 0; i < scripts.Count; i++)
			{
				if (i == currentIndex)
				{
					continue;
				}

				if (String.Equals(scripts[i].Name, enteredName, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}

			return false;
		}

		private Models.Service GetService(DataHelperService helper)
		{
			if (!Guid.TryParse(data.ServiceId, out var serviceGuid))
			{
				throw new InvalidOperationException($"Invalid service Id '{data.ServiceId}'.");
			}

			var service = helper.Read(ServiceExposers.Guid.Equal(serviceGuid)).SingleOrDefault();
			if (service == null)
			{
				throw new InvalidOperationException($"No service found with Id '{data.ServiceId}'.");
			}

			return service;
		}

		private bool TryExecuteAction(List<ServiceScripts> scripts, string serviceId)
		{
			switch (data.Action)
			{
				case ScriptAction.Remove:
					return RemoveScript(scripts);
				case ScriptAction.Add:
					return AddScript(scripts, serviceId);
				case ScriptAction.Update:
					return UpdateScript(scripts, serviceId);
				default:
					throw new InvalidOperationException($"Unsupported action '{data.Action}'.");
			}
		}

		private bool RemoveScript(List<ServiceScripts> scripts)
		{
			return scripts.RemoveAll(existing => String.Equals(existing.Name, data.ScriptName, StringComparison.OrdinalIgnoreCase)) > 0;
		}

		private bool AddScript(List<ServiceScripts> scripts, string serviceId)
		{
			var enteredScript = GetScriptFromDialog(
				dialogTitle: "Add Script",
				dialogInfoText: "Select the script to add.",
				current: null,
				serviceId: serviceId);

			if (enteredScript == null)
			{
				return false;
			}

			enteredScript.Name = GetUniqueName(scripts, enteredScript.Name);
			scripts.Add(enteredScript);
			return true;
		}

		private bool UpdateScript(List<ServiceScripts> scripts, string serviceId)
		{
			var existingIndex = scripts.FindIndex(existing => String.Equals(existing.Name, data.ScriptName, StringComparison.OrdinalIgnoreCase));
			if (existingIndex < 0)
			{
				return false;
			}

			var enteredScript = GetScriptFromDialog(
				dialogTitle: "Update Script",
				dialogInfoText: "Select the new script.",
				current: scripts[existingIndex],
				serviceId: serviceId);

			if (enteredScript == null)
			{
				return false;
			}

			var currentBaseName = GetBaseName(data.ScriptName);
			enteredScript.Name = String.Equals(enteredScript.Name, currentBaseName, StringComparison.OrdinalIgnoreCase)
				? data.ScriptName
				: GetUniqueNameExcluding(scripts, enteredScript.Name, existingIndex);

			scripts[existingIndex] = enteredScript;
			return true;
		}

		private ServiceScripts GetScriptFromDialog(string dialogTitle, string dialogInfoText, ServiceScripts current, string serviceId)
		{
			var availableScripts = data.GetScriptNamesWithServiceIdParameter();
			var dialog = CreateScriptNameDialog(dialogTitle, dialogInfoText, availableScripts, serviceId);

			if (current != null)
			{
				InitializeDialogForUpdate(dialog, availableScripts, current);
			}

			InitializeScriptSelection(dialog);
			ServiceScriptDialog.ShowDialog(dialog);

			if (!dialog.IsConfirmed)
			{
				engine.ExitSuccess("Action cancelled by user.");
				return null;
			}

			var inputParams = dialog.GetInputParameterValues();

			return new ServiceScripts
			{
				Name = dialog.ScriptName.Selected?.Trim(),
				Description = dialog.Description.Text?.Trim(),
				InputParameters = SerializeInputParameters(inputParams),
			};
		}

		private ServiceScriptsDialog CreateScriptNameDialog(string dialogTitle, string dialogInfoText, IEnumerable<string> availableScripts, string serviceId)
		{
			var dialog = new ServiceScriptsDialog(engine, availableScripts, serviceId);
			dialog.Title = dialogTitle;
			dialog.Info.Text = dialogInfoText;
			dialog.ConfirmButton.IsEnabled = false;
			return dialog;
		}

		private void InitializeDialogForUpdate(ServiceScriptsDialog dialog, IEnumerable<string> availableScripts, ServiceScripts current)
		{
			var baseName = GetBaseName(current.Name);
			var scriptMatch = availableScripts.FirstOrDefault(script => String.Equals(script, baseName, StringComparison.OrdinalIgnoreCase));
			dialog.Description.Text = current.Description ?? String.Empty;

			if (scriptMatch == null)
			{
				return;
			}

			var savedValues = DeserializeInputParameters(current.InputParameters);
			dialog.SetInputParameters(data.GetScriptInputParameters(scriptMatch), savedValues);
			dialog.ScriptName.Selected = scriptMatch;
			dialog.ConfirmButton.IsEnabled = dialog.ScriptName.Selected != ServiceScriptsDialog.DropdownPlaceholder;
		}

		private void InitializeScriptSelection(ServiceScriptsDialog dialog)
		{
			dialog.ScriptName.Changed += (sender, args) =>
			{
				dialog.ConfirmButton.IsEnabled = args.Selected != ServiceScriptsDialog.DropdownPlaceholder;

				var parameterNames = args.Selected == ServiceScriptsDialog.DropdownPlaceholder
					? new List<string>()
					: data.GetScriptInputParameters(args.Selected);

				dialog.SetInputParameters(parameterNames, savedValues: null);
				dialog.Build();
			};
		}
	}
}
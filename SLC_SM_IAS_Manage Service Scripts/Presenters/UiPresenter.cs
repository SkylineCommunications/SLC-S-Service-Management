namespace SLC_SM_IAS_Manage_Service_Scripts.Presenters
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Newtonsoft.Json;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using Skyline.DataMiner.Utils.SecureCoding.SecureSerialization.Json.Newtonsoft;
	using SLC_SM_IAS_Manage_Service_Scripts.Model;
	using SLC_SM_IAS_Manage_Service_Scripts.Views;
	using static SLC_SM_IAS_Manage_Service_Scripts.Views.ServiceScriptDialog;
	using SdmModels = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

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
			var helper = new ServiceManagementApiHelper(engine.GetUserConnection(), engine.UserLoginName);
			var service = GetService(helper);
			var scripts = service.ServiceScripts ?? new List<SdmModels.ServiceScript>();
			if (!TryExecuteAction(scripts, service.ServiceID))
			{
				return;
			}

			service.ServiceScripts = scripts;
			helper.ServiceInventory.Services.Update(service);
		}

		public void HandleApi()
		{
			var helper = new ServiceManagementApiHelper(engine.GetUserConnection(), engine.UserLoginName);
			var service = GetService(helper);
			var scripts = service.ServiceScripts ?? new List<SdmModels.ServiceScript>();
			var existingDescription = String.IsNullOrWhiteSpace(data.OriginalScriptDescription)
				? data.ScriptDescription
				: data.OriginalScriptDescription;

			switch (data.Action)
			{
				case ScriptAction.Add:
					if (scripts.Any(existing => String.Equals(existing.Description, data.ScriptDescription, StringComparison.OrdinalIgnoreCase)))
					{
						throw new InvalidOperationException("A service script with this description already exists.");
					}

					scripts.Add(new SdmModels.ServiceScript
					{
						Name = data.ScriptName,
						Description = data.ScriptDescription,
					});
					break;
				case ScriptAction.Update:
					var existingIndex = scripts.FindIndex(existing => String.Equals(existing.Description, existingDescription, StringComparison.OrdinalIgnoreCase));
					if (existingIndex < 0)
					{
						throw new InvalidOperationException("The service script to update was not found.");
					}

					if (scripts.Where((existing, index) => index != existingIndex)
						.Any(existing => String.Equals(existing.Description, data.ScriptDescription, StringComparison.OrdinalIgnoreCase)))
					{
						throw new InvalidOperationException("A service script with this description already exists.");
					}

					scripts[existingIndex].Name = data.ScriptName;
					scripts[existingIndex].Description = data.ScriptDescription;
					break;
				case ScriptAction.Remove:
					if (scripts.RemoveAll(existing => String.Equals(existing.Description, existingDescription, StringComparison.OrdinalIgnoreCase)) == 0)
					{
						throw new InvalidOperationException("The service script to remove was not found.");
					}
					break;
				default:
					throw new InvalidOperationException($"Unsupported action '{data.Action}'.");
			}

			service.ServiceScripts = scripts;
			helper.ServiceInventory.Services.Update(service);
		}

		private static string SerializeInputParameters(Dictionary<string, string> parameters)
		{
			if (parameters == null || parameters.Count == 0)
			{
				return null;
			}

			return JsonConvert.SerializeObject(parameters);
		}

		private Dictionary<string, string> DeserializeInputParameters(string json)
		{
			if (String.IsNullOrWhiteSpace(json))
			{
				return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			}

			try
			{
				return SecureNewtonsoftDeserialization.DeserializeObject<Dictionary<string, string>>(json) ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			}
			catch (Exception exception)
			{
				engine.Log("DeserializeInputParameters|Unexpected exception: " + exception);
				throw new InvalidOperationException("The stored script input parameters are invalid.", exception);
			}
		}

		private SdmModels.Service GetService(ServiceManagementApiHelper helper)
		{
			if (!Guid.TryParse(data.ServiceId, out var serviceGuid))
			{
				throw new InvalidOperationException($"Invalid service Id '{data.ServiceId}'.");
			}

			var service = helper.ServiceInventory.Services
				.Read(Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement.ServiceExposers.Identifier.Equal(serviceGuid.ToString()))
				.SingleOrDefault();
			if (service == null)
			{
				throw new InvalidOperationException($"No service found with Id '{data.ServiceId}'.");
			}

			return service;
		}

		private bool TryExecuteAction(List<SdmModels.ServiceScript> scripts, string serviceId)
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

		private bool RemoveScript(List<SdmModels.ServiceScript> scripts)
		{
			if (scripts.RemoveAll(existing => String.Equals(existing.Description, data.ScriptDescription, StringComparison.OrdinalIgnoreCase)) == 0)
			{
				throw new InvalidOperationException($"No service script with description '{data.ScriptDescription}' was found.");
			}

			return true;
		}

		private bool AddScript(List<SdmModels.ServiceScript> scripts, string serviceId)
		{
			var existingDescriptions = scripts
				.Select(s => s.Description)
				.Where(d => !String.IsNullOrWhiteSpace(d));

			var enteredScript = GetScriptFromDialog(
				dialogTitle: "Add Script",
				dialogInfoText: "Select the script to add.",
				current: null,
				serviceId: serviceId,
				existingDescriptions: existingDescriptions,
				excludedDescription: null);

			if (enteredScript == null)
			{
				return false;
			}

			scripts.Add(enteredScript);
			return true;
		}

		private bool UpdateScript(List<SdmModels.ServiceScript> scripts, string serviceId)
		{
			var existingIndex = scripts.FindIndex(existing => String.Equals(existing.Description, data.ScriptDescription, StringComparison.OrdinalIgnoreCase));
			if (existingIndex < 0)
			{
				throw new InvalidOperationException($"No service script with description '{data.ScriptDescription}' was found.");
			}

			var existingDescriptions = scripts
				.Select(s => s.Description)
				.Where(d => !String.IsNullOrWhiteSpace(d));

			var enteredScript = GetScriptFromDialog(
				dialogTitle: "Update Script",
				dialogInfoText: "Select the new script.",
				current: scripts[existingIndex],
				serviceId: serviceId,
				existingDescriptions: existingDescriptions,
				excludedDescription: data.ScriptDescription);

			if (enteredScript == null)
			{
				return false;
			}

			scripts[existingIndex] = enteredScript;
			return true;
		}

		private SdmModels.ServiceScript GetScriptFromDialog(
			string dialogTitle,
			string dialogInfoText,
			SdmModels.ServiceScript current,
			string serviceId,
			IEnumerable<string> existingDescriptions,
			string excludedDescription)
		{
			var availableScripts = data.GetScriptNamesWithServiceIdParameter();
			var dialog = new ServiceScriptsDialog(engine, availableScripts, serviceId);
			dialog.Title = dialogTitle;
			dialog.Info.Text = dialogInfoText;
			dialog.SetExistingDescriptions(existingDescriptions, excludedDescription);

			if (current != null)
			{
				InitializeDialogForUpdate(dialog, availableScripts, current);
				dialog.RefreshConfirmButton();
			}

			InitializeScriptSelection(dialog);
			ServiceScriptDialog.ShowDialog(dialog);

			if (!dialog.IsConfirmed)
			{
				engine.ExitSuccess("Action cancelled by user.");
				return null;
			}

			var inputParams = dialog.GetInputParameterValues();

			return new SdmModels.ServiceScript
			{
				Name = dialog.ScriptName.Selected?.Trim(),
				Description = dialog.Description.Text?.Trim(),
				InputParameters = SerializeInputParameters(inputParams),
			};
		}

		private void InitializeDialogForUpdate(ServiceScriptsDialog dialog, IEnumerable<string> availableScripts, SdmModels.ServiceScript current)
		{
			var scriptMatch = availableScripts.FirstOrDefault(script => String.Equals(script, current.Name, StringComparison.OrdinalIgnoreCase));
			dialog.Description.Text = current.Description ?? String.Empty;

			if (scriptMatch == null)
			{
				return;
			}

			var savedValues = DeserializeInputParameters(current.InputParameters);
			dialog.SetInputParameters(data.GetScriptInputParameters(scriptMatch), savedValues);
			dialog.ScriptName.Selected = scriptMatch;
		}

		private void InitializeScriptSelection(ServiceScriptsDialog dialog)
		{
			dialog.ScriptName.Changed += (sender, args) =>
			{
				var parameterNames = args.Selected == ServiceScriptsDialog.DropdownPlaceholder
					? new List<string>()
					: data.GetScriptInputParameters(args.Selected);

				dialog.SetInputParameters(parameterNames, savedValues: null);
				dialog.Build();
			};
		}
	}
}
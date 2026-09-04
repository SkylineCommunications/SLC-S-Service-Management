/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE        VERSION        AUTHOR            COMMENTS

02/09/2026  1.0.0.1        SKA               Initial version
02/09/2026  1.0.0.2        SKA               Select sub-script parameters explicitly and surface sub-script error messages.

Centralized import orchestrator.

This script takes no input parameters. It executes every Service Management /
MediaOps import script in the dependency order required so that all created
data ends up correctly linked:

	0. Clear Import Data    (service orders/items, inventory, specifications)
	1. Service Categories   (base taxonomy)
	2. Configuration Studio (parameters, profile definitions and profiles)
	3. Service Catalog      (service specifications, reference Configuration Studio)
	4. Resource Studio      (capabilities, capacities, resources and pools)
	5. Workflows            (workflow templates, reference Resource Studio)
	6. Service Inventory    (services and service items, reference Categories/Config Studio)
	7. Service Orders       (reference Service Inventory)
	8. Jobs                 (reference Workflows, Resource Studio and Service Inventory)

Every sub-script reads its JSON payload(s) from the DMA common documents folder
(C:\Skyline DataMiner\Documents\DMA_COMMON_DOCUMENTS). Because the import
sub-scripts throw when a declared script parameter has no value, this
orchestrator explicitly selects every parameter of every sub-script with its
DMA common documents path before starting it.
****************************************************************************
*/
namespace SLC_SM_Import_All
{
	using System;
	using System.Collections.Generic;
	using System.IO;

	using Skyline.DataMiner.Automation;

	/// <summary>
	/// Represents a DataMiner Automation script.
	/// </summary>
	public class Script
	{
		private const string CommonDocumentsFolder = @"C:\Skyline DataMiner\Documents\DMA_COMMON_DOCUMENTS";

		/// <summary>
		/// The script entry point.
		/// </summary>
		/// <param name="engine">Link with SLAutomation process.</param>
		public void Run(IEngine engine)
		{
			try
			{
				RunSafe(engine);
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
			}
			catch (Exception e)
			{
				engine.ExitFail($"Run|{e.Message}");
			}
		}

		private static void RunSafe(IEngine engine)
		{
			List<ImportStep> steps = BuildSteps();
			var failures = new List<string>();

			for (int i = 0; i < steps.Count; i++)
			{
				ImportStep step = steps[i];
				engine.GenerateInformation($"[Import All] ({i + 1}/{steps.Count}) Starting '{step.ScriptName}'...");
				ExecuteImportScript(engine, step, failures);
			}

			if (failures.Count > 0)
			{
				engine.ExitFail(
					$"[Import All] Completed with {failures.Count} failing import(s): {String.Join(" | ", failures)}");
				return;
			}

			engine.GenerateInformation(
				$"[Import All] Completed successfully. {steps.Count} import scripts executed in order.");
		}

		/// <summary>
		/// Builds the ordered list of import scripts together with the parameters each one must receive.
		/// Every file-path parameter is set to the corresponding file in the DMA common documents folder;
		/// this guarantees the sub-scripts' <c>ReadScriptParamFromApp</c> calls receive a non-null value.
		/// </summary>
		private static List<ImportStep> BuildSteps()
		{
			string categories = Path.Combine(CommonDocumentsFolder, "categories.json");
			string configuration = Path.Combine(CommonDocumentsFolder, "configuration-studio.json");
			string catalog = Path.Combine(CommonDocumentsFolder, "service-catalog.json");
			string resourceStudio = Path.Combine(CommonDocumentsFolder, "resource-studio.json");
			string workflows = Path.Combine(CommonDocumentsFolder, "workflows.json");
			string inventory = Path.Combine(CommonDocumentsFolder, "service-inventory.json");
			string orders = Path.Combine(CommonDocumentsFolder, "service-orders.json");
			string jobs = Path.Combine(CommonDocumentsFolder, "jobs.json");

			return new List<ImportStep>
			{
				new ImportStep(
					"SLC_SM_Clear Import Data",
					new Dictionary<string, string>()),
				new ImportStep(
					"SLC_SM_Import Service Categories",
					new Dictionary<string, string>
					{
						{ "JSON File Path", categories },
					}),
				new ImportStep(
					"SLC_SM_Import Configuration Studio",
					new Dictionary<string, string>
					{
						{ "JSON File Path", configuration },
						{ "Parameter ID", "NA" },
					}),
				new ImportStep(
					"SLC_SM_Import Service Catalog",
					new Dictionary<string, string>
					{
						{ "JSON File Path", catalog },
						{ "Configuration Studio JSON File Path", configuration },
					}),
				new ImportStep(
					"SLC_SM_Import Resource Studio",
					new Dictionary<string, string>
					{
						{ "JSON File Path", resourceStudio },
					}),
				new ImportStep(
					"SLC_SM_Import Workflows",
					new Dictionary<string, string>
					{
						{ "JSON File Path", workflows },
						{ "Resource Studio JSON File Path", resourceStudio },
					}),
				new ImportStep(
					"SLC_SM_Import Service Inventory",
					new Dictionary<string, string>
					{
						{ "JSON File Path", inventory },
						{ "Categories JSON File Path", categories },
						{ "Configuration Studio JSON File Path", configuration },
						{ "Workflows JSON File Path", workflows },
					}),
				new ImportStep(
					"SLC_SM_Import Service Orders",
					new Dictionary<string, string>
					{
						{ "JSON File Path", orders },
						{ "Service Inventory JSON File Path", inventory },
						{ "Categories JSON File Path", categories },
					}),
				new ImportStep(
					"SLC_SM_Import Jobs",
					new Dictionary<string, string>
					{
						{ "JSON File Path", jobs },
						{ "Workflows JSON File Path", workflows },
						{ "Resource Studio JSON File Path", resourceStudio },
						{ "Service Inventory JSON File Path", inventory },
					}),
			};
		}

		private static void ExecuteImportScript(IEngine engine, ImportStep step, List<string> failures)
		{
			SubScriptOptions subScript;
			try
			{
				subScript = engine.PrepareSubScript(step.ScriptName);
			}
			catch (Exception e)
			{
				failures.Add($"{step.ScriptName}: could not be prepared ({e.Message}). Is it deployed on this DMA?");
				engine.GenerateInformation($"[Import All] ERR: '{step.ScriptName}' could not be prepared: {e.Message}");
				return;
			}

			foreach (KeyValuePair<string, string> parameter in step.Parameters)
			{
				subScript.SelectScriptParam(parameter.Key, parameter.Value);
			}

			subScript.Synchronous = true;
			subScript.InheritScriptOutput = true;

			try
			{
				subScript.StartScript();
			}
			catch (Exception e)
			{
				string details = SafeErrorMessages(subScript);
				string message = String.IsNullOrWhiteSpace(details) ? e.Message : $"{e.Message} :: {details}";
				failures.Add($"{step.ScriptName}: {message}");
				engine.GenerateInformation($"[Import All] ERR: '{step.ScriptName}' failed: {message}");
				return;
			}

			if (subScript.HadError)
			{
				string details = SafeErrorMessages(subScript);
				failures.Add($"{step.ScriptName}: {details}");
				engine.GenerateInformation($"[Import All] ERR: '{step.ScriptName}' reported an error: {details}");
				return;
			}

			engine.GenerateInformation($"[Import All] '{step.ScriptName}' finished.");
		}

		private static string SafeErrorMessages(SubScriptOptions subScript)
		{
			try
			{
				string[] messages = subScript.GetErrorMessages();
				if (messages != null && messages.Length > 0)
				{
					return String.Join(" | ", messages);
				}
			}
			catch
			{
				// Ignore: error messages are best-effort diagnostics only.
			}

			return String.Empty;
		}

		private sealed class ImportStep
		{
			public ImportStep(string scriptName, Dictionary<string, string> parameters)
			{
				ScriptName = scriptName;
				Parameters = parameters;
			}

			public string ScriptName { get; }

			public Dictionary<string, string> Parameters { get; }
		}
	}
}

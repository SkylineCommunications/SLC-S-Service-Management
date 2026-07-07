namespace SLCSMASExecuteScript
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;

	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM;

	using Skyline.DataMiner.Utils.SecureCoding.SecureSerialization.Json.Newtonsoft;

	using static Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement.Models;

	/// <summary>
	/// Represents a DataMiner Automation script.
	/// </summary>
	public class Script
	{
		/// <summary>
		/// The script entry point.
		/// </summary>
		/// <param name="engine">Link with SLAutomation process.</param>
		public static void Run(IEngine engine)
		{
			try
			{
				RunSafe(engine);
			}
			catch (ScriptAbortException)
			{
				// Catch normal abort exceptions (engine.ExitFail or engine.ExitSuccess)
				throw;
			}
			catch (ScriptForceAbortException)
			{
				// Catch forced abort exceptions, caused via external maintenance messages.
				throw;
			}
			catch (ScriptTimeoutException)
			{
				// Catch timeout exceptions for when a script has been running for too long.
				throw;
			}
			catch (InteractiveUserDetachedException)
			{
				// Catch a user detaching from the interactive script by closing the window.
				// Only applicable for interactive scripts, can be removed for non-interactive scripts.
				throw;
			}
			catch (Exception e)
			{
				engine.ExitFail("Run|Something went wrong: " + e);
			}
		}

		private static void RunSafe(IEngine engine)
		{
			var serviceId = ReadParam(engine, "Service Id");
			var scriptDescription = ReadParam(engine, "Script Description");

			if (String.IsNullOrWhiteSpace(serviceId))
			{
				throw new InvalidOperationException("No service Id was provided.");
			}

			if (String.IsNullOrWhiteSpace(scriptDescription))
			{
				throw new InvalidOperationException("No script description was provided.");
			}

			if (!Guid.TryParse(serviceId, out var serviceGuid))
			{
				throw new InvalidOperationException($"Invalid service Id '{serviceId}'.");
			}

			var helper = new DataHelperService(engine.GetUserConnection());
			var service = helper.Read(ServiceExposers.Guid.Equal(serviceGuid)).SingleOrDefault();
			if (service == null)
			{
				throw new InvalidOperationException($"No service found with Id '{serviceId}'.");
			}

			var scripts = service.ServiceScripts ?? new List<ServiceScripts>();
			var entry = scripts.FirstOrDefault(s => String.Equals(s.Description, scriptDescription, StringComparison.OrdinalIgnoreCase));
			if (entry == null)
			{
				throw new InvalidOperationException($"No script entry with description '{scriptDescription}' found on service '{service.Name}'.");
			}

			if (String.IsNullOrWhiteSpace(entry.Name))
			{
				throw new InvalidOperationException($"The script entry with description '{scriptDescription}' has no script name configured.");
			}

			var inputParameters = DeserializeInputParameters(entry.InputParameters);

			var subScript = engine.PrepareSubScript(entry.Name);
			subScript.Synchronous = true;
			subScript.ExtendedErrorInfo = true;
			subScript.InheritScriptOutput = true;

			subScript.SelectScriptParam("Service ID", serviceId);

			foreach (var param in inputParameters)
			{
				subScript.SelectScriptParam(param.Key, param.Value);
			}

			subScript.StartScript();

			if (subScript.HadError)
			{
				throw new InvalidOperationException($"Script '{entry.Name}' failed: {String.Join(" -> ", subScript.GetErrorMessages())}");
			}
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

		private static string ReadParam(IEngine engine, string name)
		{
			var raw = engine.GetScriptParam(name)?.Value?.Trim();
			if (String.IsNullOrWhiteSpace(raw))
			{
				return raw;
			}

			if (raw.StartsWith("[") && raw.EndsWith("]"))
			{
				var values = SecureNewtonsoftDeserialization.DeserializeObject<List<string>>(raw);
				return values != null && values.Count > 0 ? values[0]?.Trim() : null;
			}

			return raw;
		}
	}
}

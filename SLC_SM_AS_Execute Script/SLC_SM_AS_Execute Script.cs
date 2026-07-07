namespace SLCSMASExecuteScript
{
	using System;
	using System.Text.RegularExpressions;

	using Skyline.DataMiner.Automation;

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
				throw;
			}
			catch (ScriptForceAbortException)
			{
				throw;
			}
			catch (ScriptTimeoutException)
			{
				throw;
			}
			catch (InteractiveUserDetachedException)
			{
				throw;
			}
			catch (Exception e)
			{
				engine.ExitFail("Run|Something went wrong: " + e);
			}
		}

		private static void RunSafe(IEngine engine)
		{
			var rawScriptName = engine.GetScriptParam("Script Name")?.Value?.Trim();
			var scriptName = ParseScriptName(rawScriptName);

			if (String.IsNullOrWhiteSpace(scriptName))
			{
				throw new InvalidOperationException("No script name was provided.");
			}

			var subScript = engine.PrepareSubScript(scriptName);
			subScript.Synchronous = true;
			subScript.ExtendedErrorInfo = true;
			subScript.InheritScriptOutput = true;
			subScript.StartScript();

			if (subScript.HadError)
			{
				throw new InvalidOperationException($"Script '{scriptName}' failed: " + String.Join(" -> ", subScript.GetErrorMessages()));
			}
		}

		/// <summary>
		/// Handles both plain script names ("My Script") and JSON array format (["My Script"])
		/// which is how low-code apps pass GQI column values to scripts.
		/// </summary>
		private static string ParseScriptName(string rawValue)
		{
			if (String.IsNullOrWhiteSpace(rawValue))
			{
				return String.Empty;
			}

			var trimmed = rawValue.Trim();

			// Low-code app passes GQI column values as a JSON array: ["Script Name"]
			if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
			{
				var match = Regex.Match(trimmed, @"\[""([^""]+)""\]");
				if (match.Success)
				{
					return match.Groups[1].Value.Trim();
				}
			}

			return trimmed;
		}
	}
}

namespace SLCSMASExecuteScript
{
	using System;

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
			var scriptName = engine.GetScriptParam("Script Name")?.Value?.Trim();

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
	}
}

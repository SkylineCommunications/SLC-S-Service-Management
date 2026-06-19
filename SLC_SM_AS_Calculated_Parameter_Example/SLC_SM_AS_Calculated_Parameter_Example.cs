namespace SLCSMASCalculatedParameterExample
{
	using System;

	using Newtonsoft.Json;

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
		public void Run(IEngine engine)
		{
			try
			{
				RunSafe(engine);
			}
			catch (ScriptAbortException)
			{
				// Catch normal abort exceptions (engine.ExitFail or engine.ExitSuccess)
				throw; // Comment if it should be treated as a normal exit of the script.
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

		private void RunSafe(IEngine engine)
		{
			try
			{
				// Read the JSON input passed by the caller.
				// Structure: { "trigger": { "profile": "<name>", "parameter": "<name>" }, "serviceConfiguration": { ... } }
				var input = engine.GetScriptParam("Input")?.Value ?? String.Empty;
				engine.GenerateInformation($"LinkedScript|Received input: {input}");

				// Implement the logic here, e.g. parse the trigger/serviceConfiguration and determine
				// which parameter values need to be updated based on the received configuration.
				var updates = new[]
				{
					new
					{
						profileName = "Packager Lineup",
						paramLabel = "GO IP PreProd",
						value = "test value from script",
					},
				};

				// Return the result as a JSON array of parameter updates.
				// Structure: [ { "profileName": "<name>", "paramLabel": "<label>", "value": "<new value>" }, ... ]
				var result = JsonConvert.SerializeObject(updates);
				engine.GenerateInformation(result);
				engine.AddScriptOutput("Result", result);
			}
			catch (Exception ex)
			{
				engine.Log($"LinkedScript|Exception: {ex.Message}");
				engine.AddScriptOutput("Result", "[]");
			}
		}
	}
}

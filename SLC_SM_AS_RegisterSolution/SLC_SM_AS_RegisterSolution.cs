using System;
using System.Linq;

using Skyline.DataMiner.Automation;
using Skyline.DataMiner.Net.Messages.SLDataGateway;
using Skyline.DataMiner.SDM.Registration;

/// <summary>
///     DataMiner Script Class.
///     Registers (or updates) this solution in the SDM solution registry so it shows up
///     alongside other registered solutions (e.g. "SDM Registrations", "SDM Abstractions").
/// </summary>
internal class Script
{
	/// <summary>
	///     The script entry point.
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
			throw;
		}
		catch (Exception e)
		{
			engine.ExitFail("Run|Something went wrong: " + e);
		}
	}

	private void RunSafe(IEngine engine)
	{
		var id = engine.GetScriptParam("Id")?.Value;
		if (String.IsNullOrWhiteSpace(id))
		{
			throw new InvalidOperationException("ID cannot be null or empty");
		}

		var displayName = engine.GetScriptParam("DisplayName")?.Value;
		if (String.IsNullOrWhiteSpace(displayName))
		{
			throw new InvalidOperationException("Display name cannot be null or empty");
		}

		var version = engine.GetScriptParam("Version")?.Value;
		if (String.IsNullOrWhiteSpace(version))
		{
			throw new InvalidOperationException("Version cannot be null or empty");
		}

		var registrar = engine.GetSdmRegistrar();
		var registration = registrar.Solutions.Read(SolutionRegistrationExposers.ID.Equal(id)).FirstOrDefault();
		if (registration == null)
		{
			registration = new SolutionRegistration
			{
				ID = id,
				DisplayName = displayName,
				Version = version,
			};
		}
		else
		{
			registration.DisplayName = displayName;
			registration.Version = version;
		}

		registrar.Solutions.CreateOrUpdate(new[] { registration });
	}
}

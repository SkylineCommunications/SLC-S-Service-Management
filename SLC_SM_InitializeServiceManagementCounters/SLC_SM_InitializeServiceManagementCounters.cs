namespace SLCSMInitializeServiceManagementCounters
{
	using System;
	using Skyline.DataMiner.Automation;
	using SLC_SM_Common.IdGeneration;

	/// <summary>
	/// Initializes the Service Management Mediation counters from existing records.
	/// </summary>
	public class Script
	{
		/// <summary>
		/// Runs the counter initialization.
		/// </summary>
		/// <param name="engine">Automation engine.</param>
		public void Run(IEngine engine)
		{
			try
			{
				new ServiceManagementIdGenerator().InitializeCounters(engine);
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
			catch (Exception ex)
			{
				engine.Log("Run|Unexpected exception: " + ex.ToString());
				engine.ExitFail("Service Management counter initialization failed.");
			}
		}
	}
}

namespace SLC_SM_RT_ScheduledConfigurationActivation.TestCases
{
	using System;
	using System.Collections.Generic;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	/// <summary>
	/// Schedules a configuration version that starts in the future without activating it.
	/// </summary>
	internal sealed class ScheduleFutureVersionTestCase : TestCase
	{
		private readonly ScheduledConfigurationActivationTestData testData;

		public ScheduleFutureVersionTestCase(ScheduledConfigurationActivationTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			var scriptParameters = new Dictionary<string, string>
			{
				{ "DOM ID", testData.ServiceId },
				{ "Payload", testData.BuildPlannedVersionPayload(activate: false) },
			};

			try
			{
				TestTools.ExecuteSubScript(this, engine, Script.ScriptUnderTest, scriptParameters);
			}
			finally
			{
				// Record whatever was created, also on failure, so that cleanup can remove it.
				testData.CapturePlannedVersion();
			}
		}

		public override void PerformCleanup(IEngine engine)
		{
			// Cleanup of every fixture is owned by the test data and triggered by the last test case.
		}
	}
}

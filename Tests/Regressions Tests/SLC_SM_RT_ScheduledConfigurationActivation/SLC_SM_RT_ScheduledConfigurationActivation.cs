/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE        VERSION        AUTHOR            COMMENTS

17/11/2026  1.0.0.1        SKA, Skyline      Initial version
****************************************************************************
*/

namespace SLC_SM_RT_ScheduledConfigurationActivation
{
	using System;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Reporting;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using SLC_SM_RT_ScheduledConfigurationActivation.TestCases;

	/// <summary>
	/// Regression test for scheduled configuration versions of "SLC_SM_IAS_Service_Configuration" (non-interactive payload).
	/// Saves an active current version, schedules a future-dated version without activating it, verifies the versioning
	/// and finally activates the planned version.
	/// </summary>
	/// <remarks>
	/// The product has no time-driven activation: a future StartDate only marks the version as planned and it becomes
	/// active through an explicit activation. This test covers that deterministic behaviour.
	/// </remarks>
	public class Script
	{
		/// <summary>
		/// The Automation script this regression test verifies.
		/// </summary>
		public const string ScriptUnderTest = "SLC_SM_IAS_Service_Configuration";

		private const string TestArea = "ServiceInventory";
		private const string TestName = "ScheduledConfigurationActivation";
		private const string TestDescription = "Schedules a future service configuration version, verifies the active version is kept until the planned version is explicitly activated.";

		/// <summary>
		/// Entry point.
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
				engine.ExitFail("Run|Something went wrong: " + ex);
			}
		}

		private static void RunSafe(IEngine engine)
		{
			var testData = new ScheduledConfigurationActivationTestData(engine);

			var test = new Test(engine, TestTools.GenerateTestName(TestArea, TestName), TestDescription)
			{
				// The test report is returned as JSON in the "report" script output. The QAPortal element that
				// launches this script picks it up and uploads it to the QA Portal. Failed test cases are recorded
				// in that report, so the script itself always ends successfully.
				PublishingMode = ReportPublishingMode.QaPortalScriptOutput,
				FailScriptOnFailure = false,
			};

			test.AddTestCase(
				new CreateFixturesTestCase(testData),
				new SaveCurrentVersionTestCase(testData),
				new ScheduleFutureVersionTestCase(testData),
				new ValidateScheduledVersionTestCase(testData),
				new ActivatePlannedVersionTestCase(testData),
				new ValidateActivationTestCase(testData));

			test.Run();
		}
	}
}

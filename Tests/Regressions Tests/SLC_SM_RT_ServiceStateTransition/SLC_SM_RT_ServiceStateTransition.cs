/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE        VERSION        AUTHOR            COMMENTS

28/09/2026  1.0.0.1        SKA, Skyline      Initial version
****************************************************************************
*/

namespace SLC_SM_RT_ServiceStateTransition
{
	using System;

	using DomHelpers.SlcServicemanagement;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Reporting;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using SLC_SM_RT_ServiceStateTransition.TestCases;

	using Statuses = DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Service_Behavior.StatusesEnum;

	/// <summary>
	/// Regression test for "Service_StateTransitions".
	/// Walks a single service through its complete lifecycle and reports every transition separately.
	/// </summary>
	public class Script
	{
		/// <summary>
		/// The Automation script this regression test verifies.
		/// </summary>
		public const string ScriptUnderTest = "Service_StateTransitions";

		private const string TestArea = "ServiceInventory";
		private const string TestName = "ServiceStateTransition";
		private const string TestDescription = "Walks a service through New, Designed, Reserved, Active and Terminated.";

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
			var testData = new ServiceStateTransitionTestData(engine);

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
				new TransitionServiceTestCase(testData, "New", "Designed", Statuses.Designed.ToString()),
				new TransitionServiceTestCase(testData, "Designed", "Reserved", Statuses.Reserved.ToString()),
				new TransitionServiceTestCase(testData, "Reserved", "Active", Statuses.Active.ToString()),
				new TransitionServiceTestCase(testData, "Active", "Terminated", Statuses.Terminated.ToString(), cleanUpAfterwards: true));

			test.Run();
		}
	}
}

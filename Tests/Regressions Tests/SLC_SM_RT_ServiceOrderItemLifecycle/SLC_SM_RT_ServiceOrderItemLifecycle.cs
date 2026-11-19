/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE        VERSION        AUTHOR            COMMENTS

28/09/2026  1.0.0.1        SKA, Skyline      Initial version
****************************************************************************
*/

namespace SLC_SM_RT_ServiceOrderItemLifecycle
{
	using System;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Reporting;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using SLC_SM_RT_ServiceOrderItemLifecycle.TestCases;

	/// <summary>
	/// Regression test for the happy path lifecycle of a service order item.
	/// The existing regression test only covers New to Acknowledged, this one walks the order item
	/// all the way to Completed.
	/// </summary>
	public class Script
	{
		/// <summary>
		/// The Automation script this regression test verifies.
		/// </summary>
		public const string ScriptUnderTest = "ServiceOrderItem_StateTranstitions";

		private const string TestArea = "ServiceOrder";
		private const string TestName = "ServiceOrderItemLifecycle";
		private const string TestDescription = "Walks a service order item from New over Acknowledged and In Progress to Completed.";

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
			var testData = new ServiceOrderFlowTestData(engine);

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
				new TransitionOrderItemTestCase(testData, "New", "Acknowledged", "Acknowledged"),
				new TransitionOrderItemTestCase(testData, "Acknowledged", "InProgress", "InProgress"),
				new TransitionOrderItemTestCase(testData, "InProgress", "Completed", "Completed", cleanUpAfterwards: true));

			test.Run();
		}
	}
}

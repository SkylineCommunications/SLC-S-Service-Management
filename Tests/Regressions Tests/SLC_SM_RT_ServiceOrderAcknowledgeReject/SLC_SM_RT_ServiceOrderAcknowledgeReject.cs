/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE        VERSION        AUTHOR            COMMENTS

17/11/2026  1.0.0.1        SKA, Skyline      Initial version
****************************************************************************
*/

namespace SLC_SM_RT_ServiceOrderAcknowledgeReject
{
	using System;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Reporting;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using SLC_SM_RT_ServiceOrderAcknowledgeReject.TestCases;

	/// <summary>
	/// Regression test for acknowledging and rejecting service orders at order level with "ServiceOrder_StateTranstitions".
	/// Order A is acknowledged, order B is rejected while New and order C is rejected while Acknowledged.
	/// The transitions must propagate to all order items, and transitions out of Rejected must be refused.
	/// </summary>
	public class Script
	{
		/// <summary>
		/// The Automation script this regression test verifies.
		/// </summary>
		public const string ScriptUnderTest = "ServiceOrder_StateTranstitions";

		private const string TestArea = "ServiceOrder";
		private const string TestName = "ServiceOrderAcknowledgeReject";
		private const string TestDescription = "Verifies acknowledging and rejecting a service order at order level propagates to all of its order items and that a rejected order cannot be transitioned further.";

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
			var testData = new ServiceOrderAcknowledgeRejectTestData(engine);

			var test = new Test(engine, TestTools.GenerateTestName(TestArea, TestName), TestDescription)
			{
				// The test report is returned as JSON in the "report" script output. The QAPortal element that
				// launches this script picks it up and uploads it to the QA Portal. Failed test cases are recorded
				// in that report, so the script itself always ends successfully.
				PublishingMode = ReportPublishingMode.QaPortalScriptOutput,
				FailScriptOnFailure = false,
			};

			// Order A: New -> Acknowledged propagates to every order item.
			// Order B: New -> Rejected rejects every order item and stores the rejection reason. Transitions out of Rejected
			// are refused, and a supported transition with a stale previous state must not move the order or its items.
			// Order C: New -> Acknowledged -> Rejected rejects every (acknowledged) order item.
			test.AddTestCase(
				new CreateFixturesTestCase(testData),
				new TransitionOrderTestCase(testData, testData.OrderA, "New", "Acknowledged", "Acknowledged", "Acknowledged"),
				new TransitionOrderTestCase(testData, testData.OrderB, "New", "Rejected", "Rejected", "Rejected"),
				new ValidateRejectionInfoTestCase(testData, testData.OrderB),
				new RefusedOrderTransitionTestCase(testData, testData.OrderB, "Rejected", "InProgress", expectScriptError: true),
				new RefusedOrderTransitionTestCase(testData, testData.OrderB, "Rejected", "Acknowledged", expectScriptError: true),
				new RefusedOrderTransitionTestCase(testData, testData.OrderB, "New", "Acknowledged", expectScriptError: false),
				new TransitionOrderTestCase(testData, testData.OrderC, "New", "Acknowledged", "Acknowledged", "Acknowledged"),
				new TransitionOrderTestCase(testData, testData.OrderC, "Acknowledged", "Rejected", "Rejected", "Rejected"),
				new ValidateRejectionInfoTestCase(testData, testData.OrderC),
				new RefusedOrderTransitionTestCase(testData, testData.OrderC, "Rejected", "InProgress", expectScriptError: true));

			test.Run();
		}
	}
}

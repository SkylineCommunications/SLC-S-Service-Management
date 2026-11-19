/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE        VERSION        AUTHOR            COMMENTS

17/11/2026  1.0.0.1        SKA, Skyline      Initial version
****************************************************************************
*/

namespace SLC_SM_RT_ServiceOrderItemRejection
{
	using System;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Reporting;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using SLC_SM_RT_ServiceOrderItemRejection.TestCases;

	/// <summary>
	/// Regression test for rejecting service order items with "ServiceOrderItem_StateTranstitions".
	/// Order A holds two order items: one is rejected while New, the other while Acknowledged.
	/// Order B holds one order item that is acknowledged (which acknowledges the order) and then rejected.
	/// Rejecting an order item never changes the status of its parent order; acknowledging an order item only
	/// acknowledges the parent order once all of its order items are Acknowledged.
	/// </summary>
	public class Script
	{
		/// <summary>
		/// The Automation script this regression test verifies.
		/// </summary>
		public const string ScriptUnderTest = "ServiceOrderItem_StateTranstitions";

		private const string TestArea = "ServiceOrder";
		private const string TestName = "ServiceOrderItemRejection";
		private const string TestDescription = "Verifies service order items can be rejected from New and Acknowledged, that rejected items cannot be transitioned further and how the parent order status follows.";

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
			var testData = new ServiceOrderItemRejectionTestData(engine);
			OrderFixture orderA = testData.OrderA;
			OrderFixture orderB = testData.OrderB;

			var test = new Test(engine, TestTools.GenerateTestName(TestArea, TestName), TestDescription)
			{
				// The test report is returned as JSON in the "report" script output. The QAPortal element that
				// launches this script picks it up and uploads it to the QA Portal. Failed test cases are recorded
				// in that report, so the script itself always ends successfully.
				PublishingMode = ReportPublishingMode.QaPortalScriptOutput,
				FailScriptOnFailure = false,
			};

			// Order A, item 1: New -> Rejected (order stays New), further transitions out of Rejected are refused.
			// Order A, item 2: New -> Acknowledged (order stays New: item 1 is not Acknowledged), then Acknowledged -> Rejected
			// (all items Rejected, the order still stays New: item rejection is not propagated to the order).
			// Order B, only item: New -> Acknowledged (acknowledges the order), then Acknowledged -> Rejected (order stays Acknowledged).
			test.AddTestCase(
				new CreateFixturesTestCase(testData),
				new TransitionOrderItemTestCase(testData, orderA, 0, "New", "Rejected", "Rejected", "New"),
				new RefusedOrderItemTransitionTestCase(testData, orderA, 0, "Rejected", "Acknowledged"),
				new TransitionOrderItemTestCase(testData, orderA, 1, "New", "Acknowledged", "Acknowledged", "New"),
				new TransitionOrderItemTestCase(testData, orderA, 1, "Acknowledged", "Rejected", "Rejected", "New"),
				new TransitionOrderItemTestCase(testData, orderB, 0, "New", "Acknowledged", "Acknowledged", "Acknowledged"),
				new TransitionOrderItemTestCase(testData, orderB, 0, "Acknowledged", "Rejected", "Rejected", "Acknowledged"),
				new RefusedOrderItemTransitionTestCase(testData, orderB, 0, "Rejected", "InProgress"));

			test.Run();
		}
	}
}

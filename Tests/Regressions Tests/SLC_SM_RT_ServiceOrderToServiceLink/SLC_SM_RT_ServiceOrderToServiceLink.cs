/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE        VERSION        AUTHOR            COMMENTS

17/11/2026  1.0.0.1        SKA, Skyline      Initial version
****************************************************************************
*/

namespace SLC_SM_RT_ServiceOrderToServiceLink
{
	using System;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Reporting;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using SLC_SM_RT_ServiceOrderToServiceLink.TestCases;

	/// <summary>
	/// Regression test for the order-level lifecycle of "ServiceOrder_StateTranstitions" creating services.
	/// Moving an acknowledged order to InProgress moves every order item to InProgress and creates a service for each of them
	/// (through "SLC_SM_Create Service Inventory Item"). Completing all order items completes the order.
	/// </summary>
	public class Script
	{
		/// <summary>
		/// The Automation script this regression test verifies.
		/// </summary>
		public const string ScriptUnderTest = "ServiceOrder_StateTranstitions";

		private const string TestArea = "ServiceOrder";
		private const string TestName = "ServiceOrderToServiceLink";
		private const string TestDescription = "Verifies moving a service order to InProgress creates a service per order item, linked to the order item and its specification, and that completing all order items completes the order.";

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
			var testData = new ServiceOrderToServiceLinkTestData(engine);
			OrderFixture order = testData.Order;

			var test = new Test(engine, TestTools.GenerateTestName(TestArea, TestName), TestDescription)
			{
				// The test report is returned as JSON in the "report" script output. The QAPortal element that
				// launches this script picks it up and uploads it to the QA Portal. Failed test cases are recorded
				// in that report, so the script itself always ends successfully.
				PublishingMode = ReportPublishingMode.QaPortalScriptOutput,
				FailScriptOnFailure = false,
			};

			// 1. Order New -> Acknowledged acknowledges both order items.
			// 2. Order Acknowledged -> InProgress moves both order items to InProgress and creates a service for each of them.
			//    Any script error fails: the script under test rethrows every sub script error as a generic "Script failed".
			// 3. Each order item links to its own new service, created from the order item and its specification.
			// 4. Completing item 1 keeps the order InProgress, and the order cannot be completed while item 2 is still InProgress.
			// 5. Completing item 2 completes the order; the very same services stay linked and untouched.
			test.AddTestCase(
				new CreateFixturesTestCase(testData),
				new TransitionOrderTestCase(testData, order, "New", "Acknowledged", "Acknowledged", "Acknowledged"),
				new TransitionOrderTestCase(testData, order, "Acknowledged", "InProgress", "InProgress", "InProgress"),
				new ValidateCreatedServicesTestCase(testData, String.Empty),
				new TransitionOrderItemTestCase(testData, order, 0, "InProgress", "Completed", "Completed", "InProgress"),
				new RefusedOrderTransitionTestCase(testData, order, "InProgress", "Completed", expectScriptError: true),
				new TransitionOrderItemTestCase(testData, order, 1, "InProgress", "Completed", "Completed", "Completed"),
				new ValidateCreatedServicesTestCase(testData, "_AfterCompletion"));

			test.Run();
		}
	}
}

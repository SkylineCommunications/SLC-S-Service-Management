/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE        VERSION        AUTHOR            COMMENTS

17/11/2026  1.0.0.1        SKA, Skyline      Initial version
****************************************************************************
*/

namespace SLC_SM_RT_ServiceUpdate
{
	using System;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Reporting;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using SLC_SM_RT_ServiceUpdate.TestCases;

	/// <summary>
	/// Regression test for editing an active service through "SLC_SM_Create Service Inventory Item" (Action "Edit", non-interactive payload).
	/// Verifies the edited fields persist while the identity, specification link, lifecycle state, configuration and service items are kept.
	/// </summary>
	public class Script
	{
		/// <summary>
		/// The Automation script this regression test verifies.
		/// </summary>
		public const string ScriptUnderTest = "SLC_SM_Create Service Inventory Item";

		/// <summary>
		/// The Automation script used to bring the service to the Active state.
		/// </summary>
		public const string StateTransitionScript = "Service_StateTransitions";

		private const string TestArea = "ServiceInventory";
		private const string TestName = "ServiceUpdate";
		private const string TestDescription = "Edits an active service (name, category, timing and indefinite runtime), preserves its ID, links, state and configuration, and verifies ID changes are rejected without modifying the service.";

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
			var testData = new ServiceUpdateTestData(engine);

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
				new ActivateServiceTestCase(testData),
				new EditServiceTestCase(testData, "FixedEnd", indefiniteRuntime: false),
				new ValidateServiceUpdateTestCase(testData, "FixedEnd"),
				new RefusedServiceIdChangeTestCase(testData),
				new EditServiceTestCase(testData, "Indefinite", indefiniteRuntime: true),
				new ValidateServiceUpdateTestCase(testData, "Indefinite", cleanUpAfterwards: true));

			test.Run();
		}
	}
}

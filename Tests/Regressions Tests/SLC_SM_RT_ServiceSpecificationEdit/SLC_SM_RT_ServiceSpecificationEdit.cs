/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE        VERSION        AUTHOR            COMMENTS

17/11/2026  1.0.0.1        SKA, Skyline      Initial version
****************************************************************************
*/

namespace SLC_SM_RT_ServiceSpecificationEdit
{
	using System;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Reporting;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using SLC_SM_RT_ServiceSpecificationEdit.TestCases;

	/// <summary>
	/// Regression test for "SLC_SM_IAS_Add Service Specification" (non-interactive Edit action).
	/// Edits a Service Specification and its service items and verifies the edit persisted while the existing relationships and configuration were kept.
	/// </summary>
	public class Script
	{
		/// <summary>
		/// The Automation script this regression test verifies.
		/// </summary>
		public const string ScriptUnderTest = "SLC_SM_IAS_Add Service Specification";

		/// <summary>
		/// The Automation script used to add a service item to the specification.
		/// </summary>
		public const string AddServiceItemScript = "SLC_SM_AS_AddServiceItem";

		private const string TestArea = "ServiceCatalog";
		private const string TestName = "ServiceSpecificationEdit";
		private const string TestDescription = "Edits a service specification (metadata and service items) and verifies the edited data, relationships and configuration persist.";

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
			var testData = new ServiceSpecificationEditTestData(engine);

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
				new EditServiceSpecificationTestCase(testData),
				new AddServiceItemTestCase(testData),
				new ValidateEditedServiceSpecificationTestCase(testData));

			test.Run();
		}
	}
}

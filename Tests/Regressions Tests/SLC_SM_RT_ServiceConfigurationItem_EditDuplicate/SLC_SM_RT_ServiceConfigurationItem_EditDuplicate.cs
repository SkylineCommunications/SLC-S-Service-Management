/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE        VERSION        AUTHOR            COMMENTS

28/09/2026  1.0.0.1        SKA, Skyline      Initial version
****************************************************************************
*/

namespace SLC_SM_RT_ServiceConfigurationItem_EditDuplicate
{
	using System;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Reporting;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using SLC_SM_RT_ServiceConfigurationItem_EditDuplicate.TestCases;

	/// <summary>
	/// Regression test for editing and duplicating a Service Configuration Item.
	/// </summary>
	public class Script
	{
		private const string TestArea = "ServiceConfiguration";
		private const string TestName = "ServiceConfigurationItem_EditDuplicate";
		private const string TestDescription = "Verifies a Service Configuration Item can be edited and duplicated, and that the duplicate points to its own parameter value.";

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
			var testData = new ServiceConfigurationItemEditDuplicateTestData(engine);

			var test = new Test(engine, TestTools.GenerateTestName(TestArea, TestName), TestDescription)
			{
				// The test report is returned as JSON in the "report" script output. The QAPortal element that
				// launches this script picks it up and uploads it to the QA Portal. Failed test cases are recorded
				// in that report, so the script itself always ends successfully.
				PublishingMode = ReportPublishingMode.QaPortalScriptOutput,
				FailScriptOnFailure = false,
			};

			test.AddTestCase(
				new CreateConfigurationItemTestCase(testData),
				new EditConfigurationItemTestCase(testData),
				new DuplicateConfigurationItemTestCase(testData),
				new ValidateEditedConfigurationItemTestCase(testData),
				new ValidateDuplicatedConfigurationItemTestCase(testData));

			test.Run();
		}
	}
}

/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE        VERSION        AUTHOR            COMMENTS

28/09/2026  1.0.0.1        SKA, Skyline      Initial version
****************************************************************************
*/

namespace SLC_SM_RT_ServiceSpecificationConfiguration_Edit
{
	using System;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Reporting;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using SLC_SM_RT_ServiceSpecificationConfiguration_Edit.TestCases;

	/// <summary>
	/// Regression test for editing a Service Specification configuration.
	/// </summary>
	public class Script
	{
		private const string TestArea = "ServiceConfiguration";
		private const string TestName = "ServiceSpecificationConfiguration_Edit";
		private const string TestDescription = "Verifies a Service Specification Configuration Value can be edited and stays linked to the specification.";

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
			var testData = new ServiceSpecificationConfigurationEditTestData(engine);

			var test = new Test(engine, TestTools.GenerateTestName(TestArea, TestName), TestDescription)
			{
				// The test report is returned as JSON in the "report" script output. The QAPortal element that
				// launches this script picks it up and uploads it to the QA Portal. Failed test cases are recorded
				// in that report, so the script itself always ends successfully.
				PublishingMode = ReportPublishingMode.QaPortalScriptOutput,
				FailScriptOnFailure = false,
			};

			test.AddTestCase(
				new CreateSpecificationTestCase(testData),
				new CreateAndLinkConfigurationTestCase(testData),
				new EditConfigurationTestCase(testData),
				new ValidateSpecificationLinksTestCase(testData),
				new ValidateEditedConfigurationTestCase(testData));

			test.Run();
		}
	}
}

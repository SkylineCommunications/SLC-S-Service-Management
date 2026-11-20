/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE        VERSION        AUTHOR            COMMENTS

28/09/2026  1.0.0.1        SKA, Skyline      Initial version
****************************************************************************
*/

namespace SLC_SM_RT_ServiceConfiguration
{
	using System;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Reporting;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using SLC_SM_RT_ServiceConfiguration.TestCases;

	/// <summary>
	/// Regression test for creating and activating a Service Configuration Version.
	/// </summary>
	public class Script
	{
		private const string TestArea = "ServiceConfiguration";
		private const string TestName = "ServiceConfiguration";
		private const string TestDescription = "Verifies a Service Configuration Version can be created, linked to a Service and activated.";

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
			var testData = new ServiceConfigurationTestData(engine);

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
				new CreateAndLinkServiceConfigurationTestCase(testData),
				new ValidateActiveConfigurationVersionTestCase(testData),
				new ValidateConfigurationVersionParametersTestCase(testData),
				new ValidateConfigurationItemTestCase(testData));

			test.Run();
		}
	}
}

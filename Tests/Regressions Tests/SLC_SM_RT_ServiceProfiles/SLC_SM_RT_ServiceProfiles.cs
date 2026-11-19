/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE        VERSION        AUTHOR            COMMENTS

17/11/2026  1.0.0.1        SKA, Skyline      Initial version
****************************************************************************
*/

namespace SLC_SM_RT_ServiceProfiles
{
	using System;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Reporting;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using SLC_SM_RT_ServiceProfiles.TestCases;

	/// <summary>
	/// Regression test for the profiles and parameter handling of "SLC_SM_IAS_Service_Spec_Configuration" (non-interactive payload).
	/// Creates a profile with a configuration parameter value on a Service Specification, edits the value and deletes the profile.
	/// </summary>
	public class Script
	{
		/// <summary>
		/// The Automation script this regression test verifies.
		/// </summary>
		public const string ScriptUnderTest = "SLC_SM_IAS_Service_Spec_Configuration";

		private const string TestArea = "ServiceCatalog";
		private const string TestName = "ServiceProfiles";
		private const string TestDescription = "Creates, edits and deletes a service specification profile with configuration parameter values and verifies persistence and links.";

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
			var testData = new ServiceProfilesTestData(engine);

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
				new CreateProfileTestCase(testData),
				new ValidateProfileTestCase(testData, "Created", ServiceProfilesTestData.InitialProfileValue, ServiceProfilesTestData.InitialStandaloneValue, expectExposedAtServiceOrder: true),
				new EditProfileTestCase(testData),
				new ValidateProfileTestCase(testData, "Edited", ServiceProfilesTestData.EditedProfileValue, ServiceProfilesTestData.EditedStandaloneValue, expectExposedAtServiceOrder: false),
				new DeleteProfileTestCase(testData),
				new ValidateProfileDeletedTestCase(testData));

			test.Run();
		}
	}
}

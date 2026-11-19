/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE        VERSION        AUTHOR            COMMENTS

17/11/2026  1.0.0.1        SKA, Skyline      Initial version
****************************************************************************
*/

namespace SLC_SM_RT_ServiceOrderEdit
{
	using System;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Reporting;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using SLC_SM_RT_ServiceOrderEdit.TestCases;

	/// <summary>
	/// Regression test for editing the order-level fields of a service order through the API mode of "SLC_SM_IAS_Add Service Order".
	/// </summary>
	public class Script
	{
		/// <summary>
		/// The Automation script this regression test verifies.
		/// </summary>
		public const string ScriptUnderTest = "SLC_SM_IAS_Add Service Order";

		private const string TestArea = "ServiceOrder";
		private const string TestName = "ServiceOrderEdit";
		private const string TestDescription = "Verifies editing the name, description, external ID, priority, organization and requested completion window of a service order persists, keeps its order number, status and order items, and that invalid edits are refused without changes.";

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
			var testData = new ServiceOrderEditTestData(engine);

			string editedName = $"RT_OrderEdit_Order_Edited_{Guid.NewGuid()}";
			string editedExternalId = $"RT-EXT-EDITED-{Guid.NewGuid()}";
			const string EditedDescription = "Edited description";
			DateTime requestedStartUtc = DateTime.UtcNow.Date.AddDays(7).AddHours(9);
			DateTime requestedEndUtc = requestedStartUtc.AddDays(2);

			var test = new Test(engine, TestTools.GenerateTestName(TestArea, TestName), TestDescription)
			{
				// The test report is returned as JSON in the "report" script output. The QAPortal element that
				// launches this script picks it up and uploads it to the QA Portal. Failed test cases are recorded
				// in that report, so the script itself always ends successfully.
				PublishingMode = ReportPublishingMode.QaPortalScriptOutput,
				FailScriptOnFailure = false,
			};

			// 1. Every editable field changes; the priority is matched case-insensitively and the requested end
			//    is given in a +02:00 offset, so it must be stored as the equivalent UTC time.
			// 2. Leaving the optional fields empty keeps the name and priority, clears description and external ID,
			//    removes the organization and the requested completion window.
			// 3. Invalid edits are refused and leave the order exactly as it was.
			test.AddTestCase(
				new CreateFixturesTestCase(testData),
				new EditOrderTestCase(
					testData,
					"all fields",
					() => new OrderEditPayload
					{
						Name = editedName,
						Description = EditedDescription,
						ExternalId = editedExternalId,
						Priority = "high",
						Organization = testData.OrganizationsAvailable ? testData.OrganizationName : null,
						CompletionSettingsEnabled = true,
						RequestedStart = ServiceOrderEditTestData.ToIso8601(requestedStartUtc, TimeSpan.Zero),
						RequestedEnd = ServiceOrderEditTestData.ToIso8601(requestedEndUtc, TimeSpan.FromHours(2)),
					},
					expected =>
					{
						expected.Name = editedName;
						expected.Description = EditedDescription;
						expected.ExternalId = editedExternalId;
						expected.Priority = "High";
						expected.OrganizationId = testData.OrganizationsAvailable ? testData.OrganizationId : (Guid?)null;
						expected.RequestedStart = requestedStartUtc;
						expected.RequestedEnd = requestedEndUtc;
					}),
				new EditOrderTestCase(
					testData,
					"clear optional fields",
					() => new OrderEditPayload { CompletionSettingsEnabled = false },
					expected =>
					{
						expected.Description = String.Empty;
						expected.ExternalId = String.Empty;
						expected.OrganizationId = null;
						expected.RequestedStart = null;
						expected.RequestedEnd = null;
					}),
				new RefusedEditOrderTestCase(
					testData,
					"unsupported priority 'Urgent'",
					() => new OrderEditPayload { Name = $"RT_OrderEdit_Refused_{Guid.NewGuid()}", Description = "Must not be stored", Priority = "Urgent" }),
				new RefusedEditOrderTestCase(
					testData,
					"unknown organization",
					() => new OrderEditPayload { Description = "Must not be stored", Organization = $"RT_OrderEdit_UnknownOrganization_{Guid.NewGuid()}" }),
				new RefusedEditOrderTestCase(
					testData,
					"requested completion before requested start",
					() => new OrderEditPayload
					{
						Description = "Must not be stored",
						CompletionSettingsEnabled = true,
						RequestedStart = ServiceOrderEditTestData.ToIso8601(requestedEndUtc, TimeSpan.Zero),
						RequestedEnd = ServiceOrderEditTestData.ToIso8601(requestedStartUtc, TimeSpan.Zero),
					}),
				new RefusedEditOrderTestCase(
					testData,
					"requested start without UTC offset",
					() => new OrderEditPayload
					{
						Description = "Must not be stored",
						CompletionSettingsEnabled = true,
						RequestedStart = requestedStartUtc.ToString("yyyy-MM-ddTHH:mm:ss", System.Globalization.CultureInfo.InvariantCulture),
					}));

			test.Run();
		}
	}
}

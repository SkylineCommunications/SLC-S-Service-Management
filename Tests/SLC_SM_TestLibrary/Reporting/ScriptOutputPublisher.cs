namespace Skyline.DataMiner.Utils.ServiceManagement.Testing.Reporting
{
	using System;

	using QAPortalAPI.Models.ReportingModels;

	using Skyline.DataMiner.Automation;

	/// <summary>
	/// Publishes the test report by writing its JSON representation to the "report" script output.
	/// <para>
	/// This is the mechanism the InfraOps solution uses. The Skyline QAPortal connector launches the
	/// regression test script, reads the "report" script output when the script ends and forwards the
	/// report to the QA Portal. Nothing else is required from the test script itself.
	/// </para>
	/// </summary>
	public class ScriptOutputPublisher : ITestReportPublisher
	{
		/// <summary>
		/// The script output key the Skyline QAPortal connector reads the report from.
		/// </summary>
		public const string ReportOutputKey = "report";

		/// <inheritdoc/>
		public void Publish(IEngine engine, TestReport report)
		{
			if (engine == null)
			{
				throw new ArgumentNullException(nameof(engine));
			}

			if (report == null)
			{
				throw new ArgumentNullException(nameof(report));
			}

			try
			{
				engine.AddScriptOutput(ReportOutputKey, report.ToJson());
			}
			catch (Exception ex)
			{
				engine.Log($"Publishing the result of '{report.TestInfo?.TestName}' to the QA Portal script output failed: {ex}");
			}
		}
	}
}

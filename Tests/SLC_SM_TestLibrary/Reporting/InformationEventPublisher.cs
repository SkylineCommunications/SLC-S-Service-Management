namespace Skyline.DataMiner.Utils.ServiceManagement.Testing.Reporting
{
	using System;
	using System.Linq;
	using System.Text;

	using QAPortalAPI.Models.ReportingModels;

	using Skyline.DataMiner.Automation;

	/// <summary>
	/// Publishes the test report as a human readable information event.
	/// This is the active publisher for the Service Management regression tests as long as the
	/// QA Portal integration is not switched on.
	/// </summary>
	public class InformationEventPublisher : ITestReportPublisher
	{
		/// <summary>
		/// Builds the information event message for the given report.
		/// </summary>
		/// <param name="report">The report of the completed run.</param>
		/// <returns>A multi line summary of the run.</returns>
		public static string Format(TestReport report)
		{
			if (report == null)
			{
				throw new ArgumentNullException(nameof(report));
			}

			int failedCount = report.TestCases.Count(x => x.TestCaseResult != QAPortalAPI.Enums.Result.Success);

			var builder = new StringBuilder();
			builder.AppendLine($"[{report.TestInfo?.TestName}] {report.TestResult.ToString().ToUpperInvariant()}");
			builder.AppendLine($"Agent: {report.SystemInfo?.AgentName}");
			builder.AppendLine($"Test cases: {report.TestCases.Count} ({report.TestCases.Count - failedCount} passed, {failedCount} failed)");

			foreach (TestCaseReport testCase in report.TestCases)
			{
				string marker = testCase.TestCaseResult == QAPortalAPI.Enums.Result.Success ? "PASS" : "FAIL";
				builder.Append($"  [{marker}] {testCase.TestCaseName}");

				if (!String.IsNullOrWhiteSpace(testCase.TestCaseResultInfo))
				{
					builder.Append($" - {testCase.TestCaseResultInfo}");
				}

				builder.AppendLine();
			}

			foreach (PerformanceTestCaseReport performanceTestCase in report.PerformanceTestCases)
			{
				builder.AppendLine($"  [TIME] {performanceTestCase.TestCaseName}: {performanceTestCase.Timing} {performanceTestCase.Unit}");
			}

			return builder.ToString();
		}

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
				engine.GenerateInformation(Format(report));
			}
			catch (Exception ex)
			{
				engine.Log($"Publishing the result of '{report.TestInfo?.TestName}' as information event failed: {ex}");
			}
		}
	}
}

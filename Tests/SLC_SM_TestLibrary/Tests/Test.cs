namespace Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;

	using QAPortalAPI.Models.ReportingModels;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Reporting;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tools;

	/// <summary>
	/// Runs a sequence of <see cref="TestCase"/> instances, collects their results and publishes them.
	/// </summary>
	public class Test
	{
		private readonly IEngine engine;
		private readonly string name;
		private readonly string description;
		private readonly bool continueOnException;
		private readonly List<TestCase> testCases = new List<TestCase>();

		/// <summary>
		/// Initializes a new instance of the <see cref="Test"/> class.
		/// </summary>
		/// <param name="engine">Link with SLAutomation process.</param>
		/// <param name="name">The unique test name reported to the QA Portal.</param>
		/// <param name="description">A short description of what the test verifies.</param>
		/// <param name="continueOnException">When <c>true</c>, remaining test cases still run after a failure.</param>
		public Test(IEngine engine, string name, string description, bool continueOnException = false)
		{
			this.engine = engine ?? throw new ArgumentNullException(nameof(engine));

			if (String.IsNullOrWhiteSpace(name))
			{
				throw new ArgumentException("Test name should not be empty", nameof(name));
			}

			this.name = name;
			this.description = description ?? String.Empty;
			this.continueOnException = continueOnException;
		}

		/// <summary>
		/// Gets or sets where the results are published to. Defaults to <see cref="ReportPublishingMode.InformationEvent"/>.
		/// Switch this to <see cref="ReportPublishingMode.QaPortalScriptOutput"/> to activate the QA Portal integration.
		/// </summary>
		public ReportPublishingMode PublishingMode { get; set; } = ReportPublishingMode.InformationEvent;

		/// <summary>
		/// Gets or sets the QA Portal connection settings, only used by <see cref="ReportPublishingMode.QaPortalApi"/>.
		/// </summary>
		public QaPortalSettings QaPortalSettings { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether the script ends with <see cref="IEngine.ExitFail(string)"/>
		/// when at least one test case failed. Enabled by default so a failing test is visible in the
		/// Automation script log as long as the QA Portal integration is not active.
		/// </summary>
		public bool FailScriptOnFailure { get; set; } = true;

		/// <summary>
		/// Adds test cases to the test. They are executed in the order they are added.
		/// </summary>
		/// <param name="newTestCases">The test cases to add.</param>
		public void AddTestCase(params TestCase[] newTestCases)
		{
			if (newTestCases == null)
			{
				throw new ArgumentNullException(nameof(newTestCases));
			}

			foreach (TestCase testCase in newTestCases)
			{
				if (testCase == null)
				{
					throw new ArgumentNullException(nameof(newTestCases), "New test cases contains a null test case");
				}

				if (String.IsNullOrWhiteSpace(testCase.Name))
				{
					throw new InvalidOperationException("Test case name should not be empty");
				}

				if (testCases.Any(x => x.Name.Equals(testCase.Name, StringComparison.Ordinal)))
				{
					throw new InvalidOperationException($"Duplicated test case name. Test case name has to be unique: {testCase.Name}");
				}

				testCases.Add(testCase);
			}
		}

		/// <summary>
		/// Executes all test cases, cleans up and publishes the results.
		/// </summary>
		/// <returns><c>true</c> when every test case passed.</returns>
		public bool Run()
		{
			Execute();
			PerformCleanup();

			TestReport report = BuildReport();
			Publish(report);

			bool succeeded = report.TestResult == QAPortalAPI.Enums.Result.Success;

			if (FailScriptOnFailure && !succeeded)
			{
				engine.ExitFail($"{name}: FAILED - {DescribeFailures(report)}");
			}

			return succeeded;
		}

		/// <summary>
		/// Executes all test cases without cleaning up or publishing.
		/// </summary>
		public void Execute()
		{
			foreach (TestCase testCase in testCases)
			{
				try
				{
					ExecuteTestCase(testCase);
				}
				catch (ScriptAbortException)
				{
					throw;
				}
				catch (Exception ex)
				{
					HandleTestCaseException(testCase, ex);

					if (!continueOnException)
					{
						break;
					}
				}
			}
		}

		/// <summary>
		/// Cleans up every test case. A failing cleanup never aborts the cleanup of the other test cases.
		/// </summary>
		public void PerformCleanup()
		{
			foreach (TestCase testCase in testCases)
			{
				try
				{
					testCase.PerformCleanup(engine);
				}
				catch (ScriptAbortException)
				{
					throw;
				}
				catch (Exception ex)
				{
					engine.GenerateInformation($"{name}: cleanup of {testCase.Name} failed: {ex}");
				}
			}
		}

		/// <summary>
		/// Aggregates the results of all test cases into a single QA Portal report.
		/// </summary>
		/// <returns>The report of the run.</returns>
		public TestReport BuildReport()
		{
			var report = new TestReport(
				new TestInfo(name, TestInfoConsts.Contact, TestInfoConsts.ProjectIds, description),
				new TestSystemInfo(DataMinerInfo.GetAgentWhereScriptIsRunning(engine)));

			foreach (TestCase testCase in testCases)
			{
				CopyResultsToReport(report, testCase);
			}

			return report;
		}

		/// <summary>
		/// Publishes the given report through the publisher that matches <see cref="PublishingMode"/>.
		/// </summary>
		/// <param name="report">The report of the run.</param>
		public void Publish(TestReport report)
		{
			if (report == null)
			{
				throw new ArgumentNullException(nameof(report));
			}

			try
			{
				ITestReportPublisher publisher = TestReportPublisherFactory.Create(PublishingMode, QaPortalSettings);
				publisher.Publish(engine, report);
			}
			catch (Exception ex)
			{
				engine.Log($"Publishing the results of {name} failed: {ex}");
			}
		}

		private static string DescribeFailures(TestReport report)
		{
			string[] failures = report.TestCases
				.Where(x => x.TestCaseResult != QAPortalAPI.Enums.Result.Success)
				.Select(x => $"{x.TestCaseName}: {x.TestCaseResultInfo}")
				.ToArray();

			return failures.Length > 0 ? String.Join(Environment.NewLine, failures) : "no test case results were recorded";
		}

		private static bool HasTestCaseReport(TestCase testCase)
		{
			return testCase.TestCaseReports.Any(x => String.Equals(x.TestCaseName, testCase.Name, StringComparison.Ordinal));
		}

		private void CopyResultsToReport(TestReport report, TestCase testCase)
		{
			foreach (TestCaseReport testCaseReport in testCase.TestCaseReports)
			{
				if (!report.TryAddTestCase(testCaseReport, out string errorMessage))
				{
					engine.Log($"Could not add result '{testCaseReport.TestCaseName}' to the report of {name}: {errorMessage}");
				}
			}

			foreach (PerformanceTestCaseReport performanceTestCaseReport in testCase.PerformanceTestCaseReports)
			{
				if (!performanceTestCaseReport.IsValid(out string validationInfo))
				{
					engine.Log($"Could not add timing '{performanceTestCaseReport.TestCaseName}' to the report of {name}: {validationInfo}");
					continue;
				}

				report.PerformanceTestCases.Add(performanceTestCaseReport);
			}
		}

		private void ExecuteTestCase(TestCase testCase)
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			testCase.Execute(engine);
			stopwatch.Stop();

			if (!testCase.PerformanceTestCaseReports.Any())
			{
				testCase.PerformanceTestCaseReports.Add(PerformanceTestCaseReport.CreateSuccesTestcase(testCase.Name, stopwatch.Elapsed));
			}

			if (!HasTestCaseReport(testCase))
			{
				testCase.TestCaseReports.Add(TestCaseReport.GetSuccessTestCase(testCase.Name));
			}
		}

		private void HandleTestCaseException(TestCase testCase, Exception exception)
		{
			// A TestCaseException carries a functional verdict, so only its message is relevant.
			string message = exception is TestCaseException ? exception.Message : exception.ToString();

			engine.Log($"{name}: test case {testCase.Name} failed: {exception}");

			if (HasTestCaseReport(testCase))
			{
				testCase.ReplaceSuccessReport(TestCaseReport.GetFailTestCase(testCase.Name, message));
			}
			else
			{
				testCase.TestCaseReports.Add(TestCaseReport.GetFailTestCase(testCase.Name, message));
			}
		}
	}
}

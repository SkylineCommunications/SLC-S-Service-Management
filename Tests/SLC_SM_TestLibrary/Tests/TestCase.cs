namespace Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using QAPortalAPI.Models.ReportingModels;

	using Skyline.DataMiner.Automation;

	/// <summary>
	/// Base class for a single step of a regression test.
	/// A test case executes one action and records one or more <see cref="TestCaseReport"/> results for it.
	/// </summary>
	public abstract class TestCase
	{
		/// <summary>
		/// Gets the unique name of the test case within its <see cref="Test"/>. Defaults to the type name.
		/// </summary>
		public virtual string Name => GetType().Name;

		/// <summary>
		/// Gets the functional results recorded by this test case.
		/// </summary>
		public ICollection<TestCaseReport> TestCaseReports { get; } = new List<TestCaseReport>();

		/// <summary>
		/// Gets the timing results recorded by this test case.
		/// </summary>
		public ICollection<PerformanceTestCaseReport> PerformanceTestCaseReports { get; } = new List<PerformanceTestCaseReport>();

		/// <summary>
		/// Executes the action under test. Throw a <see cref="TestCaseException"/> to fail the test case with a clean message.
		/// </summary>
		/// <param name="engine">Link with SLAutomation process.</param>
		public abstract void Execute(IEngine engine);

		/// <summary>
		/// Removes everything this test case created. Always invoked, also when <see cref="Execute"/> threw.
		/// </summary>
		/// <param name="engine">Link with SLAutomation process.</param>
		public abstract void PerformCleanup(IEngine engine);

		/// <summary>
		/// Records a functional result for this test case.
		/// </summary>
		/// <param name="report">The result to record.</param>
		public void AddReport(TestCaseReport report)
		{
			if (report == null)
			{
				throw new ArgumentNullException(nameof(report));
			}

			TestCaseReports.Add(report);
		}

		/// <summary>
		/// Records a timing result for this test case.
		/// </summary>
		/// <param name="report">The result to record.</param>
		public void AddReport(PerformanceTestCaseReport report)
		{
			if (report == null)
			{
				throw new ArgumentNullException(nameof(report));
			}

			PerformanceTestCaseReports.Add(report);
		}

		/// <summary>
		/// Records a success result under the given name.
		/// </summary>
		/// <param name="name">The result name. Defaults to <see cref="Name"/> when empty.</param>
		public void AddSuccess(string name = null)
		{
			AddReport(TestCaseReport.GetSuccessTestCase(String.IsNullOrWhiteSpace(name) ? Name : name));
		}

		/// <summary>
		/// Records a failure result under the given name.
		/// </summary>
		/// <param name="failureInfo">The reason of the failure.</param>
		/// <param name="name">The result name. Defaults to <see cref="Name"/> when empty.</param>
		public void AddFailure(string failureInfo, string name = null)
		{
			AddReport(TestCaseReport.GetFailTestCase(String.IsNullOrWhiteSpace(name) ? Name : name, failureInfo ?? String.Empty));
		}

		/// <summary>
		/// Replaces an already recorded success result by the given result.
		/// Useful when a later assertion invalidates an earlier optimistic result.
		/// </summary>
		/// <param name="report">The result that replaces the success result with the same name.</param>
		public void ReplaceSuccessReport(TestCaseReport report)
		{
			if (report == null)
			{
				throw new ArgumentNullException(nameof(report));
			}

			TestCaseReport existingReport = TestCaseReports
				.SingleOrDefault(x => x.TestCaseName.Equals(report.TestCaseName, StringComparison.Ordinal) && x.TestCaseResult == QAPortalAPI.Enums.Result.Success);

			if (existingReport == null)
			{
				return;
			}

			TestCaseReports.Remove(existingReport);
			AddReport(report);
		}
	}
}

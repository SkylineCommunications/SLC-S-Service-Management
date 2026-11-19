namespace SLC_SM_RT.QAOps.IntegrationTests
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Text;

	using Newtonsoft.Json.Linq;

	using Skyline.DataMiner.Core.DataMinerSystem.Common;

	/// <summary>
	/// Runs an SLC_SM_RT_* regression test script and turns its QA Portal report into an MSTest verdict.
	/// </summary>
	/// <remarks>
	/// The regression test scripts never fail themselves: they return the QA Portal report as JSON in the
	/// "report" script output. A test only passes when that report exists and every test case is a Success.
	/// </remarks>
	internal static class RegressionScriptRunner
	{
		public const string ReportOutputKey = "report";

		public static void RunAndAssert(string scriptName)
		{
			IDms dms = DmsTestSetup.Dms;
			Assert.IsNotNull(dms, "No connection to the DataMiner; see the AssemblyInitialize failure.");
			Assert.IsTrue(dms.ScriptExists(scriptName), $"Regression test script '{scriptName}' is not installed on the test DataMiner.");

			Stopwatch stopwatch = Stopwatch.StartNew();
			DmsAutomationScriptResult result = dms.GetScript(scriptName).Execute(
				Enumerable.Empty<DmsAutomationScriptParamValue>(),
				Enumerable.Empty<DmsAutomationScriptDummyValue>(),
				new DmsAutomationScriptRunOptions { ExtendedErrorInfo = true });
			stopwatch.Stop();

			string executionInfo = DescribeExecution(scriptName, result, stopwatch.Elapsed);
			Console.WriteLine(executionInfo);

			string reportJson = null;
			if (result?.ScriptOutput == null || !result.ScriptOutput.TryGetValue(ReportOutputKey, out reportJson) || String.IsNullOrWhiteSpace(reportJson))
			{
				Assert.Fail($"'{scriptName}' returned no '{ReportOutputKey}' script output, so no test case ran to completion.{Environment.NewLine}{executionInfo}");
			}

			List<TestCaseOutcome> outcomes = ParseTestCases(scriptName, reportJson);
			Console.WriteLine(DescribeOutcomes(outcomes));

			Assert.IsTrue(outcomes.Count > 0, $"The report of '{scriptName}' contains no test cases.{Environment.NewLine}{executionInfo}");

			List<TestCaseOutcome> failures = outcomes.Where(x => !x.Succeeded).ToList();
			if (failures.Count > 0)
			{
				Assert.Fail(
					$"{failures.Count} of {outcomes.Count} test case(s) of '{scriptName}' failed:{Environment.NewLine}" +
					String.Join(Environment.NewLine, failures.Select(x => $"- {x.Name} [{x.Result}]: {x.Info}")) +
					$"{Environment.NewLine}{executionInfo}");
			}
		}

		private static List<TestCaseOutcome> ParseTestCases(string scriptName, string reportJson)
		{
			JObject report;
			try
			{
				report = JObject.Parse(reportJson);
			}
			catch (Exception ex)
			{
				Assert.Fail($"The '{ReportOutputKey}' output of '{scriptName}' is not valid JSON: {ex.Message}{Environment.NewLine}{Truncate(reportJson, 1000)}");
				return null;
			}

			// QAPortalAPI serializes the results as "TestCaseResults": { "<name>": { ... } }; older versions used a "TestCases" array.
			IEnumerable<JObject> testCases = report.GetValue("TestCaseResults", StringComparison.OrdinalIgnoreCase) is JObject byName
				? byName.Properties().Select(x => x.Value).OfType<JObject>()
				: (report.GetValue("TestCases", StringComparison.OrdinalIgnoreCase) as JArray ?? new JArray()).OfType<JObject>();

			return testCases
				.Select(x => new TestCaseOutcome(
					(string)x.GetValue("TestCaseName", StringComparison.OrdinalIgnoreCase),
					x.GetValue("TestCaseResult", StringComparison.OrdinalIgnoreCase),
					(string)x.GetValue("TestCaseResultInfo", StringComparison.OrdinalIgnoreCase)))
				.ToList();
		}

		private static string DescribeExecution(string scriptName, DmsAutomationScriptResult result, TimeSpan duration)
		{
			var sb = new StringBuilder();
			sb.Append($"Script '{scriptName}' ran in {duration.TotalSeconds:0.0} s");

			if (result != null)
			{
				sb.Append($", HadError={result.HadError}, ErrorCode={result.ErrorCode}");
				if (result.ErrorMessages != null && result.ErrorMessages.Length > 0)
				{
					sb.Append($", Errors: {Truncate(String.Join(" | ", result.ErrorMessages), 1500)}");
				}
			}

			return sb.ToString();
		}

		private static string DescribeOutcomes(IEnumerable<TestCaseOutcome> outcomes)
		{
			return String.Join(Environment.NewLine, outcomes.Select(x => $"  {x.Name}: {x.Result} {x.Info}"));
		}

		private static string Truncate(string value, int max)
		{
			if (String.IsNullOrEmpty(value) || value.Length <= max)
			{
				return value;
			}

			return value.Substring(0, max) + "...(truncated)";
		}

		private sealed class TestCaseOutcome
		{
			public TestCaseOutcome(string name, JToken result, string info)
			{
				Name = name ?? "<unnamed>";
				Info = info ?? String.Empty;

				// QAPortalAPI.Enums.Result: Unknown = 0, Success = 1, Failure = 2. Serialized as name or number.
				string text = result?.ToString() ?? "Unknown";
				Result = text == "1" ? "Success" : text == "2" ? "Failure" : text == "0" ? "Unknown" : text;
			}

			public string Name { get; }

			public string Result { get; }

			public string Info { get; }

			public bool Succeeded => String.Equals(Result, "Success", StringComparison.OrdinalIgnoreCase);
		}
	}
}

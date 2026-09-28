namespace Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using Skyline.DataMiner.Automation;

	/// <summary>
	/// Helpers shared by the Service Management regression tests.
	/// </summary>
	public static class TestTools
	{
		/// <summary>
		/// Builds the QA Portal test name for a regression test.
		/// </summary>
		/// <param name="area">The Service Management area under test, for example "ServiceInventory".</param>
		/// <param name="testName">The short test name, for example "CreateServiceInventoryItem".</param>
		/// <returns>The fully qualified test name.</returns>
		public static string GenerateTestName(string area, string testName)
		{
			if (String.IsNullOrWhiteSpace(area))
			{
				throw new ArgumentException("Area should not be empty", nameof(area));
			}

			if (String.IsNullOrWhiteSpace(testName))
			{
				throw new ArgumentException("Test name should not be empty", nameof(testName));
			}

			return $"{TestInfoConsts.TestNamePrefix}_{area}_{testName}";
		}

		/// <summary>
		/// Runs the Automation script under test as a synchronous sub script and fails the test case when the outcome
		/// does not match <paramref name="isSuccessExpected"/>.
		/// </summary>
		/// <param name="testCase">The test case the outcome is recorded on.</param>
		/// <param name="engine">Link with SLAutomation process.</param>
		/// <param name="scriptName">The name of the Automation script under test.</param>
		/// <param name="scriptParameters">The script parameters, keyed by parameter description.</param>
		/// <param name="isSuccessExpected">When <c>false</c>, the sub script is expected to fail.</param>
		/// <returns><c>true</c> when the outcome matched the expectation.</returns>
		public static bool ExecuteSubScript(
			TestCase testCase,
			IEngine engine,
			string scriptName,
			IReadOnlyDictionary<string, string> scriptParameters,
			bool isSuccessExpected = true)
		{
			if (testCase == null)
			{
				throw new ArgumentNullException(nameof(testCase));
			}

			if (engine == null)
			{
				throw new ArgumentNullException(nameof(engine));
			}

			if (String.IsNullOrWhiteSpace(scriptName))
			{
				throw new ArgumentException("Script name should not be empty", nameof(scriptName));
			}

			SubScriptOptions subScript = engine.PrepareSubScript(scriptName);
			subScript.Synchronous = true;
			subScript.ExtendedErrorInfo = true;

			if (scriptParameters != null)
			{
				foreach (KeyValuePair<string, string> parameter in scriptParameters)
				{
					subScript.SelectScriptParam(parameter.Key, parameter.Value ?? String.Empty);
				}
			}

			subScript.StartScript();

			if (subScript.HadError == !isSuccessExpected)
			{
				return true;
			}

			string errorInfo = subScript.HadError
				? $"Sub script '{scriptName}' failed:{Environment.NewLine}{String.Join(Environment.NewLine, subScript.GetErrorMessages() ?? Enumerable.Empty<string>())}"
				: $"Sub script '{scriptName}' succeeded while a failure was expected.";

			throw new TestCaseException(errorInfo);
		}

		/// <summary>
		/// Fails the calling test case when the condition does not hold.
		/// </summary>
		/// <param name="condition">The condition that must hold.</param>
		/// <param name="failureInfo">The reason reported when the condition does not hold.</param>
		public static void Assert(bool condition, string failureInfo)
		{
			if (!condition)
			{
				throw new TestCaseException(failureInfo);
			}
		}

		/// <summary>
		/// Fails the calling test case when the two values are not equal.
		/// </summary>
		/// <param name="expected">The expected value.</param>
		/// <param name="actual">The actual value.</param>
		/// <param name="subject">A short description of what is compared.</param>
		public static void AssertEqual(string expected, string actual, string subject)
		{
			if (!String.Equals(expected, actual, StringComparison.Ordinal))
			{
				throw new TestCaseException($"{subject} mismatch. Expected '{expected}', got '{actual}'.");
			}
		}
	}
}

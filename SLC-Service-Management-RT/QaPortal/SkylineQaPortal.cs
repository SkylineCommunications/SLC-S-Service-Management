namespace SLC_Service_Management_RT.QaPortal
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Core.DataMinerSystem.Common;

	/// <summary>
	/// Wraps the QAPortal element that runs the regression tests and uploads their reports to the QA Portal.
	/// </summary>
	internal class SkylineQaPortal
	{
		/// <summary>
		/// The name of the QAPortal element.
		/// </summary>
		public const string ElementName = "QAPortal";

		/// <summary>
		/// The connector the QAPortal element is created from.
		/// </summary>
		public const string ProtocolName = "Skyline QAPortal";

		/// <summary>
		/// The connector version the QAPortal element is created from.
		/// </summary>
		public const string ProtocolVersion = "Production";

		/// <summary>
		/// The view the QAPortal element is placed in when it is created.
		/// </summary>
		public const string ViewName = "Regression Tests";

		private const int RunningTestsTablePid = 800;
		private const int RunningTestsStateColumnPid = 803;

		private readonly IEngine engine;

		private SkylineQaPortal(IEngine engine, IDmsElement element)
		{
			this.engine = engine;
			Element = element;
		}

		public IDmsElement Element { get; }

		/// <summary>
		/// Gets the QAPortal element, or creates it when it does not exist yet.
		/// </summary>
		/// <param name="engine">Provides access to the Automation engine.</param>
		/// <param name="dms">The DataMiner System.</param>
		/// <returns>The QAPortal element.</returns>
		public static SkylineQaPortal GetOrCreateQaPortalElement(IEngine engine, IDms dms)
		{
			IDmsElement element = dms.ElementExists(ElementName) ? dms.GetElement(ElementName) : CreateQaPortalElement(dms);

			if (element.AlarmTemplate == null)
			{
				var alarmTemplate = dms.GetProtocol(element.Protocol.Name, element.Protocol.Version).GetAlarmTemplates().FirstOrDefault();
				if (alarmTemplate != null)
				{
					element.AlarmTemplate = alarmTemplate;
					element.Update();
				}
			}

			return new SkylineQaPortal(engine, element);
		}

		/// <summary>
		/// Determines whether the QAPortal element is currently running tests.
		/// </summary>
		/// <returns><c>true</c> when at least one test is running.</returns>
		public bool AreTestsRunning()
		{
			return Element.GetTable(RunningTestsTablePid).QueryData(new[]
			{
				new ColumnFilter
				{
					Pid = RunningTestsStateColumnPid,
					Value = "1",
					ComparisonOperator = ComparisonOperator.Equal,
				},
			}).Any();
		}

		/// <summary>
		/// Adds the given tests to the QAPortal element and updates the ones that already exist.
		/// Tests of this package that are no longer in the list are removed; tests of other solutions are left alone.
		/// </summary>
		/// <param name="newTests">The tests this package delivers.</param>
		/// <param name="scriptPrefix">The script name prefix that identifies the tests of this package.</param>
		public void AddOrUpdateRegressionTests(ICollection<RegressionTest> newTests, string scriptPrefix)
		{
			Dictionary<string, RegressionTestRow> existingTests = RegressionTestRow.GetRegressionTests(engine, Element);

			foreach (RegressionTest test in newTests)
			{
				if (existingTests.TryGetValue(test.Name, out RegressionTestRow existingTest))
				{
					if (existingTest.Script == test.Script)
					{
						existingTest.UpdateExistingTestRow(test);
						continue;
					}

					// The connector cannot change the script of an existing test, so it is recreated.
					existingTest.Delete();
				}

				RegressionTestRow.CreateTest(engine, Element, test);
			}

			foreach (RegressionTestRow existingTest in existingTests.Values)
			{
				bool isOwnTest = existingTest.Script.StartsWith(scriptPrefix, StringComparison.OrdinalIgnoreCase);
				if (isOwnTest && !newTests.Any(x => x.Name == existingTest.Name))
				{
					existingTest.Delete();
				}
			}
		}

		private static IDmsElement CreateQaPortalElement(IDms dms)
		{
			IDma agent = dms.GetAgents().First();
			IDmsProtocol protocol = dms.GetProtocol(ProtocolName, ProtocolVersion);

			var config = new ElementConfiguration(dms, ElementName, protocol);
			config.Views.Add(GetOrCreateRegressionTestsView(dms));

			DmsElementId elementId = agent.CreateElement(config);

			IDmsElement element = null;
			for (int i = 0; i < 300; i++)
			{
				if (dms.ElementExists(elementId))
				{
					element = dms.GetElement(elementId);
					if (element.IsStartupComplete())
					{
						return element;
					}
				}

				Thread.Sleep(200);
			}

			if (element == null)
			{
				throw new ElementNotFoundException($"Unable to find the {ElementName} element {elementId} after one minute.");
			}

			return element;
		}

		private static IDmsView GetOrCreateRegressionTestsView(IDms dms)
		{
			if (dms.ViewExists(ViewName))
			{
				return dms.GetView(ViewName);
			}

			IDmsView rootView = dms.GetView(-1);
			int viewId = dms.CreateView(new ViewConfiguration(ViewName, rootView));

			for (int i = 0; i < 300; i++)
			{
				if (dms.ViewExists(viewId))
				{
					return dms.GetView(viewId);
				}

				Thread.Sleep(200);
			}

			throw new ViewNotFoundException($"Unable to find the '{ViewName}' view {viewId} after one minute.");
		}
	}
}

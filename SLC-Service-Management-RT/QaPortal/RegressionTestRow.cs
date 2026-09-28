namespace SLC_Service_Management_RT.QaPortal
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Core.DataMinerSystem.Common;
	using Skyline.DataMiner.Net.Messages;
	using Skyline.DataMiner.Net.Messages.Advanced;

	/// <summary>
	/// A row of the Regression Tests table (1000) of the QAPortal element.
	/// </summary>
	internal class RegressionTestRow
	{
		private const int RegressionTestsTablePid = 1000;
		private const int GroupColumnPid = 1103;
		private const int ExecOrderColumnPid = 1112;
		private const int ContextMenuPid = 999;
		private const int SetContextMenuWhat = 287;

		private readonly IEngine engine;
		private readonly IDmsTable table;
		private int execOrder;
		private string group;

		private RegressionTestRow(IEngine engine, IDmsTable table, object[] row)
		{
			this.engine = engine;
			this.table = table;
			Name = Convert.ToString(row[0]);
			Script = Convert.ToString(row[1]);
			group = Convert.ToString(row[2]);
			execOrder = Convert.ToInt32(row[11]);
		}

		public int ExecOrder
		{
			get => execOrder;
			set
			{
				execOrder = value;
				table.GetColumn<int?>(ExecOrderColumnPid).SetValue(Name, value);
			}
		}

		public string Group
		{
			get => group;
			set
			{
				group = value;
				table.GetColumn<string>(GroupColumnPid).SetValue(Name, value);
			}
		}

		public string Name { get; }

		public string Script { get; }

		/// <summary>
		/// Adds the test to the QAPortal element through the context menu of the Regression Tests table.
		/// </summary>
		/// <param name="engine">Provides access to the Automation engine.</param>
		/// <param name="element">The QAPortal element.</param>
		/// <param name="newTest">The test to add.</param>
		/// <returns>The created row.</returns>
		public static RegressionTestRow CreateTest(IEngine engine, IDmsElement element, RegressionTest newTest)
		{
			SendContextMenuAction(engine, element, "add", newTest.Name, newTest.Script);

			IDmsTable testTable = element.GetTable(RegressionTestsTablePid);

			RegressionTestRow row = null;
			for (int i = 0; i < 100; i++)
			{
				if (testTable.GetPrimaryKeys().Contains(newTest.Name))
				{
					row = new RegressionTestRow(engine, testTable, testTable.GetRow(newTest.Name));
					break;
				}

				Thread.Sleep(200);
			}

			if (row == null)
			{
				throw new KeyNotFoundException($"Unable to find {newTest.Name} in the regression tests table.");
			}

			row.ExecOrder = newTest.ExecOrder;

			if (!String.IsNullOrWhiteSpace(newTest.Group))
			{
				row.Group = newTest.Group;
			}

			return row;
		}

		/// <summary>
		/// Reads all regression tests currently registered on the QAPortal element.
		/// </summary>
		/// <param name="engine">Provides access to the Automation engine.</param>
		/// <param name="element">The QAPortal element.</param>
		/// <returns>The registered tests, by name.</returns>
		public static Dictionary<string, RegressionTestRow> GetRegressionTests(IEngine engine, IDmsElement element)
		{
			var tests = new Dictionary<string, RegressionTestRow>();

			IDmsTable testTable = element.GetTable(RegressionTestsTablePid);
			foreach (object[] row in testTable.GetRows())
			{
				var test = new RegressionTestRow(engine, testTable, row);
				tests[test.Name] = test;
			}

			return tests;
		}

		/// <summary>
		/// Removes the test from the QAPortal element.
		/// </summary>
		public void Delete()
		{
			SendContextMenuAction(engine, table.Element, "delete", Name);
		}

		/// <summary>
		/// Updates the group and execution order when they differ from the given definition.
		/// </summary>
		/// <param name="test">The expected definition of the test.</param>
		public void UpdateExistingTestRow(RegressionTest test)
		{
			if (Group != test.Group)
			{
				Group = test.Group;
			}

			if (ExecOrder != test.ExecOrder)
			{
				ExecOrder = test.ExecOrder;
			}
		}

		private static void SendContextMenuAction(IEngine engine, IDmsElement element, params string[] arguments)
		{
			var message = new SetDataMinerInfoMessage
			{
				DataMinerID = element.DmsElementId.AgentId,
				ElementID = element.DmsElementId.ElementId,
				Sa2 = new SA
				{
					Sa = new[] { Guid.NewGuid().ToString() }.Concat(arguments).ToArray(),
				},
				Uia1 = new UIA
				{
					Uia = new[]
					{
						(uint)element.DmsElementId.AgentId,
						(uint)element.DmsElementId.ElementId,
						(uint)ContextMenuPid,
					},
				},
				What = SetContextMenuWhat,
			};

			engine.SendSLNetSingleResponseMessage(message);
		}
	}
}

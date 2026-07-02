namespace SLC_SM_IAS_Manage_Service_Scripts.Views
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.IAS;
	using SLC_SM_IAS_Manage_Service_Scripts.Model;

	internal sealed class ManageServiceScriptsView
	{
		private readonly IEngine engine;

		public ManageServiceScriptsView(IEngine engine)
		{
			this.engine = engine;
		}

		public string PromptScriptName(ScriptAction action, string currentScriptName)
		{
			var dialog = new ScriptNameDialog(engine);
			dialog.Title = action == ScriptAction.Add ? "Add Script" : "Update Script";
			dialog.Info.Text = action == ScriptAction.Add
				? "Enter the script name to add."
				: "Edit the script name.";
			dialog.ScriptName.Text = action == ScriptAction.Update ? currentScriptName ?? String.Empty : String.Empty;
			dialog.ConfirmButton.Text = action == ScriptAction.Add ? "Add" : "Update";
			dialog.Build();
			dialog.Show(requireResponse: true);

			return dialog.IsConfirmed ? dialog.ScriptName.Text?.Trim() : String.Empty;
		}

		public void ShowSuccess(string serviceName, string action, IEnumerable<string> scripts)
		{
			var scriptList = scripts
				.Where(script => !String.IsNullOrWhiteSpace(script))
				.OrderBy(script => script, StringComparer.OrdinalIgnoreCase)
				.ToList();

			var scriptsText = scriptList.Count == 0
				? "- none -"
				: String.Join(Environment.NewLine, scriptList.Select(script => $"- {script}"));

			engine.ShowPopupDialog(
				"Success",
				$"{action} completed for service '{serviceName}'.{Environment.NewLine}{Environment.NewLine}Resulting scripts:{Environment.NewLine}{scriptsText}",
				"OK");
		}

		private sealed class ScriptNameDialog : ScriptDialog
		{
			public ScriptNameDialog(IEngine engine) : base(engine)
			{
				ConfirmButton.Pressed += (sender, args) => IsConfirmed = true;
				CancelButton.Pressed += (sender, args) => IsConfirmed = false;
			}

			public Label Info { get; } = new Label();

			public TextBox ScriptName { get; } = new TextBox { MinWidth = 350 };

			public Button ConfirmButton { get; } = new Button("Confirm") { Width = 120, Height = 25, Style = ButtonStyle.CallToAction };

			public Button CancelButton { get; } = new Button("Cancel") { Width = 120, Height = 25 };

			public bool IsConfirmed { get; private set; }

			public override void Build()
			{
				Clear();
				Layout.RowPosition = 0;
				AddWidget(Info, Layout.RowPosition, 0, 1, 2);
				AddWidget(ScriptName, ++Layout.RowPosition, 0, 1, 2);
				AddWidget(new WhiteSpace { Height = 25 }, ++Layout.RowPosition, 0);
				AddWidget(ConfirmButton, ++Layout.RowPosition, 0);
				AddWidget(CancelButton, Layout.RowPosition, 1);
			}
		}
	}
}

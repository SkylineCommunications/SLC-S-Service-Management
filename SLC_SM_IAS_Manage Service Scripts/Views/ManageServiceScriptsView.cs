namespace SLC_SM_IAS_Manage_Service_Scripts.Views
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Messages;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.IAS;
	using SLC_SM_IAS_Manage_Service_Scripts.Model;

	internal sealed class ManageServiceScriptsView
	{
		private const string PlaceholderOption = "- Select -";

		private readonly IEngine engine;

		public ManageServiceScriptsView(IEngine engine)
		{
			this.engine = engine;
		}

		public string PromptScriptName(ScriptAction action, string currentScriptName)
		{
			var availableScripts = GetScriptNames();

			var dialog = new ScriptNameDialog(engine, availableScripts);
			dialog.Title = action == ScriptAction.Add ? "Add Script" : "Update Script";
			dialog.Info.Text = action == ScriptAction.Add
				? "Select the script to add."
				: "Select the new script.";

			if (action == ScriptAction.Update && !String.IsNullOrWhiteSpace(currentScriptName))
			{
				var match = availableScripts.FirstOrDefault(s => String.Equals(s, currentScriptName, StringComparison.OrdinalIgnoreCase));
				dialog.ScriptName.Selected = match ?? PlaceholderOption;
			}

			dialog.Build();
			dialog.Show(requireResponse: true);

			return dialog.IsConfirmed ? dialog.ScriptName.Selected?.Trim() : String.Empty;
		}

		private IEnumerable<string> GetScriptNames()
		{
			try
			{
				var response = engine.SendSLNetSingleResponseMessage(new GetInfoMessage
				{
					DataMinerID = -1,
					HostingDataMinerID = -1,
					Type = InfoType.Scripts,
				}) as GetScriptsResponseMessage;

				if (response == null)
				{
					return Enumerable.Empty<string>();
				}

				return response.Scripts.OrderBy(x => x);
			}
			catch (Exception)
			{
				return Enumerable.Empty<string>();
			}
		}

		private sealed class ScriptNameDialog : ScriptDialog
		{
			public ScriptNameDialog(IEngine engine, IEnumerable<string> scripts) : base(engine)
			{
				var options = new[] { PlaceholderOption }.Concat(scripts);
				ScriptName = new DropDown(options) { MinWidth = 350, Selected = PlaceholderOption };

				ConfirmButton.IsEnabled = false;

				ScriptName.Changed += (sender, args) =>
				{
					ConfirmButton.IsEnabled = args.Selected != PlaceholderOption;
				};

				ConfirmButton.Pressed += (sender, args) => IsConfirmed = true;
				CancelButton.Pressed += (sender, args) => IsConfirmed = false;
			}

			public Label Info { get; } = new Label();

			public DropDown ScriptName { get; }

			public Label DescriptionLabel { get; } = new Label("Description");

			public TextBox Description { get; } = new TextBox { MinWidth = 350, Height = 60 };

			public Button ConfirmButton { get; } = new Button("Confirm") { Width = 120, Height = 25, Style = ButtonStyle.CallToAction };

			public Button CancelButton { get; } = new Button("Cancel") { Width = 120, Height = 25 };

			public bool IsConfirmed { get; private set; }

			public override void Build()
			{
				Clear();
				Layout.RowPosition = 0;
				AddWidget(Info, Layout.RowPosition, 0, 1, 2);
				AddWidget(ScriptName, ++Layout.RowPosition, 0, 1, 2);
				AddWidget(DescriptionLabel, ++Layout.RowPosition, 0, 1, 2);
				AddWidget(Description, ++Layout.RowPosition, 0, 1, 2);
				AddWidget(new WhiteSpace { Height = 25 }, ++Layout.RowPosition, 0);
				AddWidget(ConfirmButton, ++Layout.RowPosition, 0);
				AddWidget(CancelButton, Layout.RowPosition, 1);
			}
		}
	}
}

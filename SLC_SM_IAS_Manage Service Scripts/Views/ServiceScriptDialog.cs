namespace SLC_SM_IAS_Manage_Service_Scripts.Views
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using Skyline.DataMiner.Automation;

	using Skyline.DataMiner.Utils.InteractiveAutomationScript;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.IAS;

	using static Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement.Models;

	public class ServiceScriptDialog
	{
		public static void ShowDialog(ServiceScriptsDialog dialog)
		{
			dialog.Build();

			while (!dialog.IsDone)
			{
				dialog.Show(requireResponse: true);
			}
		}

		public class ServiceScriptsDialog : ScriptDialog
		{
			public const string DropdownPlaceholder = "- Select -";
			private const string ServiceIdParameterLabel = "Service ID";

			private readonly Dictionary<string, TextBox> _scriptInputParameters = new Dictionary<string, TextBox>(StringComparer.OrdinalIgnoreCase);
			private List<string> _scriptInputParameterNames = new List<string>();
			private bool _showParametersSection;

			public ServiceScriptsDialog(IEngine engine, IEnumerable<string> scripts, string serviceId) : base(engine)
			{
				var options = new[] { DropdownPlaceholder }.Concat(scripts);
				ScriptName = new DropDown(options) { MinWidth = 350, Selected = DropdownPlaceholder };
				ServiceIdBox = new TextBox { Text = serviceId ?? String.Empty, IsReadOnly = true, MinWidth = 350 };

				ConfirmButton.IsEnabled = false;
				ConfirmButton.Pressed += (sender, args) => { IsConfirmed = true; IsDone = true; };
				CancelButton.Pressed += (sender, args) => { IsDone = true; };
			}

			public Label Info { get; } = new Label();

			public DropDown ScriptName { get; }

			public Label ServiceIdLabel { get; } = new Label(ServiceIdParameterLabel);

			public TextBox ServiceIdBox { get; }

			public Label DescriptionLabel { get; } = new Label("Description");

			public TextBox Description { get; } = new TextBox { MinWidth = 350, Height = 60 };

			public Button ConfirmButton { get; } = new Button("Confirm") { Width = 120, Height = 25, Style = ButtonStyle.CallToAction };

			public Button CancelButton { get; } = new Button("Cancel") { Width = 120, Height = 25 };

			public bool IsConfirmed { get; private set; }

			public bool IsDone { get; private set; }

			public void SetExtraParameters(List<string> paramNames, List<ServiceScriptInputParameters> savedValues)
			{
				_scriptInputParameterNames = paramNames ?? new List<string>();
				_showParametersSection = _scriptInputParameterNames.Any();
				_scriptInputParameters.Clear();

				foreach (var name in _scriptInputParameterNames)
				{
					var savedValue = savedValues?.FirstOrDefault(p => String.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))?.Value ?? String.Empty;
					_scriptInputParameters[name] = new TextBox { MinWidth = 350, Text = savedValue };
				}
			}

			public List<ServiceScriptInputParameters> GetInputParameterValues()
			{
				return _scriptInputParameterNames
					.Select(name => new ServiceScriptInputParameters
					{
						Name = name,
						Value = _scriptInputParameters.TryGetValue(name, out var field) ? field.Text?.Trim() ?? String.Empty : String.Empty,
					})
					.ToList();
			}

			public override void Build()
			{
				Clear();
				Layout.RowPosition = 0;

				AddWidget(Info, Layout.RowPosition, 0, 1, 2);
				AddWidget(ScriptName, ++Layout.RowPosition, 0, 1, 2);

				AddWidget(DescriptionLabel, ++Layout.RowPosition, 0, 1, 2);
				AddWidget(Description, ++Layout.RowPosition, 0, 1, 2);

				if (_showParametersSection)
				{
					AddWidget(new Label("Input Parameters") { Style = TextStyle.Bold }, ++Layout.RowPosition, 0, 1, 2);
					AddWidget(ServiceIdLabel, ++Layout.RowPosition, 0);
					AddWidget(ServiceIdBox, Layout.RowPosition, 1);

					foreach (var paramName in _scriptInputParameterNames)
					{
						AddWidget(new Label(paramName), ++Layout.RowPosition, 0);
						AddWidget(_scriptInputParameters[paramName], Layout.RowPosition, 1);
					}
				}

				AddWidget(new WhiteSpace { Height = 25 }, ++Layout.RowPosition, 0);
				AddWidget(ConfirmButton, ++Layout.RowPosition, 0);
				AddWidget(CancelButton, Layout.RowPosition, 1);
			}
		}
	}
}
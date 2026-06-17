namespace Skyline.DataMiner.Utils.ServiceManagement.Common.IAS.Dialogs
{
	using System;
	using Library;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;

	public sealed class ErrorDialogView : ScriptDialog
	{
		private const int ButtonHeight = 30;
		private const int ButtonWidth = 110;
		private const int DetailsButtonWidth = 30;
		private const int DetailsColumnWidth = 40;

		public ErrorDialogView(IEngine engine) : base(engine)
		{
		}

		internal Button CloseButton { get; } = new Button("Close") { Height = ButtonHeight, Width = ButtonWidth, Style = ButtonStyle.CallToAction };

		internal TextBox DetailsBox { get; } = new TextBox { MinWidth = 800, IsMultiline = true, Height = 300 };

		internal CollapseButton DetailsButton { get; } = new CollapseButton { Height = ButtonHeight, Width = DetailsButtonWidth, CollapseText = Defaults.SymbolMin, ExpandText = Defaults.SymbolPlus };

		internal Label MessageLabel { get; } = new Label { MaxWidth = 850 };

		public override void Build()
		{
			Clear();
			MinWidth = 850;
			Layout.RowPosition = 0;

			AddWidget(MessageLabel, Layout.RowPosition, 0, 1, 2, HorizontalAlignment.Stretch, VerticalAlignment.Stretch);
			SetColumnWidth(0, DetailsColumnWidth);
			SetColumnWidthStretch(1);

			AddWidget(new WhiteSpace(), ++Layout.RowPosition, 0);
			AddWidget(DetailsButton, ++Layout.RowPosition, 0, verticalAlignment: VerticalAlignment.Top);
			AddWidget(DetailsBox, Layout.RowPosition, 1, 2, 1, verticalAlignment: VerticalAlignment.Stretch);

			AddWidget(new WhiteSpace(), ++Layout.RowPosition, 0);
			AddWidget(CloseButton, ++Layout.RowPosition, 0, 1, 2, HorizontalAlignment.Left);

			DetailsButton.LinkedWidgets.Clear();
			DetailsButton.LinkedWidgets.Add(DetailsBox);
		}
	}

	internal sealed class ErrorDialogModel
	{
		public ErrorDialogModel(string title, string message, string detailedMessage)
		{
			Title = title;
			Message = message;
			DetailedMessage = detailedMessage;
		}

		public string Title { get; }

		public string Message { get; }

		public string DetailedMessage { get; }
	}

	internal class ErrorDialogPresenter
	{
		private readonly ErrorDialogModel model;
		private readonly ErrorDialogView view;

		public ErrorDialogPresenter(ErrorDialogView view, ErrorDialogModel model)
		{
			this.view = view ?? throw new ArgumentNullException(nameof(view));
			this.model = model ?? throw new ArgumentNullException(nameof(model));

			view.Build();

			view.CloseButton.Pressed += OnCloseButtonPressed;
		}

		public void LoadFromModel()
		{
			view.Title = model.Title ?? "Error";
			view.DetailsBox.Text = model.DetailedMessage ?? String.Empty;
			view.MessageLabel.Text = model.Message.Wrap(800) ?? String.Empty;

			view.DetailsButton.Collapse();
			view.DetailsButton.IsVisible = !String.IsNullOrEmpty(model.DetailedMessage);
		}

		private static void OnCloseButtonPressed(object sender, EventArgs e)
		{
			throw new ScriptAbortException("close");
		}
	}
}
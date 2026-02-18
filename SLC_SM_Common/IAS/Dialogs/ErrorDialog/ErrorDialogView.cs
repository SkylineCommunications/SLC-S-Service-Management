namespace Skyline.DataMiner.Utils.ServiceManagement.Common.IAS.Dialogs
{
	using System;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;

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

		internal TextBox DetailsBox { get; } = new TextBox { MaxWidth = 800, IsMultiline = true, IsVisible = false, MinHeight = 100, MaxHeight = 250 };

		internal Button DetailsButton { get; } = new Button("➕") { Height = ButtonHeight, Width = DetailsButtonWidth };

		internal Label MessageLabel { get; } = new Label { MinWidth = 400, MaxWidth = 850 };

		public override void Build()
		{
			MinWidth = 850;
			Layout.RowPosition = 0;

			AddWidget(MessageLabel, Layout.RowPosition, 0, 1, 2, HorizontalAlignment.Stretch, VerticalAlignment.Stretch);
			SetColumnWidth(0, DetailsColumnWidth);
			SetColumnWidthStretch(1);

			AddWidget(new WhiteSpace(), ++Layout.RowPosition, 0);
			AddWidget(DetailsButton, ++Layout.RowPosition, 0, verticalAlignment: VerticalAlignment.Top);
			AddWidget(DetailsBox, ++Layout.RowPosition, 1, HorizontalAlignment.Stretch, VerticalAlignment.Stretch);

			AddWidget(new WhiteSpace(), ++Layout.RowPosition, 0);
			AddWidget(CloseButton, ++Layout.RowPosition, 1);
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
			view.DetailsButton.Pressed += OnDetailsButtonPressed;
		}

		public void LoadFromModel()
		{
			view.Title = model.Title ?? "Error";
			view.DetailsBox.Text = model.DetailedMessage ?? String.Empty;
			view.MessageLabel.Text = model.Message ?? String.Empty;

			view.DetailsButton.IsVisible = !String.IsNullOrEmpty(model.DetailedMessage);
		}

		private static void OnCloseButtonPressed(object sender, EventArgs e)
		{
			throw new ScriptAbortException("close");
		}

		private void OnDetailsButtonPressed(object sender, EventArgs e)
		{
			view.DetailsBox.IsVisible = !view.DetailsBox.IsVisible;
			UpdateDetailsButton();
		}

		private void UpdateDetailsButton()
		{
			view.DetailsButton.Text = view.DetailsBox.IsVisible ? "➖" : "➕";
		}
	}
}
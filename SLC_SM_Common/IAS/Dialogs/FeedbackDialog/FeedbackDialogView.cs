namespace Skyline.DataMiner.Utils.ServiceManagement.Common.IAS.Dialogs.FeedbackDialog
{
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;

	internal class FeedbackDialogView : ScriptDialog
	{
		public FeedbackDialogView(IEngine engine) : base(engine)
		{
		}

		public Label Info { get; } = new Label();

		public TextBox Message { get; } = new TextBox { IsMultiline = true, MinWidth = 300 };

		public Button ConfirmButton { get; } = new Button("Confirm") { Width = 150, Height = 25, Style = ButtonStyle.CallToAction };

		public override void Build()
		{
			Clear();
			Layout.RowPosition = 0;

			Title = "Details required";

			AddWidget(Info, Layout.RowPosition, 0, 1, 2);
			AddWidget(Message, ++Layout.RowPosition, 0, 1, 2);

			AddWidget(new WhiteSpace { Height = 25 }, ++Layout.RowPosition, 0);

			AddWidget(ConfirmButton, ++Layout.RowPosition, 0);
		}
	}
}
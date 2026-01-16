namespace Skyline.DataMiner.Utils.ServiceManagement.Common.IAS.Dialogs.FeedbackDialog
{
	using System;

	internal class FeedbackDialogPresenter
	{
		private readonly FeedbackDialogModel model;
		private readonly FeedbackDialogView view;

		public FeedbackDialogPresenter(FeedbackDialogView view, FeedbackDialogModel model)
		{
			this.view = view ?? throw new ArgumentNullException(nameof(view));
			this.model = model ?? throw new ArgumentNullException(nameof(model));

			Init();

			Confirm += (sender, arg) => { model.Message = view.Message.Text; };
		}

		public event EventHandler<EventArgs> Confirm;

		public void BuildView()
		{
			view.Build();
		}

		public void LoadFromModel()
		{
			view.Info.Text = model.Info;
			view.Message.Tooltip = "Please provide further details...";
		}

		private void Init()
		{
			view.ConfirmButton.Pressed += OnConfirmButtonPressed;
		}

		private void OnConfirmButtonPressed(object sender, EventArgs e)
		{
			Confirm?.Invoke(this, EventArgs.Empty);
		}
	}
}
namespace Skyline.DataMiner.Utils.ServiceManagement.Common.IAS
{
	using System;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.IAS.Dialogs;

	public static class EngineExtensions
	{
		public static bool ShowConfirmDialog(this IEngine engine, string message)
		{
			var model = new Dialogs.ConfirmDialog.ConfirmDialogModel(message);
			var view = new Dialogs.ConfirmDialog.ConfirmDialogView(engine);
			var presenter = new Dialogs.ConfirmDialog.ConfirmDialogPresenter(view, model);

			var confirmed = false;
			presenter.Cancel += (sender, arg) => { confirmed = false; };
			presenter.Confirm += (sender, arg) => { confirmed = true; };

			presenter.LoadFromModel();
			presenter.BuildView();

			view.Show(requireResponse: true);

			return confirmed;
		}

		public static string ShowFeedbackDialog(this IEngine engine, string info)
		{
			var model = new Dialogs.FeedbackDialog.FeedbackDialogModel(info);
			var view = new Dialogs.FeedbackDialog.FeedbackDialogView(engine);
			var presenter = new Dialogs.FeedbackDialog.FeedbackDialogPresenter(view, model);

			presenter.LoadFromModel();
			presenter.BuildView();

			view.Show(requireResponse: true);

			return model.Message;
		}

		public static bool ShowPopupDialog(this IEngine engine, string title, string message, string buttonText)
		{
			return ShowPopupDialog(engine, new InteractiveController(engine) { /*ScriptAbortPopupBehavior = ScriptAbortPopupBehavior.HideAlways*/ }, title, message, buttonText);
		}

		public static bool ShowPopupDialog(this IEngine engine, InteractiveController controller, string title, string message, string buttonText)
		{
			var model = new Dialogs.PopupDialog.PopupDialogModel(title, message, buttonText);
			var view = new Dialogs.PopupDialog.PopupDialogView(engine);
			var presenter = new Dialogs.PopupDialog.PopupDialogPresenter(view, model);

			var confirmed = false;
			presenter.Confirm += (sender, arg) => { confirmed = true; };

			presenter.LoadFromModel();
			presenter.BuildView();

			controller.ShowDialog(view);

			return confirmed;
		}

		public static void ShowErrorDialog(this IEngine engine, Exception ex)
		{
			var model = new ErrorDialogModel("Error", ex.Message, ex.ToString());
			var view = new ErrorDialogView(engine);
			var presenter = new ErrorDialogPresenter(view, model);

			presenter.LoadFromModel();

			new InteractiveController(engine) { /*ScriptAbortPopupBehavior = ScriptAbortPopupBehavior.HideAlways*/ }.ShowDialog(view);
		}

		public static YesNoCancelOption ShowYesNoCancelDialog(this IEngine engine, string message)
		{
			var model = new Dialogs.YesNoCancelDialog.YesNoCancelDialogModel(message);
			var view = new Dialogs.YesNoCancelDialog.YesNoCancelDialogView(engine);
			var presenter = new Dialogs.YesNoCancelDialog.YesNoCancelDialogPresenter(view, model);

			YesNoCancelOption result = YesNoCancelOption.Cancel;
			presenter.Cancel += (sender, arg) => { result = YesNoCancelOption.Cancel; };
			presenter.Yes += (sender, arg) => { result = YesNoCancelOption.Yes; };
			presenter.No += (sender, arg) => { result = YesNoCancelOption.No; };

			presenter.LoadFromModel();
			presenter.BuildView();

			view.Show(requireResponse: true);

			return result;
		}
	}
}
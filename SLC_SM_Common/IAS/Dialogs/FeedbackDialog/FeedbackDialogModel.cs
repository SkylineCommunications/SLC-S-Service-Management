namespace Skyline.DataMiner.Utils.ServiceManagement.Common.IAS.Dialogs.FeedbackDialog
{
	using System;

	internal class FeedbackDialogModel
	{
		public FeedbackDialogModel(string info)
		{
			Message = String.Empty;
			Info = info;
		}

		public string Info { get; }

		public string Message { get; set; }
	}
}
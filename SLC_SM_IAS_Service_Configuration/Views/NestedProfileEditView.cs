namespace SLC_SM_IAS_Service_Configuration.Views
{
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;

	public class NestedProfileEditView : Dialog
	{
		public NestedProfileEditView(IEngine engine) : base(engine)
		{
			Title = "Edit Nested Profile";
			MinWidth = 900;
		}

		public Button BtnBack { get; } = new Button("Back");

		public Button BtnSave { get; } = new Button("Save") { Style = ButtonStyle.CallToAction };
	}
}
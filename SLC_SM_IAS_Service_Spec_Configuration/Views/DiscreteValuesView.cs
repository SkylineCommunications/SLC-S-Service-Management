namespace SLC_SM_IAS_Service_Spec_Configuration.Views
{
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;

	public class DiscreteValuesView : Dialog
	{
		public DiscreteValuesView(IEngine engine) : base(engine)
		{
			Title = "Select Eligible Discrete Options";

			AddWidget(Options, 0, 0, 1, 2);
			AddWidget(BtnApply, 1, 0);
			AddWidget(BtnCancel, 1, 1);
		}

		public CheckBoxList<DiscreteValue> Options { get; } = new CheckBoxList<DiscreteValue>();

		public Button BtnApply { get; } = new Button("Apply Selection") { Style = ButtonStyle.CallToAction };

		public Button BtnCancel { get; } = new Button("Cancel");
	}
}
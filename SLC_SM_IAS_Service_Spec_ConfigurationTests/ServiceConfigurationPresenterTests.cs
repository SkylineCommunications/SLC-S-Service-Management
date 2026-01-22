namespace SLC_SM_IAS_Service_Spec_Configuration.Tests.Presenters
{
	using Moq;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;
	using SLC_SM_IAS_Service_Spec_Configuration.Presenters;
	using SLC_SM_IAS_Service_Spec_Configuration.Views;

	[TestClass]
	public class ServiceConfigurationPresenterTests
	{
		[TestMethod]
		public void AddConfigModel_AddsConfiguration()
		{
			// Arrange
			var engine = Mock.Of<IEngine>();
			var view = new ServiceConfigurationView(engine);
			var serviceSpecification = new Models.ServiceSpecification { ConfigurationParameters = new List<Models.ServiceSpecificationConfigurationValue>() };
			var presenter = new ServiceConfigurationPresenter(engine, new InteractiveController(engine), view, serviceSpecification);

			var param = new Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameter
			{
				ID = Guid.NewGuid(),
				Name = "TestParam",
			};

			// Act
			presenter.AddStandaloneParameterConfigModel(param);

			// Assert
			Assert.AreEqual(1, serviceSpecification.ConfigurationParameters.Count);
			Assert.AreEqual(param.ID, serviceSpecification.ConfigurationParameters[0].ConfigurationParameter.ConfigurationParameterId);
		}
	}
}
namespace SLC_SM_UDAPI_ServiceManagement.Tests.Helpers
{
	using System;
	using System.Collections.Generic;

	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using Skyline.DataMiner.SDM;
	using SLC_SM_UDAPI_ServiceManagement.Helpers.Inventory;

	[TestClass]
	public class SpecificationParameterInstanceMatcherTests
	{
		private const string ParameterId = "transport";

		[TestMethod]
		public void MatchAll_LooseMatchBeforeLabelMatch_DoesNotTakeLabelledInstance()
		{
			var main = Instance("Main");
			var backup = Instance("Backup");
			var available = new List<SpecificationParameterInstance> { main, backup };

			var matches = SpecificationParameterInstanceMatcher.MatchAll(new[] { Value("Secondary"), Value("Main") }, available);

			Assert.AreSame(backup, matches[0]);
			Assert.AreSame(main, matches[1]);
			Assert.AreEqual(0, available.Count);
		}

		[TestMethod]
		public void MatchAll_ParameterOnlyMatchBetweenLabelledInstances_ReportsAmbiguity()
		{
			var available = new List<SpecificationParameterInstance> { Instance("Main"), Instance("Backup") };
			int reported = 0;

			SpecificationParameterInstanceMatcher.MatchAll(new[] { Value("Other") }, available, (value, count) => reported = count);

			Assert.AreEqual(2, reported);
		}

		[TestMethod]
		public void Create_DuplicateConfigurationReference_CreatesOneInstance()
		{
			var configuration = new ServiceSpecificationConfigurationValue
			{
				Identifier = Guid.NewGuid().ToString(),
				ConfigurationParameterId = new SdmObjectReference<ConfigurationParameter>(ParameterId),
				MandatoryAtService = true,
			};

			var instances = SpecificationParameterInstance.Create(new[] { configuration, configuration }, new Dictionary<string, ConfigurationParameterValue>());

			Assert.AreEqual(1, instances.Count);
			Assert.AreEqual(ParameterId, instances[0].ParameterId);
		}

		private static SpecificationParameterInstance Instance(string label)
		{
			return new SpecificationParameterInstance { ReferenceId = Guid.NewGuid().ToString(), ParameterId = ParameterId, Label = label };
		}

		private static ConfigurationParameterValue Value(string label)
		{
			return new ConfigurationParameterValue
			{
				Identifier = Guid.NewGuid().ToString(),
				Label = label,
				ConfigurationParameterId = new SdmObjectReference<ConfigurationParameter>(ParameterId),
			};
		}
	}
}

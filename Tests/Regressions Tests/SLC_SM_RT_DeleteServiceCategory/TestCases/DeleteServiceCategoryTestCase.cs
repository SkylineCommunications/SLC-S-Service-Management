namespace SLC_SM_RT_DeleteServiceCategory.TestCases
{
	using System;
	using System.Collections.Generic;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	/// <summary>
	/// Runs the script under test and verifies the service category was removed.
	/// </summary>
	internal sealed class DeleteServiceCategoryTestCase : TestCase
	{
		private readonly ServiceCategoryTestData testData;

		public DeleteServiceCategoryTestCase(ServiceCategoryTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			var scriptParameters = new Dictionary<string, string>
			{
				{ "domId", testData.CategoryId },
			};

			TestTools.ExecuteSubScript(this, engine, Script.ScriptUnderTest, scriptParameters);

			TestTools.Assert(
				testData.ReadCategory() == null,
				$"Service category '{testData.CategoryId}' still exists after the delete script ran.");
		}

		public override void PerformCleanup(IEngine engine)
		{
			testData.CleanUp();
		}
	}
}

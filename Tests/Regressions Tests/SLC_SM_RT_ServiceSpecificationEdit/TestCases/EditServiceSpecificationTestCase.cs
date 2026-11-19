namespace SLC_SM_RT_ServiceSpecificationEdit.TestCases
{
	using System;
	using System.Collections.Generic;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	/// <summary>
	/// Edits the name, description and icon of the specification through the non-interactive "Edit" action of the script under test.
	/// </summary>
	internal sealed class EditServiceSpecificationTestCase : TestCase
	{
		private readonly ServiceSpecificationEditTestData testData;

		public EditServiceSpecificationTestCase(ServiceSpecificationEditTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			var scriptParameters = new Dictionary<string, string>
			{
				{ "DOM ID", testData.SpecificationId },
				{ "Action", "Edit" },

				// A non-empty payload selects the non-interactive API flow of the script.
				{ "Payload", ServiceSpecificationEditTestData.BuildEditPayload(testData.EditedName, testData.EditedDescription, testData.EditedIcon) },
			};

			TestTools.ExecuteSubScript(this, engine, Script.ScriptUnderTest, scriptParameters);
		}

		public override void PerformCleanup(IEngine engine)
		{
			// Cleanup of every fixture is owned by the test data and triggered by the last test case.
		}
	}
}

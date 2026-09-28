namespace SLC_Service_Management_RT.QaPortal
{
	/// <summary>
	/// A regression test as it is registered on the QAPortal element.
	/// </summary>
	internal class RegressionTest
	{
		/// <summary>
		/// Gets or sets the order in which the test is executed within its group.
		/// </summary>
		public int ExecOrder { get; set; }

		/// <summary>
		/// Gets or sets the group the test is shown in.
		/// </summary>
		public string Group { get; set; }

		/// <summary>
		/// Gets or sets the display name of the test. This is the key of the row on the QAPortal element.
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// Gets or sets the Automation script the QAPortal element runs for this test.
		/// </summary>
		public string Script { get; set; }
	}
}

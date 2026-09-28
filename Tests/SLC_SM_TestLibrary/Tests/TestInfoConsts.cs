namespace Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests
{
	using System.Collections.Generic;

	/// <summary>
	/// The QA Portal metadata that identifies the Service Management regression tests.
	/// </summary>
	public static class TestInfoConsts
	{
		/// <summary>
		/// The prefix of every Service Management regression test name.
		/// </summary>
		public const string TestNamePrefix = "RT_ServiceManagement";

		/// <summary>
		/// Gets the contact address reported to the QA Portal.
		/// </summary>
		public static string Contact => "squad.deploy-swift@skyline.be";

		/// <summary>
		/// Gets the QA Portal project identifiers the results are reported under.
		/// </summary>
		public static List<int> ProjectIds => new List<int> { 21075 };
	}
}

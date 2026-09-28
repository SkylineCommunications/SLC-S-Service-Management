namespace Skyline.DataMiner.Utils.ServiceManagement.Testing.Reporting
{
	/// <summary>
	/// Determines where the results of a regression test run are sent.
	/// </summary>
	public enum ReportPublishingMode
	{
		/// <summary>
		/// Writes a human readable summary of the test report as an information event.
		/// This is the mode that is currently active for the Service Management regression tests.
		/// </summary>
		InformationEvent = 0,

		/// <summary>
		/// Writes the raw QA Portal report JSON to the "report" script output.
		/// The Skyline QAPortal connector reads that output and forwards it to the portal.
		/// </summary>
		QaPortalScriptOutput = 1,

		/// <summary>
		/// Posts the report straight to the QA Portal REST API, bypassing the QAPortal element.
		/// Requires a fully configured <see cref="QaPortalSettings"/>.
		/// </summary>
		QaPortalApi = 2,
	}
}

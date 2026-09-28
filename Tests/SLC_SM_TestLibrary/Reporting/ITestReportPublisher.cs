namespace Skyline.DataMiner.Utils.ServiceManagement.Testing.Reporting
{
	using QAPortalAPI.Models.ReportingModels;

	using Skyline.DataMiner.Automation;

	/// <summary>
	/// Sends the results of a completed regression test run to a destination.
	/// </summary>
	public interface ITestReportPublisher
	{
		/// <summary>
		/// Publishes the given report.
		/// Implementations must never throw; publishing problems are reported through the engine log.
		/// </summary>
		/// <param name="engine">Link with SLAutomation process.</param>
		/// <param name="report">The report of the completed run.</param>
		void Publish(IEngine engine, TestReport report);
	}
}

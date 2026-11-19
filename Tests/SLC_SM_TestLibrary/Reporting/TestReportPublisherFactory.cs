namespace Skyline.DataMiner.Utils.ServiceManagement.Testing.Reporting
{
	using System;

	/// <summary>
	/// Creates the <see cref="ITestReportPublisher"/> that matches a <see cref="ReportPublishingMode"/>.
	/// </summary>
	public static class TestReportPublisherFactory
	{
		/// <summary>
		/// Creates the publisher for the given mode.
		/// </summary>
		/// <param name="mode">The destination of the results.</param>
		/// <param name="settings">The QA Portal connection settings. Only used by <see cref="ReportPublishingMode.QaPortalApi"/>.</param>
		/// <returns>The publisher to use.</returns>
		public static ITestReportPublisher Create(ReportPublishingMode mode, QaPortalSettings settings = null)
		{
			switch (mode)
			{
				case ReportPublishingMode.InformationEvent:
					return new InformationEventPublisher();

				case ReportPublishingMode.QaPortalScriptOutput:
					return new ScriptOutputPublisher();

				case ReportPublishingMode.QaPortalApi:
					return new QaPortalApiPublisher(settings ?? new QaPortalSettings());

				default:
					throw new NotSupportedException($"Unsupported publishing mode '{mode}'.");
			}
		}
	}
}

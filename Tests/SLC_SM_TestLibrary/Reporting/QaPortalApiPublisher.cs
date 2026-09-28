namespace Skyline.DataMiner.Utils.ServiceManagement.Testing.Reporting
{
	using System;

	using QAPortalAPI.APIHelper;
	using QAPortalAPI.Models.ReportingModels;

	using Skyline.DataMiner.Automation;

	/// <summary>
	/// Publishes the test report by posting it straight to the QA Portal REST API.
	/// <para>
	/// Use this only when the regression tests are not launched through a Skyline QAPortal element.
	/// When a QAPortal element is available, prefer <see cref="ScriptOutputPublisher"/> so that the
	/// connector owns the credentials and the retry behaviour.
	/// </para>
	/// </summary>
	public class QaPortalApiPublisher : ITestReportPublisher
	{
		private readonly QaPortalSettings settings;

		/// <summary>
		/// Initializes a new instance of the <see cref="QaPortalApiPublisher"/> class.
		/// </summary>
		/// <param name="settings">The QA Portal connection settings.</param>
		public QaPortalApiPublisher(QaPortalSettings settings)
		{
			this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
		}

		/// <inheritdoc/>
		public void Publish(IEngine engine, TestReport report)
		{
			if (engine == null)
			{
				throw new ArgumentNullException(nameof(engine));
			}

			if (report == null)
			{
				throw new ArgumentNullException(nameof(report));
			}

			if (!settings.IsValid(out string validationInfo))
			{
				engine.Log($"Not posting the result of '{report.TestInfo?.TestName}' to the QA Portal: {validationInfo}");
				return;
			}

			try
			{
				var apiHelper = new QaPortalApiHelper(
					engine.Log,
					settings.PortalLink,
					settings.ClientId,
					settings.ApiKey,
					settings.ProxyUrl ?? String.Empty);

				apiHelper.PostResult(report);
			}
			catch (Exception ex)
			{
				engine.Log($"Posting the result of '{report.TestInfo?.TestName}' to the QA Portal failed: {ex}");
			}
		}
	}
}

namespace Skyline.DataMiner.Utils.ServiceManagement.Testing.Reporting
{
	using System;

	/// <summary>
	/// Connection settings for the QA Portal REST API.
	/// Only required when <see cref="ReportPublishingMode.QaPortalApi"/> is used.
	/// </summary>
	public class QaPortalSettings
	{
		/// <summary>
		/// Gets or sets the base URL of the QA Portal.
		/// </summary>
		public string PortalLink { get; set; }

		/// <summary>
		/// Gets or sets the client identifier issued by the QA Portal.
		/// </summary>
		public string ClientId { get; set; }

		/// <summary>
		/// Gets or sets the API key issued by the QA Portal.
		/// Never hard-code this value; read it from a secure store at runtime.
		/// </summary>
		public string ApiKey { get; set; }

		/// <summary>
		/// Gets or sets the optional proxy URL used to reach the QA Portal.
		/// </summary>
		public string ProxyUrl { get; set; }

		/// <summary>
		/// Determines whether all mandatory settings are filled in.
		/// </summary>
		/// <param name="validationInfo">Describes the missing setting when the settings are incomplete.</param>
		/// <returns><c>true</c> when the settings can be used to reach the QA Portal.</returns>
		public bool IsValid(out string validationInfo)
		{
			if (String.IsNullOrWhiteSpace(PortalLink))
			{
				validationInfo = $"{nameof(PortalLink)} is not configured.";
				return false;
			}

			if (String.IsNullOrWhiteSpace(ClientId))
			{
				validationInfo = $"{nameof(ClientId)} is not configured.";
				return false;
			}

			if (String.IsNullOrWhiteSpace(ApiKey))
			{
				validationInfo = $"{nameof(ApiKey)} is not configured.";
				return false;
			}

			validationInfo = String.Empty;
			return true;
		}
	}
}

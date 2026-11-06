namespace SLC_SM_UDAPI_ServiceManagement.Models.Inventory
{
	using System;

	/// <summary>
	/// Flat configuration record consumed by the service and order-item configuration views.
	/// </summary>
	public sealed class ConfigurationRecord
	{
		public string VersionId { get; set; }

		public string VersionName { get; set; }

		public string VersionDescription { get; set; }

		public DateTime? VersionStart { get; set; }

		public DateTime? VersionEnd { get; set; }

		public bool IsActive { get; set; }

		public string RecordType { get; set; }

		public string ConfigurationId { get; set; }

		public string ParameterId { get; set; }

		public string ParameterValueId { get; set; }

		public string ParameterName { get; set; }

		public string ParameterType { get; set; }

		public string ParameterValue { get; set; }

		public bool Mandatory { get; set; }

		public string ProfileId { get; set; }

		public string ProfileName { get; set; }

		public string ProfileDefinitionId { get; set; }

		public string ProfileDefinitionName { get; set; }

		public bool ProfileReusable { get; set; }

		public bool ProfileMandatory { get; set; }

		public string NestedProfileId { get; set; }

		public string NestedProfileName { get; set; }

		public string NestedProfileDefinitionId { get; set; }

		public string NestedProfileDefinitionName { get; set; }

		public string ParameterLabel { get; set; }

		public string ParameterIsLinked { get; set; }

		public string ParameterUnit { get; set; }

		public string ParameterUnitId { get; set; }

		public string ParameterStart { get; set; }

		public string ParameterEnd { get; set; }

		public string ParameterStepSize { get; set; }

		public string ParameterDecimals { get; set; }

		public string ParameterOptions { get; set; }

		public string ParameterFixed { get; set; }

		public string ParameterNotApplicable { get; set; }

		public string ExposeAtOrder { get; set; }

		public string MandatoryAtOrder { get; set; }

		public string MandatoryAtService { get; set; }
	}
}

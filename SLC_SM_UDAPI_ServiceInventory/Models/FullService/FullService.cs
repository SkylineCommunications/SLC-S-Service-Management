namespace SLC_SM_UDAPI_ServiceInventory.Models.FullService
{
	using System;
	using System.Collections.Generic;


	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using Skyline.DataMiner.SDM;

	/// <summary>
	/// Full representation of a service where the referenced data models are resolved and embedded.
	/// </summary>
	public sealed class FullService
	{
		/// <summary>Gets or sets the service identifier.</summary>
		public string Identifier { get; set; }

		/// <summary>Gets or sets the current status.</summary>
		public string Status { get; set; }

		/// <summary>Gets or sets the name.</summary>
		public string Name { get; set; }

		/// <summary>Gets or sets the service ID.</summary>
		public string ServiceID { get; set; }

		/// <summary>Gets or sets the description.</summary>
		public string Description { get; set; }

		/// <summary>Gets or sets the icon.</summary>
		public string Icon { get; set; }

		/// <summary>Gets or sets the start time.</summary>
		public DateTime? StartTime { get; set; }

		/// <summary>Gets or sets the end time.</summary>
		public DateTime? EndTime { get; set; }

		/// <summary>Gets or sets a value indicating whether a monitoring service is generated.</summary>
		public bool? GenerateMonitoringService { get; set; }

		/// <summary>Gets or sets the monitoring service.</summary>
		public string MonitoringService { get; set; }

		/// <summary>Gets or sets the resolved service category.</summary>
		public ServiceCategory Category { get; set; }

		/// <summary>Gets or sets the service specification Id.</summary>
		public SdmObjectReference<ServiceSpecification> ServiceSpecificationId { get; set; }

		/// <summary>Gets or sets the resolved current service configuration.</summary>
		public FullServiceConfigurationVersion ServiceConfiguration { get; set; }

		/// <summary>Gets or sets all resolved configuration versions.</summary>
		public List<FullServiceConfigurationVersion> ConfigurationVersions { get; set; } = new List<FullServiceConfigurationVersion>();

		/// <summary>Gets or sets the service items.</summary>
		public List<ServiceItem> ServiceItems { get; set; } = new List<ServiceItem>();

		/// <summary>Gets or sets the service item relationships.</summary>
		public List<ServiceItemRelationship> ServiceItemsRelationships { get; set; } = new List<ServiceItemRelationship>();

		/// <summary>Gets or sets the service scripts.</summary>
		public List<ServiceScript> ServiceScripts { get; set; } = new List<ServiceScript>();
	}
}

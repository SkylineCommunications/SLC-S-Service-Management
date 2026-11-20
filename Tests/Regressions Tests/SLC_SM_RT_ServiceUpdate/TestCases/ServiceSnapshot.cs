namespace SLC_SM_RT_ServiceUpdate.TestCases
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using Skyline.DataMiner.SDM;

	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// The values of a service that an edit of its general information must not change.
	/// </summary>
	internal sealed class ServiceSnapshot
	{
		public ServiceSnapshot(Models.Service service)
		{
			if (service == null)
			{
				throw new ArgumentNullException(nameof(service));
			}

			Identifier = service.Identifier;
			ServiceIdentifier = service.ServiceID;
			Status = service.Status.ToString();
			Description = service.Description ?? String.Empty;
			SpecificationId = service.ServiceSpecificationId != null ? service.ServiceSpecificationId.Identifier ?? String.Empty : String.Empty;
			ActiveVersionId = service.ServiceConfigurationId != null ? service.ServiceConfigurationId.Identifier ?? String.Empty : String.Empty;
			VersionIds = (service.ConfigurationVersions ?? new List<SdmObjectReference<Models.ServiceConfigurationVersion>>())
				.Select(reference => reference.Identifier)
				.ToList();
			Items = (service.ServiceItems ?? new List<Models.ServiceItem>())
				.Where(item => item != null)
				.Select(item => $"{item.ServiceItemID}|{item.Label}|{item.Type}|{item.Script}|{item.DefinitionReference}|{item.ImplementationReference}")
				.ToList();
			Relationships = (service.ServiceItemsRelationships ?? new List<Models.ServiceItemRelationship>())
				.Where(relationship => relationship != null)
				.Select(relationship => $"{relationship.Type}|{relationship.ParentServiceItem}|{relationship.ParentServiceItemInterfaceId}|{relationship.ChildServiceItem}|{relationship.ChildServiceItemInterfaceId}")
				.ToList();
		}

		public string Identifier { get; }

		public string ServiceIdentifier { get; }

		public string Status { get; }

		public string Description { get; }

		public string SpecificationId { get; }

		public string ActiveVersionId { get; }

		public IReadOnlyList<string> VersionIds { get; }

		public IReadOnlyList<string> Items { get; }

		public IReadOnlyList<string> Relationships { get; }
	}
}

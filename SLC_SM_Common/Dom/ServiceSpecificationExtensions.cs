namespace Library.Dom
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Saves service specifications with the sections required by the specification DOM definition.
	/// </summary>
	public static class ServiceSpecificationExtensions
	{
		/// <summary>
		/// Creates a service specification.
		/// </summary>
		/// <param name="api">Service management API helper.</param>
		/// <param name="specification">Specification to create.</param>
		/// <returns>The created specification.</returns>
		public static Models.ServiceSpecification CreateSpecification(this IServiceManagementApiHelper api, Models.ServiceSpecification specification)
		{
			return Save(api, specification, s => api.ServiceCatalog.ServiceSpecifications.Create(s));
		}

		/// <summary>
		/// Updates a service specification.
		/// </summary>
		/// <param name="api">Service management API helper.</param>
		/// <param name="specification">Specification to update.</param>
		/// <returns>The updated specification.</returns>
		public static Models.ServiceSpecification UpdateSpecification(this IServiceManagementApiHelper api, Models.ServiceSpecification specification)
		{
			return Save(api, specification, s => api.ServiceCatalog.ServiceSpecifications.Update(s));
		}

		/// <summary>
		/// Creates or updates a service specification.
		/// </summary>
		/// <param name="api">Service management API helper.</param>
		/// <param name="specification">Specification to create or update.</param>
		/// <returns>The saved specification.</returns>
		public static Models.ServiceSpecification CreateOrUpdateSpecification(this IServiceManagementApiHelper api, Models.ServiceSpecification specification)
		{
			return Save(api, specification, s => api.ServiceCatalog.ServiceSpecifications.CreateOrUpdate(new[] { s })?.FirstOrDefault() ?? s);
		}

		/// <summary>
		/// Determines whether a service item is the empty placeholder that is stored because the specification DOM definition requires one.
		/// </summary>
		/// <param name="item">Service item to check.</param>
		/// <returns><see langword="true"/> when the item is <see langword="null"/> or the placeholder.</returns>
		public static bool IsServiceItemPlaceholder(Models.ServiceItem item)
		{
			return item == null
				|| (!item.ServiceItemID.HasValue
					&& String.IsNullOrWhiteSpace(item.Label)
					&& String.IsNullOrWhiteSpace(item.DefinitionReference)
					&& String.IsNullOrWhiteSpace(item.ImplementationReference)
					&& String.IsNullOrWhiteSpace(item.Script)
					&& String.IsNullOrWhiteSpace(item.Icon));
		}

		/// <summary>
		/// Determines whether a relationship is the empty placeholder that is stored because the specification DOM definition requires one.
		/// </summary>
		/// <param name="relationship">Relationship to check.</param>
		/// <returns><see langword="true"/> when the relationship is <see langword="null"/> or links no service items.</returns>
		public static bool IsRelationshipPlaceholder(Models.ServiceItemRelationship relationship)
		{
			return relationship == null
				|| (String.IsNullOrWhiteSpace(relationship.ParentServiceItem) && String.IsNullOrWhiteSpace(relationship.ChildServiceItem));
		}

		/// <summary>
		/// Returns the specification's service items without the DOM placeholder, for copying into a service.
		/// </summary>
		/// <param name="specification">Specification to read; may be <see langword="null"/>.</param>
		/// <returns>The real service items.</returns>
		public static List<Models.ServiceItem> GetRealServiceItems(this Models.ServiceSpecification specification)
		{
			return (specification?.ServiceItems ?? Enumerable.Empty<Models.ServiceItem>())
				.Where(item => !IsServiceItemPlaceholder(item))
				.ToList();
		}

		/// <summary>
		/// Returns the specification's service item relationships without the DOM placeholder, for copying into a service.
		/// </summary>
		/// <param name="specification">Specification to read; may be <see langword="null"/>.</param>
		/// <returns>The real relationships.</returns>
		public static List<Models.ServiceItemRelationship> GetRealServiceItemRelationships(this Models.ServiceSpecification specification)
		{
			return (specification?.ServiceItemsRelationships ?? Enumerable.Empty<Models.ServiceItemRelationship>())
				.Where(relationship => !IsRelationshipPlaceholder(relationship))
				.ToList();
		}

		private static Models.ServiceSpecification Save(
			IServiceManagementApiHelper api,
			Models.ServiceSpecification specification,
			Func<Models.ServiceSpecification, Models.ServiceSpecification> save)
		{
			if (api == null)
			{
				throw new ArgumentNullException(nameof(api));
			}

			if (specification == null)
			{
				throw new ArgumentNullException(nameof(specification));
			}

			EnsureRequiredSections(specification);
			return save(specification) ?? specification;
		}

		// The specification DOM definition requires a service item and a relationship section, and stores empty reference lists as null.
		private static void EnsureRequiredSections(Models.ServiceSpecification specification)
		{
			specification.ServiceItems = specification.ServiceItems ?? new List<Models.ServiceItem>();
			if (specification.ServiceItems.Count == 0)
			{
				specification.ServiceItems.Add(new Models.ServiceItem());
			}

			specification.ServiceItemsRelationships = specification.ServiceItemsRelationships ?? new List<Models.ServiceItemRelationship>();
			if (specification.ServiceItemsRelationships.Count == 0)
			{
				specification.ServiceItemsRelationships.Add(new Models.ServiceItemRelationship());
			}

			if (specification.ConfigurationParameters != null && specification.ConfigurationParameters.Count == 0)
			{
				specification.ConfigurationParameters = null;
			}

			if (specification.ConfigurationProfiles != null && specification.ConfigurationProfiles.Count == 0)
			{
				specification.ConfigurationProfiles = null;
			}
		}
	}
}
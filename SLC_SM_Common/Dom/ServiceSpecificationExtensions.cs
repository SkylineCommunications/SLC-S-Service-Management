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
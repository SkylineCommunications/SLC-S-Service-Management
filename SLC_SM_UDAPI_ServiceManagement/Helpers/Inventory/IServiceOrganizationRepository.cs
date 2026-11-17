namespace SLC_SM_UDAPI_ServiceManagement.Helpers.Inventory
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// Reads and writes the organization of inventory services. The SDM service model has no organization,
	/// so it is stored on the service through the People &amp; Organizations API.
	/// </summary>
	public interface IServiceOrganizationRepository
	{
		/// <summary>Reads the organization names.</summary>
		/// <returns>The organization names by ID.</returns>
		IReadOnlyDictionary<Guid, string> ReadOrganizationNames();

		/// <summary>Reads the organization of services.</summary>
		/// <param name="serviceIds">The service DOM instance IDs.</param>
		/// <returns>The organization IDs by service DOM instance ID; services without an organization are left out.</returns>
		IReadOnlyDictionary<Guid, Guid> ReadServiceOrganizationIds(IEnumerable<Guid> serviceIds);

		/// <summary>Sets or clears the organization of a service.</summary>
		/// <param name="serviceId">The service DOM instance ID.</param>
		/// <param name="organizationId">The organization ID, or <see langword="null"/> to clear it.</param>
		void SetOrganization(Guid serviceId, Guid? organizationId);
	}
}

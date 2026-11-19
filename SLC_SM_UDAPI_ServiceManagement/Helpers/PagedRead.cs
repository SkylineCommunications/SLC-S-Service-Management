namespace SLC_SM_UDAPI_ServiceManagement.Helpers
{
	using System.Collections.Generic;
	using System.Linq;

	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using Skyline.DataMiner.SDM;

	internal static class PagedRead
	{
		private const int PageSize = 500;

		/// <summary>
		/// Reads every matching record page by page. A single unpaged read can return an incomplete set on large systems.
		/// </summary>
		public static List<T> All<T>(IReadableRepository<T> repository, FilterElement<T> filter = null)
			where T : SdmObject<T>
		{
			return repository.ReadCursorPages(filter, PageSize).SelectMany(page => page).ToList();
		}
	}
}

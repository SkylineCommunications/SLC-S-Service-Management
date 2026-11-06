/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE        VERSION        AUTHOR            COMMENTS

09/10/2026	1.0.0.1		SKA, Skyline	Initial version
****************************************************************************
*/

namespace SLC_SM_GQIDS_Get_Service_Organizations
{
	using System;
	using System.Linq;

	using DomHelpers.SlcPeople_Organizations;
	using Skyline.DataMiner.Analytics.GenericInterface;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.PeopleAndOrganization;
	using SLC_SM_Common.Extensions;
	using OrganizationModel = Skyline.DataMiner.ProjectApi.ServiceManagement.API.PeopleAndOrganization.Models.Organization;

	/// <summary>
	/// Provides the organizations available for service-order selection.
	/// </summary>
	[GQIMetaData(Name = DataSourceName)]
	public sealed class GetOrganizations : IGQIDataSource, IGQIOnInit
	{
		private const string DataSourceName = "Get_ServiceOrganizations";

		private GQIDMS dms;
		private IGQILogger logger;

		/// <summary>
		/// Gets the organization columns.
		/// </summary>
		/// <returns>The organization columns.</returns>
		public GQIColumn[] GetColumns()
		{
			return new GQIColumn[]
			{
				new GQIStringColumn("Organization ID"),
				new GQIStringColumn("Organization Name"),
			};
		}

		/// <summary>
		/// Initializes the source.
		/// </summary>
		/// <param name="args">The initialization arguments.</param>
		/// <returns>The initialization result.</returns>
		public OnInitOutputArgs OnInit(OnInitInputArgs args)
		{
			this.dms = args.DMS;
			this.logger = args.Logger;
			this.logger.MinimumLogLevel = GQILogLevel.Debug;
			return default;
		}

		/// <summary>
		/// Gets the next organization page.
		/// </summary>
		/// <param name="args">The page arguments.</param>
		/// <returns>The organization page.</returns>
		public GQIPage GetNextPage(GetNextPageInputArgs args)
		{
			return this.logger.PerformanceLogger(nameof(this.GetNextPage), this.BuildPage);
		}

		private GQIPage BuildPage()
		{
			try
			{
				if (!this.dms.DomModelExists(SlcPeople_OrganizationsIds.ModuleId))
				{
					return new GQIPage(Array.Empty<GQIRow>()) { HasNextPage = false };
				}

				var organizations = new DataHelperOrganization(this.dms.GetConnection())
					.ReadBasicDetails()
					.Where(organization => organization != null
						&& organization.ID != Guid.Empty
						&& !string.IsNullOrWhiteSpace(organization.Name))
					.OrderBy(organization => organization.Name, StringComparer.OrdinalIgnoreCase)
					.Select(this.BuildRow)
					.ToArray();

				return new GQIPage(organizations) { HasNextPage = false };
			}
			catch (Exception exception)
			{
				this.dms.GenerateInformationMessage($"GQIDS|{DataSourceName}|Exception: {exception}");
				this.logger.Error($"GQIDS|{DataSourceName}|Exception: {exception}");
				throw;
			}
		}

		private GQIRow BuildRow(OrganizationModel organization)
		{
			return new GQIRow(organization.ID.ToString(), new[] { new GQICell { Value = organization.ID.ToString() }, new GQICell { Value = organization.Name } });
		}
	}
}

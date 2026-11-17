namespace SLC_SM_GQIDS_Get_Service_Specifications
{
	using System;
	using System.Linq;
	using DomHelpers.SlcServicemanagement;
	using Skyline.DataMiner.Analytics.GenericInterface;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using SLC_SM_Common.Extensions;
	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	[GQIMetaData(Name = DataSourceName)]
	public sealed class GetServiceSpecifications : IGQIDataSource, IGQIOnInit
	{
		private const string DataSourceName = "Get_ServiceSpecifications";

		private GQIDMS _dms;
		private IGQILogger _logger;

		public GQIColumn[] GetColumns()
		{
			return new GQIColumn[]
			{
				new GQIStringColumn("Specification ID"),
				new GQIStringColumn("Name"),
				new GQIStringColumn("Description"),
				new GQIDateTimeColumn("Created At"),
				new GQIStringColumn("Created By"),
				new GQIDateTimeColumn("Last Modified"),
				new GQIStringColumn("Last Modified By"),
				new GQIIntColumn("Service Item Count"),
			};
		}

		public OnInitOutputArgs OnInit(OnInitInputArgs args)
		{
			_dms = args.DMS;
			_logger = args.Logger;
			_logger.MinimumLogLevel = GQILogLevel.Debug;
			return default;
		}

		public GQIPage GetNextPage(GetNextPageInputArgs args)
		{
			return _logger.PerformanceLogger(nameof(GetNextPage), BuildPage);
		}

		private GQIPage BuildPage()
		{
			try
			{
				var api = new ServiceManagementApiHelper(_dms.GetConnection(), "Service Catalog");
				var specifications = api.ServiceCatalog.ServiceSpecifications
					.Read(new TRUEFilterElement<Models.ServiceSpecification>())
					.ToList();
				var domHelper = new DomHelper(_dms.SendMessages, SlcServicemanagementIds.ModuleId);
				var metadata = domHelper.DomInstances
					.Read(DomInstanceExposers.DomDefinitionId.Equal(SlcServicemanagementIds.Definitions.ServiceSpecifications.Id))
					.Select(instance => new ServiceSpecificationsInstance(instance))
					.ToDictionary(instance => instance.ID.Id.ToString(), StringComparer.OrdinalIgnoreCase);

				var rows = specifications
					.Select(specification => new GQIRow(
						specification.Identifier ?? String.Empty,
						new[]
						{
							new GQICell { Value = specification.Identifier ?? String.Empty },
							new GQICell { Value = specification.Name ?? String.Empty },
							new GQICell { Value = specification.Description ?? String.Empty },
							new GQICell { Value = metadata.TryGetValue(specification.Identifier ?? String.Empty, out var instance) ? instance.CreatedAt.UtcDateTime : (DateTime?)null },
							new GQICell { Value = metadata.TryGetValue(specification.Identifier ?? String.Empty, out instance) ? instance.CreatedBy : String.Empty },
							new GQICell { Value = metadata.TryGetValue(specification.Identifier ?? String.Empty, out instance) ? instance.LastModified.UtcDateTime : (DateTime?)null },
							new GQICell { Value = metadata.TryGetValue(specification.Identifier ?? String.Empty, out instance) ? instance.LastModifiedBy : String.Empty },
							new GQICell { Value = specification.ServiceItems?.Count ?? 0 },
						}))
					.ToArray();

				return new GQIPage(rows) { HasNextPage = false };
			}
			catch (Exception exception)
			{
				_dms.GenerateInformationMessage($"GQIDS|{DataSourceName}|Exception: {exception}");
				_logger.Error($"GQIDS|{DataSourceName}|Exception: {exception}");
				throw;
			}
		}
	}
}

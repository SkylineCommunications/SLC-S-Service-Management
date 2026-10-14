/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE        VERSION        AUTHOR            COMMENTS

09/10/2026	1.0.0.1		SKA, Skyline	Initial version
****************************************************************************
*/

namespace SLC_SM_GQIDS_Get_Related_Service_Orders
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Skyline.DataMiner.Analytics.GenericInterface;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;
	using SLC_SM_Common.Extensions;
	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	[GQIMetaData(Name = DataSourceName)]
	public sealed class GetRelatedServiceOrders : IGQIDataSource, IGQIInputArguments, IGQIOnInit
	{
		private const string DataSourceName = "Get_RelatedServiceOrders";
		private readonly GQIStringArgument serviceIdArg = new GQIStringArgument("Service ID") { IsRequired = true };
		private GQIDMS dms;
		private IGQILogger logger;
		private IServiceManagementApiHelper serviceManagementApiHelper;
		private string serviceId;

		public GQIColumn[] GetColumns()
		{
			return new GQIColumn[]
			{
				new GQIStringColumn("Order ID"),
				new GQIStringColumn("Order Name"),
				new GQIStringColumn("Service Order Item ID"),
				new GQIStringColumn("Action"),
				new GQIStringColumn("State"),
			};
		}

		public GQIArgument[] GetInputArguments() => new[] { serviceIdArg };

		public OnInitOutputArgs OnInit(OnInitInputArgs args)
		{
			dms = args.DMS;
			logger = args.Logger;
			logger.MinimumLogLevel = GQILogLevel.Debug;
			serviceManagementApiHelper = new ServiceManagementApiHelper(dms.GetConnection(), "Service Ordering");
			return default;
		}

		public OnArgumentsProcessedOutputArgs OnArgumentsProcessed(OnArgumentsProcessedInputArgs args)
		{
			serviceId = args.GetArgumentValue(serviceIdArg);
			return new OnArgumentsProcessedOutputArgs();
		}

		public GQIPage GetNextPage(GetNextPageInputArgs args)
		{
			return logger.PerformanceLogger(nameof(GetNextPage), BuildPage);
		}

		private GQIPage BuildPage()
		{
			try
			{
				if (!Guid.TryParse(serviceId, out var selectedServiceId))
				{
					return new GQIPage(Array.Empty<GQIRow>()) { HasNextPage = false };
				}

				var serviceOrderItems = serviceManagementApiHelper.ServiceOrder.ServiceOrderItems
					.Read(Models.ServiceOrderItemExposers.ServiceInfo.ServiceId.Equal(new Skyline.DataMiner.SDM.SdmObjectReference<Models.Service>(selectedServiceId.ToString())))
					.ToDictionary(item => item.Identifier, StringComparer.OrdinalIgnoreCase);
				if (serviceOrderItems.Count == 0)
				{
					return new GQIPage(Array.Empty<GQIRow>()) { HasNextPage = false };
				}

				var rows = serviceManagementApiHelper.ServiceOrder.ServiceOrders
					.Read(new TRUEFilterElement<Models.ServiceOrder>())
					.SelectMany(order => (order.OrderItems ?? new List<Models.ServiceOrderEntry>())
						.Where(entry => entry?.ServiceOrderItemId != null
							&& !String.IsNullOrWhiteSpace(entry.ServiceOrderItemId.Identifier)
							&& serviceOrderItems.ContainsKey(entry.ServiceOrderItemId.Identifier))
						.Select(entry =>
						{
							var item = serviceOrderItems[entry.ServiceOrderItemId.Identifier];
							return new GQIRow(
								$"{order.Identifier}:{item.Identifier}",
								new[]
								{
									new GQICell { Value = order.Identifier ?? String.Empty },
									new GQICell { Value = order.Name ?? String.Empty },
									new GQICell { Value = item.Identifier ?? String.Empty },
									new GQICell { Value = item.Action ?? String.Empty },
									new GQICell { Value = item.Status.GetDescription() },
								});
						}))
					.ToArray();

				return new GQIPage(rows) { HasNextPage = false };
			}
			catch (Exception exception)
			{
				dms.GenerateInformationMessage($"GQIDS|{DataSourceName}|Exception: {exception}");
				logger.Error($"GQIDS|{DataSourceName}|Exception: {exception}");
				throw;
			}
		}
	}
}

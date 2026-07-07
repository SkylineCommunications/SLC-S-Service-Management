namespace SLCSMGQIDSGetServiceScripts
{
	using System;
	using System.Linq;

	using DomHelpers.SlcServicemanagement;

	using Skyline.DataMiner.Analytics.GenericInterface;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM;

	using SLC_SM_Common.Extensions;

	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement.Models;

	[GQIMetaData(Name = DataSourceName)]
	public class GetServiceScripts : IGQIDataSource, IGQIInputArguments, IGQIOnInit
	{
		private const string DataSourceName = "SLC_SM_GQIDS_Get Service Scripts";

		private readonly GQIStringArgument serviceNameArg = new GQIStringArgument("Service Name") { IsRequired = true };

		private GQIDMS _dms;
		private IGQILogger _logger;
		private string _serviceName;

		public GQIColumn[] GetColumns()
		{
			return new GQIColumn[]
			{
				new GQIStringColumn("Script Name"),
				new GQIStringColumn("Description"),
				new GQIStringColumn("Input Parameters"),
			};
		}

		public GQIArgument[] GetInputArguments()
		{
			return new GQIArgument[] { serviceNameArg };
		}

		public OnArgumentsProcessedOutputArgs OnArgumentsProcessed(OnArgumentsProcessedInputArgs args)
		{
			_serviceName = args.GetArgumentValue(serviceNameArg) ?? String.Empty;
			return new OnArgumentsProcessedOutputArgs();
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
			return _logger.PerformanceLogger(nameof(GetNextPage), BuildupRows);
		}

		private GQIPage BuildupRows()
		{
			try
			{
				return new GQIPage(GetRows()) { HasNextPage = false };
			}
			catch (Exception e)
			{
				_dms.GenerateInformationMessage($"GQIDS|{DataSourceName}|Exception: {e}");
				_logger.Error($"GQIDS|{DataSourceName}|Exception: {e}");
				return new GQIPage(new GQIRow[0]);
			}
		}

		private GQIRow[] GetRows()
		{
			if (String.IsNullOrWhiteSpace(_serviceName))
			{
				return new GQIRow[0];
			}

			if (!Guid.TryParse(_serviceName, out var serviceGuid))
			{
				_logger.Error($"GQIDS|{DataSourceName}|Invalid service GUID: '{_serviceName}'");
				return new GQIRow[0];
			}

			var helpers = new DataHelpersServiceManagement(_dms.GetConnection());
			var service = helpers.Services.Read(ServiceExposers.Guid.Equal(serviceGuid)).SingleOrDefault();

			if (service == null || service.ServiceScripts == null || !service.ServiceScripts.Any())
			{
				return new GQIRow[0];
			}

			return service.ServiceScripts
				.Select(script => new GQIRow(
					new[]
					{
						new GQICell { Value = script.Name ?? String.Empty },
						new GQICell { Value = script.Description ?? String.Empty },
						new GQICell { Value = script.InputParameters },
					})
				{
					Metadata = new GenIfRowMetadata(new[] { new ObjectRefMetadata { Object = new DomInstanceId(service.ID) { ModuleId = SlcServicemanagementIds.ModuleId } } }),
				})
				.ToArray();
		}
	}
}
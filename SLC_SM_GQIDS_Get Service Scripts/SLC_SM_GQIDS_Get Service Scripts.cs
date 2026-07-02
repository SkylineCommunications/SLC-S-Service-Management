namespace SLCSMGQIDSGetServiceScripts
{
	using System;
	using System.Linq;

	using DomHelpers.SlcServicemanagement;

	using Skyline.DataMiner.Analytics.GenericInterface;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API;

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

			var helpers = new DataHelpersServiceManagement(_dms.GetConnection());
			Models.Service service = helpers.Services.ReadBasicDetails()
				.FirstOrDefault(s => String.Equals(s.Name, _serviceName, StringComparison.OrdinalIgnoreCase));

			if (service == null)
			{
				return new GQIRow[0];
			}

			var scripts = ParseScripts(service.Scripts);
			return scripts
				.Select(scriptName => new GQIRow(
					new[]
					{
						new GQICell { Value = scriptName },
					})
				{
					Metadata = new GenIfRowMetadata(new[] { new ObjectRefMetadata { Object = new DomInstanceId(service.ID) { ModuleId = SlcServicemanagementIds.ModuleId } } }),
				})
				.ToArray();
		}

		private static string[] ParseScripts(string scripts)
		{
			if (String.IsNullOrWhiteSpace(scripts))
			{
				return new string[0];
			}

			return scripts
				.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
				.Select(s => s.Trim())
				.Where(s => !String.IsNullOrWhiteSpace(s))
				.ToArray();
		}
	}
}
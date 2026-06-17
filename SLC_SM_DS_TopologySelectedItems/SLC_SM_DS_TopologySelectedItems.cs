/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE		VERSION		AUTHOR			COMMENTS

11/06/2025	1.0.0.1		RCA, Skyline	Initial version
****************************************************************************
*/
namespace SLCSMDSTopologySelectedItems
{
	using System;
	using System.Linq;
	using Skyline.DataMiner.Analytics.GenericInterface;
	using SLC_SM_Common.Extensions;

	/// <summary>
	///     Represents a data source.
	///     See: https://aka.dataminer.services/gqi-external-data-source for a complete example.
	/// </summary>
	[GQIMetaData(Name = DataSourceName)]
	public sealed class SLCSMDSTopologySelectedItems : IGQIDataSource, IGQIInputArguments, IGQIOnInit
	{
		private const string DataSourceName = "SLC_SM_DS_TopologySelectedItems";
		private readonly GQIStringArgument nodeIdsArg = new GQIStringArgument("NodeIds") { IsRequired = false };
		private readonly GQIStringArgument connectionIdsArg = new GQIStringArgument("ConnectionIds") { IsRequired = false };
		private string _nodeIds;
		private string _connectionIds;
		private GQIDMS _dms;
		private IGQILogger _logger;

		public GQIColumn[] GetColumns()
		{
			return new GQIColumn[]
			{
				new GQIStringColumn("Item"),
				new GQIStringColumn("Ids"),
			};
		}

		public GQIArgument[] GetInputArguments()
		{
			return new GQIArgument[] { nodeIdsArg, connectionIdsArg };
		}

		public GQIPage GetNextPage(GetNextPageInputArgs args)
		{
			return _logger.PerformanceLogger(nameof(GetNextPage), BuildupRows);
		}

		public OnArgumentsProcessedOutputArgs OnArgumentsProcessed(OnArgumentsProcessedInputArgs args)
		{
			_nodeIds = args.GetArgumentValue(nodeIdsArg);
			_connectionIds = args.GetArgumentValue(connectionIdsArg);

			return new OnArgumentsProcessedOutputArgs();
		}

		public OnInitOutputArgs OnInit(OnInitInputArgs args)
		{
			_dms = args.DMS;
			_logger = args.Logger;
			_logger.MinimumLogLevel = GQILogLevel.Debug;
			return default;
		}

		private GQIRow BuildRow(string type, string ids)
		{
			return new GQIRow(
				new[]
				{
					new GQICell { Value = type },
					new GQICell { Value = ids },
				});
		}

		private GQIPage BuildupRows()
		{
			try
			{
				string ids;
				string type;

				if ((_nodeIds == null || !_nodeIds.Any())
				    && (_connectionIds == null || !_connectionIds.Any()))
				{
					type = "None";
					ids = String.Empty;
				}
				else if (_nodeIds != null && _nodeIds.Any())
				{
					type = "Node";
					ids = _nodeIds;
				}
				else
				{
					type = "Connection";
					ids = _connectionIds;
				}

				return new GQIPage(new[] { BuildRow(type, ids) });
			}
			catch (Exception e)
			{
				_dms.GenerateInformationMessage($"GQIDS|{DataSourceName}|Exception: {e}");
				_logger.Error($"GQIDS|{DataSourceName}|Exception: {e}");
				return new GQIPage(Enumerable.Empty<GQIRow>().ToArray());
			}
		}
	}
}
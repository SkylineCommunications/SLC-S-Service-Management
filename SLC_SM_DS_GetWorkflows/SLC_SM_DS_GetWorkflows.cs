/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE		VERSION		AUTHOR			COMMENTS

16/06/2025	1.0.0.1		RCA, Skyline	Initial version
****************************************************************************
*/
namespace SLCSMDSGetWorkflows
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using DomHelpers.SlcProperties;
	using DomHelpers.SlcWorkflow;
	using Library.Dom;
	using Skyline.DataMiner.Analytics.GenericInterface;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.Messages;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using SLC_SM_Common.Extensions;

	/// <summary>
	///     Represents a data source.
	///     See: https://aka.dataminer.services/gqi-external-data-source for a complete example.
	/// </summary>
	[GQIMetaData(Name = DataSourceName)]
	public sealed class SLCSMDSGetWorkflows : IGQIDataSource, IGQIOnInit
	{
		private const string DataSourceName = "SLC_SM_DS_GetWorkflows";
		private GQIDMS _dms;
		private IGQILogger _logger;

		public GQIColumn[] GetColumns()
		{
			return new GQIColumn[]
			{
				new GQIStringColumn("ID"),
				new GQIStringColumn("Name"),
				new GQIStringColumn("Category"),
				new GQIStringColumn("Type"),
			};
		}

		public GQIPage GetNextPage(GetNextPageInputArgs args)
		{
			return _logger.PerformanceLogger(nameof(GetNextPage), BuildupRows);
		}

		public OnInitOutputArgs OnInit(OnInitInputArgs args)
		{
			_dms = args.DMS;
			_logger = args.Logger;
			_logger.MinimumLogLevel = GQILogLevel.Debug;

			return default;
		}

		private static string FetchBookingCategory(LiteElementInfoEvent liteElementInfoEvent, IEnumerable<PropertyChangeEventMessage> properties)
		{
			return properties
				.FirstOrDefault(
					p =>
						p.DataMinerID == liteElementInfoEvent.DataMinerID &&
						p.ElementID == liteElementInfoEvent.ElementID)
				?.Value ?? "Other";
		}

		private static string FetchWorkflowCategory(WorkflowsInstance workflow, ICollection<PropertyValuesInstance> propertyValues)
		{
			return propertyValues
				.FirstOrDefault(p => p.PropertyValueInfo.LinkedObjectID == workflow.ID.Id.ToString())
				?.PropertyValues.FirstOrDefault(v => v.PropertyName == "Category")
				?.Value ?? "Other";
		}

		private GQIRow BuildRow(WorkflowsInstance workflow, string category)
		{
			return new GQIRow(
				new[]
				{
					new GQICell { Value = workflow.ID.Id.ToString() },
					new GQICell { Value = workflow.Name },
					new GQICell { Value = category },
					new GQICell { Value = "Workflow" },
				});
		}

		private GQIRow BuildRow(LiteElementInfoEvent liteElement, string category)
		{
			return new GQIRow(
				new[]
				{
					new GQICell { Value = $"{liteElement.DataMinerID}/{liteElement.ElementID}" },
					new GQICell { Value = liteElement.Name },
					new GQICell { Value = category },
					new GQICell { Value = "SRMBooking" },
				});
		}

		private GQIPage BuildupRows()
		{
			try
			{
				var workflowsResult = _logger.PerformanceLogger(
					"Get DOM Workflows",
					() => WorkflowExtensions.GetWorkflows(_dms.SendMessages));

				var workflowPropertyValues = _logger.PerformanceLogger(
					"Get DOM Workflow Properties",
					() => PropertyExtensions.GetCategories(_dms.SendMessages));

				var workflows = _logger.PerformanceLogger(
					"Build Workflows Rows",
					() => workflowsResult
						.Select(wf => BuildRow(wf, FetchWorkflowCategory(wf, workflowPropertyValues)))
						.ToArray());

				var bookings = _logger.PerformanceLogger(
					"Get Booking Managers",
					() => _dms.SendMessages(new GetLiteElementInfo { ProtocolName = "Skyline Booking Manager" })
						.OfType<LiteElementInfoEvent>()
						.ToArray());

				var bookingPropertyValues = _logger.PerformanceLogger(
					"Get Booking Properties",
					() =>
					{
						var getPropertyMessages = bookings
							.Select(
								b => new GetPropertyValueMessage
								{
									ObjectID = $"{b.DataMinerID}/{b.ElementID}",
									ObjectType = "Element",
									PropertyName = "Category",
								})
							.Cast<DMSMessage>()
							.ToArray();
						return _dms.SendMessages(getPropertyMessages)
							.OfType<PropertyChangeEventMessage>()
							.ToArray();
					});

				var bookingRows = _logger.PerformanceLogger(
					"Build Booking Rows",
					() => bookings
						.Select(b => BuildRow(b, FetchBookingCategory(b, bookingPropertyValues)))
						.ToArray());

				return new GQIPage(workflows.Concat(bookingRows).ToArray());
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
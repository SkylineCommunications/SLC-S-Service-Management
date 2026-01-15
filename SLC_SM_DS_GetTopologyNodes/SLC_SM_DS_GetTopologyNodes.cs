/*
****************************************************************************
*  Copyright (c) 2025,  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

By using this script, you expressly agree with the usage terms and
conditions set out below.
This script and all related materials are protected by copyrights and
other intellectual property rights that exclusively belong
to Skyline Communications.

A user license granted for this script is strictly for personal use only.
This script may not be used in any way by anyone without the prior
written consent of Skyline Communications. Any sublicensing of this
script is forbidden.

Any modifications to this script by the user are only allowed for
personal use and within the intended purpose of the script,
and will remain the sole responsibility of the user.
Skyline Communications will not be responsible for any damages or
malfunctions whatsoever of the script resulting from a modification
or adaptation by the user.

The content of this script is confidential information.
The user hereby agrees to keep this confidential information strictly
secret and confidential and not to disclose or reveal it, in whole
or in part, directly or indirectly to any person, entity, organization
or administration without the prior written consent of
Skyline Communications.

Any inquiries can be addressed to:

	Skyline Communications NV
	Ambachtenstraat 33
	B-8870 Izegem
	Belgium
	Tel.	: +32 51 31 35 69
	Fax.	: +32 51 31 01 29
	E-mail	: info@skyline.be
	Web		: www.skyline.be
	Contact	: Ben Vandenberghe

****************************************************************************
Revision History:

DATE		VERSION		AUTHOR			COMMENTS

26/06/2025	1.0.0.1		RCA, Skyline	Initial version
****************************************************************************
*/
namespace SLCSMDSGetTopologyNodes
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using DomHelpers.SlcProperties;
	using DomHelpers.SlcServicemanagement;
	using DomHelpers.SlcWorkflow;
	using Library.Dom;
	using Skyline.DataMiner.Analytics.GenericInterface;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.Messages;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.Net.Sections;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.Relationship;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM;
	using SLC_SM_Common.Extensions;
	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement.Models;

	/// <summary>
	///     Represents a data source.
	///     See: https://aka.dataminer.services/gqi-external-data-source for a complete example.
	/// </summary>
	[GQIMetaData(Name = DataSourceName)]
	public sealed class SLCSMDSGetTopologyNodes : IGQIDataSource, IGQIOnInit, IGQIInputArguments
	{
		private const string DataSourceName = "SLC_SM_DS_GetTopologyNodes";
		private const string PropertyNameIcon = "Icon";
		private readonly GQIStringArgument domIdArg = new GQIStringArgument("DOM ID") { IsRequired = true };
		private Guid _domId;
		private GQIDMS _dms;
		private IGQILogger _logger;

		public GQIColumn[] GetColumns()
		{
			// Define data source columns
			// See: https://aka.dataminer.services/igqidatasource-getcolumns
			return new GQIColumn[]
			{
				new GQIStringColumn("ServiceItemId"),
				new GQIStringColumn("ServiceItemType"),
				new GQIStringColumn("ServiceItemLabel"),
				new GQIStringColumn("DefinitionReference"),
				new GQIStringColumn("ReferenceId"),
				new GQIStringColumn("Icon"),
			};
		}

		public GQIArgument[] GetInputArguments()
		{
			return new GQIArgument[]
			{
				domIdArg,
			};
		}

		public GQIPage GetNextPage(GetNextPageInputArgs args)
		{
			return _logger.PerformanceLogger(nameof(GetNextPage), BuildupRows);
		}

		public OnArgumentsProcessedOutputArgs OnArgumentsProcessed(OnArgumentsProcessedInputArgs args)
		{
			if (!Guid.TryParse(args.GetArgumentValue(domIdArg), out _domId))
			{
				_domId = Guid.Empty;
			}

			return new OnArgumentsProcessedOutputArgs();
		}

		public OnInitOutputArgs OnInit(OnInitInputArgs args)
		{
			_dms = args.DMS;
			_logger = args.Logger;
			_logger.MinimumLogLevel = GQILogLevel.Debug;
			return default;
		}

		private GQIRow BuildRow(Models.ServiceItem serviceItem, string referenceId, string icon)
		{
			GQICell[] columns = new[]
				{
					new GQICell { Value = serviceItem.ID.ToString() },
					new GQICell { Value = serviceItem.Type.ToString() },
					new GQICell { Value = serviceItem.Label ?? String.Empty },
					new GQICell { Value = serviceItem.DefinitionReference ?? String.Empty },
					new GQICell { Value = referenceId ?? String.Empty },
					new GQICell { Value = icon ?? String.Empty },
				};
			return new GQIRow(Guid.NewGuid().ToString(), columns);
		}

		private GQIPage BuildupRows()
		{
			try
			{
				var serviceItems = _logger.PerformanceLogger(
					"Get Service Items",
					() =>
						new DataHelperService(_dms.GetConnection()).Read(ServiceExposers.Guid.Equal(_domId)).FirstOrDefault()?.ServiceItems.Where(i => !String.IsNullOrEmpty(i.Label)).ToList()
						?? new DataHelperServiceSpecification(_dms.GetConnection()).Read(ServiceSpecificationExposers.Guid.Equal(_domId)).FirstOrDefault()?.ServiceItems.Where(i => !String.IsNullOrEmpty(i.Label)).ToList()
						?? new List<Models.ServiceItem>());

				var workflowPropertyValues = _logger.PerformanceLogger(
					"Get Workflow Property Values",
					() => PropertyExtensions.GetIcons(_dms.SendMessages));

				var workflowsFilter = serviceItems
					.Where(item => item.Type == SlcServicemanagementIds.Enums.ServiceitemtypesEnum.Workflow)
					.Select(item => DomInstanceExposers.Name.Equal(item.DefinitionReference))
					.Aggregate<FilterElement<DomInstance>, FilterElement<DomInstance>>(null, (acc, next) => acc == null ? next : acc.OR(next));

				var workflows = _logger.PerformanceLogger(
					"Get DOM Workflows",
					() =>
						workflowsFilter != null
							? new DomHelper(_dms.SendMessages, SlcWorkflowIds.ModuleId)
								.DomInstances
								.Read(workflowsFilter)
							: Enumerable.Empty<DomInstance>().ToList());

				var messages = serviceItems
					.Where(item => item.Type == SlcServicemanagementIds.Enums.ServiceitemtypesEnum.SRMBooking)
					.Select(item => new GetLiteElementInfo { NameFilter = item.DefinitionReference })
					.Cast<DMSMessage>()
					.ToArray();

				LiteElementInfoEvent[] elements = Array.Empty<LiteElementInfoEvent>();
				PropertyChangeEventMessage[] elementIcons = Array.Empty<PropertyChangeEventMessage>();
				if (messages.Any())
				{
					elements = _logger.PerformanceLogger(
						"Get Booking Manager Elements",
						() => _dms.SendMessages(messages)
							.OfType<LiteElementInfoEvent>()
							.ToArray());

					if (elements.Any())
					{
						var getPropertyMessages = elements
							.Select(
								element => new GetPropertyValueMessage
								{
									ObjectID = $"{element.DataMinerID}/{element.ElementID}",
									ObjectType = "Element",
									PropertyName = PropertyNameIcon,
								})
							.Cast<DMSMessage>()
							.ToArray();

						elementIcons = _logger.PerformanceLogger(
							"Get Booking Manager Element Properties",
							() => _dms.SendMessages(getPropertyMessages)
								.OfType<PropertyChangeEventMessage>()
								.ToArray());
					}
				}

				var rows = new List<GQIRow>();
				_logger.PerformanceLogger(
					"Build Rows",
					() =>
					{
						foreach (var i in serviceItems)
						{
							rows.Add(BuildRow(i, GetReferenceId(i, elements, workflows), GetIcon(i, elements, workflows, workflowPropertyValues, elementIcons)));
						}
					});

				return new GQIPage(rows.ToArray());
			}
			catch (Exception e)
			{
				_dms.GenerateInformationMessage($"GQIDS|{DataSourceName}|Exception: {e}");
				_logger.Error($"GQIDS|{DataSourceName}|Exception: {e}");
				return new GQIPage(Enumerable.Empty<GQIRow>().ToArray());
			}
		}

		private string GetIcon(
			Models.ServiceItem item,
			LiteElementInfoEvent[] elements,
			List<DomInstance> workflows,
			ICollection<PropertyValuesInstance> propertyValues,
			PropertyChangeEventMessage[] icons)
		{
			if (item.Type == SlcServicemanagementIds.Enums.ServiceitemtypesEnum.SRMBooking)
			{
				var element = elements.FirstOrDefault(e => e.Name == item.DefinitionReference);
				if (element == null)
				{
					return String.Empty;
				}

				return icons
					.FirstOrDefault(i => i.DataMinerID == element.DataMinerID && i.ElementID == element.ElementID)
					?
					.Value ?? String.Empty;
			}

			if (item.Type == SlcServicemanagementIds.Enums.ServiceitemtypesEnum.Workflow)
			{
				var workflow = workflows.FirstOrDefault(i => i.Name == item.DefinitionReference);
				if (workflow == null)
				{
					return String.Empty;
				}

				return propertyValues
					.FirstOrDefault(p => p.PropertyValueInfo.LinkedObjectID == workflow.ID.Id.ToString())
					?
					.PropertyValues.FirstOrDefault(v => v.PropertyName == PropertyNameIcon)
					?.Value ?? String.Empty;
			}

			if (item.Type == SlcServicemanagementIds.Enums.ServiceitemtypesEnum.Service)
			{
				return String.Empty;
			}

			throw new InvalidOperationException("Unsupported service item type");
		}

		private string GetReferenceId(Models.ServiceItem item, LiteElementInfoEvent[] elements, List<DomInstance> workflows)
		{
			if (item.Type == SlcServicemanagementIds.Enums.ServiceitemtypesEnum.SRMBooking)
			{
				var element = elements.FirstOrDefault(e => e.Name == item.DefinitionReference);
				return element != null ? $"{element.DataMinerID}/{element.ElementID}" : String.Empty;
			}

			if (item.Type == SlcServicemanagementIds.Enums.ServiceitemtypesEnum.Workflow)
			{
				return workflows.FirstOrDefault(i => i.Name == item.DefinitionReference)?.ID.Id.ToString() ?? String.Empty;
			}

			if (item.Type == SlcServicemanagementIds.Enums.ServiceitemtypesEnum.Service)
			{
				return new DataHelperLink(_dms.GetConnection()).Read(LinkExposers.ParentID.Equal(_domId.ToString())).FirstOrDefault()?.ChildID ?? String.Empty;
			}

			throw new InvalidOperationException("Unsupported service item type");
		}
	}
}
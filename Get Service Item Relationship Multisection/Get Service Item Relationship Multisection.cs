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

27/05/2025	1.0.0.1		RCA, Skyline	Initial version
****************************************************************************
*/

namespace GetServiceItemRelationshipMultisection
{
	using System;
	using System.Linq;
	using DomHelpers.SlcServicemanagement;
	using DomHelpers.SlcWorkflow;
	using Skyline.DataMiner.Analytics.GenericInterface;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM;
	using SLC_SM_Common.Extensions;

	/// <summary>
	///     Represents a data source.
	///     See: https://aka.dataminer.services/gqi-external-data-source for a complete example.
	/// </summary>
	[GQIMetaData(Name = DataSourceName)]
	public sealed class GetServiceItemRelationshipMultisection : IGQIDataSource, IGQIOnInit, IGQIInputArguments
	{
		private const string DataSourceName = "Get Service Item Relationship Multisection";
		private readonly GQIStringArgument domIdArg = new GQIStringArgument("DOM ID") { IsRequired = false };
		private Guid _specificationId;
		private Models.Service _serviceInstance;
		private Models.ServiceSpecification _serviceSpecificationInstance;
		private DomHelper _wfDomHelper;
		private GQIDMS _dms;
		private WorkflowsInstance[] _workflows;
		private IGQILogger _logger;

		public GQIColumn[] GetColumns()
		{
			return new GQIColumn[]
			{
				new GQIStringColumn("ID"),
				new GQIStringColumn("Type"),
				new GQIStringColumn("Child"),
				new GQIStringColumn("Parent"),
				new GQIStringColumn("Child Interface"),
				new GQIStringColumn("Parent Interface"),
				new GQIStringColumn("Source"),
				new GQIStringColumn("Destination"),
				new GQIStringColumn("Source Interface"),
				new GQIStringColumn("Destination Interface"),
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
			if (!Guid.TryParse(args.GetArgumentValue(domIdArg), out _specificationId))
			{
				_specificationId = Guid.Empty;
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

		private GQIPage BuildupRows()
		{
			try
			{
				if (_specificationId == Guid.Empty)
				{
					return EmptyPage();
				}

				Init();

				var relationships = _serviceInstance?.ServiceItemsRelationships ?? _serviceSpecificationInstance?.ServiceItemsRelationships;
				if (relationships == null)
				{
					return EmptyPage();
				}

				var items = _serviceInstance?.ServiceItems.Select(i => i.ID.ToString()).ToArray()
							?? _serviceSpecificationInstance?.ServiceItems.Select(i => i.ID.ToString()).ToArray();
				if (items == null)
				{
					return EmptyPage();
				}

				return new GQIPage(
					relationships
						.Where(r => items.Contains(r.ChildServiceItem) || items.Contains(r.ParentServiceItem))
						.Select(BuildRow)
						.ToArray());
			}
			catch (Exception e)
			{
				_dms.GenerateInformationMessage($"GQIDS|{DataSourceName}|Exception: {e}");
				_logger.Error($"GQIDS|{DataSourceName}|Exception: {e}");
				return new GQIPage(Enumerable.Empty<GQIRow>().ToArray());
			}
		}

		private GQIRow BuildRow(Models.ServiceItemRelationShip r)
		{
			return new GQIRow(
				new[]
				{
					new GQICell { Value = r.Id },
					new GQICell { Value = r.Type },
					new GQICell { Value = r.ChildServiceItem },
					new GQICell { Value = r.ParentServiceItem },
					new GQICell { Value = r.ChildServiceItemInterfaceId },
					new GQICell { Value = r.ParentServiceItemInterfaceId },
					new GQICell { Value = GetReferencedObjectName(r.ParentServiceItem) },
					new GQICell { Value = GetReferencedObjectName(r.ChildServiceItem) },
					new GQICell { Value = GetInterfaceName(r.ParentServiceItem, r.ParentServiceItemInterfaceId) },
					new GQICell { Value = GetInterfaceName(r.ChildServiceItem, r.ChildServiceItemInterfaceId) },
				});
		}

		private GQIPage EmptyPage()
		{
			return new GQIPage(Array.Empty<GQIRow>());
		}

		private string GetInterfaceName(string serviceItemId, string interfaceId)
		{
			var serviceItem = _serviceInstance?.ServiceItems.FirstOrDefault(item => item.ID.ToString() == serviceItemId)
								?? _serviceSpecificationInstance?.ServiceItems.FirstOrDefault(item => item.ID.ToString() == serviceItemId)
								?? throw new InvalidOperationException($"No Service Item found on the system with ID '{serviceItemId}'");

			var type = serviceItem.Type;

			if (type == SlcServicemanagementIds.Enums.ServiceitemtypesEnum.Workflow)
			{
				////return GetWorkflowInterfaceName(serviceItemId, interfaceId);
				return interfaceId == "1" ? "Default Workflow Output" : "Default Workflow Input";
			}

			if (type == SlcServicemanagementIds.Enums.ServiceitemtypesEnum.SRMBooking)
			{
				return interfaceId == "1" ? "Default SRM Output" : "Default SRM Input";
			}

			if (type == SlcServicemanagementIds.Enums.ServiceitemtypesEnum.Service)
			{
				return interfaceId == "1" ? "Default Service Link Output" : "Default Service Link Input";
			}

			throw new InvalidOperationException($"Unrecognized service item type {type}");
		}

		private string GetReferencedObjectName(string serviceItemId)
		{
			return _serviceInstance?.ServiceItems
					   .FirstOrDefault(i => i.ID.ToString() == serviceItemId)
					   ?.DefinitionReference
				   ?? _serviceSpecificationInstance?.ServiceItems
					   .FirstOrDefault(i => i.ID.ToString() == serviceItemId)
					   ?.DefinitionReference
				   ?? String.Empty;
		}

		private string GetWorkflowInterfaceName(string serviceItemId, string interfaceId)
		{
			var workflowName = GetReferencedObjectName(serviceItemId);
			var node = _workflows
				.FirstOrDefault(w => w.Name == workflowName)
				?.Nodeses
				.FirstOrDefault(n => n.NodeID == interfaceId);

			return node?.NodeAlias ?? String.Empty;
		}

		private void Init()
		{
			_wfDomHelper = new DomHelper(_dms.SendMessages, SlcWorkflowIds.ModuleId);

			_workflows = _wfDomHelper.DomInstances
				.Read(DomInstanceExposers.DomDefinitionId.Equal(SlcWorkflowIds.Definitions.Workflows.Id))
				.Select(w => new WorkflowsInstance(w))
				.ToArray();

			_serviceInstance = new DataHelperService(_dms.GetConnection()).Read(ServiceExposers.Guid.Equal(_specificationId)).FirstOrDefault();
			if (_serviceInstance == null)
			{
				_serviceSpecificationInstance = new DataHelperServiceSpecification(_dms.GetConnection()).Read(ServiceSpecificationExposers.Guid.Equal(_specificationId)).FirstOrDefault();
			}
		}
	}
}
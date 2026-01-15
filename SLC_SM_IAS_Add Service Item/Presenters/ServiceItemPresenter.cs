namespace SLC_SM_IAS_Add_Service_Item.Presenters
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using DomHelpers.SlcServicemanagement;
	using DomHelpers.SlcWorkflow;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.Messages;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.Net.ResourceManager.Objects;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;
	using Skyline.DataMiner.Utils.MediaOps.Common.IOData.Scheduling.Scripts.JobHandler;
	using Skyline.DataMiner.Utils.MediaOps.Helpers.Workflows;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;
	using SLC_SM_Common.Extensions;
	using SLC_SM_IAS_Add_Service_Item.ScriptModels;
	using SLC_SM_IAS_Add_Service_Item.Views;

	public class ServiceItemPresenter
	{
		private readonly IScriptModel _scriptModel;
		private readonly IEngine engine;
		private readonly string[] getServiceItemLabels;
		private readonly List<Models.Service> services = new List<Models.Service>();
		private readonly List<Models.ServiceSpecification> specifications = new List<Models.ServiceSpecification>();
		private readonly ServiceItemView view;
		private List<Option<string>> _allScripts;
		private Workflow[] _workflows;
		private Dictionary<string, List<ServiceReservationInstance>> bookings = new Dictionary<string, List<ServiceReservationInstance>>();

		public ServiceItemPresenter(IEngine engine, ServiceItemView view, string[] getServiceItemLabels, IScriptModel scriptModel)
		{
			this.engine = engine;
			this.view = view;
			this.getServiceItemLabels = getServiceItemLabels;
			this._scriptModel = scriptModel;

			// Load correct types
			view.ServiceItemType.SetOptions(
				new List<Option<SlcServicemanagementIds.Enums.ServiceitemtypesEnum>>
				{
					new Option<SlcServicemanagementIds.Enums.ServiceitemtypesEnum>(
						SlcServicemanagementIds.Enums.Serviceitemtypes.Workflow,
						SlcServicemanagementIds.Enums.ServiceitemtypesEnum.Workflow),
					new Option<SlcServicemanagementIds.Enums.ServiceitemtypesEnum>(
						SlcServicemanagementIds.Enums.Serviceitemtypes.Service,
						SlcServicemanagementIds.Enums.ServiceitemtypesEnum.Service),
					new Option<SlcServicemanagementIds.Enums.ServiceitemtypesEnum>(
						SlcServicemanagementIds.Enums.Serviceitemtypes.SRMBooking,
						SlcServicemanagementIds.Enums.ServiceitemtypesEnum.SRMBooking),
				});

			view.TboxLabel.Changed += (sender, args) => ValidateLabel(args.Value);
			view.ServiceItemType.Changed += (sender, args) => OnUpdateServiceItemType(args.Selected);
			view.DefinitionReferences.Changed += (sender, args) => OnUpdateDefinitionReference(args.Selected, view.ServiceItemType.Selected);
		}

		public List<Option<string>> AllBookingScripts
		{
			get
			{
				if (_allScripts == null)
				{
					_allScripts = engine.PerformanceLogger("Get Scripts", () => GetScriptNames(engine).Select(x => new Option<string>(x)).ToList());
					_allScripts.Insert(0, new Option<string>("-None-", null));
				}

				return _allScripts;
			}
		}

		public string Name => String.IsNullOrWhiteSpace(view.TboxLabel.Text) ? view.TboxLabel.PlaceHolder : view.TboxLabel.Text;

		public Models.ServiceItem Section => new Models.ServiceItem
		{
			Label = Name,
			Type = view.ServiceItemType.Selected,
			DefinitionReference = view.DefinitionReferences.Selected ?? String.Empty,
			Script = view.ScriptSelection.Selected ?? String.Empty,
			ImplementationReference = GetImplementationReference(),
		};

		public Workflow[] WorkFlows
		{
			get
			{
				if (_workflows == null)
				{
					var workflowHelper = new WorkflowHelper(engine);
					_workflows = workflowHelper.GetAllWorkflows().ToArray();
				}

				return _workflows;
			}
		}

		public void LoadFromModel()
		{
			OnUpdateServiceItemType(view.ServiceItemType.Selected);
		}

		public void LoadFromModel(Models.ServiceItem section)
		{
			// Load correct types
			engine.PerformanceLogger("Load", () => LoadFromModel());

			view.BtnAdd.Text = "Save";
			view.TboxLabel.Text = section.Label;

			view.ServiceItemType.Selected = section.Type;
			engine.PerformanceLogger("Update Service Item Type", () => OnUpdateServiceItemType(section.Type));

			if (!String.IsNullOrEmpty(section.DefinitionReference))
			{
				view.DefinitionReferences.Selected = section.DefinitionReference;
				engine.PerformanceLogger("Update Def. Ref.", () => OnUpdateDefinitionReference(view.DefinitionReferences.Selected, view.ServiceItemType.Selected));
			}

			if (!String.IsNullOrEmpty(section.ImplementationReference))
			{
				if (services.Exists(s => s.ID.ToString() == section.ImplementationReference))
				{
					view.ImplementationReferences.Selected = GetServiceDropDownLabel(services.Find(s => s.ID.ToString() == section.ImplementationReference));
				}
				else if (bookings.ContainsKey(view.DefinitionReferences.Selected) && bookings[view.DefinitionReferences.Selected].Exists(b => b.ID.ToString() == section.ImplementationReference))
				{
					view.ImplementationReferences.Selected = GetBookingDropDownLabel(bookings[view.DefinitionReferences.Selected].Find(s => s.ID.ToString() == section.ImplementationReference));
				}
				else
				{
					// future reference
				}
			}

			if (!String.IsNullOrEmpty(section.Script) && view.ScriptSelection.Options.Any(o => o.Value == section.Script))
			{
				view.ScriptSelection.Selected = section.Script;
			}
		}

		public string UpdateJobForWorkFlow(string label)
		{
			if (view.ServiceItemType.Selected != SlcServicemanagementIds.Enums.ServiceitemtypesEnum.Workflow)
			{
				return String.Empty;
			}

			var job = GetJobForOrder(label);
			if (job == null)
			{
				return String.Empty;
			}

			var action = new EditJobAction
			{
				DomJobId = job.ID.Id,
				End = _scriptModel.End,
			};

			// Only add start update if the job is not already running
			if (job.JobInfo.JobStart <= DateTime.UtcNow)
			{
				action.Start = _scriptModel.Start;
			}

			action.SendToJobHandler(engine, true);

			return job.ID.Id.ToString();
		}

		public bool Validate()
		{
			bool ok = true;

			ok &= ValidateLabel(Name);

			return ok;
		}

		private static bool FallsWithTimeRange(Models.Service service, DateTime? currentStart, DateTime? currentEnd)
		{
			bool endTimeOk = service.EndTime == null || (currentEnd != null && currentEnd < service.EndTime);

			return endTimeOk && currentStart >= service.StartTime;
		}

		private static string GetBookingDropDownLabel(ServiceReservationInstance reservation)
		{
			if (reservation == null)
			{
				return String.Empty;
			}

			return reservation.Name;
		}

		private static string GetServiceDropDownLabel(Models.Service s)
		{
			if (s == null)
			{
				return String.Empty;
			}

			return $"{s.Name} ({s.ServiceID})";
		}

		private static string[] GetScriptNames(IEngine engine)
		{
			try
			{
				var scriptsResponseMessage = engine.SendSLNetSingleResponseMessage(new GetInfoMessage { DataMinerID = -1, HostingDataMinerID = -1, Type = InfoType.Scripts }) as GetScriptsResponseMessage;
				if (scriptsResponseMessage == null)
				{
					return new string[0];
				}

				return scriptsResponseMessage.Scripts.Where(x =>
				{
					if (x.IndexOf("Booking", StringComparison.OrdinalIgnoreCase) < 0)
					{
						return false;
					}

					var scriptInfoResponseMessage = engine.SendSLNetSingleResponseMessage(new GetScriptInfoMessage { Name = x }) as GetScriptInfoResponseMessage;

					return scriptInfoResponseMessage.Parameters.Any(p => p.Description == "Booking Manager Element Info");
				}).OrderBy(x => x).ToArray();
			}
			catch (Exception)
			{
				return new string[0];
			}
		}

		private string GetImplementationReference()
		{
			if (view.ServiceItemType.Selected == SlcServicemanagementIds.Enums.ServiceitemtypesEnum.Service)
			{
				return services.Find(s => view.ImplementationReferences.Selected == GetServiceDropDownLabel(s))?.ID.ToString() ?? String.Empty;
			}

			if (view.ServiceItemType.Selected == SlcServicemanagementIds.Enums.ServiceitemtypesEnum.SRMBooking && bookings.ContainsKey(view.DefinitionReferences.Selected))
			{
				return bookings[view.DefinitionReferences.Selected].Find(x => view.ImplementationReferences.Selected == GetBookingDropDownLabel(x))?.ID.ToString() ?? String.Empty;
			}

			return String.Empty;
		}

		private JobsInstance GetJobForOrder(string label)
		{
			var jobFilter = DomInstanceExposers.FieldValues.DomInstanceField(SlcWorkflowIds.Sections.JobInfo.JobDescription)
				.Equal($"{_scriptModel.ID} | {label}")
				.OR(DomInstanceExposers.FieldValues.DomInstanceField(SlcWorkflowIds.Sections.JobInfo.JobDescription).Equal($"{_scriptModel.ID}|{label}"));

			var domHelper = new DomHelper(engine.SendSLNetMessages, SlcWorkflowIds.ModuleId);
			return domHelper.DomInstances.Read(jobFilter)
				.Select(x => new JobsInstance(x))
				.FirstOrDefault();
		}

		private void OnUpdateDefinitionReference(string selectedDefinitionReference, SlcServicemanagementIds.Enums.ServiceitemtypesEnum serviceItemType)
		{
			if (String.IsNullOrEmpty(selectedDefinitionReference))
			{
				view.ScriptSelection.Selected = null;
				return;
			}

			if (serviceItemType == SlcServicemanagementIds.Enums.ServiceitemtypesEnum.SRMBooking)
			{
				engine.PerformanceLogger("Update Impl. Ref. SRM Booking", () => UpdateImplementationReferenceForTypeSrmBooking(selectedDefinitionReference));
				UpdateLabelPlaceholder(selectedDefinitionReference);

				var el = engine.FindElement(selectedDefinitionReference);
				if (el == null)
				{
					view.ScriptSelection.Selected = null;
					return;
				}

				var scriptName = Convert.ToString(el.GetParameter(195));
				if (!view.ScriptSelection.Options.Any(o => o.Value == scriptName))
				{
					view.ScriptSelection.Selected = null;
					return;
				}

				view.ScriptSelection.Selected = scriptName;
			}
			else if (serviceItemType == SlcServicemanagementIds.Enums.ServiceitemtypesEnum.Service)
			{
				var selectedSpec = specifications.Find(s => s.Name == selectedDefinitionReference);
				UpdateImplementationReferenceForTypeService(selectedSpec);
				UpdateLabelPlaceholder(selectedDefinitionReference);
			}
			else
			{
				// Not required
			}
		}

		private void OnUpdateServiceItemType(SlcServicemanagementIds.Enums.ServiceitemtypesEnum serviceItemType)
		{
			if (serviceItemType == SlcServicemanagementIds.Enums.ServiceitemtypesEnum.Workflow)
			{
				var workflowOptions = WorkFlows.Select(x => x.Name).OrderBy(x => x).ToList();
				view.DefinitionReferences.SetOptions(workflowOptions);
				if (workflowOptions.Exists(x => x == "Default"))
				{
					view.DefinitionReferences.Selected = "Default";
				}

				view.ScriptSelection.SetOptions(new List<string>());
				view.ScriptSelection.IsEnabled = false;
			}
			else if (serviceItemType == SlcServicemanagementIds.Enums.ServiceitemtypesEnum.Service)
			{
				if (specifications.Count < 1)
				{
					specifications.AddRange(new DataHelperServiceSpecification(engine.GetUserConnection()).Read());
				}

				List<string> specOptions = specifications.Select(x => x.Name).OrderBy(x => x).ToList();
				specOptions.Insert(0, "-None-");
				view.DefinitionReferences.SetOptions(specOptions);
				var selectedSpec = specifications.Find(s => s.Name == view.DefinitionReferences.Selected);

				UpdateImplementationReferenceForTypeService(selectedSpec);

				view.ScriptSelection.SetOptions(new List<string>());
				view.ScriptSelection.IsEnabled = false;
			}
			else
			{
				view.ScriptSelection.SetOptions(AllBookingScripts);
				view.ScriptSelection.IsEnabled = true;

				var bookingManagers = engine.SendSLNetMessage(new GetLiteElementInfo { ProtocolName = "Skyline Booking Manager" }).OfType<LiteElementInfoEvent>().Select(x => x.Name).OrderBy(x => x).ToArray();
				view.DefinitionReferences.SetOptions(bookingManagers);
				OnUpdateDefinitionReference(view.DefinitionReferences.Selected, serviceItemType);
			}

			view.LblImplementationReference.IsVisible = serviceItemType != SlcServicemanagementIds.Enums.ServiceitemtypesEnum.Workflow;
			view.ImplementationReferences.IsVisible = serviceItemType != SlcServicemanagementIds.Enums.ServiceitemtypesEnum.Workflow;

			UpdateLabelPlaceholder(view.DefinitionReferences.Selected);
		}

		private void UpdateImplementationReferenceForTypeService(Models.ServiceSpecification selectedSpec)
		{
			services.Clear();
			if (selectedSpec != null)
			{
				services.AddRange(new DataHelperService(engine.GetUserConnection()).Read(ServiceExposers.ServiceSpecifcation.Equal(selectedSpec.ID)));
			}
			else
			{
				services.AddRange(new DataHelperService(engine.GetUserConnection()).Read());
			}

			DateTime? currentStart = _scriptModel.Start;
			DateTime? currentEnd = _scriptModel.End;
			var serviceOptions = services.Where(x => _scriptModel.ID != x.ID && FallsWithTimeRange(x, currentStart, currentEnd))
				.Select(GetServiceDropDownLabel)
				.OrderBy(s => s)
				.ToList();
			serviceOptions.Insert(0, "-None-");
			view.ImplementationReferences.SetOptions(serviceOptions);
		}

		private void UpdateImplementationReferenceForTypeSrmBooking(string selectedDefinitionReference)
		{
			if (!String.IsNullOrEmpty(selectedDefinitionReference) && !bookings.ContainsKey(selectedDefinitionReference))
			{
				bookings[selectedDefinitionReference] = new ResourceManagerHelper(engine.SendSLNetSingleResponseMessage).GetReservationInstances(
					ReservationInstanceExposers.Properties.DictStringField("Booking Manager").Equal(selectedDefinitionReference)
					.AND(ServiceReservationInstanceExposers.ContributingResourceID.Equal(Guid.Empty))
					.AND(ReservationInstanceExposers.End.GreaterThan(DateTime.UtcNow)))
					.OfType<ServiceReservationInstance>()
					.Where(x => x.ContributingResourceID == Guid.Empty)
					.ToList();
			}

			var options = bookings.ContainsKey(selectedDefinitionReference) ? bookings[selectedDefinitionReference].Select(GetBookingDropDownLabel).OrderBy(x => x).ToList() : new List<string>();
			options.Insert(0, "-None-");
			view.ImplementationReferences.SetOptions(options);
		}

		private void UpdateLabelPlaceholder(string definitionReference)
		{
			string tboxLabelPlaceHolder = $"{definitionReference}";
			while (getServiceItemLabels.Contains(tboxLabelPlaceHolder))
			{
				tboxLabelPlaceHolder += " (1)";
			}

			view.TboxLabel.PlaceHolder = tboxLabelPlaceHolder;
		}

		private bool ValidateLabel(string newValue)
		{
			if (String.IsNullOrWhiteSpace(newValue))
			{
				view.ErrorLabel.Text = "Placeholder will be used";
				return true;
			}

			if (getServiceItemLabels.Contains(newValue, StringComparer.InvariantCultureIgnoreCase))
			{
				view.ErrorLabel.Text = "Label already exists!";
				return false;
			}

			view.ErrorLabel.Text = String.Empty;
			return true;
		}
	}
}
namespace SLC_SM_IAS_Add_Service_Order_1.Presenters
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using DomHelpers.SlcPeople_Organizations;
	using DomHelpers.SlcServicemanagement;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.Helper;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;

	using SLC_SM_IAS_Add_Service_Order_1.Views;

	public class ServiceOrderPresenter
	{
		private readonly IEngine engine;
		private readonly List<string> getServiceOrderItemLabels;
		private readonly List<string> usedOrderIds;
		private readonly ServiceOrderView view;
		private Models.ServiceOrder instanceToReturn;
		private PeopleInstance[] peopleInstances;

		public ServiceOrderPresenter(IEngine engine, ServiceOrderView view, List<string> getServiceOrderItemLabels, List<string> usedOrderIds)
		{
			this.engine = engine;
			this.view = view;
			this.getServiceOrderItemLabels = getServiceOrderItemLabels;
			this.usedOrderIds = usedOrderIds;
			instanceToReturn = new Models.ServiceOrder
			{
				ContactIds = new List<Guid>(),
				OrderItems = new List<Models.ServiceOrderItems>(),
			};

			view.TboxName.Changed += (sender, args) => ValidateLabel(args.Value);
			view.Org.Changed += (sender, args) => UpdateContactOnSelectedOrganization(args.Selected);
			view.BtnCancel.Pressed += (sender, args) => throw new ScriptAbortException("OK");
		}

		public Models.ServiceOrder GetData
		{
			get
			{
				instanceToReturn.Name = Name;
				instanceToReturn.OrderId = view.OrderId.Text;
				instanceToReturn.ExternalID = view.ExternalId.Text;
				instanceToReturn.Priority = view.Priority.Selected;
				instanceToReturn.Description = view.Description.Text;
				instanceToReturn.OrganizationId = view.Org.Selected?.ID.Id;

				if (instanceToReturn.ContactIds == null)
				{
					instanceToReturn.ContactIds = new List<Guid>();
				}

				instanceToReturn.ContactIds.Clear();
				view.Contact.CheckedOptions.Select(x => x.Value.ID.Id).ForEach(f => instanceToReturn.ContactIds.Add(f));

				return instanceToReturn;
			}
		}

		public string Name => String.IsNullOrWhiteSpace(view.TboxName.Text) ? view.TboxName.PlaceHolder : view.TboxName.Text;

		public void LoadFromModel()
		{
			string defaultOrderId = GetDefaultOrderId(usedOrderIds);
			view.TboxName.PlaceHolder = defaultOrderId;
			view.OrderId.Text = defaultOrderId;

			// Load correct types
			view.Priority.SetOptions(
				new List<Option<SlcServicemanagementIds.Enums.ServiceorderpriorityEnum>>
				{
					new Option<SlcServicemanagementIds.Enums.ServiceorderpriorityEnum>(
						SlcServicemanagementIds.Enums.Serviceorderpriority.High,
						SlcServicemanagementIds.Enums.ServiceorderpriorityEnum.High),
					new Option<SlcServicemanagementIds.Enums.ServiceorderpriorityEnum>(
						SlcServicemanagementIds.Enums.Serviceorderpriority.Medium,
						SlcServicemanagementIds.Enums.ServiceorderpriorityEnum.Medium),
					new Option<SlcServicemanagementIds.Enums.ServiceorderpriorityEnum>(
						SlcServicemanagementIds.Enums.Serviceorderpriority.Low,
						SlcServicemanagementIds.Enums.ServiceorderpriorityEnum.Low),
				});

			var orgDomHelper = new DomHelper(engine.SendSLNetMessages, SlcPeople_OrganizationsIds.ModuleId);
			var orgInstances = orgDomHelper.DomInstances.Read(DomInstanceExposers.DomDefinitionId.Equal(SlcPeople_OrganizationsIds.Definitions.Organizations.Id))
				.Select(x => new OrganizationsInstance(x))
				.ToArray();

			var orgOptions = orgInstances.Select(x => new Option<OrganizationsInstance>(x.Name, x)).ToList();
			orgOptions.Insert(0, new Option<OrganizationsInstance>("-None-", null));
			view.Org.SetOptions(orgOptions);

			peopleInstances = orgDomHelper.DomInstances.Read(DomInstanceExposers.DomDefinitionId.Equal(SlcPeople_OrganizationsIds.Definitions.People.Id))
				.Select(x => new PeopleInstance(x))
				.ToArray();

			UpdateContactOnSelectedOrganization(view.Org.Selected);
		}

		public void LoadFromModel(Models.ServiceOrder instance)
		{
			instanceToReturn = instance;
			getServiceOrderItemLabels.RemoveAll(x => x == instance.Name);

			// Load correct types
			LoadFromModel();

			view.BtnAdd.Text = "Save";
			view.TboxName.Text = instance.Name;
			view.TboxName.PlaceHolder = instance.OrderId;
			view.OrderId.Text = instance.OrderId;

			if (instance.Priority.HasValue)
			{
				view.Priority.Selected = instance.Priority.Value;
			}

			if (instance.OrganizationId.HasValue && view.Org.Options.Any(x => x.Value?.ID.Id == instance.OrganizationId.Value))
			{
				view.Org.Selected = view.Org.Options.First(x => x.Value?.ID.Id == instance.OrganizationId.Value).Value;
			}

			if (instance.ContactIds.Any() && view.Contact.Options.Any(x => instance.ContactIds.Contains(x.Value.ID.Id)))
			{
				var checkedOptions = view.Contact.Options.Where(x => instance.ContactIds.Contains(x.Value.ID.Id)).ToList();
				foreach (Option<PeopleInstance> option in checkedOptions)
				{
					view.Contact.Check(option);
				}
			}
		}

		public bool Validate()
		{
			bool ok = true;

			ok &= ValidateLabel(Name);

			return ok;
		}

		private static string GetDefaultOrderId(List<string> usedOrderIds)
		{
			var maxServiceId = usedOrderIds.Select(label => Int32.TryParse(label.Split('-').Last(), out int res) ? res : 0).ToArray();
			int newNumber = maxServiceId.Length > 0 ? maxServiceId.Max() : 0;
			return $"ORDER-{newNumber + 1:000000}";
		}

		private void UpdateContactOnSelectedOrganization(OrganizationsInstance organizationsInstance)
		{
			if (organizationsInstance == null)
			{
				view.Contact.SetOptions(new List<Option<PeopleInstance>>());
				return;
			}

			view.Contact.SetOptions(
				peopleInstances.Where(x => x.Organization.Organization_57695f03 == organizationsInstance.ID.Id).OrderBy(x => x.Name).Select(x => new Option<PeopleInstance>(x.Name, x)));
		}

		private bool ValidateLabel(string newValue)
		{
			if (String.IsNullOrWhiteSpace(newValue))
			{
				view.ErrorName.Text = "Placeholder will be used";
				return true;
			}

			if (getServiceOrderItemLabels.Contains(newValue, StringComparer.InvariantCultureIgnoreCase))
			{
				view.ErrorName.Text = "Name already exists!";
				return false;
			}

			view.ErrorName.Text = String.Empty;
			return true;
		}
	}
}
namespace SLC_SM_Create_Service_Inventory_Item.Presenters
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.PeopleAndOrganization;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;

	using SLC_SM_Create_Service_Inventory_Item.Views;

	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement.Models;

	public class ServicePresenter
	{
		private readonly List<string> getServiceLabels;
		private readonly IEngine _engine;
		private readonly DataHelpersServiceManagement repo;
		private readonly ServiceView view;
		private Models.Service instanceToReturn;
		private bool isEdit = false;

		public ServicePresenter(IEngine engine, DataHelpersServiceManagement repo, ServiceView view)
		{
			_engine = engine;
			this.repo = repo;
			this.view = view;
			List<Models.Service> services = repo.Services.Read();
			getServiceLabels = services.Select(x => x.Name).ToList();
			string defaultServiceId = repo.Services.UniqueServiceId(services);
			instanceToReturn = new Models.Service
			{
				ID = Guid.NewGuid(),
				Name = defaultServiceId,
				ServiceID = defaultServiceId,
				Description = defaultServiceId,
				ServiceItems = new List<Models.ServiceItem>(),
				ServiceItemsRelationships = new List<Models.ServiceItemRelationShip>(),
			};
			view.TboxName.PlaceHolder = instanceToReturn.Name;
			view.ServiceId.Text = instanceToReturn.ServiceID;

			view.IndefiniteRuntime.Changed += (sender, args) => view.End.IsEnabled = !args.IsChecked;
			view.TboxName.Changed += (sender, args) => ValidateLabel(args.Value);
		}

		public string Name => String.IsNullOrWhiteSpace(view.TboxName.Text) ? view.TboxName.PlaceHolder : view.TboxName.Text;

		public Models.Service Instance
		{
			get
			{
				instanceToReturn.Name = Name;
				instanceToReturn.ServiceID = view.ServiceId.Text;
				instanceToReturn.Description = instanceToReturn.Description ?? String.Empty;
				instanceToReturn.StartTime = view.Start.DateTime.ToUniversalTime();
				instanceToReturn.EndTime = view.IndefiniteRuntime.IsChecked ? default(DateTime?) : view.End.DateTime.ToUniversalTime();
				instanceToReturn.GenerateMonitoringService = view.GenerateMonitoringService.IsChecked;
				instanceToReturn.Description = instanceToReturn.Description ?? String.Empty;
				instanceToReturn.Category = view.ServiceCategory.Selected;
				instanceToReturn.ServiceSpecificationId = view.Specs.Selected?.ID;
				instanceToReturn.OrganizationId = view.Organizations.Selected?.ID;
				instanceToReturn.Icon = view.ServiceCategory?.Selected?.Icon ?? String.Empty;
				instanceToReturn.ServiceConfiguration = view.ConfigurationVersions.Selected;

				return instanceToReturn;
			}
		}

		public void LoadFromModel()
		{
			// Load correct types
			var categoryOptions = repo.ServiceCategories.Read().OrderBy(x => x.Name).Select(x => new Option<Models.ServiceCategory>(x.Name, x)).ToList();
			categoryOptions.Insert(0, new Option<Models.ServiceCategory>("-None-", null));
			view.ServiceCategory.SetOptions(categoryOptions);

			var specs = repo.ServiceSpecifications.Read().OrderBy(x => x.Name).Select(x => new Option<Models.ServiceSpecification>(x.Name, x)).ToList();
			specs.Insert(0, new Option<Models.ServiceSpecification>("-None-", null));
			view.Specs.SetOptions(specs);

			var orgs = new DataHelperOrganization(_engine.GetUserConnection()).Read()
				.OrderBy(x => x.Name)
				.Select(x => new Option<Skyline.DataMiner.ProjectApi.ServiceManagement.API.PeopleAndOrganization.Models.Organization>(x.Name, x))
				.ToList();
			orgs.Insert(0, new Option<Skyline.DataMiner.ProjectApi.ServiceManagement.API.PeopleAndOrganization.Models.Organization>("-None-", null));
			view.Organizations.SetOptions(orgs);

			view.Start.DateTime = DateTime.Now + TimeSpan.FromHours(1);
			view.End.DateTime = view.Start.DateTime + TimeSpan.FromHours(1);

			view.Specs.Changed += Specs_Changed;
		}

		public void LoadFromModel(Models.Service instance)
		{
			instanceToReturn = instance;
			getServiceLabels.Remove(instance.Name);
			isEdit = true;

			// Load correct types
			LoadFromModel();

			if (instance.ConfigurationVersions != null && instance.ConfigurationVersions.Count > 0)
			{
				var specs = instance.ConfigurationVersions.OrderBy(x => x.VersionName).Select(x => new Option<Models.ServiceConfigurationVersion>(x.VersionName, x)).ToList();
				specs.Insert(0, new Option<Models.ServiceConfigurationVersion>("-None-", null));
				view.ConfigurationVersions.SetOptions(specs);
			}
			else
			{
				view.ConfigurationVersions.SetOptions(new List<Option<Models.ServiceConfigurationVersion>> { new Option<Models.ServiceConfigurationVersion>("-None-", null) });
			}

			view.BtnAdd.Text = "Save";
			view.TboxName.Text = instance.Name;
			if (!String.IsNullOrEmpty(instance.ServiceID))
			{
				view.TboxName.PlaceHolder = instance.ServiceID;
				view.ServiceId.Text = instance.ServiceID;
			}

			if (instance.StartTime.HasValue)
			{
				view.Start.DateTime = instance.StartTime.Value.ToLocalTime();
			}

			if (instance.EndTime.HasValue)
			{
				view.End.DateTime = instance.EndTime.Value.ToLocalTime();
				view.End.IsEnabled = true;
				view.IndefiniteRuntime.IsChecked = false;
			}
			else
			{
				view.End.DateTime = view.Start.DateTime + TimeSpan.FromDays(7);
				view.End.IsEnabled = false;
				view.IndefiniteRuntime.IsChecked = true;
			}

			if (instance.Category != null && view.ServiceCategory.Options.Any(s => s.Value?.ID == instance.Category.ID))
			{
				view.ServiceCategory.SelectedOption = view.ServiceCategory.Options.First(s => s.Value?.ID == instance.Category.ID);
			}

			if (instance.ServiceSpecificationId.HasValue && view.Specs.Options.Any(x => x.Value?.ID == instance.ServiceSpecificationId))
			{
				view.Specs.SelectedOption = view.Specs.Options.First(x => x.Value?.ID == instance.ServiceSpecificationId);
				view.Specs.IsEnabled = false;
			}

			if (instance.ServiceConfiguration != null && view.ConfigurationVersions.Options.Any(x => x.Value?.ID == instance.ServiceConfiguration.ID))
			{
				view.ConfigurationVersions.SelectedOption = view.ConfigurationVersions.Options.First(x => x.Value?.ID == instance.ServiceConfiguration.ID);
			}

			if (instance.OrganizationId.HasValue && view.Organizations.Options.Any(o => o.Value?.ID == instance.OrganizationId))
			{
				view.Organizations.SelectedOption = view.Organizations.Options.First(x => x.Value?.ID == instance.OrganizationId);
			}

			view.GenerateMonitoringService.IsChecked = instance.GenerateMonitoringService.GetValueOrDefault();
			view.GenerateMonitoringService.IsVisible = false;
		}

		public bool Validate()
		{
			bool ok = true;

			ok &= ValidateLabel(view.TboxName.Text);

			if (!isEdit && view.Start.DateTime < DateTime.Now)
			{
				ok = false;
				view.ErrorStart.Text = "Please make a selection which doesn't lie in the past.";
			}
			else if (!view.IndefiniteRuntime.IsChecked && view.End.DateTime < view.Start.DateTime)
			{
				ok = false;
				view.ErrorStart.Text = "End Time must come after the start time.";
			}
			else
			{
				view.ErrorStart.Text = String.Empty;
			}

			if (isEdit && view.ConfigurationVersions.Selected == null && view.ConfigurationVersions.Options.Count() > 1)
			{
				ok = false;
				view.ErrorConfigurationVersion.Text = "Please select an available configuration version.";
			}
			else
			{
				view.ErrorConfigurationVersion.Text = String.Empty;
			}

			return ok;
		}

		private void Specs_Changed(object sender, DropDown<Models.ServiceSpecification>.DropDownChangedEventArgs e)
		{
			view.GenerateMonitoringService.IsEnabled = e.SelectedOption?.Value != null;
			if (e.SelectedOption?.Value == null)
			{
				view.GenerateMonitoringService.IsChecked = false;
			}
		}

		private bool ValidateLabel(string newValue)
		{
			if (String.IsNullOrWhiteSpace(newValue))
			{
				view.ErrorName.Text = "Placeholder will be used";
				return true;
			}

			if (getServiceLabels.Contains(newValue, StringComparer.InvariantCultureIgnoreCase))
			{
				view.ErrorName.Text = "Name already exists!";
				return false;
			}

			view.ErrorName.Text = String.Empty;
			return true;
		}
	}
}
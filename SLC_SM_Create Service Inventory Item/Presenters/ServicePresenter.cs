namespace SLC_SM_Create_Service_Inventory_Item.Presenters
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using DomHelpers.SlcPeople_Organizations;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Core.DataMinerSystem.Common;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.PeopleAndOrganization;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using Skyline.DataMiner.SDM;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;

	using SLC_SM_Create_Service_Inventory_Item.Views;

	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	public class ServicePresenter
	{
		private const string DefaultDropDownOption = "-None-";

		private readonly List<string> existingServiceNames;
		private readonly List<string> existingServiceIds;
		private readonly IEngine engine;
		private readonly IServiceManagementApiHelper sdmHelper;
		private readonly ServiceView view;
		private readonly IEnumerable<IDmsService> dmsServices;
		private readonly string generatedServiceId;

		private Models.Service instanceToReturn;
		private bool isEdit;

		public ServicePresenter(IEngine engine, IServiceManagementApiHelper sdmHelper, ServiceView view, IEnumerable<IDmsService> serviceList)
		{
			this.engine = engine;
			this.sdmHelper = sdmHelper;
			this.view = view;
			dmsServices = serviceList;

			var services = sdmHelper.ServiceInventory.Services.Read(new TRUEFilterElement<Models.Service>()).ToList();
			existingServiceNames = services.Select(x => x.Name).Where(x => !String.IsNullOrWhiteSpace(x)).ToList();
			existingServiceIds = services.Select(x => x.ServiceID).Where(x => !String.IsNullOrWhiteSpace(x)).ToList();
			generatedServiceId = GenerateUniqueServiceId(existingServiceIds);

			instanceToReturn = new Models.Service
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = generatedServiceId,
				ServiceID = generatedServiceId,
				Description = generatedServiceId,
				MonitoringService = String.Empty,
				ServiceItems = new List<Models.ServiceItem>(),
				ServiceItemsRelationships = new List<Models.ServiceItemRelationship>(),
				ConfigurationVersions = new List<SdmObjectReference<Models.ServiceConfigurationVersion>>(),
			};

			view.TboxName.PlaceHolder = instanceToReturn.Name;
			view.ServiceId.Text = instanceToReturn.ServiceID;

			view.IndefiniteRuntime.Changed += (sender, args) => view.End.IsEnabled = !args.IsChecked;
			view.TboxName.Changed += (sender, args) => ValidateLabel(args.Value);
			view.ServiceId.Changed += (sender, args) => ValidateServiceId(args.Value);
			view.LinkService.Changed += (sender, args) => view.MonitoringServices.IsEnabled = args.IsChecked;
		}

		public string Name => String.IsNullOrWhiteSpace(view.TboxName.Text) ? view.TboxName.PlaceHolder : view.TboxName.Text;

		public string ServiceId => view.ServiceId.Text?.Trim();

		public Models.Service SdmInstance
		{
			get
			{
				instanceToReturn.Name = Name;
				instanceToReturn.ServiceID = ServiceId;
				instanceToReturn.Description = instanceToReturn.Description ?? String.Empty;
				instanceToReturn.StartTime = view.Start.DateTime.ToUniversalTime();
				instanceToReturn.EndTime = view.IndefiniteRuntime.IsChecked ? default(DateTime?) : view.End.DateTime.ToUniversalTime();
				instanceToReturn.GenerateMonitoringService = view.GenerateMonitoringService.IsChecked || view.LinkService.IsChecked;
				instanceToReturn.MonitoringService = view.LinkService.IsChecked ? view.MonitoringServices.Selected?.DmsServiceId.Value : String.Empty;
				instanceToReturn.Icon = view.ServiceCategory?.Selected?.Icon ?? String.Empty;
				instanceToReturn.Identifier = String.IsNullOrWhiteSpace(instanceToReturn.Identifier) ? Guid.NewGuid().ToString() : instanceToReturn.Identifier;

				instanceToReturn.CategoryId = view.ServiceCategory.Selected != null && !String.IsNullOrWhiteSpace(view.ServiceCategory.Selected.Identifier)
					? new SdmObjectReference<Models.ServiceCategory>(view.ServiceCategory.Selected.Identifier)
					: null;

				instanceToReturn.ServiceSpecificationId = view.Specs.Selected != null && !String.IsNullOrWhiteSpace(view.Specs.Selected.Identifier)
					? new SdmObjectReference<Models.ServiceSpecification>(view.Specs.Selected.Identifier)
					: null;

				var selectedConfigurationVersion = view.ConfigurationVersions.Selected;
				instanceToReturn.ServiceConfigurationId = selectedConfigurationVersion != null && !String.IsNullOrWhiteSpace(selectedConfigurationVersion.Identifier)
					? new SdmObjectReference<Models.ServiceConfigurationVersion>(selectedConfigurationVersion.Identifier)
					: null;

				if (selectedConfigurationVersion != null)
				{
					EnsureConfigurationVersionReference(selectedConfigurationVersion.Identifier);
				}

				return CloneService(instanceToReturn);
			}
		}

		public void LoadFromModel()
		{
			var categoryOptions = sdmHelper.ServiceCatalog.ServiceCategories
				.Read(new TRUEFilterElement<Models.ServiceCategory>())
				.Where(x => !String.IsNullOrEmpty(x?.Name))
				.OrderBy(x => x.Name)
				.Select(x => new Option<Models.ServiceCategory>(x.Name, x))
				.ToList();
			categoryOptions.Insert(0, new Option<Models.ServiceCategory>(DefaultDropDownOption, null));
			view.ServiceCategory.SetOptions(categoryOptions);

			var specs = sdmHelper.ServiceCatalog.ServiceSpecifications
				.Read(new TRUEFilterElement<Models.ServiceSpecification>())
				.Where(x => !String.IsNullOrEmpty(x?.Name))
				.OrderBy(x => x.Name)
				.Select(x => new Option<Models.ServiceSpecification>(x.Name, x))
				.ToList();
			specs.Insert(0, new Option<Models.ServiceSpecification>(DefaultDropDownOption, null));
			view.Specs.SetOptions(specs);

			var services = dmsServices.Where(x => !String.IsNullOrEmpty(x?.Name)).OrderBy(x => x.Name).Select(x => new Option<IDmsService>(x.Name, x)).ToList();
			view.MonitoringServices.SetOptions(services);

			var orgs = new List<Option<Skyline.DataMiner.ProjectApi.ServiceManagement.API.PeopleAndOrganization.Models.Organization>>();
			if (engine.DomModelExists(SlcPeople_OrganizationsIds.ModuleId, new[] { SlcPeople_OrganizationsIds.Sections.OrganizationInformation.Id.Id }))
			{
				orgs = new DataHelperOrganization(engine.GetUserConnection()).ReadBasicDetails()
					.Where(x => !String.IsNullOrEmpty(x?.Name))
					.OrderBy(x => x.Name)
					.Select(x => new Option<Skyline.DataMiner.ProjectApi.ServiceManagement.API.PeopleAndOrganization.Models.Organization>(x.Name, x))
					.ToList();
			}

			orgs.Insert(0, new Option<Skyline.DataMiner.ProjectApi.ServiceManagement.API.PeopleAndOrganization.Models.Organization>(DefaultDropDownOption, null));
			view.Organizations.SetOptions(orgs);

			view.Start.DateTime = DateTime.Now + TimeSpan.FromHours(1);
			view.End.DateTime = view.Start.DateTime + TimeSpan.FromHours(1);

			view.Specs.Changed += Specs_Changed;
			view.MonitoringServices.Changed += ServiceLink_Changed;
		}

		public void LoadFromModel(Models.Service source, bool isDuplication = false)
		{
			if (source == null)
			{
				throw new ArgumentNullException(nameof(source));
			}

			isEdit = !isDuplication;
			instanceToReturn = isDuplication ? CreateDuplicatedInstance(source) : CloneService(source);

			if (!isDuplication)
			{
				existingServiceNames.Remove(source.Name);
			}

			LoadFromModel();

			var configurationVersions = ResolveConfigurationVersions(instanceToReturn.ConfigurationVersions);
			var selectedConfigurationVersion = configurationVersions.FirstOrDefault(v =>
				instanceToReturn.ServiceConfigurationId != null && String.Equals(v.Identifier, instanceToReturn.ServiceConfigurationId.Identifier, StringComparison.InvariantCultureIgnoreCase));

			LoadConfigurationVersions(configurationVersions, selectedConfigurationVersion);
			LoadGeneralFields(instanceToReturn, isDuplication ? "Duplicate" : "Save");
			LoadTimeFields(instanceToReturn);
			LoadSelections(instanceToReturn, !isDuplication);
			LoadMonitoringSelection(instanceToReturn, isDuplication);

			view.GenerateMonitoringService.IsChecked = !isDuplication && instanceToReturn.GenerateMonitoringService.GetValueOrDefault();
			view.GenerateMonitoringService.IsVisible = !isEdit;
		}

		public bool Validate()
		{
			bool ok = true;
			ok &= ValidateServiceId(ServiceId);
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

		public void ShowServiceIdExistsError()
		{
			view.ServiceId.ValidationText = $"Service ID '{ServiceId}' already exists.";
			view.ServiceId.ValidationState = UIValidationState.Invalid;
			UpdateAddButtonState();
		}

		private static string GenerateUniqueServiceId(IEnumerable<string> existingServiceIds)
		{
			var maxValue = existingServiceIds
				.Where(x => !String.IsNullOrWhiteSpace(x))
				.Select(x =>
				{
					var parts = x.Split('-');
					return parts.Length > 1 && Int32.TryParse(parts.Last(), out var number) ? number : 0;
				})
				.DefaultIfEmpty(0)
				.Max();

			return $"SERVICE-{maxValue + 1:00000}";
		}

		private static Models.Service CloneService(Models.Service source)
		{
			return new Models.Service
			{
				Identifier = source.Identifier,
				Name = source.Name,
				ServiceID = source.ServiceID,
				Description = source.Description,
				StartTime = source.StartTime,
				EndTime = source.EndTime,
				GenerateMonitoringService = source.GenerateMonitoringService,
				MonitoringService = source.MonitoringService,
				Icon = source.Icon,
				CategoryId = source.CategoryId != null ? new SdmObjectReference<Models.ServiceCategory>(source.CategoryId.Identifier) : null,
				ServiceSpecificationId = source.ServiceSpecificationId != null ? new SdmObjectReference<Models.ServiceSpecification>(source.ServiceSpecificationId.Identifier) : null,
				ServiceConfigurationId = source.ServiceConfigurationId != null ? new SdmObjectReference<Models.ServiceConfigurationVersion>(source.ServiceConfigurationId.Identifier) : null,
				ConfigurationVersions = source.ConfigurationVersions?.Select(v => new SdmObjectReference<Models.ServiceConfigurationVersion>(v.Identifier)).ToList()
					?? new List<SdmObjectReference<Models.ServiceConfigurationVersion>>(),
				ServiceItems = source.ServiceItems?.Select(item => new Models.ServiceItem
				{
					ServiceItemID = item.ServiceItemID,
					Label = item.Label,
					Script = item.Script,
					DefinitionReference = item.DefinitionReference,
					ImplementationReference = item.ImplementationReference,
					Type = item.Type,
					Icon = item.Icon,
				}).ToList() ?? new List<Models.ServiceItem>(),
				ServiceItemsRelationships = source.ServiceItemsRelationships?.Select(rel => new Models.ServiceItemRelationship
				{
					Id = rel.Id,
					ParentServiceItem = rel.ParentServiceItem,
					ChildServiceItem = rel.ChildServiceItem,
				}).ToList() ?? new List<Models.ServiceItemRelationship>(),
			};
		}

		private Models.Service CreateDuplicatedInstance(Models.Service source)
		{
			var duplicate = CloneService(source);
			duplicate.Identifier = Guid.NewGuid().ToString();
			duplicate.ServiceID = generatedServiceId;
			duplicate.Name = (source.Name ?? generatedServiceId) + " (Copy)";
			duplicate.GenerateMonitoringService = false;
			duplicate.MonitoringService = String.Empty;
			return duplicate;
		}

		private void EnsureConfigurationVersionReference(string identifier)
		{
			if (String.IsNullOrWhiteSpace(identifier))
			{
				return;
			}

			instanceToReturn.ConfigurationVersions = instanceToReturn.ConfigurationVersions ?? new List<SdmObjectReference<Models.ServiceConfigurationVersion>>();
			if (!instanceToReturn.ConfigurationVersions.Any(cv => String.Equals(cv.Identifier, identifier, StringComparison.InvariantCultureIgnoreCase)))
			{
				instanceToReturn.ConfigurationVersions.Add(new SdmObjectReference<Models.ServiceConfigurationVersion>(identifier));
			}
		}

		private List<Models.ServiceConfigurationVersion> ResolveConfigurationVersions(List<SdmObjectReference<Models.ServiceConfigurationVersion>> refs)
		{
			if (refs == null || refs.Count == 0)
			{
				return new List<Models.ServiceConfigurationVersion>();
			}

			var ids = refs.Select(r => r.Identifier).Where(id => !String.IsNullOrWhiteSpace(id)).ToHashSet(StringComparer.InvariantCultureIgnoreCase);
			if (ids.Count == 0)
			{
				return new List<Models.ServiceConfigurationVersion>();
			}

			return sdmHelper.ServiceInventory.ServiceConfigurationVersions
				.Read(new TRUEFilterElement<Models.ServiceConfigurationVersion>())
				.Where(v => v != null && !String.IsNullOrWhiteSpace(v.Identifier) && ids.Contains(v.Identifier))
				.OrderBy(v => v.VersionName)
				.ToList();
		}

		private void LoadConfigurationVersions(List<Models.ServiceConfigurationVersion> versions, Models.ServiceConfigurationVersion selectedVersion)
		{
			if (versions == null || versions.Count == 0)
			{
				view.ConfigurationVersions.SetOptions(new List<Option<Models.ServiceConfigurationVersion>>
				{
					new Option<Models.ServiceConfigurationVersion>(DefaultDropDownOption, null),
				});
				return;
			}

			var options = versions.OrderBy(x => x.VersionName)
				.Select(x => new Option<Models.ServiceConfigurationVersion>(x.VersionName, x))
				.ToList();
			options.Insert(0, new Option<Models.ServiceConfigurationVersion>(DefaultDropDownOption, null));
			view.ConfigurationVersions.SetOptions(options);

			if (selectedVersion != null)
			{
				var selected = view.ConfigurationVersions.Options.FirstOrDefault(x => String.Equals(x.Value?.Identifier, selectedVersion.Identifier, StringComparison.InvariantCultureIgnoreCase));
				if (selected != null)
				{
					view.ConfigurationVersions.SelectedOption = selected;
				}
			}
		}

		private void LoadGeneralFields(Models.Service instance, string actionText)
		{
			view.BtnAdd.Text = actionText;
			view.TboxName.Text = instance.Name;
			if (!String.IsNullOrEmpty(instance.ServiceID))
			{
				view.TboxName.PlaceHolder = instance.ServiceID;
				view.ServiceId.Text = instance.ServiceID;
			}
		}

		private void LoadTimeFields(Models.Service instance)
		{
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
		}

		private void LoadSelections(Models.Service instance, bool lockSpecification)
		{
			if (instance.CategoryId != null)
			{
				var cat = view.ServiceCategory.Options.FirstOrDefault(s => String.Equals(s.Value?.Identifier, instance.CategoryId.Identifier, StringComparison.InvariantCultureIgnoreCase));
				if (cat != null)
				{
					view.ServiceCategory.SelectedOption = cat;
				}
			}

			if (instance.ServiceSpecificationId != null)
			{
				var spec = view.Specs.Options.FirstOrDefault(x => String.Equals(x.Value?.Identifier, instance.ServiceSpecificationId.Identifier, StringComparison.InvariantCultureIgnoreCase));
				if (spec != null)
				{
					view.Specs.SelectedOption = spec;
					view.Specs.IsEnabled = !lockSpecification;
				}
			}
		}

		private void LoadMonitoringSelection(Models.Service source, bool isDuplicate)
		{
			bool hasLinkedService = !isDuplicate && !String.IsNullOrEmpty(source.MonitoringService)
				&& view.MonitoringServices.Options.Any(option => option.Value?.DmsServiceId.Value == source.MonitoringService);

			view.LinkService.IsChecked = hasLinkedService;
			view.MonitoringServices.IsEnabled = hasLinkedService;
			if (hasLinkedService)
			{
				view.MonitoringServices.Selected = view.MonitoringServices.Options.First(option => option.Value?.DmsServiceId.Value == source.MonitoringService).Value;
			}
		}

		private void Specs_Changed(object sender, DropDown<Models.ServiceSpecification>.DropDownChangedEventArgs e)
		{
			view.GenerateMonitoringService.IsEnabled = e.SelectedOption?.Value != null;
			if (e.SelectedOption?.Value == null)
			{
				view.GenerateMonitoringService.IsChecked = false;
			}
		}

		private void ServiceLink_Changed(object sender, DropDown<IDmsService>.DropDownChangedEventArgs e)
		{
			if (e.SelectedOption?.Value == null)
			{
				view.LinkService.IsChecked = false;
			}
		}

		private void UpdateAddButtonState()
		{
			view.BtnAdd.IsEnabled = view.ServiceId.ValidationState != UIValidationState.Invalid && view.TboxName.ValidationState != UIValidationState.Invalid;
		}

		private bool ValidateServiceId(string value)
		{
			bool isValid = true;
			if (String.IsNullOrWhiteSpace(value))
			{
				view.ServiceId.ValidationText = "Service ID is required.";
				isValid = false;
			}
			else if (!isEdit && existingServiceIds.Contains(value.Trim(), StringComparer.InvariantCultureIgnoreCase))
			{
				view.ServiceId.ValidationText = "Service ID already exists!";
				isValid = false;
			}
			else
			{
				view.ServiceId.ValidationText = String.Empty;
			}

			view.ServiceId.ValidationState = isValid ? UIValidationState.Valid : UIValidationState.Invalid;
			UpdateAddButtonState();
			return isValid;
		}

		private bool ValidateLabel(string newValue)
		{
			bool isValid = true;
			if (String.IsNullOrWhiteSpace(newValue))
			{
				view.ErrorName.Text = "Placeholder will be used";
			}
			else if (existingServiceNames.Contains(newValue, StringComparer.InvariantCultureIgnoreCase))
			{
				view.ErrorName.Text = String.Empty;
				view.TboxName.ValidationText = "Name already exists!";
				isValid = false;
			}
			else
			{
				view.ErrorName.Text = String.Empty;
				view.TboxName.ValidationText = String.Empty;
			}

			view.TboxName.ValidationState = isValid ? UIValidationState.Valid : UIValidationState.Invalid;
			UpdateAddButtonState();
			return isValid;
		}
	}
}

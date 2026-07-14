namespace SLC_SM_Create_Service_Inventory_Item.Presenters
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using DomHelpers.SlcPeople_Organizations;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Core.DataMinerSystem.Common;
	using Skyline.DataMiner.Net.Helper;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.PeopleAndOrganization;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;

	using SLC_SM_Create_Service_Inventory_Item.Views;

	using ConfigModels = Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models;
	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement.Models;

	public class ServicePresenter
	{
		private const string DefaultDropDownOption = "-None-";

		private readonly List<string> getServiceLabels;
		private readonly IEngine _engine;
		private readonly DataHelpersServiceManagement repo;
		private readonly ServiceView view;
		private readonly IEnumerable<IDmsService> serviceList;

		private readonly string serviceId;

		private Models.Service instanceToReturn;
		private bool isEdit = false;

		public ServicePresenter(IEngine engine, DataHelpersServiceManagement repo, ServiceView view, IEnumerable<IDmsService> serviceList)
		{
			_engine = engine;
			this.repo = repo;
			this.view = view;
			this.serviceList = serviceList;

			List<Models.Service> services = repo.Services.ReadBasicDetails();

			getServiceLabels = services.Select(x => x.Name).ToList();
			serviceId = repo.Services.UniqueServiceId(services);

			instanceToReturn = new Models.Service
			{
				ID = Guid.NewGuid(),
				Name = serviceId,
				ServiceID = serviceId,
				Description = serviceId,
				MonitoringService = string.Empty,
				ServiceItems = new List<Models.ServiceItem>(),
				ServiceItemsRelationships = new List<Models.ServiceItemRelationShip>(),
			};

			view.TboxName.PlaceHolder = instanceToReturn.Name;
			view.ServiceId.Text = instanceToReturn.ServiceID;

			view.IndefiniteRuntime.Changed += (sender, args) => view.End.IsEnabled = !args.IsChecked;
			view.TboxName.Changed += (sender, args) => ValidateLabel(args.Value);
			view.RemoveLinkedService.Changed += (sender, args) => view.MonitoringServices.IsEnabled = !args.IsChecked;
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
				instanceToReturn.GenerateMonitoringService = view.GenerateMonitoringService.IsChecked || !view.RemoveLinkedService.IsChecked;
				instanceToReturn.Description = instanceToReturn.Description ?? String.Empty;
				instanceToReturn.Category = view.ServiceCategory.Selected;
				instanceToReturn.ServiceSpecificationId = view.Specs.Selected?.ID;
				instanceToReturn.OrganizationId = view.Organizations.Selected?.ID;
				instanceToReturn.MonitoringService = view.RemoveLinkedService.IsChecked ? string.Empty : view.MonitoringServices.Selected?.DmsServiceId.Value;
				instanceToReturn.Icon = view.ServiceCategory?.Selected?.Icon ?? String.Empty;
				instanceToReturn.ServiceConfiguration = view.ConfigurationVersions.Selected;
				return instanceToReturn;
			}
		}

		public void LoadFromModel()
		{
			var categoryOptions = repo.ServiceCategories.Read().Where(x => !string.IsNullOrEmpty(x?.Name)).OrderBy(x => x.Name).Select(x => new Option<Models.ServiceCategory>(x.Name, x)).ToList();
			categoryOptions.Insert(0, new Option<Models.ServiceCategory>(DefaultDropDownOption, null));
			view.ServiceCategory.SetOptions(categoryOptions);

			var specs = repo.ServiceSpecifications.Read().Where(x => !string.IsNullOrEmpty(x?.Name)).OrderBy(x => x.Name).Select(x => new Option<Models.ServiceSpecification>(x.Name, x)).ToList();
			specs.Insert(0, new Option<Models.ServiceSpecification>(DefaultDropDownOption, null));
			view.Specs.SetOptions(specs);

			var services = serviceList.Where(x => !string.IsNullOrEmpty(x?.Name)).OrderBy(x => x.Name).Select(x => new Option<IDmsService>(x.Name, x)).ToList();
			view.MonitoringServices.SetOptions(services);

			var orgs = new List<Option<Skyline.DataMiner.ProjectApi.ServiceManagement.API.PeopleAndOrganization.Models.Organization>>();
			if (this._engine.DomModelExists(SlcPeople_OrganizationsIds.ModuleId, new[] { SlcPeople_OrganizationsIds.Sections.OrganizationInformation.Id.Id }))
			{
				orgs = new DataHelperOrganization(_engine.GetUserConnection()).Read()
				.Where(x => !string.IsNullOrEmpty(x?.Name))
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
			isEdit = !isDuplication;
			instanceToReturn = isDuplication ? CreateDuplicatedInstance(source) : source;

			if (!isDuplication)
			{
				getServiceLabels.Remove(source.Name);
			}

			LoadFromModel();

			LoadConfigurationVersions(instanceToReturn.ConfigurationVersions, instanceToReturn.ServiceConfiguration);
			LoadGeneralFields(instanceToReturn, isDuplication ? "Duplicate" : "Save");
			LoadTimeFields(instanceToReturn);
			LoadSelections(instanceToReturn, !isDuplication);
			LoadMonitoringSelection(source, isDuplication);

			view.GenerateMonitoringService.IsChecked = !isDuplication && source.GenerateMonitoringService.GetValueOrDefault();
			view.GenerateMonitoringService.IsVisible = !isEdit;
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

		private static Models.ServiceConfigurationVersion DuplicateConfigurationVersion(Models.ServiceConfigurationVersion source, string newServiceId)
		{
			if (source == null)
			{
				return new Models.ServiceConfigurationVersion
				{
					ID = Guid.NewGuid(),
					CreatedAt = DateTime.UtcNow,
					VersionName = "Default (Copy)",
					Description = "Default",
					Parameters = new List<Models.ServiceConfigurationValue>(),
					Profiles = new List<Models.ServiceProfile>(),
				};
			}

			var parameterIdMap = new Dictionary<Guid, Guid>();

			var duplicateService = new Models.ServiceConfigurationVersion
			{
				ID = Guid.NewGuid(),
				VersionName = source.VersionName + " (Copy)",
				Description = source.Description,
				StartDate = source.StartDate,
				EndDate = source.EndDate,
				CreatedAt = DateTime.UtcNow,
				Parameters = DuplicateConfigurationParameters(source.Parameters, parameterIdMap),
				Profiles = DuplicateServiceProfiles(source.Profiles, newServiceId, parameterIdMap),
			};

			RemapLinkedConsumers(duplicateService, parameterIdMap);

			return duplicateService;
		}

		private static void RemapLinkedConsumers(Models.ServiceConfigurationVersion version, Dictionary<Guid, Guid> parameterIdMap)
		{
			if (parameterIdMap.Count == 0)
			{
				return;
			}

			foreach (var serviceConfigValue in version.Parameters)
			{
				RemapLinkedConsumers(serviceConfigValue?.ConfigurationParameter, parameterIdMap);
			}

			foreach (var serviceProfile in version.Profiles)
			{
				if (serviceProfile?.Profile?.ConfigurationParameterValues == null)
				{
					continue;
				}

				foreach (var paramValue in serviceProfile.Profile.ConfigurationParameterValues)
				{
					RemapLinkedConsumers(paramValue, parameterIdMap);
				}
			}
		}

		private static void RemapLinkedConsumers(ConfigModels.ConfigurationParameterValue paramValue, Dictionary<Guid, Guid> parameterIdMap)
		{
			if (paramValue?.LinkedConsumers == null || paramValue.LinkedConsumers.Count == 0)
			{
				return;
			}

			for (int i = 0; i < paramValue.LinkedConsumers.Count; i++)
			{
				Guid newId;
				if (parameterIdMap.TryGetValue(paramValue.LinkedConsumers[i], out newId))
				{
					paramValue.LinkedConsumers[i] = newId;
				}
			}
		}

		private static List<Models.ServiceConfigurationValue> DuplicateConfigurationParameters(List<Models.ServiceConfigurationValue> sourceParameters, Dictionary<Guid, Guid> parameterIdMap)
		{
			var duplicateParameters = new List<Models.ServiceConfigurationValue>();

			if (sourceParameters == null)
			{
				return duplicateParameters;
			}

			foreach (var parameter in sourceParameters)
			{
				if (parameter?.ConfigurationParameter == null)
				{
					continue;
				}

				duplicateParameters.Add(new Models.ServiceConfigurationValue
				{
					ID = Guid.NewGuid(),
					Mandatory = parameter.Mandatory,
					ConfigurationParameter = DuplicateConfigurationParameterValue(parameter.ConfigurationParameter, parameterIdMap),
				});
			}

			return duplicateParameters;
		}

		private static List<Models.ServiceProfile> DuplicateServiceProfiles(List<Models.ServiceProfile> sourceProfiles, string newServiceId, Dictionary<Guid, Guid> parameterIdMap)
		{
			var duplicatedProfiles = new List<Models.ServiceProfile>();

			if (sourceProfiles == null || sourceProfiles.Count == 0)
			{
				return duplicatedProfiles;
			}

			var profilesMapping = new Dictionary<Guid, Models.ServiceProfile>();
			foreach (var profile in sourceProfiles)
			{
				if (profile?.Profile == null)
				{
					continue;
				}

				var duplicatedServiceProfile = new Models.ServiceProfile
				{
					ID = Guid.NewGuid(),
					Mandatory = profile.Mandatory,
					ProfileDefinition = profile.ProfileDefinition,
					Profile = DuplicateProfile(profile.Profile, newServiceId, parameterIdMap),
				};

				profilesMapping[profile.Profile.ID] = duplicatedServiceProfile;
				duplicatedProfiles.Add(duplicatedServiceProfile);
			}

			foreach (var duplicatedServiceProfile in duplicatedProfiles)
			{
				if (duplicatedServiceProfile.Profile.Profiles == null || duplicatedServiceProfile.Profile.Profiles.Count == 0)
				{
					continue;
				}

				var remappedChildren = new List<Guid>();
				foreach (var nestedProfileId in duplicatedServiceProfile.Profile.Profiles)
				{
					Models.ServiceProfile duplicateNestedProfile;
					if (profilesMapping.TryGetValue(nestedProfileId, out duplicateNestedProfile))
					{
						remappedChildren.Add(duplicateNestedProfile.Profile.ID);
					}
				}

				duplicatedServiceProfile.Profile.Profiles = remappedChildren;
			}

			return duplicatedProfiles;
		}

		private static ConfigModels.Profile DuplicateProfile(ConfigModels.Profile source, string newServiceId, Dictionary<Guid, Guid> parameterIdMap)
		{
			var duplicatedParamValues = new List<ConfigModels.ConfigurationParameterValue>();
			if (source.ConfigurationParameterValues != null)
			{
				foreach (var cpv in source.ConfigurationParameterValues)
				{
					duplicatedParamValues.Add(DuplicateConfigurationParameterValue(cpv, parameterIdMap));
				}
			}

			return new ConfigModels.Profile
			{
				ID = Guid.NewGuid(),
				Name = source.Name.ReplaceTrailingParentesisContent(newServiceId),
				IsReusable = false,
				ProfileDefinitionReference = source.ProfileDefinitionReference,
				Profiles = source.Profiles != null ? new List<Guid>(source.Profiles) : new List<Guid>(),
				TestedProtocols = source.TestedProtocols != null
					? new List<ConfigModels.ProtocolTest>(source.TestedProtocols)
					: new List<ConfigModels.ProtocolTest>(),
				ConfigurationParameterValues = duplicatedParamValues,
			};
		}

		private static ConfigModels.ConfigurationParameterValue DuplicateConfigurationParameterValue(ConfigModels.ConfigurationParameterValue source, Dictionary<Guid, Guid> parameterIdMap)
		{
			var newId = Guid.NewGuid();
			parameterIdMap[source.ID] = newId;

			var duplicateCpv = new ConfigModels.ConfigurationParameterValue
			{
				ID = newId,
				Label = source.Label,
				Type = source.Type,
				ConfigurationParameterId = source.ConfigurationParameterId,
				StringValue = source.StringValue,
				DoubleValue = source.DoubleValue,
				ValueFixed = source.ValueFixed,
				IsLinked = source.IsLinked,
				LinkedScript = source.LinkedScript,
				LinkedConsumers = source.LinkedConsumers != null ? new List<Guid>(source.LinkedConsumers) : null,
			};

			if (source.NumberOptions != null)
			{
				var units = source.NumberOptions.Units?
					.Select(u => new ConfigModels.ConfigurationUnit { ID = u.ID, Name = u.Name })
					.ToList()
					?? new List<ConfigModels.ConfigurationUnit>();

				ConfigModels.ConfigurationUnit defaultUnit = null;
				if (source.NumberOptions.DefaultUnit != null)
				{
					defaultUnit = units.FirstOrDefault(u => u.ID == source.NumberOptions.DefaultUnit.ID);
					if (defaultUnit == null)
					{
						defaultUnit = new ConfigModels.ConfigurationUnit
						{
							ID = source.NumberOptions.DefaultUnit.ID,
							Name = source.NumberOptions.DefaultUnit.Name,
						};
						units.Add(defaultUnit);
					}
				}

				duplicateCpv.NumberOptions = new ConfigModels.NumberParameterOptions
				{
					ID = Guid.NewGuid(),
					MinRange = source.NumberOptions.MinRange,
					MaxRange = source.NumberOptions.MaxRange,
					StepSize = source.NumberOptions.StepSize,
					Decimals = source.NumberOptions.Decimals,
					DefaultValue = source.NumberOptions.DefaultValue,
					DefaultUnit = defaultUnit,
					Units = units,
				};
			}

			if (source.DiscreteOptions != null)
			{
				duplicateCpv.DiscreteOptions = new ConfigModels.DiscreteParameterOptions
				{
					ID = Guid.NewGuid(),
					Default = source.DiscreteOptions.Default,
					DiscreteValues = source.DiscreteOptions.DiscreteValues?
						.Select(dv => new ConfigModels.DiscreteValue { Value = dv.Value })
						.ToList()
						?? new List<ConfigModels.DiscreteValue>(),
				};
			}

			if (source.TextOptions != null)
			{
				duplicateCpv.TextOptions = new ConfigModels.TextParameterOptions
				{
					ID = Guid.NewGuid(),
					Default = source.TextOptions.Default,
					Regex = source.TextOptions.Regex,
					UserMessage = source.TextOptions.UserMessage,
				};
			}

			return duplicateCpv;
		}

		private Models.Service CreateDuplicatedInstance(Models.Service source)
		{
			var duplicatedVersions = source.ConfigurationVersions?
				.Select(v => DuplicateConfigurationVersion(v, serviceId))
				.ToList() ?? new List<Models.ServiceConfigurationVersion>();

			var activeVersion = source.ServiceConfiguration == null
				? duplicatedVersions.FirstOrDefault()
				: duplicatedVersions.FirstOrDefault(v => v.VersionName == source.ServiceConfiguration.VersionName + " (Copy)") ?? duplicatedVersions.FirstOrDefault();

			return new Models.Service
			{
				ID = Guid.NewGuid(),
				ServiceID = serviceId,
				Name = source.Name + " (Copy)",
				Description = source.Description,
				StartTime = source.StartTime,
				EndTime = source.EndTime,
				ServiceSpecificationId = source.ServiceSpecificationId,
				Category = source.Category,
				Icon = source.Icon,
				OrganizationId = source.OrganizationId,
				GenerateMonitoringService = false,
				MonitoringService = String.Empty,
				ConfigurationVersions = duplicatedVersions,
				ServiceConfiguration = activeVersion,
				ServiceItems = source.ServiceItems?
					.Select(item => new Models.ServiceItem
					{
						ID = item.ID,
						Label = item.Label,
						Script = item.Script,
						DefinitionReference = item.DefinitionReference,
						Icon = item.Icon,
					})
					.ToList() ?? new List<Models.ServiceItem>(),
				ServiceItemsRelationships = source.ServiceItemsRelationships != null
					? new List<Models.ServiceItemRelationShip>(source.ServiceItemsRelationships)
					: new List<Models.ServiceItemRelationShip>(),
			};
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

			var options = versions
				.OrderBy(x => x.VersionName)
				.Select(x => new Option<Models.ServiceConfigurationVersion>(x.VersionName, x))
				.ToList();

			options.Insert(0, new Option<Models.ServiceConfigurationVersion>(DefaultDropDownOption, null));
			view.ConfigurationVersions.SetOptions(options);

			if (selectedVersion != null && view.ConfigurationVersions.Options.Any(x => x.Value?.ID == selectedVersion.ID))
			{
				view.ConfigurationVersions.SelectedOption = view.ConfigurationVersions.Options.First(x => x.Value?.ID == selectedVersion.ID);
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
			if (instance.Category != null && view.ServiceCategory.Options.Any(s => s.Value?.ID == instance.Category.ID))
			{
				view.ServiceCategory.SelectedOption = view.ServiceCategory.Options.First(s => s.Value?.ID == instance.Category.ID);
			}

			if (instance.ServiceSpecificationId.HasValue && view.Specs.Options.Any(x => x.Value?.ID == instance.ServiceSpecificationId))
			{
				view.Specs.SelectedOption = view.Specs.Options.First(x => x.Value?.ID == instance.ServiceSpecificationId);
				view.Specs.IsEnabled = !lockSpecification;
			}

			if (instance.OrganizationId.HasValue && view.Organizations.Options.Any(o => o.Value?.ID == instance.OrganizationId))
			{
				view.Organizations.SelectedOption = view.Organizations.Options.First(x => x.Value?.ID == instance.OrganizationId);
			}
		}

		private void LoadMonitoringSelection(Models.Service source, bool isDuplicate)
		{
			if (!isDuplicate &&
				!source.MonitoringService.IsNullOrEmpty() &&
				view.MonitoringServices.Options.Any(s => s.Value?.DmsServiceId.Value == source.MonitoringService))
			{
				view.RemoveLinkedService.IsChecked = false;
				view.MonitoringServices.Selected = view.MonitoringServices.Options
					.First(x => x.Value?.DmsServiceId.Value == source.MonitoringService).Value;
				view.MonitoringServices.IsEnabled = true;
			}
			else
			{
				view.RemoveLinkedService.IsChecked = true;
				view.MonitoringServices.IsEnabled = false;
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
				view.RemoveLinkedService.IsChecked = true;
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
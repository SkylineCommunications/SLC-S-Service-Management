namespace SLC_SM_IAS_Add_Service_Order_Item_1.Presenters
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Library;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;
	using SLC_SM_IAS_Add_Service_Order_Item_1.Views;
	using ConfigurationModels = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;
	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	public class ServiceOrderItemPresenter
	{
		private readonly string[] getServiceOrderItemLabels;
		private readonly IServiceManagementApiHelper repo;
		private readonly ServiceOrderItemView view;
		private Models.ServiceOrderEntry orderEntry;
		private Models.ServiceOrderItem instanceToReturn;
		private bool isEdit;

		public ServiceOrderItemPresenter(ServiceOrderItemView view, IServiceManagementApiHelper repo, string[] getServiceOrderItemLabels)
		{
			this.view = view;
			this.repo = repo;
			this.getServiceOrderItemLabels = getServiceOrderItemLabels;
			instanceToReturn = new Models.ServiceOrderItem
			{
				Identifier = Guid.NewGuid().ToString(),
				ServiceInfo = new Models.ServiceOrderItemServiceInfo
				{
					Configurations = new List<Skyline.DataMiner.SDM.SdmObjectReference<Models.ServiceOrderItemConfigurationValue>>(),
				},
			};
			orderEntry = new Models.ServiceOrderEntry { ServiceOrderItemId = instanceToReturn };

			view.IndefiniteTime.Changed += (sender, args) => view.End.IsEnabled = !args.IsChecked;
			view.TboxName.Changed += (sender, args) => ValidateLabel(args.Value);
			view.ActionType.Changed += (sender, args) =>
			{
				UpdateUiOnActionTypeChange(args.SelectedOption);
				Validate();
			};
			view.Specification.Changed += (sender, args) =>
			{
				UpdateUiOnActionTypeChange(view.ActionType.SelectedOption);
				Validate();
			};
			view.Service.Changed += (sender, args) =>
			{
				UpdateUiOnActionTypeChange(view.ActionType.SelectedOption);
				Validate();
			};
		}

		public Models.ServiceOrderItem GetData
		{
			get
			{
				instanceToReturn.Name = Name;
				instanceToReturn.Description = view.TboxDescription.Text ?? String.Empty;
				instanceToReturn.Action = view.ActionType.Selected.ToString();
				instanceToReturn.StartTime = view.Start.IsVisible ? view.Start.DateTime : default(DateTime?);
				instanceToReturn.EndTime = !view.Start.IsVisible || view.IndefiniteTime.IsChecked ? default(DateTime?) : view.End.DateTime;
				instanceToReturn.IndefiniteRuntime = view.IndefiniteTime.IsChecked;
				var configurations = instanceToReturn.ServiceInfo?.Configurations ?? new List<Skyline.DataMiner.SDM.SdmObjectReference<Models.ServiceOrderItemConfigurationValue>>();
				instanceToReturn.ServiceInfo = new Models.ServiceOrderItemServiceInfo
				{
					ServiceCategoryId = view.Category.Selected,
					SpecificationId = view.Specification.Selected,
					ServiceId = view.Service.Selected,
					Configurations = configurations,
				};
				if (!isEdit && view.Specification.Selected != null)
				{
					var specificationConfigurationIds = view.Specification.Selected.ConfigurationParameters
						.Select(reference => reference.Identifier)
						.ToHashSet();

					var specificationConfigurations = repo.ServiceCatalog.ServiceSpecificationConfigurationValues
						.Read(new TRUEFilterElement<Models.ServiceSpecificationConfigurationValue>())
						.Where(configuration => specificationConfigurationIds.Contains(configuration.Identifier))
						.ToList();

					var parameterValuesByParameterId = repo.ServiceCatalog.ConfigurationParameterValues
						.Read(new TRUEFilterElement<ConfigurationModels.ConfigurationParameterValue>())
						.Where(value => value.ConfigurationParameterId != null && !String.IsNullOrEmpty(value.ConfigurationParameterId.Identifier))
						.GroupBy(value => value.ConfigurationParameterId.Identifier)
						.ToDictionary(group => group.Key, group => group.First());

					var orderItemConfigurations = new List<Models.ServiceOrderItemConfigurationValue>();

					foreach (var specificationConfiguration in specificationConfigurations)
					{
						if (specificationConfiguration?.ConfigurationParameterId == null)
						{
							continue;
						}

						var sourceValueId = specificationConfiguration.ConfigurationParameterId.Identifier;
						if (String.IsNullOrEmpty(sourceValueId) ||
							!parameterValuesByParameterId.TryGetValue(sourceValueId, out var sourceValue))
						{
							continue;
						}

						var orderValue = CloneConfigurationParameterValue(sourceValue);

						repo.ServiceCatalog.ConfigurationParameterValues.CreateOrUpdate(
							new[] { orderValue });

						var orderItemConfiguration = new Models.ServiceOrderItemConfigurationValue
						{
							Identifier = Guid.NewGuid().ToString(),
							ConfigurationParameterValueId =
								new Skyline.DataMiner.SDM.SdmObjectReference<ConfigurationModels.ConfigurationParameterValue>(
									orderValue.Identifier),
							Mandatory = specificationConfiguration.MandatoryAtServiceOrder,
						};

						repo.ServiceOrder.ServiceOrderItemConfigurationValues.CreateOrUpdate(
							new[] { orderItemConfiguration });

						orderItemConfigurations.Add(orderItemConfiguration);
					}

					instanceToReturn.ServiceInfo.Configurations = orderItemConfigurations
					.Select(configuration =>
						new Skyline.DataMiner.SDM.SdmObjectReference<Models.ServiceOrderItemConfigurationValue>(
							configuration.Identifier))
					.ToList();
				}

				return instanceToReturn;
			}
		}

		public Models.ServiceOrderEntry GetOrderEntry => orderEntry;

		public string Name => String.IsNullOrWhiteSpace(view.TboxName.Text) ? view.TboxName.PlaceHolder : view.TboxName.Text;

		public void LoadFromModel(int nr)
		{
			view.TboxName.PlaceHolder = $"Service Order Item #{nr + 1:000}";

			// Load correct types
			var categories = repo.ServiceCatalog.ServiceCategories.Read(new TRUEFilterElement<Models.ServiceCategory>()).OrderBy(x => x.Name).Select(x => new Option<Models.ServiceCategory>(x.Name, x)).ToList();
			categories.Insert(0, new Option<Models.ServiceCategory>("-None-", null));
			view.Category.SetOptions(categories);

			var specs = repo.ServiceCatalog.ServiceSpecifications.Read(new TRUEFilterElement<Models.ServiceSpecification>()).OrderBy(x => x.Name).Select(x => new Option<Models.ServiceSpecification>(x.Name, x)).ToList();
			specs.Insert(0, new Option<Models.ServiceSpecification>("-None-", null));
			view.Specification.SetOptions(specs);

			var serviceOptions = repo.ServiceInventory.Services.Read(new TRUEFilterElement<Models.Service>()).OrderBy(x => x.Name).Select(x => new Option<Models.Service>(x.Name, x)).ToList();
			serviceOptions.Insert(0, new Option<Models.Service>("-None-", null));
			view.Service.SetOptions(serviceOptions);

			view.Start.DateTime = DateTime.Now + TimeSpan.FromHours(1);
			view.End.DateTime = DateTime.Now + TimeSpan.FromDays(7);
			view.IndefiniteTime.IsChecked = false;

			UpdateUiOnActionTypeChange(view.ActionType.SelectedOption);
		}

		public void LoadFromModel(Models.ServiceOrderItem instance)
		{
			instanceToReturn = instance;
			orderEntry = new Models.ServiceOrderEntry { ServiceOrderItemId = instance };
			isEdit = true;

			// Load correct types
			LoadFromModel(0);

			view.BtnAdd.Text = "Save";
			view.TboxName.Text = instance.Name;
			view.ActionType.Selected = Enum.TryParse(instance.Action, true, out OrderActionType action)
				? action
				: OrderActionType.NoChange;
			view.TboxDescription.Text = instance.Description ?? String.Empty;
			view.Start.DateTime = instance.StartTime ?? DateTime.Now;
			view.End.DateTime = instance.EndTime ?? DateTime.Now + TimeSpan.FromDays(7);
			view.IndefiniteTime.IsChecked = instance.IndefiniteRuntime ?? false;
			if (view.IndefiniteTime.IsChecked)
			{
				view.End.IsEnabled = false;
			}

			var serviceCategoryInstance = view.Category.Values.FirstOrDefault(v => v?.Identifier == instance.ServiceInfo?.ServiceCategoryId.Identifier);
			if (serviceCategoryInstance != null)
			{
				view.Category.Selected = serviceCategoryInstance;
			}

			var serviceInstance = view.Service.Values.FirstOrDefault(v => v?.Identifier == instance.ServiceInfo?.ServiceId.Identifier);
			if (serviceInstance != null)
			{
				view.Service.Selected = serviceInstance;
			}

			var serviceSpecificationsInstance = view.Specification.Values.FirstOrDefault(v => v?.Identifier == instance.ServiceInfo?.SpecificationId.Identifier);
			if (serviceSpecificationsInstance != null)
			{
				view.Specification.Selected = serviceSpecificationsInstance;
			}
			else
			{
				view.Specification.Selected = view.Specification.Values.FirstOrDefault(v => v?.Identifier == view.Service.Selected?.ServiceSpecificationId.ToString());
			}

			UpdateUiOnActionTypeChange(view.ActionType.SelectedOption);
		}

		public bool Validate()
		{
			bool ok = true;

			ok &= ValidateLabel(Name);

			if (view.ActionType.Selected == OrderActionType.Add && view.Specification.Selected == null)
			{
				ok = false;
				view.ErrorSpecification.Text = "Selection is mandatory!";
			}
			else
			{
				view.ErrorSpecification.Text = String.Empty;
			}

			if ((view.ActionType.Selected == OrderActionType.Modify || view.ActionType.Selected == OrderActionType.Delete) && view.Service.Selected == null)
			{
				ok = false;
				view.ErrorService.Text = "Selection is mandatory!";
			}
			else
			{
				view.ErrorService.Text = String.Empty;
			}

			return ok;
		}

		private void UpdateUiOnActionTypeChange(Option<OrderActionType> actionTypeSelected)
		{
			view.TboxName.PlaceHolder = $"{actionTypeSelected.DisplayValue}";

			if (view.Specification.Selected != null)
			{
				view.TboxName.PlaceHolder += $" - {view.Specification.Selected?.Name}";
			}

			if (actionTypeSelected.Value == OrderActionType.Add)
			{
				view.Service.IsEnabled = false;
				view.Specification.IsEnabled = true;
			}
			else if (actionTypeSelected.Value == OrderActionType.Delete || actionTypeSelected.Value == OrderActionType.Modify)
			{
				view.Service.IsEnabled = true;
				view.Specification.IsEnabled = false;

				if (view.Service.Selected != null)
				{
					view.TboxName.PlaceHolder += $" - {view.Service.Selected.Name}";
				}
			}
			else
			{
				view.Service.IsEnabled = true;
				view.Specification.IsEnabled = true;
			}

			bool timeApplicable = actionTypeSelected.Value == OrderActionType.Add;
			view.LblStartTime.IsVisible = timeApplicable;
			view.Start.IsVisible = timeApplicable;
			view.LblEndTime.IsVisible = timeApplicable;
			view.End.IsVisible = timeApplicable;
			view.IndefiniteTime.IsVisible = timeApplicable;
		}

		private bool ValidateLabel(string newValue)
		{
			if (String.IsNullOrWhiteSpace(newValue))
			{
				view.ErrorName.Text = "Placeholder will be used.";
				return true;
			}

			if (getServiceOrderItemLabels.Contains(newValue, StringComparer.InvariantCultureIgnoreCase))
			{
				view.ErrorName.Text = "Label already exists!";
				return false;
			}

			view.ErrorName.Text = String.Empty;
			return true;
		}

		private static ConfigurationModels.ConfigurationParameterValue CloneConfigurationParameterValue(ConfigurationModels.ConfigurationParameterValue source)
		{
			return new ConfigurationModels.ConfigurationParameterValue
			{
				Identifier = Guid.NewGuid().ToString(),
				Label = source.Label,
				Type = source.Type,
				ConfigurationParameterId = source.ConfigurationParameterId,
				NumberOptionsId = source.NumberOptionsId,
				DiscreteOptionsId = source.DiscreteOptionsId,
				TextOptionsId = source.TextOptionsId,
				StringValue = source.StringValue,
				DoubleValue = source.DoubleValue,
				ValueFixed = source.ValueFixed,
				LinkedConfigurationReference = source.LinkedConfigurationReference,
				IsLinked = source.IsLinked,
				LinkedScript = source.LinkedScript,
				LinkedConsumers = source.LinkedConsumers,
			};
		}
	}
}
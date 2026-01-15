namespace SLC_SM_IAS_Add_Service_Order_Item_1.Presenters
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Library;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;
	using SLC_SM_IAS_Add_Service_Order_Item_1.Views;

	public class ServiceOrderItemPresenter
	{
		private readonly string[] getServiceOrderItemLabels;
		private readonly DataHelpersServiceManagement repo;
		private readonly ServiceOrderItemView view;
		private Models.ServiceOrderItems instanceToReturn;
		private bool isEdit;

		public ServiceOrderItemPresenter(ServiceOrderItemView view, DataHelpersServiceManagement repo, string[] getServiceOrderItemLabels)
		{
			this.view = view;
			this.repo = repo;
			this.getServiceOrderItemLabels = getServiceOrderItemLabels;
			instanceToReturn = new Models.ServiceOrderItems
			{
				ServiceOrderItem = new Models.ServiceOrderItem
				{
					ID = Guid.NewGuid(),
					Configurations = new List<Models.ServiceOrderItemConfigurationValue>(),
				},
			};

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

		public Models.ServiceOrderItems GetData
		{
			get
			{
				instanceToReturn.ServiceOrderItem.Name = Name;
				instanceToReturn.ServiceOrderItem.Action = view.ActionType.Selected.ToString();
				instanceToReturn.ServiceOrderItem.StartTime = view.Start.DateTime;
				instanceToReturn.ServiceOrderItem.EndTime = view.IndefiniteTime.IsChecked ? default(DateTime?) : view.End.DateTime;
				instanceToReturn.ServiceOrderItem.IndefiniteRuntime = view.IndefiniteTime.IsChecked;
				instanceToReturn.ServiceOrderItem.ServiceCategoryId = view.Category.Selected?.ID;
				instanceToReturn.ServiceOrderItem.SpecificationId = view.Specification.Selected?.ID;
				instanceToReturn.ServiceOrderItem.ServiceId = view.Service.Selected?.ID;

				if (!isEdit && view.Specification.Selected != null)
				{
					instanceToReturn.ServiceOrderItem.Configurations = view.Specification.Selected.ConfigurationParameters.Where(x => x?.ConfigurationParameter != null).Select(x => new Models.ServiceOrderItemConfigurationValue
					{
						ConfigurationParameter = x.ConfigurationParameter,
						Mandatory = x.MandatoryAtServiceOrder,
					}).ToList();
					foreach (var config in instanceToReturn.ServiceOrderItem.Configurations)
					{
						config.ConfigurationParameter.ID = Guid.NewGuid(); // Duplicate
					}
				}

				return instanceToReturn;
			}
		}

		public string Name => String.IsNullOrWhiteSpace(view.TboxName.Text) ? view.TboxName.PlaceHolder : view.TboxName.Text;

		public void LoadFromModel(int nr)
		{
			view.TboxName.PlaceHolder = $"Service Order Item #{nr + 1:000}";

			// Load correct types
			var categories = repo.ServiceCategories.Read().OrderBy(x => x.Name).Select(x => new Option<Models.ServiceCategory>(x.Name, x)).ToList();
			categories.Insert(0, new Option<Models.ServiceCategory>("-None-", null));
			view.Category.SetOptions(categories);

			var specs = repo.ServiceSpecifications.Read().OrderBy(x => x.Name).Select(x => new Option<Models.ServiceSpecification>(x.Name, x)).ToList();
			specs.Insert(0, new Option<Models.ServiceSpecification>("-None-", null));
			view.Specification.SetOptions(specs);

			var serviceOptions = repo.Services.Read().OrderBy(x => x.Name).Select(x => new Option<Models.Service>(x.Name, x)).ToList();
			serviceOptions.Insert(0, new Option<Models.Service>("-None-", null));
			view.Service.SetOptions(serviceOptions);

			view.Start.DateTime = DateTime.Now + TimeSpan.FromHours(1);
			view.End.DateTime = DateTime.Now + TimeSpan.FromDays(7);
			view.IndefiniteTime.IsChecked = false;

			UpdateUiOnActionTypeChange(view.ActionType.SelectedOption);
		}

		public void LoadFromModel(Models.ServiceOrderItems instance)
		{
			instanceToReturn = instance;
			isEdit = true;

			// Load correct types
			LoadFromModel(0);

			view.BtnAdd.Text = "Save";
			view.TboxName.Text = instance.ServiceOrderItem.Name;
			view.ActionType.Selected = Enum.TryParse(instance.ServiceOrderItem.Action, true, out OrderActionType action)
				? action
				: OrderActionType.NoChange;

			view.Start.DateTime = instance.ServiceOrderItem.StartTime ?? DateTime.Now;
			view.End.DateTime = instance.ServiceOrderItem.EndTime ?? DateTime.Now + TimeSpan.FromDays(7);
			view.IndefiniteTime.IsChecked = instance.ServiceOrderItem.IndefiniteRuntime ?? false;
			if (view.IndefiniteTime.IsChecked)
			{
				view.End.IsEnabled = false;
			}

			var serviceCategoryInstance = view.Category.Values.FirstOrDefault(v => v?.ID == instance.ServiceOrderItem.ServiceCategoryId);
			if (serviceCategoryInstance != null)
			{
				view.Category.Selected = serviceCategoryInstance;
			}

			var serviceInstance = view.Service.Values.FirstOrDefault(v => v?.ID == instance.ServiceOrderItem.ServiceId);
			if (serviceInstance != null)
			{
				view.Service.Selected = serviceInstance;
			}

			var serviceSpecificationsInstance = view.Specification.Values.FirstOrDefault(v => v?.ID == instance.ServiceOrderItem.SpecificationId);
			if (serviceSpecificationsInstance != null)
			{
				view.Specification.Selected = serviceSpecificationsInstance;
			}
			else
			{
				view.Specification.Selected = view.Specification.Values.FirstOrDefault(v => v?.ID == view.Service.Selected?.ServiceSpecificationId);
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
	}
}
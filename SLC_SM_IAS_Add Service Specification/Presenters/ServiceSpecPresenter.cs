namespace SLC_SM_IAS_Add_Service_Specification.Presenters
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Skyline.DataMiner.Automation;
	using SLC_SM_IAS_Add_Service_Specification.Views;
	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	public class ServiceSpecPresenter
	{
		private readonly IEngine engine;
		private readonly List<string> getServiceSpecLabels;
		private readonly ServiceSpecView view;
		private Models.ServiceSpecification instanceToReturn;

		public ServiceSpecPresenter(IEngine engine, ServiceSpecView view, List<string> getServiceSpecLabels)
		{
			this.engine = engine;
			this.view = view;
			this.getServiceSpecLabels = getServiceSpecLabels.ToList();
			instanceToReturn = new Models.ServiceSpecification();

			view.TboxName.Changed += (sender, args) => ValidateLabel(args.Value);
		}

		public Models.ServiceSpecification GetData
		{
			get
			{
				instanceToReturn.Name = Name;
				instanceToReturn.Description = view.Description.Text;
				instanceToReturn.Icon = view.Icon.Text;

				return instanceToReturn;
			}
		}

		public string Name => String.IsNullOrWhiteSpace(view.TboxName.Text) ? view.TboxName.PlaceHolder : view.TboxName.Text;

		public void LoadFromModel()
		{
			view.TboxName.PlaceHolder = GetDefaultSpecificationName(getServiceSpecLabels);
		}

		public void LoadFromModel(Models.ServiceSpecification instance)
		{
			instanceToReturn = instance;
			getServiceSpecLabels.RemoveAll(x => x == instance.Name);

			// Load correct types
			LoadFromModel();

			view.BtnAdd.Text = "Save";
			view.TboxName.Text = instance.Name;
			view.Description.Text = instance.Description;
			view.Icon.Text = instance.Icon;
		}

		public bool Validate()
		{
			bool ok = true;

			ok &= ValidateLabel(Name);

			return ok;
		}

		private static string GetDefaultSpecificationName(List<string> getServiceLabels)
		{
			var maxServiceId = getServiceLabels.Where(label => label.Contains("#")).Select(label => Int32.TryParse(label.Split('#').Last(), out int res) ? res : 0).ToArray();
			int newNumber = maxServiceId.Length > 0 ? maxServiceId.Max() : 0;
			return $"Specification #{newNumber + 1:00000}";
		}

		private bool ValidateLabel(string newValue)
		{
			if (String.IsNullOrWhiteSpace(newValue))
			{
				view.ErrorName.Text = "Placeholder will be used";
				return true;
			}

			if (getServiceSpecLabels.Contains(newValue, StringComparer.InvariantCultureIgnoreCase))
			{
				view.ErrorName.Text = "Name already exists!";
				return false;
			}

			view.ErrorName.Text = String.Empty;
			return true;
		}
	}
}
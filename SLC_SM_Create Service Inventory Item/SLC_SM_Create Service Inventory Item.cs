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
    Tel.    : +32 51 31 35 69
    Fax.    : +32 51 31 01 29
    E-mail    : info@skyline.be
    Web        : www.skyline.be
    Contact    : Ben Vandenberghe

****************************************************************************
Revision History:

DATE        VERSION        AUTHOR            COMMENTS

dd/mm/2025    1.0.0.1        XXX, Skyline    Initial version
****************************************************************************
*/

namespace SLC_SM_Create_Service_Inventory_Item
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading;
	using DomHelpers.SlcServicemanagement;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Core.DataMinerSystem.Automation;
	using Skyline.DataMiner.Core.DataMinerSystem.Common;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.IAS;
	using SLC_SM_Common.Extensions;
	using SLC_SM_Create_Service_Inventory_Item.Presenters;
	using SLC_SM_Create_Service_Inventory_Item.Views;

	/// <summary>
	///     Represents a DataMiner Automation script.
	/// </summary>
	public class Script
	{
		private InteractiveController _controller;
		private IEngine _engine;

		public enum Action
		{
			Add,
			AddItem,
			AddItemSilent,
			Edit,
		}

		/// <summary>
		///     The script entry point.
		/// </summary>
		/// <param name="engine">Link with SLAutomation process.</param>
		public void Run(IEngine engine)
		{
			/*
            * Note:
            * Do not remove the commented methods below!
            * The lines are needed to execute an interactive automation script from the non-interactive automation script or from Visio!
            *
            * engine.ShowUI();
            */
			if (engine.IsInteractive)
			{
				engine.FindInteractiveClient("Failed to run script in interactive mode", 1);
			}

			try
			{
				_engine = engine;
				_controller = new InteractiveController(engine) { ScriptAbortPopupBehavior = ScriptAbortPopupBehavior.HideAlways };

				RunSafe();
			}
			catch (ScriptAbortException)
			{
				// Catch normal abort exceptions (engine.ExitFail or engine.ExitSuccess)
			}
			catch (ScriptForceAbortException)
			{
				// Catch forced abort exceptions, caused via external maintenance messages.
			}
			catch (ScriptTimeoutException)
			{
				// Catch timeout exceptions for when a script has been running for too long.
			}
			catch (InteractiveUserDetachedException)
			{
				// Catch a user detaching from the interactive script by closing the window.
				// Only applicable for interactive scripts, can be removed for non-interactive scripts.
			}
			catch (Exception e)
			{
				engine.ShowErrorDialog(e);
				engine.Log(e.ToString());
			}
		}

		private static Guid CreateServiceItemFromOrderItem(DataHelpersServiceManagement repo, Models.ServiceOrderItem serviceOrderItem)
		{
			Models.Service newService = new Models.Service
			{
				ServiceID = repo.Services.UniqueServiceId(),
				Name = serviceOrderItem.Name,
				Description = serviceOrderItem.Name,
				StartTime = serviceOrderItem.StartTime,
				EndTime = serviceOrderItem.EndTime,
				Icon = String.Empty,
				ServiceSpecificationId = serviceOrderItem.SpecificationId,
				Category = serviceOrderItem.ServiceCategoryId.HasValue ? repo.ServiceCategories.Read(ServiceCategoryExposers.Guid.Equal(serviceOrderItem.ServiceCategoryId.Value)).FirstOrDefault() : null,
				ServiceItems = new List<Models.ServiceItem>(),
				ServiceItemsRelationships = new List<Models.ServiceItemRelationShip>(),
				ServiceConfiguration = new Models.ServiceConfigurationVersion
				{
					ID = Guid.NewGuid(),
					CreatedAt = DateTime.UtcNow,
					VersionName = "Default",
					Description = "Default",
					Parameters = new List<Models.ServiceConfigurationValue>(),
					Profiles = new List<Models.ServiceProfile>(),
				},
			};

			if (serviceOrderItem.Configurations != null)
			{
				newService.ServiceConfiguration.Parameters = serviceOrderItem.Configurations
					.Where(x => x?.ConfigurationParameter != null)
					.Select(
						x =>
						{
							var scv = new Models.ServiceConfigurationValue
							{
								ConfigurationParameter = x.ConfigurationParameter,
								Mandatory = x.Mandatory,
							};
							scv.ConfigurationParameter.ID = Guid.Empty;
							RemoveServiceParameterOptionsLinks(scv);
							return scv;
						})
					.ToList();
			}

			var spec = serviceOrderItem.SpecificationId.HasValue ? repo.ServiceSpecifications.Read(ServiceSpecificationExposers.Guid.Equal(serviceOrderItem.SpecificationId.Value)).FirstOrDefault() : null;
			if (spec != null)
			{
				newService.Icon = spec.Icon;
				if (spec.ServiceItemsRelationships != null)
				{
					foreach (var relationship in spec.ServiceItemsRelationships)
					{
						if (newService.ServiceItemsRelationships.All(r => r.Id != relationship.Id))
						{
							newService.ServiceItemsRelationships.Add(relationship);
						}
					}
				}

				if (spec.ServiceItems != null)
				{
					foreach (var item in spec.ServiceItems)
					{
						if (newService.ServiceItems.Any(x => x.ID == item.ID))
						{
							item.ID = newService.ServiceItems.Max(x => x.ID) + 1;
						}

						if (String.IsNullOrEmpty(item.Label))
						{
							item.Label = $"Service Item #{item.ID:000}";
						}

						if (String.IsNullOrEmpty(item.DefinitionReference))
						{
							item.DefinitionReference = String.Empty;
						}

						if (String.IsNullOrEmpty(item.Script))
						{
							item.Script = String.Empty;
						}

						newService.ServiceItems.Add(item);
					}
				}
			}

			Guid newServiceId = repo.Services.CreateOrUpdate(newService);
			return newServiceId;
		}

		private static Models.Service GetService(DataHelpersServiceManagement repo, Guid domId)
		{
			if (domId == Guid.Empty)
			{
				throw new InvalidOperationException("No existing DOM ID was provided as script input!");
			}

			return repo.Services.Read(ServiceExposers.Guid.Equal(domId)).FirstOrDefault()
				   ?? throw new InvalidOperationException($"No Dom Instance with ID '{domId}' found on the system!");
		}

		private static void RemoveServiceParameterOptionsLinks(Models.ServiceConfigurationValue config)
		{
			if (config.ConfigurationParameter.NumberOptions != null)
			{
				config.ConfigurationParameter.NumberOptions.ID = Guid.NewGuid();
			}

			if (config.ConfigurationParameter.DiscreteOptions != null)
			{
				config.ConfigurationParameter.DiscreteOptions.ID = Guid.NewGuid();
			}

			if (config.ConfigurationParameter.TextOptions != null)
			{
				config.ConfigurationParameter.TextOptions.ID = Guid.NewGuid();
			}
		}

		private static void RemoveParameterOptionsLinks(Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameterValue config)
		{
			if (config.NumberOptions != null)
			{
				config.NumberOptions.ID = Guid.NewGuid();
			}

			if (config.DiscreteOptions != null)
			{
				config.DiscreteOptions.ID = Guid.NewGuid();
			}

			if (config.TextOptions != null)
			{
				config.TextOptions.ID = Guid.NewGuid();
			}
		}

		private void AddOrUpdateService(DataHelpersServiceManagement repo, Models.Service instance)
		{
			if (!instance.ServiceSpecificationId.HasValue || instance.ServiceSpecificationId == Guid.Empty)
			{
				repo.Services.CreateOrUpdate(instance);
				return;
			}

			var serviceSpecificationInstance = instance.ServiceSpecificationId.HasValue ? repo.ServiceSpecifications.Read(ServiceSpecificationExposers.Guid.Equal(instance.ServiceSpecificationId.Value)).FirstOrDefault() : null;

			if (serviceSpecificationInstance != null)
			{
				////instance.Icon = serviceSpecificationInstance.Icon;
				instance.Description = serviceSpecificationInstance.Description;
			}

			instance.ServiceConfiguration = new Models.ServiceConfigurationVersion
			{
				VersionName = serviceSpecificationInstance?.Name,
				CreatedAt = DateTime.UtcNow,
				Parameters = new List<Models.ServiceConfigurationValue>(),
				Profiles = new List<Models.ServiceProfile>(),
			};

			if (serviceSpecificationInstance?.ConfigurationParameters != null)
			{
				instance.ServiceConfiguration.Parameters = serviceSpecificationInstance.ConfigurationParameters
					.Where(x => x?.ConfigurationParameter != null)
					.Select(
						x =>
						{
							var scv = new Models.ServiceConfigurationValue
							{
								ConfigurationParameter = x.ConfigurationParameter,
								Mandatory = x.MandatoryAtService,
							};
							scv.ConfigurationParameter.ID = Guid.Empty;
							RemoveServiceParameterOptionsLinks(scv);
							return scv;
						})
					.ToList();
			}

			if (serviceSpecificationInstance?.ConfigurationProfiles != null)
			{
				instance.ServiceConfiguration.Profiles = serviceSpecificationInstance.ConfigurationProfiles
					.Where(x => x?.Profile != null)
					.Select(
						x =>
						{
							var sp = new Models.ServiceProfile
							{
								ProfileDefinition = x.ProfileDefinition,
								Profile = x.Profile,
								Mandatory = x.MandatoryAtService,
							};
							sp.Profile.ID = Guid.Empty;
							sp.Profile.ConfigurationParameterValues = sp.Profile.ConfigurationParameterValues
								.Select(cpv =>
								{
									cpv.ID = Guid.Empty;
									RemoveParameterOptionsLinks(cpv);
									return cpv;
								})
								.ToList();
							return sp;
						})
					.ToList();
			}

			if (serviceSpecificationInstance?.ServiceItemsRelationships != null)
			{
				foreach (var relationship in serviceSpecificationInstance.ServiceItemsRelationships)
				{
					if (!instance.ServiceItemsRelationships.Contains(relationship))
					{
						instance.ServiceItemsRelationships.Add(relationship);
					}
				}
			}

			if (serviceSpecificationInstance?.ServiceItems != null)
			{
				foreach (var item in serviceSpecificationInstance.ServiceItems)
				{
					if (!instance.ServiceItems.Contains(item))
					{
						if (instance.ServiceItems.Any(x => x.ID == item.ID))
						{
							item.ID = instance.ServiceItems.Max(x => x.ID) + 1;
						}

						if (String.IsNullOrEmpty(item.Label))
						{
							item.Label = $"Service Item #{item.ID:000}";
						}

						if (String.IsNullOrEmpty(item.DefinitionReference))
						{
							item.DefinitionReference = String.Empty;
						}

						if (String.IsNullOrEmpty(item.Script))
						{
							item.Script = String.Empty;
						}

						item.Icon = instance.Icon; // inherit icon from service
						instance.ServiceItems.Add(item);
					}
				}
			}

			repo.Services.CreateOrUpdate(instance);

			if (instance.GenerateMonitoringService == true)
			{
				TryCreateDmsService(instance);
			}
		}

		private void TryCreateDmsService(Models.Service instance)
		{
			var dms = _engine.GetDms();

			if (dms.ServiceExistsSafe(instance.Name, out IDmsService _))
			{
				throw new InvalidOperationException($"A DataMiner service with name {instance.Name} already exists.");
			}

			var serviceConfiguration = new ServiceConfiguration(dms, instance.Name);
			var serviceId = dms.GetAgents().First().CreateService(serviceConfiguration);

			SetServiceIcon(dms, serviceId, instance.Icon);
		}

		private void SetServiceIcon(IDms dms, DmsServiceId serviceId, string icon)
		{
			if (!dms.PropertyExists("Logo", PropertyType.Service))
			{
				dms.CreateProperty("Logo", PropertyType.Service, false, false, false);
			}

			WaitUntilServiceCreated(serviceId, 5000);
			var service = dms.GetService(serviceId);

			var property = service.Properties.SingleOrDefault(p => p.Definition.Name == "Logo").AsWritable();

			property.Value = icon;
			service.Update();
		}

		private void WaitUntilServiceCreated(DmsServiceId serviceId, int timeout)
		{
			var sw = System.Diagnostics.Stopwatch.StartNew();

			while (_engine.FindServiceByKey(serviceId.Value) == null)
			{
				if (sw.ElapsedMilliseconds > timeout)
				{
					throw new TimeoutException($"Service {serviceId} was not created within {timeout} ms.");
				}

				Thread.Sleep(250);
			}
		}

		private void CreateNewServiceAndLinkItToServiceOrder(DataHelpersServiceManagement repo, Models.ServiceOrderItem serviceOrderItem)
		{
			if (serviceOrderItem.ServiceId.HasValue && repo.Services.Read(ServiceExposers.Guid.Equal(serviceOrderItem.ServiceId.Value)).Any())
			{
				// Already initialized - don't do anything, safety check
				return;
			}

			// Create new service item based on order
			Guid newServiceId = _engine.PerformanceLogger("Create Service Inventory Item", () => CreateServiceItemFromOrderItem(repo, serviceOrderItem));

			// Provide link on Service Order
			serviceOrderItem.ServiceId = newServiceId;
			_engine.PerformanceLogger("Update Order", () => repo.ServiceOrderItems.CreateOrUpdate(serviceOrderItem));

			// Update state
			_engine.PerformanceLogger("Update Order Item State", () =>
			{
				if (serviceOrderItem.Status == SlcServicemanagementIds.Behaviors.Serviceorderitem_Behavior.StatusesEnum.New)
				{
					serviceOrderItem = repo.ServiceOrderItems.UpdateState(serviceOrderItem, SlcServicemanagementIds.Behaviors.Serviceorderitem_Behavior.TransitionsEnum.New_To_Acknowledged);
					serviceOrderItem = repo.ServiceOrderItems.UpdateState(serviceOrderItem, SlcServicemanagementIds.Behaviors.Serviceorderitem_Behavior.TransitionsEnum.Acknowledged_To_Inprogress);
				}

				if (serviceOrderItem.Status == SlcServicemanagementIds.Behaviors.Serviceorderitem_Behavior.StatusesEnum.Acknowledged)
				{
					serviceOrderItem = repo.ServiceOrderItems.UpdateState(serviceOrderItem, SlcServicemanagementIds.Behaviors.Serviceorderitem_Behavior.TransitionsEnum.Acknowledged_To_Inprogress);
				}
			});

			// Update state of main Service Order as well
			_engine.PerformanceLogger("Update Order State", () =>
			{
				Models.ServiceOrder order = repo.ServiceOrders.Read(ServiceOrderExposers.ServiceOrderItemsExposers.ServiceOrderItem.Equal(serviceOrderItem)).FirstOrDefault();
				if (order != null)
				{
					if (order.Status == SlcServicemanagementIds.Behaviors.Serviceorder_Behavior.StatusesEnum.New)
					{
						order = repo.ServiceOrders.UpdateState(order, SlcServicemanagementIds.Behaviors.Serviceorder_Behavior.TransitionsEnum.New_To_Acknowledged);
						order = repo.ServiceOrders.UpdateState(order, SlcServicemanagementIds.Behaviors.Serviceorder_Behavior.TransitionsEnum.Acknowledged_To_Inprogress);
					}

					if (order.Status == SlcServicemanagementIds.Behaviors.Serviceorder_Behavior.StatusesEnum.Acknowledged)
					{
						order = repo.ServiceOrders.UpdateState(order, SlcServicemanagementIds.Behaviors.Serviceorder_Behavior.TransitionsEnum.Acknowledged_To_Inprogress);
					}
				}
			});
		}

		private void RunSafe()
		{
			string actionRaw = _engine.ReadScriptParamFromApp("Action");
			if (!Enum.TryParse(actionRaw, true, out Action action))
			{
				action = Action.AddItem;
			}

			string domIdRaw = _engine.ReadScriptParamFromApp("DOM ID");
			Guid.TryParse(domIdRaw, out Guid domId);

			var repo = new DataHelpersServiceManagement(_engine.GetUserConnection());

			// Init views
			var view = new ServiceView(_engine, action);
			var presenter = new ServicePresenter(_engine, repo, view);

			if (action == Action.AddItem)
			{
				var d = new MessageDialog(_engine, "Create Service Inventory Item from the selected service order item?") { Title = "Create Service Inventory Item From Order Item" };
				d.OkButton.Pressed += (sender, args) =>
				{
					AddServiceItemForOrder(domId, repo);
				};
				_controller.ShowDialog(d);
			}
			else if (action == Action.AddItemSilent)
			{
				AddServiceItemForOrder(domId, repo);
			}
			else if (action == Action.Add)
			{
				presenter.LoadFromModel();
				view.BtnAdd.Pressed += (sender, args) =>
				{
					if (presenter.Validate())
					{
						AddOrUpdateService(repo, presenter.Instance);
						throw new ScriptAbortException("OK");
					}
				};
			}
			else
			{
				// EDIT MODE
				view.BtnAdd.Text = "Save";
				presenter.LoadFromModel(GetService(repo, domId));
				view.BtnAdd.Pressed += (sender, args) =>
				{
					if (presenter.Validate())
					{
						repo.Services.CreateOrUpdate(presenter.Instance); // Only update service info
						throw new ScriptAbortException("OK");
					}
				};
			}

			// Events
			view.BtnCancel.Pressed += (sender, args) => throw new ScriptAbortException("OK");

			// Run interactive
			_controller.ShowDialog(view);
		}

		private void AddServiceItemForOrder(Guid domId, DataHelpersServiceManagement repo)
		{
			var serviceOrderItem = repo.ServiceOrderItems.Read(ServiceOrderItemExposers.Guid.Equal(domId)).FirstOrDefault();
			if (domId == Guid.Empty || serviceOrderItem == null)
			{
				throw new InvalidOperationException($"No Service Order Item with ID '{domId}' found on the system!");
			}

			_engine.PerformanceLogger("Create New Service Inventory Item + Link to Order", () => CreateNewServiceAndLinkItToServiceOrder(repo, serviceOrderItem));
			throw new ScriptAbortException("OK");
		}
	}
}
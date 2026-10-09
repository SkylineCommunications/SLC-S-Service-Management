/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE		VERSION		AUTHOR			COMMENTS

28/05/2025	1.0.0.1		RME, Skyline	Initial version
09/10/2026	1.0.0.2		SKA, Skyline	DataMinerApp support
09/10/2026	1.0.0.3		SKA, Skyline	DataMinerApp support
09/10/2026	1.0.0.3		SKA, Skyline	DataMinerApp support
****************************************************************************
*/

namespace SLC_SM_IAS_Service_Order_Configuration
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using Skyline.DataMiner.SDM;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.IAS;
	using SLC_SM_IAS_Service_Order_Configuration.Presenters;
	using SLC_SM_IAS_Service_Order_Configuration.Views;

	/// <summary>
	/// Represents a DataMiner Automation script.
	/// </summary>
	public class Script
	{
		private InteractiveController _controller;
		private IEngine _engine;

		/// <summary>
		/// The script entry point.
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

			try
			{
				_engine = engine;
				_controller = new InteractiveController(engine) { ScriptAbortPopupBehavior = ScriptAbortPopupBehavior.HideAlways };
				if (!String.IsNullOrWhiteSpace(_engine.ReadScriptParamFromApp("Payload")))
				{
					RunApiSafe();
					return;
				}

				RunSafe();
			}
			catch (ScriptAbortException)
			{
				// Catch normal abort exceptions (engine.ExitFail or engine.ExitSuccess)
				throw;
			}
			catch (ScriptForceAbortException)
			{
				// Catch forced abort exceptions, caused via external maintenance messages.
				throw;
			}
			catch (ScriptTimeoutException)
			{
				// Catch timeout exceptions for when a script has been running for too long.
				throw;
			}
			catch (InteractiveUserDetachedException)
			{
				// Catch a user detaching from the interactive script by closing the window.
				// Only applicable for interactive scripts, can be removed for non-interactive scripts.
				throw;
			}
			catch (Exception e)
			{
				engine.HandleUnexpectedException(e);
			}
		}

		private void RunApiSafe()
		{
			var payload = _engine.ReadJsonPayload<ApiPayload>("Payload", "service-order-item configuration");
			Guid domId = _engine.ReadScriptParamFromApp<Guid>("DOM ID");
			var helper = new ServiceManagementApiHelper(_engine.GetUserConnection(), "Service Ordering");
			var item = ReadServiceOrderItem(helper, domId);

			if (payload.DeleteAll)
			{
				DeleteAllConfigurations(helper, item);
				return;
			}

			DeleteConfigurations(helper, payload);
			var parameters = ReadConfigurationParameters(helper);
			var references = SaveConfigurationParameters(helper, payload.Parameters, parameters);
			UpdateServiceOrderItemConfigurations(helper, item, references);
		}

		private static ServiceOrderItem ReadServiceOrderItem(ServiceManagementApiHelper helper, Guid domId)
		{
			return helper.ServiceOrder.ServiceOrderItems
				.Read(ServiceOrderItemExposers.Identifier.Equal(domId.ToString()))
				.FirstOrDefault() ?? throw new InvalidOperationException($"Order item '{domId}' was not found.");
		}

		private static Dictionary<string, ConfigurationParameter> ReadConfigurationParameters(ServiceManagementApiHelper helper)
		{
			return helper.ServiceCatalog.ConfigurationParameters
				.Read(new TRUEFilterElement<ConfigurationParameter>())
				.ToDictionary(parameter => parameter.Identifier, StringComparer.OrdinalIgnoreCase);
		}

		private static void DeleteAllConfigurations(ServiceManagementApiHelper helper, ServiceOrderItem item)
		{
			item.ServiceInfo = item.ServiceInfo ?? new ServiceOrderItemServiceInfo();
			item.ServiceInfo.Configurations = new List<SdmObjectReference<ServiceOrderItemConfigurationValue>>();
			helper.ServiceOrder.ServiceOrderItems.Update(item);
		}

		private static void DeleteConfigurations(ServiceManagementApiHelper helper, ApiPayload payload)
		{
			foreach (var configurationId in payload.DeleteConfigurationIds ?? new List<string>())
			{
				var configuration = helper.ServiceOrder.ServiceOrderItemConfigurationValues
					.Read(ServiceOrderItemConfigurationValueExposers.Identifier.Equal(configurationId))
					.FirstOrDefault();
				if (configuration != null)
				{
					helper.ServiceOrder.ServiceOrderItemConfigurationValues.Delete(configuration);
				}
			}
		}

		private static List<SdmObjectReference<ServiceOrderItemConfigurationValue>> SaveConfigurationParameters(
			ServiceManagementApiHelper helper,
			IEnumerable<ApiParameter> parameterPayloads,
			IReadOnlyDictionary<string, ConfigurationParameter> parameters)
		{
			var references = new List<SdmObjectReference<ServiceOrderItemConfigurationValue>>();
			foreach (var parameter in parameterPayloads ?? new List<ApiParameter>())
			{
				if (!parameters.TryGetValue(parameter.ParameterId ?? String.Empty, out var definition))
				{
					throw new InvalidOperationException($"Configuration parameter '{parameter.ParameterId}' was not found.");
				}

				var value = String.IsNullOrWhiteSpace(parameter.ParameterValueId)
					? new ConfigurationParameterValue { Identifier = Guid.NewGuid().ToString() }
					: helper.ServiceCatalog.ConfigurationParameterValues
						.Read(ConfigurationParameterValueExposers.Identifier.Equal(parameter.ParameterValueId))
						.FirstOrDefault() ?? throw new InvalidOperationException($"Configuration parameter value '{parameter.ParameterValueId}' was not found.");
				value.ApplyConfigurationValue(definition, parameter.Value, parameter.Mandatory);
				helper.ServiceCatalog.ConfigurationParameterValues.CreateOrUpdate(new[] { value });

				var configuration = String.IsNullOrWhiteSpace(parameter.ConfigurationId)
					? new ServiceOrderItemConfigurationValue { Identifier = Guid.NewGuid().ToString() }
					: helper.ServiceOrder.ServiceOrderItemConfigurationValues
						.Read(ServiceOrderItemConfigurationValueExposers.Identifier.Equal(parameter.ConfigurationId))
						.FirstOrDefault() ?? throw new InvalidOperationException($"Configuration '{parameter.ConfigurationId}' was not found.");
				configuration.ConfigurationParameterValueId = new SdmObjectReference<ConfigurationParameterValue>(value.Identifier);
				configuration.Mandatory = parameter.Mandatory;
				helper.ServiceOrder.ServiceOrderItemConfigurationValues.CreateOrUpdate(new[] { configuration });
				references.Add(new SdmObjectReference<ServiceOrderItemConfigurationValue>(configuration.Identifier));
			}

			return references;
		}

		private static void UpdateServiceOrderItemConfigurations(
			ServiceManagementApiHelper helper,
			ServiceOrderItem item,
			List<SdmObjectReference<ServiceOrderItemConfigurationValue>> references)
		{
			item.ServiceInfo = item.ServiceInfo ?? new ServiceOrderItemServiceInfo();
			item.ServiceInfo.Configurations = references;
			helper.ServiceOrder.ServiceOrderItems.Update(item);
		}

		private void RunSafe()
		{
			// Input
			Guid domId = _engine.ReadScriptParamFromApp<Guid>("DOM ID");

			var serviceManagementApi = new ServiceManagementApiHelper(_engine.GetUserConnection(), "Service Ordering");
			var instance = serviceManagementApi.ServiceOrder.ServiceOrderItems.Read(ServiceOrderItemExposers.Identifier.Equal(domId.ToString())).FirstOrDefault()
				?? throw new InvalidOperationException($"Instance with ID '{domId}' does not exist");

			// Model-View-Presenter
			var view = new ServiceConfigurationView(_engine);
			var presenter = new ServiceConfigurationPresenter(_engine, _controller, view, instance, serviceManagementApi);

			presenter.LoadFromModel();

			// Run Interactive
			_controller.ShowDialog(view);
		}
	}
}
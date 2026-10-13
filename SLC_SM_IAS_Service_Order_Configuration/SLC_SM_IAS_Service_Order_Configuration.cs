/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE		VERSION		AUTHOR			COMMENTS

28/05/2025	1.0.0.1		RME, Skyline	Initial version
09/10/2026	1.0.0.2		SKA, Skyline	DataMinerApp support
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
			var ownedConfigurations = ResolveOwnedConfigurationGraph(helper, item);
			ValidatePayloadReferences(payload, ownedConfigurations);

			if (payload.DeleteAll)
			{
				DeleteAllConfigurations(helper, item);
				return;
			}

			DeleteConfigurations(helper, payload, ownedConfigurations);
			var parameters = ReadConfigurationParameters(helper);
			var references = SaveConfigurationParameters(helper, payload.Parameters, parameters, ownedConfigurations);
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

		private static OwnedConfigurationGraph ResolveOwnedConfigurationGraph(ServiceManagementApiHelper helper, ServiceOrderItem item)
		{
			var graph = new OwnedConfigurationGraph();
			var references = item.ServiceInfo?.Configurations;
			if (references == null)
			{
				return graph;
			}

			foreach (var reference in references)
			{
				var configurationId = reference == null ? null : reference.Identifier;
				if (String.IsNullOrWhiteSpace(configurationId) || !graph.ConfigurationIds.Add(configurationId))
				{
					continue;
				}

				var configuration = helper.ServiceOrder.ServiceOrderItemConfigurationValues
					.Read(ServiceOrderItemConfigurationValueExposers.Identifier.Equal(configurationId))
					.FirstOrDefault();
				if (configuration == null)
				{
					continue;
				}

				graph.Configurations[configurationId] = configuration;
				var parameterValueReference = configuration.ConfigurationParameterValueId;
				var parameterValueId = parameterValueReference == null ? null : parameterValueReference.Identifier;
				if (String.IsNullOrWhiteSpace(parameterValueId) || !graph.ParameterValueIds.Add(parameterValueId))
				{
					continue;
				}

				var parameterValue = helper.ServiceCatalog.ConfigurationParameterValues
					.Read(ConfigurationParameterValueExposers.Identifier.Equal(parameterValueId))
					.FirstOrDefault();
				if (parameterValue != null)
				{
					graph.ParameterValues[parameterValueId] = parameterValue;
				}
			}

			return graph;
		}

		private static void ValidatePayloadReferences(ApiPayload payload, OwnedConfigurationGraph ownedConfigurations)
		{
			foreach (var configurationId in payload.DeleteConfigurationIds ?? new List<string>())
			{
				if (!String.IsNullOrWhiteSpace(configurationId))
				{
					ValidateOwnedConfigurationId(configurationId, ownedConfigurations);
				}
			}

			foreach (var parameter in payload.Parameters ?? new List<ApiParameter>())
			{
				if (parameter == null)
				{
					throw new InvalidOperationException("A configuration parameter entry cannot be null.");
				}

				if (!String.IsNullOrWhiteSpace(parameter.ConfigurationId))
				{
					ValidateOwnedConfigurationId(parameter.ConfigurationId, ownedConfigurations);
				}

				if (!String.IsNullOrWhiteSpace(parameter.ParameterValueId))
				{
					ValidateOwnedParameterValueId(parameter.ParameterValueId, ownedConfigurations);
				}
			}
		}

		private static void ValidateOwnedConfigurationId(string configurationId, OwnedConfigurationGraph ownedConfigurations)
		{
			if (!ownedConfigurations.ConfigurationIds.Contains(configurationId))
			{
				throw new InvalidOperationException("The requested configuration is not assigned to the target service order item.");
			}

			if (!ownedConfigurations.Configurations.ContainsKey(configurationId))
			{
				throw new InvalidOperationException("A configuration assigned to the target service order item could not be found.");
			}
		}

		private static void ValidateOwnedParameterValueId(string parameterValueId, OwnedConfigurationGraph ownedConfigurations)
		{
			if (!ownedConfigurations.ParameterValueIds.Contains(parameterValueId))
			{
				throw new InvalidOperationException("The requested configuration parameter value is not assigned to the target service order item.");
			}

			if (!ownedConfigurations.ParameterValues.ContainsKey(parameterValueId))
			{
				throw new InvalidOperationException("A configuration parameter value assigned to the target service order item could not be found.");
			}
		}

		private static void DeleteAllConfigurations(ServiceManagementApiHelper helper, ServiceOrderItem item)
		{
			item.ServiceInfo = item.ServiceInfo ?? new ServiceOrderItemServiceInfo();
			item.ServiceInfo.Configurations = new List<SdmObjectReference<ServiceOrderItemConfigurationValue>>();
			helper.ServiceOrder.ServiceOrderItems.Update(item);
		}

		private static void DeleteConfigurations(
			ServiceManagementApiHelper helper,
			ApiPayload payload,
			OwnedConfigurationGraph ownedConfigurations)
		{
			var deletedConfigurationIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (var configurationId in payload.DeleteConfigurationIds ?? new List<string>())
			{
				if (!String.IsNullOrWhiteSpace(configurationId)
					&& deletedConfigurationIds.Add(configurationId))
				{
					helper.ServiceOrder.ServiceOrderItemConfigurationValues.Delete(ownedConfigurations.Configurations[configurationId]);
				}
			}
		}

		private static List<SdmObjectReference<ServiceOrderItemConfigurationValue>> SaveConfigurationParameters(
			ServiceManagementApiHelper helper,
			IEnumerable<ApiParameter> parameterPayloads,
			IReadOnlyDictionary<string, ConfigurationParameter> parameters,
			OwnedConfigurationGraph ownedConfigurations)
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
					: ownedConfigurations.ParameterValues[parameter.ParameterValueId];
				value.ApplyConfigurationValue(definition, parameter.Value, parameter.Mandatory);
				helper.ServiceCatalog.ConfigurationParameterValues.CreateOrUpdate(new[] { value });

				var configuration = String.IsNullOrWhiteSpace(parameter.ConfigurationId)
					? new ServiceOrderItemConfigurationValue { Identifier = Guid.NewGuid().ToString() }
					: ownedConfigurations.Configurations[parameter.ConfigurationId];
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

		private sealed class OwnedConfigurationGraph
		{
			public HashSet<string> ConfigurationIds { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			public Dictionary<string, ServiceOrderItemConfigurationValue> Configurations { get; } =
				new Dictionary<string, ServiceOrderItemConfigurationValue>(StringComparer.OrdinalIgnoreCase);

			public HashSet<string> ParameterValueIds { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			public Dictionary<string, ConfigurationParameterValue> ParameterValues { get; } =
				new Dictionary<string, ConfigurationParameterValue>(StringComparer.OrdinalIgnoreCase);
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
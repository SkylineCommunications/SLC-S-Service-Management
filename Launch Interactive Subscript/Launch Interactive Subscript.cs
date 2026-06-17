/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE        VERSION        AUTHOR           COMMENTS

dd/mm/2025    1.0.0.1      RME, Skyline		Initial version
****************************************************************************
*/

namespace Launch_Interactive_Subscript
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using DomHelpers.SlcConfigurations;
	using Library;
	using Library.Dom;
	using Newtonsoft.Json;
	using Newtonsoft.Json.Converters;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.IAS;
	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement.Models;

	/// <summary>
	///     Represents a DataMiner Automation script.
	/// </summary>
	public class Script
	{
		private IEngine engine;

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

			try
			{
				this.engine = engine;
				RunSafe();
			}
			catch (Exception e)
			{
				engine.ShowErrorDialog(e);
			}
		}

		private static List<ServiceCharacteristic> GetServiceItemCharacteristics(Models.Service service)
		{
			List<ServiceCharacteristic> serviceItemCharacteristics = new List<ServiceCharacteristic>();

			// Add references from other bookings under the service
			serviceItemCharacteristics.AddRange(service.ServiceItems.Select(s => new ServiceCharacteristic
			{
				////Id = ,
				Name = "Service Item Implementation Reference",
				Label = s.DefinitionReference,
				Type = SlcConfigurationsIds.Enums.Type.Text,
				StringValue = s.ImplementationReference,
			}));
			return serviceItemCharacteristics;
		}

		private static List<ServiceCharacteristic> GetServiceCharacteristics(Models.Service service, List<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameter> configurationParameters) => service.ServiceConfiguration?.Parameters.Select(
							x => new ServiceCharacteristic
							{
								Id = x.ConfigurationParameter.ConfigurationParameterId,
								Name = configurationParameters.FirstOrDefault(c => c.ID == x.ConfigurationParameter.ConfigurationParameterId)?.Name ?? String.Empty,
								Label = x.ConfigurationParameter.Label,
								Type = x.ConfigurationParameter.Type,
								StringValue = x.ConfigurationParameter.StringValue,
								DoubleValue = x.ConfigurationParameter.DoubleValue,
							})
						.ToList()
						?? new List<ServiceCharacteristic>();

		private static List<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameter> GetFilteredConfigurationParameters(IEngine engine, Models.Service service)
		{
			FilterElement<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameter> filterConfigParams =
				new ORFilterElement<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameter>();
			var usedConfigurationParameterIds = service.ServiceConfiguration?.Parameters
				.Where(x => x?.ConfigurationParameter?.ConfigurationParameterId != null && x.ConfigurationParameter.ConfigurationParameterId != Guid.Empty)
				.Select(x => x.ConfigurationParameter.ConfigurationParameterId)
				.ToList()
												?? new List<Guid>();
			foreach (Guid guid in usedConfigurationParameterIds)
			{
				filterConfigParams = filterConfigParams.OR(ConfigurationParameterExposers.Guid.Equal(guid));
			}

			var configurationParameters = !filterConfigParams.isEmpty()
				? new DataHelperConfigurationParameter(engine.GetUserConnection()).Read(filterConfigParams)
				: new List<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameter>();
			return configurationParameters;
		}

		private static string RunScript(IEngine engine, string scriptName, string bookingManagerElementName, ServiceItemDetails serviceItemDetails)
		{
			var subScript = engine.PrepareSubScript(scriptName);
			subScript.Synchronous = true;
			subScript.ExtendedErrorInfo = true;
			subScript.InheritScriptOutput = true;

			subScript.SelectScriptParam("Booking Manager Element Info", $"{{ \"Element\":\"{bookingManagerElementName}\",\"TableIndex\":\"\",\"Action\":\"New\",{JsonConvert.SerializeObject(serviceItemDetails).TrimStart('{')}");

			subScript.StartScript();

			if (subScript.HadError)
			{
				throw new InvalidOperationException($"Failed to start the Booking Manager script '{scriptName}' due to:\r\n" + String.Join(@"\r\n ->", subScript.GetErrorMessages()));
			}

			return subScript.GetScriptResult().FirstOrDefault(x => x.Key == "ReservationID").Value;
		}

		private void RunSafe()
		{
			Guid domId = engine.ReadScriptParamFromApp<Guid>("DOM ID");

			var srvHelper = new DataHelpersServiceManagement(engine.GetUserConnection());
			Models.Service service = srvHelper.Services.Read(ServiceExposers.Guid.Equal(domId)).FirstOrDefault()
									 ?? throw new InvalidOperationException($"No Service exists on the system with ID '{domId}'");

			string itemLabel = engine.ReadScriptParamFromApp("Item Label");
			var serviceItem = service.ServiceItems.Find(s => s.Label == itemLabel);
			if (serviceItem == null)
			{
				throw new NotSupportedException($"No service item with label '{itemLabel}' exists under service '{service.Name}', please reload the page or revise the setup.");
			}

			var configurationParameters = GetFilteredConfigurationParameters(engine, service);

			var serviceItemDetails = new ServiceItemDetails
			{
				Name = service.Name.Split(Path.GetInvalidFileNameChars())[0],
				Start = service.StartTime.HasValue ? new DateTimeOffset(service.StartTime.Value).ToUnixTimeMilliseconds() : new DateTimeOffset(DateTime.UtcNow + TimeSpan.FromHours(1)).ToUnixTimeMilliseconds(),
				End = service.EndTime.HasValue ? new DateTimeOffset(service.EndTime.Value).ToUnixTimeMilliseconds() : default(long?),
				ServiceCharacteristics = GetServiceCharacteristics(service, configurationParameters),
				ServiceItemCharacteristics = GetServiceItemCharacteristics(service),
			};

			string scriptOutput = RunScript(engine, serviceItem.Script, serviceItem.DefinitionReference, serviceItemDetails);

			serviceItem.ImplementationReference = !String.IsNullOrEmpty(scriptOutput) ? scriptOutput : Defaults.ReferenceUnknown;
			srvHelper.Services.CreateOrUpdate(service);

			// Update Service Item to active (if applicable)
			if (!String.IsNullOrEmpty(scriptOutput))
			{
				service.UpdateStatusOnServiceItem(engine.GetUserConnection());
			}
		}
	}

	internal sealed class ServiceItemDetails
	{
		public string Name { get; set; }

		public long Start { get; set; }

		public long? End { get; set; }

		public List<ServiceCharacteristic> ServiceCharacteristics { get; set; }

		public List<ServiceCharacteristic> ServiceItemCharacteristics { get; set; }
	}

	internal sealed class ServiceCharacteristic
	{
		public Guid Id { get; set; }

		public string Name { get; set; }

		public string Label { get; set; }

		[JsonConverter(typeof(StringEnumConverter))]
		public SlcConfigurationsIds.Enums.Type Type { get; set; }

		public string StringValue { get; set; }

		public double? DoubleValue { get; set; }
	}
}
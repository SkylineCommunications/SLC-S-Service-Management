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
namespace Launch_Interactive_Subscript
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using DomHelpers.SlcConfigurations;
	using Newtonsoft.Json;
	using Newtonsoft.Json.Converters;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.IAS;
	using static DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Service_Behavior;
	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement.Models;

	/// <summary>
	///     Represents a DataMiner Automation script.
	/// </summary>
	public class Script
	{
		private const string ReferenceUnknown = "Reference Unknown";

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
				Guid domId = engine.ReadScriptParamFromApp<Guid>("DOM ID");

				var srvHelper = new DataHelpersServiceManagement(engine.GetUserConnection());
				Models.Service service = srvHelper.Services.Read(ServiceExposers.Guid.Equal(domId)).FirstOrDefault()
										 ?? throw new InvalidOperationException($"No Service exists on the system with ID '{domId}'");

				string itemLabel = engine.ReadScriptParamFromApp("Item Label");
				var serviceItem = service.ServiceItems.Find(s => s.Label == itemLabel);
				if (serviceItem == null)
				{
					return;
				}

				var configurationParameters = GetFilteredConfigurationParameters(engine, service);

				List<ServiceCharacteristic> serviceCharacteristics = service.ServiceConfiguration?.Parameters.Select(
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

				var serviceItemDetails = new ServiceItemDetails
				{
					Name = service.Name.Split(Path.GetInvalidFileNameChars())[0],
					Start = service.StartTime.HasValue ? new DateTimeOffset(service.StartTime.Value).ToUnixTimeMilliseconds() : new DateTimeOffset(DateTime.UtcNow + TimeSpan.FromHours(1)).ToUnixTimeMilliseconds(),
					End = service.EndTime.HasValue ? new DateTimeOffset(service.EndTime.Value).ToUnixTimeMilliseconds() : default(long?),
					ServiceCharacteristics = serviceCharacteristics,
					ServiceItemCharacteristics = serviceItemCharacteristics,
				};

				string scriptOutput = RunScript(engine, serviceItem.Script, serviceItem.DefinitionReference, serviceItemDetails);

				serviceItem.ImplementationReference = !String.IsNullOrEmpty(scriptOutput) ? scriptOutput : ReferenceUnknown;
				srvHelper.Services.CreateOrUpdate(service);

				// Update Service Item to active (if applicable)
				if (!String.IsNullOrEmpty(scriptOutput))
				{
					UpdateState(srvHelper, service);
				}
			}
			catch (Exception e)
			{
				engine.ShowErrorDialog(e);
			}
		}

		private static void UpdateState(DataHelpersServiceManagement srvHelper, Models.Service service)
		{
			// If all items are in progress -> move to In Progress
			if (!service.ServiceItems.All(x => !String.IsNullOrEmpty(x.ImplementationReference) && x.ImplementationReference != ReferenceUnknown))
			{
				return;
			}

			if (service.Status == StatusesEnum.New)
			{
				service = srvHelper.Services.UpdateState(service, TransitionsEnum.New_To_Designed);
			}

			if (service.Status == StatusesEnum.Designed)
			{
				service = srvHelper.Services.UpdateState(service, TransitionsEnum.Designed_To_Reserved);
			}

			if (service.Status == StatusesEnum.Reserved)
			{
				service = srvHelper.Services.UpdateState(service, TransitionsEnum.Reserved_To_Active);
			}
		}

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
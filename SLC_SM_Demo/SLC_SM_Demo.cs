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
	Tel.	: +32 51 31 35 69
	Fax.	: +32 51 31 01 29
	E-mail	: info@skyline.be
	Web		: www.skyline.be
	Contact	: Ben Vandenberghe

****************************************************************************
Revision History:

DATE		VERSION		AUTHOR			COMMENTS

05/08/2025	1.0.0.1		RME, Skyline	Initial version
****************************************************************************
*/

namespace SLCSMDemo
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using DomHelpers.SlcConfigurations;
	using DomHelpers.SlcPeople_Organizations;
	using DomHelpers.SlcServicemanagement;
	using Newtonsoft.Json;
	using Skyline.DataMiner.Analytics.GenericInterface.QueryBuilder;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.Helper;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.Net.Sections;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM;
	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement.Models;

	/// <summary>
	/// Represents a DataMiner Automation script.
	/// </summary>
	public class Script
	{
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
				RunSafe(engine);
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
				engine.ExitFail("Run|Something went wrong: " + e);
			}
		}

		private void RunSafe(IEngine engine)
		{
			//FilterServiceOnCategory(engine);

			//FilterServiceOnCharacteristic(engine);

			//CreateServiceInventoryItemsPerOrder(engine);
			//UpdateAllFixedValueParams(engine);
			AddFieldDescriptor(engine);
		}

		private static void AddFieldDescriptor(IEngine engine)
		{
			DomHelper domHelper = new DomHelper(engine.SendSLNetMessages, SlcServicemanagementIds.ModuleId);

			var ownerField = new DomInstanceFieldDescriptor
			{
				ID = new FieldDescriptorID(new Guid("1856aa22-c48b-4cfa-9418-2e4aa1ced47b")),
				Name = "Owner",
				FieldType = typeof(Guid),
				IsOptional = true,
				IsHidden = false,
				IsReadonly = false,
				IsSoftDeleted = false,
				DomDefinitionIds = { new DomDefinitionId(SlcPeople_OrganizationsIds.Definitions.People.Id) }
			};

			var section = domHelper.SectionDefinitions.Read(SectionDefinitionExposers.ID.Equal(SlcServicemanagementIds.Sections.ServiceOrderInfo.Id))[0] as CustomSectionDefinition;
			section.AddOrReplaceFieldDescriptor(ownerField);

			domHelper.SectionDefinitions.Update(section);
		}

		private static void FilterServiceOnCategory(IEngine engine)
		{
			string categoryType = "Channel";
			string categoryName = "ARD";

			var dataHelpers = new DataHelpersServiceManagement(engine.GetUserConnection());

			var categoryToMatch = dataHelpers.ServiceCategories.Read().Find(x => x.Type == categoryType && x.Name == categoryName)
								  ?? throw new InvalidOperationException($"No Category found matching '{categoryType}-{categoryName}'");

			var services = dataHelpers.Services.Read(ServiceExposers.ServiceCategory.Equal(categoryToMatch));
			engine.GenerateInformation($"Service(s) found:\r\n{String.Join(Environment.NewLine, services.Select(s => $"{s.Name} ({s.ID})"))}");
		}

		private static void FilterServiceOnCharacteristic(IEngine engine)
		{
			string configurationParameter = "Service Type";
			//string configurationParameterLabel = "Service Type";
			string configurationParameterValue = "Channel";

			var dataHelpersSrvMgmt = new DataHelpersServiceManagement(engine.GetUserConnection());
			var services = dataHelpersSrvMgmt.Services.GetServicesByCharacteristic(configurationParameter, null, configurationParameterValue);

			engine.GenerateInformation($"Service(s) found:\r\n{String.Join(Environment.NewLine, services.Select(s => $"{s.Name} ({s.ID})"))}");
		}

		private static void UpdateAllFixedValueParams(IEngine engine)
		{
			DomHelper domHelper = new DomHelper(engine.SendSLNetMessages, SlcConfigurationsIds.ModuleId);
			var instances = domHelper.DomInstances.Read(DomInstanceExposers.DomDefinitionId.Equal(SlcConfigurationsIds.Definitions.ConfigurationParameterValue.Id));
			engine.GenerateInformation(JsonConvert.SerializeObject(instances));
			//foreach (var item in instances)
			//{
			//	item.Sections.First(x => x.SectionDefinitionID.Id == SlcConfigurationsIds.Sections.ConfigurationParameterValue.Id.Id).RemoveFieldValueById(SlcConfigurationsIds.Sections.ConfigurationParameterValue.ValueFixed);
			//}

			//engine.GenerateInformation($"Items to update: {instances.Count}");
			//foreach (var batch in instances.Batch(100))
			//{
			//	domHelper.DomInstances.CreateOrUpdate(batch.ToList());
			//}

			//var section = domHelper.SectionDefinitions.Read(SectionDefinitionExposers.ID.Equal(SlcConfigurationsIds.Sections.ConfigurationParameterValue.Id.Id)).FirstOrDefault();
			//section.GetFieldDescriptorById(SlcConfigurationsIds.Sections.ConfigurationParameterValue.ValueFixed).FieldType = typeof(bool);
			//domHelper.SectionDefinitions.Update(section);
		}

		private static void CreateServiceInventoryItemsPerOrder(IEngine engine)
		{
			var dataHelper = new DataHelpersServiceManagement(engine.GetUserConnection());
			var orders = dataHelper.ServiceOrders.Read();
			int i = 0;
			foreach (var order in orders)
			{
				foreach (var orderItem in order.OrderItems)
				{
					if (orderItem.ServiceOrderItem.ServiceId.HasValue)
					{
						continue;
					}

					////if (i > 10)
					////{
					////	return;
					////}

					RunScript(engine, orderItem.ServiceOrderItem);
					i++;
				}
			}
		}

		private static void RunScript(IEngine engine, Models.ServiceOrderItem orderItem)
		{
			engine.GenerateInformation($"Creating Service Inventory Item for Order Item ID {orderItem.ID}/{orderItem.Name}");

			// Prepare a subscript
			SubScriptOptions subScript = engine.PrepareSubScript("SLC_SM_Create Service Inventory Item");

			// Link the main script dummies to the subscript
			subScript.SelectScriptParam("DOM ID", orderItem.ID.ToString());
			subScript.SelectScriptParam("Action", "AddItemSilent");

			// Set some more options
			subScript.Synchronous = true;
			subScript.InheritScriptOutput = true;

			// Launch the script
			subScript.StartScript();
			if (subScript.HadError)
			{
				throw new InvalidOperationException("Script failed");
			}
		}
	}
}

namespace SLC_Service_Management_RT.RegressionTests
{
	using System;
	using System.Collections.Generic;

	using Skyline.AppInstaller;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Core.DataMinerSystem.Automation;
	using Skyline.DataMiner.Core.DataMinerSystem.Common;
	using Skyline.DataMiner.Net.AppPackages;

	using SLC_Service_Management_RT.QaPortal;

	/// <summary>
	/// Installs the regression test scripts and registers them on the QAPortal element.
	/// </summary>
	internal class RegressionTestInstaller
	{
		/// <summary>
		/// Every regression test script of this package starts with this prefix.
		/// </summary>
		public const string ScriptPrefix = "SLC_SM_RT_";

		private const string ServiceCatalogGroup = "Service Management - Service Catalog";
		private const string ServiceInventoryGroup = "Service Management - Service Inventory";
		private const string ServiceOrderGroup = "Service Management - Service Order";

		private static readonly RegressionTest[] Tests =
		{
			// Service Catalog
			new RegressionTest { Group = ServiceCatalogGroup, Name = "Create Service Specification", Script = "SLC_SM_RT_CreateServiceSpecification" },
			new RegressionTest { Group = ServiceCatalogGroup, Name = "Delete Service Specification", Script = "SLC_SM_RT_DynamicDeleteSpec" },
			new RegressionTest { Group = ServiceCatalogGroup, Name = "Delete Service Category", Script = "SLC_SM_RT_DeleteServiceCategory" },
			new RegressionTest { Group = ServiceCatalogGroup, Name = "Delete Service", Script = "SLC_SM_RT_DynamicDeleteService" },
			new RegressionTest { Group = ServiceCatalogGroup, Name = "Service Specification Configuration", Script = "SLC_SM_RT_ServiceSpecificationConfiguration" },
			new RegressionTest { Group = ServiceCatalogGroup, Name = "Edit Service Specification Configuration", Script = "SLC_SM_RT_ServiceSpecificationConfiguration_Edit" },

			// Service Inventory
			new RegressionTest { Group = ServiceInventoryGroup, Name = "Add Service Item", Script = "SLC_SM_RT_AddServiceItem" },
			new RegressionTest { Group = ServiceInventoryGroup, Name = "Add Service Item with Duplicate Label", Script = "SLC_SM_RT_AddServiceItemDuplicateLabel" },
			new RegressionTest { Group = ServiceInventoryGroup, Name = "Create Service Inventory Item", Script = "SLC_SM_RT_CreateServiceInventoryItem" },
			new RegressionTest { Group = ServiceInventoryGroup, Name = "Service Configuration", Script = "SLC_SM_RT_ServiceConfiguration" },
			new RegressionTest { Group = ServiceInventoryGroup, Name = "Edit and Duplicate Service Configuration", Script = "SLC_SM_RT_ServiceConfiguration_EditDuplicate" },
			new RegressionTest { Group = ServiceInventoryGroup, Name = "Service Configuration Item", Script = "SLC_SM_RT_ServiceConfigurationItem" },
			new RegressionTest { Group = ServiceInventoryGroup, Name = "Edit and Duplicate Service Configuration Item", Script = "SLC_SM_RT_ServiceConfigurationItem_EditDuplicate" },
			new RegressionTest { Group = ServiceInventoryGroup, Name = "Service State Transition", Script = "SLC_SM_RT_ServiceStateTransition" },
			new RegressionTest { Group = ServiceInventoryGroup, Name = "Invalid Service Transition", Script = "SLC_SM_RT_InvalidServiceTransition" },

			// Service Order
			new RegressionTest { Group = ServiceOrderGroup, Name = "Create Service Order", Script = "SLC_SM_RT_CreateServiceOrder" },
			new RegressionTest { Group = ServiceOrderGroup, Name = "Service Order Item", Script = "SLC_SM_RT_ServiceOrderItem" },
			new RegressionTest { Group = ServiceOrderGroup, Name = "Edit Service Order Item", Script = "SLC_SM_RT_ServiceOrderItem_Edit" },
			new RegressionTest { Group = ServiceOrderGroup, Name = "Service Order Item Configuration", Script = "SLC_SM_RT_ServiceOrderItemConfiguration" },
			new RegressionTest { Group = ServiceOrderGroup, Name = "Edit Service Order Item Configuration", Script = "SLC_SM_RT_ServiceOrderItemConfiguration_Edit" },
			new RegressionTest { Group = ServiceOrderGroup, Name = "Service Order Item State Transition", Script = "SLC_SM_RT_ServiceOrderItemStateTransition" },
			new RegressionTest { Group = ServiceOrderGroup, Name = "Service Order Item Lifecycle", Script = "SLC_SM_RT_ServiceOrderItemLifecycle" },
			new RegressionTest { Group = ServiceOrderGroup, Name = "Service Order Item Cancellation", Script = "SLC_SM_RT_ServiceOrderItemCancellationFlow" },
			new RegressionTest { Group = ServiceOrderGroup, Name = "Service Order Completion", Script = "SLC_SM_RT_ServiceOrderCompletion" },
		};

		private readonly AppInstaller installer;
		private readonly IDms dms;
		private readonly Lazy<SkylineQaPortal> lazySkylineQaPortal;

		public RegressionTestInstaller(IEngine engine, AppInstallContext context)
		{
			if (engine == null)
			{
				throw new ArgumentNullException(nameof(engine));
			}

			installer = new AppInstaller(Engine.SLNetRaw, context);
			dms = engine.GetDms();
			lazySkylineQaPortal = new Lazy<SkylineQaPortal>(() => SkylineQaPortal.GetOrCreateQaPortalElement(engine, dms));
		}

		private SkylineQaPortal SkylineQaPortal => lazySkylineQaPortal.Value;

		/// <summary>
		/// Installs the regression test scripts and their shared library.
		/// </summary>
		public void InstallDefaultContent()
		{
			installer.Log("Installing default content...");
			installer.InstallDefaultContent();
		}

		/// <summary>
		/// Gets or creates the QAPortal element and registers every regression test on it.
		/// </summary>
		public void AddOrUpdateQaPortalElement()
		{
			installer.Log("Registering the regression tests on the QAPortal element...");
			SkylineQaPortal.AddOrUpdateRegressionTests(new List<RegressionTest>(Tests), ScriptPrefix);
		}

		/// <summary>
		/// Determines whether the QAPortal element is currently running tests.
		/// </summary>
		/// <returns><c>true</c> when tests are running; <c>false</c> when they are not or the connector is not available.</returns>
		public bool AreTestsRunning()
		{
			if (!dms.ProtocolExists(SkylineQaPortal.ProtocolName, SkylineQaPortal.ProtocolVersion))
			{
				return false;
			}

			return SkylineQaPortal.AreTestsRunning();
		}
	}
}

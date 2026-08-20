/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE		VERSION		AUTHOR			COMMENTS

28/08/2025	1.0.0.1		RCA, Skyline	Initial version
20/08/2026	1.0.0.2		SKA, Skyline	Integration with Document Hub
****************************************************************************
*/

namespace SLCSMASSetIcon
{
	using System;
	using System.Collections;
	using System.IO;
	using System.Linq;
	using DomHelpers.SlcServicemanagement;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.Solutions.DocumentHub.Automation;
	using Skyline.DataMiner.Solutions.DocumentHub.SDM.Models;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.IAS;
	using SLC_SM_AS_SetIcon;

	/// <summary>
	/// Represents a DataMiner Automation script.
	/// </summary>
	public class Script
	{
		private const string PublicPathPrefix = "/Public/";

		private ScriptData _scriptData;
		private DomHelper _domHelper;
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
			if (engine.IsInteractive)
			{
				engine.FindInteractiveClient("Failed to run script in interactive mode", 1);
			}

			try
			{
				RunSafe(engine);
			}
			catch (ScriptAbortException)
			{
				// Catch normal abort exceptions (engine.ExitFail or engine.ExitSuccess)
				throw; // Comment if it should be treated as a normal exit of the script.
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
				engine.ShowErrorDialog(e);
			}
		}

		private static void SetServiceItemIcon()
		{
			throw new NotImplementedException();
		}

		private static bool IsWebPathOrUrl(string value)
		{
			if (value.StartsWith(PublicPathPrefix, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			return Uri.TryCreate(value, UriKind.Absolute, out Uri uri) &&
				(uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
		}

		private static string NormalizeWebPath(string webPath)
		{
			if (Uri.TryCreate(webPath, UriKind.Absolute, out Uri uri) &&
				(uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
			{
				return webPath;
			}

			return webPath.StartsWith("/") ? webPath : $"/{webPath}";
		}

		private static bool TryConvertToPublicWebPath(string input, out string webPath)
		{
			webPath = String.Empty;
			if (String.IsNullOrWhiteSpace(input))
			{
				return false;
			}

			const string marker = "public\\";
			var normalizedInput = input.Replace('/', '\\');
			var publicIndex = normalizedInput.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
			if (publicIndex < 0)
			{
				if (normalizedInput.StartsWith("webfilemanager\\", StringComparison.OrdinalIgnoreCase))
				{
					webPath = NormalizeWebPath($"/Public/{normalizedInput.Replace('\\', '/')}");
					return true;
				}

				return false;
			}

			var afterPublic = normalizedInput.Substring(publicIndex + "public".Length).Replace('\\', '/');
			webPath = NormalizeWebPath($"/Public{afterPublic}");
			return true;
		}

		private void RunSafe(IEngine engine)
		{
			_engine = engine;
			_scriptData = new ScriptData(engine);
			_domHelper = new DomHelper(engine.SendSLNetMessages, SlcServicemanagementIds.ModuleId);

			SetIcon();
		}

		private void SetIcon()
		{
			switch (_scriptData.Type)
			{
				case ScriptData.ObjectType.ServiceCategory:
					SetServiceCategoryIcon();
					break;
				case ScriptData.ObjectType.Service:
					SetServiceIcon();
					break;
				case ScriptData.ObjectType.ServiceItem:
					SetServiceItemIcon();
					break;
				default:
					break;
			}
		}

		private void SetServiceIcon()
		{
			var filter = DomInstanceExposers.DomDefinitionId.Equal(SlcServicemanagementIds.Definitions.Services.Id)
				.AND(DomInstanceExposers.Id.Equal(_scriptData.DomId));

			var instance = _domHelper.DomInstances.Read(filter).FirstOrDefault();
			if (instance == null)
			{
				throw new Exception($"Could not find instance with id {_scriptData.DomId}");
			}

			var service = new ServicesInstance(instance);
			var icon = ResolveIconWebPath(_scriptData.Name);

			service.ServiceInfo.Icon = icon;
			service.Save(_domHelper);
		}

		private void SetServiceCategoryIcon()
		{
			var filter = DomInstanceExposers.DomDefinitionId.Equal(SlcServicemanagementIds.Definitions.ServiceCategory.Id)
				.AND(DomInstanceExposers.Id.Equal(_scriptData.DomId));

			var instance = _domHelper.DomInstances.Read(filter).FirstOrDefault();
			if (instance == null)
			{
				throw new Exception($"Could not find instance with id {_scriptData.DomId}");
			}

			var serviceCategory = new ServiceCategoryInstance(instance);

			serviceCategory.ServiceCategoryInfo.Icon = ResolveIconWebPath(_scriptData.Name);
			serviceCategory.Save(_domHelper);
		}

		private string ResolveIconWebPath(string value)
		{
			if (String.IsNullOrWhiteSpace(value))
			{
				return String.Empty;
			}

			var trimmedValue = value.Trim();
			if (IsWebPathOrUrl(trimmedValue))
			{
				return NormalizeWebPath(trimmedValue);
			}

			if (TryConvertToPublicWebPath(trimmedValue, out string webPath))
			{
				return webPath;
			}

			if (TryResolveDocumentHubPathByFileName(trimmedValue, out string resolvedPath))
			{
				return resolvedPath;
			}

			return trimmedValue;
		}

		private bool TryResolveDocumentHubPathByFileName(string input, out string resolvedPath)
		{
			resolvedPath = String.Empty;
			var fileName = Path.GetFileName(input);
			if (String.IsNullOrWhiteSpace(fileName))
			{
				return false;
			}

			try
			{
				dynamic documentHubHelper = _engine.GetDocumentHubApiHelper();
				var docHubClient = _engine.GetDocHubClient();
				var buckets = ((IEnumerable)documentHubHelper.DocumentBuckets.Read(null))
					.Cast<DocumentBucket>()
					.OrderByDescending(bucket => bucket.IsDefault)
					.ThenBy(bucket => bucket.Name ?? String.Empty)
					.ToList();

				if (buckets.Count == 0)
				{
					return false;
				}

				foreach (DocumentBucket bucket in buckets)
				{
					var files = docHubClient.Files.ReadFiles(bucket, null);
					var match = files.FirstOrDefault(file => String.Equals(file.GetName(), fileName, StringComparison.OrdinalIgnoreCase));
					if (match == null)
					{
						continue;
					}

					var webPath = match.GetWebPath();
					if (String.IsNullOrWhiteSpace(webPath))
					{
						continue;
					}

					resolvedPath = NormalizeWebPath(webPath);
					return true;
				}
			}
			catch
			{
				// Ignore lookup failures and fall back to input value.
			}

			return false;
		}
	}
}

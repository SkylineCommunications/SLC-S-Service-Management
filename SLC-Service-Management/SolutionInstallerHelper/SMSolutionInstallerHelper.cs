namespace SLC_ServiceManagement.SolutionInstallerHelper
{
	using System;
	using System.Collections.Generic;
	using System.Drawing;
	using System.IO;
	using System.Linq;
	using System.Net.Http;
	using System.Net.Http.Headers;
	using System.Text;
	using System.Threading;
	using global::SolutionInstallerHelper.Models;
	using Newtonsoft.Json;
	using Newtonsoft.Json.Linq;

	using Skyline.AppInstaller;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net;
	using Skyline.DataMiner.Net.AppPackages;
	using Skyline.DataMiner.Net.Exceptions;
	using Skyline.DataMiner.Net.GRPCConnection;
	using Skyline.DataMiner.Net.Messages;
	using Skyline.DataMiner.Net.Messages.Advanced;
	using Skyline.DataMiner.Utils.AppInstallerHelper;
	using Skyline.DataMiner.Utils.SecureCoding.SecureIO;
	using Skyline.DataMiner.Utils.SecureCoding.SecureSerialization.Json.Newtonsoft;
	using SLC_ServiceManagement.SolutionInstallerHelper.Models;

	/// <summary>
	/// Performs the installation steps of the Service Management package: default content, DOM import,
	/// SDM registration, MediaOps fallback dependencies, the DataMiner app, and the DataMiner Assistant
	/// custom agent shipped under <c>SetupContent\Agents\{Agents,Skills,Scripts}</c>.
	/// The Assistant part is adapted from the SLC-S-InfraOps SolutionInstaller.
	/// </summary>
	/// <remarks>
	/// The Assistant steps (<see cref="DeployAssistantContext"/> and <see cref="RegisterAssistantTools"/>) are non-fatal:
	/// a missing or older Assistant DxM never aborts the package installation. The other steps throw on failure.
	/// </remarks>
	public class SMSolutionInstallerHelper
	{
		private const int AgentBindMaxAttempts = 36;
		private const int AgentBindRetryDelayMs = 5000;
		private const int AgentPostBindVerifyDelayMs = 5000;
		private const int AgentPostBindVerifyMaxRetries = 3;
		private const int ToolRegisterMaxAttempts = 4;
		private const int ToolRegisterRetryDelayMs = 2000;

		private static readonly string[] _contextFolders = { "Skills", "Scripts", "Agents" };
		private static readonly string[] _solutionAgents = { "57beb7fa-dfe4-49ca-885d-5220e4f28b52" };

		private readonly AppInstallContext _context;
		private readonly IEngine _engine;
		private readonly AppInstaller _installer;
		private readonly string _ownedPrefix;
		private readonly string _setupContentPath;
		private readonly IConnection _userConnection;

		/// <summary>
		/// Initializes a new instance of the <see cref="SMSolutionInstallerHelper"/> class.
		/// </summary>
		/// <param name="engine">The Automation engine.</param>
		/// <param name="context">The package installation context.</param>
		/// <param name="ownedPrefix">The name prefix that identifies Assistant skills and script files owned by this package.</param>
		/// <remarks>The installer and the Assistant web API calls use the connection returned by <see cref="IEngine.GetUserConnection"/>.</remarks>
		/// <exception cref="ArgumentNullException">
		public SMSolutionInstallerHelper(IEngine engine, AppInstallContext context, string ownedPrefix)
		{
			_engine = engine ?? throw new ArgumentNullException(nameof(engine));
			_context = context ?? throw new ArgumentNullException(nameof(context));
			_ownedPrefix = ownedPrefix ?? throw new ArgumentNullException(nameof(ownedPrefix));

			_userConnection = _engine.GetUserConnection();
			_installer = new AppInstaller(_userConnection, context);
			_setupContentPath = _installer.GetSetupContentDirectory();
		}

		/// <summary>
		/// Copies the shipped agent, skills and script-tool context files from <c>SetupContent\Agents</c> into the Assistant's
		/// <c>Context\Custom</c> folder, overwriting existing files. Stale items are removed only when they are owned by this
		/// package (known agent IDs, or skills and scripts that start with the owned prefix).
		/// </summary>
		/// <remarks>Non-fatal: failures are logged and do not abort the installation.</remarks>
		public void DeployAssistantContext()
		{
			try
			{
				SecurePath setupContentRoot = SecurePath.CreateSecurePath(_setupContentPath);
				SecurePath setupAgentsContent = SecurePath.ConstructSecurePath(setupContentRoot, "Agents");
				SecurePath customRoot = SecurePath.ConstructSecurePath(
					Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
					"Skyline Communications",
					"DataMiner Assistant",
					"Synced Documents",
					"Context",
					"Custom");

				Directory.CreateDirectory(customRoot);

				foreach (var folderName in _contextFolders)
				{
					SecurePath source = SecurePath.ConstructSecurePath(setupAgentsContent, folderName);
					SecurePath target = SecurePath.ConstructSecurePath(customRoot, folderName);

					if (!Directory.Exists(source))
					{
						continue;
					}

					try
					{
						Directory.CreateDirectory(target);
						RemoveAgentStaleOwnedItems(source, target, folderName);
						DeployAgentShippedItems(source, target);
						_engine.Log($"DeployAssistantContext: deployed '{folderName}'.");
					}
					catch (Exception folderEx)
					{
						_engine.Log($"DeployAssistantContext: failed to deploy '{folderName}' (non-fatal): {folderEx.Message}");
					}
				}
			}
			catch (Exception ex)
			{
				_installer.Log($"Skipping Assistant context deployment: {ex.Message}");
				_engine.Log($"DeployAssistantContext failed (non-fatal): {ex}");
			}
		}

		/// <summary>
		/// Registers the Service Management static web app in DataMiner.
		/// </summary>
		public void DeployDataMinerApp()
		{
			var installerHelper = new AppInstallerHelper(_engine.GetUserConnection(), _context);
			installerHelper.WebApplications.RegisterStaticApp(
				id: Constants.SolutionAppId,
				name: Constants.SolutionAppName,
				url: Constants.SolutionAppUrl,
				icon: "ms-Icon--BuildDefinition",
				color: Color.FromArgb(220, 38, 38));
		}

		/// <summary>
		/// Checks whether MediaOps is installed according to the SDM registry. If it is not, deploys the shipped
		/// fallback solution library DLLs that are not yet present on the agent.
		/// </summary>
		/// <exception cref="IOException">Copying a fallback DLL failed.</exception>
		/// <exception cref="DataMinerException">Syncing a fallback DLL failed.</exception>
		public void DeployMediaOpsDependencies()
		{
			_installer.Log("Validating MediaOps dependency for Service Management installation (SDM registry + fallback DLL checks)...");

			if (IsMediaOpsInstalledInSdmRegistry())
			{
				_installer.Log("MediaOps is installed according to the SDM registry.");
			}
			else
			{
				_installer.Log("MediaOps is not installed according to the SDM registry. Falling back to file-based dependency validation.");
				EnsureSolutionLibraryFallbackDependencies(_engine, _installer);
			}
		}

		/// <summary>
		/// Imports the DOM definitions from <c>SetupContent\DOMImportExport</c> by running the DOM ImportExport subscript.
		/// </summary>
		/// <exception cref="DirectoryNotFoundException">The DOM import folder does not exist.</exception>
		/// <exception cref="InvalidOperationException">The DOM ImportExport subscript reported an error.</exception>
		public void ImportSolutionDom()
		{
			_installer.Log("Importing DOM...");
			var path = SecurePath.ConstructSecurePath(_setupContentPath, "DOMImportExport");
			_engine.GenerateInformation($"setupContentPath for DOM: {path}");

			if (!Directory.Exists(path))
			{
				throw new DirectoryNotFoundException($"DOM import folder was not found: '{path}'.");
			}

			RunDomImportSubScript(_engine, path);
			_installer.Log("DOM import completed successfully.");
		}

		/// <summary>
		/// Installs the package default content.
		/// </summary>
		public void InstallDefaultContent()
		{
			_installer.Log("Installing default content...");
			_installer.InstallDefaultContent();
		}

		/// <summary>
		/// Registers the script tools described by <c>SetupContent\Agents\Scripts\*.md</c> through the DataMiner web API
		/// and binds them to the shipped agent. Existing tools with the same name are deleted and recreated.
		/// </summary>
		/// <remarks>
		/// Non-fatal: failures are logged and do not abort the installation. Requires a connection that can issue
		/// an authentication ticket; installer connections that cannot are logged as skipped.
		/// </remarks>
		public void RegisterAssistantTools()
		{
			_installer.Log("Registering DataMiner Assistant script tools...");

			try
			{
				SecurePath setupContentRoot = SecurePath.CreateSecurePath(_setupContentPath);
				SecurePath scriptsDir = SecurePath.ConstructSecurePath(setupContentRoot, "Agents", "Scripts");
				var tools = ParseDirectory(scriptsDir);

				if (tools.Count == 0)
				{
					_installer.Log("RegisterAssistantTools: no script tool context files found; skipping.");
					return;
				}

				var baseUri = CreateBaseUrl(_userConnection);
				var userTicket = GetTicket(_userConnection);

				using (var httpClient = new HttpClient())
				{
					var webConnectionId = ConnectToWebApi(httpClient, userTicket, baseUri);

					if (String.IsNullOrEmpty(webConnectionId))
					{
						throw new InvalidOperationException("ConnectAppAndInfoUsingTicket returned an empty connection id.");
					}

					TryRegisterTools(httpClient, webConnectionId, baseUri, tools, out List<string> registeredTools);
					TryBindAgentToTools(httpClient, webConnectionId, baseUri, registeredTools);
				}
			}
			catch (Exception ex)
			{
				_installer.Log($"Skipping Assistant tools registration: {ex.Message}");
				_engine.Log($"RegisterAssistantTools failed (non-fatal): {ex}");
			}
		}

		/// <summary>
		/// Registers the solution, with the version of the package being installed, in the SDM registry.
		/// </summary>
		/// <exception cref="InvalidOperationException">The registration subscript reported an error.</exception>
		public void RegisterSolution()
		{
			_installer.Log($"Registering Solution {Constants.SolutionDisplayName} [{Constants.SolutionCatalogId}] with version {_context.AppInfo.Version} in SDM...");

			var subscript = _engine.PrepareSubScript(Constants.SolutionRegisterScriptName);
			subscript.SelectScriptParam("Id", Constants.SolutionCatalogId);
			subscript.SelectScriptParam("DisplayName", Constants.SolutionDisplayName);
			subscript.SelectScriptParam("Version", _context.AppInfo.Version);

			subscript.Synchronous = true;
			subscript.ExtendedErrorInfo = true;
			subscript.StartScript();

			if (subscript.HadError)
			{
				throw new InvalidOperationException($"Run Subscript '{Constants.SolutionRegisterScriptName}' failed: {String.Join(" -> ", subscript.GetErrorMessages())}");
			}
		}

		private static string ConnectToWebApi(HttpClient httpClient, string ticket, Uri baseUri)
		{
			string json = JsonConvert.SerializeObject(new ConnectAppAndInfoUsingTicketMessage(ticket));
			using (var content = new StringContent(json, Encoding.UTF8, MediaTypeHeaderValue.Parse("application/json").MediaType))
			{
				var response = httpClient.PostAsync(new Uri(baseUri, "API/v1/json.asmx/ConnectAppAndInfoUsingTicket"), content).GetAwaiter().GetResult();
				response.EnsureSuccessStatusCode();
				string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
				return SecureNewtonsoftDeserialization.DeserializeObject<ConnectAppAndInfoUsingTicketResponse>(body).D.Connection;
			}
		}

		private static Uri CreateBaseUrl(IConnection connection)
		{
			if (connection is GRPCConnection grpc)
			{
				return new Uri(grpc.ConnectionString);
			}

			var info = connection.HandleSingleResponseMessage(new GetInfoMessage(InfoType.LocalGeneralInfoMessage)) as GeneralInfoEventMessage
				?? throw new DataMinerException("Failed to retrieve general info.");

			var protocol = info.HTTPS ? "https" : "http";
			var host = !String.IsNullOrEmpty(info.CertificateAddressName) ? info.CertificateAddressName : "localhost";

			return new Uri($"{protocol}://{host}/");
		}

		private static void EnsureSolutionLibraryFallbackDependencies(IEngine engine, AppInstaller installer)
		{
			foreach (var dependency in Constants.SolutionLibraryFallbackDependencies)
			{
				EnsureSolutionLibraryFallbackDependency(engine, installer, dependency);
			}
		}

		private static void EnsureSolutionLibraryFallbackDependency(IEngine engine, AppInstaller installer, FallbackDependency dependency)
		{
			if (File.Exists(dependency.TargetDllPath))
			{
				installer.Log($"{dependency.SolutionLibraryName} runtime DLL already available at '{dependency.TargetDllPath}'. Skipping fallback deployment.");
				return;
			}

			if (!File.Exists(dependency.SourceDllPath))
			{
				installer.Log($"Fallback {dependency.SolutionLibraryName} DLL is not present at '{dependency.SourceDllPath}'. Skipping fallback deployment.");
				return;
			}

			Directory.CreateDirectory(dependency.TargetDirectory);
			File.Copy(dependency.SourceDllPath, dependency.TargetDllPath, overwrite: false);
			SyncFile(engine, dependency.TargetDllPath);
			installer.Log($"Deployed fallback {dependency.SolutionLibraryName} DLL to '{dependency.TargetDirectory}'.");
		}

		private static string FindAssistantInstallDir()
		{
			var candidates = new[]
			{
				@"C:\Program Files\Skyline Communications\DataMiner Assistant",
				@"C:\Program Files (x86)\Skyline Communications\DataMiner Assistant",
			};

			foreach (var path in candidates)
			{
				if (IsAssistantInstallDir(path))
					return path;
			}

			return null;
		}

		private static string GetTicket(IConnection connection)
		{
			try
			{
				var response = connection.HandleSingleResponseMessage(new RequestTicketMessage(TicketType.Authentication, Array.Empty<byte>())) as TicketResponseMessage;
				return response?.Ticket ?? throw new InvalidOperationException("Empty ticket response.");
			}
			catch (Exception ex)
			{
				throw new InvalidOperationException("Retrieving a web API ticket failed.", ex);
			}
		}

		private static bool IsAssistantInstallDir(string path)
		{
			return Directory.Exists(path) && File.Exists(Path.Combine(path, "appsettings.json"));
		}

		private static bool ParseBool(string value, bool fallback)
		{
			if (bool.TryParse(value, out bool parsed))
			{
				return parsed;
			}

			return fallback;
		}

		private static IReadOnlyList<DMAToolInfo> ParseDirectory(string scriptsDirectory)
		{
			var tools = new List<DMAToolInfo>();

			if (string.IsNullOrEmpty(scriptsDirectory) || !Directory.Exists(scriptsDirectory))
			{
				return tools;
			}

			foreach (var file in Directory.GetFiles(scriptsDirectory, "*.md"))
			{
				var tool = ParseFile(file);
				if (tool != null)
				{
					tools.Add(tool);
				}
			}

			return tools;
		}

		private static DMAToolInfo ParseFile(string filePath)
		{
			string content = File.ReadAllText(filePath);
			if (string.IsNullOrEmpty(content))
			{
				return null;
			}

			string normalized = content.Replace("\r\n", "\n").Replace("\r", "\n");

			string frontMatter;
			string body;
			if (!TrySplit(normalized, out frontMatter, out body))
			{
				return null;
			}

			string name = null;
			string scriptName = null;
			string description = null;
			bool sync = true;
			bool requiresUserValidation = false;
			var arguments = new List<DMAScriptParameter>();

			var lines = frontMatter.Split('\n');
			for (int i = 0; i < lines.Length; i++)
			{
				string line = lines[i];
				if (line.Trim().Length == 0)
				{
					continue;
				}

				// Top-level keys start in column 0.
				if (!char.IsWhiteSpace(line[0]))
				{
					int colon = line.IndexOf(':');
					if (colon < 0)
					{
						continue;
					}

					string key = line.Substring(0, colon).Trim();
					string value = StripScalar(line.Substring(colon + 1));

					switch (key)
					{
						case "name":
							name = value;
							break;

						case "scriptName":
							scriptName = value;
							break;

						case "description":
							description = value;
							break;

						case "sync":
							sync = ParseBool(value, true);
							break;

						case "requiresUserValidation":
							requiresUserValidation = ParseBool(value, false);
							break;

						case "inputArguments":
							i = ParseInputArguments(lines, i + 1, arguments);
							break;

						default:
							break;
					}
				}
			}

			if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(scriptName))
			{
				return null;
			}

			if (arguments.Count == 0)
			{
				arguments.Add(new DMAScriptParameter
				{
					Name = "JsonInputData",
					Description = "Single stringified JSON envelope { operation, payload }.",
					Example = string.Empty,
				});
			}

			return new DMAToolInfo
			{
				Name = name,
				ScriptName = scriptName,
				Description = description ?? string.Empty,
				Instructions = body.Trim(),
				Parameters = arguments,
				Source = "Custom",
				Type = "Script",
				ResponseBehavior = sync ? "Wait" : "Continue",
				RequiresUserValidation = requiresUserValidation,
			};
		}

		private static int ParseInputArguments(string[] lines, int start, List<DMAScriptParameter> arguments)
		{
			DMAScriptParameter current = null;
			int i = start;

			for (; i < lines.Length; i++)
			{
				string line = lines[i];

				if (line.Trim().Length == 0)
				{
					continue;
				}

				// A new top-level key (column 0, non-whitespace) ends the list.
				if (!char.IsWhiteSpace(line[0]))
				{
					return i - 1;
				}

				string trimmed = line.TrimStart();

				if (trimmed.StartsWith("- "))
				{
					current = new DMAScriptParameter();
					arguments.Add(current);
					trimmed = trimmed.Substring(2).TrimStart();
				}

				if (current == null)
				{
					continue;
				}

				int colon = trimmed.IndexOf(':');
				if (colon < 0)
				{
					continue;
				}

				string key = trimmed.Substring(0, colon).Trim();
				string value = StripScalar(trimmed.Substring(colon + 1));

				switch (key)
				{
					case "name":
						current.Name = value;
						break;

					case "description":
						current.Description = value;
						break;

					case "example":
						current.Example = value;
						break;

					default:
						break;
				}
			}

			return i - 1;
		}

		private static TResponse PostAssistantApi<TResponse>(HttpClient httpClient, Uri baseUri, string methodName, object message)
					where TResponse : class
		{
			string json = JsonConvert.SerializeObject(message);
			using (var content = new StringContent(json, Encoding.UTF8, MediaTypeHeaderValue.Parse("application/json").MediaType))
			{
				var response = httpClient.PostAsync(new Uri(baseUri, $"API/v1/Internal.asmx/{methodName}"), content).GetAwaiter().GetResult();
				string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
				if (!response.IsSuccessStatusCode)
				{
					throw new InvalidOperationException($"POST {methodName} -> HTTP {(int)response.StatusCode} {response.ReasonPhrase}.");
				}

				return SecureNewtonsoftDeserialization.DeserializeObject<TResponse>(body);
			}
		}

		private static void RunDomImportSubScript(IEngine engine, string path)
		{
			var subScript = engine.PrepareSubScript(Constants.DomImportExportScriptName);
			subScript.ExtendedErrorInfo = true;
			subScript.SelectScriptParam("Action", "Import");
			subScript.SelectScriptParam("Path", path);
			subScript.SelectScriptParam("ModuleNames", "-1");
			subScript.Synchronous = true;
			subScript.StartScript();

			if (subScript.HadError)
			{
				throw new InvalidOperationException($"Run Subscript 'DOM ImportExport' failed: {String.Join(" -> ", subScript.GetErrorMessages())}");
			}
		}

		private static string StripScalar(string raw)
		{
			if (raw == null)
			{
				return string.Empty;
			}

			string value = raw.Trim();
			if (value.Length >= 2)
			{
				char first = value[0];
				char last = value[value.Length - 1];
				if ((first == '\'' && last == '\'') || (first == '"' && last == '"'))
				{
					value = value.Substring(1, value.Length - 2);
					value = first == '\''
						? value.Replace("''", "'")
						: value.Replace("\\\"", "\"");
				}
			}

			return value;
		}

		private static void SyncFile(IEngine engine, string path)
		{
			var message = new SetDataMinerInfoMessage
			{
				What = 41,
				StrInfo1 = path,
				IInfo2 = 34,
			};

			var response = engine.SendSLNetSingleResponseMessage(message) ?? throw new DataMinerException("Server response was null while syncing fallback dependency.");
			if (response is CreateProtocolFileResponse createProtocolFileResponse && createProtocolFileResponse.ErrorCode != 0)
			{
				throw new DataMinerException($"Syncing fallback dependency failed with error code: {createProtocolFileResponse.ErrorCode}.");
			}
		}

		private static bool TrySplit(string normalized, out string frontMatter, out string body)
		{
			frontMatter = null;
			body = null;

			if (!normalized.StartsWith("---\n"))
			{
				return false;
			}

			int searchStart = 4;
			int closing = normalized.IndexOf("\n---", searchStart, StringComparison.Ordinal);
			if (closing < 0)
			{
				return false;
			}

			frontMatter = normalized.Substring(searchStart, closing - searchStart);

			int bodyStart = normalized.IndexOf('\n', closing + 1);
			body = bodyStart < 0 ? string.Empty : normalized.Substring(bodyStart + 1);
			return true;
		}

		private void DeployAgentShippedItems(SecurePath source, SecurePath target)
		{
			foreach (var name in Directory.GetFiles(source).Select(Path.GetFileName))
			{
				var sourceFile = SecurePath.ConstructSecurePath(source, name);
				var targetFile = SecurePath.ConstructSecurePath(target, name);

				File.Copy(sourceFile, targetFile, overwrite: true);
				_engine.Log($"DeployAssistantContext: wrote '{targetFile}'.");
			}

			foreach (var name in Directory.GetDirectories(source).Select(d => new DirectoryInfo(d).Name))
			{
				var sourceDir = SecurePath.ConstructSecurePath(source, name);
				var targetDir = SecurePath.ConstructSecurePath(target, name);

				Directory.CreateDirectory(targetDir);
				_engine.Log($"DeployAssistantContext: created directory '{targetDir}'.");

				DeployAgentShippedItems(sourceDir, targetDir);
			}
		}

		/// <summary>
		/// Creates or merges <c>appsettings.custom.json</c> of the Assistant DxM to enable custom agents and script tools.
		/// </summary>
		private void DeployAssistantAppSettings()
		{
			try
			{
				string installDir = FindAssistantInstallDir();
				if (String.IsNullOrEmpty(installDir))
				{
					_installer.Log("DataMiner Assistant DxM not found; custom agent feature flags not set.");
					return;
				}

				string targetPath = Path.Combine(installDir, "appsettings.custom.json");
				var desired = JObject.Parse(SLC_ServiceManagement.SolutionInstallerHelper.Assistant.AppSettingsContent.AssistantAppSettingsJson);
				JObject merged = desired;

				if (File.Exists(targetPath))
				{
					try
					{
						string raw = File.ReadAllText(targetPath);
						var existing = String.IsNullOrWhiteSpace(raw) ? new JObject() : JObject.Parse(raw);
						existing.Merge(desired, new JsonMergeSettings
						{
							MergeArrayHandling = MergeArrayHandling.Replace,
							MergeNullValueHandling = MergeNullValueHandling.Ignore,
						});
						merged = existing;
					}
					catch (JsonException ex)
					{
						_engine.Log("DeployAssistantAppSettings: existing file unparseable (" + ex.Message + "); replacing with defaults.");
					}
				}

				File.WriteAllText(targetPath, JsonConvert.SerializeObject(merged, Formatting.Indented));
				_engine.Log("DeployAssistantAppSettings: wrote " + targetPath + ".");
			}
			catch (Exception ex)
			{
				_installer.Log("Skipping Assistant appsettings deployment: " + ex.Message);
				_engine.Log("DeployAssistantAppSettings failed (non-fatal): " + ex);
			}
		}

		private DMAAgentInfo GetAgent(HttpClient httpClient, string connectionId, Uri baseUri, string agentId)
		{
			var message = new GetAssistantAgentByIdMessage { AgentId = agentId, Connection = connectionId };
			return PostAssistantApi<GetAssistantAgentByIdResponse>(httpClient, baseUri, "GetAssistantAgentById", message)?.D;
		}

		private string GetShippedAgentId()
		{
			SecurePath agentsDir = SecurePath.ConstructSecurePath(_setupContentPath, "Agents", "Agents");
			if (!Directory.Exists(agentsDir))
			{
				return null;
			}

			return Directory.GetDirectories(agentsDir)
				.Where(d => File.Exists(SecurePath.ConstructSecurePath(d, "agent.md")))
				.Select(d => new DirectoryInfo(d).Name)
				.FirstOrDefault();
		}

		private bool IsMediaOpsInstalledInSdmRegistry()
		{
			try
			{
				var helper = new AppPackageHelper(_engine.SendSLNetMessage);
				var installedApps = helper.GetInstalledApps();
				var mediaOpsApp = installedApps.FirstOrDefault(app =>
				{
					var appInfo = app?.AppInfo;
					var appIdName = appInfo?.AppID?.Name;
					return String.Equals(appIdName, Constants.MediaOpsCatalogId, StringComparison.OrdinalIgnoreCase)
						|| String.Equals(appInfo?.DisplayName, Constants.MediaOpsCatalogName, StringComparison.OrdinalIgnoreCase)
						|| String.Equals(appInfo?.Name, Constants.MediaOpsCatalogName, StringComparison.OrdinalIgnoreCase);
				});

				if (mediaOpsApp == null)
				{
					return false;
				}

				_installer.Log($"MediaOps detected in SDM registry: Name='{mediaOpsApp.AppInfo?.Name}', DisplayName='{mediaOpsApp.AppInfo?.DisplayName}', Version='{mediaOpsApp.AppInfo?.Version}'.");
				return true;
			}
			catch (Exception e)
			{
				_installer.Log($"Failed to query SDM registry for MediaOps detection. Falling back to file-based dependency validation. Reason: {e.Message}");
				return false;
			}
		}

		private bool IsOwned(string name)
		{
			return !String.IsNullOrEmpty(name) && name.StartsWith(_ownedPrefix, StringComparison.OrdinalIgnoreCase);
		}

		private void RemoveAgentStaleOwnedItems(SecurePath source, SecurePath target, string folderName)
		{
			if (String.Equals(folderName, "Agents", StringComparison.OrdinalIgnoreCase))
			{
				// Agents are GUID-named; only remove folders whose GUID is a known agent of this solution
				// and that are no longer shipped in the source package.
				var shippedAgents = new HashSet<string>(
					Directory.GetDirectories(source)
					.Select(d => new DirectoryInfo(d).Name),
					StringComparer.OrdinalIgnoreCase);

				var ownAgents = new HashSet<string>(
					_solutionAgents.Select(a => a.ToString()),
					StringComparer.OrdinalIgnoreCase);

				foreach (var name in Directory.GetDirectories(target)
					.Select(d => new DirectoryInfo(d).Name)
					.Where(n => ownAgents.Contains(n) && !shippedAgents.Contains(n)))
				{
					TryDelete(SecurePath.ConstructSecurePath(target, name), isDirectory: true);
				}
			}
			else if (String.Equals(folderName, "Scripts", StringComparison.OrdinalIgnoreCase))
			{
				var shipped = new HashSet<string>(
					Directory.GetFiles(source).Select(Path.GetFileName),
					StringComparer.OrdinalIgnoreCase);

				foreach (var name in Directory.GetFiles(target)
					.Select(Path.GetFileName)
					.Where(n => IsOwned(n) && !shipped.Contains(n)))
				{
					TryDelete(SecurePath.ConstructSecurePath(target, name), isDirectory: false);
				}
			}
			else if (String.Equals(folderName, "Skills", StringComparison.OrdinalIgnoreCase))
			{
				var shippedDirs = new HashSet<string>(
					Directory.GetDirectories(source).Select(d => new DirectoryInfo(d).Name),
					StringComparer.OrdinalIgnoreCase);

				foreach (var name in Directory.GetDirectories(target)
					.Select(d => new DirectoryInfo(d).Name)
					.Where(n => IsOwned(n) && !shippedDirs.Contains(n)))
				{
					TryDelete(SecurePath.ConstructSecurePath(target, name), isDirectory: true);
				}
			}
			else
			{
				// TODO: log a warning that we don't know how to prune this folder type, but still deploy the shipped items.
			}
		}

		private bool TryBindAgentToTools(HttpClient httpClient, string connectionId, Uri baseUri, IReadOnlyList<string> toolNames)
		{
			try
			{
				string agentId = GetShippedAgentId();
				if (String.IsNullOrEmpty(agentId))
				{
					_installer.Log("TryBindAgentToTools: no agent folder found under SetupContent\\Agents; skipping.");
					return false;
				}

				DMAAgentInfo agent;
				if (!TryGetAgentWithWait(httpClient, connectionId, baseUri, agentId, out agent))
				{
					_installer.Log($"TryBindAgentToTools: failed to get agent '{agentId}' with wait; skipping bind (a reinstall will retry).");
					return false;
				}

				var bound = new List<string>(agent.Tools ?? new List<string>());
				var missingTools = toolNames
					.Where(n => !bound.Any(b => String.Equals(b, n, StringComparison.OrdinalIgnoreCase)))
					.ToList();

				if (missingTools.Count == 0)
				{
					_installer.Log($"TryBindAgentToTools: all {toolNames.Count} tool(s) bound to agent '{agentId}'.");
					return true;
				}

				bound.AddRange(missingTools);
				var message = new UpdateAssistantAgentMessage
				{
					AgentId = agentId,
					Connection = connectionId,
					Configuration = new DMAAgentConfiguration
					{
						Tools = bound,
						Skills = agent.Skills ?? new List<string>(),
						Name = agent.Name,
						Description = agent.Description,
						Prompt = agent.Prompt,
					},
				};

				var response = PostAssistantApi<UpdateAssistantAgentResponse>(
					httpClient,
					baseUri,
					"UpdateAssistantAgent",
					message);

				_installer.Log($"TryBindAgentToTools: bind {missingTools.Count} tool(s): {(response?.D?.IsSuccess == true ? "ok" : response?.D?.ErrorMessage ?? "no response")}");

				// The DxM may reload agent.md asynchronously and drop the tools again; verify after a delay.
				Thread.Sleep(AgentPostBindVerifyDelayMs);
				return true;
			}
			catch (Exception ex)
			{
				_engine.Log($"TryBindAgentToTools failed (non-fatal): {ex}");
				return false;
			}
		}

		private bool TryCreateTool(HttpClient httpClient, string connectionId, Uri baseUri, DMAToolInfo tool)
		{
			try
			{
				var response = PostAssistantApi<AssistantToolMutationResponse>(
					httpClient,
					baseUri,
					"CreateAssistantTool",
					new CreateAssistantToolMessage { Connection = connectionId, ToolInfo = tool });

				if (response?.D == null || !response.D.IsSuccess)
				{
					_installer.Log($"CreateAssistantTool '{tool.Name}' failed: {response?.D?.ErrorMessage ?? "no response"}");
					return false;
				}

				return true;
			}
			catch (Exception ex)
			{
				_installer.Log($"CreateAssistantTool '{tool.Name}' failed: {ex}");
				return false;
			}
		}

		private void TryDelete(SecurePath path, bool isDirectory)
		{
			try
			{
				if (isDirectory && Directory.Exists(path))
				{
					Directory.Delete(path, recursive: true);
				}
				else if (!isDirectory && File.Exists(path))
				{
					File.Delete(path);
				}
			}
			catch (Exception ex)
			{
				_engine.Log($"DeployAssistantContext: failed to remove '{path}' (non-fatal): {ex.Message}");
			}
		}

		private bool TryDeleteTool(HttpClient httpClient, string connectionId, Uri baseUri, string toolName)
		{
			try
			{
				// TODO: consider merging instead of deleting and recreating, to preserve user edits to the tool description or instructions.
				var message = new DeleteAssistantToolMessage
				{
					Connection = connectionId,
					Reference = new DMAToolReference { Name = toolName, Type = 1 },
				};

				var response = PostAssistantApi<AssistantToolMutationResponse>(httpClient, baseUri, "DeleteAssistantTool", message);
				string error = response?.D?.ErrorMessage ?? String.Empty;
				if (response?.D == null ||
					!response.D.IsSuccess ||
					error.IndexOf("not found", StringComparison.OrdinalIgnoreCase) < 0)
				{
					_installer.Log($"DeleteAssistantTool '{toolName}' returned: {error}");
					return false;
				}

				return response.D.IsSuccess;
			}
			catch (Exception ex)
			{
				_installer.Log($"DeleteAssistantTool '{toolName}' failed: {ex}");
				return false;
			}
		}

		private bool TryGetAgentWithWait(HttpClient httpClient, string connectionId, Uri baseUri, string agentId, out DMAAgentInfo agent)
		{
			for (int attempt = 1; attempt <= AgentBindMaxAttempts; attempt++)
			{
				agent = GetAgent(httpClient, connectionId, baseUri, agentId);

				if (agent == null ||
					String.IsNullOrEmpty(agent.AgentId) ||
					String.IsNullOrEmpty(agent.Name))
				{
					Thread.Sleep(AgentBindRetryDelayMs);
				}
				else
				{
					return true;
				}
			}

			agent = null;
			return false;
		}

		private bool TryRegisterTools(HttpClient httpClient, string connectionId, Uri baseUri, IEnumerable<DMAToolInfo> tools, out List<string> registeredTools)
		{
			registeredTools = new List<string>();
			bool result = true;
			foreach (var tool in tools)
			{
				// TODO: consider merging instead of deleting and recreating, to preserve user edits to the tool description or instructions.
				TryDeleteTool(httpClient, connectionId, baseUri, tool.Name);

				if (!TryCreateTool(httpClient, connectionId, baseUri, tool))
				{
					result = false;
				}

				registeredTools.Add(tool.Name);
			}

			_installer.Log($"RegisterAssistantTools: registered {registeredTools.Count}/{tools.Count()} tool(s).");
			return result;
		}
	}
}
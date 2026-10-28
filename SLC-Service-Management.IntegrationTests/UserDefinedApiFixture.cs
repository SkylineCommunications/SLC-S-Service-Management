namespace SLC_Service_Management.IntegrationTests
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Net;
	using System.Net.Http;
	using System.Net.Http.Headers;
	using System.Threading;

	using Skyline.DataMiner.Core.DataMinerSystem.Common;
	using Skyline.DataMiner.Net;
	using Skyline.DataMiner.Net.Apps.UserDefinableApis;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;

	/// <summary>
	/// Makes the Service Management User-Defined API callable on a clean DataMiner: runs the solution's
	/// <c>SLC_SM_Setup_UDAPI</c> script (the solution installer does not), creates a dedicated API token and grants it
	/// on every Service Management API definition. Disposing removes the grants and deletes the token.
	/// </summary>
	public sealed class UserDefinedApiFixture : IDisposable
	{
		public const string SetupScriptName = "SLC_SM_Setup_UDAPI";
		public const string RoutePrefix = "service-management/v1/";

		private static readonly TimeSpan EndpointReadyTimeout = TimeSpan.FromMinutes(3);

		private readonly UserDefinableApiHelper helper;
		private readonly List<ApiDefinition> grantedDefinitions = new List<ApiDefinition>();
		private ApiToken token;

		private UserDefinedApiFixture(UserDefinableApiHelper helper)
		{
			this.helper = helper;
		}

		/// <summary>Gets the base URL of the Service Management API, without a trailing slash.</summary>
		public string BaseUrl { get; } = DmsTestSetup.WebBaseUrl + "/api/custom/" + RoutePrefix.TrimEnd('/');

		/// <summary>Gets the secret of the token that is allowed on every Service Management API route.</summary>
		public string Secret { get; private set; }

		/// <summary>Gets a human-readable description of the provisioned API, for assertion messages.</summary>
		public string Description { get; private set; }

		public static UserDefinedApiFixture Provision(Connection connection, IDms dms)
		{
			var helper = new UserDefinableApiHelper(connection.HandleMessages);
			var fixture = new UserDefinedApiFixture(helper);

			try
			{
				fixture.RunSetupScript(dms);
				fixture.CreateTokenAndGrant();
				fixture.WaitUntilEndpointAnswers();
				return fixture;
			}
			catch
			{
				fixture.Dispose();
				throw;
			}
		}

		public HttpClient CreateHttpClient(string secret)
		{
			var handler = new HttpClientHandler
			{
				// The QAOps DataMiner serves HTTPS with a self-signed certificate.
				ServerCertificateCustomValidationCallback = (message, certificate, chain, errors) => true,
			};

			var client = new HttpClient(handler, disposeHandler: true)
			{
				BaseAddress = new Uri(BaseUrl + "/"),
				Timeout = TimeSpan.FromSeconds(60),
			};

			if (!String.IsNullOrEmpty(secret))
			{
				client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secret);
			}

			return client;
		}

		public void Dispose()
		{
			if (token == null)
			{
				return;
			}

			foreach (var definition in grantedDefinitions)
			{
				try
				{
					definition.SecuritySettings.AllowedTokens.Remove(token.ID);
					helper.ApiDefinitions.Update(definition);
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Could not revoke the test token on '{definition.Route}' (suppressed): {ex.Message}");
				}
			}

			try
			{
				helper.ApiTokens.Delete(token);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Could not delete the test token (suppressed): {ex.Message}");
			}

			token = null;
		}

		private void RunSetupScript(IDms dms)
		{
			IDmsAutomationScript script;
			try
			{
				script = dms.GetScript(SetupScriptName);
			}
			catch (ScriptNotFoundException ex)
			{
				throw new InvalidOperationException($"Automation script '{SetupScriptName}' is not installed. Was the Service Management .dmapp deployed before the tests ran? Installed scripts: {String.Join(", ", dms.GetScripts().Select(s => s.Name).OrderBy(n => n))}", ex);
			}

			var result = script.Execute(
				Enumerable.Empty<DmsAutomationScriptParamValue>(),
				Enumerable.Empty<DmsAutomationScriptDummyValue>(),
				new DmsAutomationScriptRunOptions { ExtendedErrorInfo = true, CheckSets = false });

			if (result.HadError)
			{
				throw new InvalidOperationException($"'{SetupScriptName}' failed: {String.Join(" -> ", result.ErrorMessages ?? Array.Empty<string>())}");
			}
		}

		private void CreateTokenAndGrant()
		{
			var allDefinitions = helper.ApiDefinitions.Read(new TRUEFilterElement<ApiDefinition>()).ToList();
			var definitions = allDefinitions
				.Where(d => d.Route != null && d.Route.StartsWith(RoutePrefix, StringComparison.OrdinalIgnoreCase))
				.OrderBy(d => d.Route)
				.ToList();

			if (definitions.Count == 0)
			{
				throw new InvalidOperationException($"'{SetupScriptName}' ran but no API definition with route prefix '{RoutePrefix}' exists. All routes: {String.Join(", ", allDefinitions.Select(d => d.Route))}");
			}

			Secret = ApiTokenSecretGenerator.GenerateSecret();
			token = helper.ApiTokens.Create(new ApiToken
			{
				Name = "SLC-Service-Management.IntegrationTests " + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"),
				Secret = Secret,
			});

			foreach (var definition in definitions)
			{
				definition.SecuritySettings.AllowedTokens.Add(token.ID);
				grantedDefinitions.Add(helper.ApiDefinitions.Update(definition));
			}

			Description = $"{definitions.Count} API definitions with prefix '{RoutePrefix}' granted to token '{token.Name}': {String.Join(", ", definitions.Select(d => d.Route))}";
		}

		private void WaitUntilEndpointAnswers()
		{
			var deadline = DateTime.UtcNow + EndpointReadyTimeout;
			string lastOutcome = "(no request made)";

			using (var client = CreateHttpClient(Secret))
			{
				while (DateTime.UtcNow < deadline)
				{
					try
					{
						using (var response = client.GetAsync("serviceCategory?limit=1").GetAwaiter().GetResult())
						{
							if (response.StatusCode == HttpStatusCode.OK)
							{
								return;
							}

							lastOutcome = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}: {ApiResponse.Truncate(response.Content.ReadAsStringAsync().GetAwaiter().GetResult())}";
						}
					}
					catch (Exception ex)
					{
						lastOutcome = ex.GetType().Name + ": " + ex.Message;
					}

					Thread.Sleep(TimeSpan.FromSeconds(5));
				}
			}

			throw new TimeoutException($"The Service Management API at {BaseUrl} did not answer GET serviceCategory?limit=1 with HTTP 200 within {EndpointReadyTimeout.TotalMinutes} minutes. Last outcome: {lastOutcome}. {Description}");
		}
	}
}

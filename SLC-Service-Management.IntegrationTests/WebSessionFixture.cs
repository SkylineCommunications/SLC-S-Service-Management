namespace SLC_Service_Management.IntegrationTests
{
	using System;
	using System.Net;
	using System.Net.Http;
	using System.Text;

	using Newtonsoft.Json;
	using Newtonsoft.Json.Linq;

	/// <summary>
	/// A DataMiner web API session (<c>Json.asmx/ConnectApp</c>). The web app reads the connection ID from the
	/// <c>DMAConnection</c> cookie that the DataMiner <c>/auth/</c> page normally sets, so tests inject it directly.
	/// </summary>
	public sealed class WebSessionFixture : IDisposable
	{
		public const string CookieName = "DMAConnection";

		private readonly HttpClient client;

		private WebSessionFixture(HttpClient client, string connectionId)
		{
			this.client = client;
			ConnectionId = connectionId;
		}

		public string ConnectionId { get; }

		public static WebSessionFixture Connect(string baseUrl, string userName, string password)
		{
			ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

			var handler = new HttpClientHandler
			{
				ServerCertificateCustomValidationCallback = (message, certificate, chain, errors) => true,
			};

			var client = new HttpClient(handler, disposeHandler: true)
			{
				BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"),
				Timeout = TimeSpan.FromSeconds(60),
			};

			try
			{
				string body = JsonConvert.SerializeObject(new
				{
					host = String.Empty,
					login = userName,
					password,
					clientAppName = "SLC-Service-Management.IntegrationTests",
					clientAppVersion = "1.0",
					clientComputerName = Environment.MachineName,
				});

				using (var content = new StringContent(body, Encoding.UTF8, "application/json"))
				using (var response = client.PostAsync("API/v1/Json.asmx/ConnectApp", content).GetAwaiter().GetResult())
				{
					string responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
					if (!response.IsSuccessStatusCode)
					{
						throw new InvalidOperationException($"ConnectApp at {client.BaseAddress} failed with HTTP {(int)response.StatusCode}: {ApiResponse.Truncate(responseBody)}");
					}

					string connectionId = JObject.Parse(responseBody)["d"]?.ToString();
					if (String.IsNullOrWhiteSpace(connectionId))
					{
						throw new InvalidOperationException($"ConnectApp did not return a connection ID: {ApiResponse.Truncate(responseBody)}");
					}

					return new WebSessionFixture(client, connectionId);
				}
			}
			catch
			{
				client.Dispose();
				throw;
			}
		}

		public void Dispose()
		{
			try
			{
				string body = JsonConvert.SerializeObject(new { connection = ConnectionId });
				using (var content = new StringContent(body, Encoding.UTF8, "application/json"))
				{
					client.PostAsync("API/v1/Json.asmx/LogOut", content).GetAwaiter().GetResult().Dispose();
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine("Web session logout failed (suppressed): " + ex.Message);
			}
			finally
			{
				client.Dispose();
			}
		}
	}
}

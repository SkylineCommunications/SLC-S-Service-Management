namespace SLC_Service_Management.IntegrationTests
{
	using System;

	using Microsoft.VisualStudio.TestTools.UnitTesting;

	using Skyline.DataMiner.CICD.Tools.WinEncryptedKeys.Lib;
	using Skyline.DataMiner.Core.DataMinerSystem.Common;
	using Skyline.DataMiner.Net;

	/// <summary>
	/// Connects to the QAOps-provisioned DataMiner (localhost) once per test run and provisions the fixtures every test
	/// needs: the Service Management User-Defined API with a dedicated token, a DataMiner web session and a browser.
	/// </summary>
	[TestClass]
	public static class DmsTestSetup
	{
		public const string Host = "localhost";
		public const string WebBaseUrl = "https://localhost";

		private const int ConnectionTimeout = 120000;
		private const string SubscriptionSetId = "SLC-Service-Management.IntegrationTests";

		private static Connection connection;
		private static bool subscriptionsInitialized;

		public static IDms Dms { get; private set; }

		public static string UserName { get; private set; }

		public static string Password { get; private set; }

		public static UserDefinedApiFixture Api { get; private set; }

		public static WebSessionFixture WebSession { get; private set; }

		public static BrowserFixture Browser { get; private set; }

		[AssemblyInitialize]
		public static void AssemblyInitialize(TestContext context)
		{
			try
			{
				LoadCredentials();

				connection = Skyline.DataMiner.Net.ConnectionSettings.GetConnection(Host);
				connection.ClientApplicationName = "VSTEST.CONSOLE.EXE";
				connection.PollingRequestTimeout = ConnectionTimeout;
				connection.ConnectTimeoutTime = ConnectionTimeout;
				connection.AuthenticateMessageTimeout = ConnectionTimeout;
				connection.Authenticate(UserName, Password);
				connection.Subscribe();
				connection.ClearSubscriptions(SubscriptionSetId);
				subscriptionsInitialized = true;

				Dms = connection.GetDms();

				Api = UserDefinedApiFixture.Provision(connection, Dms);
				WebSession = WebSessionFixture.Connect(WebBaseUrl, UserName, Password);
				Browser = BrowserFixture.Launch();
			}
			catch
			{
				// Cleanup must never replace the real initialization failure.
				DisposeAll();
				throw;
			}
		}

		[AssemblyCleanup]
		public static void AssemblyCleanup()
		{
			DisposeAll();
		}

		private static void LoadCredentials()
		{
			if (!Keys.TryRetrieveKey("QAOpsDataMinerUser", out string userName) || String.IsNullOrWhiteSpace(userName))
			{
				throw new InvalidOperationException("Could not retrieve the encrypted key 'QAOpsDataMinerUser'. These tests only run on a QAOps-provisioned DataMiner.");
			}

			if (!Keys.TryRetrieveKey("QAOpsDataMinerPassword", out string password) || String.IsNullOrWhiteSpace(password))
			{
				throw new InvalidOperationException("Could not retrieve the encrypted key 'QAOpsDataMinerPassword'. These tests only run on a QAOps-provisioned DataMiner.");
			}

			UserName = userName;
			Password = password;
		}

		private static void DisposeAll()
		{
			SafeDispose(() => Browser?.Dispose());
			Browser = null;

			SafeDispose(() => WebSession?.Dispose());
			WebSession = null;

			SafeDispose(() => Api?.Dispose());
			Api = null;

			Dms = null;

			if (connection == null)
			{
				return;
			}

			try
			{
				if (subscriptionsInitialized)
				{
					connection.ClearSubscriptions(SubscriptionSetId);
				}

				connection.Close();
			}
			catch (Exception ex)
			{
				Console.WriteLine("DmsTestSetup cleanup error (suppressed): " + ex);
			}
			finally
			{
				connection.Dispose();
				connection = null;
				subscriptionsInitialized = false;
			}
		}

		private static void SafeDispose(Action dispose)
		{
			try
			{
				dispose();
			}
			catch (Exception ex)
			{
				Console.WriteLine("DmsTestSetup cleanup error (suppressed): " + ex);
			}
		}
	}
}

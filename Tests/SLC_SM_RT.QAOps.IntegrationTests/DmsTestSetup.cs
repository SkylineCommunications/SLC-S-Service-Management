namespace SLC_SM_RT.QAOps.IntegrationTests
{
	using System;

	using Skyline.DataMiner.CICD.Tools.WinEncryptedKeys.Lib;
	using Skyline.DataMiner.Core.DataMinerSystem.Common;
	using Skyline.DataMiner.Net;

	/// <summary>
	/// Connects to the local QAOps DataMiner once per test run.
	/// </summary>
	[TestClass]
	public static class DmsTestSetup
	{
		private const string Host = "localhost";
		private const int ConnectionTimeout = 120000;
		private const string SubscriptionSetId = "IntegrationTests";

		private static Connection connection;
		private static bool subscriptionsInitialized;

		public static IDms Dms { get; private set; }

		[AssemblyInitialize]
		public static void AssemblyInitialize(TestContext context)
		{
			try
			{
				connection = Skyline.DataMiner.Net.ConnectionSettings.GetConnection(Host);
				connection.ClientApplicationName = "VSTEST.CONSOLE.EXE";
				connection.PollingRequestTimeout = ConnectionTimeout;
				connection.ConnectTimeoutTime = ConnectionTimeout;
				connection.AuthenticateMessageTimeout = ConnectionTimeout;

				if (!Keys.TryRetrieveKey("QAOpsDataMinerUser", out string userName) || String.IsNullOrWhiteSpace(userName))
				{
					throw new InvalidOperationException("Could not retrieve 'QAOpsDataMinerUser'.");
				}

				if (!Keys.TryRetrieveKey("QAOpsDataMinerPassword", out string password) || String.IsNullOrWhiteSpace(password))
				{
					throw new InvalidOperationException("Could not retrieve 'QAOpsDataMinerPassword'.");
				}

				connection.Authenticate(userName, password);
				connection.Subscribe();
				connection.ClearSubscriptions(SubscriptionSetId);
				subscriptionsInitialized = true;

				Dms = connection.GetDms();
			}
			catch
			{
				DisposeConnection();
				throw;
			}
		}

		[AssemblyCleanup]
		public static void AssemblyCleanup()
		{
			DisposeConnection();
		}

		private static void DisposeConnection()
		{
			Dms = null;

			if (connection == null)
			{
				return;
			}

			try
			{
				// Clearing subscriptions on an unauthenticated connection throws and would mask the original failure.
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
	}
}

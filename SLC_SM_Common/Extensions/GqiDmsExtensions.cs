namespace SLC_SM_Common.Extensions
{
	using System;
	using System.Diagnostics;
	using Skyline.DataMiner.Analytics.GenericInterface;
	using Skyline.DataMiner.Net;
	using Skyline.DataMiner.Net.Messages;

	public static class GqiDmsExtensions
	{
		public static void GenerateInformationMessage(this GQIDMS dms, string text)
		{
			dms.SendMessage(new GenerateAlarmMessage(GenerateAlarmMessage.AlarmSeverity.Information, text) { Status = GenerateAlarmMessage.AlarmStatus.Cleared });
		}

		public static void GenerateInformationMessage(this IConnection connection, string text)
		{
			connection.HandleMessage(new GenerateAlarmMessage(GenerateAlarmMessage.AlarmSeverity.Information, text) { Status = GenerateAlarmMessage.AlarmStatus.Cleared });
		}

		public static T PerformanceLogger<T>(this IGQILogger logger, string methodName, Func<T> func)
		{
			if (func == null)
			{
				throw new ArgumentNullException(nameof(func));
			}

			var stopwatch = Stopwatch.StartNew();

			try
			{
				return func();
			}
			finally
			{
				stopwatch.Stop();
				logger.Information($"[{methodName}] executed in {stopwatch.ElapsedMilliseconds} ms");
			}
		}

		public static void PerformanceLogger(this IGQILogger logger, string methodName, Action action)
		{
			if (action == null)
			{
				throw new ArgumentNullException(nameof(action));
			}

			var stopwatch = Stopwatch.StartNew();

			try
			{
				action();
			}
			finally
			{
				stopwatch.Stop();
				logger.Information($"[{methodName}] executed in {stopwatch.ElapsedMilliseconds} ms");
			}
		}
	}
}
namespace Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using Newtonsoft.Json;
	using Skyline.DataMiner.Analytics.GenericInterface;
	using Skyline.DataMiner.Automation;

	public static class ScriptExtensions
	{
		public static T ReadScriptParamFromApp<T>(this IEngine engine, string name)
		{
			return ReadScriptParamsFromApp<T>(engine, name).FirstOrDefault();
		}

		public static string ReadScriptParamFromApp(this IEngine engine, string name)
		{
			return ReadScriptParamsFromApp<string>(engine, name).FirstOrDefault();
		}

		public static ICollection<T> ReadScriptParamsFromApp<T>(this IEngine engine, string name)
		{
			string param = engine.GetScriptParam(name)?.Value;
			if (param == null)
			{
				throw new ArgumentException($"No script input parameter provided with name '{name}'");
			}

			if (param.StartsWith("[") && param.EndsWith("]"))
			{
				return JsonConvert.DeserializeObject<ICollection<T>>(param);
			}

			object value;
			if (typeof(T) == typeof(Guid))
			{
				value = Guid.Parse(param);
			}
			else if (typeof(T).IsEnum)
			{
				value = Enum.Parse(typeof(T), param, ignoreCase: true);
			}
			else
			{
				value = Convert.ChangeType(param, typeof(T));
			}

			return new List<T> { (T)value };
		}

		public static ICollection<string> ReadScriptParamsFromApp(this IEngine engine, string name)
		{
			return ReadScriptParamsFromApp<string>(engine, name);
		}

		public static T PerformanceLogger<T>(this IEngine engine, string methodName, Func<T> func)
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
				engine.GenerateInformation($"[{methodName}] executed in {stopwatch.ElapsedMilliseconds} ms");
			}
		}

		public static void PerformanceLogger(this IEngine engine, string methodName, Action action)
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
				engine.GenerateInformation($"[{methodName}] executed in {stopwatch.ElapsedMilliseconds} ms");
			}
		}
	}
}
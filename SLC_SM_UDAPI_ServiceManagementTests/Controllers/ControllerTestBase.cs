namespace SLC_SM_UDAPI_ServiceManagement.Tests.Controllers
{
	using System.Collections.Generic;
	using System.Reflection;
	using Newtonsoft.Json.Linq;
	using Skyline.DataMiner.Utils.UserDefinedApiToolkit;

	/// <summary>
	/// Common helpers for all UDAPI controller tests: API context wiring and result inspection.
	/// </summary>
	public abstract class ControllerTestBase
	{
		/// <summary>Attaches the context the UDAPI pipeline normally supplies; its setters are internal to the toolkit.</summary>
		protected static void AttachContext(ControllerBase controller)
		{
			var context = new ApiContext();
			SetProperty(context, nameof(ApiContext.InputConverters), new List<IInputConverter> { new NewtonsoftConverter() });
			SetProperty(context, nameof(ApiContext.OutputConverters), new List<IOutputConverter> { new NewtonsoftConverter() });
			SetProperty(controller, nameof(ControllerBase.ApiContext), context);
		}

		protected static int StatusCode(IApiResult result)
		{
			Assert.IsInstanceOfType(result, typeof(StatusCodeResult));
			return ((StatusCodeResult)result).StatusCode;
		}

		protected static string ErrorDetail(IApiResult result)
		{
			var value = result.GetType().GetProperty("Value").GetValue(result);
			return (string)JObject.FromObject(value)["error"][0]["detail"];
		}

		/// <summary>Sets the read-only SDM <c>Status</c> property through its backing field.</summary>
		protected static void SetStatus(object sdmObject, object status)
		{
			var field = sdmObject.GetType().GetField("<Status>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
			Assert.IsNotNull(field, "Status backing field not found; package layout changed.");
			field.SetValue(sdmObject, status);
		}

		private static void SetProperty(object target, string name, object value)
		{
			var property = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			Assert.IsNotNull(property, $"Property '{name}' not found; toolkit layout changed.");
			property.SetValue(target, value);
		}
	}
}

namespace SLC_SM_UDAPI_ServiceManagement.Controllers
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Microsoft.Extensions.Logging;
	using Newtonsoft.Json.Linq;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using Skyline.DataMiner.SDM;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;
	using Skyline.DataMiner.Utils.UserDefinedApiToolkit;
	using SLC_SM_UDAPI_ServiceManagement.Exceptions;
	using SLC_SM_UDAPI_ServiceManagement.Helpers.Inventory;
	using SLC_SM_UDAPI_ServiceManagement.Models.Inventory;

	public partial class ServiceInventoryController
	{
		/// <summary>
		/// Lists the related service orders for a service.
		/// </summary>
		[HttpGet("{id}/relatedOrder")]
		public IApiResult ListRelatedServiceOrders([FromRoute] string id)
		{
			try
			{
				var service = FindService(id);
				if (service == null)
				{
					return NotFound(CreateError("Not found", $"Service '{id?.Trim()}' does not exist.", 404));
				}

				var serviceItems = serviceManagementApiHelper.ServiceOrder.ServiceOrderItems
					.Read(ServiceOrderItemExposers.ServiceInfo.ServiceId.Equal(new SdmObjectReference<Service>(service.Identifier)))
					.Where(item => item != null && !String.IsNullOrWhiteSpace(item.Identifier))
					.ToDictionary(item => item.Identifier, StringComparer.OrdinalIgnoreCase);
				if (serviceItems.Count == 0)
				{
					return Ok(new object[0]);
				}

				var orders = serviceManagementApiHelper.ServiceOrder.ServiceOrders.Read(new TRUEFilterElement<ServiceOrder>());
				var related = orders
					.SelectMany(order => (order.OrderItems ?? new List<ServiceOrderEntry>())
						.Where(entry => entry?.ServiceOrderItemId != null
							&& !String.IsNullOrWhiteSpace(entry.ServiceOrderItemId.Identifier)
							&& serviceItems.ContainsKey(entry.ServiceOrderItemId.Identifier))
						.Select(entry =>
						{
							var item = serviceItems[entry.ServiceOrderItemId.Identifier];
							return new
							{
								OrderInstanceId = order.Identifier ?? String.Empty,
								OrderId = order.OrderId ?? String.Empty,
								OrderName = order.Name ?? String.Empty,
								ServiceOrderItemId = item.Identifier ?? String.Empty,
								Action = item.Action ?? String.Empty,
								State = item.Status.GetDescription(),
							};
						}))
					.ToList();

				return Ok(related);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "ListRelatedServiceOrders failed for service {ServiceId}: {Message}", id, ex.Message);
				return InternalServerError(CreateError("Internal server error", "An unexpected error occurred while retrieving the related service orders.", 500));
			}
		}

		/// <summary>
		/// Lists the flat configuration records used by the service configuration editor.
		/// </summary>
		[HttpGet("{id}/configuration")]
		public IApiResult GetServiceConfiguration([FromRoute] string id)
		{
			try
			{
				var service = FindService(id);
				if (service == null)
				{
					return NotFound(CreateError("Not found", $"Service '{id?.Trim()}' does not exist.", 404));
				}

				return Ok(new ServiceConfigurationReader(serviceManagementApiHelper).ReadService(service));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "GetServiceConfiguration failed for service {ServiceId}: {Message}", id, ex.Message);
				return InternalServerError(CreateError("Internal server error", "An unexpected error occurred while retrieving the service configuration.", 500));
			}
		}

		/// <summary>
		/// Saves or deletes a service configuration version.
		/// </summary>
		[HttpPut("{id}/configuration")]
		public IApiResult SaveServiceConfiguration([FromRoute] string id, [FromBody] JObject body)
		{
			try
			{
				var service = FindService(id);
				if (service == null)
				{
					return NotFound(CreateError("Not found", $"Service '{id?.Trim()}' does not exist.", 404));
				}

				if (body == null)
				{
					return BadRequest(CreateError("Invalid request", "A request body is required.", 400));
				}

				ServiceConfigurationRequest request;
				try
				{
					request = body.ToObject<ServiceConfigurationRequest>();
				}
				catch (Newtonsoft.Json.JsonException)
				{
					return BadRequest(CreateError("Invalid request", "The request body contains invalid values.", 400));
				}

				new ServiceConfigurationWriter(serviceManagementApiHelper).Save(service, request);
				var saved = serviceManagementApiHelper.ServiceInventory.Services
					.Read(ServiceExposers.Identifier.Equal(service.Identifier))
					.FirstOrDefault() ?? service;
				return Ok(new ServiceConfigurationReader(serviceManagementApiHelper).ReadService(saved));
			}
			catch (InvalidConfigurationException ex)
			{
				_logger.LogWarning(ex, "Invalid SaveServiceConfiguration request: {Message}", ex.Message);
				return BadRequest(CreateError("Invalid request", ex.Message, 400));
			}
			catch (Exception ex)
			{
				string errorReference = Guid.NewGuid().ToString("N");
				_logger.LogError(ex, "SaveServiceConfiguration failed for service {ServiceId}. Error reference: {ErrorReference}", id, errorReference);
				return InternalServerError(CreateError(
					"Internal server error",
					$"An unexpected error occurred while saving the service configuration. Reference: {errorReference}",
					500));
			}
		}

		/// <summary>
		/// Duplicates a service, its configuration versions and service items.
		/// </summary>
		[HttpPost("{id}/duplicate")]
		public IApiResult DuplicateService([FromRoute] string id)
		{
			try
			{
				var source = FindService(id);
				if (source == null)
				{
					return NotFound(CreateError("Not found", $"Service '{id?.Trim()}' does not exist.", 404));
				}

				var services = serviceManagementApiHelper.ServiceInventory.Services
					.Read(new TRUEFilterElement<Service>())
					.ToList();
				var duplicate = new ServiceDuplicator(serviceManagementApiHelper)
					.Duplicate(source, services.Select(service => service.ServiceID));
				return Created(duplicate);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "DuplicateService failed for service {ServiceId}: {Message}", id, ex.Message);
				return InternalServerError(CreateError("Internal server error", "An unexpected error occurred while duplicating the service.", 500));
			}
		}

		/// <summary>
		/// Lists the scripts configured on a service.
		/// </summary>
		[HttpGet("{id}/script")]
		public IApiResult ListServiceScripts([FromRoute] string id)
		{
			try
			{
				var service = FindService(id);
				if (service == null)
				{
					return NotFound(CreateError("Not found", $"Service '{id?.Trim()}' does not exist.", 404));
				}

				return Ok(service.ServiceScripts ?? new List<ServiceScript>());
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "ListServiceScripts failed for service {ServiceId}: {Message}", id, ex.Message);
				return InternalServerError(CreateError("Internal server error", "An unexpected error occurred while retrieving service scripts.", 500));
			}
		}

		/// <summary>
		/// Adds a script to a service.
		/// </summary>
		[HttpPost("{id}/script")]
		public IApiResult AddServiceScript([FromRoute] string id, [FromBody] JObject request)
		{
			try
			{
				var service = FindService(id);
				if (service == null)
				{
					return NotFound(CreateError("Not found", $"Service '{id?.Trim()}' does not exist.", 404));
				}

				if (request == null)
				{
					return BadRequest(CreateError("Invalid request", "A request body is required.", 400));
				}

				string name = ReadRequestString(request, "name")?.Trim();
				string description = ReadRequestString(request, "description")?.Trim();
				if (String.IsNullOrWhiteSpace(name) || String.IsNullOrWhiteSpace(description))
				{
					return BadRequest(CreateError("Invalid request", "'name' and 'description' are required.", 400));
				}

				var scripts = service.ServiceScripts ?? new List<ServiceScript>();
				if (scripts.Any(script => String.Equals(script.Description, description, StringComparison.OrdinalIgnoreCase)))
				{
					return Conflict(CreateError("Conflict", "A service script with this description already exists.", 409));
				}

				var added = new ServiceScript
				{
					Name = name,
					Description = description,
					InputParameters = ReadRequestString(request, "inputParameters"),
				};
				scripts.Add(added);
				service.ServiceScripts = scripts;
				serviceManagementApiHelper.ServiceInventory.Services.Update(service);
				return Created(added);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "AddServiceScript failed for service {ServiceId}: {Message}", id, ex.Message);
				return InternalServerError(CreateError("Internal server error", "An unexpected error occurred while adding the service script.", 500));
			}
		}

		/// <summary>
		/// Updates the script name and description.
		/// </summary>
		[HttpPatch("{id}/script")]
		public IApiResult UpdateServiceScript([FromRoute] string id, [FromBody] JObject request)
		{
			try
			{
				var service = FindService(id);
				if (service == null)
				{
					return NotFound(CreateError("Not found", $"Service '{id?.Trim()}' does not exist.", 404));
				}

				if (request == null)
				{
					return BadRequest(CreateError("Invalid request", "A request body is required.", 400));
				}

				var scripts = service.ServiceScripts ?? new List<ServiceScript>();
				string originalDescription = ReadRequestString(request, "originalDescription");
				string name = ReadRequestString(request, "name")?.Trim();
				string description = ReadRequestString(request, "description")?.Trim();
				if (String.IsNullOrWhiteSpace(originalDescription) || String.IsNullOrWhiteSpace(name) || String.IsNullOrWhiteSpace(description))
				{
					return BadRequest(CreateError("Invalid request", "'originalDescription', 'name' and 'description' are required.", 400));
				}

				int index = scripts.FindIndex(script => String.Equals(script.Description, originalDescription, StringComparison.OrdinalIgnoreCase));
				if (index < 0)
				{
					return NotFound(CreateError("Not found", "The service script to update was not found.", 404));
				}

				if (scripts.Where((script, scriptIndex) => scriptIndex != index)
					.Any(script => String.Equals(script.Description, description, StringComparison.OrdinalIgnoreCase)))
				{
					return Conflict(CreateError("Conflict", "A service script with this description already exists.", 409));
				}

				scripts[index].Name = name;
				scripts[index].Description = description;
				if (Has(request, "inputParameters"))
				{
					scripts[index].InputParameters = ReadRequestString(request, "inputParameters");
				}

				service.ServiceScripts = scripts;
				serviceManagementApiHelper.ServiceInventory.Services.Update(service);
				return Ok(scripts[index]);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "UpdateServiceScript failed for service {ServiceId}: {Message}", id, ex.Message);
				return InternalServerError(CreateError("Internal server error", "An unexpected error occurred while updating the service script.", 500));
			}
		}

		/// <summary>
		/// Removes a script from a service.
		/// </summary>
		[HttpDelete("{id}/script")]
		public IApiResult DeleteServiceScript([FromRoute] string id, [FromBody] JObject request)
		{
			try
			{
				var service = FindService(id);
				if (service == null)
				{
					return NotFound(CreateError("Not found", $"Service '{id?.Trim()}' does not exist.", 404));
				}

				string description = ReadRequestString(request, "description")?.Trim();
				if (String.IsNullOrWhiteSpace(description))
				{
					return BadRequest(CreateError("Invalid request", "'description' is required.", 400));
				}

				var scripts = service.ServiceScripts ?? new List<ServiceScript>();
				if (scripts.RemoveAll(script => String.Equals(script.Description, description, StringComparison.OrdinalIgnoreCase)) == 0)
				{
					return NotFound(CreateError("Not found", "The service script to remove was not found.", 404));
				}

				service.ServiceScripts = scripts;
				serviceManagementApiHelper.ServiceInventory.Services.Update(service);
				return NoContent();
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "DeleteServiceScript failed for service {ServiceId}: {Message}", id, ex.Message);
				return InternalServerError(CreateError("Internal server error", "An unexpected error occurred while deleting the service script.", 500));
			}
		}

		private static string ReadRequestString(JObject request, string property)
		{
			return request?.GetValue(property, StringComparison.OrdinalIgnoreCase)?.Type == JTokenType.String
				? (string)request.GetValue(property, StringComparison.OrdinalIgnoreCase)
				: null;
		}
	}
}

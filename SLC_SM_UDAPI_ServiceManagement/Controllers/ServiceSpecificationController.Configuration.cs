namespace SLC_SM_UDAPI_ServiceManagement.Controllers
{
	using System;
	using Microsoft.Extensions.Logging;
	using Newtonsoft.Json;
	using Newtonsoft.Json.Linq;
	using Skyline.DataMiner.Utils.UserDefinedApiToolkit;
	using SLC_SM_UDAPI_ServiceManagement.Exceptions;
	using SLC_SM_UDAPI_ServiceManagement.Helpers.Specification;
	using SLC_SM_UDAPI_ServiceManagement.Models.Specification;

	public partial class ServiceSpecificationController
	{
		/// <summary>
		/// Returns the flat configuration records for a service specification.
		/// </summary>
		/// <param name="id">Specification identifier (UUID).</param>
		/// <returns>The specification configuration records.</returns>
		[HttpGet("{id}/configuration")]
		public IApiResult GetServiceSpecificationConfiguration(string id)
		{
			_logger.LogInformation("GetServiceSpecificationConfiguration called with id={Id}", id);

			try
			{
				if (!TryParseId(id, out var specId))
				{
					return BadRequest(CreateError("Invalid request", "'id' must be a valid UUID.", 400));
				}

				var specification = ReadSpecification(specId);
				if (specification == null)
				{
					return NotFound(CreateError("Not found", $"Service specification '{specId}' does not exist.", 404));
				}

				return Ok(new SpecificationConfigurationReader(serviceManagementApiHelper).Read(specification));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "GetServiceSpecificationConfiguration failed: {Message}", ex.Message);
				return InternalServerError(CreateError("Internal server error", "An unexpected error occurred while retrieving the service specification configuration.", 500));
			}
		}

		/// <summary>
		/// Replaces the configuration records of a service specification.
		/// </summary>
		/// <param name="id">Specification identifier (UUID).</param>
		/// <param name="body">The configuration payload.</param>
		/// <returns>The saved specification configuration records.</returns>
		[HttpPut("{id}/configuration")]
		[Consumes("application/json")]
		public IApiResult SaveServiceSpecificationConfiguration(string id, [FromBody] JObject body)
		{
			_logger.LogInformation("SaveServiceSpecificationConfiguration called with id={Id}", id);

			try
			{
				if (!TryParseId(id, out var specId))
				{
					return BadRequest(CreateError("Invalid request", "'id' must be a valid UUID.", 400));
				}

				if (body == null)
				{
					return BadRequest(CreateError("Invalid request", "A request body is required.", 400));
				}

				SpecificationConfigurationRequest request;
				try
				{
					request = body.ToObject<SpecificationConfigurationRequest>();
				}
				catch (JsonException ex)
				{
					return BadRequest(CreateError("Invalid request", $"The request body is invalid: {ex.Message}", 400));
				}

				var specification = ReadSpecification(specId);
				if (specification == null)
				{
					return NotFound(CreateError("Not found", $"Service specification '{specId}' does not exist.", 404));
				}

				// The configuration version name is the specification name, so a rename must stay unique.
				string newName = request?.Name?.Trim();
				if (!String.IsNullOrEmpty(newName) && NameExists(newName, specification.Identifier))
				{
					return Conflict(CreateError("Conflict", $"A service specification with name '{newName}' already exists.", 409));
				}

				new SpecificationConfigurationWriter(serviceManagementApiHelper).Save(specification, request);

				var saved = ReadSpecification(specId) ?? specification;
				return Ok(new SpecificationConfigurationReader(serviceManagementApiHelper).Read(saved));
			}
			catch (InvalidConfigurationException ex)
			{
				return BadRequest(CreateError("Invalid request", ex.Message, 400));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "SaveServiceSpecificationConfiguration failed: {Message}", ex.Message);
				return InternalServerError(CreateError("Internal server error", "An unexpected error occurred while saving the service specification configuration.", 500));
			}
		}
	}
}

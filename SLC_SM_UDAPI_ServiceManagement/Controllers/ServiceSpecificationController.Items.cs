namespace SLC_SM_UDAPI_ServiceManagement.Controllers
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Library.Dom;
	using Microsoft.Extensions.Logging;
	using Newtonsoft.Json;
	using Newtonsoft.Json.Linq;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using Skyline.DataMiner.Utils.UserDefinedApiToolkit;
	using SLC_SM_UDAPI_ServiceManagement.Exceptions;
	using SLC_SM_UDAPI_ServiceManagement.Models.Inventory;
	using SLC_SM_UDAPI_ServiceManagement.Models.Specification;

	public partial class ServiceSpecificationController
	{
		/// <summary>
		/// Adds a service item to a specification.
		/// </summary>
		/// <param name="id">Specification identifier (UUID).</param>
		/// <param name="request">The service item to add.</param>
		/// <returns>The created service item.</returns>
		[HttpPost("{id}/item")]
		public IApiResult AddServiceSpecificationItem(string id, [FromBody] ServiceRequestItem request)
		{
			try
			{
				if (!TryParseId(id, out var specificationId))
				{
					return BadRequest(CreateError("Invalid request", "'id' must be a valid UUID.", 400));
				}

				if (request == null)
				{
					return BadRequest(CreateError("Invalid request", "A request body is required.", 400));
				}

				var specification = ReadSpecification(specificationId);
				if (specification == null)
				{
					return NotFound(CreateError("Not found", $"Service specification '{specificationId}' does not exist.", 404));
				}

				string label = RequireServiceItemLabel(request.Label);
				var items = (specification.ServiceItems ?? new List<ServiceItem>())
					.Where(candidate => candidate != null && !IsServiceItemPlaceholder(candidate))
					.ToList();
				long nextId = items.Where(candidate => candidate.ServiceItemID.HasValue)
					.Select(candidate => candidate.ServiceItemID.Value)
					.DefaultIfEmpty(-1)
					.Max() + 1;
				var item = new ServiceItem
				{
					ServiceItemID = nextId,
					Label = label,
					Type = ParseServiceItemType(request.Type),
					DefinitionReference = request.DefinitionReference ?? String.Empty,
					ImplementationReference = request.ImplementationReference ?? String.Empty,
					Script = request.Script ?? String.Empty,
					Icon = request.Icon ?? String.Empty,
				};
				items.Add(item);

				string validationError = ValidateReferences(new ServiceSpecificationRequest
				{
					ServiceItems = items,
					ServiceItemRelationships = specification.ServiceItemsRelationships,
				});
				if (validationError != null)
				{
					return BadRequest(CreateError("Invalid request", validationError, 400));
				}

				specification.ServiceItems = items;
				serviceManagementApiHelper.UpdateSpecification(specification);
				return Created(item);
			}
			catch (InvalidQueryException ex)
			{
				return BadRequest(CreateError("Invalid request", ex.Message, 400));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "AddServiceSpecificationItem failed: {Message}", ex.Message);
				return InternalServerError(CreateError("Internal server error", "An unexpected error occurred while adding the service specification item.", 500));
			}
		}

		/// <summary>
		/// Updates a service item in a specification.
		/// </summary>
		/// <param name="id">Specification identifier (UUID).</param>
		/// <param name="itemId">Service item identifier.</param>
		/// <param name="patch">The properties to update.</param>
		/// <returns>The updated service item.</returns>
		[HttpPatch("{id}/item/{itemId}")]
		public IApiResult UpdateServiceSpecificationItem(string id, string itemId, [FromBody] JObject patch)
		{
			try
			{
				if (!TryParseId(id, out var specificationId))
				{
					return BadRequest(CreateError("Invalid request", "'id' must be a valid UUID.", 400));
				}

				if (!Int64.TryParse(itemId?.Trim(), out var serviceItemId))
				{
					return BadRequest(CreateError("Invalid request", "'itemId' must be a service item ID.", 400));
				}

				if (patch == null)
				{
					return BadRequest(CreateError("Invalid request", "A request body is required.", 400));
				}

				var specification = ReadSpecification(specificationId);
				if (specification == null)
				{
					return NotFound(CreateError("Not found", $"Service specification '{specificationId}' does not exist.", 404));
				}

				var items = specification.ServiceItems ?? new List<ServiceItem>();
				var item = items.FirstOrDefault(candidate => candidate?.ServiceItemID == serviceItemId);
				if (item == null)
				{
					return NotFound(CreateError("Not found", $"Service item '{serviceItemId}' does not exist in service specification '{specificationId}'.", 404));
				}

				var request = patch.ToObject<ServiceRequestItem>();
				if (request == null)
				{
					return BadRequest(CreateError("Invalid request", "The request body must be a JSON object.", 400));
				}

				if (Has(patch, "label"))
				{
					item.Label = RequireServiceItemLabel(request.Label);
				}

				if (Has(patch, "type"))
				{
					item.Type = ParseServiceItemType(request.Type);
				}

				if (Has(patch, "definitionReference"))
				{
					item.DefinitionReference = request.DefinitionReference ?? String.Empty;
				}

				if (Has(patch, "implementationReference"))
				{
					item.ImplementationReference = request.ImplementationReference ?? String.Empty;
				}

				if (Has(patch, "script"))
				{
					item.Script = request.Script ?? String.Empty;
				}

				if (Has(patch, "icon"))
				{
					item.Icon = request.Icon ?? String.Empty;
				}

				string validationError = ValidateReferences(new ServiceSpecificationRequest
				{
					ServiceItems = items,
					ServiceItemRelationships = specification.ServiceItemsRelationships,
				});
				if (validationError != null)
				{
					return BadRequest(CreateError("Invalid request", validationError, 400));
				}

				specification.ServiceItems = items;
				serviceManagementApiHelper.UpdateSpecification(specification);
				return Ok(item);
			}
			catch (InvalidQueryException ex)
			{
				return BadRequest(CreateError("Invalid request", ex.Message, 400));
			}
			catch (JsonException)
			{
				return BadRequest(CreateError("Invalid request", "The request body contains invalid values.", 400));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "UpdateServiceSpecificationItem failed: {Message}", ex.Message);
				return InternalServerError(CreateError("Internal server error", "An unexpected error occurred while updating the service specification item.", 500));
			}
		}

		/// <summary>
		/// Removes a service item from a specification.
		/// </summary>
		/// <param name="id">Specification identifier (UUID).</param>
		/// <param name="itemId">Service item identifier.</param>
		/// <returns>An empty successful response.</returns>
		[HttpDelete("{id}/item/{itemId}")]
		public IApiResult DeleteServiceSpecificationItem(string id, string itemId)
		{
			try
			{
				if (!TryParseId(id, out var specificationId))
				{
					return BadRequest(CreateError("Invalid request", "'id' must be a valid UUID.", 400));
				}

				if (!Int64.TryParse(itemId?.Trim(), out var serviceItemId))
				{
					return BadRequest(CreateError("Invalid request", "'itemId' must be a service item ID.", 400));
				}

				var specification = ReadSpecification(specificationId);
				if (specification == null)
				{
					return NotFound(CreateError("Not found", $"Service specification '{specificationId}' does not exist.", 404));
				}

				var items = specification.ServiceItems ?? new List<ServiceItem>();
				var item = items.FirstOrDefault(candidate => candidate?.ServiceItemID == serviceItemId);
				if (item == null)
				{
					return NotFound(CreateError("Not found", $"Service item '{serviceItemId}' does not exist in service specification '{specificationId}'.", 404));
				}

				items.Remove(item);
				if (items.Count == 0)
				{
					items.Add(new ServiceItem());
				}

				string removedId = serviceItemId.ToString();
				specification.ServiceItems = items;
				specification.ServiceItemsRelationships = (specification.ServiceItemsRelationships ?? new List<ServiceItemRelationship>())
					.Where(relationship => relationship != null
						&& !String.Equals(relationship.ParentServiceItem, removedId, StringComparison.OrdinalIgnoreCase)
						&& !String.Equals(relationship.ChildServiceItem, removedId, StringComparison.OrdinalIgnoreCase))
					.ToList();
				serviceManagementApiHelper.UpdateSpecification(specification);
				return NoContent();
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "DeleteServiceSpecificationItem failed: {Message}", ex.Message);
				return InternalServerError(CreateError("Internal server error", "An unexpected error occurred while deleting the service specification item.", 500));
			}
		}
	}
}

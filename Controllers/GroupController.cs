using HRMSAPI.Data;
using HRMSAPI.DTO;
using HRMSAPI.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HRMSAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    // [Authorize]
    public class GroupController : ControllerBase
    {
        private readonly IGroupService _groupService;
        private readonly ILogger<GroupController> _logger;

        public GroupController(IGroupService groupService, ILogger<GroupController> logger)
        {
            _groupService = groupService ?? throw new ArgumentNullException(nameof(groupService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

       
        [HttpPost("upsert")]
        public async Task<IActionResult> UpsertGroup([FromBody] GroupUpsertDto groupDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var result = await _groupService.UpsertGroupAsync(groupDto);
                return StatusCode((int)result.Code,new { 
                    Status = result.Status,
                    Message = result.Message
                });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Validation error in UpsertGroup");
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in UpsertGroup");
                return StatusCode(500, new { success = false, message = "An error occurred while processing the request" });
            }
        }

        /// <summary>
        /// Delete a group (soft delete)
        /// </summary>
        /// <param name="id">Group ID to delete</param>
        /// <returns>Success status</returns>
        [HttpGet("DeleteGroup")]
        public async Task<IActionResult> DeleteGroup([FromQuery]int id)
        {
            try
            {
                if (id <= 0)
                {
                    return BadRequest(new { success = false, message = "Invalid group ID" });
                }

                var result = await _groupService.DeleteGroupAsync(id);
                return StatusCode((int)result.Code, new
                {
                    Status = result.Status,
                    Message = result.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in DeleteGroup for ID: {Id}", id);
                return StatusCode(500, new { success = false, message = "An error occurred while processing the request" });
            }
        }

        /// <summary>
        /// Get all active groups
        /// </summary>
        /// <returns>List of all groups</returns>
        [HttpGet("all")]
        public async Task<IActionResult> GetAllGroups()
        {
            try
            {
                var groups = await _groupService.GetAllGroupsAsync();
                return StatusCode((int)groups.Code, new
                {
                    Status = groups.Status,
                    Message = groups.Message,
                    Data = groups.Data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in GetAllGroups");
                return StatusCode(500, new { success = false, message = "An error occurred while processing the request" });
            }
        }

        /// <summary>
        /// Get a specific group by ID
        /// </summary>
        /// <param name="id">Group ID</param>
        /// <returns>Group details</returns>
        //[HttpGet("{id}")]
        //public async Task<IActionResult> GetGroupById(int id)
        //{
        //    try
        //    {
        //        if (id <= 0)
        //        {
        //            return BadRequest(new { success = false, message = "Invalid group ID" });
        //        }

        //        var group = await _groupService.GetGroupByIdAsync(id);
        //        if (group != null)
        //        {
        //            return Ok(new { success = true, data = group, message = "Group retrieved successfully" });
        //        }
        //        else
        //        {
        //            return NotFound(new { success = false, message = "Group not found" });
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error occurred in GetGroupById for ID: {Id}", id);
        //        return StatusCode(500, new { success = false, message = "An error occurred while processing the request" });
        //    }
        //}
    }
}

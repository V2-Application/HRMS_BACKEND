using DocumentFormat.OpenXml.Office2010.Excel;
using HRMSAPI.Data;
using HRMSAPI.DTO;
using HRMSAPI.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Roomsy.DTOS.GenericsResponses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace HRMSAPI.Implementation
{
    public class GroupService : IGroupService
    {
        private readonly HRMSContext _context;
        private readonly ILogger<GroupService> _logger;

        public GroupService(HRMSContext context, ILogger<GroupService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ExecuteAndReponse> UpsertGroupAsync(GroupUpsertDto groupDto)
        {
            if (groupDto == null)
            {
                throw new ArgumentNullException(nameof(groupDto), "Group data is required.");
            }

            try
            {
                GroupMaster group;

                if (groupDto.Id>0)
                {
                    // Update existing group
                    group = await _context.GroupMasters.AsQueryable().FirstOrDefaultAsync(row=>row.Id==groupDto.Id);
                    if (group == null)
                    {
                        throw new ArgumentException($"Group with ID {groupDto.Id} not found.");
                    }
                    // Check if group name already exists
                    if (await _context.GroupMasters.AsNoTracking().AsQueryable().AnyAsync(g => g.GroupName == group.GroupName && !g.IsDeleted.Value && g.Id!=groupDto.Id))
                    {
                        throw new ArgumentException($"Group with name '{group.GroupName}' already exists.");
                    }
                    // Update properties
                    if (!string.IsNullOrEmpty(groupDto.GroupName))
                        group.GroupName = groupDto.GroupName;
                    
                    group.UpdatedBy = "System";
                    group.UpdatedOn = DateTime.UtcNow;

                    _logger.LogInformation("Updating group with ID: {Id}", groupDto.Id);
                }
                else
                {
                    // Create new group
                    group = new GroupMaster
                    {
                        GroupName = groupDto.GroupName ?? throw new ArgumentException("GroupName is required for new groups."),
                        CreatedBy = "System",
                        CreatedOn = DateTime.UtcNow,
                    };

                    // Check if group name already exists
                    if (await _context.GroupMasters.AsNoTracking().AsQueryable().AnyAsync(g => g.GroupName == group.GroupName && !g.IsDeleted.Value))
                    {
                        throw new ArgumentException($"Group with name '{group.GroupName}' already exists.");
                    }

                    await _context.GroupMasters.AddAsync(group);
                    _logger.LogInformation("Creating new group with name: {GroupName}", group.GroupName);
                }

                await _context.SaveChangesAsync();
                //_logger.LogInformation("Group {Action} successfully with ID: {Id}", group.Id ? "updated" : "created", group.Id);

                return new ExecuteAndReponse { 
                    Status = true,
                    Message = "Upserted Successfully",
                    Code = HttpStatusCode.OK,
                };
            }
            catch (Exception ex)
            {
                //_logger.LogError(ex, "Error occurred while {Action} group", id.HasValue ? "updating" : "creating");
                return new ExecuteAndReponse
                {
                    Status = false,
                    Message = ex.Message,
                    Code = HttpStatusCode.BadRequest,
                };
            }
        }

        public async Task<ExecuteAndReponse> DeleteGroupAsync(int id)
        {
            if (id <= 0)
            {
                throw new ArgumentException("Invalid group ID.");
            }

            try
            {
                var group = await _context.GroupMasters.AsQueryable().FirstOrDefaultAsync(row=>row.Id==id);
                if (group == null)
                {
                    _logger.LogWarning("Group with ID {Id} not found for deletion", id);
                    return new ExecuteAndReponse { 
                        Status = false,
                        Message = $"Group not found for deletion",
                        Code = HttpStatusCode.BadRequest,
                    };
                }

                // Soft delete - mark as deleted instead of removing from database
                group.IsDeleted = true;
                group.IsActive = false;
                group.UpdatedBy = "System";
                group.UpdatedOn = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                _logger.LogInformation("Group with ID {Id} deleted successfully", id);

                return new ExecuteAndReponse { 
                    Status = true,
                    Message = "Group Deleted Successfully",
                    Code = HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while deleting group with ID: {Id}", id);
                return new ExecuteAndReponse
                {
                    Status = false,
                    Message =ex.Message,
                    Code = HttpStatusCode.BadRequest,
                }; ;
            }
        }

        public async Task<FetchAndResponse> GetAllGroupsAsync()
        {
            try
            {
                var groups = await _context.GroupMasters.AsNoTracking().AsQueryable()
                    .Where(g => !g.IsDeleted.Value)
                    .OrderBy(g => g.GroupName)
                    .ToListAsync();
                if (groups == null)
                {
                    //_logger.LogWarning("Group with ID {Id} not found for deletion", id);
                    return new FetchAndResponse
                    {
                        Status = false,
                        Message = $"No Data Found",
                        Code = HttpStatusCode.BadRequest,
                        Data = null
                    };
                }
                _logger.LogInformation("Retrieved {Count} groups", groups.Count);
                return new FetchAndResponse { 
                    Status= true,
                    Message = "Fetched Sucessfully",
                    Data = groups,
                    Code = HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving all groups");
                return new FetchAndResponse
                {
                    Status = false,
                    Message = ex.Message,
                    Code = HttpStatusCode.BadRequest,
                    Data = null
                };
            }
        }

        //public async Task<GroupMaster> GetGroupByIdAsync(int id)
        //{
        //    if (id <= 0)
        //    {
        //        throw new ArgumentException("Invalid group ID.");
        //    }

        //    try
        //    {
        //        var group = await _context.GroupMasters
        //            .FirstOrDefaultAsync(g => g.Id == id && !g.IsDeleted.Value);

        //        if (group == null)
        //        {
        //            _logger.LogWarning("Group with ID {Id} not found", id);
        //            return null;
        //        }

        //        _logger.LogInformation("Retrieved group with ID: {Id}", id);
        //        return group;
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error occurred while retrieving group with ID: {Id}", id);
        //        throw;
        //    }
        //}
    }
}

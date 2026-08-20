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
    public class EmployeeRoleService : IEmployeeRoleService
    {
        private readonly HRMSContext _context;
        private readonly ILogger<EmployeeRoleService> _logger;

        public EmployeeRoleService(HRMSContext context, ILogger<EmployeeRoleService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ExecuteAndReponse> BulkUpsertEmployeeRolesAsync(EmployeeRoleBulkUpsertDto request)
        {
            if (request?.EmployeeRoles == null || !request.EmployeeRoles.Any())
            {
                return new ExecuteAndReponse
                {
                    Status = false,
                    Message = "Employee roles data is required.",
                    Code = HttpStatusCode.BadRequest
                };
            }

            try
            {
                // Get all valid employees and roles for validation and mapping
                var validEmployees = await _context.tblEmployees.AsNoTracking().AsQueryable()
                    .Where(e => e.IsActive == true && e.IsDeleted != true)
                    .ToDictionaryAsync(e => e.Ecode?.Trim().ToLower(), e => e.EmployeeId);

                var validRoles = await _context.tblRoles.AsNoTracking().AsQueryable()
                    .ToDictionaryAsync(r => r.RoleName?.Trim().ToLower(), r => r.RoleId);

                // Validate all ECode and RoleName combinations
                var validationErrors = new List<string>();
                var validData = new List<EmployeeRoleUpsertDto>();

                foreach (var item in request.EmployeeRoles)
                {
                    if (string.IsNullOrWhiteSpace(item.Ecode) || string.IsNullOrWhiteSpace(item.RoleName))
                    {
                        validationErrors.Add($"ECode and RoleName are required for item: {item.Ecode ?? "NULL"} - {item.RoleName ?? "NULL"}");
                        continue;
                    }

                    var ecode = item.Ecode.Trim().ToLower();
                    var roleName = item.RoleName.Trim().ToLower();

                    if (!validEmployees.ContainsKey(ecode))
                    {
                        validationErrors.Add($"ECode '{item.Ecode}' not found in employee table");
                        continue;
                    }

                    if (!validRoles.ContainsKey(roleName))
                    {
                        validationErrors.Add($"RoleName '{item.RoleName}' not found in role table");
                        continue;
                    }

                    validData.Add(item);
                }

                if (validationErrors.Any())
                {
                    return new ExecuteAndReponse
                    {
                        Status = false,
                        Message = $"Validation errors: {string.Join("; ", validationErrors)}",
                        Code = HttpStatusCode.BadRequest
                    };
                }

                // Process valid data for upsert
                var updatedCount = 0;
                var insertedCount = 0;

                foreach (var item in validData)
                {
                    var ecode = item.Ecode.Trim().ToLower();
                    var roleName = item.RoleName.Trim().ToLower();
                    
                    var employeeId = validEmployees[ecode];
                    var roleId = validRoles[roleName];

                    // Check if employee role already exists
                    var existingEmployeeRole = await _context.tblEmployeeRoles
                        .FirstOrDefaultAsync(er => er.EmployeeId == employeeId);

                    if (existingEmployeeRole != null)
                    {
                        // Update existing role
                        existingEmployeeRole.RoleId = roleId;
                        existingEmployeeRole.LastUpdatedBy = "System";
                        existingEmployeeRole.LastUpdatedOn = DateTime.UtcNow;
                        updatedCount++;
                        
                        _logger.LogInformation("Updated employee role for ECode: {ECode}, EmployeeId: {EmployeeId}, RoleId: {RoleId}", 
                            item.Ecode, employeeId, roleId);
                    }
                    else
                    {
                        // Insert new employee role
                        var newEmployeeRole = new tblEmployeeRole
                        {
                            EmployeeId = employeeId,
                            RoleId = roleId,
                            AssignedOn = DateTime.UtcNow,
                            AssignedBy = "System",
                            LastUpdatedBy = "System",
                            LastUpdatedOn = DateTime.UtcNow
                        };

                        await _context.tblEmployeeRoles.AddAsync(newEmployeeRole);
                        insertedCount++;
                        
                        _logger.LogInformation("Inserted new employee role for ECode: {ECode}, EmployeeId: {EmployeeId}, RoleId: {RoleId}", 
                            item.Ecode, employeeId, roleId);
                    }
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Bulk upsert completed: {UpdatedCount} updated, {InsertedCount} inserted", updatedCount, insertedCount);

                return new ExecuteAndReponse
                {
                    Status = true,
                    Message = $"Successfully processed {validData.Count} employee roles. Updated: {updatedCount}, Inserted: {insertedCount}",
                    Code = HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while bulk upserting employee roles");
                return new ExecuteAndReponse
                {
                    Status = false,
                    Message = ex.Message,
                    Code = HttpStatusCode.BadRequest
                };
            }
        }

        public async Task<FetchAndResponse> GetAllEmployeeRolesAsync()
        {
            try
            {
                var employeeRoles = await _context.tblEmployeeRoles.AsNoTracking().AsQueryable()
                    .Join(_context.tblEmployees.AsNoTracking().AsQueryable(),
                        er => er.EmployeeId,
                        emp => emp.EmployeeId,
                        (er, emp) => new { er, emp })
                    .Join(_context.tblRoles.AsNoTracking().AsQueryable(),
                        combined => combined.er.RoleId,
                        role => role.RoleId,
                        (combined, role) => new EmployeeRoleResponseDtoo
                        {
                            EmployeeRoleId = combined.er.EmployeeRoleId,
                            EmployeeId = combined.er.EmployeeId,
                            Ecode = combined.emp.Ecode,
                            EmployeeName = $"{combined.emp.FirstName} {combined.emp.MiddleName} {combined.emp.LastName}".Trim(),
                            IsActive = combined.emp.IsActive ?? false,
                            RoleId = combined.er.RoleId,
                            RoleName = role.RoleName,
                            AssignedOn = combined.er.AssignedOn,
                            AssignedBy = combined.er.AssignedBy,
                            LastUpdatedBy = combined.er.LastUpdatedBy,
                            LastUpdatedOn = combined.er.LastUpdatedOn
                        })
                    .OrderBy(er => er.Ecode)
                    .ThenBy(er => er.RoleName)
                    .ToListAsync();

                if (employeeRoles == null || !employeeRoles.Any())
                {
                    return new FetchAndResponse
                    {
                        Status = false,
                        Message = "No employee roles found",
                        Code = HttpStatusCode.BadRequest,
                        Data = null
                    };
                }

                _logger.LogInformation("Retrieved {Count} employee roles", employeeRoles.Count);
                return new FetchAndResponse
                {
                    Status = true,
                    Message = "Employee roles fetched successfully",
                    Data = employeeRoles,
                    Code = HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving employee roles");
                return new FetchAndResponse
                {
                    Status = false,
                    Message = ex.Message,
                    Code = HttpStatusCode.BadRequest,
                    Data = null
                };
            }
        }
    }
}

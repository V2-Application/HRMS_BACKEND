using HRMSAPI.Data;
using HRMSAPI.DTO;
using HRMSAPI.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

public class AssignLocationService : IAssignLocationService
{
    private readonly HRMSContext _context; // Replace with your actual DbContext
    private readonly ILogger<AssignLocationService> _logger;

    public AssignLocationService(HRMSContext context, ILogger<AssignLocationService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> CreateLocationAssignmentAsync(List<AssignLocationsDto> assignLocations, string createdBy)
    {
        if (assignLocations == null || !assignLocations.Any())
        {
            throw new ArgumentException("At least one location assignment is required.", nameof(assignLocations));
        }
        if (string.IsNullOrEmpty(createdBy))
        {
            throw new ArgumentException("CreatedBy cannot be null or empty.", nameof(createdBy));
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            foreach (var newAssignLocation in assignLocations)
            {
                // Validate existing fields
                if (newAssignLocation.EmployeeId <= 0)
                {
                    throw new ArgumentException("EmployeeId must be a positive integer.", nameof(newAssignLocation.EmployeeId));
                }
                if ( newAssignLocation.AssignedLocation <= 0)
                {
                    throw new ArgumentException("AssignedLocation must be a positive integer.", nameof(newAssignLocation.AssignedLocation));
                }
                if (string.IsNullOrEmpty(newAssignLocation.AssignedReason))
                {
                    throw new ArgumentException("AssignedReason cannot be null or empty.", nameof(newAssignLocation.AssignedReason));
                }
                // Validate new fields
                if (newAssignLocation.DesignationId <= 0)
                {
                    throw new ArgumentException("DesignationId must be a positive integer.", nameof(newAssignLocation.DesignationId));
                }
                if (newAssignLocation.DepartmentId <= 0)
                {
                    throw new ArgumentException("DepartmentId must be a positive integer.", nameof(newAssignLocation.DepartmentId));
                }

                // Verify EmployeeId exists
                var employeeExists = await _context.tblEmployees
                    .AnyAsync(e => e.EmployeeId == newAssignLocation.EmployeeId)
                    .ConfigureAwait(false);
                if (!employeeExists)
                {
                    throw new KeyNotFoundException($"Employee with ID {newAssignLocation.EmployeeId} not found.");
                }

                // Verify AssignedLocation exists
                var locationExists = await _context.tblLocations
                    .AnyAsync(l => l.LocationId == newAssignLocation.AssignedLocation)
                    .ConfigureAwait(false);
                if (!locationExists)
                {
                    throw new KeyNotFoundException($"Location with ID {newAssignLocation.AssignedLocation} not found.");
                }

                // Verify DesignationId exists
                var designationExists = await _context.tblDesignations
                    .AnyAsync(d => d.DesignationId == newAssignLocation.DesignationId)
                    .ConfigureAwait(false);
                if (!designationExists)
                {
                    throw new KeyNotFoundException($"Designation with ID {newAssignLocation.DesignationId} not found.");
                }

                // Verify DepartmentId exists
                var departmentExists = await _context.tblDepartments
                    .AnyAsync(d => d.DepartmentId == newAssignLocation.DepartmentId)
                    .ConfigureAwait(false);
                if (!departmentExists)
                {
                    throw new KeyNotFoundException($"Department with ID {newAssignLocation.DepartmentId} not found.");
                }

                // Check for existing active assignment
                var existingAssignment = await _context.AssignLocationHistories
                    .AnyAsync(a => a.EmployeeId == newAssignLocation.EmployeeId
                                && a.AssignedLocation == newAssignLocation.AssignedLocation
                                && a.IsActive == true)
                    .ConfigureAwait(false);
                if (existingAssignment)
                {
                    throw new InvalidOperationException($"Active assignment already exists for EmployeeId {newAssignLocation.EmployeeId} and LocationId {newAssignLocation.AssignedLocation}.");
                }

                var newEntry = new AssignLocationHistory
                {
                    EmployeeId = newAssignLocation.EmployeeId,
                    CandidateId = newAssignLocation.CandidateId,
                    AssignedLocation = (int?)newAssignLocation.AssignedLocation,
                    AssignedReason = newAssignLocation.AssignedReason,
                    IsActive = true,
                    AssignedOnDate = DateTime.UtcNow,
                    ReleasedOnDate = newAssignLocation.ReleasedOnDate,
                    TransferApprovalStatus = (newAssignLocation.TransferApprovalStatus ?? 4),
                    IsReportingHeadApproval = (newAssignLocation.IsReportingHeadApproval ?? 4),
                    IsHRApproval = (int?)(newAssignLocation.IsHRApproval ?? 4),
                    PermanentTransfer = (bool)newAssignLocation.PermanentTransfer ? newAssignLocation.PermanentTransfer : false,
                    TemporaryTransfer = (bool)newAssignLocation.TemporaryTransfer ? newAssignLocation.TemporaryTransfer : false,
                   designationid = (int?)newAssignLocation.DesignationId, // Added
                   departmentid = (int?)newAssignLocation.DepartmentId // Added
                   ,CreatedBy=createdBy,
                   CreatedOn=DateTime.Now,
                   IsDeleted=false,
                };
                _context.AssignLocationHistories.Add(newEntry);
            }

            var rowsAffected = await _context.SaveChangesAsync().ConfigureAwait(false);
            await transaction.CommitAsync().ConfigureAwait(false);

            _logger.LogInformation("Successfully created {Count} location assignments for EmployeeId: {EmployeeId}",
                assignLocations.Count, assignLocations.First().EmployeeId);

            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync().ConfigureAwait(false);
            _logger.LogError(ex, "Error creating location assignments for EmployeeId: {EmployeeId}",
                assignLocations.FirstOrDefault()?.EmployeeId ?? 0);
            throw new InvalidOperationException("An error occurred while creating location assignments.", ex);
        }
    }
    public async Task<List<AssignLocationsDto>> GetLocationAssignmentsAsync(
    JwtLoginDetailDto loginDetail,
    bool activeOnly = false,
    long? employeeId = null,bool isHr = false)
    
    {
        try
        {
            long? loginEmployeeId = null;
            if (isHr)
            {

            }
            else
            {
                // Make it optional
                if (!string.IsNullOrEmpty(loginDetail?.EmployeeId))
                {
                    loginEmployeeId = Convert.ToInt64(loginDetail.EmployeeId);
                }
            }
            var parameters = new[]
            {
            new SqlParameter("@LoginEmployeeId", SqlDbType.BigInt)
            { Value = (object?)loginEmployeeId ?? DBNull.Value },

            new SqlParameter("@EmployeeId", SqlDbType.BigInt)
            { Value = (object?)employeeId ?? DBNull.Value },

            new SqlParameter("@ActiveOnly", SqlDbType.Bit)
            { Value = activeOnly }
        };

            var result = await _context.Database
                .SqlQueryRaw<AssignLocationsDto>(
                    "EXEC sp_GetLocationAssignments @LoginEmployeeId, @EmployeeId, @ActiveOnly",
                    parameters)
                .ToListAsync()
                .ConfigureAwait(false);

            if (!result.Any())
            {
                _logger.LogWarning("No assignments found. LoginEmployeeId: {LoginEmployeeId}, EmployeeId: {EmployeeId}, ActiveOnly: {ActiveOnly}",
                    loginEmployeeId, employeeId, activeOnly);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while calling sp_GetLocationAssignments. LoginEmployeeId: {LoginEmployeeId}, EmployeeId: {EmployeeId}, ActiveOnly: {ActiveOnly}",
                loginDetail?.EmployeeId, employeeId, activeOnly);
            throw new ApplicationException("Error while getting location assignments.", ex);
        }
    }

    public async Task<bool> ApproveLocationAssignmentAsync(AssignLocationApprovalDto approvalDto, string updatedBy)
    {
        if (approvalDto == null)
            throw new ArgumentException("Approval data is required.", nameof(approvalDto));

        if (approvalDto.AssignLocationHistoryId <= 0)
            throw new ArgumentException("AssignLocationHistoryId must be a positive integer.", nameof(approvalDto.AssignLocationHistoryId));

        if (string.IsNullOrEmpty(updatedBy))
            throw new ArgumentException("UpdatedBy cannot be null or empty.", nameof(updatedBy));

        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var assignment = await _context.AssignLocationHistories
                .FirstOrDefaultAsync(a => a.AssignLocationHistoryId == approvalDto.AssignLocationHistoryId)
                .ConfigureAwait(false);

            if (assignment == null)
                throw new KeyNotFoundException($"Location assignment with ID {approvalDto.AssignLocationHistoryId} not found.");

            // Validate user is sending at least one approval flag
            if (!approvalDto.IsReportingHeadApproval.HasValue && !approvalDto.IsHRApproval.HasValue)
                throw new ArgumentException("At least ReportingHeadApproval or HRApproval must be provided.");

            // HR approval came first — reject if manager has not approved
            if (approvalDto.IsHRApproval == 1 && assignment.IsReportingHeadApproval != 1)
                throw new InvalidOperationException("Cannot approve by HR until Reporting Head has approved.");

            // Apply approvals
            if (approvalDto.IsReportingHeadApproval.HasValue)
                assignment.IsReportingHeadApproval = approvalDto.IsReportingHeadApproval.Value;

            if (approvalDto.IsHRApproval.HasValue)
                assignment.IsHRApproval = approvalDto.IsHRApproval.Value;
            if (!String.IsNullOrEmpty(updatedBy)) {
                assignment.UpdatedBy = updatedBy;
                assignment.UpdatedOn = DateTime.Now;
             }
            // Finalize TransferApprovalStatus based on both
            if (assignment.IsReportingHeadApproval == 1 && assignment.IsHRApproval == 1)
            {
                assignment.TransferApprovalStatus = 1; // Approved
            }
            else if (assignment.IsReportingHeadApproval == 2 || assignment.IsHRApproval == 2)
            {
                assignment.TransferApprovalStatus = 2; // Rejected if either rejected
            }
            else
            {
                assignment.TransferApprovalStatus = 4; // Still pending
            }

            var rowsAffected = await _context.SaveChangesAsync().ConfigureAwait(false);
            await transaction.CommitAsync().ConfigureAwait(false);

            _logger.LogInformation("Approval updated successfully for AssignLocationHistoryId: {AssignLocationHistoryId} by {UpdatedBy}",
                approvalDto.AssignLocationHistoryId, updatedBy);

            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync().ConfigureAwait(false);
            _logger.LogError(ex, "Error in approval for AssignLocationHistoryId: {AssignLocationHistoryId} by {UpdatedBy}",
                approvalDto?.AssignLocationHistoryId, updatedBy);
            throw new InvalidOperationException("An error occurred while updating location assignment approval.", ex);
        }
    }

}
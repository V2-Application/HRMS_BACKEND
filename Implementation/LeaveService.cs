using DocumentFormat.OpenXml.Office2010.Excel;
using HRMSAPI.Data;
using HRMSAPI.DTO;
using HRMSAPI.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Data;
using static HRMSAPI.Implementation.EmpAttendanceService;

namespace HRMSAPI.Implementation
{
    public class LeaveService : ILeaveService
    {
        private readonly IConfiguration _configuration;
        private readonly HRMSContext _context;
        private readonly ILogger<LeaveService> _logger;

        public LeaveService(HRMSContext context, IConfiguration configuration, ILogger<LeaveService> logger)
        {
            _configuration = configuration;
            _context = context;
            _logger = logger;
        }
        //public async Task<List<LeaveRequestDto>> GetList(long id)
        //{
        //    try
        //    {
        //        var leaveTypes = await _context.tblLeaveTypes
        //            .AsNoTracking()
        //            .ToDictionaryAsync(x => x.LeaveTypeId, x => x.LeaveTypeName);

        //        var employees = await _context.tblEmployees
        //            .AsNoTracking()
        //            .ToDictionaryAsync(x => x.EmployeeId, x => x.FULL_NAME);

        //        var statuses = await _context.tblStatuses
        //            .AsNoTracking()
        //            .ToDictionaryAsync(x => x.StatusId, x => x.StatusName);

        //        // Filter leave requests by EmployeeId
        //        var leaveRequests = await _context.tblLeaveRequests
        //            .AsNoTracking()
        //            .Where(c => c.EmployeeId == id)
        //            .ToListAsync();

        //        var response = leaveRequests.Select(c => new LeaveRequestDto
        //        {
        //            LeaveRequestId = c.LeaveRequestId,
        //            LeaveTypeName = leaveTypes.TryGetValue(c.LeaveTypeId ?? 0, out var leaveType) ? leaveType : "N/A",
        //            EmployeeId = c.EmployeeId ?? 0,
        //            EmployeeName = employees.TryGetValue(c.EmployeeId ?? 0, out var employeeName) ? employeeName : "N/A",
        //            StatusId = c.StatusId ?? 0,
        //            StatusName = statuses.TryGetValue(c.StatusId ?? 0, out var statusName) ? statusName : "N/A",
        //            LeaveTypeId = c.LeaveTypeId ?? 0,
        //            StartDate = c.StartDate,
        //            EndDate = c.EndDate,
        //            ReportingManagerId = c.ReportingManagerId ?? 0,
        //            Reason = c.Reason,
        //            Remarks = c.Remarks,
        //            RelieverName = (from history in _context.AssignLocationHistories.AsNoTracking()
        //                            join reliever in _context.tblEmployees.AsNoTracking()
        //                                on history.EmployeeId equals reliever.EmployeeId
        //                            where history.EmployeeId == reliever.EmployeeId 
        //                            select reliever.FULL_NAME)
        //                            .FirstOrDefault(),
        //            RelieverEcode = (from history in _context.AssignLocationHistories.AsNoTracking()
        //                            join reliever in _context.tblEmployees.AsNoTracking()
        //                                on history.EmployeeId equals reliever.EmployeeId
        //                            where history.EmployeeId == reliever.EmployeeId
        //                            select reliever.Ecode)
        //                            .FirstOrDefault()
        //        }).ToList();

        //        return response;
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"Error in GetList: {ex.Message}");
        //        throw new ApplicationException("An error occurred while fetching leave requests.", ex);
        //    }
        //}
        public async Task<List<LeaveRequestDto>> GetList(long id)
        {
            try
            {
                var response = await _context.Database
                    .SqlQueryRaw<LeaveRequestDto>("EXEC GetLeaveRequestList @EmployeeId = {0}", id)
                    .ToListAsync();

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching leave requests for EmployeeId: {EmployeeId}", id);
                throw new ApplicationException("An error occurred while fetching leave requests.", ex);
            }
        }

        // Alternative approach using SqlParameter for better security
        public async Task<List<LeaveRequestDto>> GetListWithParameter(long id)
        {
            try
            {
                var parameter = new SqlParameter("@EmployeeId", SqlDbType.BigInt) { Value = id };

                var response = await _context.Database
                    .SqlQueryRaw<LeaveRequestDto>("EXEC GetLeaveRequestList @EmployeeId", parameter)
                    .ToListAsync();

                return response;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetListWithParameter: {ex.Message}");
                throw new ApplicationException("An error occurred while fetching leave requests.", ex);
            }
        }

        public async Task<tblLeaveRequest> LeaveRequest(LeaveRequestDto dtoObject)
        {
            if (dtoObject == null)
            {
                throw new ArgumentNullException(nameof(dtoObject), "Leave request data cannot be null.");
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                tblLeaveRequest? leaveRequest = null;

                if (dtoObject.IsRevoked == true)
                {
                    // Fetch the existing leave request
                    leaveRequest = await _context.tblLeaveRequests
                        .FirstOrDefaultAsync(l => l.LeaveRequestId == dtoObject.LeaveRequestId)
                        .ConfigureAwait(false);

                    if (leaveRequest == null)
                    {
                        throw new KeyNotFoundException($"Leave request with ID {dtoObject.LeaveRequestId} not found.");
                    }

                    // Restore balance if the leave was pending (StatusId == 2)
                    if (leaveRequest.StatusId == 2)
                    {
                        //var leaveBalance = await _context.tblEmployeeLeaveBalancenewasperportals
                        //    .FirstOrDefaultAsync(b => b.EmployeeId == leaveRequest.EmployeeId)
                        //    .ConfigureAwait(false);

                        //if (leaveBalance != null)
                        //{
                        //    decimal leaveDays = CalculateLeaveDays(leaveRequest);
                        //    if (leaveRequest.LeaveTypeId == 7) // CompOff Leave
                        //    {
                        //        leaveBalance.CompOffBalance += leaveDays;
                        //    }
                        //    else if (leaveRequest.LeaveTypeId == 15) // Casual Leave
                        //    {
                        //        leaveBalance.CasualLeaveBalance += leaveDays;
                        //    }
                        //    else if (leaveRequest.LeaveTypeId == 17) // Earned Leave
                        //    {
                        //        leaveBalance.EarnedLeaveBalance += leaveDays;
                        //    }
                        //    _context.tblEmployeeLeaveBalancenewasperportals.Update(leaveBalance);
                        //}
                    }

                    // Update the leave request
                    leaveRequest.IsRevoked = true;
                    leaveRequest.CreatedBy = dtoObject.CreatedBy;
                    leaveRequest.CreatedOn = DateTime.UtcNow;

                    _context.tblLeaveRequests.Update(leaveRequest);
                }
                else
                {
                    if (dtoObject.StartDate > dtoObject.EndDate)
                    {
                        throw new ArgumentException("Start Date cannot be later than End Date.");
                    }

                    // Check for overlapping leave requests
                    var existingLeaveRequests = await _context.tblLeaveRequests
                        .AsNoTracking()
                        .Where(lr => lr.EmployeeId == dtoObject.EmployeeId 
                            && lr.IsRevoked != true 
                            && lr.StatusId==1
                            //&& lr.LeaveRequestId != (dtoObject.LeaveRequestId ?? 0)
                            )
                        .ToListAsync()
                        .ConfigureAwait(false);

                    var newStartDate = dtoObject.StartDate.Date;
                    var newEndDate = dtoObject.EndDate.Date;
                    var overlappingDates = new List<DateTime>();

                    foreach (var existingRequest in existingLeaveRequests)
                    {
                        if (existingRequest.StartDate == null || existingRequest.EndDate == null)
                            continue;

                        var existingStartDate = existingRequest.StartDate.Date;
                        var existingEndDate = existingRequest.EndDate.Date;

                        // Check if date ranges overlap
                        if (newStartDate <= existingEndDate && newEndDate >= existingStartDate)
                        {
                            // Find overlapping dates
                            var overlapStart = newStartDate > existingStartDate ? newStartDate : existingStartDate;
                            var overlapEnd = newEndDate < existingEndDate ? newEndDate : existingEndDate;

                            for (var date = overlapStart; date <= overlapEnd; date = date.AddDays(1))
                            {
                                if (!overlappingDates.Contains(date))
                                {
                                    overlappingDates.Add(date);
                                }
                            }
                        }
                    }

                    if (overlappingDates.Any())
                    {
                        var overlappingDatesStr = string.Join(", ", overlappingDates.OrderBy(d => d).Select(d => d.ToString("dd-MM-yyyy")));
                        throw new InvalidOperationException(
                            $"Leave request overlaps with existing leave. The following dates are already under applied leave: {overlappingDatesStr}");
                    }

                    // Calculate leave days
                    decimal leaveDays = CalculateLeaveDays(new tblLeaveRequest
                    {
                        StartDate = dtoObject.StartDate,
                        EndDate = dtoObject.EndDate,
                        FirstHalf = dtoObject.FirstHalf,
                        SecondHalf = dtoObject.SecondHalf,
                        FullDay = dtoObject.FullDay
                    });

                    // Fetch and validate leave balance
                    //var leaveBalance = await _context.tblEmployeeLeaveBalancenewasperportals
                    //    .FirstOrDefaultAsync(b => b.EmployeeId == dtoObject.EmployeeId)
                    //    .ConfigureAwait(false);
                    var currentMonth = DateTime.Now.ToString("MMM-yy");
                    var leaveBalance = await _context.GetProcedures().sp_GetEmployeeLeaveBalanceAsync(dtoObject.EmployeeId, currentMonth);

                    if (leaveBalance == null)
                    {
                        throw new InvalidOperationException("Leave balance not found for the employee.");
                    }

                    // Check sufficient balance
                    //bool hasSufficientBalance = false;
                    //if (dtoObject.LeaveTypeId == 7) // CompOff Leave
                    //{
                    //    hasSufficientBalance = leaveBalance[0].Remaining_CompOff >= leaveDays;
                    //}
                    //else if (dtoObject.LeaveTypeId == 15) // Casual Leave
                    //{
                    //    hasSufficientBalance = leaveBalance[0].Remaining_CL >= leaveDays;
                    //}
                    //else if (dtoObject.LeaveTypeId == 17) // Earned Leave
                    //{
                    //    hasSufficientBalance = leaveBalance[0].Remaining_EL >= leaveDays;
                    //}
                    //else
                    //{
                    //    throw new ArgumentException($"Invalid LeaveTypeId: {dtoObject.LeaveTypeId}");
                    //}

                    //if (!hasSufficientBalance)
                    //{
                    //    throw new InvalidOperationException("Insufficient leave balance.");
                    //}

                    // Deduct from balance (pending)
                    //if (dtoObject.LeaveTypeId == 7) // CompOff Leave
                    //{
                    //    leaveBalance.CompOffBalance -= leaveDays;
                    //}
                    //else if (dtoObject.LeaveTypeId == 15) // Casual Leave
                    //{
                    //    leaveBalance.CasualLeaveBalance -= leaveDays;
                    //}
                    //else if (dtoObject.LeaveTypeId == 17) // Earned Leave
                    //{
                    //    leaveBalance.EarnedLeaveBalance -= leaveDays;
                    //}
                    //_context.tblEmployeeLeaveBalancenewasperportals.Update(leaveBalance);

                    // Create a new leave request
                    leaveRequest = new tblLeaveRequest
                    {
                        EmployeeId = dtoObject.EmployeeId,
                        LeaveTypeId = dtoObject.LeaveTypeId ?? 7,
                        StartDate = dtoObject.StartDate,
                        EndDate = dtoObject.EndDate,
                        Reason = dtoObject.Reason,
                        StatusId = dtoObject.StatusId != null ? dtoObject.StatusId : 4,
                        ReportingManagerId = dtoObject.ReportingManagerId,
                        CreatedBy = dtoObject.CreatedBy,
                        CreatedOn = DateTime.UtcNow,
                        Remarks = dtoObject.Remarks,
                        IsRevoked = false,
                        FirstHalf = dtoObject.FirstHalf,
                        SecondHalf = dtoObject.SecondHalf,
                        FullDay = dtoObject.FullDay
                    };

                    await _context.tblLeaveRequests.AddAsync(leaveRequest).ConfigureAwait(false);
                }

                await _context.SaveChangesAsync().ConfigureAwait(false);
                await transaction.CommitAsync().ConfigureAwait(false);

                return leaveRequest;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync().ConfigureAwait(false);
                _logger.LogError(ex, "Error processing leave request. LeaveRequestId: {LeaveRequestId}, IsRevoked: {IsRevoked}",
                    dtoObject.LeaveRequestId, dtoObject.IsRevoked);

                throw new Exception("An error occurred while processing the leave request. Please try again later.", ex);
            }
        }
        public async Task<List<EmployeeLeaveBalanceDto>> GetEmployeeLeaveBalanceAsync(long employeeId)
        {
            try
            {
                // Fetch the employee's leave balance
                var leaveBalance = await _context.tblEmployeeLeaveBalancenewasperportals
                    .AsNoTracking()
                    .Where(lb => lb.EmployeeId == employeeId)
                    .FirstOrDefaultAsync();

                // Fetch pending and approved leave requests
                var leaveRequests = await _context.tblLeaveRequests
                    .AsNoTracking()
                    .Where(lr => lr.EmployeeId == employeeId && (lr.StatusId == 1 || lr.StatusId == 2)) // Pending or Approved
                    .ToListAsync();

                // Initialize deductions
                decimal compOffDeduction = 0m;
                decimal casualLeaveDeduction = 0m;
                decimal earnedLeaveDeduction = 0m;

                // Calculate deductions if leave requests exist
                if (leaveRequests != null && leaveRequests.Any())
                {
                    foreach (var request in leaveRequests)
                    {
                        // Calculate leave duration (in days)
                        decimal leaveDays = CalculateLeaveDays(request);

                        // Assign deduction to the appropriate leave type
                        if (request.LeaveTypeId == 7) // CompOff Leave
                        {
                            compOffDeduction += leaveDays;
                        }
                        else if (request.LeaveTypeId == 15) // Casual Leave
                        {
                            casualLeaveDeduction += leaveDays;
                        }
                        else if (request.LeaveTypeId == 17) // Earned Leave
                        {
                            earnedLeaveDeduction += leaveDays;
                        }
                    }
                }

                // Initialize DTO list with updated LeaveTypeId values
                var result = new List<EmployeeLeaveBalanceDto>
        {
            new EmployeeLeaveBalanceDto
            {
                LeaveType = "CompOff Leave",
                LeaveTypeId = 7,
                AvailableBalance = leaveBalance?.CompOffBalance ?? 0m - compOffDeduction,
                Acquired = leaveBalance?.CompOffAcquired ?? 0m,
                Utilized = (leaveBalance?.CompOffUsed ?? 0m) + compOffDeduction,
                AnnualAllotment = leaveBalance?.CompOffAcquired ?? 0m
            },
            new EmployeeLeaveBalanceDto
            {
                LeaveType = "Casual Leave",
                LeaveTypeId = 15,
                AvailableBalance = leaveBalance?.CasualLeaveBalance ?? 0m - casualLeaveDeduction,
                Acquired = leaveBalance?.CasualLeaveAcquired ?? 0m,
                Utilized = (leaveBalance?.CasualLeaveUsed ?? 0m) + casualLeaveDeduction,
                AnnualAllotment = leaveBalance?.CasualLeaveAcquired ?? 0m
            },
            new EmployeeLeaveBalanceDto
            {
                LeaveType = "Earned Leave",
                LeaveTypeId = 17,
                AvailableBalance = leaveBalance?.EarnedLeaveBalance ?? 0m - earnedLeaveDeduction,
                Acquired = leaveBalance?.EarnedLeaveAcquired ?? 0m,
                Utilized = (leaveBalance?.EarnedLeaveUsed ?? 0m) + earnedLeaveDeduction,
                AnnualAllotment = leaveBalance?.EarnedLeaveAcquired ?? 0m
            }
        };

                _logger.LogInformation("Retrieved leave balance for EmployeeId: {EmployeeId}, found {Count} leave types.", employeeId, result.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching employee leave balance for EmployeeId: {EmployeeId}", employeeId);
                // Return default DTO list with zeros and updated LeaveTypeId values
                return new List<EmployeeLeaveBalanceDto>
        {
            new EmployeeLeaveBalanceDto
            {
                LeaveType = "CompOff Leave",
                LeaveTypeId = 7,
                AvailableBalance = 0m,
                Acquired = 0m,
                Utilized = 0m,
                AnnualAllotment = 0m
            },
            new EmployeeLeaveBalanceDto
            {
                LeaveType = "Casual Leave",
                LeaveTypeId = 15,
                AvailableBalance = 0m,
                Acquired = 0m,
                Utilized = 0m,
                AnnualAllotment = 0m
            },
            new EmployeeLeaveBalanceDto
            {
                LeaveType = "Earned Leave",
                LeaveTypeId = 17,
                AvailableBalance = 0m,
                Acquired = 0m,
                Utilized = 0m,
                AnnualAllotment = 0m
            }
        };
            }
        }
        private decimal CalculateLeaveDays(tblLeaveRequest request)
        {
            decimal leaveDays = 0m;

            if (request == null || request.StartDate == null || request.EndDate == null)
            {
                return leaveDays; // Return 0 if request or dates are null
            }

            if (request.FullDay == true || request.FullDay == null)
            {
                // Calculate full days between StartDate and EndDate (inclusive)
                leaveDays = (request.EndDate.Date - request.StartDate.Date).Days + 1;
            }
            else if (request.FirstHalf == true || request.SecondHalf == true)
            {
                // Handle half-day logic
                leaveDays = 0.5m; // Count as half a day
            }

            return leaveDays < 0 ? 0m : leaveDays; // Ensure non-negative days
        }

        public async Task<PagedResult<LeaveRequestDto>> GetLeaveRequestsAsync(
      long managerId,
      string role,
      int statusId = 0,
      int pageNumber = 1,
      int pageSize = 10,
      string? searchTerm = null)
        {
            // Validate input
            if (managerId <= 0)
            {
                throw new ArgumentException("Manager ID must be a positive integer.", nameof(managerId));
            }
            if (string.IsNullOrWhiteSpace(role))
            {
                throw new ArgumentException("Role cannot be empty or null.", nameof(role));
            }
            if (pageNumber < 1)
            {
                throw new ArgumentException("Page number must be greater than 0.", nameof(pageNumber));
            }
            if (pageSize < 1)
            {
                throw new ArgumentException("Page size must be greater than 0.", nameof(pageSize));
            }

            try
            {
                // Original query (unchanged)
                var query = from request in _context.tblLeaveRequests.AsNoTracking()
                            join employee in _context.tblEmployees.AsNoTracking()
                                on request.EmployeeId equals employee.EmployeeId into empGroup
                            from employee in empGroup.DefaultIfEmpty()
                            join status in _context.tblStatuses.AsNoTracking()
                                on request.StatusId equals status.StatusId into statusGroup
                            from status in statusGroup.DefaultIfEmpty()
                            join leaveType in _context.tblLeaveTypes.AsNoTracking()
                                on request.LeaveTypeId equals leaveType.LeaveTypeId into leaveTypeGroup
                            from leaveType in leaveTypeGroup.DefaultIfEmpty()
                            join location in _context.tblLocations.AsNoTracking()
                                on employee.LocationId equals location.LocationId into locationGroup
                            from location in locationGroup.DefaultIfEmpty()
                            select new
                            {
                                request,
                                employee,
                                status,
                                leaveType,
                                location
                            };

                // Apply filtering based on role
                if (role.Trim().ToLower() != "superadmin")
                {
                    query = query.Where(x => x.request.ReportingManagerId == managerId);
                }

                // Apply statusId filter
                if (statusId != 0)
                {
                    query = query.Where(x => x.request.StatusId == statusId);
                }

                // Apply search filter across relevant columns
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    searchTerm = searchTerm.Trim().ToLower();
                    query = query.Where(x =>
                        (x.employee.FULL_NAME != null && x.employee.FULL_NAME.ToLower().Contains(searchTerm)) ||
                        (x.employee.FirstName != null && x.employee.FirstName.ToLower().Contains(searchTerm)) ||
                        (x.employee.LastName != null && x.employee.LastName.ToLower().Contains(searchTerm)) ||
                        (x.request.Reason != null && x.request.Reason.ToLower().Contains(searchTerm)) ||
                        (x.request.Remarks != null && x.request.Remarks.ToLower().Contains(searchTerm)) ||
                        x.request.LeaveRequestId.ToString().Contains(searchTerm) ||
                        x.request.EmployeeId.ToString().Contains(searchTerm) ||
                        (x.employee.Ecode != null && x.employee.Ecode.ToLower().Contains(searchTerm)) ||
                        (x.location != null && x.location.LocationName != null && x.location.LocationName.ToLower().Contains(searchTerm)) ||
                        (x.location != null && x.location.STCode != null && x.location.STCode.ToLower().Contains(searchTerm))
                    );
                }

                // Get total record count before pagination
                int totalRecords = await query.CountAsync();

                // Apply pagination
                query = query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize);

                // Fetch results to memory for client-side processing
                var results = await query.ToListAsync();

                // Map to DTO with client-side ReportHeadName and Reliever resolution
                var result = results.Select(x => new LeaveRequestDto
                {
                    LeaveRequestId = x.request.LeaveRequestId,
                    EmployeeId = x.request.EmployeeId,
                    EmployeeName = x.employee != null
                        ? (x.employee.FULL_NAME
                            ?? (x.employee.FirstName != null && x.employee.LastName != null
                                ? $"{x.employee.FirstName} {x.employee.LastName}"
                                : x.employee.FirstName
                                    ?? x.employee.LastName
                                    ?? "N/A"))
                        : "N/A",
                    Ecode = x.employee != null ? x.employee.Ecode ?? "N/A" : "N/A",
                    StatusId = x.request.StatusId,
                    StatusName = x.status != null ? x.status.StatusName : "N/A",
                    LeaveTypeId = x.request.LeaveTypeId,
                    LeaveTypeName = x.leaveType != null ? x.leaveType.LeaveTypeName : "N/A",
                    StartDate = x.request.StartDate,
                    EndDate = x.request.EndDate,
                    Reason = x.request.Reason != null ? x.request.Reason.Trim() : null,
                    Remarks = x.request.Remarks != null ? x.request.Remarks.Trim() : null,
                    IsRevoked = x.request.IsRevoked,
                    ReportingManagerId = x.request.ReportingManagerId,
                    ReportHeadEcode = x.employee != null ? x.employee.ReportHeadEcode ?? "N/A" : "N/A",
                    ReportHeadName = x.employee != null && x.employee.ReportHeadEcode != null
                        ? (_context.tblEmployees.AsNoTracking()
                            .Where(row => row.Ecode == x.employee.ReportHeadEcode)
                            .Select(row => row.FULL_NAME)
                            .FirstOrDefault() ?? "N/A")
                        : "N/A",
                    LocationName = x.location != null ? x.location.LocationName ?? "N/A" : "N/A",
                    STCode = x.location != null ? x.location.STCode ?? "N/A" : "N/A",
                    LocationId = x.location != null ? x.location.LocationId : 0,
                    CreatedOn = x.request.CreatedOn,
                    CreatedBy = x.request.CreatedBy,
                    UpdatedOn = x.request.UpdatedOn,
                    LastUpdatedBy = x.request.LastUpdatedBy,
                    FirstHalf = x.request.FirstHalf,
                    SecondHalf = x.request.SecondHalf,
                    FullDay = x.request.FullDay,
                    RelieverName = (from history in _context.AssignLocationHistories.AsNoTracking()
                                    join reliever in _context.tblEmployees.AsNoTracking()
                                        on history.EmployeeId equals reliever.EmployeeId
                                    where history.EmployeeId.HasValue
                                        && history.EmployeeId ==reliever.EmployeeId
                                        && history.AssignedReason == "Reliever"
                                        && history.IsActive == true
                                        && history.AssignedOnDate <= x.request.StartDate
                                        && (history.ReleasedOnDate == null || history.ReleasedOnDate >= x.request.StartDate)
                                    orderby history.AssignedOnDate descending
                                    select reliever.FULL_NAME)
                                    .FirstOrDefault() ?? "N/A",
                    RelieverEcode = (from history in _context.AssignLocationHistories.AsNoTracking()
                                     join reliever in _context.tblEmployees.AsNoTracking()
                                         on history.EmployeeId equals reliever.EmployeeId
                                     where history.EmployeeId.HasValue
                                         && history.EmployeeId == reliever.EmployeeId
                                         && history.AssignedReason == "Reliever"
                                         && history.IsActive == true
                                         && history.AssignedOnDate <= x.request.StartDate
                                         && (history.ReleasedOnDate == null || history.ReleasedOnDate >= x.request.StartDate)
                                     orderby history.AssignedOnDate descending
                                     select reliever.Ecode)
                                     .FirstOrDefault() ?? "N/A"
                }).OrderByDescending(x => x.LeaveRequestId).ToList();

                return new PagedResult<LeaveRequestDto>(result, totalRecords);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve leave requests for managerId: {ManagerId}", managerId);
                throw new InvalidOperationException("Failed to retrieve leave requests.", ex);
            }
        }
        //public async Task<bool> UpdateLeaveRequestStatusAsync(long requestId, UpdateLeaveRequestDto updateDto, string updatedBy)
        //{
        //    if (requestId <= 0)
        //    {
        //        throw new ArgumentException("Request ID must be a positive integer.", nameof(requestId));
        //    }
        //    if (updateDto == null)
        //    {
        //        throw new ArgumentNullException(nameof(updateDto), "Update data is required.");
        //    }
        //    if (string.IsNullOrEmpty(updatedBy))
        //    {
        //        throw new ArgumentException("UpdatedBy cannot be null or empty.", nameof(updatedBy));
        //    }
        //    AssignedLocationDTO assignLocations;
        //    try
        //    {
        //        //assignLocations = JsonConvert.DeserializeObject<List<AssignedLocationDTO>>(details.AssignLocationsListJson ?? "")
        //        //                  ?? new List<AssignedLocationDTO>();
        //        assignLocations = JsonConvert.DeserializeObject<AssignedLocationDTO>(updateDto.AssignLocationsListJson ?? "")
        //                          ?? new AssignedLocationDTO();
        //    }
        //    catch
        //    {
        //        assignLocations = new AssignedLocationDTO();
        //    }

        //    await using var transaction = await _context.Database.BeginTransactionAsync();
        //    try
        //    {
        //        // Fetch the leave request
        //        var request = await _context.tblLeaveRequests
        //            .FirstOrDefaultAsync(r => r.LeaveRequestId == requestId)
        //            .ConfigureAwait(false);

        //        if (request == null)
        //        {
        //            _logger.LogWarning("Leave request not found for ID: {RequestId}", requestId);
        //            return false;
        //        }

        //        if ((bool)request.IsRevoked)
        //        {
        //            throw new InvalidOperationException("Cannot update status of a revoked leave request.");
        //        }

        //        // Validate StatusId
        //        if (!IsValidStatusId(updateDto.StatusId))
        //        {
        //            throw new ArgumentException($"Invalid status ID: {updateDto.StatusId}", nameof(updateDto.StatusId));
        //        }

        //        // Fetch leave balance
        //        var leaveBalance = await _context.tblEmployeeLeaveBalancenewasperportals
        //            .FirstOrDefaultAsync(b => b.EmployeeId == request.EmployeeId)
        //            .ConfigureAwait(false);

        //        if (leaveBalance == null)
        //        {
        //            throw new InvalidOperationException("Leave balance not found for the employee.");
        //        }

        //        // Calculate leave days
        //        decimal leaveDays = CalculateLeaveDays(request);

        //        // Handle balance adjustments
        //        if (request.StatusId == 2) // Current status is Pending
        //        {
        //            if (updateDto.StatusId == 1) // Approved
        //            {
        //                // Update the Used fields
        //                switch (request.LeaveTypeId)
        //                {
        //                    case 1: // CompOff
        //                        leaveBalance.CompOffUsed += leaveDays;
        //                        break;
        //                    case 2: // Casual Leave
        //                        leaveBalance.CasualLeaveUsed += leaveDays;
        //                        break;
        //                    case 3: // Earned Leave
        //                        leaveBalance.EarnedLeaveUsed += leaveDays;
        //                        break;
        //                }
        //            }
        //            else if (updateDto.StatusId == 3) // Rejected
        //            {
        //                // Restore the balance
        //                switch (request.LeaveTypeId)
        //                {
        //                    case 1: // CompOff
        //                        leaveBalance.CompOffBalance += leaveDays;
        //                        break;
        //                    case 2: // Casual Leave
        //                        leaveBalance.CasualLeaveBalance += leaveDays;
        //                        break;
        //                    case 3: // Earned Leave
        //                        leaveBalance.EarnedLeaveBalance += leaveDays;
        //                        break;
        //                }
        //            }
        //            _context.tblEmployeeLeaveBalancenewasperportals.Update(leaveBalance);
        //        }

        //        // Fetch employee Ecode
        //        var ecode = await _context.tblEmployees
        //            .Where(e => e.EmployeeId == request.EmployeeId)
        //            .Select(e => e.Ecode)
        //            .FirstOrDefaultAsync()
        //            .ConfigureAwait(false);

        //        if (string.IsNullOrEmpty(ecode))
        //        {
        //            _logger.LogWarning("Employee not found for EmployeeId: {EmployeeId}", request.EmployeeId);
        //            return false;
        //        }

        //        // Update leave request properties
        //        request.StatusId = updateDto.StatusId;
        //        request.Remarks = updateDto.Remarks?.Trim();
        //        request.LastUpdatedBy = updatedBy;
        //        request.UpdatedOn = DateTime.UtcNow;

        //        // Handle attendance update if approved (StatusId == 1)
        //        if (updateDto.StatusId == 1)
        //        {
        //            var startDate = request.StartDate.Date;
        //            var endDate = request.EndDate.Date;

        //            for (var date = startDate; date <= endDate; date = date.AddDays(1))
        //            {
        //                var attendance = await _context.tblEmployeeMultiPunches
        //                    .FirstOrDefaultAsync(a => a.UserID == ecode && a.PunchDate.Date == date)
        //                    .ConfigureAwait(false);

        //                if (attendance == null)
        //                {
        //                    attendance = new tblEmployeeMultiPunch
        //                    {
        //                        UserID = ecode,
        //                        PunchDate = date,
        //                        IsOnLeave = true,
        //                        LeaveTypeId = request.LeaveTypeId,
        //                        LastUpdatedBy = updatedBy,
        //                        CreatedOn = DateTime.UtcNow,
        //                        CreatedBy = updatedBy,
        //                        LeaveRequestId = request.LeaveRequestId,
        //                    };
        //                    _context.tblEmployeeMultiPunches.Add(attendance);
        //                }
        //                else
        //                {
        //                    attendance.IsOnLeave = true;
        //                    attendance.LeaveTypeId = request.LeaveTypeId;
        //                    attendance.LastUpdatedBy = updatedBy;
        //                    attendance.CreatedOn = DateTime.UtcNow;
        //                    attendance.LeaveRequestId = request.LeaveRequestId;
        //                    _context.tblEmployeeMultiPunches.Update(attendance);
        //                }
        //            }
        //        }
        //        #region AssignedLocation
        //        var assignedLocations = _context.AssignLocationHistories.AsQueryable().Where(row => row.EmployeeId == request.EmployeeId).ToList();

        //        var newAssignLocation = updateDto.assignLocations;
        //        //foreach (var newAssignLocation in newAssignLocations)
        //        //{
        //        try
        //        {
        //            var newEntry1 = new AssignLocationHistory
        //            {
        //                EmployeeId = request.EmployeeId,
        //                AssignedLocation = newAssignLocation.assignedLocation,
        //                AssignedReason = newAssignLocation.assignedReason,
        //                IsActive = true,
        //                AssignedOnDate = DateTime.UtcNow,
        //                ReleasedOnDate = newAssignLocation.releasedOnDate,
        //                // Set other fields
        //            };
        //            _context.AssignLocationHistories.Add(newEntry1);
        //            //}
        //            ra = await _context.SaveChangesAsync();
        //        }
        //        catch (Exception ex) { }
        //        //if (ra < 1)
        //        //    return BuildExecuteErrorResponse("Unable to Save AssignLocation Details", HttpStatusCode.BadRequest);

        //        #endregion AssignedLocation
        //        _context.tblLeaveRequests.Update(request);
        //        await _context.SaveChangesAsync().ConfigureAwait(false);
        //        await transaction.CommitAsync().ConfigureAwait(false);

        //        _logger.LogInformation(
        //            "Successfully updated leave request {RequestId} with status {StatusId}",
        //            requestId,
        //            updateDto.StatusId);

        //        return true;
        //    }
        //    catch (Exception ex)
        //    {
        //        await transaction.RollbackAsync().ConfigureAwait(false);
        //        _logger.LogError(ex, "Unexpected error updating leave request status for request {RequestId}", requestId);
        //        throw new InvalidOperationException("An error occurred while updating the leave request.", ex);
        //    }
        //}
        private bool IsValidStatusId(int statusId)
        {
            return _context.tblStatuses.Any(s => s.StatusId == statusId);
        }

        public async Task<List<EmployeeLeaveBalanceDto>> GetEmployeeLeaveBalanceById(long employeeId)
        {
            try
            {
                var prevMonth = DateTime.Now.ToString("MMM-yy");
                // Fetch the employee's leave balance
                //var leaveBalance = await _context.tblEmployeeLeaveBalancenewasperportals
                //    .AsNoTracking()
                //    .Where(lb => lb.EmployeeId == employeeId && lb.MONTH==prevMonth)
                //    .FirstOrDefaultAsync();

                //// Fetch pending and approved leave requests
                //var leaveRequests = await _context.tblLeaveRequests
                //    .AsNoTracking()
                //    .Where(lr => lr.EmployeeId == employeeId && (lr.StatusId == 1 || lr.StatusId == 2) ) // Pending or Approved
                //    .ToListAsync();

                //// Initialize deductions
                //decimal compOffDeduction = 0m;
                //decimal casualLeaveDeduction = 0m;
                //decimal earnedLeaveDeduction = 0m;

                // Calculate deductions if leave requests exist
                //if (leaveRequests != null && leaveRequests.Any())
                //{
                //    foreach (var request in leaveRequests)
                //    {
                //        // Calculate leave duration (in days)
                //        decimal leaveDays = CalculateLeaveDays(request);

                //        // Assign deduction to the appropriate leave type
                //        switch (request.LeaveTypeId) // Adjust based on your LeaveTypeId mappings
                //        {
                //            case 1: // CompOff Leave
                //                compOffDeduction += leaveDays;
                //                break;
                //            case 2: // Casual Leave
                //                casualLeaveDeduction += leaveDays;
                //                break;
                //            case 3: // Earned Leave
                //                earnedLeaveDeduction += leaveDays;
                //                break;
                //        }
                //    }
                //}

                var leaveBalance = await _context.GetProcedures().sp_GetEmployeeLeaveBalanceAsync(employeeId, prevMonth); 

                // Initialize DTO list with default values
                var result = new List<EmployeeLeaveBalanceDto>
                {
                    new EmployeeLeaveBalanceDto
                    {
                        LeaveType = "CompOff Leave",
                        LeaveTypeId = 1,
                        AvailableBalance = leaveBalance[0]?.Remaining_CompOff ?? 0,
                        Acquired = 0,
                        Utilized = 0,
                        AnnualAllotment = 0
                        //Acquired = leaveBalance?.CompOffAcquired ?? 0m,
                        //Utilized = (leaveBalance?.CompOffUsed ?? 0m) + compOffDeduction,
                        //AnnualAllotment = leaveBalance?.CompOffAcquired ?? 0m
                    },
                    new EmployeeLeaveBalanceDto
                    {
                        LeaveType = "Casual Leave",
                        LeaveTypeId = 2,
                        AvailableBalance = leaveBalance[0]?.Remaining_CL ?? 0,
                        Acquired = 0,
                        Utilized = 0,
                        AnnualAllotment = 0
                        //Acquired = leaveBalance?.CasualLeaveAcquired ?? 0m,
                        //Utilized = (leaveBalance?.CasualLeaveUsed ?? 0m) + casualLeaveDeduction,
                        //AnnualAllotment = leaveBalance?.CasualLeaveAcquired ?? 0m
                    },
                    new EmployeeLeaveBalanceDto
                    {
                        LeaveType = "Earned Leave",
                        LeaveTypeId = 3,
                        AvailableBalance = leaveBalance[0]?.Remaining_EL ?? 0,
                        Acquired = 0,
                        Utilized = 0,
                        AnnualAllotment = 0,
                        //Acquired = leaveBalance?.EarnedLeaveAcquired ?? 0m,
                        //Utilized = (leaveBalance?.EarnedLeaveUsed ?? 0m) + earnedLeaveDeduction,
                        //AnnualAllotment = leaveBalance?.EarnedLeaveAcquired ?? 0m
                    }
                };

                _logger.LogInformation("Retrieved leave balance for EmployeeId: {EmployeeId}, found {Count} leave types.", employeeId, result.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching employee leave balance for EmployeeId: {EmployeeId}", employeeId);
                // Return default DTO list with zeros instead of throwing an error
                return new List<EmployeeLeaveBalanceDto>
                {
                    new EmployeeLeaveBalanceDto
                    {
                        LeaveType = "CompOff Leave",
                        LeaveTypeId = 1,
                        AvailableBalance = 0m,
                        Acquired = 0m,
                        Utilized = 0m,
                        AnnualAllotment = 0m
                    },
                    new EmployeeLeaveBalanceDto
                    {
                        LeaveType = "Casual Leave",
                        LeaveTypeId = 2,
                        AvailableBalance = 0m,
                        Acquired = 0m,
                        Utilized = 0m,
                        AnnualAllotment = 0m
                    },
                    new EmployeeLeaveBalanceDto
                    {
                        LeaveType = "Earned Leave",
                        LeaveTypeId = 3,
                        AvailableBalance = 0m,
                        Acquired = 0m,
                        Utilized = 0m,
                        AnnualAllotment = 0m
                    }
                };
            }
        }


        public async Task<bool> UpdateLeaveRequestStatusAsync(long requestId, UpdateLeaveRequestDto updateDto, string updatedBy)
        {
            if (requestId <= 0)
            {
                throw new ArgumentException("Request ID must be a positive integer.", nameof(requestId));
            }
            if (updateDto == null)
            {
                throw new ArgumentNullException(nameof(updateDto), "Update data is required.");
            }
            if (string.IsNullOrEmpty(updatedBy))
            {
                throw new ArgumentException("UpdatedBy cannot be null or empty.", nameof(updatedBy));
            }

            AssignedLocationDTO assignLocations;
            try
            {
                assignLocations = JsonConvert.DeserializeObject<AssignedLocationDTO>(updateDto.AssignLocationsListJson ?? "")
                                 ?? new AssignedLocationDTO();
            }
            catch
            {
                assignLocations = new AssignedLocationDTO();
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Fetch the leave request
                var request = await _context.tblLeaveRequests
                    .FirstOrDefaultAsync(r => r.LeaveRequestId == requestId)
                    .ConfigureAwait(false);

                if (request == null)
                {
                    _logger.LogWarning("Leave request not found for ID: {RequestId}", requestId);
                    return false;
                }

                if ((bool)request.IsRevoked)
                {
                    throw new InvalidOperationException("Cannot update status of a revoked leave request.");
                }

                // Validate StatusId
                if (!IsValidStatusId(updateDto.StatusId))
                {
                    throw new ArgumentException($"Invalid status ID: {updateDto.StatusId}", nameof(updateDto.StatusId));
                }

                // Fetch leave balance
                var leaveBalance = await _context.tblEmployeeLeaveBalancenewasperportals
                    .FirstOrDefaultAsync(b => b.EmployeeId == request.EmployeeId)
                    .ConfigureAwait(false);

                if (leaveBalance == null)
                {
                    throw new InvalidOperationException("Leave balance not found for the employee.");
                }

                // Calculate leave days
                decimal leaveDays = CalculateLeaveDays(request);

                // Handle balance adjustments
                if (request.StatusId == 4) // Current status is Pending
                {
                    if (updateDto.StatusId == 1) // Approved
                    {
                        // Update the Used fields
                        switch (request.LeaveTypeId)
                        {
                            case 7: // CompOff
                                leaveBalance.CompOffUsed += leaveDays;
                                break;
                            case 15: // Casual Leave
                                leaveBalance.CasualLeaveUsed += leaveDays;
                                break;
                            case 17: // Earned Leave
                                leaveBalance.EarnedLeaveUsed += leaveDays;
                                break;
                            default:
                                throw new ArgumentException($"Invalid LeaveTypeId: {request.LeaveTypeId}");
                        }
                    }
                    else if (updateDto.StatusId == 2) // Rejected
                    {
                        // Restore the balance
                        switch (request.LeaveTypeId)
                        {
                            case 7: // CompOff
                                leaveBalance.CompOffBalance += leaveDays;
                                break;
                            case 15: // Casual Leave
                                leaveBalance.CasualLeaveBalance += leaveDays;
                                break;
                            case 17: // Earned Leave
                                leaveBalance.EarnedLeaveBalance += leaveDays;
                                break;
                            default:
                                throw new ArgumentException($"Invalid LeaveTypeId: {request.LeaveTypeId}");
                        }
                    }
                    _context.tblEmployeeLeaveBalancenewasperportals.Update(leaveBalance);
                }

                // Fetch employee Ecode
                var ecode = await _context.tblEmployees
                    .Where(e => e.EmployeeId == request.EmployeeId)
                    .Select(e => e.Ecode)
                    .FirstOrDefaultAsync()
                    .ConfigureAwait(false);

                if (string.IsNullOrEmpty(ecode))
                {
                    _logger.LogWarning("Employee not found for EmployeeId: {EmployeeId}", request.EmployeeId);
                    return false;
                }

                // Update leave request properties
                request.StatusId = updateDto.StatusId;
                request.Remarks = updateDto.Remarks?.Trim();
                request.LastUpdatedBy = updatedBy;
                request.UpdatedOn = DateTime.UtcNow;
                request.RelieverEmployeeId = updateDto.RelieverEmployeeId;

                // Handle attendance update if approved (StatusId == 1)
                if (updateDto.StatusId == 1)
                {
                    var startDate = request.StartDate.Date;
                    var endDate = request.EndDate.Date;

                    for (var date = startDate; date <= endDate; date = date.AddDays(1))
                    {
                        var attendance = await _context.tblEmployeeMultiPunches
                            .FirstOrDefaultAsync(a => a.UserID == ecode && a.PunchDate.Date == date)
                            .ConfigureAwait(false);

                        if (attendance == null)
                        {
                            attendance = new tblEmployeeMultiPunch
                            {
                                UserID = ecode,
                                PunchDate = date,
                                IsOnLeave = true,
                                LeaveTypeId = request.LeaveTypeId,
                                LastUpdatedBy = updatedBy,
                                CreatedOn = DateTime.UtcNow,
                                CreatedBy = updatedBy,
                                LeaveRequestId = request.LeaveRequestId,
                            };
                            _context.tblEmployeeMultiPunches.Add(attendance);
                        }
                        else
                        {
                            attendance.IsOnLeave = true;
                            attendance.LeaveTypeId = request.LeaveTypeId;
                            attendance.LastUpdatedBy = updatedBy;
                            attendance.CreatedOn = DateTime.UtcNow;
                            attendance.LeaveRequestId = request.LeaveRequestId;
                            _context.tblEmployeeMultiPunches.Update(attendance);
                        }
                    }
                }

                #region AssignedLocation
                // Only process location data if assignLocations list is not null or empty
                if (updateDto.assignLocations != null && updateDto.assignLocations.Any())
                {
                    foreach (var newAssignLocation in updateDto.assignLocations)
                    {
                        // Validate required fields
                        if (newAssignLocation.assignedLocation == null || string.IsNullOrEmpty(newAssignLocation.assignedReason))
                        {
                            _logger.LogWarning("Skipping invalid location data for EmployeeId: {EmployeeId}", request.EmployeeId);
                            continue; // Skip invalid entries
                        }

                        try
                        {
                            var newEntry = new AssignLocationHistory
                            {
                                EmployeeId = newAssignLocation.CandidateId,
                                CandidateId = newAssignLocation.CandidateId,
                                AssignedLocation = newAssignLocation.assignedLocation ?? 0,
                                AssignedReason = newAssignLocation.assignedReason,
                                IsActive = newAssignLocation.isActive ?? true,
                                AssignedOnDate = DateTime.UtcNow,
                                ReleasedOnDate = newAssignLocation.releasedOnDate,
                                TransferApprovalStatus = 1,
                                IsReportingHeadApproval = 1,
                                IsHRApproval = 1
                            };
                            _context.AssignLocationHistories.Add(newEntry);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error adding AssignLocationHistory for EmployeeId: {EmployeeId}", request.EmployeeId);
                            throw; // Rethrow to rollback transaction
                        }
                    }
                }
                #endregion AssignedLocation

                _context.tblLeaveRequests.Update(request);
                var rowsAffected = await _context.SaveChangesAsync().ConfigureAwait(false);
                await transaction.CommitAsync().ConfigureAwait(false);

                _logger.LogInformation(
                    "Successfully updated leave request {RequestId} with status {StatusId}",
                    requestId,
                    updateDto.StatusId);

                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync().ConfigureAwait(false);
                _logger.LogError(ex, "Unexpected error updating leave request status for request {RequestId}", requestId);
                throw new InvalidOperationException("An error occurred while updating the leave request.", ex);
            }
        }
    } 
}

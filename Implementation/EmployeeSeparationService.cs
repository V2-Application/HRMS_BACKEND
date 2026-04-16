using HRMSAPI.Data;
using HRMSAPI.DTO;
using HRMSAPI.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Data;
using static Emgu.CV.Stitching.Stitcher;

public class EmployeeSeparationService : IEmployeeSeparationService
{
    private readonly HRMSContext _context; // Replace with your actual DbContext

    public EmployeeSeparationService(HRMSContext context)
    {
        _context = context;
    }

    public async Task<bool> CreateEmployeeSeparationAsync(EmployeeSeparationDto model)
    {
        try
        {
            // Validate EmployeeId exists
            var employeeExists = await _context.tblEmployees.AnyAsync(e => e.EmployeeId == model.EmployeeId);
            if (!employeeExists)
            {
                throw new Exception("Employee does not exist.");
            }

            var employeeName = await _context.tblEmployees
                .Where(e => e.EmployeeId == model.EmployeeId)
                .Select(e => e.FULL_NAME)
                .FirstOrDefaultAsync();

            var hasPendingSeparation = await _context.tblEmployeeSeprations
                .AnyAsync(s => s.EmployeeId == model.EmployeeId && (s.IsRevoked == false || s.IsRevoked == null)
                && (s.IsApprovedByManager == null || s.IsApprovedByManager == false));

            if (hasPendingSeparation)
            {
                throw new Exception("A separation request is already submitted and pending processing.");
            }

            // Validate ResignationType exists and get ResignationTypeId
            var resignationType = await _context.tblResignationTypes
                .FirstOrDefaultAsync(r => r.ResignationTypeId == model.ResignationTypeId);
            if (resignationType == null)
            {
                throw new Exception("Invalid Resignation Type.");
            }

            // Map model to entity
            var separation = new tblEmployeeSepration
            {
                EmployeeId = model.EmployeeId,
                LastDay = model.LastDay,
                NoticePeriod = model.NoticePeriod,
                ResignationTypeId = resignationType.ResignationTypeId,
                ResignationDate = model.ResignationDate,
                Remarks = model.Remarks,
                IsApprovedByManager = model.IsApprovedByManager,
                //IsApprovedByManager = (bool)model.IsApprovedByManager
                CreatedBy = employeeName,
                LastUpdatedBy = employeeName,
                LastUpdatedOn = DateTime.UtcNow,
            };

            // Add to database
            _context.tblEmployeeSeprations.Add(separation);
            await _context.SaveChangesAsync();

            return true;
        }
        catch (Exception)
        {
            throw; // Rethrow to be handled by controller
        }
    }
    public async Task<List<EmployeeSeparationResponseDto>> GetEmployeeSeparationsAsync(long empId, CancellationToken ct = default)
    {
        var result = new List<EmployeeSeparationResponseDto>();

        // ✅ Fetch resignation types first (before opening the reader)
        var resignationTypes = await _context.tblResignationTypes
            .AsNoTracking()
            .ToDictionaryAsync(rt => rt.ResignationTypeId, rt => rt.ResignationTypeName, ct);

        await using var connection = _context.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "dbo.usp_GetEmployeeSeparations";
        command.CommandType = CommandType.StoredProcedure;

        // Parameter(s)
        var pEmpId = command.CreateParameter();
        pEmpId.ParameterName = "@EmpId";
        pEmpId.DbType = DbType.Int64;
        pEmpId.Value = empId;
        command.Parameters.Add(pEmpId);

        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(ct);

        await using var reader = await command.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            int employeeSeprationId = reader.GetInt32(reader.GetOrdinal("EmployeeSeprationId"));

            long employeeId = reader.GetInt64(reader.GetOrdinal("EmployeeId"));

            DateTime? lastDay = reader.IsDBNull(reader.GetOrdinal("LastDay"))
                ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("LastDay"));

            int? noticePeriod = reader.IsDBNull(reader.GetOrdinal("NoticePeriod"))
                ? (int?)null : reader.GetInt32(reader.GetOrdinal("NoticePeriod"));

            int resignationTypeId = reader.GetInt32(reader.GetOrdinal("ResignationTypeId"));

            DateTime? resignationDate = reader.IsDBNull(reader.GetOrdinal("ResignationDate"))
                ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("ResignationDate"));

            string remarks = reader.IsDBNull(reader.GetOrdinal("Remarks"))
                ? string.Empty : reader.GetString(reader.GetOrdinal("Remarks"));

            bool? isApprovedByManager = reader.IsDBNull(reader.GetOrdinal("IsApprovedByManager"))
                ? (bool?)null : reader.GetBoolean(reader.GetOrdinal("IsApprovedByManager"));

            bool? isApprovedByHR = reader.IsDBNull(reader.GetOrdinal("IsApprovedByHR"))
                ? (bool?)null : reader.GetBoolean(reader.GetOrdinal("IsApprovedByHR"));

            bool? isRevoked = reader.IsDBNull(reader.GetOrdinal("IsRevoked"))
                ? (bool?)null : reader.GetBoolean(reader.GetOrdinal("IsRevoked"));

            string reportingHeadStatus = isApprovedByManager == true ? "Approved"
                : isApprovedByManager == false ? "Rejected"
                : "Pending";

            string hrStatus = isApprovedByHR == true ? "Approved"
                : isApprovedByHR == false ? "Rejected"
                : "Pending";

            string status = isRevoked == true ? "Revoked"
                : (isApprovedByManager == true && isApprovedByHR == true) ? "Approved"
                : (isApprovedByManager == false || isApprovedByHR == false) ? "Rejected"
                : "Pending";

            decimal earnedLeaveBalance = reader.IsDBNull(reader.GetOrdinal("EarnedLeaveBalance"))
                ? 0m : reader.GetDecimal(reader.GetOrdinal("EarnedLeaveBalance"));

            result.Add(new EmployeeSeparationResponseDto
            {
                EmployeeSeprationId = employeeSeprationId,
                EmpId = employeeId,
                LastDay = (DateTime)lastDay,
                NoticePeriod = (int)noticePeriod,
                ResignationType = resignationTypes.TryGetValue(resignationTypeId, out var typeName) ? typeName : "Unknown",
                ResignationDate = (DateTime)resignationDate,
                Remarks = remarks,
                IsApprovedByManager = isApprovedByManager,
                IsApprovedByHR = isApprovedByHR,
                IsRevoked = isRevoked ?? false,
                ReportingHeadStatus = reportingHeadStatus,
                HRStatus = hrStatus,
                Status = status,
                EarnedLeaveBalance = earnedLeaveBalance
            });
        }

        return result;
    }


    public async Task<List<EmployeeSeparationResponseDto>> GetEmployeeSeparationsByManagerIdAsync(long empId)
    {
        try
        {
            var separations = await _context.tblEmployeeSeprations
                .Where(s => s.EmployeeId == empId)
                .Join(
                    _context.tblResignationTypes,
                    separation => separation.ResignationTypeId,
                    resignationType => resignationType.ResignationTypeId,
                    (separation, resignationType) => new EmployeeSeparationResponseDto
                    {
                        EmployeeSeprationId = separation.EmployeeSeprationId,
                        EmpId = separation.EmployeeId,
                        LastDay = (DateTime)separation.LastDay,
                        NoticePeriod = (int)separation.NoticePeriod,
                        ResignationType = resignationType.ResignationTypeName,
                        ResignationDate = (DateTime)separation.ResignationDate,
                        Remarks = separation.Remarks,
                        IsApprovedByManager = (bool)separation.IsApprovedByManager,
                        IsApprovedByHR = separation.IsApprovedByHR,
                    })
                .ToListAsync();

            return separations;
        }
        catch (Exception)
        {
            throw; // Rethrow to be handled by controller
        }
    }
    //public async Task<(List<EmployeeSeparationResponseDto> PaginatedResignations, int TotalCount)> GetResignationsByManagerAsync(long? managerId, int pageNumber, int pageSize, string searchTerm)
    //{
    //    try
    //    {
    //        IQueryable<EmployeeSeparationResponseDto> query = _context.tblEmployeeSeprations
    //            .Join(
    //                _context.tblResignationTypes,
    //                separation => separation.ResignationTypeId,
    //                resignationType => resignationType.ResignationTypeId,
    //                (separation, resignationType) => new { separation, resignationType })
    //            .Join(
    //                _context.tblEmployees,
    //                combined => combined.separation.EmployeeId,
    //                employee => employee.EmployeeId,
    //                (combined, employee) => new EmployeeSeparationResponseDto
    //                {
    //                    EmployeeSeprationId = combined.separation.EmployeeSeprationId,
    //                    EmpId = Convert.ToInt32(combined.separation.EmployeeId),
    //                    LastDay = (DateTime)combined.separation.LastDay,
    //                    NoticePeriod = (int)combined.separation.NoticePeriod,
    //                    ResignationType = combined.resignationType.ResignationTypeName,
    //                    ResignationDate = (DateTime)combined.separation.ResignationDate,
    //                    Remarks = combined.separation.Remarks,
    //                    IsApprovedByManager = (bool)combined.separation.IsApprovedByManager,
    //                    IsApprovedByHR = combined.separation.IsApprovedByHR,
    //                    ReportHeadEcode = employee.ReportHeadEcode,
    //                    Status = combined.separation.IsRevoked == true
    //                    ? "Revoked"
    //                    : combined.separation.IsApprovedByManager == true && combined.separation.IsApprovedByHR == true
    //                        ? "Approved"
    //                        : combined.separation.IsApprovedByManager == false || combined.separation.IsApprovedByHR == false
    //                            ? "Rejected"
    //                            : "Pending",
    //                    ReportingHeadStatus = combined.separation.IsRevoked == true
    //                    ? "Revoked"
    //                    : combined.separation.IsApprovedByManager == null
    //                    ? "Pending"
    //                    : combined.separation.IsApprovedByManager == true
    //                    ? "Approved"
    //                    : "Rejected",

    //                    FullName = employee.FULL_NAME,
    //                    Firstname = employee.FirstName,
    //                    LastName = employee.LastName,
    //                    Email = employee.EMAIL_ADDRESS,
    //                    Ecode = employee.Ecode,
    //                    Ename = employee.FULL_NAME,

    //                });

    //        if (managerId.HasValue)
    //        {
    //            var managerEmployee = await _context.tblEmployees
    //                .FirstOrDefaultAsync(e => e.EmployeeId == managerId.Value);

    //            if (managerEmployee == null)
    //            {
    //                return (new List<EmployeeSeparationResponseDto>(), 0);
    //            }

    //            var reportingEmployees = await _context.tblEmployees
    //                .Where(e => e.ReportHeadEcode == managerEmployee.Ecode)
    //                .Select(e => e.EmployeeId)
    //                .ToListAsync();

    //            query = query.Where(s => reportingEmployees.Contains(Convert.ToInt32(s.EmpId)));
    //        }

    //        // Apply search filter across all columns if searchTerm is provided
    //        if (!string.IsNullOrWhiteSpace(searchTerm))
    //        {
    //            searchTerm = searchTerm.ToLower();
    //            query = query.Where(s =>
    //                s.EmployeeSeprationId.ToString().Contains(searchTerm) ||
    //                s.EmpId.ToString().Contains(searchTerm) || s.Firstname.ToString().Contains(searchTerm) ||
    //                s.LastName.ToString().Contains(searchTerm) || s.FullName.ToString().Contains(searchTerm)
    //                || s.Email.ToString().Contains(searchTerm) || s.NoticePeriod.ToString().Contains(searchTerm) ||
    //                  (s.ResignationType != null && s.ResignationType.ToLower().Contains(searchTerm)) ||

    //                (s.Remarks != null && s.Remarks.ToLower().Contains(searchTerm)) ||
    //                s.IsApprovedByManager.ToString().ToLower().Contains(searchTerm) ||
    //                (s.ReportHeadEcode != null && s.ReportHeadEcode.ToString().Contains(searchTerm)));
    //        }

    //        // Get total count before pagination
    //        int totalCount = await query.CountAsync();

    //        // Apply pagination
    //        query = query.Skip((pageNumber - 1) * pageSize).Take(pageSize); var resignations = await query.ToListAsync();
    //        return (resignations, totalCount);
    //    }
    //    catch (Exception)
    //    {
    //        throw;
    //    }
    //}
    public async Task<(List<EmployeeSeparationResponseDto>? Data,
                  int TotalCount,
                  byte[]? ExcelBytes)> GetResignationsByManagerAsync(
    long? managerId,
    int pageNumber,
    int pageSize,
    string searchTerm,
    bool isExcel)
    {
        IQueryable<EmployeeSeparationResponseDto> query = _context.tblEmployeeSeprations
            .Join(_context.tblResignationTypes,
                s => s.ResignationTypeId,
                r => r.ResignationTypeId,
                (s, r) => new { s, r })
            .Join(_context.tblEmployees,
                x => x.s.EmployeeId,
                e => e.EmployeeId,
                (x, e) => new EmployeeSeparationResponseDto
                {
                    EmployeeSeprationId = x.s.EmployeeSeprationId,
                    EmpId = (int)x.s.EmployeeId,
                    ResignationType = x.r.ResignationTypeName,
                    ResignationDate = x.s.ResignationDate,
                    LastDay = (DateTime)x.s.LastDay,
                    FullName = e.FULL_NAME,
                    Email = e.EMAIL_ADDRESS,
                    Ecode = e.Ecode,
                    ReportHeadEcode = e.ReportHeadEcode,
                    IsApprovedByManager = x.s.IsApprovedByManager,
                    IsApprovedByHR = x.s.IsApprovedByHR,
                    ManagerRemarks = x.s.ManagerRemarks,
                    ReportingHeadStatus = x.s.IsApprovedByManager == true ? "Approved"
                        : x.s.IsApprovedByManager == false ? "Rejected"
                        : "Pending",
                    Status =
                        x.s.IsRevoked == true ? "Revoked" :
                        x.s.IsApprovedByManager == true && x.s.IsApprovedByHR == true ? "Approved" :
                        x.s.IsApprovedByManager == false || x.s.IsApprovedByHR == false ? "Rejected" :
                        "Pending"
                });

        // Manager filter
        if (managerId.HasValue)
        {
            var managerEcode = await _context.tblEmployees
                .Where(e => e.EmployeeId == managerId)
                .Select(e => e.Ecode)
                .FirstOrDefaultAsync();

            query = query.Where(x => x.ReportHeadEcode == managerEcode);
        }

        // Search
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            searchTerm = searchTerm.ToLower();
            query = query.Where(x =>
                x.FullName.ToLower().Contains(searchTerm) ||
                x.Ecode.ToLower().Contains(searchTerm) ||
                x.Email.ToLower().Contains(searchTerm));
        }
        int totalCount = await query.CountAsync();

        // 👉 EXCEL PATH (NO PAGINATION)
        if (isExcel)
        {
            var allData = await query.ToListAsync();
            var excelBytes = GenerateExcel(allData);

            return (null, totalCount, excelBytes);
        }

        // 👉 NORMAL LIST PATH
        var pagedData = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (pagedData, totalCount, null);
    }
    private byte[] GenerateExcel(List<EmployeeSeparationResponseDto> data)
    {
        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var ws = workbook.Worksheets.Add("Resignations");

        // 🔹 Headers
        ws.Cell(1, 1).Value = "Ecode";
        ws.Cell(1, 2).Value = "Name";
        ws.Cell(1, 3).Value = "Email";
        ws.Cell(1, 4).Value = "Resignation Type";
        ws.Cell(1, 5).Value = "Last Day";
        ws.Cell(1, 6).Value = "Report Head";
        ws.Cell(1, 7).Value = "Report Head Status";
        ws.Cell(1, 8).Value = "Status";

        // 🔹 Data
        int row = 2;
        foreach (var item in data)
        {
            ws.Cell(row, 1).Value = item.Ecode;
            ws.Cell(row, 2).Value = item.FullName;
            ws.Cell(row, 3).Value = item.Email;
            ws.Cell(row, 4).Value = item.ResignationType;
            ws.Cell(row, 5).Value = item.LastDay.ToString("yyyy-MM-dd");
            ws.Cell(row, 6).Value = item.ReportHeadEcode;
            ws.Cell(row, 7).Value = item.ReportingHeadStatus;
            ws.Cell(row, 8).Value = item.Status;

            row++;
        }

        // 🔹 Formatting
        ws.Range(1, 1, 1, 8).Style.Font.Bold = true;
        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<bool> ProcessSeparationActionAsync(int employeeSeprationId, long userId, string actionType, string remarks, string role, DateTime lastDay, string employeeId)
    {
        try
        {
            role = role?.ToLower();
            actionType = actionType?.ToLower();

            var separation = await _context.tblEmployeeSeprations
                .Include(s => s.Employee)
                .FirstOrDefaultAsync(s => s.EmployeeSeprationId == employeeSeprationId);

            if (separation == null)
            {
                throw new Exception("Separation request not found.");
            }

            var employeeCode = _context.tblEmployees
                .Where(e => e.EmployeeId == userId)
                .Select(e => e.Ecode)
                .FirstOrDefault();

            if (string.IsNullOrEmpty(employeeCode))
            {
                throw new Exception("Employee code not found for the user.");
            }
            // Check if resignation is revoked or processed by HR
            if (separation.IsRevoked == true || (separation.IsRevoked != null && separation.IsRevoked != false))
            {
                throw new Exception("Cannot process: Resignation has already been revoked.");
            }

            if (separation.IsApprovedByHR != null)
            {
                throw new Exception("Cannot process: Resignation has already been processed by HR.");
            }

            if (actionType == "revoke")
            {
                // Validate that the user is the employee who submitted the resignation
                if (separation.EmployeeId != userId)
                {
                    throw new Exception("Unauthorized: Only the employee who submitted the resignation can revoke it.");
                }

                // Check if the resignation is already approved by manager
                if (separation.IsApprovedByManager == true)
                {
                    throw new Exception("Cannot revoke: Resignation has already been approved by manager.");
                }

                // Mark as revoked and store remarks
                separation.IsRevoked = true;
                separation.ManagerRemarks = remarks ?? "Revoked by employee";
                separation.LastDay = lastDay;
                separation.LastUpdatedOn = DateTime.UtcNow;
                separation.LastUpdatedBy = employeeCode;
                _context.tblEmployeeSeprations.Update(separation);
            }
            else if (actionType == "approve" || actionType == "rejected")
            {
                if (role == "hr")
                {
                    // HR action: Validate role and manager approval
                    if (role != "hr")
                    {
                        throw new Exception("Unauthorized: Only HR personnel can approve/reject this request.");
                    }

                    if (separation.IsApprovedByManager != true)
                    {
                        throw new Exception("Cannot process: Manager approval is required before HR action.");
                    }

                    // Update HR approval status and remarks
                    separation.IsApprovedByHR = actionType == "approve";
                    separation.HRRemarks = remarks;
                    separation.LastDay = lastDay;
                    separation.LastUpdatedOn = DateTime.UtcNow;
                    separation.LastUpdatedBy = employeeCode;
                    _context.tblEmployeeSeprations.Update(separation);
                }
                else
                {
                    // Manager action: Validate reporting hierarchy, no role check
                    var managerEmployee = await _context.tblEmployees
                        .FirstOrDefaultAsync(e => e.EmployeeId == userId);

                    if (managerEmployee == null)
                    {
                        throw new Exception("Manager not found.");
                    }

                    if (separation.Employee?.ReportHeadEcode != managerEmployee.Ecode)
                    {
                        throw new Exception("Unauthorized: Manager does not have permission to approve/reject this request.");
                    }

                    // Update manager approval status and remarks
                    separation.IsApprovedByManager = actionType == "approve";
                    separation.ManagerRemarks = remarks;
                    separation.LastDay = lastDay;
                    separation.LastUpdatedOn = DateTime.UtcNow;
                    separation.LastUpdatedBy = employeeCode;
                    separation.IsActive = actionType == "approve" ? false : true;

                    _context.tblEmployeeSeprations.Update(separation);
                }
            }
            else
            {
                throw new Exception("Invalid action type. Use 'Approve', 'Rejected', or 'Revoke'.");
            }

            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception)
        {
            throw;
        }
    }
    public async Task<EmployeeSeparationResponseSDto?> GetEmployeeSeparationByIdAsync(
       int separationId,
       CancellationToken ct = default)
    {
        // Preload resignation types (Id -> Name)
        var resignationTypes = await _context.tblResignationTypes
            .AsNoTracking()
            .ToDictionaryAsync(rt => rt.ResignationTypeId, rt => rt.ResignationTypeName, ct); // [14]

        await using var connection = _context.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "dbo.usp_GetEmployeeSeparationById";
        command.CommandType = CommandType.StoredProcedure;

        var pSepId = command.CreateParameter();
        pSepId.ParameterName = "@SeparationId";
        pSepId.DbType = DbType.Int32;
        pSepId.Value = separationId;
        command.Parameters.Add(pSepId);

        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(ct);

        await using var reader = await command.ExecuteReaderAsync(ct); // [3][5]

        if (!await reader.ReadAsync(ct))
            return null;

        // Ordinals once for performance
        int oEmployeeSeprationId = reader.GetOrdinal("EmployeeSeprationId");
        int oEmployeeId = reader.GetOrdinal("EmployeeId");
        int oFirstName = reader.GetOrdinal("FirstName");
        int oLastName = reader.GetOrdinal("LastName");
        int oFullName = reader.GetOrdinal("FullName");
        int oJoinDate = reader.GetOrdinal("JoinDate");
        int oDepartmentId = reader.GetOrdinal("DepartmentId");
        int oDepartmentName = reader.GetOrdinal("DepartmentName");
        int oReportingHeadEcode = reader.GetOrdinal("ReportingHeadEcode");
        int oReportingHeadName = reader.GetOrdinal("ReportingHeadName");
        int oLastDay = reader.GetOrdinal("LastDay");
        int oNoticePeriod = reader.GetOrdinal("NoticePeriod");
        int oResignationTypeId = reader.GetOrdinal("ResignationTypeId");
        int oResignationDate = reader.GetOrdinal("ResignationDate");
        int oRemarks = reader.GetOrdinal("Remarks");
        int oIsApprovedByManager = reader.GetOrdinal("IsApprovedByManager");
        int oIsApprovedByHR = reader.GetOrdinal("IsApprovedByHR");
        int oManagerRemarks = reader.GetOrdinal("ManagerRemarks");
        int oIsRevoked = reader.GetOrdinal("IsRevoked");
        int oEarnedLeaveBalance = reader.GetOrdinal("EarnedLeaveBalance");

        // Safe reads with IsDBNull checks. [12][6]
        int id = reader.GetInt32(oEmployeeSeprationId);
        long employeeId = reader.IsDBNull(oEmployeeId) ? 0L : reader.GetInt64(oEmployeeId);

        string? firstName = reader.IsDBNull(oFirstName) ? null : reader.GetString(oFirstName);
        string? lastName = reader.IsDBNull(oLastName) ? null : reader.GetString(oLastName);
        string? fullName = reader.IsDBNull(oFullName) ? null : reader.GetString(oFullName);

        DateTime? joinDate = reader.IsDBNull(oJoinDate) ? (DateTime?)null : reader.GetDateTime(oJoinDate);

        int? departmentId = reader.IsDBNull(oDepartmentId) ? (int?)null : reader.GetInt32(oDepartmentId);
        string? departmentName = reader.IsDBNull(oDepartmentName) ? null : reader.GetString(oDepartmentName);

        string? reportingHeadEcode = reader.IsDBNull(oReportingHeadEcode) ? null : reader.GetString(oReportingHeadEcode);
        string? reportingHeadName = reader.IsDBNull(oReportingHeadName) ? null : reader.GetString(oReportingHeadName);

        DateTime? lastDay = reader.IsDBNull(oLastDay) ? (DateTime?)null : reader.GetDateTime(oLastDay);
        int? noticePeriod = reader.IsDBNull(oNoticePeriod) ? (int?)null : reader.GetInt32(oNoticePeriod);

        int resignationTypeId = reader.IsDBNull(oResignationTypeId) ? 0 : reader.GetInt32(oResignationTypeId);
        string resignationType = resignationTypes.TryGetValue(resignationTypeId, out var rtName) ? rtName : "Unknown"; // [14]

        DateTime? resignationDate = reader.IsDBNull(oResignationDate) ? (DateTime?)null : reader.GetDateTime(oResignationDate);

        string remarks = reader.IsDBNull(oRemarks) ? string.Empty : reader.GetString(oRemarks);

        bool? isApprovedByManager = reader.IsDBNull(oIsApprovedByManager) ? (bool?)null : reader.GetBoolean(oIsApprovedByManager);
        bool? isApprovedByHR = reader.IsDBNull(oIsApprovedByHR) ? (bool?)null : reader.GetBoolean(oIsApprovedByHR);
        bool isRevoked = !reader.IsDBNull(oIsRevoked) && reader.GetBoolean(oIsRevoked);

        string managerRemarks = reader.IsDBNull(oManagerRemarks) ? string.Empty : reader.GetString(oManagerRemarks);

        decimal earnedLeaveBalance = reader.IsDBNull(oEarnedLeaveBalance) ? 0m : reader.GetDecimal(oEarnedLeaveBalance);

        string reportingHeadStatus = isApprovedByManager == true ? "Approved"
            : isApprovedByManager == false ? "Rejected" : "Pending"; // [5]

        string hrStatus = isApprovedByHR == true ? "Approved"
            : isApprovedByHR == false ? "Rejected" : "Pending"; // [5]

        string status = isRevoked ? "Revoked"
            : (isApprovedByManager == true && isApprovedByHR == true) ? "Approved"
            : (isApprovedByManager == false || isApprovedByHR == false) ? "Rejected"
            : "Pending"; // [5]

        return new EmployeeSeparationResponseSDto
        {
            Id = id,
            EmployeeId = employeeId,
            FirstName = firstName,
            LastName = lastName,
            FullName = fullName,
            JoinDate = joinDate,
            DepartmentId = departmentId,
            DepartmentName = departmentName,
            ReportingHeadEcode = reportingHeadEcode,
            ReportingHeadName = reportingHeadName,
            LastDay = (DateTime)lastDay,
            NoticePeriod = (int)noticePeriod,
            ResignationTypeId = resignationTypeId,
            ResignationType = resignationType,
            ResignationDate = (DateTime)resignationDate,
            Remarks = remarks,
            IsApprovedByManager = isApprovedByManager ?? false,
            IsApprovedByHR = isApprovedByHR,
            ManagerRemarks = managerRemarks,
            IsRevoked = isRevoked,
            ReportingHeadStatus = reportingHeadStatus,
            HRStatus = hrStatus,
            Status = status,
            EarnedLeaveBalance = earnedLeaveBalance
        };
    }


}


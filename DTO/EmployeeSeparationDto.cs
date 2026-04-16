public class EmployeeSeparationDto
{
    public long EmployeeId { get; set; } 
    public DateTime LastDay { get; set; }
    public int NoticePeriod { get; set; } 
    public int ResignationTypeId { get; set; }
    public DateTime ResignationDate { get; set; }
    public string? Remarks { get; set; }
    public bool? IsApprovedByManager { get; set; }
    public string? ManagerRemarks { get; set; }
    public bool IsRevoked { get; set; }
}
public class ApproveSeparationDto
{
    public long EmployeeSeprationId { get; set; }
    public long ManagerId { get; set; }
    public bool IsApproved { get; set; }
    public string ManagerRemarks { get; set; }
}


public class ProcessSeparationActionDto
{
    public int EmployeeSeprationId { get; set; }
    public long UserId { get; set; }
    public string ActionType { get; set; } // "Approve", "Rejected", "Revoke"
    public string Remarks { get; set; }
    public DateTime LastDay { get; set; }
}
public class EmployeeSeparationResponseSDto
{
    public int Id { get; set; }
    public long EmployeeId { get; set; }

    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? FullName { get; set; }

    public DateTime? JoinDate { get; set; }

    public int? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }

    public string? ReportingHeadEcode { get; set; }
    public string? ReportingHeadName { get; set; }

    public DateTime? LastDay { get; set; }
    public int? NoticePeriod { get; set; }

    public int ResignationTypeId { get; set; }
    public DateTime? ResignationDate { get; set; }

    public string Remarks { get; set; } = string.Empty;

    public bool? IsApprovedByManager { get; set; }
    public string ManagerRemarks { get; set; } = string.Empty;
    public bool? IsRevoked { get; set; }

    public decimal? EarnedLeaveBalance { get; set; }
    public bool? IsApprovedByHR { get; set; }

    // Optional convenience fields, if needed:
    public string? ResignationType { get; set; }
    public string? ReportingHeadStatus { get; set; }
    public string? HRStatus { get; set; }
    public string? Status { get; set; }
}

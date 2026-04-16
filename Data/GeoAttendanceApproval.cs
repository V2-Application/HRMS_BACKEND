using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HRMSAPI.Data;

public class GeoAttendanceApproval
{
    [Key]
    public int Id { get; set; }

    public long EmployeeId { get; set; }

    [Column(TypeName = "date")]
    public DateTime PunchDate { get; set; }

    // Level 1: Manager Approval
    public int ManagerApprovalStatusId { get; set; } = 4;   // 4=Pending
    [StringLength(100)]
    public string? ManagerApproverId { get; set; }
    public DateTime? ManagerApprovalOn { get; set; }
    [StringLength(500)]
    public string? ManagerRemarks { get; set; }

    // Level 2: Master Approval
    public int MasterApprovalStatusId { get; set; } = 4;    // 4=Pending
    [StringLength(100)]
    public string? MasterApproverId { get; set; }
    public DateTime? MasterApprovalOn { get; set; }
    [StringLength(500)]
    public string? MasterRemarks { get; set; }

    // Final computed status: 4=Pending, 5=ManagerApproved, 1=Approved, 2=Rejected
    public int FinalStatusId { get; set; } = 4;

    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    public DateTime? LastUpdatedOn { get; set; }

    // Navigation properties
    [ForeignKey("EmployeeId")]
    public virtual tblEmployee Employee { get; set; }

    [ForeignKey("FinalStatusId")]
    public virtual tblStatus FinalStatus { get; set; }

    [ForeignKey("ManagerApprovalStatusId")]
    public virtual tblStatus ManagerApprovalStatus { get; set; }

    [ForeignKey("MasterApprovalStatusId")]
    public virtual tblStatus MasterApprovalStatus { get; set; }
}

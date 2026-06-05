using System;
using System.ComponentModel.DataAnnotations;

namespace HRMSAPI.DTO
{
    public class DepartmentUpsertDto
    {
        public int? DepartmentId { get; set; }

        [Required(ErrorMessage = "Department name is required.")]
        [StringLength(200, ErrorMessage = "Department name max 200 chars.")]
        public string DepartmentName { get; set; }
    }

    public class DepartmentResponseDto
    {
        public int DepartmentId { get; set; }
        public string DepartmentName { get; set; }
        public string DepartmentCode { get; set; }
        public bool IsActive { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? CreatedOn { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? UpdatedOn { get; set; }
        public long? UpdatedBy { get; set; }
    }

    public class DesignationUpsertDto
    {
        public int? DesignationId { get; set; }

        [Required(ErrorMessage = "Designation name is required.")]
        [StringLength(200, ErrorMessage = "Designation name max 200 chars.")]
        public string DesignationName { get; set; }
    }

    public class DesignationResponseDto
    {
        public int DesignationId { get; set; }
        public string DesignationName { get; set; }
        public string DesignationCode { get; set; }
        public bool IsActive { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? CreatedOn { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? UpdatedOn { get; set; }
        public long? UpdatedBy { get; set; }
    }

    public class ToggleActiveStatusDto
    {
        public int Id { get; set; }
        public bool IsActive { get; set; }
    }

    // --- Sub-Department (3-level hierarchy under a department) ---
    // A node's parent is the department (L1) or another sub-department (L2/L3).
    public class SubDepartmentUpsertDto
    {
        public int? SubDepartmentId { get; set; }

        [Required(ErrorMessage = "Sub-department name is required.")]
        [StringLength(200, ErrorMessage = "Sub-department name max 200 chars.")]
        public string SubDepartmentName { get; set; }

        [StringLength(10, ErrorMessage = "Sub-department code max 10 chars.")]
        public string? SubDepartmentCode { get; set; }

        // Root department the chain belongs to (carried on every level).
        public int DepartmentId { get; set; }

        // NULL for level 1; the parent sub-department's id for levels 2 and 3.
        public int? ParentSubDepartmentId { get; set; }

        // 1, 2, or 3.
        public int DepthLevel { get; set; }
    }

    public class SubDepartmentResponseDto
    {
        public int SubDepartmentId { get; set; }
        public string SubDepartmentName { get; set; }
        public string SubDepartmentCode { get; set; }
        public int DepartmentId { get; set; }
        public int? ParentSubDepartmentId { get; set; }
        public int DepthLevel { get; set; }
        public bool IsActive { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? CreatedOn { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? UpdatedOn { get; set; }
        public long? UpdatedBy { get; set; }
    }
}

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
}

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HRMSAPI.DTO
{
    /// <summary>
    /// A role as the Role Master page shows it. EmployeeCount is included so the
    /// page can warn before deactivating a role people still hold.
    /// </summary>
    public class RoleMasterDto
    {
        public int RoleId { get; set; }

        public string RoleName { get; set; }

        public string Description { get; set; }

        public bool IsActive { get; set; }

        public DateTime? CreatedOn { get; set; }

        public string CreatedBy { get; set; }

        public string LastUpdatedBy { get; set; }

        public int EmployeeCount { get; set; }

        public int CandidateCount { get; set; }
    }

    /// <summary>One row's outcome from the Role Master bulk upload.</summary>
    public class RoleUploadRowResult
    {
        /// <summary>Excel row number, so a problem row is easy to find.</summary>
        public int Row { get; set; }

        public string RoleName { get; set; }

        /// <summary>Created | Updated | Skipped | Error.</summary>
        public string Outcome { get; set; }

        public string Message { get; set; }
    }

    public class RoleUploadResultDto
    {
        public int Created { get; set; }
        public int Updated { get; set; }
        public int Skipped { get; set; }
        public List<RoleUploadRowResult> Rows { get; set; } = new List<RoleUploadRowResult>();
    }

    public class RoleMasterUpsertDto
    {
        public int RoleId { get; set; }

        [Required(ErrorMessage = "Role Name is required")]
        [StringLength(100, ErrorMessage = "Role Name cannot exceed 100 characters")]
        public string RoleName { get; set; }

        // Nullable on purpose: the project builds with nullable reference types
        // enabled, so a plain `string` here would be treated as REQUIRED by model
        // validation and every save without a description would be rejected.
        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;
    }
}

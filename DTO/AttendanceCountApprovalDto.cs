using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace HRMSAPI.DTO
{
    /// <summary>
    /// DTO for creating attendance count approval request
    /// Note: This is for JSON body when files are uploaded separately
    /// </summary>
    public class CreateAttendanceCountApprovalDto
    {
        [Required(ErrorMessage = "Employee code is required")]
        [StringLength(50, ErrorMessage = "Employee code cannot exceed 50 characters")]
        public string ECode { get; set; }

        [Required(ErrorMessage = "Month-Year is required")]
        [StringLength(10, ErrorMessage = "Month-Year cannot exceed 10 characters")]
        [RegularExpression(@"^(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)-\d{2}$", 
            ErrorMessage = "Month-Year must be in format MMM-YY (e.g., Jan-25)")]
        public string MonthYear { get; set; }

        [Required(ErrorMessage = "Attendance count is required")]
        [Range(0, 31, ErrorMessage = "Attendance count must be between 0 and 31")]
        public int AttendanceCount { get; set; }

        [StringLength(1000, ErrorMessage = "Remarks cannot exceed 1000 characters")]
        public string? EmployeeRemarks { get; set; }

        // For pre-uploaded files (URL-based)
        public List<AttachmentDto>? Attachments { get; set; }
    }

    /// <summary>
    /// DTO for creating attendance count approval request with file upload
    /// Used with [FromForm] for multipart/form-data
    /// </summary>
    public class CreateAttendanceCountApprovalWithFilesDto
    {
        [Required(ErrorMessage = "Employee code is required")]
        public string ECode { get; set; }

        [Required(ErrorMessage = "Month-Year is required")]
        [RegularExpression(@"^(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)-\d{2}$", 
            ErrorMessage = "Month-Year must be in format MMM-YY (e.g., Jan-25)")]
        public string MonthYear { get; set; }

        [Required(ErrorMessage = "Attendance count is required")]
        [Range(0, 31, ErrorMessage = "Attendance count must be between 0 and 31")]
        public int AttendanceCount { get; set; }

        public string? EmployeeRemarks { get; set; }

        // Physical files to upload
        public List<IFormFile>? Files { get; set; }
    }

    /// <summary>
    /// DTO for attachment files
    /// </summary>
    public class AttachmentDto
    {
        public string FileUrl { get; set; }
        public string? FileName { get; set; }
        public long? FileSize { get; set; }
    }

    /// <summary>
    /// DTO for CM approval action
    /// </summary>
    public class CMApprovalDto
    {
        [Required(ErrorMessage = "Approval ID is required")]
        public long AttendanceCountApprovalId { get; set; }

        [Required(ErrorMessage = "Approval decision is required")]
        public bool IsApproved { get; set; }

        [StringLength(1000, ErrorMessage = "Remarks cannot exceed 1000 characters")]
        public string? CMRemarks { get; set; }
    }

    /// <summary>
    /// DTO for RM approval action
    /// </summary>
    public class RMApprovalDto
    {
        [Required(ErrorMessage = "Approval ID is required")]
        public long AttendanceCountApprovalId { get; set; }

        [Required(ErrorMessage = "Approval decision is required")]
        public bool IsApproved { get; set; }

        [StringLength(1000, ErrorMessage = "Remarks cannot exceed 1000 characters")]
        public string? RMRemarks { get; set; }
    }

    /// <summary>
    /// DTO for attendance count approval response
    /// </summary>
    public class AttendanceCountApprovalResponseDto
    {
        public long AttendanceCountApprovalId { get; set; }
        public string ECode { get; set; }
        public string? EmployeeName { get; set; }
        public string MonthYear { get; set; } // Format: MMM-YY (e.g., Jan-25)
        public int AttendanceCount { get; set; }
        public string? EmployeeRemarks { get; set; }

        // CM Approval Details (Lower Level)
        public bool? IsCMApproved { get; set; } // NULL = Not Reviewed, false = Rejected, true = Approved
        public string? CMApprovedBy { get; set; }
        public DateTime? CMApprovedOn { get; set; }
        public string? CMRemarks { get; set; }

        // RM Approval Details (Upper Level - Can Override CM)
        public bool? IsRMApproved { get; set; } // NULL = Not Reviewed, false = Rejected, true = Approved
        public string? RMApprovedBy { get; set; }
        public DateTime? RMApprovedOn { get; set; }
        public string? RMRemarks { get; set; }

        // Calculated Status (Dynamic based on approvals)
        public string Status { get; set; }
        public string StatusDescription { get; set; }

        // Attachments
        public List<AttachmentResponseDto>? Attachments { get; set; }

        // Audit
        public string? CreatedBy { get; set; }
        public DateTime? CreatedOn { get; set; }
        public string? LastUpdatedBy { get; set; }
        public DateTime? UpdatedOn { get; set; }

        // Employee Details
        public string? DesignationName { get; set; }
        public string? DepartmentName { get; set; }
        public string? LocationName { get; set; }
    }

    /// <summary>
    /// DTO for attachment response
    /// </summary>
    public class AttachmentResponseDto
    {
        public long AttachmentId { get; set; }
        public string FileUrl { get; set; }
        public string? FileName { get; set; }
        public long? FileSize { get; set; }
        public DateTime? CreatedOn { get; set; }
    }

    /// <summary>
    /// DTO for paginated attendance count approval list
    /// </summary>
    public class PagedAttendanceCountApprovalDto
    {
        public List<AttendanceCountApprovalResponseDto> Data { get; set; }
        public int TotalRecords { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    /// <summary>
    /// Helper class for calculating attendance count approval status
    /// Status is calculated dynamically based on CM and RM approval flags
    /// </summary>
    public static class AttendanceCountApprovalStatusHelper
    {
        public static (string Status, string Description) CalculateStatus(bool? isCMApproved, bool? isRMApproved)
        {
            // RM is upper level, their decision is final
            if (isRMApproved == true)
                return ("Approved", "Approved by RM");
            
            if (isRMApproved == false)
                return ("Rejected", "Rejected by RM");
            
            // If RM hasn't reviewed yet
            if (isCMApproved == true)
                return ("Pending RM", "CM Approved, Pending RM Approval");
            
            if (isCMApproved == false)
                return ("Pending RM", "CM Rejected, Pending RM Review (RM can override)");
            
            // Both are null - initial state
            return ("Pending CM", "Pending CM Review");
        }
    }
}


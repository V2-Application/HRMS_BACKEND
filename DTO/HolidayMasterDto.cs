using System;

namespace HRMSAPI.DTO
{
    public class HolidayMasterUpsertDto
    {
        public int? Id { get; set; }
        public int? LocationType { get; set; }
        public string LocationValue { get; set; }
        public string HolidayName { get; set; }
        public DateTime? HolidayDate { get; set; }
    }

    public class HolidayMasterResponseDto
    {
        public int Id { get; set; }
        public int? LocationType { get; set; }
        public string LocationTypeName { get; set; }
        public string LocationValue { get; set; }
        public string HolidayName { get; set; }
        public DateTime? HolidayDate { get; set; }
        public bool? IsActive { get; set; }
        public bool? IsDeleted { get; set; }
        public DateTime? CreatedOn { get; set; }
        public string CreatedBy { get; set; }
        public string UpdatedBy { get; set; }
        public DateTime? UpdatedOn { get; set; }
    }

    public class HolidayMasterUploadDto
    {
        public string LocationTypeName { get; set; }
        public string LocationValue { get; set; }
        public string HolidayName { get; set; }
        public DateTime? HolidayDate { get; set; }
    }
    public class LocationDesignationPolicyDto
    {
        public int? LocationDesignationPolicyId { get; set; }
        public string LocationCategoryId { get; set; }
        public int? DesignationId { get; set; }
        public string TotalAttendanceTo { get; set; }
        public decimal WeeklyOff { get; set; }
        public int? ForWhichWeeks { get; set; }
        public string MonthYear { get; set; }
        public decimal TotalAttendanceFrom { get; set; }
        //public decimal TotalAttendanceTo { get; set; }
        public bool? IsActive { get; set; }
    }
    public class LocationDesignationPolicyResponseDto
    {
        public int LocationDesignationPolicyId { get; set; }
        public string LocationCategoryId { get; set; }
        public string LocationCategoryName { get; set; }
        public int? DesignationId { get; set; }
        public string? DesignationName { get; set; }
        public string TotalAttendance { get; set; }
        public decimal WeeklyOff { get; set; }
        public int? ForWhichWeeks { get; set; }
        public string MonthYear { get; set; }
        public bool IsActive { get; set; }
        public decimal? TotalAttendanceFrom { get; set; }
        public decimal? TotalAttendanceTo { get; set; }
    }

    public class ToggleLocationDesignationPolicyStatusDto
    {
        public List<int> LocationDesignationPolicyIds { get; set; }
        public bool IsActive { get; set; }
    }
}

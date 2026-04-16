using ClosedXML.Excel;
using System.Text.Json.Serialization;

namespace HRMSAPI.DTO
{

    public class PunchFetchDto
    {
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; }
        public string ECode { get; set; }
        public DateTime? AttendanceDate { get; set; }
        public string MachineType { get; set; }
        public string DesignationName { get; set; }
        public string LocationName { get; set; }
        public string STCode { get; set; }
        public string DepartmentName { get; set; }
        public string Punch1 { get; set; }
        public string Punch2 { get; set; }
        public string Punch3 { get; set; }
        public string Punch4 { get; set; }
        public string Punch5 { get; set; }
        public string Punch6 { get; set; }
        public string Punch7 { get; set; }
        public string Punch8 { get; set; }
        public string Punch9 { get; set; }
        public string Punch10 { get; set; }
        public string Punch11 { get; set; }
        public string Punch12 { get; set; }
        public TimeSpan? PunchIn { get; set; }
        public TimeSpan? PunchOut { get; set; }
        public string? TotalWorkingHours { get; set; }
        public string? Status { get; set; }
        public string? RegularizePunchIn { get; internal set; }
        public string? RegularizePuncOut { get; internal set; }
        public bool? IsRegularize { get; set; } = false;
        public int? TotalWorkingDays { get; set; }
        public string? TotalWorkingMinutes { get; internal set; }
        public XLCellValue LateMinutes { get; internal set; }
        public XLCellValue EarlyMinutes { get; internal set; }
        public string? TotalMonthlyWorkingHours { get; internal set; }
    }
    public class EmployeePunchDto
    {
        [JsonPropertyName("machine_type")]
        public string Machine_Type { get; set; }
        public string UserID { get; set; }
        public DateTime PDate { get; set; }
        public string Punch1 { get; set; }
        public string Punch2 { get; set; }
        public string Punch3 { get; set; }
        public string Punch4 { get; set; }
        public string Punch5 { get; set; }
        public string Punch6 { get; set; }
        public string Punch7 { get; set; }
        public string Punch8 { get; set; }
        public string Punch9 { get; set; }
        public string Punch10 { get; set; }
        public string Punch11 { get; set; }
        public string Punch12 { get; set; }
        [JsonPropertyName("no_of_punches")]
        public int NoOfPunches { get; set; }
        [JsonPropertyName("total_hours")]
        public double TotalHours { get; set; }
    }
    public class AttendanceRangeGetDto
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string? ECode { get; set; }
    }
  
}

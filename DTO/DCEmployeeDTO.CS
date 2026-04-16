namespace HRMSAPI.DTO
{
    public class DCEmployeeDTO
    {
        public string? ECode { get; set; }
        public string? EmpName { get; set; }
        public string? EmailId { get; set; }
        public bool? IsContract { get; set; }
        public string? Designation { get; set; }
        public string? Department { get; set; }
        public string? SubDepartment { get; set; }
        public string? ReportingManagerName { get; set; }
        public string? RptPersonCard { get; set; }
        public string? ActInAct { get; set; }
    }
    public class DCLoginRequest
    {
        public string RptPersonCard { get; set; }
        public string Password { get; set; }
    }
}

namespace HRMSAPI.DTO
{
    public class EmployeeMultiPunchesChangeLogDto
    {
        public string Ecode { get; set; }
        public string UserID { get; set; }
        public DateTime PunchDate { get; set; }
        public string ColumnName { get; set; }
        public string OldValue { get; set; }
        public string NewValue { get; set; }
        public string VersionLabel { get; set; }
        public string ChangedBy { get; set; }
        public DateTime ChangedOn { get; set; }
    }
}


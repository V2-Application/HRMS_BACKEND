namespace HRMSAPI.DTO
{
    public class EmployeeChangeLogDto
    {
        public string Ecode { get; set; }
        public string ColumnName { get; set; }
        public string OldValue { get; set; }
        public string NewValue { get; set; }
        public string VersionLabel { get; set; }
        public string ChangedBy { get; set; }
        public DateTime ChangedOn { get; set; }
        public string ChangedIp { get; set; }   // system/device IP the change was made from
    }
}


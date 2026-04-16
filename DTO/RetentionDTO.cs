namespace HRMSAPI.DTO
{
    public class RetentionDTO
    {
        public int RetentionId { get; set; }
        public string LocCode { get; set; }
        public string Location { get; set; }
        public string Ecode { get; set; }
        public string Name { get; set; }
        public DateTime? JoiningDate { get; set; }
        public string EmpStatus { get; set; }
        public decimal RetBonus { get; set; }
        public DateTime? DateOfComplition { get; set; }
        public DateTime? RetentionStartDate { get; set; }
        public bool IsActive { get; set; }
        public bool? RetentionApplicable { get; set; }
    }
}

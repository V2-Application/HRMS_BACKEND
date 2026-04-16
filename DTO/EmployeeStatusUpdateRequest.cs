namespace HRMSAPI.DTO
{
    public class EmployeeStatusUpdateRequest
    {
        public long id { get; set; }
        public bool isactive { get; set; }
        public string remarks { get; set; }
        public DateTime? leavingDate { get; set; }
        public string? lastUpdatedBy { get; set; }
        public int? ResignationTypeId { get; set; } 
        public int? AbscondingReasonId { get; set; }
        public int? BlackListReasonId { get; set; }
    }
    public class EmployeeStatusUpdateWithReasonAndAttachmentRequest  : EmployeeStatusUpdateRequest { 
        public int? reasonid { get; set; }
        public List<IFormFile>? inactiveattachment { get; set; }
    }

}

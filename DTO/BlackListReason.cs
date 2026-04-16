namespace HRMSAPI.DTO
{
    public class BlackListReason
    {
        public int BlackListReasonId { get; set; }

        public int? ResignationTypeId { get; set; }

        public string BlacklListReasonName { get; set; }
        public ResignationTypeDto ResignationType { get; set; } // Navigation property
    }
}

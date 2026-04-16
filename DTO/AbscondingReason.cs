namespace HRMSAPI.DTO
{
    public class AbscondingReason
    {
        public int AbscondingReasonId { get; set; }

        public int? ResignationTypeId { get; set; }

        public string AbscondingReasonName { get; set; }
        public ResignationTypeDto ResignationType { get; set; } // Navigation property
    }
}

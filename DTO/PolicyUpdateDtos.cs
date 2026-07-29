namespace HRMSAPI.DTO
{
    // Update a single PTax (PTPolicyMaster) line item by Id. No inserts.
    // Strings are nullable so model binding does NOT auto-require them (nullable
    // reference types would otherwise make a blank Gender fail with "The Gender
    // field is required"); required-ness is enforced in the service with clear messages.
    public class PtaxUpdateDto
    {
        public int? Id { get; set; }   // null for new rows (Create); required for Update
        public string? State { get; set; }
        public decimal? SlabMin { get; set; }
        public decimal? SlabMax { get; set; }
        public decimal? PtRate { get; set; }
        public string? Frequency { get; set; }
        public string? Gender { get; set; }   // optional / can be blank
    }

    // Update a single LWF (LWFPolicyMaster) line item by Id. No inserts.
    public class LwfUpdateDto
    {
        public int? Id { get; set; }   // null for new rows (Create); required for Update
        public string? State { get; set; }
        public string? Frequency { get; set; }   // required (validated in service)
        public decimal? Employee { get; set; }
        public decimal? EmployeeMax { get; set; }
        public decimal? Employer { get; set; }      // maps to DB column Employeer
        public decimal? EmployerMax { get; set; }   // maps to DB column EmployeerMax
    }
}

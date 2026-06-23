namespace HRMSAPI.DTO
{
    // Update a single PTax (PTPolicyMaster) line item by Id. No inserts.
    public class PtaxUpdateDto
    {
        public int Id { get; set; }
        public string State { get; set; }
        public decimal? SlabMin { get; set; }
        public decimal? SlabMax { get; set; }
        public decimal? PtRate { get; set; }
        public string Frequency { get; set; }
        public string Gender { get; set; }   // optional / can be blank
    }

    // Update a single LWF (LWFPolicyMaster) line item by Id. No inserts.
    public class LwfUpdateDto
    {
        public int Id { get; set; }
        public string State { get; set; }
        public string Frequency { get; set; }   // required
        public decimal? Employee { get; set; }
        public decimal? EmployeeMax { get; set; }
        public decimal? Employer { get; set; }      // maps to DB column Employeer
        public decimal? EmployerMax { get; set; }   // maps to DB column EmployeerMax
    }
}

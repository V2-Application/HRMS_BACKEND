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

        // How payroll must READ the Employee / Employer figures above:
        //   "Flat"    -> a rupee amount per Frequency (Punjab 5/20, Goa 10/30, ...)
        //   "Percent" -> a percentage of earned gross, capped by the Max
        //                (Haryana 0.2% capped at 35 / 0.4% capped at 70)
        // Blank is treated as "Flat" so existing rows keep behaving as before.
        public string? CalcType { get; set; }
    }
}

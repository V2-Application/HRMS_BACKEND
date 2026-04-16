namespace HRMSAPI.DTO
{
    public class BgvListDTO
    {
        public long CandidateId { get; set; }
        public long? BgvId { get; set; }

        public long? AuditorId { get; set; }

        public string Name { get; set; }

        public string Email { get; set; }

        public DateTime DOB { get; set; }

        public string Designation { get; set; }

        public string Department { get; set; }

        public string Mobile { get; set; }

        public string Store { get; set; }

        public string Ecode { get; set; }
    }
}

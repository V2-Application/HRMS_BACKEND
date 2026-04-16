namespace HRMSAPI.DTO
{
    public class UpdateStatusDto
    {
        public long CandidateId { get; set; }
        public int StatusId { get; set; }
        //public string HRName { get; set; }
        //public DateTime CallDate { get; set; }
        //public TimeSpan CallStartTime { get; set; }
        //public TimeSpan CallEndTime { get; set; }
        //public string CallResponse { get; set; }
        public bool IsApplicant { get; set; }
        public bool IsApplicantApproved { get; set; } = false;
    }


    public class ChangePasswordDto
    {
        public string OldPassword { get; set; }
        public string NewPassword { get; set; }
    }

}

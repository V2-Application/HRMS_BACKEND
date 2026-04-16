namespace HRMSAPI.DTO
{

        public class ScheduleInterviewDto
        {
            public int ApplicantId { get; set; }
            public string CandidateName { get; set; }
            public string InterviewDateTime { get; set; }
            public string InterviewMode { get; set; }
            public string InterviewLocation { get; set; }
            public string Notes { get; set; }
            public int Round { get; set; }
            public List<long> Interviewers { get; set; } = new();
            public int LocationId { get; set; }
            public string CreatedBy { get; set; } = "";
    }
    }

public class ScheduleInterview
{
    public int ApplicantId { get; set; }
    public string CandidateName { get; set; }
    public DateTime InterviewDateTime { get; set; }
    public string InterviewMode { get; set; }
    public string InterviewLocation { get; set; }

    public List<InterviewRoundType> AllInterviewRounds { get; set; }




}


public class InterviewRoundType
{
    public int TempId { get; set; }
    public string Round { get; set; }
    public string Level { get; set; }
    public string Status { get; set; }
    public string Remark { get; set; }

    public List<InterviewerType> Interviewers { get; set; }

}

public class InterviewerType
{
    public int InterviewRoundTempId { get; set; }
    public string Name { get; set; }
    public string Feedback { get; set; }
}


public class UpdateInterviewerFeedbackRequest
{
    public int ScheduleId { get; set; }
    public string Feedback { get; set; }
    public string StatusName { get; set; }
 

}



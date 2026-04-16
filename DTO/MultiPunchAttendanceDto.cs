namespace HRMSAPI.DTO
{
    public class MultiPunchAttendanceDto
    {
        public string UserId { get; set; }
        public DateTime PunchDate { get; set; }
        public string Punch1 { get; set; }
        public string Punch2 { get; set; }
        public string Punch3 { get; set; }
        public string Punch4 { get; set; }
        public string Punch5 { get; set; }
        public string Punch6 { get; set; }
        public string Punch7 { get; set; }
        public string Punch8 { get; set; }
        public string Punch9 { get; set; }
        public string Punch10 { get; set; }
        public string Punch11 { get; set; }
        public string Punch12 { get; set; }
        public int NoOfPunches { get; set; }
        public string TotalHours { get; set; }
    }
}


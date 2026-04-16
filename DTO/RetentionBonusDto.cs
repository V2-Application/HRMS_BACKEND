using System;

namespace HRMSAPI.DTO
{
    public class RetentionBonusRequestDto
    {
        public string Ecode { get; set; }
        public DateTime RetentionStart { get; set; }
        public DateTime RetentionEnd { get; set; }
        public decimal Percentage { get; set; }
    }

    public class RetentionBonusStatusUpdateDto
    {
        public int RetentionId { get; set; }
        public bool Accepted { get; set; }
    }

    public class RetentionBonusResponseDto
    {
        public int RetentionId { get; set; }
        public string Ecode { get; set; }
        public DateTime LetterIssueDate { get; set; }
        public DateTime RetentionStart { get; set; }
        public DateTime RetentionEnd { get; set; }
        public decimal MonthlyGrossAtIssue { get; set; }
        public decimal Percentage { get; set; }
        public decimal BonusAmount { get; set; }
        public bool? Accepted { get; set; }
        public DateTime? AcceptedOn { get; set; }
    }
}


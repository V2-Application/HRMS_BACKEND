





namespace HRMSAPI.Implementation
{
    public class PasswordResetTokens : Data.PasswordResetToken
    {
        public long EmployeeId { get; set; }
        public string Token { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsUsed { get; set; }
    }
}
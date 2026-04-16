namespace HRMSAPI.Interfaces
{
    public interface IEmailService
    {
        Task<bool> SendEmailAsync(List<string> toList, List<string> ccList, string subject, string body);

        Task<bool> SendOfferLetterEmail(List<string> toList, List<string> ccList, string subject, string body, string attachmentPath = null);
    }
}

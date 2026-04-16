namespace HRMSAPI.Implementation
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Mail;
    using System.Threading.Tasks;
    using DocumentFormat.OpenXml.Vml;
    using HRMSAPI.Interfaces; // Single using statement for the interface
    using Microsoft.Extensions.Options;
    using MimeKit;
    using MailKit.Security;




    public class EmailService : IEmailService
    {
        private readonly EmailSettings _emailSettings;

        public EmailService(IOptions<EmailSettings> emailSettings)
        {
            _emailSettings = emailSettings.Value ?? throw new ArgumentNullException(nameof(emailSettings));
        }

        //public async Task<bool> SendEmailAsync(List<string> toList, List<string> ccList, string subject, string body)
        //{
        //    try
        //    {
        //        using (var smtpClient = new SmtpClient(_emailSettings.SmtpServer, _emailSettings.Port))
        //        {
        //            smtpClient.EnableSsl = _emailSettings.EnableSSL; // Use the setting from EmailSettings
        //            smtpClient.Credentials = new NetworkCredential(_emailSettings.SenderEmail, _emailSettings.SenderPassword);

        //            using (var mailMessage = new MailMessage())
        //            {
        //                // Add To recipients
        //                foreach (var to in toList.Where(email => !string.IsNullOrWhiteSpace(email)))
        //                {
        //                    mailMessage.To.Add(to);
        //                }

        //                // Add CC recipients
        //                if (ccList != null)
        //                {
        //                    foreach (var cc in ccList.Where(email => !string.IsNullOrWhiteSpace(email)))
        //                    {
        //                        mailMessage.CC.Add(cc);
        //                    }
        //                }

        //                mailMessage.From = new MailAddress(_emailSettings.SenderEmail); // Sender's email from settings
        //                mailMessage.Subject = subject;
        //                mailMessage.Body = body;
        //                mailMessage.IsBodyHtml = true; // Set to false if plain text is preferred

        //                await smtpClient.SendMailAsync(mailMessage);
        //            }
        //        }
        //        return true; // Success
        //    }
        //    catch (Exception ex)
        //    {
        //        // Log the exception if you have a logging framework (e.g., ILogger)
        //        Console.WriteLine($"Email sending failed: {ex.Message}");
        //        return false; // Failure
        //    }
        //}



        public async Task<bool> SendEmailAsync(List<string> toList, List<string> ccList, string subject, string body)
        {
            try
            {
                var email = new MimeMessage();
                email.From.Add(MailboxAddress.Parse(_emailSettings.SenderEmail));

                foreach (var to in toList.Where(email => !string.IsNullOrWhiteSpace(email)))
                {
                    email.To.Add(MailboxAddress.Parse(to));
                }

                if (ccList != null)
                {
                    foreach (var cc in ccList.Where(email => !string.IsNullOrWhiteSpace(email)))
                    {
                        email.Cc.Add(MailboxAddress.Parse(cc));
                    }
                }

                email.Subject = subject;
                email.Body = new TextPart("html") { Text = body };

                using var smtp = new MailKit.Net.Smtp.SmtpClient();

                try
                {
                    await smtp.ConnectAsync(_emailSettings.SmtpServer, _emailSettings.Port, SecureSocketOptions.SslOnConnect);
                    await smtp.AuthenticateAsync(_emailSettings.SenderEmail, _emailSettings.SenderPassword);
                    await smtp.SendAsync(email);
                    await smtp.DisconnectAsync(true);
                }
                catch (AuthenticationException authEx)
                {
                    Console.WriteLine($"Authentication failed: {authEx.Message}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"MailKit email send failed: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SendOfferLetterEmail( List<string> toList, List<string> ccList,string subject,string body,string attachmentPath = null)
        {
            try
            {
                var email = new MimeMessage();
                email.From.Add(MailboxAddress.Parse(_emailSettings.SenderEmail));

               
                foreach (var to in toList.Where(e => !string.IsNullOrWhiteSpace(e)))
                    email.To.Add(MailboxAddress.Parse(to));

                
                if (ccList != null)
                {
                    foreach (var cc in ccList.Where(e => !string.IsNullOrWhiteSpace(e)))
                        email.Cc.Add(MailboxAddress.Parse(cc));
                }

                email.Subject = subject;

               
                var builder = new BodyBuilder
                {
                    HtmlBody = body
                };

                
                if (!string.IsNullOrWhiteSpace(attachmentPath) && File.Exists(attachmentPath))
                {
                    builder.Attachments.Add(attachmentPath);
                }

                email.Body = builder.ToMessageBody();

                using var smtp = new MailKit.Net.Smtp.SmtpClient();
                await smtp.ConnectAsync(_emailSettings.SmtpServer, _emailSettings.Port, SecureSocketOptions.SslOnConnect);
                await smtp.AuthenticateAsync(_emailSettings.SenderEmail, _emailSettings.SenderPassword);
                await smtp.SendAsync(email);
                await smtp.DisconnectAsync(true);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"MailKit SendEmailAsync Error: {ex.Message}");
                return false;
            }
        }




    }

    public class EmailSettings
    {
        public string SmtpServer { get; set; }
        public int Port { get; set; }
        public string SenderEmail { get; set; }
        public string SenderPassword { get; set; }
        public bool EnableSSL { get; set; }
    }
}
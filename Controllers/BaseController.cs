using HRMSAPI.Implementation;
using HRMSAPI.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMSAPI.Controllers
{
    public class BaseController : ControllerBase
    {
        public readonly IEmailService _emailService;
        
        public BaseController(IEmailService emailService)
        {
            _emailService = emailService;
        }

        public async Task<bool> SendEmailAsync(List<string> toList, List<string> ccList, string subject, string body)
        {
            return await _emailService.SendEmailAsync(toList, ccList, subject, body);
        }

    }
}

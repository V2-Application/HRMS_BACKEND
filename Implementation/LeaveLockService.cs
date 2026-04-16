using HRMSAPI.Interfaces;
using HRMSAPI.Models.Auth;
using Microsoft.Extensions.Logging;
using System.Net;

namespace HRMSAPI.Implementation
{
    public class LeaveLockService : ILeaveLockService
    {
        private readonly ILogger<LeaveLockService> _logger;

        public LeaveLockService(ILogger<LeaveLockService> logger)
        {
            _logger = logger;
        }

        public async Task<Response> CheckLeaveLockStatusAsync()
        {
            try
            {
                var currentDate = DateTime.Now.Date;
                var currentMonth = currentDate.Month;
                var currentYear = currentDate.Year;
                var day = currentDate.Day;

                // Calculate the 26th of current month
                var lockStartDate = new DateTime(currentYear, currentMonth, 26);

                // Calculate the 2nd of next month
                var nextMonth = currentMonth == 12 ? 1 : currentMonth + 1;
                var nextMonthYear = currentMonth == 12 ? currentYear + 1 : currentYear;
                var lockEndDate = new DateTime(nextMonthYear, nextMonth, 2);

                // Check if current date is between 26th of current month and 2nd of next month
                bool isLocked = currentDate >= lockStartDate && currentDate <= lockEndDate;

                if (isLocked)
                {
                    // Calculate the 3rd of next month for the message
                    var unlockDate = new DateTime(nextMonthYear, nextMonth, 3);
                    var unlockDateFormatted = unlockDate.ToString("MMM dd,yyyy");

                    var data = new
                    {
                        isLeavesLocked = false,
                        message = $"Leaves are locked due to payroll time, please check after next month ({unlockDateFormatted})"
                    };

                    _logger.LogInformation("Leave lock status checked: Leaves are locked until {UnlockDate}", unlockDateFormatted);

                    return new Response
                    {
                        Status = true,
                        Message = "Fetched",
                        StatusCode = HttpStatusCode.OK,
                        Data = data
                    };
                }
                else
                {
                    var data = new
                    {
                        isLeavesLocked = false,
                        message = "Showing Leaves"
                    };

                    _logger.LogInformation("Leave lock status checked: Leaves are not locked");

                    return new Response
                    {
                        Status = true,
                        Message = "Fetched",
                        StatusCode = HttpStatusCode.OK,
                        Data = data
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking leave lock status");
                return new Response
                {
                    Status = false,
                    Message = "An error occurred while checking leave lock status.",
                    StatusCode = HttpStatusCode.InternalServerError,
                    Data = null
                };
            }
        }
    }
}


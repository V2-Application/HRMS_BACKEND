using HRMSAPI.Data;
using HRMSAPI.DTO;
using HRMSAPI.Interfaces;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HRMSAPI.Implementation
{
    public class OCRservice : IOCRservice
    {
        public readonly HRMSContext _context;
        private readonly ILogger<OCRservice> _logger;
        public OCRservice(HRMSContext context, ILogger<OCRservice> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<OCRMasterResponseDto>> GetOCRMasterAsync(string? subject = null)
        {
            try
            {
                var query = _context.tblOCRMasters
                    .Where(x => x.IsDeleted == false);

                if (!string.IsNullOrWhiteSpace(subject))
                {
                    query = query.Where(x => x.Subject == subject);
                }

                var key = await query.OrderBy(x => x.Subject)
                    .ThenBy(x => x.Key)
                    .Select(x => new OCRMasterResponseDto
                    {
                        Id = x.Id,
                        Subject = x.Subject,
                        Key = x.Key,
                        IsActive = x.IsActive
                    })
                    .ToListAsync();

                return key;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching OCR master. Subject filter: {Subject}", subject);

                throw;
            }
        }

    }
}
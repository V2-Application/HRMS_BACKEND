using HRMSAPI.Data;
using HRMSAPI.DTO;
using HRMSAPI.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Roomsy.DTOS.GenericsResponses;
using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace HRMSAPI.Implementation
{
    public class RetentionService : IRetentionService
    {
        private readonly HRMSContext _context;
        private readonly ILogger<RetentionService> _logger;
        public RetentionService(HRMSContext context, ILogger<RetentionService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        public async Task<ExecuteAndReponse> CreateRetentionBonusAsync(RetentionBonusRequestDto request, string userId)
        {
            if (request == null)
            {
                return new ExecuteAndReponse
                {
                    Status = false,
                    Message = "Retention bonus data is required.",
                    Code = HttpStatusCode.BadRequest
                };
            }

            var trimmedEcode = request.Ecode?.Trim();
            if (string.IsNullOrWhiteSpace(trimmedEcode))
            {
                return new ExecuteAndReponse
                {
                    Status = false,
                    Message = "Ecode is required.",
                    Code = HttpStatusCode.BadRequest
                };
            }

            if (request.Percentage <= 0)
            {
                return new ExecuteAndReponse
                {
                    Status = false,
                    Message = "Percentage must be greater than zero.",
                    Code = HttpStatusCode.BadRequest
                };
            }

            if (request.RetentionEnd < request.RetentionStart)
            {
                return new ExecuteAndReponse
                {
                    Status = false,
                    Message = "Retention end date cannot be earlier than start date.",
                    Code = HttpStatusCode.BadRequest
                };
            }

            try
            {
                var employee = await _context.tblEmployees
                    .AsNoTracking()
                    .AsQueryable()
                    .FirstOrDefaultAsync(e =>
                        e.Ecode == trimmedEcode &&
                        e.IsActive == true &&
                        e.IsDeleted == false);

                if (employee == null)
                {
                    return new ExecuteAndReponse
                    {
                        Status = false,
                        Message = $"Employee with Ecode {trimmedEcode} not found or inactive.",
                        Code = HttpStatusCode.NotFound
                    };
                }

                if (!employee.monthlyGrossCTC.HasValue || employee.monthlyGrossCTC.Value <= 0)
                {
                    return new ExecuteAndReponse
                    {
                        Status = false,
                        Message = "Employee does not have a valid Monthly Gross CTC value.",
                        Code = HttpStatusCode.BadRequest
                    };
                }

                var overlap = await _context.tblRetentionBonus
                    .AsNoTracking()
                    .AsQueryable()
                    .Where(r =>
                        r.ECode == trimmedEcode &&
                        r.IsDeleted != true &&
                        r.RetentionStart <= request.RetentionEnd &&
                        r.RetentionEnd >= request.RetentionStart)
                    .FirstOrDefaultAsync();

                if (overlap != null)
                {
                    return new ExecuteAndReponse
                    {
                        Status = false,
                        Message = $"Requested period {request.RetentionStart:dd-MMM-yyyy} to {request.RetentionEnd:dd-MMM-yyyy} overlaps with existing retention bonus {overlap.RetentionStart:dd-MMM-yyyy} to {overlap.RetentionEnd:dd-MMM-yyyy}.",
                        Code = HttpStatusCode.BadRequest
                    };
                }

                var monthlyGross = decimal.Round(employee.monthlyGrossCTC.Value, 2, MidpointRounding.AwayFromZero);
                var bonusAmount = decimal.Round(
                    monthlyGross * (request.Percentage / 100m),
                    2,
                    MidpointRounding.AwayFromZero);

                var now = DateTime.Now;
                var createdBy = string.IsNullOrWhiteSpace(userId) ? "System" : userId;

                var retentionRecord = new tblRetentionBonu
                {
                    ECode = trimmedEcode,
                    LetterIssueDate = now,
                    RetentionStart = request.RetentionStart,
                    RetentionEnd = request.RetentionEnd,
                    MonthlyGrossAtIssue = monthlyGross,
                    Percentage = request.Percentage,
                    BonusAmount = bonusAmount,
                    CreatedBy = createdBy,
                    CreatedOn = now,
                    UpdatedBy = createdBy,
                    UpdatedOn = now,
                    IsActive = true,
                    IsDeleted = false
                };

                await _context.tblRetentionBonus.AddAsync(retentionRecord);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created retention bonus for Ecode {Ecode} with BonusAmount {BonusAmount}", trimmedEcode, bonusAmount);

                return new ExecuteAndReponse
                {
                    Status = true,
                    Message = "Retention bonus created successfully.",
                    Code = HttpStatusCode.Created
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while creating retention bonus for Ecode {Ecode}", request.Ecode);
                return new ExecuteAndReponse
                {
                    Status = false,
                    Message = ex.Message,
                    Code = HttpStatusCode.InternalServerError
                };
            }
        }
        public async Task<FetchAndResponse> GetRetentionBonusesAsync(string ecode)
        {
            try
            {
                var query = _context.tblRetentionBonus
                    .AsNoTracking()
                    .Where(r => r.IsDeleted != true);

                if (!string.IsNullOrWhiteSpace(ecode))
                {
                    var trimmedEcode = ecode.Trim();
                    query = query.Where(r => r.ECode == trimmedEcode);
                }

                var records = await query
                    .OrderByDescending(r => r.LetterIssueDate)
                    .Select(r => new RetentionBonusResponseDto
                    {
                        RetentionId = r.RetentionID,
                        Ecode = r.ECode,
                        LetterIssueDate = r.LetterIssueDate,
                        RetentionStart = r.RetentionStart,
                        RetentionEnd = r.RetentionEnd,
                        MonthlyGrossAtIssue = r.MonthlyGrossAtIssue,
                        Percentage = r.Percentage,
                        BonusAmount = r.BonusAmount,
                        Accepted = r.Accepted,
                        AcceptedOn = r.AcceptedOn
                    })
                    .ToListAsync();

                if (records == null || !records.Any())
                {
                    return new FetchAndResponse
                    {
                        Status = false,
                        Message = "No retention bonus records found.",
                        Code = HttpStatusCode.NotFound,
                        Data = null
                    };
                }

                return new FetchAndResponse
                {
                    Status = true,
                    Message = "Retention bonus records fetched successfully.",
                    Code = HttpStatusCode.OK,
                    Data = records
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching retention bonuses.");
                return new FetchAndResponse
                {
                    Status = false,
                    Message = ex.Message,
                    Code = HttpStatusCode.InternalServerError,
                    Data = null
                };
            }
        }
        public async Task<ExecuteAndReponse> UpdateRetentionBonusStatusAsync(RetentionBonusStatusUpdateDto request, string userId)
        {
            if (request == null || request.RetentionId <= 0)
            {
                return new ExecuteAndReponse
                {
                    Status = false,
                    Message = "Valid retention bonus ID is required.",
                    Code = HttpStatusCode.BadRequest
                };
            }

            try
            {
                var record = await _context.tblRetentionBonus
                    .FirstOrDefaultAsync(r => r.RetentionID == request.RetentionId && r.IsDeleted != true);

                if (record == null)
                {
                    return new ExecuteAndReponse
                    {
                        Status = false,
                        Message = "Retention bonus record not found.",
                        Code = HttpStatusCode.NotFound
                    };
                }

                record.Accepted = request.Accepted;
                record.AcceptedOn = request.Accepted ? DateTime.Now : null;
                record.UpdatedBy = string.IsNullOrWhiteSpace(userId) ? "System" : userId;
                record.UpdatedOn = DateTime.Now;

                await _context.SaveChangesAsync();

                return new ExecuteAndReponse
                {
                    Status = true,
                    Message = $"Retention bonus {(request.Accepted ? "approved" : "rejected")} successfully.",
                    Code = HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while updating retention bonus status for Id {Id}", request.RetentionId);
                return new ExecuteAndReponse
                {
                    Status = false,
                    Message = ex.Message,
                    Code = HttpStatusCode.InternalServerError
                };
            }
        }
    }
}


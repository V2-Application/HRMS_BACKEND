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
    public class EcodeWiseBonusProvisioningPolicyMappingService : IEcodeWiseBonusProvisioningPolicyMappingService
    {
        private readonly HRMSContext _context;
        private readonly ILogger<EcodeWiseBonusProvisioningPolicyMappingService> _logger;

        public EcodeWiseBonusProvisioningPolicyMappingService(HRMSContext context, ILogger<EcodeWiseBonusProvisioningPolicyMappingService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<FetchAndResponse> GetAllEcodeWiseBonusProvisioningPolicyMappingsAsync()
        {
            try
            {
                var mappings = await (from mapping in _context.EcodeWiseBonusProvisioningPolicyMappings
                                      join employee in _context.tblEmployees on mapping.Ecode equals employee.Ecode into empGroup
                                      from emp in empGroup.DefaultIfEmpty()
                                      join policy in _context.BonusProvisioningPolicyMasters on mapping.BonusProvisioningPolicyMaster equals policy.Id into policyGroup
                                      from policy in policyGroup.DefaultIfEmpty()
                                      where mapping.IsActive == true && mapping.IsDeleted == false
                                      orderby mapping.Ecode
                                      select new EcodeWiseBonusProvisioningPolicyMappingResponseDto
                                      {
                                          Id = mapping.Id,
                                          Ecode = mapping.Ecode,
                                          FullName = emp == null 
                                              ? string.Empty
                                              : (string.IsNullOrWhiteSpace(emp.FULL_NAME) 
                                                  ? ($"{(emp.FirstName ?? string.Empty).Trim()} {(emp.LastName ?? string.Empty).Trim()}".Trim())
                                                  : emp.FULL_NAME.Trim()),
                                          BonusProvisioningPolicyMaster = mapping.BonusProvisioningPolicyMaster,
                                          PolicyName = policy == null ? string.Empty : (policy.PolicyName ?? string.Empty),
                                          Freq = policy == null || string.IsNullOrWhiteSpace(policy.PayFreq)
                                              ? string.Empty
                                              : (policy.PayFreq.Trim().ToUpper() == "A" 
                                                  ? "Annually" 
                                                  : (policy.PayFreq.Trim().ToUpper() == "M" 
                                                      ? "Monthly" 
                                                      : policy.PayFreq.Trim())),
                                          CreatedBy = mapping.CreatedBy,
                                          CreatedOn = mapping.CreatedOn,
                                          UpdatedOn = mapping.UpdatedOn,
                                          UpdatedBy = mapping.UpdatedBy,
                                          IsActive = mapping.IsActive,
                                          IsDeleted = mapping.IsDeleted
                                      })
                    .AsNoTracking()
                    .AsQueryable()
                    .ToListAsync();

                if (mappings == null || !mappings.Any())
                {
                    return new FetchAndResponse
                    {
                        Status = false,
                        Message = "No data found",
                        Code = HttpStatusCode.NotFound,
                        Data = null
                    };
                }

                _logger.LogInformation("Retrieved {Count} EcodeWiseBonusProvisioningPolicyMapping records", mappings.Count);
                return new FetchAndResponse
                {
                    Status = true,
                    Message = "Fetched successfully",
                    Data = mappings,
                    Code = HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving EcodeWiseBonusProvisioningPolicyMappings");
                return new FetchAndResponse
                {
                    Status = false,
                    Message = ex.Message,
                    Code = HttpStatusCode.InternalServerError,
                    Data = null
                };
            }
        }

        public async Task<FetchAndResponse> GetAllBonusProvisioningPoliciesAsync()
        {
            try
            {
                var policies = await _context.BonusProvisioningPolicyMasters
                    .AsNoTracking()
                    .AsQueryable()
                    .Where(p => p.IsActive == true && p.IsDeleted == false)
                    .OrderBy(p => p.PolicyName)
                    .Select(p => new BonusProvisioningPolicyResponseDto
                    {
                        Id = p.Id,
                        PolicyName = p.PolicyName,
                        CreatedBy = p.CreatedBy,
                        CreatedOn = p.CreatedOn,
                        UpdatedOn = p.UpdatedOn,
                        UpdatedBy = p.UpdatedBy,
                        IsActive = p.IsActive,
                        IsDeleted = p.IsDeleted
                    })
                    .ToListAsync();

                if (policies == null || !policies.Any())
                {
                    return new FetchAndResponse
                    {
                        Status = false,
                        Message = "No data found",
                        Code = HttpStatusCode.NotFound,
                        Data = null
                    };
                }

                _logger.LogInformation("Retrieved {Count} BonusProvisioningPolicyMaster records", policies.Count);
                return new FetchAndResponse
                {
                    Status = true,
                    Message = "Fetched successfully",
                    Data = policies,
                    Code = HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving BonusProvisioningPolicyMasters");
                return new FetchAndResponse
                {
                    Status = false,
                    Message = ex.Message,
                    Code = HttpStatusCode.InternalServerError,
                    Data = null
                };
            }
        }

        public async Task<ExecuteAndReponse> UpsertEcodeWiseBonusProvisioningPolicyMappingAsync(EcodeWiseBonusProvisioningPolicyMappingUpsertDto dto, string userId)
        {
            if (dto == null)
            {
                return new ExecuteAndReponse
                {
                    Status = false,
                    Message = "EcodeWiseBonusProvisioningPolicyMapping data is required",
                    Code = HttpStatusCode.BadRequest
                };
            }

            try
            {
                // Validate required fields
                if (string.IsNullOrWhiteSpace(dto.Ecode))
                {
                    return new ExecuteAndReponse
                    {
                        Status = false,
                        Message = "Ecode is required",
                        Code = HttpStatusCode.BadRequest
                    };
                }

                var trimmedEcode = dto.Ecode.Trim();

                // Validate salary requirement for specific policy IDs
                var restrictedPolicyIds = new[]
                {
                    Guid.Parse("2366FC08-6EC3-F011-B1EA-8C84747E00C5"),
                    Guid.Parse("C6BDAC6D-6EC3-F011-B1EA-8C84747E00C5")
                };

                if (dto.BonusProvisioningPolicyMaster.HasValue && 
                    restrictedPolicyIds.Contains(dto.BonusProvisioningPolicyMaster.Value))
                {
                    var employee = await _context.tblEmployees
                        .AsNoTracking()
                        .AsQueryable()
                        .FirstOrDefaultAsync(e => e.Ecode == trimmedEcode 
                            && e.IsActive == true 
                            && e.IsDeleted == false);

                    if (employee == null)
                    {
                        return new ExecuteAndReponse
                        {
                            Status = false,
                            Message = "Employee not found",
                            Code = HttpStatusCode.NotFound
                        };
                    }

                    if (!employee.GROSS_SALARY.HasValue || employee.GROSS_SALARY.Value <= 21000)
                    {
                        return new ExecuteAndReponse
                        {
                            Status = false,
                            Message = "This category applies only to employees whose salary is above ₹21,000 per month.",
                            Code = HttpStatusCode.BadRequest
                        };
                    }
                }

                // Check if Ecode exists with IsActive = 1 and IsDeleted = 0
                var existingMapping = await _context.EcodeWiseBonusProvisioningPolicyMappings
                    .AsQueryable()
                    .FirstOrDefaultAsync(m => m.Ecode == trimmedEcode 
                        && m.IsActive == true 
                        && m.IsDeleted == false);

                if (existingMapping != null)
                {
                    // Update existing record
                    existingMapping.BonusProvisioningPolicyMaster = dto.BonusProvisioningPolicyMaster;
                    existingMapping.UpdatedBy = userId;
                    existingMapping.UpdatedOn = DateTime.UtcNow;

                    _logger.LogInformation("Updating EcodeWiseBonusProvisioningPolicyMapping for Ecode: {Ecode}", trimmedEcode);
                }
                else
                {
                    // Insert new record
                    var newMapping = new EcodeWiseBonusProvisioningPolicyMapping
                    {
                        Id = Guid.NewGuid(),
                        Ecode = trimmedEcode,
                        BonusProvisioningPolicyMaster = dto.BonusProvisioningPolicyMaster,
                        CreatedBy = userId,
                        CreatedOn = DateTime.UtcNow,
                        IsActive = true,
                        IsDeleted = false
                    };

                    await _context.EcodeWiseBonusProvisioningPolicyMappings.AddAsync(newMapping);
                    _logger.LogInformation("Creating new EcodeWiseBonusProvisioningPolicyMapping for Ecode: {Ecode}", trimmedEcode);
                }

                await _context.SaveChangesAsync();

                return new ExecuteAndReponse
                {
                    Status = true,
                    Message = "Upserted successfully",
                    Code = HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while upserting EcodeWiseBonusProvisioningPolicyMapping");
                return new ExecuteAndReponse
                {
                    Status = false,
                    Message = ex.Message,
                    Code = HttpStatusCode.InternalServerError
                };
            }
        }

        public async Task<ExecuteAndReponse> DeleteEcodeWiseBonusProvisioningPolicyMappingAsync(Guid id)
        {
            if (id == Guid.Empty)
            {
                return new ExecuteAndReponse
                {
                    Status = false,
                    Message = "Invalid ID",
                    Code = HttpStatusCode.BadRequest
                };
            }

            try
            {
                var mapping = await _context.EcodeWiseBonusProvisioningPolicyMappings
                    .AsQueryable()
                    .FirstOrDefaultAsync(m => m.Id == id);

                if (mapping == null)
                {
                    return new ExecuteAndReponse
                    {
                        Status = false,
                        Message = $"EcodeWiseBonusProvisioningPolicyMapping with ID {id} not found",
                        Code = HttpStatusCode.NotFound
                    };
                }

                // Soft delete - set IsActive to false and IsDeleted to true
                mapping.IsActive = false;
                mapping.IsDeleted = true;
                mapping.UpdatedOn = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                _logger.LogInformation("Soft deleted EcodeWiseBonusProvisioningPolicyMapping with ID: {Id}", id);

                return new ExecuteAndReponse
                {
                    Status = true,
                    Message = "Deleted successfully",
                    Code = HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while deleting EcodeWiseBonusProvisioningPolicyMapping with ID: {Id}", id);
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


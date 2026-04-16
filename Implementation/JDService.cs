using HRMSAPI.Data;
using HRMSAPI.Interfaces;
using Microsoft.EntityFrameworkCore;
using Roomsy.DTOS.GenericsResponses;
using System.Net;

namespace HRMSAPI.Implementation
{
    public class JDService : BaseService, IJDService
    {
        private readonly HRMSContext _context;

        public JDService(HRMSContext context) : base(context)
        {
            _context = context;
        }

        public async Task<ExecuteAndReponse> UpsertJDsAsync(List<JDUpsertDto> jdList)
        {
            try
            {
                if (jdList == null || !jdList.Any())
                {
                    return BuildExecuteErrorResponse("No JD data provided", HttpStatusCode.BadRequest);
                }

                // Validate required fields
                foreach (var jd in jdList)
                {
                    if (string.IsNullOrWhiteSpace(jd.DesignationName))
                    {
                        return BuildExecuteErrorResponse("Designation Name is required", HttpStatusCode.BadRequest);
                    }
                    if (string.IsNullOrWhiteSpace(jd.KeyResponsibility))
                    {
                        return BuildExecuteErrorResponse("Key Responsibility is required", HttpStatusCode.BadRequest);
                    }
                    if (string.IsNullOrWhiteSpace(jd.KeySkills))
                    {
                        return BuildExecuteErrorResponse("Key Skills is required", HttpStatusCode.BadRequest);
                    }
                }

                // Get all unique designation names from the request
                var designationNames = jdList.Select(jd => jd.DesignationName.Trim()).Distinct().ToList();

                // Fetch all designations that match the provided names
                var existingDesignations = await _context.tblDesignations
                    .Where(d => designationNames.Contains(d.DesignationName))
                    .ToListAsync();

                // Create a dictionary for quick lookup
                var designationDict = existingDesignations
                    .ToDictionary(d => d.DesignationName, d => d.DesignationId, StringComparer.OrdinalIgnoreCase);

                // Check for missing designations
                var missingDesignations = designationNames
                    .Where(name => !designationDict.ContainsKey(name))
                    .ToList();

                if (missingDesignations.Any())
                {
                    return BuildExecuteErrorResponse($"The following designations do not exist: {string.Join(", ", missingDesignations)}", HttpStatusCode.BadRequest);
                }

                var jdsToCreate = new List<JD>();
                var jdsToUpdate = new List<JD>();

                foreach (var jdDto in jdList)
                {
                    var designationId = designationDict[jdDto.DesignationName.Trim()];

                    if (jdDto.JDId == 0)
                    {
                        // Create new JD
                        jdsToCreate.Add(new JD
                        {
                            DesignationId = designationId,
                            KeyResponsibility = jdDto.KeyResponsibility.Trim(),
                            KeySkills = jdDto.KeySkills.Trim()
                        });
                    }
                    else
                    {
                        // Update existing JD
                        var existingJD = await _context.JDs.FindAsync(jdDto.JDId);
                        if (existingJD == null)
                        {
                            return BuildExecuteErrorResponse($"JD with ID {jdDto.JDId} not found", HttpStatusCode.NotFound);
                        }

                        existingJD.DesignationId = designationId;
                        existingJD.KeyResponsibility = jdDto.KeyResponsibility.Trim();
                        existingJD.KeySkills = jdDto.KeySkills.Trim();

                        jdsToUpdate.Add(existingJD);
                    }
                }

                // Save changes
                if (jdsToCreate.Any())
                {
                    await _context.JDs.AddRangeAsync(jdsToCreate);
                }

                if (jdsToUpdate.Any())
                {
                    _context.JDs.UpdateRange(jdsToUpdate);
                }

                await _context.SaveChangesAsync();

                var createdCount = jdsToCreate.Count;
                var updatedCount = jdsToUpdate.Count;

                string message = "";
                if (createdCount > 0 && updatedCount > 0)
                {
                    message = $"Successfully created {createdCount} JDs and updated {updatedCount} JDs";
                }
                else if (createdCount > 0)
                {
                    message = $"Successfully created {createdCount} JDs";
                }
                else
                {
                    message = $"Successfully updated {updatedCount} JDs";
                }

                return BuildExecuteSuccessResponse(message);
            }
            catch (Exception ex)
            {
                return BuildExecuteErrorResponse($"Error processing JDs: {ex.Message}", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ExecuteAndReponse> DeleteJDAsync(int jdId)
        {
            try
            {
                var jd = await _context.JDs.FindAsync(jdId);
                if (jd == null)
                {
                    return BuildExecuteErrorResponse($"JD with ID {jdId} not found", HttpStatusCode.NotFound);
                }

                _context.JDs.Remove(jd);
                await _context.SaveChangesAsync();

                return BuildExecuteSuccessResponse($"JD with ID {jdId} deleted successfully");
            }
            catch (Exception ex)
            {
                return BuildExecuteErrorResponse($"Error deleting JD: {ex.Message}", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<FetchAndResponse> GetAllJDsAsync()
        {
            try
            {
                var jds = await _context.vw_JDs.ToListAsync();

                if (jds == null || !jds.Any())
                {
                    return BuildFetchErrorResponse("No JDs found", HttpStatusCode.NotFound);
                }

                var result = jds.Select(jd => new JDResponseDto
                {
                    JDId = jd.JDId,
                    DesignationName = jd.DesignationName,
                    KeyResponsibility = jd.KeyResponsibility,
                    KeySkills = jd.KeySkills
                }).ToList();

                return BuildFetchSuccessResponse("JDs fetched successfully", result);
            }
            catch (Exception ex)
            {
                return BuildFetchErrorResponse($"Error fetching JDs: {ex.Message}", HttpStatusCode.InternalServerError);
            }
        }
    }
}

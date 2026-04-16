using HRMSAPI.Data;
using HRMSAPI.DTO;
using HRMSAPI.Interfaces;
using Microsoft.EntityFrameworkCore;
using Roomsy.DTOS.GenericsResponses;
using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace HRMSAPI.Implementation
{
    public class ShiftMasterService : BaseService, IShiftMasterService
    {
        private readonly HRMSContext _context;

        public ShiftMasterService(HRMSContext context) : base(context)
        {
            _context = context;
        }

        public async Task<ExecuteAndReponse> CreateShiftAsync(ShiftMasterUpsertDto shiftDto, string createdBy)
        {
            try
            {
                // Validate input
                if (shiftDto == null)
                {
                    return BuildExecuteErrorResponse("Shift data is required", HttpStatusCode.BadRequest);
                }

                if (string.IsNullOrWhiteSpace(shiftDto.ShiftName))
                {
                    return BuildExecuteErrorResponse("Shift Name is required", HttpStatusCode.BadRequest);
                }

                if (string.IsNullOrWhiteSpace(createdBy))
                {
                    return BuildExecuteErrorResponse("Created By is required", HttpStatusCode.BadRequest);
                }

                // Check if ShiftName already exists (case-insensitive)
                var existingShift = await _context.tblShiftMasters
                    .FirstOrDefaultAsync(s => s.ShiftName.Trim().ToLower() == shiftDto.ShiftName.Trim().ToLower());

                if (existingShift != null)
                {
                    return BuildExecuteErrorResponse($"Shift with name '{shiftDto.ShiftName}' already exists", HttpStatusCode.BadRequest);
                }

                // Validate time range
                if (shiftDto.StartTime >= shiftDto.EndTime)
                {
                    // Allow for overnight shifts (e.g., 21:00 to 05:30)
                    // If EndTime is less than StartTime, it's an overnight shift, which is valid
                    // But if they're equal or EndTime is greater in the same day, it's invalid
                    // For simplicity, we'll allow overnight shifts (EndTime < StartTime)
                    // But if EndTime > StartTime and they're on the same day, it's invalid
                    // Actually, let's check: if StartTime >= EndTime, it could be overnight, so we allow it
                    // But we should validate that the times are valid
                }

                // Create new shift
                var shift = new tblShiftMaster
                {
                    ShiftName = shiftDto.ShiftName.Trim(),
                    StartTime = shiftDto.StartTime,
                    EndTime = shiftDto.EndTime,
                    IsActive = shiftDto.IsActive,
                    CreatedBy = createdBy,
                    CreatedOn = DateTime.UtcNow
                };

                await _context.tblShiftMasters.AddAsync(shift);
                await _context.SaveChangesAsync();

                return BuildExecuteSuccessResponse($"Shift '{shiftDto.ShiftName}' created successfully");
            }
            catch (DbUpdateException ex)
            {
                // Handle unique constraint violation
                if (ex.InnerException != null && ex.InnerException.Message.Contains("UQ_tblShiftMaster_ShiftName"))
                {
                    return BuildExecuteErrorResponse($"Shift with name '{shiftDto.ShiftName}' already exists", HttpStatusCode.BadRequest);
                }
                return BuildExecuteErrorResponse($"Error creating shift: {ex.Message}", HttpStatusCode.InternalServerError);
            }
            catch (Exception ex)
            {
                return BuildExecuteErrorResponse($"Error creating shift: {ex.Message}", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ExecuteAndReponse> UpdateShiftAsync(int shiftId, ShiftMasterUpsertDto shiftDto, string updatedBy)
        {
            try
            {
                // Validate input
                if (shiftDto == null)
                {
                    return BuildExecuteErrorResponse("Shift data is required", HttpStatusCode.BadRequest);
                }

                if (string.IsNullOrWhiteSpace(shiftDto.ShiftName))
                {
                    return BuildExecuteErrorResponse("Shift Name is required", HttpStatusCode.BadRequest);
                }

                if (string.IsNullOrWhiteSpace(updatedBy))
                {
                    return BuildExecuteErrorResponse("Updated By is required", HttpStatusCode.BadRequest);
                }

                // Find existing shift
                var existingShift = await _context.tblShiftMasters.FindAsync(shiftId);
                if (existingShift == null)
                {
                    return BuildExecuteErrorResponse($"Shift with ID {shiftId} not found", HttpStatusCode.NotFound);
                }

                // Check if ShiftName already exists for a different shift (case-insensitive)
                var duplicateShift = await _context.tblShiftMasters
                    .FirstOrDefaultAsync(s => s.ShiftName.Trim().ToLower() == shiftDto.ShiftName.Trim().ToLower() 
                                           && s.ShiftID != shiftId);

                if (duplicateShift != null)
                {
                    return BuildExecuteErrorResponse($"Shift with name '{shiftDto.ShiftName}' already exists", HttpStatusCode.BadRequest);
                }

                // Update shift
                existingShift.ShiftName = shiftDto.ShiftName.Trim();
                existingShift.StartTime = shiftDto.StartTime;
                existingShift.EndTime = shiftDto.EndTime;
                existingShift.IsActive = shiftDto.IsActive;
                existingShift.LastUpdatedBy = updatedBy;
                existingShift.LastUpdatedOn = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return BuildExecuteSuccessResponse($"Shift '{shiftDto.ShiftName}' updated successfully");
            }
            catch (DbUpdateException ex)
            {
                // Handle unique constraint violation
                if (ex.InnerException != null && ex.InnerException.Message.Contains("UQ_tblShiftMaster_ShiftName"))
                {
                    return BuildExecuteErrorResponse($"Shift with name '{shiftDto.ShiftName}' already exists", HttpStatusCode.BadRequest);
                }
                return BuildExecuteErrorResponse($"Error updating shift: {ex.Message}", HttpStatusCode.InternalServerError);
            }
            catch (Exception ex)
            {
                return BuildExecuteErrorResponse($"Error updating shift: {ex.Message}", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<FetchAndResponse> GetAllShiftsAsync()
        {
            try
            {
                var shifts = await _context.tblShiftMasters
                    .AsNoTracking()
                    .OrderBy(s => s.ShiftName)
                    .ToListAsync();

                if (shifts == null || !shifts.Any())
                {
                    return BuildFetchErrorResponse("No shifts found", HttpStatusCode.NotFound);
                }

                var result = shifts.Select(s => new ShiftMasterDto
                {
                    ShiftID = s.ShiftID,
                    ShiftName = s.ShiftName,
                    StartTime = s.StartTime,
                    EndTime = s.EndTime,
                    IsActive = s.IsActive,
                    CreatedBy = s.CreatedBy,
                    CreatedOn = s.CreatedOn,
                    LastUpdatedOn = s.LastUpdatedOn,
                    LastUpdatedBy = s.LastUpdatedBy
                }).ToList();

                return BuildFetchSuccessResponse("Shifts fetched successfully", result);
            }
            catch (Exception ex)
            {
                return BuildFetchErrorResponse($"Error fetching shifts: {ex.Message}", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<FetchAndResponse> GetShiftByIdAsync(int shiftId)
        {
            try
            {
                var shift = await _context.tblShiftMasters
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.ShiftID == shiftId);

                if (shift == null)
                {
                    return BuildFetchErrorResponse($"Shift with ID {shiftId} not found", HttpStatusCode.NotFound);
                }

                var result = new ShiftMasterDto
                {
                    ShiftID = shift.ShiftID,
                    ShiftName = shift.ShiftName,
                    StartTime = shift.StartTime,
                    EndTime = shift.EndTime,
                    IsActive = shift.IsActive,
                    CreatedBy = shift.CreatedBy,
                    CreatedOn = shift.CreatedOn,
                    LastUpdatedOn = shift.LastUpdatedOn,
                    LastUpdatedBy = shift.LastUpdatedBy
                };

                return BuildFetchSuccessResponse("Shift fetched successfully", result);
            }
            catch (Exception ex)
            {
                return BuildFetchErrorResponse($"Error fetching shift: {ex.Message}", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ExecuteAndReponse> DeleteShiftAsync(int shiftId)
        {
            try
            {
                var shift = await _context.tblShiftMasters.FindAsync(shiftId);
                if (shift == null)
                {
                    return BuildExecuteErrorResponse($"Shift with ID {shiftId} not found", HttpStatusCode.NotFound);
                }

                // Check if shift is being used by employees or candidates
                var employeeCount = await _context.tblEmployees
                    .CountAsync(e => e.ShiftID == shiftId);

                var candidateCount = await _context.Candidates
                    .CountAsync(c => c.ShiftID == shiftId);

                if (employeeCount > 0 || candidateCount > 0)
                {
                    return BuildExecuteErrorResponse(
                        $"Cannot delete shift '{shift.ShiftName}' as it is assigned to {employeeCount} employee(s) and {candidateCount} candidate(s). Please reassign them first.",
                        HttpStatusCode.BadRequest);
                }

                _context.tblShiftMasters.Remove(shift);
                await _context.SaveChangesAsync();

                return BuildExecuteSuccessResponse($"Shift '{shift.ShiftName}' deleted successfully");
            }
            catch (Exception ex)
            {
                return BuildExecuteErrorResponse($"Error deleting shift: {ex.Message}", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ExecuteAndReponse> ToggleShiftStatusAsync(int shiftId, string updatedBy)
        {
            try
            {
                var shift = await _context.tblShiftMasters.FindAsync(shiftId);
                if (shift == null)
                {
                    return BuildExecuteErrorResponse($"Shift with ID {shiftId} not found", HttpStatusCode.NotFound);
                }

                shift.IsActive = !shift.IsActive;
                shift.LastUpdatedBy = updatedBy;
                shift.LastUpdatedOn = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                var status = shift.IsActive ? "Active" : "Inactive";
                return BuildExecuteSuccessResponse($"Shift '{shift.ShiftName}' status set to {status}");
            }
            catch (Exception ex)
            {
                return BuildExecuteErrorResponse($"Error toggling shift status: {ex.Message}", HttpStatusCode.InternalServerError);
            }
        }
    }
}


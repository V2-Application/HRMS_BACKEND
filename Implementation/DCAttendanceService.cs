using HRMSAPI.Data;
using HRMSAPI.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

public class DCAttendanceService : IDDCAttendanceService
{
    private readonly HRMSContext _context;
    private readonly ILogger<DCAttendanceService> _logger;

    public DCAttendanceService(HRMSContext context, ILogger<DCAttendanceService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<long>> InsertAttendanceAsync(List<DCAttendanceDTO> attendances)
    {
        try
        {
            if (attendances == null || !attendances.Any())
            {
                _logger.LogWarning("Attempted to insert empty or null attendance records list.");
                throw new ArgumentNullException(nameof(attendances));
            }

            var entities = attendances.Select(attendance => new tblDCAttendance
            {
                Ecode = attendance.Ecode,
                Status = attendance.Status,
                AttendanceDate = DateTime.UtcNow,
                SubmitOn = DateTime.UtcNow // Set to server's current UTC time
            }).ToList();

            await _context.tblDCAttendances.AddRangeAsync(entities);
            await _context.SaveChangesAsync();

            var attendanceIds = entities.Select(e => e.DCAttendanceId).ToList();
            _logger.LogInformation("Successfully inserted {Count} attendance records.", attendanceIds.Count);
            return attendanceIds;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inserting attendance records for {Count} entries.", attendances?.Count ?? 0);
            throw;
        }
    }
}
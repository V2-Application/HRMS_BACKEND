using HRMSAPI.Data;
using HRMSAPI.DTO;
using HRMSAPI.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
public class StoreRoutingService : IStoreRoutingService
{
    private readonly HRMSContext _context;
    private readonly ILogger<StoreRoutingService> _logger;
    private readonly string _uploadPath;

    public StoreRoutingService(HRMSContext context, ILogger<StoreRoutingService> logger, IWebHostEnvironment env)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _uploadPath = Path.Combine(env.WebRootPath, "uploads");
        if (!Directory.Exists(_uploadPath))
            Directory.CreateDirectory(_uploadPath);
    }

    public async Task<(bool Success, string Message)> AddStoreRoutingTransactionAsync(StoreRoutingTransactionDTO model)
    {
        if (model == null || model.LocationId <= 0 || model.StoreRoutingMasterId <= 0 || model.ActionById <= 0)
        {
            _logger.LogWarning("Invalid input data for StoreRoutingTransaction.");
            return (false, "Invalid input data: LocationId, StoreRoutingMasterId, ActionById, and Remarks are required.");
        }

        try
        {
            //// Validate LocationId and StoreRoutingMasterId
            //var validLocation = await _context.tblLocations
            //    .AsNoTracking()
            //    .AnyAsync(l => l.LocationId == model.LocationId);
            //if (!validLocation)
            //{
            //    _logger.LogWarning("Invalid LocationId: {LocationId}", model.LocationId);
            //    return (false, $"Invalid LocationId: {model.LocationId}");
            //}

            //var validStoreRoutingMaster = await _context.StoreRoutingMasters
            //    .AsNoTracking()
            //    .AnyAsync(srm => srm.Id == model.StoreRoutingMasterId);
            //if (!validStoreRoutingMaster)
            //{
            //    _logger.LogWarning("Invalid StoreRoutingMasterId: {StoreRoutingMasterId}", model.StoreRoutingMasterId);
            //    return (false, $"Invalid StoreRoutingMasterId: {model.StoreRoutingMasterId}");
            //}

            // Process file uploads
            string attachments = null;
            if (model.Attachments != null && model.Attachments.Any())
            {
                var attachmentPaths = new List<string>();
                foreach (var file in model.Attachments)
                {
                    if (file.Length > 0)
                    {
                        var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                        var filePath = Path.Combine(_uploadPath, fileName);
                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await file.CopyToAsync(stream);
                        }
                        attachmentPaths.Add($"/uploads/{fileName}");
                    }
                }
                attachments = string.Join(",", attachmentPaths);
            }

            using var connection = _context.Database.GetDbConnection();
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = "usp_AddStoreRoutingTransaction";
            command.CommandType = CommandType.StoredProcedure;

            // Add parameters
            command.Parameters.Add(new SqlParameter("@LocationId", model.LocationId));
            command.Parameters.Add(new SqlParameter("@StoreRoutingMasterId", model.StoreRoutingMasterId));
            command.Parameters.Add(new SqlParameter("@Remarks", (object)model.Remarks ?? DBNull.Value));
            command.Parameters.Add(new SqlParameter("@ActionById", model.ActionById));
            command.Parameters.Add(new SqlParameter("@Attachments", (object)attachments ?? DBNull.Value));

            await command.ExecuteNonQueryAsync();
            await connection.CloseAsync();

            return (true, "Store routing transaction processed successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing store routing transaction for LocationId: {LocationId}, StoreRoutingMasterId: {StoreRoutingMasterId}", model.LocationId, model.StoreRoutingMasterId);
            return (false, $"Error processing store routing transaction: {ex.Message}");
        }
    }
    public async Task<List<StoreRoutingStatusDTO>> GetStoreRoutingStatusAsync(int locationId)
    {
        if (locationId <= 0)
        {
            _logger.LogWarning("Invalid LocationId: {LocationId}", locationId);
            throw new ArgumentException("LocationId must be greater than 0.");
        }

        try
        {
            var records = new List<StoreRoutingStatusDTO>();
            var currentRecord = default(StoreRoutingStatusDTO);
            int? lastMasterId = null;

            using var connection = _context.Database.GetDbConnection();
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = "usp_GetStoreRoutingStatus";
            command.CommandType = CommandType.StoredProcedure;

            command.Parameters.Add(new SqlParameter("@LocationId", locationId));

            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                int masterId = reader.GetInt32("StoreRoutingMasterId");

                // Start a new record when StoreRoutingMasterId changes
                if (lastMasterId != masterId)
                {
                    if (currentRecord != null)
                    {
                        records.Add(currentRecord);
                    }

                    currentRecord = new StoreRoutingStatusDTO
                    {
                        StoreRoutingMasterId = masterId,
                        StagingName = reader.GetString("StagingName"),
                        RoutingName = reader.GetString("RoutingName"),
                        BgtTimeline = reader.IsDBNull("BgtTimeline") ? null : reader.GetString("BgtTimeline"),
                        TransactionId = reader.IsDBNull("TransactionId") ? null : reader.GetInt32("TransactionId"),
                        Remarks = reader.IsDBNull("Remarks") ? null : reader.GetString("Remarks"),
                        ActionDate = reader.IsDBNull("ActionDate") ? null : reader.GetDateTime("ActionDate"),
                        ActionById = reader.IsDBNull("ActionById") ? null : reader.GetInt32("ActionById")
                    };

                    lastMasterId = masterId;
                }

                // Add attachment if present
                if (!reader.IsDBNull("Attachment"))
                {
                    currentRecord.Attachments.Add(reader.GetString("Attachment"));
                }
            }

            // Add the last record if it exists
            if (currentRecord != null)
            {
                records.Add(currentRecord);
            }

            await connection.CloseAsync();
            return records;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving store routing status for LocationId: {LocationId}", locationId);
            throw;
        }
    }
    public async Task<StoreRoutingResponse> GetStoreRoutingStatusByLocationIdAsync(int locationId)
    {
        var details = new List<StoreRoutingDetail>();
        StoreRoutingSummary summary = null;

        using (var connection = _context.Database.GetDbConnection())
        {
            await connection.OpenAsync();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "usp_GetStoreRoutingStatusByLocaionId";
                command.CommandType = System.Data.CommandType.StoredProcedure;
                command.Parameters.Add(new SqlParameter("@LocationId", locationId));

                using (var reader = await command.ExecuteReaderAsync())
                {
                    // Read the first result set (Details)
                    while (await reader.ReadAsync())
                    {
                        // Deserialize JSON array of attachments
                        var attachmentsJson = reader.IsDBNull(11) ? null : reader.GetString(11);
                        var attachments = string.IsNullOrEmpty(attachmentsJson)
                            ? new List<Attachments1>()
                            : JsonConvert.DeserializeObject<List<Attachments1>>(attachmentsJson);

                        details.Add(new StoreRoutingDetail
                        {
                            StoreRoutingMasterId = reader.GetInt32(0),
                            StagingName = reader.IsDBNull(1) ? null : reader.GetString(1),
                            RoutingName = reader.IsDBNull(2) ? null : reader.GetString(2),
                            BgtTimeline = reader.IsDBNull(3) ? null : reader.GetString(3),
                            StagingSequence = reader.IsDBNull(4) ? (int?)null : reader.GetInt32(4),
                            RoutingSequence = reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5),
                            TransactionId = reader.IsDBNull(6) ? (int?)null : reader.GetInt32(6),
                            LocationId = reader.IsDBNull(7) ? (int?)null : reader.GetInt32(7),
                            Remarks = reader.IsDBNull(8) ? null : reader.GetString(8),
                            ActionDate = reader.IsDBNull(9) ? (DateTime?)null : reader.GetDateTime(9),
                            ActionById = reader.IsDBNull(10) ? (int?)null : reader.GetInt32(10),
                            Attachments = attachments,
                            Status = reader.IsDBNull(12) ? null : reader.GetString(12)
                        });
                    }

                    // Move to the next result set (Summary)
                    if (await reader.NextResultAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            summary = new StoreRoutingSummary
                            {
                                LocationId = reader.GetInt32(0),
                                TotalRoutingSteps = reader.GetInt32(1),
                                CompletedSteps = reader.GetInt32(2),
                                PendingSteps = reader.GetInt32(3),
                                OverallStatus = reader.IsDBNull(4) ? null : reader.GetString(4)
                            };
                        }
                    }
                }
            }
        }

        return new StoreRoutingResponse
        {
            Details = details,
            Summary = summary
        };
    }
}


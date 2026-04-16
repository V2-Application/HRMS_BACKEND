using ClosedXML.Excel;
using HRMSAPI.Data;
using HRMSAPI.DTO;
using HRMSAPI.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Roomsy.DTOS.GenericsResponses;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace HRMSAPI.Implementation
{
    public class HolidayMasterService : IHolidayMasterService
    {
        private readonly HRMSContext _context;
        private readonly ILogger<HolidayMasterService> _logger;

        public HolidayMasterService(HRMSContext context, ILogger<HolidayMasterService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ExecuteAndReponse> UpsertHolidayAsync(HolidayMasterUpsertDto holidayDto)
        {
            if (holidayDto == null)
            {
                return new ExecuteAndReponse
                {
                    Status = false,
                    Message = "Holiday data is required.",
                    Code = HttpStatusCode.BadRequest
                };
            }

            try
            {
                // Validate required fields
                if (string.IsNullOrWhiteSpace(holidayDto.HolidayName) || !holidayDto.HolidayDate.HasValue)
                {
                    return new ExecuteAndReponse
                    {
                        Status = false,
                        Message = "HolidayName and HolidayDate are required.",
                        Code = HttpStatusCode.BadRequest
                    };
                }

                // Check if holiday already exists by ID
                HolidayMaster existingHoliday = null;
                if (holidayDto.Id.HasValue && holidayDto.Id.Value > 0)
                {
                    existingHoliday = await _context.HolidayMasters.AsQueryable()
                        .FirstOrDefaultAsync(h => h.Id == holidayDto.Id.Value && !h.IsDeleted.Value);
                }
                else
                {
                    // For new records, check if a holiday with same name, date, location type and value already exists
                    var duplicateHoliday = await _context.HolidayMasters.AsNoTracking().AsQueryable()
                        .FirstOrDefaultAsync(h => h.HolidayName == holidayDto.HolidayName &&
                                                h.HolidayDate == holidayDto.HolidayDate &&
                                                h.LocationType == holidayDto.LocationType &&
                                                h.LocationValue == holidayDto.LocationValue &&
                                                !h.IsDeleted.Value);

                    if (duplicateHoliday != null)
                    {
                        return new ExecuteAndReponse
                        {
                            Status = false,
                            Message = $"Holiday '{holidayDto.HolidayName}' on {holidayDto.HolidayDate.Value:dd/MM/yyyy} already exists for the specified location type and value.",
                            Code = HttpStatusCode.BadRequest
                        };
                    }
                }

                // Validate LocationType if provided
                if (holidayDto.LocationType.HasValue)
                {
                    var locationTypeExists = await _context.LocationTypeMasters.AsNoTracking().AsQueryable()
                        .AnyAsync(lt => lt.Id == holidayDto.LocationType && !lt.IsDeleted.Value);

                    if (!locationTypeExists)
                    {
                        return new ExecuteAndReponse
                        {
                            Status = false,
                            Message = $"LocationType  not found.",
                            Code = HttpStatusCode.BadRequest
                        };
                    }
                }

                // Validate LocationValue if provided
                if (!string.IsNullOrWhiteSpace(holidayDto.LocationValue))
                {
                    if (holidayDto.LocationType.HasValue)
                    {
                        // Check if LocationValue exists based on LocationType
                        bool locationValueExists = false;

                        if (holidayDto.LocationType == 2) // Assuming 2 is for Group
                        {
                            locationValueExists = await _context.GroupMasters.AsNoTracking().AsQueryable()
                                .AnyAsync(g => g.Id.ToString() == holidayDto.LocationValue && !g.IsDeleted.Value);
                        }
                        else if (holidayDto.LocationType == 1) // Assuming 1 is for Store
                        {
                            locationValueExists = await _context.tblLocations.AsNoTracking().AsQueryable()
                                .AnyAsync(s => s.LocationId.ToString() == holidayDto.LocationValue && s.IsActive.Value);
                        }

                        if (!locationValueExists)
                        {
                            return new ExecuteAndReponse
                            {
                                Status = false,
                                Message = $"LocationValue '{holidayDto.LocationValue}' not found for the specified LocationType.",
                                Code = HttpStatusCode.BadRequest
                            };
                        }
                    }
                }

                if (holidayDto.Id.HasValue && holidayDto.Id.Value > 0)
                {
                    if (existingHoliday != null)
                    {
                        // For updates, check if another holiday with same name, date, location type and value exists (excluding current record)
                        var duplicateHoliday = await _context.HolidayMasters.AsNoTracking().AsQueryable()
                            .FirstOrDefaultAsync(h => h.HolidayName == holidayDto.HolidayName &&
                                                    h.HolidayDate == holidayDto.HolidayDate &&
                                                    h.LocationType == holidayDto.LocationType &&
                                                    h.LocationValue == holidayDto.LocationValue &&
                                                    h.Id != holidayDto.Id.Value &&
                                                    !h.IsDeleted.Value);

                        if (duplicateHoliday != null)
                        {
                            return new ExecuteAndReponse
                            {
                                Status = false,
                                Message = $"Holiday '{holidayDto.HolidayName}' on {holidayDto.HolidayDate.Value:dd/MM/yyyy} already exists for the specified location type and value.",
                                Code = HttpStatusCode.BadRequest
                            };
                        }

                        // Update existing holiday
                        existingHoliday.LocationType = holidayDto.LocationType;
                        existingHoliday.LocationValue = holidayDto.LocationValue;
                        existingHoliday.HolidayName = holidayDto.HolidayName;
                        existingHoliday.HolidayDate = holidayDto.HolidayDate.Value;
                        existingHoliday.IsActive = true;
                        existingHoliday.UpdatedBy = "System";
                        existingHoliday.UpdatedOn = DateTime.UtcNow;

                        await _context.SaveChangesAsync();

                        _logger.LogInformation("Holiday '{HolidayName}' updated successfully with ID: {Id}", existingHoliday.HolidayName, existingHoliday.Id);

                        return new ExecuteAndReponse
                        {
                            Status = true,
                            Message = "Holiday updated successfully",
                            Code = HttpStatusCode.OK
                        };
                    }
                    else
                    {
                        // ID provided but holiday not found
                        return new ExecuteAndReponse
                        {
                            Status = false,
                            Message = $"Holiday with ID {holidayDto.Id.Value} not found.",
                            Code = HttpStatusCode.BadRequest
                        };
                    }
                }
                else
                {
                    // Create new holiday
                    var holiday = new HolidayMaster
                    {
                        LocationType = holidayDto.LocationType,
                        LocationValue = holidayDto.LocationValue,
                        HolidayName = holidayDto.HolidayName,
                        HolidayDate = holidayDto.HolidayDate.Value,
                        IsActive = true,
                        IsDeleted = false,
                        CreatedBy = "System",
                        CreatedOn = DateTime.UtcNow,
                    };

                    await _context.HolidayMasters.AddAsync(holiday);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Holiday '{HolidayName}' created successfully with ID: {Id}", holiday.HolidayName, holiday.Id);

                    return new ExecuteAndReponse
                    {
                        Status = true,
                        Message = "Holiday created successfully",
                        Code = HttpStatusCode.OK
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while upserting holiday");
                return new ExecuteAndReponse
                {
                    Status = false,
                    Message = ex.Message,
                    Code = HttpStatusCode.BadRequest
                };
            }
        }

        public async Task<ExecuteAndReponse> DeleteHolidayAsync(int id)
        {
            if (id <= 0)
            {
                return new ExecuteAndReponse
                {
                    Status = false,
                    Message = "Invalid holiday ID.",
                    Code = HttpStatusCode.BadRequest
                };
            }

            try
            {
                var holiday = await _context.HolidayMasters
                    .FirstOrDefaultAsync(row => row.Id == id);

                if (holiday == null)
                {
                    _logger.LogWarning("Holiday with ID {Id} not found for deletion", id);
                    return new ExecuteAndReponse
                    {
                        Status = false,
                        Message = "Holiday not found for deletion",
                        Code = HttpStatusCode.BadRequest
                    };
                }

                // Soft delete
                holiday.IsDeleted = true;
                holiday.IsActive = false;
                holiday.UpdatedBy = "System";
                holiday.UpdatedOn = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                _logger.LogInformation("Holiday with ID {Id} deleted successfully", id);

                return new ExecuteAndReponse
                {
                    Status = true,
                    Message = "Holiday deleted successfully",
                    Code = HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while deleting holiday with ID: {Id}", id);
                return new ExecuteAndReponse
                {
                    Status = false,
                    Message = ex.Message,
                    Code = HttpStatusCode.BadRequest
                };
            }
        }

        public async Task<FetchAndResponse> GetAllHolidaysAsync(string storeCodeOrGroupName = null, int? month = null)
        {
            try
            {
                var holidays = await _context.GetProcedures().GetHolidaysAsync(storeCodeOrGroupName, month);

                if (holidays == null || !holidays.Any())
                {
                    return new FetchAndResponse
                    {
                        Status = false,
                        Message = "No holidays found",
                        Code = HttpStatusCode.BadRequest,
                        Data = null
                    };
                }

                _logger.LogInformation("Retrieved {Count} holidays", holidays.Count);
                return new FetchAndResponse
                {
                    Status = true,
                    Message = "Holidays fetched successfully",
                    Data = holidays,
                    Code = HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving holidays");
                return new FetchAndResponse
                {
                    Status = false,
                    Message = ex.Message,
                    Code = HttpStatusCode.BadRequest,
                    Data = null
                };
            }
        }

        public async Task<FetchAndResponse> UploadHolidaysAsync(IFormFile file)
        {
            var expectedHeaders = new[] { "LocationTypeName", "LocationValue", "HolidayName", "HolidayDate" };

            if (file == null || file.Length == 0)
                return new FetchAndResponse
                {
                    Status = false,
                    Message = "No file uploaded",
                    Code = HttpStatusCode.BadRequest,
                    Data = null
                };

            try
            {
                using var stream = file.OpenReadStream();
                using var workbook = new XLWorkbook(stream);
                var worksheet = workbook.Worksheet(1);

                // Validate headers
                for (int i = 0; i < expectedHeaders.Length; i++)
                {
                    var cellValue = worksheet.Cell(1, i + 1).GetValue<string>()?.Trim();
                    if (!string.Equals(cellValue, expectedHeaders[i], StringComparison.OrdinalIgnoreCase))
                        return new FetchAndResponse
                        {
                            Status = false,
                            Message = $"Header mismatch at column {i + 1}: Expected '{expectedHeaders[i]}', found '{cellValue}'",
                            Code = HttpStatusCode.BadRequest,
                            Data = null
                        };
                }

                if (worksheet.Row(1).CellsUsed().Count() != expectedHeaders.Length)
                    return new FetchAndResponse
                    {
                        Status = false,
                        Message = "Header count mismatch",
                        Code = HttpStatusCode.BadRequest,
                        Data = null
                    };

                var rows = worksheet.RowsUsed().Skip(1).ToList();
                var uploadData = new List<HolidayMasterUploadDto>();

                // Parse Excel data
                foreach (var row in rows)
                {
                    var locationTypeName = row.Cell(1).GetValue<string>()?.Trim();
                    var locationValue = row.Cell(2).GetValue<string>()?.Trim();
                    var holidayName = row.Cell(3).GetValue<string>()?.Trim();
                    var holidayDateStr = row.Cell(4).GetValue<string>()?.Trim();

                    if (!string.IsNullOrEmpty(locationTypeName) && !string.IsNullOrEmpty(holidayName) && !string.IsNullOrEmpty(holidayDateStr))
                    {
                        if (DateTime.TryParse(holidayDateStr, out DateTime holidayDate))
                        {
                            uploadData.Add(new HolidayMasterUploadDto
                            {
                                LocationTypeName = locationTypeName,
                                LocationValue = locationValue,
                                HolidayName = holidayName,
                                HolidayDate = holidayDate
                            });
                        }
                    }
                }

                if (!uploadData.Any())
                    return new FetchAndResponse
                    {
                        Status = false,
                        Message = "No valid data found in Excel",
                        Code = HttpStatusCode.BadRequest,
                        Data = null
                    };

                // Get all valid location type names
                var validLocationTypes = await _context.LocationTypeMasters.AsNoTracking().AsQueryable()
                    .Where(lt => !lt.IsDeleted.Value)
                    .ToDictionaryAsync(lt => lt.LocationTypeName.ToLower(), lt => lt.Id);

                var validGroup = await _context.GroupMasters.AsNoTracking().AsQueryable()
                    .Where(lt => !lt.IsDeleted.Value)
                    .ToDictionaryAsync(lt => lt.GroupName.ToLower(), lt => lt.Id);
                var validLocation = await _context.tblLocations.AsNoTracking().AsQueryable()
                    .Where(lt => lt.IsActive.Value)
                    .ToDictionaryAsync(lt => lt.STCode.ToLower(), lt => lt.LocationId);



                // Validate location type names
                var invalidLocationTypes = uploadData.Where(d => !validLocationTypes.ContainsKey(d.LocationTypeName.ToLower()))
                    .Select(d => d.LocationTypeName)
                    .Distinct()
                    .ToList();

                if (invalidLocationTypes.Any())
                {
                    var validLocationTypeNames = string.Join(", ", validLocationTypes.Keys);
                    return new FetchAndResponse
                    {
                        Status = false,
                        Message = $"Invalid location type names found: {string.Join(", ", invalidLocationTypes)}. Valid types are: {validLocationTypeNames}",
                        Code = HttpStatusCode.BadRequest,
                        Data = null
                    };
                }

                // Validate location values and check for duplicate holidays
                var duplicateHolidays = new List<string>();
                var invalidLocationValues = new List<string>();

                foreach (var data in uploadData)
                {
                    var locationTypeId = validLocationTypes[data.LocationTypeName.ToLower()];

                    // Check if location value exists
                    bool locationValueExists = false;
                    if (locationTypeId == 2) // Group
                    {
                        locationValueExists = validGroup.Keys.AsEnumerable()
                            .Any(g => g == data.LocationValue.ToLower());


                        //locationValueExists = await _context.GroupMasters.AsNoTracking().AsQueryable()
                        //    .AnyAsync(g => g.GroupName == data.LocationValue && !g.IsDeleted.Value);

                    }
                    else if (locationTypeId == 1) // Store
                    {
                        locationValueExists = validLocation.Keys.AsEnumerable()
                            .Any(g => g == data.LocationValue.ToLower());
                        //locationValueExists = await _context.tblLocations.AsNoTracking().AsQueryable()
                        //    .AnyAsync(s => s.STCode == data.LocationValue && s.IsActive.Value);

                    }

                    if (!locationValueExists)
                    {
                        invalidLocationValues.Add($"{data.LocationValue} (Type: {data.LocationTypeName})");
                    }
                    if (!validLocation.ContainsKey(data.LocationValue.ToLower()) && !validGroup.ContainsKey(data.LocationValue.ToLower()))
                    {
                        throw new Exception($"No Location with name : {data.LocationTypeName} exists");
                    }
                    int id = 0;
                    if (validLocation.ContainsKey(data.LocationValue.ToLower()))
                        id = validLocation[data.LocationValue.ToLower()];

                    if (validGroup.ContainsKey(data.LocationValue.ToLower()))
                        id = validGroup[data.LocationValue.ToLower()];
                    // Check for duplicate holiday (same name and date)
                    var duplicateExists = await _context.HolidayMasters.AsNoTracking().AsQueryable()
                .AnyAsync(h => h.HolidayName == data.HolidayName &&
                              h.HolidayDate == data.HolidayDate &&
                              h.LocationType == validLocationTypes[data.LocationTypeName.ToLower()] &&
                              (h.LocationValue == id.ToString()) &&
                              !h.IsDeleted.Value);

                    if (duplicateExists)
                    {
                        duplicateHolidays.Add($"{data.HolidayName} on {data.HolidayDate.Value:dd/MM/yyyy}");
                    }
                }

                if (invalidLocationValues.Any())
                {
                    return new FetchAndResponse
                    {
                        Status = false,
                        Message = $"Invalid location values found: {string.Join(", ", invalidLocationValues)}",
                        Code = HttpStatusCode.BadRequest,
                        Data = null
                    };
                }

                if (duplicateHolidays.Any())
                {
                    return new FetchAndResponse
                    {
                        Status = false,
                        Message = $"Duplicate holidays found: {string.Join(", ", duplicateHolidays)}",
                        Code = HttpStatusCode.BadRequest,
                        Data = null
                    };
                }

                // Process upload data
                var newHolidays = new List<HolidayMaster>();
                foreach (var data in uploadData)
                {
                    var locationTypeId = validLocationTypes[data.LocationTypeName.ToLower()];
                    int locationValueId = 0;
                    if (locationTypeId == 1)
                    {
                        locationValueId = validLocation[data.LocationValue.ToLower()];
                    }
                    else if (locationTypeId == 2)
                    {
                        locationValueId = validGroup[data.LocationValue.ToLower()];
                    }

                    if (locationValueId > 0)
                    {
                        newHolidays.Add(new HolidayMaster
                        {
                            LocationType = locationTypeId,
                            LocationValue = locationValueId.ToString(),
                            HolidayName = data.HolidayName,
                            HolidayDate = data.HolidayDate.Value,
                            IsActive = true,
                            IsDeleted = false,
                            CreatedBy = "System",
                            CreatedOn = DateTime.UtcNow,
                            UpdatedBy = "System",
                            UpdatedOn = DateTime.UtcNow
                        });
                    }
                }

                await _context.HolidayMasters.AddRangeAsync(newHolidays);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully uploaded {Count} holidays", newHolidays.Count);

                return new FetchAndResponse
                {
                    Status = true,
                    Message = $"Successfully uploaded {newHolidays.Count} holidays",
                    Data = newHolidays,
                    Code = HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while uploading holidays");
                return new FetchAndResponse
                {
                    Status = false,
                    Message = ex.Message,
                    Code = HttpStatusCode.BadRequest,
                    Data = null
                };
            }
        }

        public async Task UpsertPolicyDesignation(List<LocationDesignationPolicyDto> policies,JwtLoginDetailDto createdBy)
        {
            using var connection = _context.Database.GetDbConnection();
            await connection.OpenAsync();

            using var transaction = connection.BeginTransaction();

            try
            {
                foreach (var item in policies)
                {
                    using var command = connection.CreateCommand();
                    command.Transaction = transaction;
                    command.CommandText = "usp_UpsertLocationDesignationPolicy";
                    command.CommandType = CommandType.StoredProcedure;

                    command.Parameters.Add(new SqlParameter("@LocationDesignationPolicyId",
                        item.LocationDesignationPolicyId ?? (object)DBNull.Value));

                    command.Parameters.Add(new SqlParameter("@LocationCategoryId", item.LocationCategoryId));

                    command.Parameters.Add(new SqlParameter("@DesignationId",item.DesignationId
                        ?? (object)DBNull.Value));

                    command.Parameters.Add(new SqlParameter("@TotalAttendanceFrom",item.TotalAttendanceFrom));

                    command.Parameters.Add(new SqlParameter("@TotalAttendance",item.TotalAttendanceTo));

                    command.Parameters.Add(new SqlParameter("@WeeklyOff", item.WeeklyOff));

                    command.Parameters.Add(new SqlParameter("@ForWhichWeeks", item.ForWhichWeeks
                        ?? (object)DBNull.Value));

                    command.Parameters.Add(new SqlParameter("@MonthYear", item.MonthYear));

                    if(item.IsActive == null)
                        item.IsActive = true;

                    command.Parameters.Add(new SqlParameter("@IsActive", item.IsActive));

                    command.Parameters.Add(new SqlParameter("@CreatedBy", createdBy.EmployeeId));

                    await command.ExecuteNonQueryAsync();
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
            finally
            {
                await connection.CloseAsync();
            }
        }

        //public async Task<PagedResult<LocationDesignationPolicyResponseDto>>GetByMonthYearAsync(string monthYear,int pageNumber,int pageSize,string? searchTerm)
        //{
        //    try
        //    {
        //        var result = new List<LocationDesignationPolicyResponseDto>();
        //        int totalCount = 0;

        //        using var connection = _context.Database.GetDbConnection();
        //        await connection.OpenAsync();

        //        using var command = connection.CreateCommand();
        //        command.CommandText = "usp_GetLocationDesignationPolicy_ByMonthYear";
        //        command.CommandType = CommandType.StoredProcedure;

        //        command.Parameters.Add(new SqlParameter(
        //            "@MonthYear",
        //            string.IsNullOrWhiteSpace(monthYear)
        //                ? (object)DBNull.Value
        //                : monthYear));

        //        command.Parameters.Add(new SqlParameter("@PageNumber", pageNumber));
        //        command.Parameters.Add(new SqlParameter("@PageSize", pageSize));
        //        command.Parameters.Add(new SqlParameter(
        //            "@SearchTerm",
        //            string.IsNullOrWhiteSpace(searchTerm)
        //                ? (object)DBNull.Value
        //                : searchTerm));

        //        using var reader = await command.ExecuteReaderAsync();

        //        while (await reader.ReadAsync())
        //        {
        //            if (totalCount == 0 && reader["TotalCount"] != DBNull.Value)
        //            {
        //                totalCount = Convert.ToInt32(reader["TotalCount"]);
        //            }

        //            result.Add(new LocationDesignationPolicyResponseDto
        //            {
        //                LocationDesignationPolicyId = reader.GetInt32(
        //                    reader.GetOrdinal("LocationDesignationPolicyId")),
        //                LocationCategoryId = reader["LocationCategoryId"]?.ToString(),
        //                //LocationCategoryName = reader["LocationCategoryName"]?.ToString(),
        //                LocationCategoryName = $"{reader["LocationCategoryId"]} - {reader["LocationCategoryName"]}",
        //                DesignationId = reader["DesignationId"] == DBNull.Value? (int?)null: Convert.ToInt32(reader["DesignationId"]),
        //                DesignationName = reader["DesignationName"] == DBNull.Value? null: reader["DesignationName"].ToString(),
        //                TotalAttendance = reader["TotalAttendance"]?.ToString(),
        //                WeeklyOff = Convert.ToDecimal(reader["WeeklyOff"]),
        //                ForWhichWeeks = reader["ForWhichWeeks"] == DBNull.Value? (int?)null: Convert.ToInt32(reader["ForWhichWeeks"]),
        //                MonthYear = reader["MonthYear"]?.ToString(),
        //                TotalAttendanceFrom = reader["TotalAttendanceFrom"] == DBNull.Value? (decimal?)null: Convert.ToDecimal(reader["TotalAttendanceFrom"]),
        //                TotalAttendanceTo = reader["TotalAttendanceTo"] == DBNull.Value? (decimal?)null: Convert.ToDecimal(reader["TotalAttendanceTo"]),
        //                IsActive = Convert.ToBoolean(reader["IsActive"])
        //            });
        //        }

        //        return new PagedResult<LocationDesignationPolicyResponseDto>(result,totalCount);
        //    }
        //    catch
        //    {
        //        throw;
        //    }
        //}

        public async Task<PagedResult<LocationDesignationPolicyResponseDto>>GetByMonthYearAsync(string monthYear,string? searchTerm)
        {
            var result = new List<LocationDesignationPolicyResponseDto>();
            int totalCount = 0;

            using var connection = _context.Database.GetDbConnection();
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = "usp_GetLocationDesignationPolicy_ByMonthYear";
            command.CommandType = CommandType.StoredProcedure;

            command.Parameters.Add(new SqlParameter("@MonthYear", monthYear));;
            command.Parameters.Add(new SqlParameter(
                "@SearchTerm",
                string.IsNullOrWhiteSpace(searchTerm)
                    ? (object)DBNull.Value
                    : searchTerm));

            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                if (totalCount == 0 && reader["TotalCount"] != DBNull.Value)
                    totalCount = Convert.ToInt32(reader["TotalCount"]);

                result.Add(new LocationDesignationPolicyResponseDto
                {
                    LocationDesignationPolicyId =
                        Convert.ToInt32(reader["LocationDesignationPolicyId"]),

                    LocationCategoryId =
                        reader["LocationCategoryId"]?.ToString(),

                    LocationCategoryName =
    reader["LocationCategoryId"] == DBNull.Value
        ? (reader["LocationCategoryName"] == DBNull.Value
            ? null
            : reader["LocationCategoryName"].ToString())
        : (reader["LocationCategoryName"] == DBNull.Value
            ? reader["LocationCategoryId"].ToString()
            : $"{reader["LocationCategoryId"]} - {reader["LocationCategoryName"]}"),

                    DesignationId =
                        reader["DesignationId"] == DBNull.Value
                            ? null
                            : Convert.ToInt32(reader["DesignationId"]),

                    DesignationName =
                        reader["DesignationName"] == DBNull.Value
                            ? null
                            : reader["DesignationName"].ToString(),

                    TotalAttendanceFrom =
                        reader["TotalAttendanceFrom"] == DBNull.Value
                            ? null
                            : Convert.ToDecimal(reader["TotalAttendanceFrom"]),

                    TotalAttendanceTo =
                        reader["TotalAttendance"] == DBNull.Value
                            ? null
                            : Convert.ToDecimal(reader["TotalAttendance"]),

                    WeeklyOff =
                        Convert.ToDecimal(reader["WeeklyOff"]),

                    ForWhichWeeks =
                        reader["ForWhichWeeks"] == DBNull.Value
                            ? null
                            : Convert.ToInt32(reader["ForWhichWeeks"]),

                    MonthYear =
                        reader["MonthYear"]?.ToString(),

                    IsActive =
    reader["IsActive"] == DBNull.Value
        ? false     // or true, based on business rule
        : Convert.ToBoolean(reader["IsActive"])
                });
            }

            return new PagedResult<LocationDesignationPolicyResponseDto>(
                result, totalCount);
        }

        public async Task ToggleActiveStatusAsync(List<int> ids,bool isActive,JwtLoginDetailDto updatedBy)
        {
            try
            {
                var records = await _context.tblLocationDesignationPolicies
                    .Where(x => ids.Contains(x.LocationDesignationPolicyId))
                    .ToListAsync();

                if (!records.Any())
                    throw new Exception("No valid records found.");

                var now = DateTime.Now;

                foreach (var item in records)
                {
                    var history = new tblLocationDesignationPolicyHistory
                    {
                        LocationDesignationPolicyId = item.LocationDesignationPolicyId,
                        LocationCategoryId = item.LocationCategoryId,
                        LocationCategoryName = item.LocationCategoryName,
                        DesignationId = item.DesignationId,
                        DesignationName = item.DesignationName,
                        TotalAttendanceFrom = item.TotalAttendanceFrom,
                        TotalAttendanceTo = item.TotalAttendanceTo,
                        WeeklyOff = item.WeeklyOff,
                        ForWhichWeeks = item.ForWhichWeeks,
                        Month_Year = item.Month_Year,
                        IsActive = item.IsActive,          // previous state
                        IsDeleted = item.IsDeleted,
                        ActionType = isActive ? "ACTIVATE" : "DEACTIVATE",
                        ActionBy = updatedBy.EmployeeId,
                        ActionOn = now
                    };

                    _context.tblLocationDesignationPolicyHistories.Add(history);

                    item.IsActive = isActive;
                    item.isActiveBy = updatedBy.EmployeeId;   // ✅ NEW
                    item.isActiveOn = now;                     // ✅ NEW
                    item.UpdatedBy = updatedBy.EmployeeId;
                    item.UpdatedOn = now;
                }

                await _context.SaveChangesAsync();
            }
            catch
            {
                throw;
            }
        }

        public async Task<List<LocationDesignationPolicyResponseDto>>GetByMonthYearForExcelAsync(string monthYear,string? searchTerm)
        {
            var result =await GetByMonthYearAsync(monthYear,searchTerm);
            return result.Data;
        }

        public static class ExcelHelper
        {
            public static FileResult GeneratePolicyExcel(List<LocationDesignationPolicyResponseDto> data,string fileName)
            {
                using var workbook = new XLWorkbook();
                var sheet = workbook.Worksheets.Add("Policy");

                string[] headers ={"Location","Designation","Attendance From","Attendance To","Weekly Off",
                                   //"Weeks",
                    "Month Year","Active"};

                for (int i = 0; i < headers.Length; i++)
                {
                    sheet.Cell(1, i + 1).Value = headers[i];
                    sheet.Cell(1, i + 1).Style.Font.Bold = true;
                    sheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
                }

                int row = 2;
                foreach (var item in data)
                {
                    sheet.Cell(row, 1).Value = item.LocationCategoryName;
                    sheet.Cell(row, 2).Value = item.DesignationName ?? "ALL";
                    sheet.Cell(row, 3).Value = item.TotalAttendanceFrom;
                    sheet.Cell(row, 4).Value = item.TotalAttendanceTo;
                    sheet.Cell(row, 5).Value = item.WeeklyOff;
                    //sheet.Cell(row, 6).Value = item.ForWhichWeeks;
                    sheet.Cell(row, 6).Value = item.MonthYear;
                    sheet.Cell(row, 7).Value = item.IsActive ? "Yes" : "No";
                    row++;
                }

                sheet.Columns().AdjustToContents();
                sheet.SheetView.FreezeRows(1);

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);

                return new FileContentResult(stream.ToArray(),"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
                {
                    FileDownloadName = fileName
                };
            }
        }

        public async Task<int> ImportPolicyDesignationAsync(IFormFile file,JwtLoginDetailDto createdBy)
        {
            var policies = new List<LocationDesignationPolicyDto>();

            // Load designation master once
            var designationMap = await _context.tblDesignations
                .ToDictionaryAsync(
                    x => x.DesignationName.Trim().ToLower(),
                    x => x.DesignationId);

            using (var workbook = new XLWorkbook(file.OpenReadStream()))
            {
                var sheet = workbook.Worksheet(1);
                var rows = sheet.RowsUsed().Skip(1); // skip header

                int rowNumber = 1;

                foreach (var row in rows)
                {
                    rowNumber++;

                    try
                    {
                        var locationCategoryId = row.Cell(1).GetString()?.Trim();
                        if (string.IsNullOrWhiteSpace(locationCategoryId))
                            throw new Exception("LocationCategoryId is required");

                        var designationName = row.Cell(2).GetString()?.Trim();
                        int? designationId = null;

                        if (!string.IsNullOrWhiteSpace(designationName))
                        {
                            var key = designationName.ToLower();

                            if (!designationMap.TryGetValue(key, out var resolvedId))
                                throw new Exception($"Invalid Designation '{designationName}'");

                            designationId = resolvedId;
                        }

                        int? forWhichWeeks = null;
                        var weeksCell = row.Cell(6);

                        if (!weeksCell.IsEmpty())
                        {
                            if (weeksCell.TryGetValue<int>(out var weeks))
                                forWhichWeeks = weeks;
                            else if (weeksCell.TryGetValue<double>(out var d))
                                forWhichWeeks = Convert.ToInt32(d);
                        }

                        policies.Add(new LocationDesignationPolicyDto
                        {
                            LocationDesignationPolicyId = 0,

                            LocationCategoryId =
                                row.Cell(1).GetString()?.Trim(),

                            DesignationId = designationId,   

                            TotalAttendanceFrom =
                                row.Cell(3).GetValue<decimal>(),

                            TotalAttendanceTo =
                                row.Cell(4).GetValue<string>(),

                            WeeklyOff =
                                row.Cell(5).GetValue<decimal>(),

                            ForWhichWeeks = forWhichWeeks,

                            MonthYear =
                                 ConvertToMonthYear(row.Cell(7)),

                            IsActive = true
                        });
                    }
                    catch (Exception ex)
                    {
                        throw new Exception(
                            $"Invalid data at Excel row {rowNumber}: {ex.Message}");
                    }
                }
            }

            await UpsertPolicyDesignation(policies, createdBy);

            return policies.Count;
        }
        private static string ConvertToMonthYear(IXLCell cell)
        {
            // Excel date cell
            if (cell.DataType == XLDataType.DateTime)
            {
                return cell.GetDateTime().ToString("MMM-yy", CultureInfo.InvariantCulture);
            }

            var value = cell.GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(value))
                throw new Exception("MonthYear is required");

            // Already MMM-yy
            if (DateTime.TryParseExact(
                value,
                "MMM-yy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
            {
                return parsed.ToString("MMM-yy");
            }

            // Any date format
            if (DateTime.TryParse(value, out var date))
            {
                return date.ToString("MMM-yy");
            }

            throw new Exception($"Invalid MonthYear '{value}'");
        }

    }

}
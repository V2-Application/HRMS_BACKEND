using ClosedXML.Excel;
using HRMSAPI.Data;
using HRMSAPI.DTO;
using HRMSAPI.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Roomsy.DTOS.GenericsResponses;
using System.Data;

namespace HRMSAPI.Implementation
{
    public class NetPaybleBatchService : INetPaybleBatchService
    {
        private readonly HRMSContext _context;
        private readonly ILogger<NetPaybleBatchService> _logger;

        public NetPaybleBatchService(HRMSContext context, ILogger<NetPaybleBatchService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<FetchAndResponse> GetNetPaybleBatchListAsync(string? ecode = null, bool asExcel = false, int? page = null, int? pageSize = null)
        {
            try
            {
                if (asExcel)
                {
                    var excelBytes = await ExportNetPaybleBatchToExcelAsync(ecode);
                    return new FetchAndResponse
                    {
                        Status = true,
                        Message = "Net Payable Batch data exported successfully",
                        Code = System.Net.HttpStatusCode.OK,
                        Data = excelBytes
                    };
                }

                var data = await GetNetPaybleBatchDataAsync(ecode, page, pageSize);
                return new FetchAndResponse
                {
                    Status = true,
                    Message = "Net Payable Batch data retrieved successfully",
                    Code = System.Net.HttpStatusCode.OK,
                    Data = data
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving Net Payable Batch data for Ecode: {Ecode}", ecode);
                return new FetchAndResponse
                {
                    Status = false,
                    Message = "An error occurred while retrieving Net Payable Batch data",
                    Code = System.Net.HttpStatusCode.InternalServerError,
                    Data = null
                };
            }
        }

        public async Task<byte[]> ExportNetPaybleBatchToExcelAsync(string? ecode = null)
        {
            try
            {
                var data = await GetNetPaybleBatchDataAsync(ecode);
                
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("NetPaybleBatch");

                // Add headers
                var headers = new[]
                {
                    "Unique ID", "Ecode", "Location Code", "Location Name", "Employee Name", "Designation", "Department", 
                    "Month Year", "Budget Salary", "Gross Earnings", "Additions", "Deductions", "Reimbursement", 
                    "Net Payable (After Deduction with Addition)", "Net Payable (Without Reimbursement)", "Run At", "Batch No"
                };

                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(1, i + 1).Value = headers[i];
                    worksheet.Cell(1, i + 1).Style.Font.Bold = true;
                }

                // Add data
                for (int i = 0; i < data.Count; i++)
                {
                    var item = data[i];
                    int row = i + 2;
                    
                    worksheet.Cell(row, 1).Value = item.UniqueId;
                    worksheet.Cell(row, 2).Value = item.Ecode;
                    worksheet.Cell(row, 3).Value = item.LocationCode;
                    worksheet.Cell(row, 4).Value = item.LocationName;
                    worksheet.Cell(row, 5).Value = item.EmployeeName;
                    worksheet.Cell(row, 6).Value = item.Designation;
                    worksheet.Cell(row, 7).Value = item.Department;
                    worksheet.Cell(row, 8).Value = item.MonthYear;
                    worksheet.Cell(row, 9).Value = item.BgtSalary;
                    worksheet.Cell(row, 10).Value = item.GrossEarnings;
                    worksheet.Cell(row, 11).Value = item.Additions;
                    worksheet.Cell(row, 12).Value = item.Deductions;
                    worksheet.Cell(row, 13).Value = item.Reimbursement;
                    worksheet.Cell(row, 14).Value = item.NetPaybleAfterDeductionWithAddition;
                    worksheet.Cell(row, 15).Value = item.NetPaybleWithoutReimbursement;
                    worksheet.Cell(row, 16).Value = item.RunAt;
                    worksheet.Cell(row, 17).Value = item.BatchNo;
                }

                // Auto-fit columns
                worksheet.ColumnsUsed().AdjustToContents();

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                return stream.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting Net Payable Batch data to Excel for Ecode: {Ecode}", ecode);
                throw;
            }
        }

        public async Task<List<NetPaybleBatchDto>> GetNetPaybleBatchDataAsync(string? ecode = null, int? page = null, int? pageSize = null)
        {
            try
            {
                var netPaybleBatches = new List<NetPaybleBatchDto>();

                using (var connection = _context.Database.GetDbConnection())
                {
                    await connection.OpenAsync();

                    using (var command = connection.CreateCommand())
                    {
                        var whereClause = string.IsNullOrWhiteSpace(ecode) ? "" : "WHERE Ecode = @Ecode";
                        var paginationClause = "";
                        
                        if (page.HasValue && pageSize.HasValue)
                        {
                            var offset = (page.Value - 1) * pageSize.Value;
                            paginationClause = $"ORDER BY Ecode OFFSET {offset} ROWS FETCH NEXT {pageSize.Value} ROWS ONLY";
                        }

                        command.CommandText = $@"
                            SELECT 
                                UniqueId, Ecode, Location_Code, [Location Name], [Employee Name], designation, department,
                                [Month-Year], [Bgt Salary], [Gross Earnings], Additions, Deductions, Reimbersment,
                                [Net Payble(After Deduction with Addition)], [Net Payble(Without Reimbersment)], RunAt, BatchNo
                            FROM [dbo].[Net_Payble_Batch] 
                            {whereClause}
                            {paginationClause}";

                        command.CommandType = CommandType.Text;

                        if (!string.IsNullOrWhiteSpace(ecode))
                        {
                            var ecodeParam = command.CreateParameter();
                            ecodeParam.ParameterName = "@Ecode";
                            ecodeParam.DbType = DbType.String;
                            ecodeParam.Value = ecode;
                            command.Parameters.Add(ecodeParam);
                        }

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                netPaybleBatches.Add(new NetPaybleBatchDto
                                {
                                    UniqueId = reader.IsDBNull(reader.GetOrdinal("UniqueId")) ? null : reader.GetString(reader.GetOrdinal("UniqueId")),
                                    Ecode = reader.IsDBNull(reader.GetOrdinal("Ecode")) ? null : reader.GetString(reader.GetOrdinal("Ecode")),
                                    LocationCode = reader.IsDBNull(reader.GetOrdinal("Location_Code")) ? null : reader.GetString(reader.GetOrdinal("Location_Code")),
                                    LocationName = reader.IsDBNull(reader.GetOrdinal("Location Name")) ? null : reader.GetString(reader.GetOrdinal("Location Name")),
                                    EmployeeName = reader.IsDBNull(reader.GetOrdinal("Employee Name")) ? null : reader.GetString(reader.GetOrdinal("Employee Name")),
                                    Designation = reader.IsDBNull(reader.GetOrdinal("designation")) ? null : reader.GetString(reader.GetOrdinal("designation")),
                                    Department = reader.IsDBNull(reader.GetOrdinal("department")) ? null : reader.GetString(reader.GetOrdinal("department")),
                                    MonthYear = reader.IsDBNull(reader.GetOrdinal("Month-Year")) ? null : reader.GetString(reader.GetOrdinal("Month-Year")),
                                    BgtSalary = reader.IsDBNull(reader.GetOrdinal("Bgt Salary")) ? null : reader.GetDecimal(reader.GetOrdinal("Bgt Salary")),
                                    GrossEarnings = reader.IsDBNull(reader.GetOrdinal("Gross Earnings")) ? null : reader.GetDecimal(reader.GetOrdinal("Gross Earnings")),
                                    Additions = reader.IsDBNull(reader.GetOrdinal("Additions")) ? null : reader.GetDecimal(reader.GetOrdinal("Additions")),
                                    Deductions = reader.IsDBNull(reader.GetOrdinal("Deductions")) ? null : reader.GetDecimal(reader.GetOrdinal("Deductions")),
                                    Reimbursement = reader.IsDBNull(reader.GetOrdinal("Reimbersment")) ? null : reader.GetDecimal(reader.GetOrdinal("Reimbersment")),
                                    NetPaybleAfterDeductionWithAddition = reader.IsDBNull(reader.GetOrdinal("Net Payble(After Deduction with Addition)")) ? null : reader.GetDecimal(reader.GetOrdinal("Net Payble(After Deduction with Addition)")),
                                    NetPaybleWithoutReimbursement = reader.IsDBNull(reader.GetOrdinal("Net Payble(Without Reimbersment)")) ? null : reader.GetDecimal(reader.GetOrdinal("Net Payble(Without Reimbersment)")),
                                    RunAt = reader.IsDBNull(reader.GetOrdinal("RunAt")) ? null : reader.GetDateTime(reader.GetOrdinal("RunAt")),
                                    BatchNo = reader.IsDBNull(reader.GetOrdinal("BatchNo")) ? null : reader.GetInt32(reader.GetOrdinal("BatchNo"))
                                });
                            }
                        }
                    }
                }

                return netPaybleBatches;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving Net Payable Batch data for Ecode: {Ecode}", ecode);
                throw;
            }
        }
    }
}

using ClosedXML.Excel;
using HRMSAPI.Data;
using HRMSAPI.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Roomsy.DTOS.GenericsResponses;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace HRMSAPI.Implementation
{
    public class ViewsService : BaseService,IViewService
    {
        private readonly HRMSContext _context;
        public ViewsService(HRMSContext context) : base(context) {
        
            _context = context;
        }

        public async Task<byte[]> ExportEmpAttendanceFormatToExcelAsync(string ecode = null)
        {
            var conn = _context.Database.GetDbConnection();
            await conn.OpenAsync();
            using (var cmd = conn.CreateCommand())
            {
                if (!string.IsNullOrWhiteSpace(ecode))
                {
                    cmd.CommandText = "SELECT * FROM [HRMS].[dbo].[vw_Emp_Attendance_Format] (NOLOCK) WHERE [Ecode] = @Ecode";
                    var param = cmd.CreateParameter();
                    param.ParameterName = "@Ecode";
                    param.Value = ecode;
                    cmd.Parameters.Add(param);
                }
                else
                {
                    cmd.CommandText = "SELECT * FROM [HRMS].[dbo].[vw_Emp_Attendance_Format] (NOLOCK)";
                }
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    var dt = new DataTable();
                    dt.Load(reader);

                    using (var workbook = new XLWorkbook())
                    {
                        var worksheet = workbook.Worksheets.Add("AttendanceFormat");
                        for (int i = 0; i < dt.Columns.Count; i++)
                        {
                            worksheet.Cell(1, i + 1).Value = dt.Columns[i].ColumnName;
                        }
                        for (int row = 0; row < dt.Rows.Count; row++)
                        {
                            for (int col = 0; col < dt.Columns.Count; col++)
                            {
                                var cellValue = dt.Rows[row][col];
                                worksheet.Cell(row + 2, col + 1).Value = cellValue == DBNull.Value ? "" : cellValue.ToString();
                            }
                        }
                        using (var stream = new MemoryStream())
                        {
                            workbook.SaveAs(stream);
                            return stream.ToArray();
                        }
                    }
                }
            }
        }

        public async Task<byte[]> ExportBgtSalaryStructWithEmpDetailsToExcelAsync(string ecode = null)
        {
            var conn = _context.Database.GetDbConnection();
            await conn.OpenAsync();
            using (var cmd = conn.CreateCommand())
            {
                if (!string.IsNullOrWhiteSpace(ecode))
                {
                    cmd.CommandText = @"SELECT * FROM [HRMS].[dbo].[vw_BgtSalaryStructWithEmpDetails] (NOLOCK) WHERE [ecode] = @Ecode";
                    var param = cmd.CreateParameter();
                    param.ParameterName = "@Ecode";
                    param.Value = ecode;
                    cmd.Parameters.Add(param);
                }
                else
                {
                    cmd.CommandText = @"SELECT * FROM [HRMS].[dbo].[vw_BgtSalaryStructWithEmpDetails] (NOLOCK)";
                }
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    var dt = new System.Data.DataTable();
                    dt.Load(reader);

                    using (var workbook = new ClosedXML.Excel.XLWorkbook())
                    {
                        var worksheet = workbook.Worksheets.Add("BgtSalaryStructWithEmpDetails");
                        for (int i = 0; i < dt.Columns.Count; i++)
                        {
                            worksheet.Cell(1, i + 1).Value = dt.Columns[i].ColumnName;
                        }
                        for (int row = 0; row < dt.Rows.Count; row++)
                        {
                            for (int col = 0; col < dt.Columns.Count; col++)
                            {
                                var cellValue = dt.Rows[row][col];
                                worksheet.Cell(row + 2, col + 1).Value = cellValue == System.DBNull.Value ? "" : cellValue.ToString();
                            }
                        }
                        using (var stream = new System.IO.MemoryStream())
                        {
                            workbook.SaveAs(stream);
                            return stream.ToArray();
                        }
                    }
                }
            }
        }

        public async Task<byte[]> ExportLeaveMasterToExcelAsync(string ecode = null)
        {
            var conn = _context.Database.GetDbConnection();
            await conn.OpenAsync();
            using (var cmd = conn.CreateCommand())
            {
                if (!string.IsNullOrWhiteSpace(ecode))
                {
                    cmd.CommandText = @"SELECT * FROM [HRMS].[dbo].[vw_LeaveMaster] (NOLOCK) WHERE [ECODE] = @Ecode";
                    var param = cmd.CreateParameter();
                    param.ParameterName = "@Ecode";
                    param.Value = ecode;
                    cmd.Parameters.Add(param);
                }
                else
                {
                    cmd.CommandText = @"SELECT * FROM [HRMS].[dbo].[vw_LeaveMaster] (NOLOCK)";
                }
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    var dt = new System.Data.DataTable();
                    dt.Load(reader);

                    using (var workbook = new ClosedXML.Excel.XLWorkbook())
                    {
                        var worksheet = workbook.Worksheets.Add("LeaveMaster");
                        for (int i = 0; i < dt.Columns.Count; i++)
                        {
                            worksheet.Cell(1, i + 1).Value = dt.Columns[i].ColumnName;
                        }
                        for (int row = 0; row < dt.Rows.Count; row++)
                        {
                            for (int col = 0; col < dt.Columns.Count; col++)
                            {
                                var cellValue = dt.Rows[row][col];
                                worksheet.Cell(row + 2, col + 1).Value = cellValue == System.DBNull.Value ? "" : cellValue.ToString();
                            }
                        }
                        using (var stream = new System.IO.MemoryStream())
                        {
                            workbook.SaveAs(stream);
                            return stream.ToArray();
                        }
                    }
                }
            }
        }

        public async Task<byte[]> ExportPfMasterToExcelAsync(string ecode = null)
        {
            var conn = _context.Database.GetDbConnection();
            await conn.OpenAsync();
            using (var cmd = conn.CreateCommand())
            {
                if (!string.IsNullOrWhiteSpace(ecode))
                {
                    cmd.CommandText = @"SELECT * FROM [HRMS].[dbo].[vw_PfMaster] (NOLOCK) WHERE [ECODE] = @Ecode";
                    var param = cmd.CreateParameter();
                    param.ParameterName = "@Ecode";
                    param.Value = ecode;
                    cmd.Parameters.Add(param);
                }
                else
                {
                    cmd.CommandText = @"SELECT * FROM [HRMS].[dbo].[vw_PfMaster] (NOLOCK)";
                }
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    var dt = new System.Data.DataTable();
                    dt.Load(reader);

                    using (var workbook = new ClosedXML.Excel.XLWorkbook())
                    {
                        var worksheet = workbook.Worksheets.Add("PfMaster");
                        for (int i = 0; i < dt.Columns.Count; i++)
                        {
                            worksheet.Cell(1, i + 1).Value = dt.Columns[i].ColumnName;
                        }
                        for (int row = 0; row < dt.Rows.Count; row++)
                        {
                            for (int col = 0; col < dt.Columns.Count; col++)
                            {
                                var cellValue = dt.Rows[row][col];
                                worksheet.Cell(row + 2, col + 1).Value = cellValue == System.DBNull.Value ? "" : cellValue.ToString();
                            }
                        }
                        using (var stream = new System.IO.MemoryStream())
                        {
                            workbook.SaveAs(stream);
                            return stream.ToArray();
                        }
                    }
                }
            }
        }

        public async Task<byte[]> ExportEsicMasterToExcelAsync(string ecode = null)
        {
            var conn = _context.Database.GetDbConnection();
            await conn.OpenAsync();
            using (var cmd = conn.CreateCommand())
            {
                if (!string.IsNullOrWhiteSpace(ecode))
                {
                    cmd.CommandText = @"SELECT * FROM [HRMS].[dbo].[vw_EsicMaster] (NOLOCK) WHERE [ECODE] = @Ecode";
                    var param = cmd.CreateParameter();
                    param.ParameterName = "@Ecode";
                    param.Value = ecode;
                    cmd.Parameters.Add(param);
                }
                else
                {
                    cmd.CommandText = @"SELECT * FROM [HRMS].[dbo].[vw_EsicMaster] (NOLOCK)";
                }
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    var dt = new System.Data.DataTable();
                    dt.Load(reader);

                    using (var workbook = new ClosedXML.Excel.XLWorkbook())
                    {
                        var worksheet = workbook.Worksheets.Add("EsicMaster");
                        for (int i = 0; i < dt.Columns.Count; i++)
                        {
                            worksheet.Cell(1, i + 1).Value = dt.Columns[i].ColumnName;
                        }
                        for (int row = 0; row < dt.Rows.Count; row++)
                        {
                            for (int col = 0; col < dt.Columns.Count; col++)
                            {
                                var cellValue = dt.Rows[row][col];
                                worksheet.Cell(row + 2, col + 1).Value = cellValue == System.DBNull.Value ? "" : cellValue.ToString();
                            }
                        }
                        using (var stream = new System.IO.MemoryStream())
                        {
                            workbook.SaveAs(stream);
                            return stream.ToArray();
                        }
                    }
                }
            }
        }

        public async Task<byte[]> ExportTotalDeductionToExcelAsync(string ecode = null)
        {
            var conn = _context.Database.GetDbConnection();
            await conn.OpenAsync();
            using (var cmd = conn.CreateCommand())
            {
                if (!string.IsNullOrWhiteSpace(ecode))
                {
                    cmd.CommandText = @"SELECT * FROM [HRMS].[dbo].[vw_TotalDeduction] (NOLOCK) WHERE [ECODE] = @Ecode";
                    var param = cmd.CreateParameter();
                    param.ParameterName = "@Ecode";
                    param.Value = ecode;
                    cmd.Parameters.Add(param);
                }
                else
                {
                    cmd.CommandText = @"SELECT * FROM [HRMS].[dbo].[vw_TotalDeduction] (NOLOCK)";
                }
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    var dt = new System.Data.DataTable();
                    dt.Load(reader);

                    using (var workbook = new ClosedXML.Excel.XLWorkbook())
                    {
                        var worksheet = workbook.Worksheets.Add("TotalDeduction");
                        // Write headers dynamically
                        for (int i = 0; i < dt.Columns.Count; i++)
                        {
                            worksheet.Cell(1, i + 1).Value = dt.Columns[i].ColumnName;
                        }
                        // Write data dynamically
                        for (int row = 0; row < dt.Rows.Count; row++)
                        {
                            for (int col = 0; col < dt.Columns.Count; col++)
                            {
                                var cellValue = dt.Rows[row][col];
                                worksheet.Cell(row + 2, col + 1).Value = cellValue == System.DBNull.Value ? "" : cellValue.ToString();
                            }
                        }
                        using (var stream = new System.IO.MemoryStream())
                        {
                            workbook.SaveAs(stream);
                            return stream.ToArray();
                        }
                    }
                }
            }
        }
        public async Task<FetchAndResponse> GetTotalDeductionListAsync(string ecode = null, bool asExcel = false, int? page = null, int? pageSize = null)
        {
            try
            {
                if (asExcel)
                {
                    var query = _context.vw_TotalDeduction_Downloads.AsNoTracking().AsQueryable();
                    if (!string.IsNullOrWhiteSpace(ecode))
                    {
                        query = query.Where(x => x.ECODE == ecode);
                    }
                    var res = await query.ToListAsync();
                    if (res == null || res.Count == 0)
                        return BuildFetchErrorResponse("No Data Found", System.Net.HttpStatusCode.NotFound);

                    // Convert to DataTable
                    var dt = new System.Data.DataTable();
                    var props = typeof(vw_TotalDeduction_Download).GetProperties();
                    foreach (var prop in props)
                        dt.Columns.Add(prop.Name, Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType);
                    foreach (var item in res)
                    {
                        var values = new object[props.Length];
                        for (int i = 0; i < props.Length; i++)
                        {
                            var val = props[i].GetValue(item);
                            values[i] = val ?? (dt.Columns[i].DataType == typeof(string) ? "" : DBNull.Value);
                        }
                        dt.Rows.Add(values);
                    }
                    // Create Excel
                    using (var workbook = new ClosedXML.Excel.XLWorkbook())
                    {
                        var worksheet = workbook.Worksheets.Add("TotalDeduction");
                        for (int i = 0; i < dt.Columns.Count; i++)
                            worksheet.Cell(1, i + 1).Value = dt.Columns[i].ColumnName;
                        for (int row = 0; row < dt.Rows.Count; row++)
                            for (int col = 0; col < dt.Columns.Count; col++)
                                worksheet.Cell(row + 2, col + 1).Value = dt.Rows[row][col] == null || dt.Rows[row][col] == DBNull.Value ? "" : dt.Rows[row][col].ToString();
                        using (var stream = new System.IO.MemoryStream())
                        {
                            workbook.SaveAs(stream);
                            return BuildFetchSuccessResponse("Fetched Successfully (Excel)", stream.ToArray());
                        }
                    }
                }
                else
                {
                    var query = _context.vw_TotalDeductions.AsNoTracking().AsQueryable();
                    if (!string.IsNullOrWhiteSpace(ecode))
                    {
                        query = query.Where(x => x.ECODE == ecode);
                    }
                    var totalCount = await query.CountAsync();
                    if (totalCount == 0)
                        return BuildFetchErrorResponse("No Data Found", System.Net.HttpStatusCode.NotFound);
                    List<vw_TotalDeduction> res;
                    if (page.HasValue && pageSize.HasValue && page > 0 && pageSize > 0)
                    {
                        res = await query.Skip((page.Value - 1) * pageSize.Value).Take(pageSize.Value).ToListAsync();
                        var resultObj = new {
                            Data = res,
                            TotalCount = totalCount,
                            Page = page,
                            PageSize = pageSize
                        };
                        return BuildFetchSuccessResponse("Fetched Successfully", resultObj);
                    }
                    else
                    {
                        res = await query.ToListAsync();
                        return BuildFetchSuccessResponse("Fetched Successfully", res);
                    }
                }
            }
            catch (Exception ex)
            {
                return BuildFetchErrorResponse(ex.Message, System.Net.HttpStatusCode.BadRequest);
            }
        }
        public async Task<FetchAndResponse> GetEsicMaster(string ecode = null, bool asExcel = false, int? page = null, int? pageSize = null)
        {
            try
            {
                if (asExcel)
                {
                    var query = _context.vw_EsicMaster_Downloads.AsNoTracking().AsQueryable();
                    if (!string.IsNullOrWhiteSpace(ecode))
                    {
                        query = query.Where(x => x.ECODE == ecode);
                    }
                    var res = await query.ToListAsync();
                    if (res == null || res.Count == 0)
                        return BuildFetchErrorResponse("No Data Found", System.Net.HttpStatusCode.NotFound);

                    // Convert to DataTable
                    var dt = new System.Data.DataTable();
                    var props = typeof(vw_EsicMaster_Download).GetProperties();
                    foreach (var prop in props)
                        dt.Columns.Add(prop.Name, Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType);
                    foreach (var item in res)
                    {
                        var values = new object[props.Length];
                        for (int i = 0; i < props.Length; i++)
                        {
                            var val = props[i].GetValue(item);
                            values[i] = val ?? (dt.Columns[i].DataType == typeof(string) ? "" : DBNull.Value);
                        }
                        dt.Rows.Add(values);
                    }
                    // Create Excel
                    using (var workbook = new ClosedXML.Excel.XLWorkbook())
                    {
                        var worksheet = workbook.Worksheets.Add("EsicMaster");
                        for (int i = 0; i < dt.Columns.Count; i++)
                            worksheet.Cell(1, i + 1).Value = dt.Columns[i].ColumnName;
                        for (int row = 0; row < dt.Rows.Count; row++)
                            for (int col = 0; col < dt.Columns.Count; col++)
                                worksheet.Cell(row + 2, col + 1).Value = dt.Rows[row][col] == null || dt.Rows[row][col] == DBNull.Value ? "" : dt.Rows[row][col].ToString();
                        using (var stream = new System.IO.MemoryStream())
                        {
                            workbook.SaveAs(stream);
                            return BuildFetchSuccessResponse("Fetched Successfully (Excel)", stream.ToArray());
                        }
                    }
                }
                else
                {
                    var query = _context.vw_EsicMasters.AsNoTracking().AsQueryable();
                    if (!string.IsNullOrWhiteSpace(ecode))
                    {
                        query = query.Where(x => x.ECODE == ecode);
                    }
                    var totalCount = await query.CountAsync();
                    if (totalCount == 0)
                        return BuildFetchErrorResponse("No Data Found", System.Net.HttpStatusCode.NotFound);
                    List<vw_EsicMaster> res;
                    if (page.HasValue && pageSize.HasValue && page > 0 && pageSize > 0)
                    {
                        res = await query.Skip((page.Value - 1) * pageSize.Value).Take(pageSize.Value).ToListAsync();
                        var resultObj = new {
                            Data = res,
                            TotalCount = totalCount,
                            Page = page,
                            PageSize = pageSize
                        };
                        return BuildFetchSuccessResponse("Fetched Successfully", resultObj);
                    }
                    else
                    {
                        res = await query.ToListAsync();
                        return BuildFetchSuccessResponse("Fetched Successfully", res);
                    }
                }
            }
            catch (Exception ex)
            {
                return BuildFetchErrorResponse(ex.Message, System.Net.HttpStatusCode.BadRequest);
            }
        }
        public async Task<FetchAndResponse> GetLeaveMaster(string ecode = null, bool asExcel = false, int? page = null, int? pageSize = null)
        {
            try
            {
                if (asExcel)
                {
                    var query = _context.vw_LeaveMaster_Downloads.AsNoTracking().AsQueryable();
                    if (!string.IsNullOrWhiteSpace(ecode))
                    {
                        query = query.Where(x => x.ECODE == ecode);
                    }
                    var res = await query.ToListAsync();
                    if (res == null || res.Count == 0)
                        return BuildFetchErrorResponse("No Data Found", System.Net.HttpStatusCode.NotFound);

                    // Convert to DataTable
                    var dt = new System.Data.DataTable();
                    var props = typeof(vw_LeaveMaster_Download).GetProperties();
                    foreach (var prop in props)
                        dt.Columns.Add(prop.Name, Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType);
                    foreach (var item in res)
                    {
                        var values = new object[props.Length];
                        for (int i = 0; i < props.Length; i++)
                        {
                            var val = props[i].GetValue(item);
                            values[i] = val ?? (dt.Columns[i].DataType == typeof(string) ? "" : DBNull.Value);
                        }
                        dt.Rows.Add(values);
                    }
                    // Create Excel
                    using (var workbook = new ClosedXML.Excel.XLWorkbook())
                    {
                        var worksheet = workbook.Worksheets.Add("LeaveMaster");
                        for (int i = 0; i < dt.Columns.Count; i++)
                            worksheet.Cell(1, i + 1).Value = dt.Columns[i].ColumnName;
                        for (int row = 0; row < dt.Rows.Count; row++)
                            for (int col = 0; col < dt.Columns.Count; col++)
                                worksheet.Cell(row + 2, col + 1).Value = dt.Rows[row][col] == null || dt.Rows[row][col] == DBNull.Value ? "" : dt.Rows[row][col].ToString();
                        using (var stream = new System.IO.MemoryStream())
                        {
                            workbook.SaveAs(stream);
                            return BuildFetchSuccessResponse("Fetched Successfully (Excel)", stream.ToArray());
                        }
                    }
                }
                else
                {
                    var query = _context.vw_LeaveMasters.AsNoTracking().AsQueryable();
                    if (!string.IsNullOrWhiteSpace(ecode))
                    {
                        query = query.Where(x => x.ECODE == ecode);
                    }
                    var totalCount = await query.CountAsync();
                    if (totalCount == 0)
                        return BuildFetchErrorResponse("No Data Found", System.Net.HttpStatusCode.NotFound);
                    List<vw_LeaveMaster> res;
                    if (page.HasValue && pageSize.HasValue && page > 0 && pageSize > 0)
                    {
                        res = await query.Skip((page.Value - 1) * pageSize.Value).Take(pageSize.Value).ToListAsync();
                        var resultObj = new {
                            Data = res,
                            TotalCount = totalCount,
                            Page = page,
                            PageSize = pageSize
                        };
                        return BuildFetchSuccessResponse("Fetched Successfully", resultObj);
                    }
                    else
                    {
                        res = await query.ToListAsync();
                        return BuildFetchSuccessResponse("Fetched Successfully", res);
                    }
                }
            }
            catch (Exception ex)
            {
                return BuildFetchErrorResponse(ex.Message, System.Net.HttpStatusCode.BadRequest);
            }
        }
        public async Task<FetchAndResponse> GetPfMaster(string ecode = null, bool asExcel = false, int? page = null, int? pageSize = null)
        {
            try
            {
                if (asExcel)
                {
                    var query = _context.vw_PfMaster_Download1s.AsNoTracking().AsQueryable();
                    if (!string.IsNullOrWhiteSpace(ecode))
                    {
                        query = query.Where(x => x.ECODE == ecode);
                    }
                    var res = await query.ToListAsync();
                    if (res == null || res.Count == 0)
                        return BuildFetchErrorResponse("No Data Found", System.Net.HttpStatusCode.NotFound);

                    // Convert to DataTable
                    var dt = new System.Data.DataTable();
                    var props = typeof(vw_PfMaster_Download1).GetProperties();
                    foreach (var prop in props)
                        dt.Columns.Add(prop.Name, Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType);
                    foreach (var item in res)
                    {
                        var values = new object[props.Length];
                        for (int i = 0; i < props.Length; i++)
                        {
                            var val = props[i].GetValue(item);
                            values[i] = val ?? (dt.Columns[i].DataType == typeof(string) ? "" : DBNull.Value);
                        }
                        dt.Rows.Add(values);
                    }
                    // Create Excel
                    using (var workbook = new ClosedXML.Excel.XLWorkbook())
                    {
                        var worksheet = workbook.Worksheets.Add("PfMaster");
                        for (int i = 0; i < dt.Columns.Count; i++)
                            worksheet.Cell(1, i + 1).Value = dt.Columns[i].ColumnName;
                        for (int row = 0; row < dt.Rows.Count; row++)
                            for (int col = 0; col < dt.Columns.Count; col++)
                                worksheet.Cell(row + 2, col + 1).Value = dt.Rows[row][col] == null || dt.Rows[row][col] == DBNull.Value ? "" : dt.Rows[row][col].ToString();
                        using (var stream = new System.IO.MemoryStream())
                        {
                            workbook.SaveAs(stream);
                            return BuildFetchSuccessResponse("Fetched Successfully (Excel)", stream.ToArray());
                        }
                    }
                }
                else
                {
                    var query = _context.vw_PfMaster1s.AsNoTracking().AsQueryable();
                    if (!string.IsNullOrWhiteSpace(ecode))
                    {
                        query = query.Where(x => x.ECODE == ecode);
                    }
                    var totalCount = await query.CountAsync();
                    if (totalCount == 0)
                        return BuildFetchErrorResponse("No Data Found", System.Net.HttpStatusCode.NotFound);
                    List<vw_PfMaster1> res;
                    if (page.HasValue && pageSize.HasValue && page > 0 && pageSize > 0)
                    {
                        res = await query.Skip((page.Value - 1) * pageSize.Value).Take(pageSize.Value).ToListAsync();
                        var resultObj = new {
                            Data = res,
                            TotalCount = totalCount,
                            Page = page,
                            PageSize = pageSize
                        };
                        return BuildFetchSuccessResponse("Fetched Successfully", resultObj);
                    }
                    else
                    {
                        res = await query.ToListAsync();
                        return BuildFetchSuccessResponse("Fetched Successfully", res);
                    }
                }
            }
            catch (Exception ex)
            {
                return BuildFetchErrorResponse(ex.Message, System.Net.HttpStatusCode.BadRequest);
            }
        }

        public async Task<FetchAndResponse> GetBgtSalaryWithEmpDetails(string ecode = null, bool asExcel = false, int? page = null, int? pageSize = null)
        {
            try
            {
                if (asExcel)
                {
                    var query = _context.vw_BgtSalaryStructWithEmpDetails_Downloads.AsNoTracking().AsQueryable();
                    if (!string.IsNullOrWhiteSpace(ecode))
                    {
                        query = query.Where(x => x.ecode == ecode);
                    }
                    var res = await query.ToListAsync();
                    if (res == null || res.Count == 0)
                        return BuildFetchErrorResponse("No Data Found", System.Net.HttpStatusCode.NotFound);

                    // Convert to DataTable
                    var dt = new System.Data.DataTable();
                    var props = typeof(vw_BgtSalaryStructWithEmpDetails_Download).GetProperties();
                    foreach (var prop in props)
                        dt.Columns.Add(prop.Name, Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType);
                    foreach (var item in res)
                    {
                        var values = new object[props.Length];
                        for (int i = 0; i < props.Length; i++)
                        {
                            var val = props[i].GetValue(item);
                            values[i] = val ?? (dt.Columns[i].DataType == typeof(string) ? "" : DBNull.Value);
                        }
                        dt.Rows.Add(values);
                    }
                    // Create Excel
                    using (var workbook = new ClosedXML.Excel.XLWorkbook())
                    {
                        var worksheet = workbook.Worksheets.Add("BgtSalaryStructWithEmpDetails");
                        for (int i = 0; i < dt.Columns.Count; i++)
                            worksheet.Cell(1, i + 1).Value = dt.Columns[i].ColumnName;
                        for (int row = 0; row < dt.Rows.Count; row++)
                            for (int col = 0; col < dt.Columns.Count; col++)
                                worksheet.Cell(row + 2, col + 1).Value = dt.Rows[row][col] == null || dt.Rows[row][col] == DBNull.Value ? "" : dt.Rows[row][col].ToString();
                        using (var stream = new System.IO.MemoryStream())
                        {
                            workbook.SaveAs(stream);
                            return BuildFetchSuccessResponse("Fetched Successfully (Excel)", stream.ToArray());
                        }
                    }
                }
                else
                {
                    var query = _context.vw_BgtSalaryStructWithEmpDetails.AsNoTracking().AsQueryable();
                    if (!string.IsNullOrWhiteSpace(ecode))
                    {
                        query = query.Where(x => x.ecode == ecode);
                    }
                    var totalCount = await query.CountAsync();
                    if (totalCount == 0)
                        return BuildFetchErrorResponse("No Data Found", System.Net.HttpStatusCode.NotFound);
                    List<vw_BgtSalaryStructWithEmpDetail> res;
                    if (page.HasValue && pageSize.HasValue && page > 0 && pageSize > 0)
                    {
                        res = await query.Skip((page.Value - 1) * pageSize.Value).Take(pageSize.Value).ToListAsync();
                        var resultObj = new {
                            Data = res,
                            TotalCount = totalCount,
                            Page = page,
                            PageSize = pageSize
                        };
                        return BuildFetchSuccessResponse("Fetched Successfully", resultObj);
                    }
                    else
                    {
                        res = await query.ToListAsync();
                        return BuildFetchSuccessResponse("Fetched Successfully", res);
                    }
                }
            }
            catch (Exception ex)
            {
                return BuildFetchErrorResponse(ex.Message, System.Net.HttpStatusCode.BadRequest);
            }
        }

        public async Task<FetchAndResponse> GetEmpAttendanceFormat(string ecode = null, bool asExcel = false, int? page = null, int? pageSize = null)
        {
            try
            {
                if (asExcel)
                {
                    var query = _context.vw_Emp_Attendance_Format_Downloads.AsNoTracking().AsQueryable();
                    if (!string.IsNullOrWhiteSpace(ecode))
                    {
                        query = query.Where(x => x.Ecode == ecode);
                    }
                    var res = await query.ToListAsync();
                    if (res == null || res.Count == 0)
                        return BuildFetchErrorResponse("No Data Found", System.Net.HttpStatusCode.NotFound);

                    // Convert to DataTable
                    var dt = new System.Data.DataTable();
                    var props = typeof(vw_Emp_Attendance_Format_Download).GetProperties();
                    foreach (var prop in props)
                        dt.Columns.Add(prop.Name, Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType);
                    foreach (var item in res)
                    {
                        var values = new object[props.Length];
                        for (int i = 0; i < props.Length; i++)
                        {
                            var val = props[i].GetValue(item);
                            values[i] = val ?? (dt.Columns[i].DataType == typeof(string) ? "" : DBNull.Value);
                        }
                        dt.Rows.Add(values);
                    }
                    // Create Excel
                    using (var workbook = new ClosedXML.Excel.XLWorkbook())
                    {
                        var worksheet = workbook.Worksheets.Add("EmpAttendanceFormat");
                        for (int i = 0; i < dt.Columns.Count; i++)
                            worksheet.Cell(1, i + 1).Value = dt.Columns[i].ColumnName;
                        for (int row = 0; row < dt.Rows.Count; row++)
                            for (int col = 0; col < dt.Columns.Count; col++)
                                worksheet.Cell(row + 2, col + 1).Value = dt.Rows[row][col] == null || dt.Rows[row][col] == DBNull.Value ? "" : dt.Rows[row][col].ToString();
                        using (var stream = new System.IO.MemoryStream())
                        {
                            workbook.SaveAs(stream);
                            return BuildFetchSuccessResponse("Fetched Successfully (Excel)", stream.ToArray());
                        }
                    }
                }
                else
                {
                    var query = _context.vw_Emp_Attendance_Formats.AsNoTracking().AsQueryable();
                    if (!string.IsNullOrWhiteSpace(ecode))
                    {
                        query = query.Where(x => x.Ecode == ecode);
                    }
                    var totalCount = await query.CountAsync();
                    if (totalCount == 0)
                        return BuildFetchErrorResponse("No Data Found", System.Net.HttpStatusCode.NotFound);
                    List<vw_Emp_Attendance_Format> res;
                    if (page.HasValue && pageSize.HasValue && page > 0 && pageSize > 0)
                    {
                        res = await query.Skip((page.Value - 1) * pageSize.Value).Take(pageSize.Value).ToListAsync();
                        var resultObj = new {
                            Data = res,
                            TotalCount = totalCount,
                            Page = page,
                            PageSize = pageSize
                        };
                        return BuildFetchSuccessResponse("Fetched Successfully", resultObj);
                    }
                    else
                    {
                        res = await query.ToListAsync();
                        return BuildFetchSuccessResponse("Fetched Successfully", res);
                    }
                }
            }
            catch (Exception ex)
            {
                return BuildFetchErrorResponse(ex.Message, System.Net.HttpStatusCode.BadRequest);
            }
        }
        public async Task<FetchAndResponse> GetSalaryFormatAsync(string ecode = null, bool asExcel = false, int page = 1, int pageSize = 20)
        {
            try
            {
                var query = _context.vw_Salary_Formats.AsNoTracking().AsQueryable();
                if (!string.IsNullOrWhiteSpace(ecode))
                {
                    query = query.Where(x => x.E_CODE == ecode);
                }
                var totalCount = await query.CountAsync();
                if (totalCount == 0)
                    return BuildFetchErrorResponse("No Data Found", System.Net.HttpStatusCode.NotFound);

                if (asExcel)
                {
                    var res = await query.ToListAsync();
                    // Convert to DataTable
                    var dt = new System.Data.DataTable();
                    var props = typeof(vw_Salary_Format).GetProperties();
                    foreach (var prop in props)
                        dt.Columns.Add(prop.Name, Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType);
                    foreach (var item in res)
                    {
                        var values = new object[props.Length];
                        for (int i = 0; i < props.Length; i++)
                        {
                            var val = props[i].GetValue(item);
                            values[i] = val ?? (dt.Columns[i].DataType == typeof(string) ? "" : DBNull.Value);
                        }
                        dt.Rows.Add(values);
                    }
                    // Create Excel
                    using (var workbook = new ClosedXML.Excel.XLWorkbook())
                    {
                        var worksheet = workbook.Worksheets.Add("SalaryFormat");
                        for (int i = 0; i < dt.Columns.Count; i++)
                            worksheet.Cell(1, i + 1).Value = dt.Columns[i].ColumnName;
                        for (int row = 0; row < dt.Rows.Count; row++)
                            for (int col = 0; col < dt.Columns.Count; col++)
                                worksheet.Cell(row + 2, col + 1).Value = dt.Rows[row][col] == null || dt.Rows[row][col] == DBNull.Value ? "" : dt.Rows[row][col].ToString();
                        using (var stream = new System.IO.MemoryStream())
                        {
                            workbook.SaveAs(stream);
                            return BuildFetchSuccessResponse("Fetched Successfully (Excel)", stream.ToArray());
                        }
                    }
                }
                else
                {
                    var res = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
                    var resultObj = new {
                        Data = res,
                        TotalCount = totalCount,
                        Page = page,
                        PageSize = pageSize
                    };
                    return BuildFetchSuccessResponse("Fetched Successfully", resultObj);
                }
            }
            catch (Exception ex)
            {
                return BuildFetchErrorResponse(ex.Message, System.Net.HttpStatusCode.BadRequest);
            }
        }
        public async Task<ExecuteAndReponse> UploadEmployeeDeductionsExcelAsync(IFormFile file)
        {
            var expectedHeaders = new[]
            {
        "ECode", "MONTH", "Year", "TDS", "PTax", "Loan", "CashShort", "DieselDeduction", "Penality", "Lwf"
    };

            if (file == null || file.Length == 0)
                return BuildExecuteErrorResponse("No file uploaded", HttpStatusCode.BadRequest);

            using var stream = file.OpenReadStream();
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheet(1);

            // Validate headers
            for (int i = 0; i < expectedHeaders.Length; i++)
            {
                var cellValue = worksheet.Cell(1, i + 1).GetValue<string>().Trim();
                if (!string.Equals(cellValue, expectedHeaders[i], StringComparison.OrdinalIgnoreCase))
                    return BuildExecuteErrorResponse($"Header mismatch at column {i + 1}: Expected '{expectedHeaders[i]}', found '{cellValue}'", HttpStatusCode.BadRequest);
            }

            var seenKeys = new HashSet<string>();
            var rows = worksheet.RowsUsed().Skip(1);
            var conn = _context.Database.GetDbConnection();
            await conn.OpenAsync();

            using var transaction = conn.BeginTransaction();
            try
            {
                foreach (var row in rows)
                {
                    var ecode = row.Cell(1).GetValue<string>()?.Trim();
                    var month = row.Cell(2).GetValue<string>()?.Trim();
                    var yearStr = row.Cell(3).GetValue<string>()?.Trim();

                    // Validate MONTH format
                    if (!System.Text.RegularExpressions.Regex.IsMatch(month ?? "", @"^[A-Za-z]{3}-\d{2}$", RegexOptions.IgnoreCase))
                        return BuildExecuteErrorResponse($"Invalid MONTH format for ECODE {ecode}: '{month}'. Expected format is MMM-YY (e.g., Jun-25).", HttpStatusCode.BadRequest);

                    // Validate YEAR format
                    if (!int.TryParse(yearStr, out int year) || yearStr.Length != 4)
                        return BuildExecuteErrorResponse($"Invalid YEAR format for ECODE {ecode}: '{yearStr}'. Expected format is YYYY (e.g., 2025).", HttpStatusCode.BadRequest);

                    // Check for duplicate compound key in Excel
                    var key = $"{ecode}|{month}|{year}";
                    if (!seenKeys.Add(key))
                        return BuildExecuteErrorResponse($"Duplicate ECODE+MONTH+YEAR found in Excel: {key}", HttpStatusCode.BadRequest);

                    // Read deduction values (handle empty as null)
                    decimal? tds = decimal.TryParse(row.Cell(4).GetValue<string>(), out var tdsVal) ? tdsVal : (decimal?)null;
                    decimal? ptax = decimal.TryParse(row.Cell(5).GetValue<string>(), out var ptaxVal) ? ptaxVal : (decimal?)null;
                    decimal? loan = decimal.TryParse(row.Cell(6).GetValue<string>(), out var loanVal) ? loanVal : (decimal?)null;
                    decimal? cashShort = decimal.TryParse(row.Cell(7).GetValue<string>(), out var cashShortVal) ? cashShortVal : (decimal?)null;
                    decimal? dieselDeduction = decimal.TryParse(row.Cell(8).GetValue<string>(), out var dieselDeductionVal) ? dieselDeductionVal : (decimal?)null;
                    decimal? penality = decimal.TryParse(row.Cell(9).GetValue<string>(), out var penalityVal) ? penalityVal : (decimal?)null;
                    decimal? lwf = decimal.TryParse(row.Cell(10).GetValue<string>(), out var lwfVal) ? lwfVal : (decimal?)null;

                    // Call stored procedure for upsert
                    using (var cmd = conn.CreateCommand()){
                        cmd.Transaction = transaction;
                        cmd.CommandText = "sp_MergeEmployeeDeduction";
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.Add(new SqlParameter("@ECODE", ecode));
                        cmd.Parameters.Add(new SqlParameter("@MONTH", month));
                        cmd.Parameters.Add(new SqlParameter("@YEAR", year));
                        cmd.Parameters.Add(new SqlParameter("@TDS", (object?)tds ?? DBNull.Value));
                        cmd.Parameters.Add(new SqlParameter("@PTax", (object?)ptax ?? DBNull.Value));
                        cmd.Parameters.Add(new SqlParameter("@Loan", (object?)loan ?? DBNull.Value));
                        cmd.Parameters.Add(new SqlParameter("@CashShort", (object?)cashShort ?? DBNull.Value));
                        cmd.Parameters.Add(new SqlParameter("@DieselDeduction", (object?)dieselDeduction ?? DBNull.Value));
                        cmd.Parameters.Add(new SqlParameter("@Penality", (object?)penality ?? DBNull.Value));
                        cmd.Parameters.Add(new SqlParameter("@Lwf", (object?)lwf ?? DBNull.Value));
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
                await transaction.CommitAsync();
                return BuildExecuteSuccessResponse("Employee deductions updated successfully");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return BuildExecuteErrorResponse($"Error updating employee deductions: {ex.Message}", HttpStatusCode.BadRequest);
            }
        }

        public async Task<FetchAndResponse> GetNetPaybleListAsync(string ecode = null, bool asExcel = false, int? page = null, int? pageSize = null)
        {
            try
            {
                if (asExcel)
                {
                    var query = _context.Net_Payble_Downloads.AsNoTracking().AsQueryable();
                    if (!string.IsNullOrWhiteSpace(ecode))
                    {
                        query = query.Where(x => x.Ecode == ecode);
                    }
                    var res = await query.ToListAsync();
                    if (res == null || res.Count == 0)
                        return BuildFetchErrorResponse("No Data Found", System.Net.HttpStatusCode.NotFound);

                    // Convert to DataTable
                    var dt = new System.Data.DataTable();
                    var props = typeof(Net_Payble_Download).GetProperties();
                    foreach (var prop in props)
                        dt.Columns.Add(prop.Name, Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType);
                    foreach (var item in res)
                    {
                        var values = new object[props.Length];
                        for (int i = 0; i < props.Length; i++)
                        {
                            var val = props[i].GetValue(item);
                            values[i] = val ?? (dt.Columns[i].DataType == typeof(string) ? "" : DBNull.Value);
                        }
                        dt.Rows.Add(values);
                    }
                    // Create Excel
                    using (var workbook = new ClosedXML.Excel.XLWorkbook())
                    {
                        var worksheet = workbook.Worksheets.Add("NetPayble");
                        for (int i = 0; i < dt.Columns.Count; i++)
                            worksheet.Cell(1, i + 1).Value = dt.Columns[i].ColumnName;
                        for (int row = 0; row < dt.Rows.Count; row++)
                            for (int col = 0; col < dt.Columns.Count; col++)
                                worksheet.Cell(row + 2, col + 1).Value = dt.Rows[row][col] == null || dt.Rows[row][col] == DBNull.Value ? "" : dt.Rows[row][col].ToString();
                        using (var stream = new System.IO.MemoryStream())
                        {
                            workbook.SaveAs(stream);
                            return BuildFetchSuccessResponse("Fetched Successfully (Excel)", stream.ToArray());
                        }
                    }
                }
                else
                {
                    var query = _context.Net_Paybles.AsNoTracking().AsQueryable();
                    if (!string.IsNullOrWhiteSpace(ecode))
                    {
                        query = query.Where(x => x.Ecode == ecode);
                    }
                    var totalCount = await query.CountAsync();
                    if (totalCount == 0)
                        return BuildFetchErrorResponse("No Data Found", System.Net.HttpStatusCode.NotFound);
                    List<Net_Payble> res;
                    if (page.HasValue && pageSize.HasValue && page > 0 && pageSize > 0)
                    {
                        res = await query.Skip((page.Value - 1) * pageSize.Value).Take(pageSize.Value).ToListAsync();
                        var resultObj = new {
                            Data = res,
                            TotalCount = totalCount,
                            Page = page,
                            PageSize = pageSize
                        };
                        return BuildFetchSuccessResponse("Fetched Successfully", resultObj);
                    }
                    else
                    {
                        res = await query.ToListAsync();
                        return BuildFetchSuccessResponse("Fetched Successfully", res);
                    }
                }
            }
            catch (Exception ex)
            {
                return BuildFetchErrorResponse(ex.Message, System.Net.HttpStatusCode.BadRequest);
            }
        }

        public async Task<FetchAndResponse> GetPaybleDaysAsync(string ecode = null, bool asExcel = false, int? page = null, int? pageSize = null)
        {
            try
            {
                if (asExcel)
                {
                    var query = _context.PaybleDays_Downloads.AsNoTracking().AsQueryable();
                    if (!string.IsNullOrWhiteSpace(ecode))
                    {
                        query = query.Where(x => x.Ecode == ecode);
                    }
                    var res = await query.ToListAsync();
                    if (res == null || res.Count == 0)
                        return BuildFetchErrorResponse("No Data Found", System.Net.HttpStatusCode.NotFound);

                    // Convert to DataTable
                    var dt = new System.Data.DataTable();
                    var props = typeof(PaybleDays_Download).GetProperties();
                    foreach (var prop in props)
                        dt.Columns.Add(prop.Name, Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType);
                    foreach (var item in res)
                    {
                        var values = new object[props.Length];
                        for (int i = 0; i < props.Length; i++)
                        {
                            var val = props[i].GetValue(item);
                            values[i] = val ?? (dt.Columns[i].DataType == typeof(string) ? "" : DBNull.Value);
                        }
                        dt.Rows.Add(values);
                    }
                    // Create Excel
                    using (var workbook = new ClosedXML.Excel.XLWorkbook())
                    {
                        var worksheet = workbook.Worksheets.Add("PaybleDays");
                        for (int i = 0; i < dt.Columns.Count; i++)
                            worksheet.Cell(1, i + 1).Value = dt.Columns[i].ColumnName;
                        for (int row = 0; row < dt.Rows.Count; row++)
                            for (int col = 0; col < dt.Columns.Count; col++)
                                worksheet.Cell(row + 2, col + 1).Value = dt.Rows[row][col] == null || dt.Rows[row][col] == DBNull.Value ? "" : dt.Rows[row][col].ToString();
                        using (var stream = new System.IO.MemoryStream())
                        {
                            workbook.SaveAs(stream);
                            return BuildFetchSuccessResponse("Fetched Successfully (Excel)", stream.ToArray());
                        }
                    }
                }
                else
                {
                    var query = _context.PaybleDays.AsNoTracking().AsQueryable();
                    if (!string.IsNullOrWhiteSpace(ecode))
                    {
                        query = query.Where(x => x.Ecode == ecode);
                    }
                    var totalCount = await query.CountAsync();
                    if (totalCount == 0)
                        return BuildFetchErrorResponse("No Data Found", System.Net.HttpStatusCode.NotFound);
                    List<PaybleDay> res;
                    if (page.HasValue && pageSize.HasValue && page > 0 && pageSize > 0)
                    {
                        res = await query.Skip((page.Value - 1) * pageSize.Value).Take(pageSize.Value).ToListAsync();
                        var resultObj = new {
                            Data = res,
                            TotalCount = totalCount,
                            Page = page,
                            PageSize = pageSize
                        };
                        return BuildFetchSuccessResponse("Fetched Successfully", resultObj);
                    }
                    else
                    {
                        res = await query.ToListAsync();
                        return BuildFetchSuccessResponse("Fetched Successfully", res);
                    }
                }
            }
            catch (Exception ex)
            {
                return BuildFetchErrorResponse(ex.Message, System.Net.HttpStatusCode.BadRequest);
            }
        }

        //public async Task<FetchAndResponse> GetBgtSalaryMaster(string ecode=null) {
        //    try { }
        //    catch (Exception ex) { }
        //}
    }
} 
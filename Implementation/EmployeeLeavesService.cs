//using HRMSAPI.Data;
//using HRMSAPI.Interfaces;
//using ClosedXML.Excel;
//using Microsoft.AspNetCore.Http;
//using Microsoft.EntityFrameworkCore;
//using Roomsy.DTOS.GenericsResponses;
//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Net;
//using System.Threading.Tasks;

//namespace HRMSAPI.Implementation
//{
//    public class EmployeeLeavesService : BaseService, IEmployeeLeavesService
//    {
//        private readonly HRMSContext _context;
//        public EmployeeLeavesService(HRMSContext context) : base(context)
//        {
//            _context = context;
//        }

//        public async Task<ExecuteAndReponse> UploadEmployeeLeavesCompOffExcel(IFormFile file, string user)
//        {
//            if (file == null || file.Length == 0)
//                return BuildExecuteErrorResponse("File is mandatory to serve.", HttpStatusCode.BadRequest);

//            int inserted = 0, updated = 0, skipped = 0;
//            var excelRows = new List<(string ECode, string STCode, int Month, string Year, decimal? CompOff_OPN_Leave, decimal? CompOff_CLS_Leave, decimal? CompOff_EARN_Leave, decimal? CompOff_AVAIL_Leave)>();
//            using (var stream = file.OpenReadStream())
//            using (var workbook = new XLWorkbook(stream))
//            {
//                var worksheet = workbook.Worksheet(1);
//                var headerRow = worksheet.Row(1);
//                var colMap = headerRow.Cells().ToDictionary(
//                    c => c.GetString().Trim().ToUpper(),
//                    c => c.Address.ColumnNumber
//                );

//                foreach (var row in worksheet.RowsUsed().Skip(1))
//                {
//                    string ecode = row.Cell(colMap.GetValueOrDefault("ECODE", 0)).GetString().Trim();
//                    string stcode = row.Cell(colMap.GetValueOrDefault("STCODE", 0)).GetString().Trim();
//                    string monthStr = row.Cell(colMap.GetValueOrDefault("MONTH", 0)).GetString().Trim();
//                    string year = row.Cell(colMap.GetValueOrDefault("YEAR", 0)).GetString().Trim();
//                    decimal? compOff_OPN_Leave = TryParseDecimal(row.Cell(colMap.GetValueOrDefault("COMPOFF_OPN_LEAVE", 0)).GetString());
//                    decimal? compOff_CLS_Leave = TryParseDecimal(row.Cell(colMap.GetValueOrDefault("COMPOFF_CLS_LEAVE", 0)).GetString());
//                    decimal? compOff_EARN_Leave = TryParseDecimal(row.Cell(colMap.GetValueOrDefault("COMPOFF_EARN_LEAVE", 0)).GetString());
//                    decimal? compOff_AVAIL_Leave = TryParseDecimal(row.Cell(colMap.GetValueOrDefault("COMPOFF_AVAIL_LEAVE", 0)).GetString());

//                    if (string.IsNullOrWhiteSpace(ecode) || string.IsNullOrWhiteSpace(stcode) || string.IsNullOrWhiteSpace(monthStr) || string.IsNullOrWhiteSpace(year) || !int.TryParse(monthStr, out int month))
//                    {
//                        skipped++;
//                        continue;
//                    }

//                    excelRows.Add((ecode, stcode, month, year, compOff_OPN_Leave, compOff_CLS_Leave, compOff_EARN_Leave, compOff_AVAIL_Leave));
//                }
//            }

//            //var keys = excelRows.Select(x => new { x.ECode, x.STCode, x.Month, x.Year }).ToList();
//            //var existingRecords = await _context.tblEmployeeLeaves.AsQueryable()
//            //    .Where(x => keys.Any(k => k.ECode == x.ECode && k.STCode == x.STCode && k.Month == x.MONTH && k.Year == x.Year) && (x.IsDeleted == null || x.IsDeleted == false) && (x.IsActive == null || x.IsActive == true))
//            //    .ToListAsync();
//            var keySet = new HashSet<string>(excelRows.Select(x => $"{x.ECode}|{x.STCode}|{x.Month}"));
//            var existingRecords = await _context.tblEmployeeLeaves.AsQueryable()
//                .Where(x => keySet.Contains(x.ECode + "|" + x.STCode + "|" + x.MONTH)
//                    && (x.IsDeleted == null || x.IsDeleted == false)
//                    && (x.IsActive == null || x.IsActive == true))
//                .ToListAsync();
//            var existingDict = existingRecords.ToDictionary(
//                x => $"{x.ECode}|{x.STCode}|{x.MONTH}|{x.Year}",
//                x => x
//            );

//            foreach (var row in excelRows)
//            {
//                string key = $"{row.ECode}|{row.STCode}|{row.Month}|{row.Year}";
//                if (existingDict.TryGetValue(key, out var existing))
//                {
//                    existing.CompOff_OPN_Leave = row.CompOff_OPN_Leave;
//                    existing.CompOff_CLS_Leave = row.CompOff_CLS_Leave;
//                    existing.CompOff_EARN_Leave = row.CompOff_EARN_Leave;
//                    existing.CompOff_AVAIL_Leave = row.CompOff_AVAIL_Leave;
//                    existing.UpdatedBy = user;
//                    existing.UpdatedOn = DateTime.Now;
//                    updated++;
//                }
//                else
//                {
//                    var newRec = new tblEmployeeLeaf
//                    {
//                        ECode = row.ECode,
//                        STCode = row.STCode,
//                        MONTH = row.Month,
//                        Year = row.Year,
//                        CompOff_OPN_Leave = row.CompOff_OPN_Leave,
//                        CompOff_CLS_Leave = row.CompOff_CLS_Leave,
//                        CompOff_EARN_Leave = row.CompOff_EARN_Leave,
//                        CompOff_AVAIL_Leave = row.CompOff_AVAIL_Leave,
//                        CreatedBy = user,
//                        CreatedOn = DateTime.Now,
//                        IsActive = true,
//                        IsDeleted = false
//                    };
//                    _context.tblEmployeeLeaves.Add(newRec);
//                    inserted++;
//                }
//            }

//            int ra = await _context.SaveChangesAsync();
//            if (ra > 0)
//            {
//                return BuildExecuteSuccessResponse($"Inserted: {inserted}, Updated: {updated}, Skipped: {skipped}");
//            }
//            return BuildExecuteErrorResponse("Unable to Update , something went Wrong", HttpStatusCode.InternalServerError);
//        }

//        private decimal? TryParseDecimal(string value)
//        {
//            if (decimal.TryParse(value, out decimal result))
//                return result;
//            return null;
//        }
//    }
//} 
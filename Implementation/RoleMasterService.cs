using ClosedXML.Excel;
using HRMSAPI.Data;
using HRMSAPI.DTO;
using HRMSAPI.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Roomsy.DTOS.GenericsResponses;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace HRMSAPI.Implementation
{
    /// <summary>
    /// Masters -> Role Master: the HR-maintained role list (dbo.tblRoleMaster).
    ///
    /// This is NOT the V2 Parivar portal role. Portal/RBAC roles live in
    /// dbo.tblRole and drive page access and the approval layers; they are not
    /// editable from here, and nothing in this service touches them.
    /// </summary>
    public class RoleMasterService : BaseService, IRoleMasterService
    {
        private readonly HRMSContext _context;

        public RoleMasterService(HRMSContext context) : base(context)
        {
            _context = context;
        }

        /// <summary>
        /// Every HR role, active and inactive, with how many employees and
        /// candidates currently carry it - so the page can say what deactivating
        /// one affects.
        /// </summary>
        public async Task<FetchAndResponse> GetAllRolesAsync()
        {
            try
            {
                var roles = await _context.tblRoleMasters
                    .AsNoTracking()
                    .Select(r => new RoleMasterDto
                    {
                        RoleId = r.RoleMasterId,
                        RoleName = r.RoleName,
                        Description = r.Description,
                        IsActive = r.IsActive,
                        CreatedOn = r.CreatedOn,
                        CreatedBy = r.CreatedBy,
                        LastUpdatedBy = r.LastUpdatedBy,
                        EmployeeCount = _context.tblEmployees.Count(e => e.RoleMasterId == r.RoleMasterId),
                        CandidateCount = _context.Candidates.Count(c => c.RoleMasterId == r.RoleMasterId)
                    })
                    .OrderBy(r => r.RoleName)
                    .ToListAsync();

                return BuildFetchSuccessResponse("Roles fetched successfully", roles);
            }
            catch (Exception ex)
            {
                return BuildFetchErrorResponse($"Error fetching roles: {ex.Message}", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<FetchAndResponse> GetRoleByIdAsync(int roleId)
        {
            try
            {
                var role = await _context.tblRoleMasters
                    .AsNoTracking()
                    .Where(r => r.RoleMasterId == roleId)
                    .Select(r => new RoleMasterDto
                    {
                        RoleId = r.RoleMasterId,
                        RoleName = r.RoleName,
                        Description = r.Description,
                        IsActive = r.IsActive,
                        CreatedOn = r.CreatedOn,
                        CreatedBy = r.CreatedBy,
                        LastUpdatedBy = r.LastUpdatedBy,
                        EmployeeCount = _context.tblEmployees.Count(e => e.RoleMasterId == r.RoleMasterId),
                        CandidateCount = _context.Candidates.Count(c => c.RoleMasterId == r.RoleMasterId)
                    })
                    .FirstOrDefaultAsync();

                if (role == null)
                {
                    return BuildFetchErrorResponse($"Role with ID {roleId} not found", HttpStatusCode.NotFound);
                }

                return BuildFetchSuccessResponse("Role fetched successfully", role);
            }
            catch (Exception ex)
            {
                return BuildFetchErrorResponse($"Error fetching role: {ex.Message}", HttpStatusCode.InternalServerError);
            }
        }

        /// <summary>Active roles only - the feed behind the Role dropdowns.</summary>
        public async Task<FetchAndResponse> GetActiveRolesAsync()
        {
            try
            {
                var roles = await _context.tblRoleMasters
                    .AsNoTracking()
                    .Where(r => r.IsActive)
                    .OrderBy(r => r.RoleName)
                    .Select(r => new RoleMasterDto
                    {
                        RoleId = r.RoleMasterId,
                        RoleName = r.RoleName,
                        Description = r.Description,
                        IsActive = r.IsActive
                    })
                    .ToListAsync();

                return BuildFetchSuccessResponse("Roles fetched successfully", roles);
            }
            catch (Exception ex)
            {
                return BuildFetchErrorResponse($"Error fetching roles: {ex.Message}", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ExecuteAndReponse> CreateRoleAsync(RoleMasterUpsertDto roleDto, string createdBy)
        {
            try
            {
                if (roleDto == null)
                {
                    return BuildExecuteErrorResponse("Role data is required", HttpStatusCode.BadRequest);
                }

                if (string.IsNullOrWhiteSpace(roleDto.RoleName))
                {
                    return BuildExecuteErrorResponse("Role Name is required", HttpStatusCode.BadRequest);
                }

                var roleName = roleDto.RoleName.Trim();

                // Case-insensitive duplicate check: two roles differing only in case
                // would be indistinguishable to whoever picks one from a dropdown.
                var duplicate = await _context.tblRoleMasters
                    .FirstOrDefaultAsync(r => r.RoleName.Trim().ToLower() == roleName.ToLower());

                if (duplicate != null)
                {
                    return BuildExecuteErrorResponse($"Role '{duplicate.RoleName}' already exists", HttpStatusCode.BadRequest);
                }

                var role = new tblRoleMaster
                {
                    RoleName = roleName,
                    Description = string.IsNullOrWhiteSpace(roleDto.Description) ? null : roleDto.Description.Trim(),
                    IsActive = roleDto.IsActive,
                    CreatedBy = createdBy,
                    CreatedOn = DateTime.Now
                };

                await _context.tblRoleMasters.AddAsync(role);
                await _context.SaveChangesAsync();

                return BuildExecuteSuccessResponse($"Role '{roleName}' created successfully");
            }
            catch (Exception ex)
            {
                return BuildExecuteErrorResponse($"Error creating role: {ex.Message}", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ExecuteAndReponse> UpdateRoleAsync(int roleId, RoleMasterUpsertDto roleDto, string updatedBy)
        {
            try
            {
                if (roleDto == null)
                {
                    return BuildExecuteErrorResponse("Role data is required", HttpStatusCode.BadRequest);
                }

                if (string.IsNullOrWhiteSpace(roleDto.RoleName))
                {
                    return BuildExecuteErrorResponse("Role Name is required", HttpStatusCode.BadRequest);
                }

                var role = await _context.tblRoleMasters.FindAsync(roleId);
                if (role == null)
                {
                    return BuildExecuteErrorResponse($"Role with ID {roleId} not found", HttpStatusCode.NotFound);
                }

                var roleName = roleDto.RoleName.Trim();

                var duplicate = await _context.tblRoleMasters
                    .FirstOrDefaultAsync(r => r.RoleMasterId != roleId && r.RoleName.Trim().ToLower() == roleName.ToLower());

                if (duplicate != null)
                {
                    return BuildExecuteErrorResponse($"Role '{duplicate.RoleName}' already exists", HttpStatusCode.BadRequest);
                }

                role.RoleName = roleName;
                role.Description = string.IsNullOrWhiteSpace(roleDto.Description) ? null : roleDto.Description.Trim();
                role.IsActive = roleDto.IsActive;
                role.LastUpdatedBy = updatedBy;
                role.LastUpdatedOn = DateTime.Now;

                await _context.SaveChangesAsync();

                // A rename follows everyone who holds the role, since employees and
                // candidates store the id, not the text.
                var holders = await _context.tblEmployees.CountAsync(e => e.RoleMasterId == roleId);
                var note = holders > 0 ? $" {holders} employee(s) carry this role and now show the new name." : string.Empty;

                return BuildExecuteSuccessResponse($"Role '{roleName}' updated successfully.{note}");
            }
            catch (Exception ex)
            {
                return BuildExecuteErrorResponse($"Error updating role: {ex.Message}", HttpStatusCode.InternalServerError);
            }
        }

        // ===== Bulk upload =================================================

        private const string ColRoleName = "Role Name";
        private const string ColDescription = "Description";
        private const string ColActive = "Active";

        /// <summary>
        /// Template for the Role Master uploader: the three headers plus example
        /// rows, so the expected format is self-evident.
        /// </summary>
        public byte[] BuildUploadTemplate()
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Roles");

            var headers = new[] { ColRoleName, ColDescription, ColActive };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(1, i + 1).Value = headers[i];
                ws.Cell(1, i + 1).Style.Font.Bold = true;
                ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
            }

            ws.Cell(2, 1).Value = "Store Operations";
            ws.Cell(2, 2).Value = "Runs store floor operations";
            ws.Cell(2, 3).Value = "Yes";

            ws.Cell(3, 1).Value = "Warehouse Supervisor";
            ws.Cell(3, 2).Value = "";
            ws.Cell(3, 3).Value = "Yes";

            ws.Cell(5, 1).Value =
                "Role Name is required and must be unique. Description and Active are optional " +
                "(Active accepts Yes/No, leave blank for Yes). A role name that already exists is " +
                "UPDATED, not duplicated. Nothing is ever deleted by an upload.";
            ws.Cell(5, 1).Style.Font.Italic = true;

            ws.ColumnsUsed().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        /// <summary>
        /// Creates roles that are new and updates the ones that already exist,
        /// matched on name (case-insensitive). Never deletes: a role that is absent
        /// from the sheet is simply left alone. Every row is reported back so a
        /// partial upload is never a mystery.
        /// </summary>
        public async Task<FetchAndResponse> BulkUploadRolesAsync(IFormFile file, string uploadedBy)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return BuildFetchErrorResponse("No file uploaded", HttpStatusCode.BadRequest);
                }

                using var stream = file.OpenReadStream();
                using var workbook = new XLWorkbook(stream);
                var ws = workbook.Worksheet(1);

                // Header lookup by NAME, not position, so column order does not matter
                // and extra columns are ignored rather than breaking the upload.
                var headerRow = ws.Row(1);
                var headerIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var cell in headerRow.CellsUsed())
                {
                    var name = cell.GetValue<string>()?.Trim();
                    if (!string.IsNullOrWhiteSpace(name) && !headerIndex.ContainsKey(name))
                        headerIndex[name] = cell.Address.ColumnNumber;
                }

                if (!headerIndex.ContainsKey(ColRoleName))
                {
                    return BuildFetchErrorResponse(
                        $"A '{ColRoleName}' column is required. Download the template for the expected format.",
                        HttpStatusCode.BadRequest);
                }

                int nameCol = headerIndex[ColRoleName];
                int descCol = headerIndex.TryGetValue(ColDescription, out var dc) ? dc : 0;
                int activeCol = headerIndex.TryGetValue(ColActive, out var ac) ? ac : 0;

                var existing = await _context.tblRoleMasters.ToListAsync();
                var byName = existing.ToDictionary(r => r.RoleName.Trim(), r => r, StringComparer.OrdinalIgnoreCase);

                var results = new List<RoleUploadRowResult>();
                var seenInSheet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                int created = 0, updated = 0, skipped = 0;
                var now = DateTime.Now;

                int rowNo = 1;
                foreach (var row in ws.RowsUsed().Skip(1))
                {
                    rowNo++;

                    var roleName = row.Cell(nameCol).GetValue<string>()?.Trim();
                    if (string.IsNullOrWhiteSpace(roleName))
                    {
                        // A blank name is an empty row, not an error worth shouting about.
                        continue;
                    }

                    if (roleName.Length > 100)
                    {
                        results.Add(new RoleUploadRowResult { Row = rowNo, RoleName = roleName, Outcome = "Error", Message = "Role Name cannot exceed 100 characters" });
                        skipped++;
                        continue;
                    }

                    if (!seenInSheet.Add(roleName))
                    {
                        results.Add(new RoleUploadRowResult { Row = rowNo, RoleName = roleName, Outcome = "Skipped", Message = "Repeated in the sheet; the first occurrence was used" });
                        skipped++;
                        continue;
                    }

                    var description = descCol > 0 ? row.Cell(descCol).GetValue<string>()?.Trim() : null;
                    if (!string.IsNullOrEmpty(description) && description.Length > 500)
                    {
                        results.Add(new RoleUploadRowResult { Row = rowNo, RoleName = roleName, Outcome = "Error", Message = "Description cannot exceed 500 characters" });
                        skipped++;
                        continue;
                    }

                    // Active is optional; blank means Yes. Anything unrecognisable is an
                    // error rather than a silent guess.
                    bool isActive = true;
                    if (activeCol > 0)
                    {
                        var activeRaw = row.Cell(activeCol).GetValue<string>()?.Trim();
                        if (!string.IsNullOrWhiteSpace(activeRaw))
                        {
                            switch (activeRaw.ToLowerInvariant())
                            {
                                case "yes": case "y": case "true": case "1": case "active":
                                    isActive = true; break;
                                case "no": case "n": case "false": case "0": case "inactive":
                                    isActive = false; break;
                                default:
                                    results.Add(new RoleUploadRowResult { Row = rowNo, RoleName = roleName, Outcome = "Error", Message = $"'{activeRaw}' is not a valid Active value (use Yes or No)" });
                                    skipped++;
                                    continue;
                            }
                        }
                    }

                    if (byName.TryGetValue(roleName, out var role))
                    {
                        role.RoleName = roleName; // normalises casing to what was uploaded
                        role.Description = string.IsNullOrWhiteSpace(description) ? role.Description : description;
                        role.IsActive = isActive;
                        role.LastUpdatedBy = uploadedBy;
                        role.LastUpdatedOn = now;

                        results.Add(new RoleUploadRowResult { Row = rowNo, RoleName = roleName, Outcome = "Updated", Message = "Existing role updated" });
                        updated++;
                    }
                    else
                    {
                        var newRole = new tblRoleMaster
                        {
                            RoleName = roleName,
                            Description = string.IsNullOrWhiteSpace(description) ? null : description,
                            IsActive = isActive,
                            CreatedBy = uploadedBy,
                            CreatedOn = now
                        };
                        await _context.tblRoleMasters.AddAsync(newRole);
                        byName[roleName] = newRole;

                        results.Add(new RoleUploadRowResult { Row = rowNo, RoleName = roleName, Outcome = "Created", Message = "New role created" });
                        created++;
                    }
                }

                if (created == 0 && updated == 0)
                {
                    return BuildFetchErrorResponse(
                        results.Count == 0
                            ? "The sheet has no role rows."
                            : "Nothing was applied - every row was skipped. See the row details.",
                        HttpStatusCode.BadRequest);
                }

                await _context.SaveChangesAsync();

                return BuildFetchSuccessResponse(
                    $"Upload complete. Created: {created}, Updated: {updated}, Skipped: {skipped}.",
                    new RoleUploadResultDto
                    {
                        Created = created,
                        Updated = updated,
                        Skipped = skipped,
                        Rows = results
                    });
            }
            catch (Exception ex)
            {
                return BuildFetchErrorResponse($"Error uploading roles: {ex.Message}", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ExecuteAndReponse> ToggleRoleStatusAsync(int roleId, string updatedBy)
        {
            try
            {
                var role = await _context.tblRoleMasters.FindAsync(roleId);
                if (role == null)
                {
                    return BuildExecuteErrorResponse($"Role with ID {roleId} not found", HttpStatusCode.NotFound);
                }

                var wasActive = role.IsActive;
                role.IsActive = !wasActive;
                role.LastUpdatedBy = updatedBy;
                role.LastUpdatedOn = DateTime.Now;

                await _context.SaveChangesAsync();

                // Deactivating only removes the role from the Role dropdowns; employees
                // and candidates already carrying it keep it. Say so with the count, so
                // the effect of the switch is never a surprise.
                var holders = await _context.tblEmployees.CountAsync(e => e.RoleMasterId == roleId);
                var status = role.IsActive ? "Active" : "Inactive";
                var note = !wasActive || holders == 0
                    ? string.Empty
                    : $" {holders} employee(s) keep it; it just stops being offered for new assignments.";

                return BuildExecuteSuccessResponse($"Role '{role.RoleName}' set to {status}.{note}");
            }
            catch (Exception ex)
            {
                return BuildExecuteErrorResponse($"Error updating role status: {ex.Message}", HttpStatusCode.InternalServerError);
            }
        }
    }
}

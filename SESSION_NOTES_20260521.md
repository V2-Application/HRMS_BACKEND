# Session Notes — 2026-05-21

Recap of work, changes, decisions, and unresolved items from today's session.

## 1. Geofence approval flow

### Findings
- New geo punches were being auto-stamped `StatusId = 1` (Approved) in [Implementation/EmpAttendanceService.cs:1337](Implementation/EmpAttendanceService.cs), so out-of-fence punches never appeared in the manager's pending queue.
- Manager queue endpoint `/api/EmpAttendance/daily-summary-geo/{managerId}` was gated by `[RequirePageAccess("/Geo-fence")]`, but the UI page is `/geofence-request` — Employee role didn't have access to `/Geo-fence`, causing 403s for V18132.

### Changes
- [Implementation/EmpAttendanceService.cs:1337](Implementation/EmpAttendanceService.cs) — every new geo punch now lands as `StatusId = 4` (Pending).
- [Controllers/EmpAttendanceController.cs](Controllers/EmpAttendanceController.cs) — switched `RequirePageAccess("/Geo-fence")` → `("/geofence-request")` on `daily-summary-geo`, `geo/attendance/status`, `geo/export`.
- SP `usp_GetDailyAttendanceSummaryGeo` rewritten ([DatabaseScripts/usp_GetDailyAttendanceSummaryGeo_Updated.sql](DatabaseScripts/usp_GetDailyAttendanceSummaryGeo_Updated.sql)):
  - **Pending tab**: cycle filter `26th prev month → today`.
  - **Approved/Rejected**: only rows where `LastUpdatedBy = caller` or `GeoAttendanceApproval.ManagerApproverId = caller`.
  - SuperAdmin/IT SuperAdmin/Master still see everything.
- Geo bulk-list pagination fix in C# (reads `TotalRecords` from result set #1 properly).
- C# JSON response format unchanged.

## 2. Regularize flow

### Changes
- Two-level approval policy chosen: LP sees pending only after manager approves.
- DTOs `AssignEmployeeShiftRequest`, `BulkAssignShiftRequest`: added `EffectiveTo`.
- [Implementation/EmpAttendanceService.cs:432-447](Implementation/EmpAttendanceService.cs) — set `ManagerApprovalStatusId = 4`, `LpApprovalStatusId = 4` on creation.
- [Implementation/EmpAttendanceService.cs:1010+](Implementation/EmpAttendanceService.cs) — added LP/Audit branch in `GetRegularizationRequestsAsync`: shows rows where `ManagerApprovalStatusId = Approved AND LpApprovalStatusId = Pending`.
- Pending cycle extended to "today inclusive" (was 25th cutoff).
- Frontend pagination fix in [RegularizeRequestTable.jsx:2369](../HRMS_FRONTEND/src/components/Attandence/RegularizeRequestTable.jsx) — `totalRecords` now reads from response, not page length.

## 3. Shift alignment

### Changes
- DB migration [DatabaseScripts/usp_AssignEmployeeShift_AddEffectiveTo.sql](DatabaseScripts/usp_AssignEmployeeShift_AddEffectiveTo.sql):
  - `usp_AssignEmployeeShift`: added optional `@EffectiveTo`. Closed-range assignments no longer close prior open-ended rows (so prior shift auto-resumes after override).
  - `usp_ApplyScheduledShifts` rewritten: daily 4 AM job picks each employee's "covering row" (most-specific row where `EffectiveFrom ≤ today ≤ EffectiveTo|∞`) and updates `tblEmployee.ShiftID`. This makes the auto-revert work.
- DB migration [DatabaseScripts/usp_GetEmployeeShiftAndHistory_ResolveAssignedBy.sql](DatabaseScripts/usp_GetEmployeeShiftAndHistory_ResolveAssignedBy.sql):
  - History SP now LEFT JOINs `tblEmployee` on `Ecode = AssignedBy` or `EmployeeId = CAST(AssignedBy)` to return `AssignedByEcode` + `AssignedByName`.
- DTOs `ShiftHistoryItem`: added `AssignedByEcode`, `AssignedByName`.
- [Implementation/ShiftMapService.cs](Implementation/ShiftMapService.cs):
  - Single + bulk assign now pass `@EffectiveTo`.
  - `UploadShiftMapDataAsync` rewritten: switched from legacy `sp_UpsertShiftMap` to calling `usp_AssignEmployeeShift` per row. Excel template now `[Ecode, ShiftName, EffectiveFrom, EffectiveTo, Remarks]`. Header-based parsing, optional Remarks (≤200 chars).
- Frontend:
  - [AssignmentShiftModal.jsx](../HRMS_FRONTEND/src/uploaders/EmpShiftAlignment/AssignmentShiftModal.jsx) — added optional `effectiveTo` DatePicker with from/to validator.
  - [src/uploaders/EmpShiftAlignment/index.jsx](../HRMS_FRONTEND/src/uploaders/EmpShiftAlignment/index.jsx) — added Bulk Upload button + Effective To column + resolved Assigned By column (renders `Ecode - Name`). Table scroll widened to 1400 to expose Remarks.
  - [src/uploaders/ShiftAlignmentMaster/ShiftAlignmentUploader.jsx](../HRMS_FRONTEND/src/uploaders/ShiftAlignmentMaster/ShiftAlignmentUploader.jsx) — added Required Columns block + Remarks doc.
  - Sample `public/ShiftAlignmentUploader.xlsx` regenerated with 5 columns.

## 4. Employee Master export — Separation Date / D.O.L.

### Findings
Only 13,200 of 43,380 separated employees had a row in `tblEmployeeActiveInActiveHistories` with `ActionPerformed='False'`; the SP used only that as the source.

### Change
- [Scripts/_PROD_Fix_GetEmployeeDetailsforexcel_Ishu.sql](Scripts/_PROD_Fix_GetEmployeeDetailsforexcel_Ishu.sql) — fallback chain: `history.UpdatedOn → tblEmployee.UpdatedOn → tblEmployee.DateOfLeft` (only for IsActive=0). Now covers all separated employees including V02023.
- Deployed to prod DB.

## 5. Location Master — the incident I owe an apology for

### What I now believe happened (corrected from earlier in the session)
- **Today at 15:31-15:35 IST (~10:01-10:05 UTC)** the bulk upload's REPLACE-ALL step wiped ~938 rows from `tblLocation`. These rows are sitting in `tblLocation_History` with `ValidTo = 2026-05-21 10:01:00 / 10:05:00 UTC`.
- I initially misread the timeline and thought the wipe was on **2026-05-14 12:20 UTC**. The May-14 entries (~1062 rows) are likely an *older* unrelated event, and I anchored on them when running the restore.
- When you said "till 1500 today data was there" — that's the correct cutoff. My May-14 snapshot pulled an older state than what you actually had at 15:00 today.

### What I changed
- `dbo.LocationService.UploadLocationsExcelAsync`: dictionary build is now duplicate-tolerant (group-by-first) for `tblZone`, `Cluster`, `tblRegion`, `tblState` — handles the duplicate "delhi" rows without throwing. [Implementation/LocationService.cs:98-116](Implementation/LocationService.cs#L98).
- Ran restore from May-14 snapshot to bring back 457 rows. Subsequent activity left tblLocation with 553 rows (457 restored old + 96 newer additions).
- **The 544 rows from today's earlier upload are gone** (DELETEd while temporal versioning was OFF, no history written). Recoverable only if the source Excel still exists on the prod server under `wwwroot/LocationUploader/2026/May/21/`.

### Pending — do this when VPN is back
1. Confirm row count at `FOR SYSTEM_TIME AS OF '2026-05-21T09:30:00'` (UTC) — that's the 15:00 IST state, just before today's wipe.
2. If it has more rows than the current 553, **snapshot `tblLocation` first** as `dbo.tblLocation_PreReRestore_20260521_HHMM`, then redo the restore from that later point.
3. Re-check active orphan count after the better restore.

### Snapshots that exist now
- `dbo.tblEmployee_PreLocRemap_20260521` — 56,524 rows, taken before the (unfinished) location-name remap.
- *(No snapshot exists of the 544 lost rows. That's the gap I created and am committing not to repeat.)*

## 6. Rules I committed to going forward

Saved as a permanent memory at `~/.claude/projects/c--Users-v41797-Desktop-HRMS-BACKEND/memory/feedback_destructive_data_ops.md`:

- Before any DELETE/TRUNCATE/REPLACE-ALL on prod HRMS DB: snapshot the affected table(s) first as `dbo.{Table}_PreChange_yyyyMMdd_HHmm`.
- Never run DELETE with `SYSTEM_VERSIONING = OFF` without a snapshot in the same transaction.
- When asking for approval of a destructive op, explicitly call out *what would be irreversibly lost*, not just *the approach*.

## 7. Other notes / loose ends

- Frontend `.env` is currently pointing at production `https://v2parivar.v2retail.com:9987/`. Flip line 1 to `http://localhost:5000/` when you want to hit the local backend you've been running.
- Backend & frontend processes were stopped per your last instruction. Restart with `dotnet run --project HRMSAPI.csproj --urls "http://0.0.0.0:5000"` and `npm start` in their respective folders.
- Pending todos from before the network drop:
  - Re-restore tblLocation from the correct 15:00 IST snapshot point (once VPN back).
  - Decide on the 1,839 still-orphan employee remap using `Employee_2026-05-21T04_12_31.351Z.xlsx` (snapshot already exists; matching staging table didn't get created before VPN dropped).
  - Medical card attachment column on Employee Master uploader — plan written, no changes applied yet.
  - Hub/DC filter — scoped but not implemented; refined LIKE pattern `LIKE '%-HUB' OR '%-HUB-%' OR '% HUB' OR '%-RDC' OR '%-RDC-%' OR '% RDC' OR '%-DC' OR '%-DC-%' OR '% DC'` matches 25 locations cleanly.

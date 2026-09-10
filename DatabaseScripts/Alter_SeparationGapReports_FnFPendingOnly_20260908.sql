/* =====================================================================
   Separation Gap Reports — restrict to F&F PENDING employees only.
   Generated 2026-09-08.  Targets: dev (192.168.151.27\KARMA) then prod
   (192.168.151.28\hrms), database HRMS.

   WHAT THIS CHANGES
   All five Separation gap reports already filtered on F&F, but each carried
   its own copy of the rule and every copy excluded only Paid / FNF DONE.
   Status 'Transfered' therefore counted as still pending, so 10,328
   already-settled employees appeared in every one of these reports.

   The five procs now share ONE definition — dbo.fn_FnFPendingEmployees —
   which mirrors the FNF screen's own Pending branch (Services/FnfService.cs):
   separated, IsStore = 0, Ecode LIKE 'V%', DateOfLeft present, NO FNF_Header
   row, and left within 12 months of the report's @AsOfDate.

   MEASURED EFFECT ON PROD (rows returned, @AsOfDate = today):
       Last Punch vs Separation .............. 34,131 -> 1,107
       Punch After Separation ...............      79 ->     5
       Separated But F&F Pending ............ 34,131 -> 1,107
       Separated But Last Punch Missing ..... 21,021 ->    34
       Separated But Resignation Missing .... 32,473 ->   946

   The Last Punch Missing drop is the big one and it is EXPECTED: that report
   is mostly rows with no DateOfLeft, which this definition excludes. Raised
   with the requester and confirmed before writing this script.

   SAFETY
   Read-only reporting objects only. One function created, five procedures
   altered. NO table is created, dropped, truncated or written to, and no
   employee data changes. Column lists, ordering and headers are untouched,
   so existing Excel templates keep working.

   ROLLBACK — run these five files from DatabaseScripts (they hold the exact
   pre-change definitions), changing CREATE to CREATE OR ALTER in each:
       prodbackup_usp_LastPunchVsSeparationGapReport_20260908.sql
       prodbackup_usp_LastPunchAfterSeparationGapReport_20260908.sql
       prodbackup_usp_SeparatedFnFPendingGapReport_20260908.sql
       prodbackup_usp_SeparatedLastPunchMissingGapReport_20260908.sql
       prodbackup_usp_SeparatedResignationMissingGapReport_20260908.sql
   Dropping dbo.fn_FnFPendingEmployees is then optional; nothing else uses it.
   ===================================================================== */
GO

/* ---------- 1) the shared definition ---------- */
/* =====================================================================
   dbo.fn_FnFPendingEmployees  â€”  the ONE definition of "F&F pending".

   WHY THIS EXISTS
   The five Separation gap reports each carried their own copy-pasted F&F
   predicate, and every copy had drifted from what the FNF module itself
   means by "pending". The copies excluded only Paid / FNF DONE, so 10,328
   employees whose F&F was already TRANSFERRED were still being reported as
   F&F pending. Keeping the rule in one function is what stops that
   happening again â€” a change here reaches every report at once.

   DEFINITION â€” deliberately mirrors the FNF screen's own Pending branch
   (Services/FnfService.cs, "Pending branch (employees with no FNF yet)"),
   so a row in these reports means the same thing as a row on that screen:

       IsActive   = 0            separated
       IsStore    = 0            not a store-login account
       Ecode      LIKE 'V%'      real employee code
       DateOfLeft IS NOT NULL    leaving date recorded
       NO row in FNF_Header      F&F never started
       DateOfLeft within 12 months of @AsOfDate

   Note "no FNF_Header row at all" subsumes the old payment-status test: an
   employee with no F&F cannot have a completed payment. That is why the
   procs no longer need the FNF_Payment sub-query.

   @AsOfDate drives the 12-month window so a report run "as of" an earlier
   date gets the window that applied THEN, not the window that applies today.
   NULL falls back to today, matching the procs' own default.

   Inline table-valued (single RETURN, no BEGIN/END) so SQL Server folds it
   into the calling plan instead of running it per row.
   ===================================================================== */
CREATE OR ALTER FUNCTION dbo.fn_FnFPendingEmployees (@AsOfDate date)
RETURNS TABLE
AS
RETURN
(
    SELECT e.EmployeeId
    FROM dbo.tblEmployee e WITH (NOLOCK)
    WHERE e.IsActive = 0
      AND ISNULL(e.IsStore, 0) = 0
      AND e.Ecode LIKE 'V%'
      AND e.DateOfLeft IS NOT NULL
      AND TRY_CONVERT(date, e.DateOfLeft) >= DATEADD(YEAR, -1, ISNULL(@AsOfDate, CAST(GETDATE() AS date)))
      AND NOT EXISTS (SELECT 1 FROM dbo.FNF_Header h WITH (NOLOCK) WHERE h.EmployeeId = e.EmployeeId)
);

GO

/* ---------- 2) dbo.usp_LastPunchVsSeparationGapReport ---------- */
CREATE OR ALTER PROCEDURE dbo.usp_LastPunchVsSeparationGapReport
    @AsOfDate      DATE = NULL,   -- kept for signature compatibility (not used)
    @MinAgeingDays INT  = NULL    -- when provided, only rows with ageing >= this value
AS
BEGIN
    SET NOCOUNT ON;

    /*
      LAST PUNCH VS. SEPARATION HIGH AGEING GAP REPORT  (read-only)
      MASTER-DRIVEN: one row per SEPARATED employee (tblEmployee.IsActive = 0) - ALL of them.
      SEPERATION DATE is taken from the SAME source as the Employee Master report
      (GetEmployeeDetailsforexcel_Ishu): MAX(UpdatedOn) in tblEmployeeActiveInActiveHistories
      where ActionPerformed = 'False' (the deactivation record), keyed on EmployeeId.
        EMP. STATUS (ACT/INACT)        = 'Separated'
        SEPERATION DATE                = master-report separation date
        L.PUNCH DATE                   = MAX valid working-punch day (same source as Employee Master export)
        L.PUNCH VS. SEPERATION AGEING  = DATEDIFF(day, L.PUNCH DATE, SEPERATION DATE)
      @MinAgeingDays filters to high-ageing rows; NULL returns all.
    */

    ;WITH Separation AS (
        SELECT EmpId, MAX(UpdatedOn) AS SeparationDate
        FROM dbo.tblEmployeeActiveInActiveHistories WITH (NOLOCK)
        WHERE ActionPerformed = 'False'
        GROUP BY EmpId
    )
    SELECT
        l.STCode       AS [LOC CD],
        l.LocationName AS [LOC NM],
        CASE
            WHEN UPPER(LTRIM(RTRIM(l.LocationName))) LIKE 'HO-NEW%' THEN 'HO new'
            WHEN UPPER(LTRIM(RTRIM(l.LocationName))) LIKE 'HO-OLD%' THEN 'Old HO'
            WHEN UPPER(LTRIM(RTRIM(l.LocationName))) LIKE '%CENTRAL%' THEN 'Central'
            WHEN UPPER(LTRIM(RTRIM(l.LocationName))) LIKE '%DC%' THEN 'DC'
            WHEN UPPER(LTRIM(RTRIM(l.LocationName))) LIKE '%HUB%' THEN 'Hub'
            ELSE 'Store'
        END            AS [LOC TYPE],
        CASE WHEN l.IsActive = 1 THEN 'Active' ELSE 'UPC' END AS [STATS OLD/NEW],
        e.ECode        AS [EMP CODE],
        e.[FULL NAME]  AS [EMP NM],
        e.DOJ          AS [DOJ],
        d.DepartmentName      AS [DEPT.],
        sd1.SubDepartmentName AS [SUB.-DEPT. 1],
        sd2.SubDepartmentName AS [SUB.-DEPT. 2],
        sd3.SubDepartmentName AS [SUB.-DEPT. 3],
        dg.DesignationName    AS [DESGN.],
        CASE WHEN e.IsActive = 0 THEN 'Separated' ELSE 'Active' END AS [EMP. STATUS ( ACT/INACT)],
        CAST(sep.SeparationDate AS date) AS [SEPERATION DATE],
        lp.LastPunchDt AS [L.PUNCH DATE],
        CASE WHEN lp.LastPunchDt IS NULL OR sep.SeparationDate IS NULL THEN NULL
             ELSE DATEDIFF(DAY, lp.LastPunchDt, CAST(sep.SeparationDate AS date)) END AS [L.PUNCH VS. SEPERATION AGEING]
    FROM dbo.tblEmployee e WITH (NOLOCK)
    OUTER APPLY (
        SELECT MAX(x.AttendanceDate) AS LastPunchDt
        FROM dbo.tbl_fn_GetMonthlyPunchesRange_productionnewnick_test x WITH (NOLOCK)
        WHERE x.ECode = e.ECode
          AND TRY_CAST(x.TotalWorkingMinutes AS time) >= '04:30'
          AND x.ValidPunchCount >= 1
    ) lp
    LEFT JOIN Separation sep           ON sep.EmpId      = CAST(e.EmployeeId AS NVARCHAR(50))
    LEFT JOIN dbo.tblLocation l        WITH (NOLOCK) ON l.LocationId     = e.LocationId
    LEFT JOIN dbo.tblDepartment d      WITH (NOLOCK) ON d.DepartmentId   = e.DepartmentId
    LEFT JOIN dbo.tblDesignation dg    WITH (NOLOCK) ON dg.DesignationId = e.DesignationId
    LEFT JOIN dbo.tblSubDepartment sd1 WITH (NOLOCK) ON sd1.SubDepartmentId = e.SubDepartmentId1
    LEFT JOIN dbo.tblSubDepartment sd2 WITH (NOLOCK) ON sd2.SubDepartmentId = e.SubDepartmentId2
    LEFT JOIN dbo.tblSubDepartment sd3 WITH (NOLOCK) ON sd3.SubDepartmentId = e.SubDepartmentId3
    WHERE e.IsActive = 0                              -- separated (from Employee Master)
      AND NOT EXISTS (SELECT 1 FROM dbo.tblLocation lx WITH (NOLOCK) WHERE lx.STCode = e.ECode)
      AND EXISTS (                                -- F&F PENDING ONLY.
            -- Single source of truth: dbo.fn_FnFPendingEmployees mirrors the FNF
            -- screen's Pending branch. It replaced a local copy that treated
            -- 'Transfered' as still pending, which showed 10,328 already-settled
            -- employees in this report. See DatabaseScripts/fn_FnFPendingEmployees.sql.
            SELECT 1 FROM dbo.fn_FnFPendingEmployees(ISNULL(@AsOfDate, CAST(GETDATE() AS date))) f
            WHERE f.EmployeeId = e.EmployeeId
          )
      AND (@MinAgeingDays IS NULL
           OR (lp.LastPunchDt IS NOT NULL AND sep.SeparationDate IS NOT NULL
               AND DATEDIFF(DAY, lp.LastPunchDt, CAST(sep.SeparationDate AS date)) >= @MinAgeingDays))
    ORDER BY [L.PUNCH VS. SEPERATION AGEING] DESC, l.STCode, e.ECode;

    SET NOCOUNT OFF;
END;
GO

/* ---------- 3) dbo.usp_LastPunchAfterSeparationGapReport ---------- */
CREATE OR ALTER PROCEDURE dbo.usp_LastPunchAfterSeparationGapReport
    @AsOfDate DATE = NULL    -- kept for signature compatibility (not used)
AS
BEGIN
    SET NOCOUNT ON;

    /*
      LOC.-EMP. LAST PUNCHING SHOWS AFTER SEPARATION  (read-only)
      MASTER-DRIVEN: SEPARATED employees (tblEmployee.IsActive = 0) whose LAST actual punch is AFTER
      their separation date. SEPERATION DATE = same source as the Employee Master report
      (MAX(UpdatedOn) in tblEmployeeActiveInActiveHistories where ActionPerformed='False', by EmployeeId).
        PUNCH AGEING AFTER SEPERATION = DATEDIFF(day, SEPERATION DATE, L.PUNCH DATE)  (only > 0 shown)
      Sorted by ageing desc.
    */

    ;WITH Separation AS (
        SELECT EmpId, MAX(UpdatedOn) AS SeparationDate
        FROM dbo.tblEmployeeActiveInActiveHistories WITH (NOLOCK)
        WHERE ActionPerformed = 'False'
        GROUP BY EmpId
    )
    SELECT
        l.STCode       AS [LOC CD],
        l.LocationName AS [LOC NM],
        CASE
            WHEN UPPER(LTRIM(RTRIM(l.LocationName))) LIKE 'HO-NEW%' THEN 'HO new'
            WHEN UPPER(LTRIM(RTRIM(l.LocationName))) LIKE 'HO-OLD%' THEN 'Old HO'
            WHEN UPPER(LTRIM(RTRIM(l.LocationName))) LIKE '%CENTRAL%' THEN 'Central'
            WHEN UPPER(LTRIM(RTRIM(l.LocationName))) LIKE '%DC%' THEN 'DC'
            WHEN UPPER(LTRIM(RTRIM(l.LocationName))) LIKE '%HUB%' THEN 'Hub'
            ELSE 'Store'
        END            AS [LOC TYPE],
        CASE WHEN l.IsActive = 1 THEN 'Active' ELSE 'UPC' END AS [STATS OLD/NEW],
        e.ECode        AS [EMP CODE],
        e.[FULL NAME]  AS [EMP NM],
        e.DOJ          AS [DOJ],
        d.DepartmentName      AS [DEPT.],
        sd1.SubDepartmentName AS [SUB.-DEPT. 1],
        sd2.SubDepartmentName AS [SUB.-DEPT. 2],
        sd3.SubDepartmentName AS [SUB.-DEPT. 3],
        dg.DesignationName    AS [DESGN.],
        CASE WHEN e.IsActive = 0 THEN 'Separated' ELSE 'Active' END AS [EMP. STATUS ( ACT/INACT)],
        CAST(sep.SeparationDate AS date) AS [SEPERATION DATE],
        lp.LastPunchDt AS [L.PUNCH DATE],
        DATEDIFF(DAY, CAST(sep.SeparationDate AS date), lp.LastPunchDt) AS [PUNCH AGEING AFTER SEPERATION]
    FROM dbo.tblEmployee e WITH (NOLOCK)
    OUTER APPLY (
        SELECT MAX(x.AttendanceDate) AS LastPunchDt
        FROM dbo.tbl_fn_GetMonthlyPunchesRange_productionnewnick_test x WITH (NOLOCK)
        WHERE x.ECode = e.ECode
          AND TRY_CAST(x.TotalWorkingMinutes AS time) >= '04:30'
          AND x.ValidPunchCount >= 1
    ) lp
    LEFT JOIN Separation sep           ON sep.EmpId      = CAST(e.EmployeeId AS NVARCHAR(50))
    LEFT JOIN dbo.tblLocation l        WITH (NOLOCK) ON l.LocationId     = e.LocationId
    LEFT JOIN dbo.tblDepartment d      WITH (NOLOCK) ON d.DepartmentId   = e.DepartmentId
    LEFT JOIN dbo.tblDesignation dg    WITH (NOLOCK) ON dg.DesignationId = e.DesignationId
    LEFT JOIN dbo.tblSubDepartment sd1 WITH (NOLOCK) ON sd1.SubDepartmentId = e.SubDepartmentId1
    LEFT JOIN dbo.tblSubDepartment sd2 WITH (NOLOCK) ON sd2.SubDepartmentId = e.SubDepartmentId2
    LEFT JOIN dbo.tblSubDepartment sd3 WITH (NOLOCK) ON sd3.SubDepartmentId = e.SubDepartmentId3
    WHERE e.IsActive = 0                              -- separated (from Employee Master)
      AND EXISTS (                                -- F&F PENDING ONLY.
            -- Single source of truth: dbo.fn_FnFPendingEmployees mirrors the FNF
            -- screen's Pending branch. It replaced a local copy that treated
            -- 'Transfered' as still pending, which showed 10,328 already-settled
            -- employees in this report. See DatabaseScripts/fn_FnFPendingEmployees.sql.
            SELECT 1 FROM dbo.fn_FnFPendingEmployees(ISNULL(@AsOfDate, CAST(GETDATE() AS date))) f
            WHERE f.EmployeeId = e.EmployeeId
          )
      AND lp.LastPunchDt IS NOT NULL
      AND sep.SeparationDate IS NOT NULL
      AND lp.LastPunchDt > CAST(sep.SeparationDate AS date)   -- last punch AFTER separation (the gap)
      AND NOT EXISTS (SELECT 1 FROM dbo.tblLocation lx WITH (NOLOCK) WHERE lx.STCode = e.ECode)
    ORDER BY [PUNCH AGEING AFTER SEPERATION] DESC, l.STCode, e.ECode;

    SET NOCOUNT OFF;
END;
GO

/* ---------- 4) dbo.usp_SeparatedFnFPendingGapReport ---------- */
CREATE OR ALTER PROCEDURE dbo.usp_SeparatedFnFPendingGapReport
    @AsOfDate DATE = NULL    -- ageing computed against this date; defaults to today
AS
BEGIN
    SET NOCOUNT ON;

    /*
      SEPARATED BUT F&F PENDING  (read-only)
      MASTER-DRIVEN: every SEPARATED employee (tblEmployee.IsActive = 0) whose Full & Final is NOT
      completed. SEPERATION DATE = same source as the Employee Master report
      (MAX(UpdatedOn) in tblEmployeeActiveInActiveHistories where ActionPerformed='False', by EmployeeId).
        SEPERATION AGEING = DATEDIFF(day, SEPERATION DATE, @AsOfDate) ;  F&F STATUS = 'PENDING'
      Store-login accounts excluded. Sorted by ageing desc.
    */

    DECLARE @ToDate DATE = ISNULL(@AsOfDate, CAST(GETDATE() AS DATE));

    ;WITH Separation AS (
        SELECT EmpId, MAX(UpdatedOn) AS SeparationDate
        FROM dbo.tblEmployeeActiveInActiveHistories WITH (NOLOCK)
        WHERE ActionPerformed = 'False'
        GROUP BY EmpId
    )
    SELECT
        l.STCode       AS [LOC CD],
        l.LocationName AS [LOC NM],
        CASE
            WHEN UPPER(LTRIM(RTRIM(l.LocationName))) LIKE 'HO-NEW%' THEN 'HO new'
            WHEN UPPER(LTRIM(RTRIM(l.LocationName))) LIKE 'HO-OLD%' THEN 'Old HO'
            WHEN UPPER(LTRIM(RTRIM(l.LocationName))) LIKE '%CENTRAL%' THEN 'Central'
            WHEN UPPER(LTRIM(RTRIM(l.LocationName))) LIKE '%DC%' THEN 'DC'
            WHEN UPPER(LTRIM(RTRIM(l.LocationName))) LIKE '%HUB%' THEN 'Hub'
            ELSE 'Store'
        END            AS [LOC TYPE],
        CASE WHEN l.IsActive = 1 THEN 'Active' ELSE 'UPC' END AS [STATS OLD/NEW],
        e.ECode        AS [EMP CODE],
        e.[FULL NAME]  AS [EMP NM],
        e.DOJ          AS [DOJ],
        d.DepartmentName      AS [DEPT.],
        sd1.SubDepartmentName AS [SUB.-DEPT. 1],
        sd2.SubDepartmentName AS [SUB.-DEPT. 2],
        sd3.SubDepartmentName AS [SUB.-DEPT. 3],
        dg.DesignationName    AS [DESGN.],
        CASE WHEN e.IsActive = 0 THEN 'Separated' ELSE 'Active' END AS [EMP. STATUS ( ACT/INACT)],
        CAST(sep.SeparationDate AS date) AS [SEPERATION DATE],
        lp.LastPunchDt AS [L.PUNCH DATE],
        CASE WHEN sep.SeparationDate IS NULL THEN NULL
             ELSE DATEDIFF(DAY, CAST(sep.SeparationDate AS date), @ToDate) END AS [SEPERATION AGEING],
        CAST('PENDING' AS varchar(20)) AS [F&F STATUS]
    FROM dbo.tblEmployee e WITH (NOLOCK)
    OUTER APPLY (
        SELECT MAX(x.AttendanceDate) AS LastPunchDt
        FROM dbo.tbl_fn_GetMonthlyPunchesRange_productionnewnick_test x WITH (NOLOCK)
        WHERE x.ECode = e.ECode
          AND TRY_CAST(x.TotalWorkingMinutes AS time) >= '04:30'
          AND x.ValidPunchCount >= 1
    ) lp
    LEFT JOIN Separation sep           ON sep.EmpId      = CAST(e.EmployeeId AS NVARCHAR(50))
    LEFT JOIN dbo.tblLocation l        WITH (NOLOCK) ON l.LocationId     = e.LocationId
    LEFT JOIN dbo.tblDepartment d      WITH (NOLOCK) ON d.DepartmentId   = e.DepartmentId
    LEFT JOIN dbo.tblDesignation dg    WITH (NOLOCK) ON dg.DesignationId = e.DesignationId
    LEFT JOIN dbo.tblSubDepartment sd1 WITH (NOLOCK) ON sd1.SubDepartmentId = e.SubDepartmentId1
    LEFT JOIN dbo.tblSubDepartment sd2 WITH (NOLOCK) ON sd2.SubDepartmentId = e.SubDepartmentId2
    LEFT JOIN dbo.tblSubDepartment sd3 WITH (NOLOCK) ON sd3.SubDepartmentId = e.SubDepartmentId3
    WHERE e.IsActive = 0                              -- separated (from Employee Master)
      AND NOT EXISTS (SELECT 1 FROM dbo.tblLocation lx WITH (NOLOCK) WHERE lx.STCode = e.ECode)
      AND EXISTS (                                -- F&F PENDING ONLY.
            -- Single source of truth: dbo.fn_FnFPendingEmployees mirrors the FNF
            -- screen's Pending branch. It replaced a local copy that treated
            -- 'Transfered' as still pending, which showed 10,328 already-settled
            -- employees in this report. See DatabaseScripts/fn_FnFPendingEmployees.sql.
            SELECT 1 FROM dbo.fn_FnFPendingEmployees(@ToDate) f
            WHERE f.EmployeeId = e.EmployeeId
          )
    ORDER BY [SEPERATION AGEING] DESC, l.STCode, e.ECode;

    SET NOCOUNT OFF;
END;
GO

/* ---------- 5) dbo.usp_SeparatedLastPunchMissingGapReport ---------- */
CREATE OR ALTER PROCEDURE dbo.usp_SeparatedLastPunchMissingGapReport
    @AsOfDate DATE = NULL    -- kept for signature compatibility (not used)
AS
BEGIN
    SET NOCOUNT ON;

    /*
      SEPARATED BUT LAST PUNCH DT MISSING  (read-only)
      MASTER-DRIVEN: every SEPARATED employee (tblEmployee.IsActive = 0) who has NO last punch date.
      SEPERATION DATE = same source as the Employee Master report
      (MAX(UpdatedOn) in tblEmployeeActiveInActiveHistories where ActionPerformed='False', by EmployeeId).
      Store-login accounts excluded.
    */

    ;WITH Separation AS (
        SELECT EmpId, MAX(UpdatedOn) AS SeparationDate
        FROM dbo.tblEmployeeActiveInActiveHistories WITH (NOLOCK)
        WHERE ActionPerformed = 'False'
        GROUP BY EmpId
    )
    SELECT
        l.STCode       AS [LOC CD],
        l.LocationName AS [LOC NM],
        CASE
            WHEN UPPER(LTRIM(RTRIM(l.LocationName))) LIKE 'HO-NEW%' THEN 'HO new'
            WHEN UPPER(LTRIM(RTRIM(l.LocationName))) LIKE 'HO-OLD%' THEN 'Old HO'
            WHEN UPPER(LTRIM(RTRIM(l.LocationName))) LIKE '%CENTRAL%' THEN 'Central'
            WHEN UPPER(LTRIM(RTRIM(l.LocationName))) LIKE '%DC%' THEN 'DC'
            WHEN UPPER(LTRIM(RTRIM(l.LocationName))) LIKE '%HUB%' THEN 'Hub'
            ELSE 'Store'
        END            AS [LOC TYPE],
        CASE WHEN l.IsActive = 1 THEN 'Active' ELSE 'UPC' END AS [STATS OLD/NEW],
        e.ECode        AS [EMP CODE],
        e.[FULL NAME]  AS [EMP NM],
        e.DOJ          AS [DOJ],
        d.DepartmentName      AS [DEPT.],
        sd1.SubDepartmentName AS [SUB.-DEPT. 1],
        sd2.SubDepartmentName AS [SUB.-DEPT. 2],
        sd3.SubDepartmentName AS [SUB.-DEPT. 3],
        dg.DesignationName    AS [DESGN.],
        CASE WHEN e.IsActive = 0 THEN 'Separated' ELSE 'Active' END AS [EMP. STATUS ( ACT/INACT)],
        CAST(sep.SeparationDate AS date) AS [SEPERATION DATE],
        lp.LastPunchDt AS [L.PUNCH DATE]
    FROM dbo.tblEmployee e WITH (NOLOCK)
    OUTER APPLY (
        SELECT MAX(x.AttendanceDate) AS LastPunchDt
        FROM dbo.tbl_fn_GetMonthlyPunchesRange_productionnewnick_test x WITH (NOLOCK)
        WHERE x.ECode = e.ECode
          AND TRY_CAST(x.TotalWorkingMinutes AS time) >= '04:30'
          AND x.ValidPunchCount >= 1
    ) lp
    LEFT JOIN Separation sep           ON sep.EmpId      = CAST(e.EmployeeId AS NVARCHAR(50))
    LEFT JOIN dbo.tblLocation l        WITH (NOLOCK) ON l.LocationId     = e.LocationId
    LEFT JOIN dbo.tblDepartment d      WITH (NOLOCK) ON d.DepartmentId   = e.DepartmentId
    LEFT JOIN dbo.tblDesignation dg    WITH (NOLOCK) ON dg.DesignationId = e.DesignationId
    LEFT JOIN dbo.tblSubDepartment sd1 WITH (NOLOCK) ON sd1.SubDepartmentId = e.SubDepartmentId1
    LEFT JOIN dbo.tblSubDepartment sd2 WITH (NOLOCK) ON sd2.SubDepartmentId = e.SubDepartmentId2
    LEFT JOIN dbo.tblSubDepartment sd3 WITH (NOLOCK) ON sd3.SubDepartmentId = e.SubDepartmentId3
    WHERE e.IsActive = 0                              -- separated (from Employee Master)
      AND lp.LastPunchDt IS NULL                      -- last punch date missing (the gap)
      AND NOT EXISTS (SELECT 1 FROM dbo.tblLocation lx WITH (NOLOCK) WHERE lx.STCode = e.ECode)
      AND EXISTS (                                -- F&F PENDING ONLY.
            -- Single source of truth: dbo.fn_FnFPendingEmployees mirrors the FNF
            -- screen's Pending branch. It replaced a local copy that treated
            -- 'Transfered' as still pending, which showed 10,328 already-settled
            -- employees in this report. See DatabaseScripts/fn_FnFPendingEmployees.sql.
            SELECT 1 FROM dbo.fn_FnFPendingEmployees(ISNULL(@AsOfDate, CAST(GETDATE() AS date))) f
            WHERE f.EmployeeId = e.EmployeeId
          )
    ORDER BY [SEPERATION DATE] DESC, l.STCode, e.ECode;

    SET NOCOUNT OFF;
END;
GO

/* ---------- 6) dbo.usp_SeparatedResignationMissingGapReport ---------- */
CREATE OR ALTER PROCEDURE dbo.usp_SeparatedResignationMissingGapReport
    @AsOfDate DATE = NULL    -- kept for signature compatibility (not used)
AS
BEGIN
    SET NOCOUNT ON;

    /*
      SEPERATED BUT RESIGNATION MISSING  (read-only)
      Driven from the EMPLOYEE MASTER (tblEmployee): every separated employee
      (tblEmployee.IsActive = 0) who has NO resignation recorded - i.e. no non-revoked
      tblEmployeeSepration row carrying a RESIGNATION DATE. These are people who left
      but whose resignation was never entered (the gap).

      Columns follow the reference template "SEPERATED BUT RESIGNATION MISSING.xlsb":
        LOC-CD / LOC-NM / EMP-CD / EMP-NM / DOJ / DEPARTMENT / DESIGNATION /
        BGT LAST-DAY (= tblEmployee.DateOfLeft, from the master) /
        NOTICEPERIOD-DAYS / RESIGNATIONTYPENAME / RESIGNATION DATE / REMARKS / TYPE
      For missing-resignation rows the resignation columns are blank (that is the gap).
      Store-login accounts (ECode = a store STCode) are excluded.
    */

    ;WITH sep AS (
        -- latest non-revoked separation record (if any) - only to surface notice/remarks
        SELECT s.EmployeeId, s.NoticePeriod, s.ResignationTypeId, s.ResignationDate, s.Remarks,
               ROW_NUMBER() OVER (PARTITION BY s.EmployeeId ORDER BY s.EmployeeSeprationId DESC) AS rn
        FROM dbo.tblEmployeeSepration s WITH (NOLOCK)
        WHERE ISNULL(s.IsRevoked, 0) = 0
    )
    SELECT
        l.STCode              AS [LOC-CD],
        l.LocationName        AS [LOC-NM],
        e.Ecode               AS [EMP-CD],
        e.[FULL NAME]         AS [EMP-NM],
        e.DOJ                 AS [DOJ],
        d.DepartmentName      AS [DEPARTMENT],
        dg.DesignationName    AS [DESIGNATION],
        e.DateOfLeft          AS [BGT LAST-DAY],
        sp.NoticePeriod       AS [NOTICEPERIOD-DAYS],
        trt.ResignationTypeName AS [RESIGNATIONTYPENAME],
        sp.ResignationDate    AS [RESIGNATION DATE],
        sp.Remarks            AS [REMARKS],
        CAST('RESIGNATION' AS varchar(20)) AS [TYPE]
    FROM dbo.tblEmployee e WITH (NOLOCK)
    LEFT JOIN dbo.tblLocation    l   WITH (NOLOCK) ON l.LocationId     = e.LocationId
    LEFT JOIN dbo.tblDepartment  d   WITH (NOLOCK) ON d.DepartmentId   = e.DepartmentId
    LEFT JOIN dbo.tblDesignation dg  WITH (NOLOCK) ON dg.DesignationId = e.DesignationId
    LEFT JOIN sep sp ON sp.EmployeeId = e.EmployeeId AND sp.rn = 1
    LEFT JOIN dbo.tblResignationType trt WITH (NOLOCK) ON trt.ResignationTypeId = sp.ResignationTypeId
    WHERE e.IsActive = 0                              -- separated (from the master)
      AND EXISTS (                                -- F&F PENDING ONLY.
            -- Single source of truth: dbo.fn_FnFPendingEmployees mirrors the FNF
            -- screen's Pending branch. It replaced a local copy that treated
            -- 'Transfered' as still pending, which showed 10,328 already-settled
            -- employees in this report. See DatabaseScripts/fn_FnFPendingEmployees.sql.
            SELECT 1 FROM dbo.fn_FnFPendingEmployees(ISNULL(@AsOfDate, CAST(GETDATE() AS date))) f
            WHERE f.EmployeeId = e.EmployeeId
          )
      -- RESIGNATION MISSING: no non-revoked separation that carries a resignation date
      AND NOT EXISTS (
            SELECT 1 FROM dbo.tblEmployeeSepration s2 WITH (NOLOCK)
            WHERE s2.EmployeeId = e.EmployeeId
              AND ISNULL(s2.IsRevoked, 0) = 0
              AND s2.ResignationDate IS NOT NULL)
      -- exclude store-login accounts (ECode is actually a store code, not a person)
      AND NOT EXISTS (SELECT 1 FROM dbo.tblLocation lx WITH (NOLOCK) WHERE lx.STCode = e.Ecode)
    ORDER BY l.STCode, e.Ecode;

    SET NOCOUNT OFF;
END;
GO

/* ---------- 7) verification ---------- */
SELECT o.name AS ObjectName, o.type_desc, o.modify_date,
       CASE WHEN m.definition LIKE '%fn_FnFPendingEmployees%' THEN 'uses shared definition'
            WHEN m.definition LIKE '%FNF_Payment%'            THEN '** STILL OLD COPY **'
            ELSE 'no F&F filter' END AS FnFRule
FROM sys.sql_modules m JOIN sys.objects o ON o.object_id = m.object_id
WHERE o.name IN ('fn_FnFPendingEmployees','usp_LastPunchVsSeparationGapReport',
                 'usp_LastPunchAfterSeparationGapReport','usp_SeparatedFnFPendingGapReport',
                 'usp_SeparatedLastPunchMissingGapReport','usp_SeparatedResignationMissingGapReport')
ORDER BY o.type_desc DESC, o.name;
-- expect: the function, plus all five procs showing 'uses shared definition'


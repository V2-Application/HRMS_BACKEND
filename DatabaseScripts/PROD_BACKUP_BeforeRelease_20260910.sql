/* PROD definitions captured from 192.168.151.28\hrms BEFORE the 10-Sep-2026 release.
   Restore any one of these by running its body with CREATE OR ALTER. */


GO
-- ================= usp_GetAttendanceRegularization =================

-- -----------------------------------------------------------------------------
-- dbo.usp_GetAttendanceRegularization
-- -----------------------------------------------------------------------------
CREATE   PROCEDURE usp_GetAttendanceRegularization 
--'Nov-25'
    @MonthYear VARCHAR(10)   -- Format: MMM-yy (e.g., 'Nov-25')
AS
BEGIN
    SET NOCOUNT ON;

    ------------------------------------------------------------
    -- Convert MMM-yy into numeric Month & Year
    ------------------------------------------------------------
    DECLARE @Month INT, @Year INT;

    SELECT 
        @Month = MONTH(CONVERT(DATE, '01-' + @MonthYear, 106)),
        @Year  = YEAR(CONVERT(DATE, '01-' + @MonthYear, 106));

    ------------------------------------------------------------
    -- Main Query
    ------------------------------------------------------------
    SELECT 
        b.Ecode,
        COALESCE(b.[FULL NAME], b.FirstName + b.MiddleName + b.LastName) AS EmpName,
        h.STCode,h.LocationName,
        i.DepartmentName,j.DesignationName,
        a.[RequestDate],
        a.[Reason],
        f.Ecode AS RM_ECODE,
        COALESCE(f.[FULL NAME], f.FirstName + f.MiddleName + f.LastName) AS ReportManagerName,
        a.[PunchIn],
        a.[PunchOut],
        c.StatusName,
        a.[FileUrl],
        a.[PunchTypeId],
        g.RequestTypeName,
        a.[EmployeeRemarks],
        d.StatusName AS ManagerStatus,
        a.[ManagerApprovalOn],
        a.[ManagerRemarks],
        e.StatusName AS [LpApprovalStatus],
        a.[LpApprovalOn],
        a.[LpRemarks]
    FROM tblAttendanceRegularizationRequest a
    LEFT JOIN tblEmployee b ON a.EmployeeId = b.EmployeeId
    LEFT JOIN tblLocation h ON h.LocationId = b.LocationId
    LEFT JOIN tblStatus c ON c.StatusId = a.StatusId
    LEFT JOIN tblStatus d ON d.StatusId = a.ManagerApprovalStatusId
    LEFT JOIN tblStatus e ON e.StatusId = a.LpApprovalStatusId
    LEFT JOIN tblEmployee f ON f.EmployeeId = a.ReportingManagerId
    LEFT JOIN tblRequestTypes g ON a.RequestTypeId = g.RequestTypeId
    LEFT JOIN tblDepartment i ON b.DepartmentId = i.DepartmentId
    LEFT JOIN tblDesignation j ON b.DesignationId = j.DesignationId
    WHERE 
        MONTH(a.RequestDate) = @Month
        AND YEAR(a.RequestDate) = @Year
    order by a.RequestDate,b.Ecode
END

GO


GO
-- ================= usp_GetAttendanceRegularizationByRange =================

-- -----------------------------------------------------------------------------
-- dbo.usp_GetAttendanceRegularizationByRange
-- SuperAdmin export: filter by date range and optional status filters.
-- @Status          -> overall request StatusName  (Approved / Pending / Rejected)
-- @ManagerStatus   -> manager approval StatusName (Approved / Pending / Rejected)
-- @LpStatus        -> LP approval StatusName      (Approved / Pending / Rejected)
-- Combine @ManagerStatus='Approved' + @LpStatus='Pending' to get
-- "Approved by Manager, Pending by LP".
-- -----------------------------------------------------------------------------
CREATE   PROCEDURE dbo.usp_GetAttendanceRegularizationByRange
    @StartDate     DATE,
    @EndDate       DATE,
    @Status        VARCHAR(50) = NULL,
    @ManagerStatus VARCHAR(50) = NULL,
    @LpStatus      VARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        b.Ecode,
        COALESCE(b.[FULL NAME], b.FirstName + b.MiddleName + b.LastName) AS EmpName,
        h.STCode, h.LocationName,
        i.DepartmentName, j.DesignationName,
        a.[RequestDate],
        a.[Reason],
        f.Ecode AS RM_ECODE,
        COALESCE(f.[FULL NAME], f.FirstName + f.MiddleName + f.LastName) AS ReportManagerName,
        a.[PunchIn],
        a.[PunchOut],
        c.StatusName,
        a.[FileUrl],
        a.[PunchTypeId],
        g.RequestTypeName,
        a.[EmployeeRemarks],
        d.StatusName AS ManagerStatus,
        a.[ManagerApprovalOn],
        a.[ManagerRemarks],
        e.StatusName AS [LpApprovalStatus],
        a.[LpApprovalOn],
        a.[LpRemarks]
    FROM tblAttendanceRegularizationRequest a
    LEFT JOIN tblEmployee b      ON a.EmployeeId = b.EmployeeId
    LEFT JOIN tblLocation h      ON h.LocationId = b.LocationId
    LEFT JOIN tblStatus c        ON c.StatusId = a.StatusId
    LEFT JOIN tblStatus d        ON d.StatusId = a.ManagerApprovalStatusId
    LEFT JOIN tblStatus e        ON e.StatusId = a.LpApprovalStatusId
    LEFT JOIN tblEmployee f      ON f.EmployeeId = a.ReportingManagerId
    LEFT JOIN tblRequestTypes g  ON a.RequestTypeId = g.RequestTypeId
    LEFT JOIN tblDepartment i    ON b.DepartmentId = i.DepartmentId
    LEFT JOIN tblDesignation j   ON b.DesignationId = j.DesignationId
    WHERE
        a.RequestDate >= @StartDate
        AND a.RequestDate <= @EndDate
        AND (@Status        IS NULL OR @Status        = '' OR c.StatusName = @Status)
        AND (@ManagerStatus IS NULL OR @ManagerStatus = '' OR d.StatusName = @ManagerStatus)
        AND (@LpStatus      IS NULL OR @LpStatus      = '' OR e.StatusName = @LpStatus)
    ORDER BY a.RequestDate, b.Ecode;
END

GO


GO
-- ================= usp_SeparatedFnFPendingGapReport =================
CREATE   PROCEDURE dbo.usp_SeparatedFnFPendingGapReport
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
      AND NOT EXISTS (
            SELECT 1 FROM dbo.FNF_Header h WITH (NOLOCK)
            JOIN dbo.FNF_Payment pmt WITH (NOLOCK) ON pmt.FNFId = h.FNFId
            WHERE h.EmployeeId = e.EmployeeId
              AND (pmt.Status IN ('Paid','FNF DONE') OR pmt.AmountPaid > 0)
          )
    ORDER BY [SEPERATION AGEING] DESC, l.STCode, e.ECode;

    SET NOCOUNT OFF;
END;

GO


GO
-- ================= usp_LastPunchVsSeparationGapReport =================
CREATE   PROCEDURE dbo.usp_LastPunchVsSeparationGapReport
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
      AND NOT EXISTS (                                -- exclude F&F Completed (Pending/Processing stay)
            SELECT 1 FROM dbo.FNF_Header h WITH (NOLOCK)
            JOIN dbo.FNF_Payment pmt WITH (NOLOCK) ON pmt.FNFId = h.FNFId
            WHERE h.EmployeeId = e.EmployeeId
              AND (pmt.Status IN ('Paid','FNF DONE') OR pmt.AmountPaid > 0)
          )
      AND (@MinAgeingDays IS NULL
           OR (lp.LastPunchDt IS NOT NULL AND sep.SeparationDate IS NOT NULL
               AND DATEDIFF(DAY, lp.LastPunchDt, CAST(sep.SeparationDate AS date)) >= @MinAgeingDays))
    ORDER BY [L.PUNCH VS. SEPERATION AGEING] DESC, l.STCode, e.ECode;

    SET NOCOUNT OFF;
END;

GO


GO
-- ================= usp_LastPunchAfterSeparationGapReport =================
CREATE   PROCEDURE dbo.usp_LastPunchAfterSeparationGapReport
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
      AND NOT EXISTS (                                -- exclude F&F Completed (Pending/Processing stay)
            SELECT 1 FROM dbo.FNF_Header h WITH (NOLOCK)
            JOIN dbo.FNF_Payment pmt WITH (NOLOCK) ON pmt.FNFId = h.FNFId
            WHERE h.EmployeeId = e.EmployeeId
              AND (pmt.Status IN ('Paid','FNF DONE') OR pmt.AmountPaid > 0)
          )
      AND lp.LastPunchDt IS NOT NULL
      AND sep.SeparationDate IS NOT NULL
      AND lp.LastPunchDt > CAST(sep.SeparationDate AS date)   -- last punch AFTER separation (the gap)
      AND NOT EXISTS (SELECT 1 FROM dbo.tblLocation lx WITH (NOLOCK) WHERE lx.STCode = e.ECode)
    ORDER BY [PUNCH AGEING AFTER SEPERATION] DESC, l.STCode, e.ECode;

    SET NOCOUNT OFF;
END;

GO


GO
-- ================= usp_SeparatedLastPunchMissingGapReport =================
CREATE   PROCEDURE dbo.usp_SeparatedLastPunchMissingGapReport
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
      AND NOT EXISTS (                                -- exclude F&F Completed (Pending/Processing stay)
            SELECT 1 FROM dbo.FNF_Header h WITH (NOLOCK)
            JOIN dbo.FNF_Payment pmt WITH (NOLOCK) ON pmt.FNFId = h.FNFId
            WHERE h.EmployeeId = e.EmployeeId
              AND (pmt.Status IN ('Paid','FNF DONE') OR pmt.AmountPaid > 0)
          )
    ORDER BY [SEPERATION DATE] DESC, l.STCode, e.ECode;

    SET NOCOUNT OFF;
END;

GO


GO
-- ================= usp_SeparatedResignationMissingGapReport =================
CREATE   PROCEDURE dbo.usp_SeparatedResignationMissingGapReport
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
      AND NOT EXISTS (                                -- exclude F&F Completed (Pending/Processing stay)
            SELECT 1 FROM dbo.FNF_Header h WITH (NOLOCK)
            JOIN dbo.FNF_Payment pmt WITH (NOLOCK) ON pmt.FNFId = h.FNFId
            WHERE h.EmployeeId = e.EmployeeId
              AND (pmt.Status IN ('Paid','FNF DONE') OR pmt.AmountPaid > 0)
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


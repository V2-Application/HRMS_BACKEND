CREATE PROCEDURE dbo.usp_LastPunchVsSeparationGapReport
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
        CAST(COALESCE(sep.SeparationDate, e.UpdatedOn, e.DateOfLeft) AS date) AS [SEPERATION DATE],
        lp.LastPunchDt AS [L.PUNCH DATE],
        CASE WHEN lp.LastPunchDt IS NULL OR COALESCE(sep.SeparationDate, e.UpdatedOn, e.DateOfLeft) IS NULL THEN NULL
             ELSE DATEDIFF(DAY, lp.LastPunchDt, CAST(COALESCE(sep.SeparationDate, e.UpdatedOn, e.DateOfLeft) AS date)) END AS [L.PUNCH VS. SEPERATION AGEING]
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
    WHERE e.IsActive = 0 AND e.ECode LIKE 'V%' AND NOT EXISTS (SELECT 1 FROM dbo.tblDesignation dx WITH (NOLOCK) WHERE dx.DesignationId = e.DesignationId AND dx.DesignationName LIKE '%NAPS%') AND YEAR(COALESCE(sep.SeparationDate, e.UpdatedOn, e.DateOfLeft)) = 2026                              -- separated (from Employee Master)
      AND NOT EXISTS (SELECT 1 FROM dbo.tblLocation lx WITH (NOLOCK) WHERE lx.STCode = e.ECode)
      AND (@MinAgeingDays IS NULL
           OR (lp.LastPunchDt IS NOT NULL AND sep.SeparationDate IS NOT NULL
               AND DATEDIFF(DAY, lp.LastPunchDt, CAST(COALESCE(sep.SeparationDate, e.UpdatedOn, e.DateOfLeft) AS date)) >= @MinAgeingDays))
    ORDER BY [L.PUNCH VS. SEPERATION AGEING] DESC, l.STCode, e.ECode;

    SET NOCOUNT OFF;
END;


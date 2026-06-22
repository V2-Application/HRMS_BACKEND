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
      AND NOT EXISTS (
            SELECT 1 FROM dbo.FNF_Header h WITH (NOLOCK)
            JOIN dbo.FNF_Payment pmt WITH (NOLOCK) ON pmt.FNFId = h.FNFId
            WHERE h.EmployeeId = e.EmployeeId
              AND (pmt.Status IN ('Paid','FNF DONE') OR pmt.AmountPaid > 0)
          )
    ORDER BY [SEPERATION AGEING] DESC, l.STCode, e.ECode;

    SET NOCOUNT OFF;
END;

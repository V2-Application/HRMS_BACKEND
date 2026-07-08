CREATE OR ALTER PROCEDURE dbo.usp_LocEmpLeaveAvailedGapReport
    @AsOfDate DATE = NULL    -- defaults to YESTERDAY; the report covers the pay cycle through this date
AS
BEGIN
    SET NOCOUNT ON;

    /*
      LOC-EMP LEAVE AVAILED > 4 DAYS GAP REPORT  (read-only)

      One row per employee whose LEAVE AVAILED days in the current pay cycle through @AsOfDate exceed 4.

      Pay cycle = 26th of previous month .. 25th of selected month, derived from the SELECTED DATE
      (@AsOfDate) -- same rule as the other gap reports:
          DAY(@AsOfDate) >= 26  -> cycle = next month   (e.g. 26-Jun-2026 -> Jul-26 cycle)
          DAY(@AsOfDate) <  26  -> cycle = current month (e.g. 24-Jun-2026 -> Jun-26 cycle)
      Counts are cycle-to-date: CycleStart .. @AsOfDate.

      Leave availed = number of days flagged IsOnLeave = 1 in the materialized
      tbl_fn_GetMonthlyPunchesRange_productionnewnick_test (derived from approved leave requests).
    */

    DECLARE @ToDate     DATE = ISNULL(@AsOfDate, DATEADD(DAY, -1, CAST(GETDATE() AS DATE)));
    DECLARE @CycleStart DATE =
        CASE WHEN DAY(@ToDate) >= 26
             THEN DATEFROMPARTS(YEAR(@ToDate), MONTH(@ToDate), 26)
             ELSE DATEADD(MONTH, -1, DATEFROMPARTS(YEAR(@ToDate), MONTH(@ToDate), 26))
        END;

    IF OBJECT_ID('tempdb..#Emp') IS NOT NULL DROP TABLE #Emp;
    CREATE TABLE #Emp(
        EmployeeId BIGINT NOT NULL PRIMARY KEY, ECode NVARCHAR(50) NOT NULL,
        LocationId INT NULL, DepartmentId INT NULL, DesignationId INT NULL,
        SubDepartmentId1 INT NULL, EmployeeName NVARCHAR(255) NULL
    );
    INSERT INTO #Emp(EmployeeId,ECode,LocationId,DepartmentId,DesignationId,SubDepartmentId1,EmployeeName)
    SELECT e.EmployeeId, e.ECode, e.LocationId, e.DepartmentId, e.DesignationId,
           e.SubDepartmentId1, e.[FULL NAME]
    FROM dbo.tblEmployee e WITH (NOLOCK)
    WHERE e.IsActive = 1 AND e.ECode IS NOT NULL;
    CREATE INDEX IX_Emp_ECode ON #Emp(ECode);

    -- Leave-availed days per employee for the cycle window.
    IF OBJECT_ID('tempdb..#Lv') IS NOT NULL DROP TABLE #Lv;
    CREATE TABLE #Lv(EmployeeId BIGINT NOT NULL PRIMARY KEY, LeaveDays INT NOT NULL);
    INSERT INTO #Lv(EmployeeId, LeaveDays)
    SELECT e.EmployeeId, COUNT(*)
    FROM #Emp e
    JOIN dbo.tbl_fn_GetMonthlyPunchesRange_productionnewnick_test t WITH (NOLOCK)
      ON t.ECode = e.ECode
     AND CONVERT(date, t.AttendanceDate) BETWEEN @CycleStart AND @ToDate
     AND t.IsOnLeave = 1
    GROUP BY e.EmployeeId;

    SELECT
        l.STCode       AS [LOC CD],
        l.LocationName AS [LOC NM],
        CASE
            WHEN l.STCode = 'RH01' OR UPPER(LTRIM(RTRIM(l.LocationName))) LIKE 'HO-NEW%' THEN 'HO new'
            WHEN l.STCode = 'RD04' OR UPPER(LTRIM(RTRIM(l.LocationName))) LIKE 'HO-OLD%' THEN 'Old HO'
            WHEN l.STCode = 'RH02' OR UPPER(LTRIM(RTRIM(l.LocationName))) LIKE '%CENTRAL%' THEN 'Central'
            WHEN l.STCode IN ('DW01','DH24') THEN 'DC'
            WHEN UPPER(LTRIM(RTRIM(l.LocationName))) LIKE '%DC%' OR UPPER(LTRIM(RTRIM(l.LocationName))) LIKE '%HUB%' THEN 'Hub'
            ELSE 'Store'
        END            AS [LOC TYPE],
        CAST(NULL AS varchar(50))  AS [STATS OLD/NEW],   -- manual column, per template
        e.ECode        AS [EMP CODE],
        e.EmployeeName AS [EMP NM],
        d.DepartmentName      AS [DEPT.],
        sd1.SubDepartmentName AS [SUB.-DEPT.],
        dg.DesignationName    AS [DESGN.],
        v.LeaveDays           AS [TOTAL LEAVE AVAILED > 4 DAYS],
        CAST(NULL AS varchar(255)) AS [RCA],
        CAST(NULL AS varchar(255)) AS [ATR],
        CAST(NULL AS varchar(255)) AS [HR REMARKS]
    FROM #Emp e
    JOIN #Lv v ON v.EmployeeId = e.EmployeeId AND v.LeaveDays > 4
    LEFT JOIN dbo.tblLocation l        WITH (NOLOCK) ON l.LocationId     = e.LocationId
    LEFT JOIN dbo.tblDepartment d      WITH (NOLOCK) ON d.DepartmentId   = e.DepartmentId
    LEFT JOIN dbo.tblDesignation dg    WITH (NOLOCK) ON dg.DesignationId = e.DesignationId
    LEFT JOIN dbo.tblSubDepartment sd1 WITH (NOLOCK) ON sd1.SubDepartmentId = e.SubDepartmentId1
    ORDER BY l.STCode, e.ECode;

    SET NOCOUNT OFF;
END;

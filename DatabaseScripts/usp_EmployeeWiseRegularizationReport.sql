CREATE OR ALTER PROCEDURE dbo.usp_EmployeeWiseRegularizationReport
    @AsOfDate DATE = NULL    -- defaults to YESTERDAY ("today's" report uses data through yesterday)
AS
BEGIN
    SET NOCOUNT ON;

    /*
      LOC-EMP TD/MTD REGULARIZATION GAP REPORT  (read-only)
      One row per employee with at least one MANUAL-PUNCH attendance day in the current pay cycle
      (materialized Status in 'Manual Present' / 'MP' / 'Manual Punch' / 'Manual Present Half Day').
        LOC CD / LOC NM / LOC TYPE / LOC STATUS (Active|UPC)
        EMP CODE / EMP NM / DEPT. / SUB.-DEPT. / DESGN.
        ACT EMP.  = active employees at the location
        TD        = manual-punch days for @AsOfDate (yesterday)
        MTD       = manual-punch days for the current pay cycle (26th-of-prev .. 25th) through @AsOfDate
      NOTE: manual-present in the materialized table is applied with a lag (it trails the live punch
      data), so the most-recent dates may show 0 until the table refreshes.
      Only employees with MTD > 0 are returned (gap report).
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

    IF OBJECT_ID('tempdb..#Reg') IS NOT NULL DROP TABLE #Reg;
    CREATE TABLE #Reg(EmployeeId BIGINT NOT NULL PRIMARY KEY, TD INT NOT NULL, MTD INT NOT NULL);
    INSERT INTO #Reg(EmployeeId, TD, MTD)
    SELECT e.EmployeeId,
           SUM(CASE WHEN CONVERT(date,t.AttendanceDate) = @ToDate THEN 1 ELSE 0 END),
           COUNT(*)
    FROM #Emp e
    JOIN dbo.tbl_fn_GetMonthlyPunchesRange_productionnewnick_test t WITH (NOLOCK)
      ON t.ECode = e.ECode
     AND CONVERT(date,t.AttendanceDate) BETWEEN @CycleStart AND @ToDate
     AND t.Status IN (N'Manual Present', N'MP', N'Manual Punch', N'Manual Present Half Day')
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
        CASE WHEN l.IsActive = 1 THEN 'Active' ELSE 'UPC' END AS [LOC STATUS],
        e.ECode        AS [EMP CODE],
        e.EmployeeName AS [EMP NM],
        d.DepartmentName      AS [DEPT.],
        sd1.SubDepartmentName AS [SUB.-DEPT.],
        dg.DesignationName    AS [DESGN.],
        ISNULL(ae.ActEmp, 0)  AS [ACT EMP.],
        m.TD  AS [TD],
        m.MTD AS [MTD]
    FROM #Emp e
    JOIN #Reg m ON m.EmployeeId = e.EmployeeId AND m.MTD > 0
    LEFT JOIN dbo.tblLocation l       WITH (NOLOCK) ON l.LocationId   = e.LocationId
    LEFT JOIN dbo.tblDepartment d     WITH (NOLOCK) ON d.DepartmentId = e.DepartmentId
    LEFT JOIN dbo.tblDesignation dg   WITH (NOLOCK) ON dg.DesignationId = e.DesignationId
    LEFT JOIN dbo.tblSubDepartment sd1 WITH (NOLOCK) ON sd1.SubDepartmentId = e.SubDepartmentId1
    LEFT JOIN (SELECT LocationId, COUNT(*) AS ActEmp FROM dbo.tblEmployee WITH (NOLOCK) WHERE IsActive = 1 GROUP BY LocationId) ae
           ON ae.LocationId = e.LocationId
    ORDER BY l.STCode, e.ECode;

    SET NOCOUNT OFF;
END;

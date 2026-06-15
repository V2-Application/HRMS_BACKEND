CREATE OR ALTER PROCEDURE dbo.usp_EmployeeWiseGeofenceReport
    @AsOfDate DATE = NULL    -- defaults to YESTERDAY ("today's" report uses data through yesterday)
AS
BEGIN
    SET NOCOUNT ON;

    /*
      LOC-EMP TD/MTD GEO-FENCING GAP REPORT  (read-only)
      One row per employee with at least one geo-fence attendance day (materialized Status='GF')
      in the current pay-cycle window.
        LOC CD / LOC NM / LOC TYPE / LOC STATUS (Active|UPC)
        EMP CODE / EMP NM / DEPT. / SUB.-DEPT. 1/2/3 / DESGN.
        TD  = geo-fence days for @AsOfDate (yesterday)
        MTD = geo-fence days for the current pay cycle (26th-of-prev .. 25th) through @AsOfDate
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
        SubDepartmentId1 INT NULL, SubDepartmentId2 INT NULL, SubDepartmentId3 INT NULL,
        EmployeeName NVARCHAR(255) NULL
    );
    INSERT INTO #Emp(EmployeeId,ECode,LocationId,DepartmentId,DesignationId,SubDepartmentId1,SubDepartmentId2,SubDepartmentId3,EmployeeName)
    SELECT e.EmployeeId, e.ECode, e.LocationId, e.DepartmentId, e.DesignationId,
           e.SubDepartmentId1, e.SubDepartmentId2, e.SubDepartmentId3, e.[FULL NAME]
    FROM dbo.tblEmployee e WITH (NOLOCK)
    WHERE e.IsActive = 1 AND e.ECode IS NOT NULL;
    CREATE INDEX IX_Emp_ECode ON #Emp(ECode);

    IF OBJECT_ID('tempdb..#Geo') IS NOT NULL DROP TABLE #Geo;
    CREATE TABLE #Geo(EmployeeId BIGINT NOT NULL PRIMARY KEY, TD INT NOT NULL, MTD INT NOT NULL);
    INSERT INTO #Geo(EmployeeId, TD, MTD)
    SELECT e.EmployeeId,
           SUM(CASE WHEN CONVERT(date,t.AttendanceDate) = @ToDate THEN 1 ELSE 0 END),
           COUNT(*)
    FROM #Emp e
    JOIN dbo.tbl_fn_GetMonthlyPunchesRange_productionnewnick_test t WITH (NOLOCK)
      ON t.ECode = e.ECode
     AND CONVERT(date,t.AttendanceDate) BETWEEN @CycleStart AND @ToDate
     AND t.Status = N'GF'
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
        sd1.SubDepartmentName AS [SUB.-DEPT. 1],
        sd2.SubDepartmentName AS [SUB.-DEPT. 2],
        sd3.SubDepartmentName AS [SUB.-DEPT. 3],
        dg.DesignationName    AS [DESGN.],
        m.TD  AS [TD],
        m.MTD AS [MTD]
    FROM #Emp e
    JOIN #Geo m ON m.EmployeeId = e.EmployeeId AND m.MTD > 0
    LEFT JOIN dbo.tblLocation l       WITH (NOLOCK) ON l.LocationId   = e.LocationId
    LEFT JOIN dbo.tblDepartment d     WITH (NOLOCK) ON d.DepartmentId = e.DepartmentId
    LEFT JOIN dbo.tblDesignation dg   WITH (NOLOCK) ON dg.DesignationId = e.DesignationId
    LEFT JOIN dbo.tblSubDepartment sd1 WITH (NOLOCK) ON sd1.SubDepartmentId = e.SubDepartmentId1
    LEFT JOIN dbo.tblSubDepartment sd2 WITH (NOLOCK) ON sd2.SubDepartmentId = e.SubDepartmentId2
    LEFT JOIN dbo.tblSubDepartment sd3 WITH (NOLOCK) ON sd3.SubDepartmentId = e.SubDepartmentId3
    ORDER BY l.STCode, e.ECode;

    SET NOCOUNT OFF;
END;

CREATE OR ALTER PROCEDURE dbo.usp_LocationWiseRegularizationReport
    @AsOfDate DATE = NULL    -- defaults to YESTERDAY ("today's" report uses data through yesterday)
AS
BEGIN
    SET NOCOUNT ON;

    /*
      LOC-WISE TD/MTD REGULARIZATION GAP REPORT  (read-only)
      One row per location. Regularization = MANUAL-PUNCH attendance from the materialized table
      (Status in 'Manual Present' / 'MP' / 'Manual Punch' / 'Manual Present Half Day').
        LOC CD / LOC NM / LOC TYPE / LOC STATUS (Active|UPC) / ACT EMP (all active emp at the location)
        TD  = manual-punch days at the store for @AsOfDate (yesterday)
        MTD = manual-punch days for the current pay cycle (26th-of-prev .. 25th) through @AsOfDate
      NOTE: manual-present in the materialized table is applied with a lag (it trails the live punch
      data), so the most-recent dates may show 0 until the table refreshes.
      Returns ALL locations.
    */

    DECLARE @ToDate     DATE = ISNULL(@AsOfDate, DATEADD(DAY, -1, CAST(GETDATE() AS DATE)));
    DECLARE @CycleStart DATE =
        CASE WHEN DAY(@ToDate) >= 26
             THEN DATEFROMPARTS(YEAR(@ToDate), MONTH(@ToDate), 26)
             ELSE DATEADD(MONTH, -1, DATEFROMPARTS(YEAR(@ToDate), MONTH(@ToDate), 26))
        END;

    IF OBJECT_ID('tempdb..#Emp') IS NOT NULL DROP TABLE #Emp;
    CREATE TABLE #Emp(EmployeeId BIGINT NOT NULL PRIMARY KEY, ECode NVARCHAR(50) NOT NULL, LocationId INT NULL);
    INSERT INTO #Emp(EmployeeId, ECode, LocationId)
    SELECT e.EmployeeId, e.ECode, e.LocationId
    FROM dbo.tblEmployee e WITH (NOLOCK)
    WHERE e.IsActive = 1 AND e.ECode IS NOT NULL;
    CREATE INDEX IX_Emp_ECode ON #Emp(ECode);

    IF OBJECT_ID('tempdb..#Reg') IS NOT NULL DROP TABLE #Reg;
    CREATE TABLE #Reg(LocationId INT NOT NULL PRIMARY KEY, TD INT NOT NULL, MTD INT NOT NULL);
    INSERT INTO #Reg(LocationId, TD, MTD)
    SELECT e.LocationId,
           SUM(CASE WHEN CONVERT(date,t.AttendanceDate) = @ToDate THEN 1 ELSE 0 END),
           COUNT(*)
    FROM #Emp e
    JOIN dbo.tbl_fn_GetMonthlyPunchesRange_productionnewnick_test t WITH (NOLOCK)
      ON t.ECode = e.ECode
     AND CONVERT(date,t.AttendanceDate) BETWEEN @CycleStart AND @ToDate
     AND t.Status IN (N'Manual Present', N'MP', N'Manual Punch', N'Manual Present Half Day')
    WHERE e.LocationId IS NOT NULL
    GROUP BY e.LocationId;

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
        ISNULL(ae.ActEmp, 0) AS [ACT EMP],
        ISNULL(g.TD, 0)      AS [TD],
        ISNULL(g.MTD, 0)     AS [MTD]
    FROM dbo.tblLocation l WITH (NOLOCK)
    LEFT JOIN (SELECT LocationId, COUNT(*) AS ActEmp FROM dbo.tblEmployee WITH (NOLOCK) WHERE IsActive = 1 GROUP BY LocationId) ae
           ON ae.LocationId = l.LocationId
    LEFT JOIN #Reg g ON g.LocationId = l.LocationId
    ORDER BY l.STCode;

    SET NOCOUNT OFF;
END;

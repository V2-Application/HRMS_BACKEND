CREATE OR ALTER PROCEDURE dbo.usp_LocationWiseAbscondingReport
    @AsOfDate  DATE = NULL,    -- report "as of" date; defaults to today
    @BatchSize INT  = 8000     -- kept for signature compatibility (no longer used)
AS
BEGIN
    SET NOCOUNT ON;

    /*
      LOC-WISE ABSCONDING REPORT  (read-only; marks/changes nothing)
      One row per location:
        LOC CODE  = tblLocation.STCode
        LOC NM    = tblLocation.LocationName
        LOC TYPE  = name-based: HO-NEW/RH01 -> 'HO new'; HO-OLD/RD04 -> 'Old HO';
                    RH02 / name 'CENTRAL' -> 'Central'; STCode DW01/DH24 -> 'DC';
                    name has 'DC'(incl RDC) or 'HUB' -> 'Hub'; everything else -> 'Store'
        ACT EMP   = ALL active employees (tblEmployee.IsActive=1) mapped to that location
        TD        = of those, how many qualify as ABSCONDING as of @AsOfDate via the live rule:
                    STCode 'RH01' -> 7-day window, all other stores -> 6-day window;
                    Sat/Sun/weekly-offs/holidays count as absent; absconding only when there is
                    NO attendance evidence on ANY day in the trailing window, the employee is not
                    on approved/pending leave during it, the full window is covered by attendance
                    rows, and they were active for the whole window (DOJ on/before window start).
      Set-based (single pass over the materialized attendance table) so it stays fast.
      Returns ALL locations (active + inactive); ACT EMP / TD are 0 where none apply.
    */

    DECLARE @ToDate DATE = ISNULL(@AsOfDate, CAST(GETDATE() AS DATE));

    /* 0) active employees (+ per-store window) */
    IF OBJECT_ID('tempdb..#ActiveEmp') IS NOT NULL DROP TABLE #ActiveEmp;
    CREATE TABLE #ActiveEmp(
        EmployeeId  BIGINT       NOT NULL PRIMARY KEY,
        ECode       NVARCHAR(50) NOT NULL,
        LocationId  INT          NULL,
        DOJ         DATE         NULL,
        WindowDays  INT          NOT NULL,
        WindowStart DATE         NOT NULL
    );

    INSERT INTO #ActiveEmp(EmployeeId, ECode, LocationId, DOJ, WindowDays, WindowStart)
    SELECT e.EmployeeId, e.ECode, e.LocationId, CAST(e.DOJ AS DATE),
           CASE WHEN l.STCode = 'RH01' THEN 7 ELSE 6 END,
           DATEADD(DAY, -((CASE WHEN l.STCode = 'RH01' THEN 7 ELSE 6 END) - 1), @ToDate)
    FROM dbo.tblEmployee e WITH (NOLOCK)
    LEFT JOIN dbo.tblLocation l WITH (NOLOCK) ON l.LocationId = e.LocationId
    WHERE e.IsActive = 1 AND e.ECode IS NOT NULL;

    CREATE INDEX IX_ActiveEmp_ECode ON #ActiveEmp(ECode);

    /* 1) per-employee attendance facts within their own trailing window (single set-based pass) */
    IF OBJECT_ID('tempdb..#Agg') IS NOT NULL DROP TABLE #Agg;
    CREATE TABLE #Agg(EmployeeId BIGINT NOT NULL PRIMARY KEY, DaysCovered INT NOT NULL, SeenDays INT NOT NULL);

    INSERT INTO #Agg(EmployeeId, DaysCovered, SeenDays)
    SELECT a.EmployeeId,
           COUNT(DISTINCT CONVERT(date, t.AttendanceDate)),
           SUM(CASE WHEN t.Status IN (N'Present',N'Manual Present',N'Manual Present Half Day',N'Half Day Absent',N'Mispunch',N'GF')
                      OR ISNULL(t.ValidPunchCount,0) > 0 OR t.IsRegularize = 1 THEN 1 ELSE 0 END)
    FROM #ActiveEmp a
    JOIN dbo.tbl_fn_GetMonthlyPunchesRange_productionnewnick_test t WITH (NOLOCK)
      ON t.ECode = a.ECode
     AND CONVERT(date, t.AttendanceDate) BETWEEN a.WindowStart AND @ToDate
    GROUP BY a.EmployeeId;

    /* 2) would-abscond set: no evidence, full window coverage, joined before window, not on leave */
    IF OBJECT_ID('tempdb..#Abscond') IS NOT NULL DROP TABLE #Abscond;
    CREATE TABLE #Abscond(EmployeeId BIGINT NOT NULL PRIMARY KEY, LocationId INT NULL);

    INSERT INTO #Abscond(EmployeeId, LocationId)
    SELECT a.EmployeeId, a.LocationId
    FROM #ActiveEmp a
    LEFT JOIN #Agg g ON g.EmployeeId = a.EmployeeId
    WHERE ISNULL(g.SeenDays, 0) = 0
      AND ISNULL(g.DaysCovered, 0) = a.WindowDays
      AND (a.DOJ IS NULL OR a.DOJ <= a.WindowStart)
      AND NOT EXISTS (
            SELECT 1 FROM dbo.tblLeaveRequest lr WITH (NOLOCK)
            WHERE lr.EmployeeId = a.EmployeeId AND lr.StatusId IN (1,2) AND lr.IsRevoked = 0
              AND lr.StartDate <= @ToDate AND lr.EndDate >= a.WindowStart);

    /* 3) location-wise rollup (all locations) */
    SELECT
        l.STCode       AS [LOC CODE],
        l.LocationName AS [LOC NM],
        CASE
            WHEN l.STCode = 'RH01' OR UPPER(LTRIM(RTRIM(l.LocationName))) LIKE 'HO-NEW%' THEN 'HO new'
            WHEN l.STCode = 'RD04' OR UPPER(LTRIM(RTRIM(l.LocationName))) LIKE 'HO-OLD%' THEN 'Old HO'
            WHEN l.STCode = 'RH02' OR UPPER(LTRIM(RTRIM(l.LocationName))) LIKE '%CENTRAL%' THEN 'Central'
            WHEN l.STCode IN ('DW01','DH24') THEN 'DC'
            WHEN UPPER(LTRIM(RTRIM(l.LocationName))) LIKE '%DC%' OR UPPER(LTRIM(RTRIM(l.LocationName))) LIKE '%HUB%' THEN 'Hub'
            ELSE 'Store'
        END            AS [LOC TYPE],
        ISNULL(ae.ActEmp, 0) AS [ACT EMP],
        ISNULL(ab.TD, 0)     AS [TD]
    FROM dbo.tblLocation l WITH (NOLOCK)
    LEFT JOIN (SELECT LocationId, COUNT(*) AS ActEmp FROM dbo.tblEmployee WITH (NOLOCK) WHERE IsActive = 1 GROUP BY LocationId) ae
           ON ae.LocationId = l.LocationId
    LEFT JOIN (SELECT LocationId, COUNT(*) AS TD FROM #Abscond GROUP BY LocationId) ab
           ON ab.LocationId = l.LocationId
    ORDER BY l.STCode;

    SET NOCOUNT OFF;
END;

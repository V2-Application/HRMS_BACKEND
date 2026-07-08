CREATE OR ALTER PROCEDURE dbo.usp_LocationWiseAbsentReport
    @AsOfDate DATE = NULL    -- defaults to YESTERDAY ("today's" report uses data through yesterday)
AS
BEGIN
    SET NOCOUNT ON;

    /*
      LOC._ABSENT TD/MTD GAP REPORT  (read-only)
      One row per location:
        LOC CD / LOC NM / LOC TYPE / LOC STATUS
        ACT EMP.   = ALL active employees mapped to that location
        TD         = ABSENT days at the store for @AsOfDate (yesterday)
        MTD        = ABSENT days for the current pay cycle through @AsOfDate
        RCA / ATR / HR REMARKS -> blank manual columns
      Only full-day 'Absent'/'On Leave' is counted (Half/Quarter Day Absent are NOT included).
      DOJ rule: days BEFORE an employee's date-of-joining are NOT counted as absent (joins on the
      15th -> 1st..14th not counted).
      Pay cycle = 26th of prev month .. 25th of current; cycle start = most recent 26th on/before @AsOfDate.
      Returns ALL locations. Marks/changes nothing.
    */

    DECLARE @ToDate     DATE = ISNULL(@AsOfDate, DATEADD(DAY, -1, CAST(GETDATE() AS DATE)));
    DECLARE @CycleStart DATE =
        CASE WHEN DAY(@ToDate) >= 26
             THEN DATEFROMPARTS(YEAR(@ToDate), MONTH(@ToDate), 26)
             ELSE DATEADD(MONTH, -1, DATEFROMPARTS(YEAR(@ToDate), MONTH(@ToDate), 26))
        END;

    IF OBJECT_ID('tempdb..#Emp') IS NOT NULL DROP TABLE #Emp;
    CREATE TABLE #Emp(EmployeeId BIGINT NOT NULL PRIMARY KEY, ECode NVARCHAR(50) NOT NULL, LocationId INT NULL, DOJ DATE NULL);
    INSERT INTO #Emp(EmployeeId, ECode, LocationId, DOJ)
    SELECT e.EmployeeId, e.ECode, e.LocationId, TRY_CONVERT(date, e.[DOJ])
    FROM dbo.tblEmployee e WITH (NOLOCK)
    WHERE e.IsActive = 1 AND e.ECode IS NOT NULL;
    CREATE INDEX IX_Emp_ECode ON #Emp(ECode);

    IF OBJECT_ID('tempdb..#Abs') IS NOT NULL DROP TABLE #Abs;
    CREATE TABLE #Abs(LocationId INT NOT NULL PRIMARY KEY, TD INT NOT NULL, MTD INT NOT NULL);
    INSERT INTO #Abs(LocationId, TD, MTD)
    SELECT e.LocationId,
           SUM(CASE WHEN CONVERT(date,t.AttendanceDate) = @ToDate THEN 1 ELSE 0 END),
           COUNT(*)
    FROM #Emp e
    JOIN dbo.tbl_fn_GetMonthlyPunchesRange_productionnewnick_test t WITH (NOLOCK)
      ON t.ECode = e.ECode
     AND CONVERT(date,t.AttendanceDate) BETWEEN @CycleStart AND @ToDate
     AND t.Status IN (N'Absent', N'On Leave')
     AND (e.DOJ IS NULL OR CONVERT(date,t.AttendanceDate) >= e.DOJ)   -- not absent before joining
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
        ISNULL(ae.ActEmp, 0) AS [ACT EMP.],
        ISNULL(a.TD, 0)      AS [TD],
        ISNULL(a.MTD, 0)     AS [MTD],
        CAST(NULL AS varchar(200)) AS [RCA],
        CAST(NULL AS varchar(200)) AS [ATR],
        CAST(NULL AS varchar(200)) AS [HR REMARKS]
    FROM dbo.tblLocation l WITH (NOLOCK)
    LEFT JOIN (SELECT LocationId, COUNT(*) AS ActEmp FROM dbo.tblEmployee WITH (NOLOCK) WHERE IsActive = 1 GROUP BY LocationId) ae
           ON ae.LocationId = l.LocationId
    LEFT JOIN #Abs a ON a.LocationId = l.LocationId
    ORDER BY l.STCode;

    SET NOCOUNT OFF;
END;

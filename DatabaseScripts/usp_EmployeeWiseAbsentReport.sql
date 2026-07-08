CREATE OR ALTER PROCEDURE dbo.usp_EmployeeWiseAbsentReport
    @AsOfDate DATE = NULL    -- defaults to YESTERDAY ("today's" report uses data through yesterday)
AS
BEGIN
    SET NOCOUNT ON;

    /*
      LOC_EMP._TD/MTD ABSENT GAP REPORT  (read-only)
      One row per ACTIVE employee who has at least one full-day ABSENT in the current pay cycle:
        LOC CD / LOC NM / LOC TYPE / STATS OLD/NEW
        EMP CODE / EMP NM / DEPT. / SUB.-DEPT. / DESGN.
        D.O.J         = tblEmployee.[DOJ] (date of joining)
        TD            = full-day ABSENT on @AsOfDate (yesterday) -> 1 or 0
        MTD           = full-day ABSENT count for the current pay cycle through @AsOfDate
        RCA / ATR / HR REMARKS -> blank manual columns
      Only full-day 'Absent'/'On Leave' is counted (Half/Quarter Day Absent are NOT included).
      DOJ rule: days BEFORE an employee's date-of-joining are NOT counted as absent (the employee
      had not joined yet) -- e.g. joins on the 15th -> 1st..14th are not counted.
      Pay cycle = 26th of prev month .. 25th of current; cycle start = most recent 26th on/before @AsOfDate.
      Store-login / system accounts are EXCLUDED. Only employees with MTD absent > 0 are returned.
    */

    DECLARE @ToDate     DATE = ISNULL(@AsOfDate, DATEADD(DAY, -1, CAST(GETDATE() AS DATE)));
    DECLARE @CycleStart DATE =
        CASE WHEN DAY(@ToDate) >= 26
             THEN DATEFROMPARTS(YEAR(@ToDate), MONTH(@ToDate), 26)
             ELSE DATEADD(MONTH, -1, DATEFROMPARTS(YEAR(@ToDate), MONTH(@ToDate), 26))
        END;

    -- active employees (exclude store-login accounts where ECode is actually a store code)
    IF OBJECT_ID('tempdb..#Emp') IS NOT NULL DROP TABLE #Emp;
    CREATE TABLE #Emp(EmployeeId BIGINT NOT NULL PRIMARY KEY, ECode NVARCHAR(50) NOT NULL, DOJ DATE NULL);
    INSERT INTO #Emp(EmployeeId, ECode, DOJ)
    SELECT e.EmployeeId, e.ECode, TRY_CONVERT(date, e.[DOJ])
    FROM dbo.tblEmployee e WITH (NOLOCK)
    WHERE e.IsActive = 1 AND e.ECode IS NOT NULL
      AND e.[FULL NAME] IS NOT NULL AND LTRIM(RTRIM(e.[FULL NAME])) <> '' AND e.[FULL NAME] <> e.ECode
      AND NOT EXISTS (SELECT 1 FROM dbo.tblLocation lx WITH (NOLOCK) WHERE lx.STCode = e.ECode);
    CREATE INDEX IX_Emp_ECode ON #Emp(ECode);

    -- full-day ABSENT TD / MTD per employee (days before DOJ are excluded)
    IF OBJECT_ID('tempdb..#Abs') IS NOT NULL DROP TABLE #Abs;
    CREATE TABLE #Abs(EmployeeId BIGINT NOT NULL PRIMARY KEY, TD INT NOT NULL, MTD INT NOT NULL);
    INSERT INTO #Abs(EmployeeId, TD, MTD)
    SELECT e.EmployeeId,
           SUM(CASE WHEN CONVERT(date,t.AttendanceDate) = @ToDate THEN 1 ELSE 0 END),
           COUNT(*)
    FROM #Emp e
    JOIN dbo.tbl_fn_GetMonthlyPunchesRange_productionnewnick_test t WITH (NOLOCK)
      ON t.ECode = e.ECode
     AND CONVERT(date,t.AttendanceDate) BETWEEN @CycleStart AND @ToDate
     AND t.Status IN (N'Absent', N'On Leave')
     AND (e.DOJ IS NULL OR CONVERT(date,t.AttendanceDate) >= e.DOJ)   -- not absent before joining
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
        CAST(NULL AS varchar(50))  AS [STATS OLD/NEW],
        e.ECode                    AS [EMP CODE],
        e.[FULL NAME]              AS [EMP NM],
        d.DepartmentName           AS [DEPT.],
        sd1.SubDepartmentName      AS [SUB.-DEPT.],
        dg.DesignationName         AS [DESGN.],
        TRY_CONVERT(date, e.[DOJ]) AS [D.O.J],
        a.TD                       AS [TD],
        a.MTD                      AS [MTD],
        CAST(NULL AS varchar(200)) AS [RCA],
        CAST(NULL AS varchar(200)) AS [ATR],
        CAST(NULL AS varchar(200)) AS [HR REMARKS]
    FROM #Abs a
    JOIN dbo.tblEmployee e WITH (NOLOCK) ON e.EmployeeId = a.EmployeeId
    LEFT JOIN dbo.tblLocation l        WITH (NOLOCK) ON l.LocationId       = e.LocationId
    LEFT JOIN dbo.tblDepartment d      WITH (NOLOCK) ON d.DepartmentId     = e.DepartmentId
    LEFT JOIN dbo.tblDesignation dg    WITH (NOLOCK) ON dg.DesignationId   = e.DesignationId
    LEFT JOIN dbo.tblSubDepartment sd1 WITH (NOLOCK) ON sd1.SubDepartmentId = e.SubDepartmentId1
    WHERE a.MTD > 0
    ORDER BY l.STCode, e.ECode;

    SET NOCOUNT OFF;
END;

CREATE OR ALTER PROCEDURE dbo.usp_LocationWiseActEmpVsAttendanceReport
    @AsOfDate DATE = NULL    -- the TD date; defaults to YESTERDAY (data through yesterday)
AS
BEGIN
    SET NOCOUNT ON;

    /*
      LOC._ACT EMP. VS. ACT ATTEND. GAP REPORT  (read-only, TD / single day)
      One row per location:
        LOC CD      = tblLocation.STCode
        LOC NM      = tblLocation.LocationName
        LOC TYPE    = name-based (HO new / Old HO / Central / DC(DW01,DH24) / Hub / Store)
        LOC STATUS  = 'Active' if tblLocation.IsActive=1 else 'UPC'
        ACT EMP.    = ALL active employees (tblEmployee.IsActive=1) mapped to that location
        ACT ATTEND. = active employees who ACTUALLY ATTENDED on @AsOfDate (punched / present-type
                      status: Present, GF, Manual Present, MIS, Half/Quarter Day Absent)
        DIFF.       = ACT EMP. - ACT ATTEND.  (active employees who did NOT attend that day)
        RCA / ATR / HR REMARKS -> left blank (manual follow-up columns, per the template)
      Attendance is counted per the employee's CURRENT location, so DIFF is always >= 0.
      "Today's" download uses data THROUGH YESTERDAY (@AsOfDate defaults to GETDATE()-1).
      Returns ALL locations. Marks/changes nothing.
    */

    DECLARE @ToDate DATE = ISNULL(@AsOfDate, DATEADD(DAY, -1, CAST(GETDATE() AS DATE)));

    IF OBJECT_ID('tempdb..#Emp') IS NOT NULL DROP TABLE #Emp;
    CREATE TABLE #Emp(EmployeeId BIGINT NOT NULL PRIMARY KEY, ECode NVARCHAR(50) NOT NULL, LocationId INT NULL);
    INSERT INTO #Emp(EmployeeId, ECode, LocationId)
    SELECT e.EmployeeId, e.ECode, e.LocationId
    FROM dbo.tblEmployee e WITH (NOLOCK)
    WHERE e.IsActive = 1 AND e.ECode IS NOT NULL;
    CREATE INDEX IX_Emp_ECode ON #Emp(ECode);

    -- distinct active employees who ATTENDED on @ToDate, by their current location
    IF OBJECT_ID('tempdb..#Att') IS NOT NULL DROP TABLE #Att;
    CREATE TABLE #Att(LocationId INT NOT NULL PRIMARY KEY, AttendCnt INT NOT NULL);
    INSERT INTO #Att(LocationId, AttendCnt)
    SELECT e.LocationId, COUNT(DISTINCT e.EmployeeId)
    FROM #Emp e
    JOIN dbo.tbl_fn_GetMonthlyPunchesRange_productionnewnick_test t WITH (NOLOCK)
      ON t.ECode = e.ECode
     AND CONVERT(date,t.AttendanceDate) = @ToDate
     AND t.Status IN (N'Present', N'GF', N'Manual Present', N'MIS', N'Half Day Absent', N'Quarter Day Absent')
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
        ISNULL(ae.ActEmp, 0)        AS [ACT EMP.],
        ISNULL(att.AttendCnt, 0)    AS [ACT ATTEND.],
        ISNULL(ae.ActEmp, 0) - ISNULL(att.AttendCnt, 0) AS [DIFF.],
        CAST(NULL AS varchar(200)) AS [RCA],
        CAST(NULL AS varchar(200)) AS [ATR],
        CAST(NULL AS varchar(200)) AS [HR REMARKS]
    FROM dbo.tblLocation l WITH (NOLOCK)
    LEFT JOIN (SELECT LocationId, COUNT(*) AS ActEmp FROM dbo.tblEmployee WITH (NOLOCK) WHERE IsActive = 1 GROUP BY LocationId) ae
           ON ae.LocationId = l.LocationId
    LEFT JOIN #Att att ON att.LocationId = l.LocationId
    ORDER BY l.STCode;

    SET NOCOUNT OFF;
END;

CREATE   PROCEDURE dbo.usp_EmpLocChangeLMvsTMGapReport
    @AsOfDate DATE = NULL    -- defaults to YESTERDAY; the cycle/month is derived from this date
AS
BEGIN
    SET NOCOUNT ON;

    /*
      EMP LOC CHANGE LM vs TM GAP REPORT  (read-only)

      One row per active employee whose WORK LOCATION changed between:
          LM = LAST month  (the month before the cycle month)
          TM = THIS month  (the pay-cycle month derived from @AsOfDate)
      i.e. the employee's location code in the LM snapshot differs from the TM snapshot.

      Pay-cycle month is derived from the SELECTED DATE (@AsOfDate), same rule as the other reports:
          DAY(@AsOfDate) >= 26  -> cycle month = next month   (e.g. 26-Jun-2026 -> Jul-26)
          DAY(@AsOfDate) <  26  -> cycle month = current month (e.g. 24-Jun-2026 -> Jun-26)

      Location per month is taken from dbo.EmpAttendanceViewSnapshot (Location_Code / [Location Name])
      for the MONTH label (e.g. 'Jun-26'); the LATEST run per employee+month is used (highest ID).
    */

    DECLARE @ToDate DATE = ISNULL(@AsOfDate, DATEADD(DAY, -1, CAST(GETDATE() AS DATE)));

    DECLARE @TMFirst DATE =
        CASE WHEN DAY(@ToDate) >= 26
             THEN DATEADD(MONTH, 1, DATEFROMPARTS(YEAR(@ToDate), MONTH(@ToDate), 1))
             ELSE DATEFROMPARTS(YEAR(@ToDate), MONTH(@ToDate), 1)
        END;
    DECLARE @LMFirst DATE = DATEADD(MONTH, -1, @TMFirst);

    DECLARE @TM_Month varchar(7) = LEFT(DATENAME(MONTH, @TMFirst), 3) + '-' + RIGHT(CONVERT(varchar, YEAR(@TMFirst)), 2);
    DECLARE @LM_Month varchar(7) = LEFT(DATENAME(MONTH, @LMFirst), 3) + '-' + RIGHT(CONVERT(varchar, YEAR(@LMFirst)), 2);

    IF OBJECT_ID('tempdb..#Emp') IS NOT NULL DROP TABLE #Emp;
    CREATE TABLE #Emp(
        EmployeeId BIGINT NOT NULL PRIMARY KEY, ECode NVARCHAR(50) NOT NULL,
        DepartmentId INT NULL, DesignationId INT NULL, SubDepartmentId1 INT NULL,
        EmployeeName NVARCHAR(255) NULL, DOJ DATE NULL
    );
    INSERT INTO #Emp(EmployeeId,ECode,DepartmentId,DesignationId,SubDepartmentId1,EmployeeName,DOJ)
    SELECT e.EmployeeId, e.ECode, e.DepartmentId, e.DesignationId, e.SubDepartmentId1,
           e.[FULL NAME], TRY_CONVERT(date, e.[DOJ])
    FROM dbo.tblEmployee e WITH (NOLOCK)
    WHERE e.IsActive = 1 AND e.ECode IS NOT NULL;
    CREATE INDEX IX_Emp_ECode ON #Emp(ECode);

    -- Latest location per employee for THIS month and LAST month.
    IF OBJECT_ID('tempdb..#TM') IS NOT NULL DROP TABLE #TM;
    SELECT ECode, LocCode, LocName
    INTO #TM
    FROM (
        SELECT s.Ecode AS ECode, s.Location_Code AS LocCode, s.[Location Name] AS LocName,
               ROW_NUMBER() OVER (PARTITION BY s.Ecode ORDER BY s.ID DESC) AS rn
        FROM dbo.EmpAttendanceViewSnapshot s WITH (NOLOCK)
        WHERE s.MONTH = @TM_Month
    ) q WHERE rn = 1;
    CREATE INDEX IX_TM ON #TM(ECode);

    IF OBJECT_ID('tempdb..#LM') IS NOT NULL DROP TABLE #LM;
    SELECT ECode, LocCode, LocName
    INTO #LM
    FROM (
        SELECT s.Ecode AS ECode, s.Location_Code AS LocCode, s.[Location Name] AS LocName,
               ROW_NUMBER() OVER (PARTITION BY s.Ecode ORDER BY s.ID DESC) AS rn
        FROM dbo.EmpAttendanceViewSnapshot s WITH (NOLOCK)
        WHERE s.MONTH = @LM_Month
    ) q WHERE rn = 1;
    CREATE INDEX IX_LM ON #LM(ECode);

    SELECT
        e.ECode        AS [EMP CODE],
        e.EmployeeName AS [EMP NM],
        d.DepartmentName      AS [DEPT.],
        sd1.SubDepartmentName AS [SUB.-DEPT.],
        dg.DesignationName    AS [DESGN.],
        e.DOJ                 AS [D.O.J],
        lm.LocCode AS [LM LOC CD],
        lm.LocName AS [LM LOC NM],
        CASE
            WHEN lm.LocCode = 'RH01' OR UPPER(LTRIM(RTRIM(lm.LocName))) LIKE 'HO-NEW%' THEN 'HO new'
            WHEN lm.LocCode = 'RD04' OR UPPER(LTRIM(RTRIM(lm.LocName))) LIKE 'HO-OLD%' THEN 'Old HO'
            WHEN lm.LocCode = 'RH02' OR UPPER(LTRIM(RTRIM(lm.LocName))) LIKE '%CENTRAL%' THEN 'Central'
            WHEN lm.LocCode IN ('DW01','DH24') THEN 'DC'
            WHEN UPPER(LTRIM(RTRIM(lm.LocName))) LIKE '%DC%' OR UPPER(LTRIM(RTRIM(lm.LocName))) LIKE '%HUB%' THEN 'Hub'
            ELSE 'Store'
        END AS [LM LOC TYPE],
        tm.LocCode AS [TM LOC CD],
        tm.LocName AS [TM LOC NM],
        CASE
            WHEN tm.LocCode = 'RH01' OR UPPER(LTRIM(RTRIM(tm.LocName))) LIKE 'HO-NEW%' THEN 'HO new'
            WHEN tm.LocCode = 'RD04' OR UPPER(LTRIM(RTRIM(tm.LocName))) LIKE 'HO-OLD%' THEN 'Old HO'
            WHEN tm.LocCode = 'RH02' OR UPPER(LTRIM(RTRIM(tm.LocName))) LIKE '%CENTRAL%' THEN 'Central'
            WHEN tm.LocCode IN ('DW01','DH24') THEN 'DC'
            WHEN UPPER(LTRIM(RTRIM(tm.LocName))) LIKE '%DC%' OR UPPER(LTRIM(RTRIM(tm.LocName))) LIKE '%HUB%' THEN 'Hub'
            ELSE 'Store'
        END AS [TM LOC TYPE],
        'LOC CHANGE'               AS [VAR. LM VS. CM],
        CAST(NULL AS varchar(255)) AS [RCA],
        CAST(NULL AS varchar(255)) AS [ATR],
        CAST(NULL AS varchar(255)) AS [HR REMARKS]
    FROM #Emp e
    JOIN #LM lm ON lm.ECode = e.ECode
    JOIN #TM tm ON tm.ECode = e.ECode
    LEFT JOIN dbo.tblDepartment d      WITH (NOLOCK) ON d.DepartmentId   = e.DepartmentId
    LEFT JOIN dbo.tblDesignation dg    WITH (NOLOCK) ON dg.DesignationId = e.DesignationId
    LEFT JOIN dbo.tblSubDepartment sd1 WITH (NOLOCK) ON sd1.SubDepartmentId = e.SubDepartmentId1
    WHERE ISNULL(NULLIF(LTRIM(RTRIM(lm.LocCode)), ''), '~') <> ISNULL(NULLIF(LTRIM(RTRIM(tm.LocCode)), ''), '~')
    ORDER BY e.ECode;

    SET NOCOUNT OFF;
END;


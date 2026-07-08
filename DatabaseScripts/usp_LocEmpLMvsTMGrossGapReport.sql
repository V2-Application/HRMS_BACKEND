CREATE OR ALTER PROCEDURE dbo.usp_LocEmpLMvsTMGrossGapReport
    @AsOfDate DATE = NULL    -- defaults to YESTERDAY; the cycle/month is derived from this date
AS
BEGIN
    SET NOCOUNT ON;

    /*
      LOC-EMP LM vs TM GROSS SALARY GAP REPORT  (read-only)

      Compares each active employee's BUDGETED monthly gross salary between:
          TM = THIS month  (the pay-cycle month derived from @AsOfDate)
          LM = LAST month  (the month before TM)
      GAP = LM - TM. One row per employee where both months have a value and they differ.

      Pay-cycle month is derived from the SELECTED DATE (@AsOfDate), same rule as the other reports:
          DAY(@AsOfDate) >= 26  -> cycle month = next month   (e.g. 26-Jun-2026 -> Jul-26)
          DAY(@AsOfDate) <  26  -> cycle month = current month (e.g. 24-Jun-2026 -> Jun-26)

      Source: dbo.EmpAttendanceViewSnapshot.[Monthly Gross CTC(Bud.)] for the MONTH label
      (e.g. 'Jun-26'); the LATEST run per employee+month is used (highest ID).
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
        LocationId INT NULL, DepartmentId INT NULL, DesignationId INT NULL,
        SubDepartmentId1 INT NULL, EmployeeName NVARCHAR(255) NULL
    );
    INSERT INTO #Emp(EmployeeId,ECode,LocationId,DepartmentId,DesignationId,SubDepartmentId1,EmployeeName)
    SELECT e.EmployeeId, e.ECode, e.LocationId, e.DepartmentId, e.DesignationId,
           e.SubDepartmentId1, e.[FULL NAME]
    FROM dbo.tblEmployee e WITH (NOLOCK)
    WHERE e.IsActive = 1 AND e.ECode IS NOT NULL;
    CREATE INDEX IX_Emp_ECode ON #Emp(ECode);

    -- Latest budgeted monthly gross per employee for THIS month and LAST month.
    IF OBJECT_ID('tempdb..#TM') IS NOT NULL DROP TABLE #TM;
    SELECT ECode, Gross
    INTO #TM
    FROM (
        SELECT s.Ecode AS ECode, s.[Monthly Gross CTC(Bud.)] AS Gross,
               ROW_NUMBER() OVER (PARTITION BY s.Ecode ORDER BY s.ID DESC) AS rn
        FROM dbo.EmpAttendanceViewSnapshot s WITH (NOLOCK)
        WHERE s.MONTH = @TM_Month
    ) q WHERE rn = 1;
    CREATE INDEX IX_TM ON #TM(ECode);

    IF OBJECT_ID('tempdb..#LM') IS NOT NULL DROP TABLE #LM;
    SELECT ECode, Gross
    INTO #LM
    FROM (
        SELECT s.Ecode AS ECode, s.[Monthly Gross CTC(Bud.)] AS Gross,
               ROW_NUMBER() OVER (PARTITION BY s.Ecode ORDER BY s.ID DESC) AS rn
        FROM dbo.EmpAttendanceViewSnapshot s WITH (NOLOCK)
        WHERE s.MONTH = @LM_Month
    ) q WHERE rn = 1;
    CREATE INDEX IX_LM ON #LM(ECode);

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
        lm.Gross              AS [LM],
        tm.Gross              AS [TM],
        (lm.Gross - tm.Gross) AS [GAP],
        CAST(NULL AS varchar(255)) AS [RCA],
        CAST(NULL AS varchar(255)) AS [ATR],
        CAST(NULL AS varchar(255)) AS [HR REMARKS]
    FROM #Emp e
    JOIN #LM lm ON lm.ECode = e.ECode
    JOIN #TM tm ON tm.ECode = e.ECode
    LEFT JOIN dbo.tblLocation l        WITH (NOLOCK) ON l.LocationId     = e.LocationId
    LEFT JOIN dbo.tblDepartment d      WITH (NOLOCK) ON d.DepartmentId   = e.DepartmentId
    LEFT JOIN dbo.tblDesignation dg    WITH (NOLOCK) ON dg.DesignationId = e.DesignationId
    LEFT JOIN dbo.tblSubDepartment sd1 WITH (NOLOCK) ON sd1.SubDepartmentId = e.SubDepartmentId1
    WHERE ISNULL(lm.Gross, 0) <> ISNULL(tm.Gross, 0)
    ORDER BY l.STCode, e.ECode;

    SET NOCOUNT OFF;
END;

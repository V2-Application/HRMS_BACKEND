-- PROD variant: optimized list proc preserving PROD's original behavior
-- (PROD's original did NOT filter IsDeleted/IsStore, unlike dev). Counts verified identical to pre-change.
-- TRIM: @Mode='mainview' enriches only ACTIVE + ABSCONDED (ResignationTypeId=10) rows (active tabs only).
-- TUNE: SARGable CreatedBy/UpdatedBy joins + set-based month-year (no FORMAT()).
-- Counts (#Filtered) stay over the FULL set -> badges unchanged. No base-table data is modified.
CREATE OR ALTER PROCEDURE [dbo].[GetEmployeeDetailsWithCards_Test]
    @PageNumber INT = 0,
    @PageSize INT = 0,
    @SearchTerm NVARCHAR(100) = '',
    @Mode NVARCHAR(10) = 'all',
    @ManagerId NVARCHAR(10) = '',
    @TotalEmployees INT OUTPUT,
    @CurrentPageNumber INT OUTPUT,
    @ActiveCount INT OUTPUT,
    @InactiveCount INT OUTPUT,
    @AbscondCount INT OUTPUT,
    @LocCount INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @MECode  NVARCHAR(10) = NULL;
    DECLARE @IsStore BIT = NULL;
    DECLARE @ApplyFilter BIT = 0;

    IF NULLIF(@ManagerId, '') IS NOT NULL AND @ManagerId <> ''
    BEGIN
        SELECT @MECode = Ecode, @IsStore = IsStore
        FROM tblEmployee WITH (NOLOCK)
        WHERE EmployeeId = TRY_CONVERT(BIGINT, @ManagerId);

        IF (ISNULL(@MECode, '') <> '' AND @IsStore IS NOT NULL)
            SET @ApplyFilter = 1;
    END

    ----------------------------------------------------------------------
    -- 1) Lightweight filtered key set (used for counts + pagination)
    --    NOTE: PROD keeps the original (no IsDeleted/IsStore filter) so the
    --          count cards match prod's pre-optimization values exactly.
    ----------------------------------------------------------------------
    SELECT e.EmployeeId, e.IsActive, l.STCode
    INTO #Filtered
    FROM tblEmployee e WITH (NOLOCK)
    LEFT JOIN tblDepartment  d  WITH (NOLOCK) ON d.DepartmentId  = e.DepartmentId
    LEFT JOIN tblDesignation dg WITH (NOLOCK) ON dg.DesignationId = e.DesignationId
    LEFT JOIN tblLocation    l  WITH (NOLOCK) ON l.LocationId     = e.LocationId
    WHERE
        (@SearchTerm = ''
         OR e.[FULL NAME] LIKE '%' + @SearchTerm + '%'
         OR e.Ecode = @SearchTerm
         OR d.DepartmentName LIKE '%' + @SearchTerm + '%'
         OR dg.DesignationName LIKE '%' + @SearchTerm + '%'
         OR l.LocationName LIKE '%' + @SearchTerm + '%'
         OR l.STCode LIKE '%' + @SearchTerm + '%')
        AND (
            @ApplyFilter = 0
            OR (@IsStore = 1 AND l.STCode = @MECode)
            OR (@IsStore = 0 AND e.ReportHeadEcode = @MECode)
        )
    OPTION (RECOMPILE);

    ----------------------------------------------------------------------
    -- 2) Count cards (always over the FULL filtered set -> badges unchanged)
    ----------------------------------------------------------------------
    SELECT @TotalEmployees = COUNT(*) FROM #Filtered;
    SELECT @ActiveCount    = COUNT(*) FROM #Filtered WHERE IsActive = 1;
    SELECT @InactiveCount  = COUNT(*) FROM #Filtered WHERE IsActive = 0;
    SELECT @LocCount       = COUNT(DISTINCT STCode) FROM #Filtered WHERE IsActive = 1;
    SELECT @AbscondCount   = COUNT(*)
    FROM #Filtered f
    INNER JOIN tblEmployeeSepration b WITH (NOLOCK) ON f.EmployeeId = b.EmployeeId
    WHERE b.ResignationTypeId = 10;

    SET @CurrentPageNumber = @PageNumber;

    ----------------------------------------------------------------------
    -- 3) EmployeeIds to ENRICH (#PageEmp).
    --    @Mode='mainview' -> only ACTIVE + ABSCONDED rows (active tabs only).
    --    Any other @Mode  -> unchanged behaviour (full set / page slice).
    ----------------------------------------------------------------------
    CREATE TABLE #PageEmp (EmployeeId BIGINT NOT NULL PRIMARY KEY);

    ;WITH src AS (
        SELECT f.EmployeeId
        FROM #Filtered f
        WHERE @Mode <> 'mainview'
           OR f.IsActive = 1
           OR EXISTS (SELECT 1 FROM tblEmployeeSepration s WITH (NOLOCK)
                      WHERE s.EmployeeId = f.EmployeeId AND s.ResignationTypeId = 10)
    )
    INSERT INTO #PageEmp (EmployeeId)
    SELECT EmployeeId FROM src
    ORDER BY EmployeeId DESC
    OFFSET (CASE WHEN @PageNumber > 0 AND @PageSize > 0 THEN (@PageNumber - 1) * @PageSize ELSE 0 END) ROWS
    FETCH NEXT (CASE WHEN @PageSize > 0 THEN @PageSize ELSE 2147483647 END) ROWS ONLY;

    ----------------------------------------------------------------------
    -- 4) Enrich ONLY the page (experience CTE limited to the page) + output
    ----------------------------------------------------------------------
    ;WITH exp AS (
        SELECT a.Ecode, b.[Name of Company] AS CompanyName, b.[From] AS FromDate, b.[To] AS ToDate,
               CAST(DATEDIFF(DAY, b.[From], ISNULL(b.[To], GETDATE())) / 365.25 AS DECIMAL(10,2)) AS YearsDec
        FROM tblEmployee (NOLOCK) a
        INNER JOIN #PageEmp pe ON pe.EmployeeId = a.EmployeeId
        LEFT JOIN tblExperience (NOLOCK) b ON b.CID = a.CandidateId
        WHERE a.IsDeleted = 0 AND a.IsActive = 1
    ),
    ranked AS (
        SELECT Ecode, CompanyName, FromDate, ToDate, YearsDec,
               ROW_NUMBER() OVER (PARTITION BY Ecode ORDER BY FromDate DESC, ToDate DESC) AS rn
        FROM exp
    ),
    finalResult AS (
        SELECT Ecode,
            MAX(CASE WHEN rn = 1 THEN CompanyName END) AS [COMPANY NAME-1],
            MAX(CASE WHEN rn = 1 THEN UPPER(LEFT(DATENAME(MONTH, FromDate),3)) + '-' + CONVERT(varchar(4), YEAR(FromDate)) END) AS [From-I],
            MAX(CASE WHEN rn = 1 THEN ISNULL(UPPER(LEFT(DATENAME(MONTH, ToDate),3)) + '-' + CONVERT(varchar(4), YEAR(ToDate)), 'PRESENT') END) AS [To-I],
            MAX(CASE WHEN rn = 1 THEN YearsDec END) AS [YEARS-1],
            MAX(CASE WHEN rn = 2 THEN CompanyName END) AS [COMPANY NAME-2],
            MAX(CASE WHEN rn = 2 THEN UPPER(LEFT(DATENAME(MONTH, FromDate),3)) + '-' + CONVERT(varchar(4), YEAR(FromDate)) END) AS [From-II],
            MAX(CASE WHEN rn = 2 THEN ISNULL(UPPER(LEFT(DATENAME(MONTH, ToDate),3)) + '-' + CONVERT(varchar(4), YEAR(ToDate)), 'PRESENT') END) AS [To-II],
            MAX(CASE WHEN rn = 2 THEN YearsDec END) AS [YEARS-2],
            MAX(CASE WHEN rn = 3 THEN CompanyName END) AS [COMPANY NAME-3],
            MAX(CASE WHEN rn = 3 THEN UPPER(LEFT(DATENAME(MONTH, FromDate),3)) + '-' + CONVERT(varchar(4), YEAR(FromDate)) END) AS [From-III],
            MAX(CASE WHEN rn = 3 THEN ISNULL(UPPER(LEFT(DATENAME(MONTH, ToDate),3)) + '-' + CONVERT(varchar(4), YEAR(ToDate)), 'PRESENT') END) AS [To-III],
            MAX(CASE WHEN rn = 3 THEN YearsDec END) AS [YEARS-3],
            CAST(SUM(YearsDec) AS DECIMAL(10,2)) AS [TTL EXPERIENCE]
        FROM ranked WHERE rn <= 3 GROUP BY Ecode
    )
    SELECT
        CASE WHEN l.STCode='RH01' THEN ISNULL(eZone.Zone,'-')    ELSE zone.ZoneName  END AS ZoneName,
        CASE WHEN l.STCode='RH01' THEN ISNULL(eZone.Region,'-')  ELSE reg.RegionName END AS RegionName,
        CASE WHEN l.STCode='RH01' THEN ISNULL(eZone.Cluster,'-') ELSE cl.ClusterName END AS ClusterName,
        l.STCode,
        l.LocationName,
        e.Ecode,
        e.[FULL NAME] AS FullName,
        e.GENDER AS Gender,
        e.DOB,
        CAST(DATEDIFF(DAY, e.DOB, GETDATE()) / 365.25 AS DECIMAL(10,2)) AS AgeInYears,
        e.DepartmentId,
        e.DesignationId,
        d.DepartmentName,
        dg.DesignationName,
        e.DOJ,
        trt.ResignationTypeName,
        e.DateOfLeft,
        e.[BANK NAME],
        e.[A/C NO],
        e.[BANK IFSC CODE],
        e.[PERMANENT ADDRESS],
        e.[PERMANENT ADDRESS PIN CODE],
        e.[PRESENT ADDRESS],
        e.[PRESENT ADDRESS PIN CODE],
        e.MOBILE,
        e.[EMAIL ADDRESS],
        e.[AADHAR NO],
        e.[PAN NO],
        e.[HIGHEST QUALIFICATION],
        e.[FATHER'S NAME],
        e.[MOTHER'S NAME],
        e.[MARITIAL STATUS],
        e.ReportHeadEcode,
        report.[FULL NAME] AS ReportHeadFullName,
        reportdg.DesignationName AS ReportHeadDesignation,
        fr.[COMPANY NAME-1],
        fr.[From-I],
        fr.[To-I],
        fr.[YEARS-1],
        fr.[COMPANY NAME-2],
        fr.[From-II],
        fr.[To-II],
        fr.[YEARS-2],
        fr.[COMPANY NAME-3],
        fr.[From-III],
        fr.[To-III],
        fr.[YEARS-3],
        fr.[TTL EXPERIENCE],
        l.IsActive AS LocStatus,
        CASE
            WHEN e.IsActive = 0 AND trt.ResignationTypeId = 8 THEN 'Abscond'
            WHEN e.IsActive = 0 AND e.DateOfLeft IS NOT NULL
                 AND DATEDIFF(DAY, CAST(e.DateOfLeft AS date), CAST(GETDATE() AS date)) BETWEEN 0 AND 30 THEN 'Inactive'
            WHEN e.IsActive = 0 AND e.DateOfLeft IS NOT NULL
                 AND DATEDIFF(DAY, CAST(e.DateOfLeft AS date), CAST(GETDATE() AS date)) > 30 THEN 'Left'
            WHEN e.IsActive = 0 AND e.DateOfLeft IS NULL THEN 'Left'
            ELSE 'Active'
        END AS EmployeeStatus,
        e.EmployeeId,
        e.CandidateId,
        'E-'+l.STCode+'-'+TRY_CAST(d.DepartmentId AS VARCHAR(50))+'-'+TRY_CAST(dg.DesignationId AS VARCHAR(50))+'-'+
        CASE
            WHEN e.CompanyId = 1 THEN RIGHT(e.Ecode, LEN(e.Ecode) - 1)
            WHEN e.CompanyId = 2 THEN RIGHT(e.Ecode, LEN(e.Ecode) - 3)
            WHEN e.CompanyId = 3 THEN RIGHT(e.Ecode, LEN(e.Ecode) - 2)
            ELSE e.Ecode
        END AS [LocBasedECode],
        e.IsActive,
        e.IsDeleted,
        e.DOJ AS DateOfJoining,
        e.IsStore,
        e.CreatedOn,
        e.UpdatedOn,
        c.[FULL NAME] +' ('+c.Ecode+')' AS CreatedBy,
        u.[FULL NAME] +' ('+u.Ecode+')' AS UpdatedBy
    FROM tblEmployee (NOLOCK) e
    INNER JOIN #PageEmp pe ON pe.EmployeeId = e.EmployeeId
    LEFT JOIN tblDepartment  (NOLOCK) d   ON d.DepartmentId  = e.DepartmentId
    LEFT JOIN tblDesignation (NOLOCK) dg  ON dg.DesignationId = e.DesignationId
    LEFT JOIN tblLocation    (NOLOCK) l   ON l.LocationId     = e.LocationId
    LEFT JOIN tblRegion      (NOLOCK) reg ON l.RegionId       = reg.RegionId
    LEFT JOIN tblZone        (NOLOCK) zone ON l.ZoneId        = zone.Id
    LEFT JOIN Cluster        (NOLOCK) cl  ON l.ClusterId      = cl.Id
    LEFT JOIN (
        SELECT Ecode, MAX(Zone) AS Zone, MAX(Region) AS Region, MAX(Cluster) AS Cluster
        FROM EcodeZoneRegionClusterMapping WITH (NOLOCK) GROUP BY Ecode
    ) eZone ON eZone.Ecode = e.Ecode
    LEFT JOIN (
        SELECT EmployeeId, MAX(ResignationTypeId) AS ResignationTypeId
        FROM tblEmployeeSepration WITH (NOLOCK) GROUP BY EmployeeId
    ) sep ON sep.EmployeeId = e.EmployeeId
    LEFT JOIN tblResignationType (NOLOCK) trt ON trt.ResignationTypeId = sep.ResignationTypeId
    LEFT JOIN tblEmployee    (NOLOCK) u   ON u.EmployeeId = TRY_CAST(e.UpdatedBy AS BIGINT)
    LEFT JOIN tblEmployee    (NOLOCK) c   ON c.EmployeeId = TRY_CAST(e.CreatedBy AS BIGINT)
    LEFT JOIN tblEmployee    (NOLOCK) report ON e.ReportHeadEcode = report.Ecode
    LEFT JOIN tblDesignation (NOLOCK) reportdg ON reportdg.DesignationId = report.DesignationId
    LEFT JOIN finalResult fr ON fr.Ecode = e.Ecode
    ORDER BY e.EmployeeId DESC
    OPTION (RECOMPILE);
END;

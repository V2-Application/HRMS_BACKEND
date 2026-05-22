CREATE PROCEDURE [dbo].[GetEmployeeDetailsWithCards_Test]        
    @PageNumber INT = 0,                  
    @PageSize INT = 0,                    
    @SearchTerm NVARCHAR(100) = '',       
    @Mode NVARCHAR(10) = 'all',       
    @ManagerId Nvarchar(10) = '',    
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
    
    IF NULLIF(@ManagerId, '') IS NOT NULL and @ManagerId<>''    
    BEGIN    
        -- If EmployeeId is numeric in your table, use TRY_CONVERT    
        SELECT    
            @MECode  = Ecode,    
            @IsStore = IsStore    
        FROM tblEmployee WITH (NOLOCK)    
        WHERE EmployeeId = TRY_CONVERT(BIGINT, @ManagerId);    
    
        -- Apply filter only when we actually found a manager row with both values    
        IF (ISNULL(@MECode, '') <> '' AND @IsStore IS NOT NULL)    
            SET @ApplyFilter = 1;    
    END
    -------------------------------------------------------------------      
    -- Table variable: ALL columns in final order (used by both outputs)      
    ----------------------------------------------------------------------      
    DECLARE @FilteredEmployees TABLE (        
        ZoneName NVARCHAR(50),      
        RegionName NVARCHAR(50),      
        ClusterName NVARCHAR(50),      
        STCode NVARCHAR(50),        
        LocationName NVARCHAR(100),        
        Ecode NVARCHAR(50),        
        FullName NVARCHAR(100),      
        -- Added personal/HR fields you asked for      
        Gender NVARCHAR(50),      
        DOB DATE,      
        AgeInYears DECIMAL(10,2),      
        DepartmentId int,        
        DesignationId int,       
        DepartmentName NVARCHAR(100),        
        DesignationName NVARCHAR(100),        
        DOJ DATE,      
        ResignationTypeName NVARCHAR(200),      
        DateOfLeft DATE,      
        [BANK NAME] NVARCHAR(200),      
        [A/C NO] NVARCHAR(50),      
        [BANK IFSC CODE] NVARCHAR(50),      
        [PERMANENT ADDRESS] NVARCHAR(500),      
        [PERMANENT ADDRESS PIN CODE] NVARCHAR(20),      
        [PRESENT ADDRESS] NVARCHAR(500),      
        [PRESENT ADDRESS PIN CODE] NVARCHAR(20),      
        MOBILE NVARCHAR(50),      
        [EMAIL ADDRESS] NVARCHAR(200),      
        [AADHAR NO] NVARCHAR(50),      
        [PAN NO] NVARCHAR(50),      
        [HIGHEST QUALIFICATION] NVARCHAR(200),      
        [FATHER'S NAME] NVARCHAR(200),      
        [MOTHER'S NAME] NVARCHAR(200),      
        [MARITIAL STATUS] NVARCHAR(50),      
        ReportHeadEcode NVARCHAR(50),      
        ReportHeadFullName NVARCHAR(100),      
        ReportHeadDesignation NVARCHAR(100),      
        -- Experience columns (3 companies + total)      
        [COMPANY NAME-1] NVARCHAR(300),      
        [From-I] NVARCHAR(10),      
        [To-I] NVARCHAR(10),      
        [YEARS-1] DECIMAL(10,2),      
        [COMPANY NAME-2] NVARCHAR(300),      
        [From-II] NVARCHAR(10),      
        [To-II] NVARCHAR(10),      
        [YEARS-2] DECIMAL(10,2),      
        [COMPANY NAME-3] NVARCHAR(300),      
        [From-III] NVARCHAR(10),      
        [To-III] NVARCHAR(10),      
        [YEARS-3] DECIMAL(10,2),      
        [TTL EXPERIENCE] DECIMAL(10,2),      
        LocStatus bit,      
        EmployeeStatus nvarchar(100),      
        -- trailing IDs & audit (to match your previous output pattern)      
        EmployeeId BIGINT,      
        CandidateId BIGINT,        
        LocBasedECode NVARCHAR(100),        
        IsActive BIT,        
        IsDeleted BIT,        
        DateOfJoining DATETIME,        
        IsStore BIT,        
        CreatedOn DATETIME,        
        UpdatedOn DATETIME,        
        CreatedBy NVARCHAR(50),        
        UpdatedBy NVARCHAR(50)      
    );        
      
    ----------------------------------------------------------------------      
    -- Experience CTEs (latest 3 roles)      
    ----------------------------------------------------------------------      
    ;WITH exp AS (      
        SELECT      
            a.Ecode,      
            b.[Name of Company] AS CompanyName,      
            b.[From] AS FromDate,      
            b.[To]   AS ToDate,      
            CAST(      
                DATEDIFF(DAY, b.[From], ISNULL(b.[To], GETDATE())) / 365.25      
                AS DECIMAL(10,2)      
            ) AS YearsDec      
        FROM tblEmployee (NOLOCK) a      
        LEFT JOIN tblExperience  (NOLOCK) b      
            ON b.CID = a.CandidateId      
        WHERE a.IsDeleted = 0 AND a.IsActive = 1      
    ),      
    ranked AS (      
        SELECT      
            Ecode,      
            CompanyName,      
            FromDate,      
            ToDate,      
            YearsDec,      
            ROW_NUMBER() OVER (      
                PARTITION BY Ecode      
            ORDER BY FromDate DESC, ToDate DESC      
            ) AS rn      
        FROM exp      
    ),      
    finalResult AS (      
        SELECT      
            Ecode,      
            MAX(CASE WHEN rn = 1 THEN CompanyName END)                                  AS [COMPANY NAME-1],      
            MAX(CASE WHEN rn = 1 THEN UPPER(FORMAT(FromDate, 'MMM-yyyy')) END)          AS [From-I],      
            MAX(CASE WHEN rn = 1 THEN ISNULL(UPPER(FORMAT(ToDate, 'MMM-yyyy')), 'PRESENT') END) AS [To-I],      
            MAX(CASE WHEN rn = 1 THEN YearsDec END)                                     AS [YEARS-1],      
            MAX(CASE WHEN rn = 2 THEN CompanyName END)                                  AS [COMPANY NAME-2],      
            MAX(CASE WHEN rn = 2 THEN UPPER(FORMAT(FromDate, 'MMM-yyyy')) END)          AS [From-II],      
            MAX(CASE WHEN rn = 2 THEN ISNULL(UPPER(FORMAT(ToDate, 'MMM-yyyy')), 'PRESENT') END) AS [To-II],      
            MAX(CASE WHEN rn = 2 THEN YearsDec END)                                     AS [YEARS-2],      
            MAX(CASE WHEN rn = 3 THEN CompanyName END)                                  AS [COMPANY NAME-3],      
            MAX(CASE WHEN rn = 3 THEN UPPER(FORMAT(FromDate, 'MMM-yyyy')) END)          AS [From-III],      
            MAX(CASE WHEN rn = 3 THEN ISNULL(UPPER(FORMAT(ToDate, 'MMM-yyyy')), 'PRESENT') END) AS [To-III],      
            MAX(CASE WHEN rn = 3 THEN YearsDec END)                                     AS [YEARS-3],      
            CAST(SUM(YearsDec) AS DECIMAL(10,2))                                        AS [TTL EXPERIENCE]      
        FROM ranked      
        WHERE rn <= 3      
        GROUP BY Ecode      
    )      
      
    ----------------------------------------------------------------------      
    -- Insert in the same order as the table definition      
    ----------------------------------------------------------------------      
    INSERT INTO @FilteredEmployees      
    SELECT DISTINCT
        CASE WHEN l.STCode='RH01' THEN isnull(eZone.Zone,'-')    ELSE zone.ZoneName   END as ZoneName,      
        CASE WHEN l.STCode='RH01' THEN isnull(eZone.Region,'-')  ELSE reg.RegionName  END as RegionName,      
        CASE WHEN l.STCode='RH01' THEN isnull(eZone.Cluster,'-') ELSE cl.ClusterName  END as ClusterName,      
        l.STCode,        
        l.LocationName,        
        e.Ecode,        
        e.[FULL NAME] AS FullName,      
        -- Added personal/HR fields      
        e.GENDER,      
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
        -- Experience columns      
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
        l.IsActive LocStatus,      
        CASE      
            WHEN e.IsActive = 0 AND trt.ResignationTypeId = 8 THEN 'Abscond'      
            WHEN e.IsActive = 0 AND e.DateOfLeft IS NOT NULL      
                 AND DATEDIFF(DAY, CAST(e.DateOfLeft AS date), CAST(GETDATE() AS date)) BETWEEN 0 AND 30 THEN 'Inactive'      
            WHEN e.IsActive = 0 AND e.DateOfLeft IS NOT NULL      
                 AND DATEDIFF(DAY, CAST(e.DateOfLeft AS date), CAST(GETDATE() AS date)) > 30 THEN 'Left'      
            WHEN e.IsActive = 0 AND e.DateOfLeft IS NULL THEN 'Left'      
            ELSE 'Active'      
        END AS EmployeeStatus,      
        -- trailing IDs & audit      
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
        c.[FULL NAME] +' ('+c.Ecode+')' as CreatedBy,        
        u.[FULL NAME] +' ('+u.Ecode+')' as UpdatedBy      
    FROM  tblEmployee             (NOLOCK) e        
    LEFT JOIN tblDepartment       (NOLOCK) d   ON d.DepartmentId  = e.DepartmentId        
    LEFT JOIN tblDesignation      (NOLOCK) dg  ON dg.DesignationId = e.DesignationId        
    LEFT JOIN tblLocation         (NOLOCK) l   ON l.LocationId     = e.LocationId        
    LEFT JOIN tblRegion           (NOLOCK) reg ON l.RegionId       = reg.RegionId      
    LEFT JOIN tblZone             (NOLOCK) zone ON l.ZoneId        = zone.Id      
    LEFT JOIN Cluster             (NOLOCK) cl  ON l.ClusterId      = cl.Id      

    -- DEDUPED: one row per Ecode from mapping table
    LEFT JOIN (
        SELECT Ecode,
               MAX(Zone)   AS Zone,
               MAX(Region) AS Region,
               MAX(Cluster) AS Cluster
        FROM EcodeZoneRegionClusterMapping WITH (NOLOCK)
        GROUP BY Ecode
    ) eZone ON eZone.Ecode = e.Ecode      

    -- DEDUPED: one row per employee from separation table
    LEFT JOIN (
        SELECT EmployeeId, MAX(ResignationTypeId) AS ResignationTypeId
        FROM tblEmployeeSepration WITH (NOLOCK)
        GROUP BY EmployeeId
    ) sep ON sep.EmployeeId = e.EmployeeId
    LEFT JOIN tblResignationType (NOLOCK) trt ON trt.ResignationTypeId = sep.ResignationTypeId

    LEFT JOIN tblEmployee         (NOLOCK) u   ON TRY_CAST(e.UpdatedBy AS INT) = TRY_CAST(u.EmployeeId AS INT)        
    LEFT JOIN tblEmployee         (NOLOCK) c   ON TRY_CAST(e.CreatedBy AS INT) = TRY_CAST(c.EmployeeId AS INT)        
    LEFT JOIN tblEmployee         (NOLOCK) report ON e.ReportHeadEcode = report.Ecode      
    LEFT JOIN tblDesignation      (NOLOCK) reportdg ON reportdg.DesignationId = report.DesignationId      
    -- CTE join: no table hint allowed
    LEFT JOIN finalResult fr ON fr.Ecode = e.Ecode      
    WHERE      
        -- search filter      
        (@SearchTerm = ''         
         OR e.[FULL NAME] LIKE '%' + @SearchTerm + '%'        
         OR e.Ecode = @SearchTerm        
         OR d.DepartmentName LIKE '%' + @SearchTerm + '%'        
         OR dg.DesignationName LIKE '%' + @SearchTerm + '%'        
         OR l.LocationName LIKE '%' + @SearchTerm + '%'        
         OR l.STCode LIKE '%' + @SearchTerm + '%')        
        -- requested hard filters      
        --AND e.IsActive = 1      
        AND e.IsDeleted = 0      
        AND e.IsStore = 0      
        AND (    
            @ApplyFilter = 0                                  -- no manager provided or not found  no filtering    
            OR (@IsStore = 1 AND l.STCode = @MECode)          -- manager is store  filter by location code    
            OR (@IsStore = 0 AND e.ReportHeadEcode = @MECode) -- manager is not store  filter by report head    
        );    
        --AND fr.[TTL EXPERIENCE] > 0;      
      
    ----------------------------------------------------------------------      
    -- Counts      
    ----------------------------------------------------------------------      
    SELECT @TotalEmployees = COUNT(*) FROM @FilteredEmployees;        
    SELECT @ActiveCount    = COUNT(*) FROM @FilteredEmployees WHERE IsActive = 1;        
    SELECT @InactiveCount  = COUNT(*) FROM @FilteredEmployees WHERE IsActive = 0;        
    SELECT @LocCount       = COUNT(DISTINCT STCode) FROM @FilteredEmployees WHERE IsActive = 1;      
      
    SELECT @AbscondCount = COUNT(*)      
    FROM @FilteredEmployees a      
    INNER JOIN tblEmployeeSepration  (NOLOCK) b ON a.EmployeeId = b.EmployeeId      
    WHERE b.ResignationTypeId = 10;      
      
    SET @CurrentPageNumber = @PageNumber;        
      
    ----------------------------------------------------------------------      
    -- Outputs (same column order in both branches)      
    ----------------------------------------------------------------------      
    IF @PageNumber > 0 AND @PageSize > 0        
    BEGIN        
        SELECT      
            ZoneName,      
            RegionName,      
            ClusterName,      
            STCode,        
            LocationName,        
            Ecode,        
            FullName,      
            Gender,      
            DOB,      
            AgeInYears,      
            DepartmentId,      
            DesignationId,      
            DepartmentName,        
            DesignationName,        
            DOJ,      
            ResignationTypeName,      
            DateOfLeft,      
            [BANK NAME],      
            [A/C NO],      
            [BANK IFSC CODE],      
            [PERMANENT ADDRESS],      
            [PERMANENT ADDRESS PIN CODE],      
            [PRESENT ADDRESS],      
            [PRESENT ADDRESS PIN CODE],      
            MOBILE,      
            [EMAIL ADDRESS],      
            [AADHAR NO],      
            [PAN NO],      
            [HIGHEST QUALIFICATION],      
            [FATHER'S NAME],      
            [MOTHER'S NAME],      
            [MARITIAL STATUS],      
            ReportHeadEcode,      
            ReportHeadFullName,      
            ReportHeadDesignation,      
            [COMPANY NAME-1],      
            [From-I],      
            [To-I],      
            [YEARS-1],      
            [COMPANY NAME-2],      
            [From-II],      
            [To-II],      
            [YEARS-2],      
            [COMPANY NAME-3],      
            [From-III],      
            [To-III],      
            [YEARS-3],      
            [TTL EXPERIENCE],      
            LocStatus,      
            EmployeeStatus,      
            EmployeeId,      
            CandidateId,        
            LocBasedECode,        
            IsActive,        
            IsDeleted,        
            DateOfJoining,        
            IsStore,        
            CreatedOn,        
            UpdatedOn,        
            CreatedBy,        
            UpdatedBy      
        FROM @FilteredEmployees        
        ORDER BY EmployeeId DESC        
        OFFSET (@PageNumber - 1) * @PageSize ROWS        
        FETCH NEXT @PageSize ROWS ONLY;        
    END        
    ELSE        
    BEGIN        
        SELECT      
            ZoneName,      
            RegionName,      
            ClusterName,      
            STCode,        
            LocationName,        
            Ecode,        
            FullName,      
            Gender,      
            DOB,      
            AgeInYears,      
            DepartmentId,      
            DesignationId,      
            DepartmentName,        
            DesignationName,        
            DOJ,      
            ResignationTypeName,      
            DateOfLeft,      
            [BANK NAME],      
            [A/C NO],      
            [BANK IFSC CODE],      
            [PERMANENT ADDRESS],      
            [PERMANENT ADDRESS PIN CODE],      
            [PRESENT ADDRESS],      
            [PRESENT ADDRESS PIN CODE],      
            MOBILE,      
            [EMAIL ADDRESS],      
            [AADHAR NO],      
            [PAN NO],      
            [HIGHEST QUALIFICATION],      
            [FATHER'S NAME],      
            [MOTHER'S NAME],      
            [MARITIAL STATUS],      
            ReportHeadEcode,      
            ReportHeadFullName,      
            ReportHeadDesignation,      
            [COMPANY NAME-1],      
            [From-I],      
            [To-I],      
            [YEARS-1],      
            [COMPANY NAME-2],      
            [From-II],      
            [To-II],      
            [YEARS-2],      
            [COMPANY NAME-3],      
            [From-III],      
            [To-III],      
            [YEARS-3],      
            [TTL EXPERIENCE],      
            LocStatus,      
            EmployeeStatus,      
            EmployeeId,      
            CandidateId,        
            LocBasedECode,        
            IsActive,        
            IsDeleted,        
            DateOfJoining,        
            IsStore,        
            CreatedOn,        
            UpdatedOn,        
            CreatedBy,        
            UpdatedBy      
        FROM @FilteredEmployees        
        ORDER BY EmployeeId DESC;        
    END        
END; 


(1 rows affected)

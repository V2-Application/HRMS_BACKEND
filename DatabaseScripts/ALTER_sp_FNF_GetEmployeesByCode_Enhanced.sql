-- ALTER existing procedure to add pagination, global search, and date range parameters
-- This will preserve the existing procedure while adding new parameters

ALTER PROCEDURE [dbo].[sp_FNF_GetEmployeesByCode]    
    @SearchEcode nvarchar(50) = NULL,    
    @TopRows     int = 50000,
    @GlobalSearch nvarchar(100) = NULL,
    @FromDate    DateTime = NULL,
    @ToDate      DateTime = NULL,
    @Page        int = 1,
    @PageSize    int = 20
AS    
BEGIN    
    SET NOCOUNT ON;    
    SET @TopRows = 50000;    
    
    -- Validate pagination parameters
    IF @Page < 1 SET @Page = 1;
    IF @PageSize < 1 SET @PageSize = 20;
    
    DECLARE @q nvarchar(50) = NULLIF(LTRIM(RTRIM(@SearchEcode)), '');    
    DECLARE @GlobalSearchQuery nvarchar(100) = NULLIF(LTRIM(RTRIM(@GlobalSearch)), '');
    DECLARE @TwoMonthsAgo DATE = DATEADD(YEAR, -1, GETDATE());    
    
    -- Create a temporary table to hold all filtered data
    CREATE TABLE #FilteredData (
        EmployeeId BIGINT,
        EmployeeCode NVARCHAR(20),
        Name NVARCHAR(50),
        Department NVARCHAR(255),
        Designation NVARCHAR(255),
        DateOfJoining DATETIME,
        DateOfLeaving DATETIME,
        IsFNFCompleted BIT,
        UnpaidSalaryAmount DECIMAL(18,2),
        UnpaidSalaryDays INT,
        UnpaidSalaryMonth NVARCHAR(50),
        ResignationType NVARCHAR(50),
        ResignationDate DATETIME,
        SeparationLastDay DATETIME,
        ManagerApproved BIT,
        HRApproved BIT,
        ResignationAttachment NVARCHAR(MAX)
    );

    -- Insert filtered data into temporary table
    INSERT INTO #FilteredData
    SELECT 
        e.EmployeeId,
        e.Ecode AS EmployeeCode,
        e.[FULL NAME] AS Name,
        ISNULL(d.DepartmentName, '') AS Department,
        ISNULL(g.DesignationName, '') AS Designation,
        e.[JOINING DATE] AS DateOfJoining,
        e.[DateOfLeft] AS DateOfLeaving,
        ISNULL(e.IsFNFCompleted, 0) AS IsFNFCompleted,
        0 AS UnpaidSalaryAmount, -- Calculate as needed
        0 AS UnpaidSalaryDays,    -- Calculate as needed
        NULL AS UnpaidSalaryMonth,
        ISNULL(rt.ResignationTypeName, '') AS ResignationType,
        ts.ResignationDate,
        ts.LastDay AS SeparationLastDay,
        ISNULL(ts.IsApprovedByManager, 0) AS ManagerApproved,
        ISNULL(ts.IsApprovedByHR, 0) AS HRApproved,
        r.Attachment AS ResignationAttachment
    FROM dbo.tblEmployee e
    LEFT JOIN dbo.tblEmployeeSepration ts ON ts.EmployeeId = e.EmployeeId
    LEFT JOIN dbo.tblDepartment d ON d.DepartmentId = e.DepartmentId
    LEFT JOIN dbo.tblDesignation g ON g.DesignationId = e.DesignationId
    LEFT JOIN dbo.tblResignationType rt ON rt.ResignationTypeId = ts.ResignationTypeId
    LEFT JOIN (
        SELECT TOP 1 er.EmployeeId, er.Attachment
        FROM dbo.EmployeeResignationChecklistResponse er
        WHERE er.Attachment IS NOT NULL
        GROUP BY er.EmployeeId, er.Attachment
    ) r ON r.EmployeeId = e.EmployeeId
    WHERE 
        ISNULL(e.IsStore, 0) <> 1 
        AND ISNULL(e.IsActive, 0) = 0
        AND e.[DateOfLeft] IS NOT NULL
        AND e.[DateOfLeft] < @TwoMonthsAgo
        -- Employee code filter
        AND (@q IS NULL OR e.Ecode LIKE '%' + @q + '%')
        -- Global search filter (search across multiple fields)
        AND (@GlobalSearchQuery IS NULL OR 
             e.Ecode LIKE '%' + @GlobalSearchQuery + '%' OR
             e.[FULL NAME] LIKE '%' + @GlobalSearchQuery + '%' OR
             ISNULL(d.DepartmentName, '') LIKE '%' + @GlobalSearchQuery + '%' OR
             ISNULL(g.DesignationName, '') LIKE '%' + @GlobalSearchQuery + '%' OR
             ISNULL(rt.ResignationTypeName, '') LIKE '%' + @GlobalSearchQuery + '%')
        -- Date range filter on ResignationDate
        AND (@FromDate IS NULL OR ts.ResignationDate >= @FromDate)
        AND (@ToDate IS NULL OR ts.ResignationDate <= @ToDate)
    ORDER BY COALESCE(ts.LastDay, ts.ResignationDate, e.[DateOfLeft], e.[JOINING DATE], CONVERT(date,'19000101')) DESC;

    -- Return total count
    SELECT COUNT(*) AS TotalCount FROM #FilteredData;

    -- Return paginated data
    SELECT 
        EmployeeId,
        EmployeeCode,
        Name,
        Department,
        Designation,
        DateOfJoining,
        DateOfLeaving,
        IsFNFCompleted,
        UnpaidSalaryAmount,
        UnpaidSalaryDays,
        UnpaidSalaryMonth,
        ResignationType,
        ResignationDate,
        SeparationLastDay,
        ManagerApproved,
        HRApproved,
        ResignationAttachment
    FROM #FilteredData
    ORDER BY ResignationDate DESC
    OFFSET (@Page - 1) * @PageSize ROWS
    FETCH NEXT @PageSize ROWS ONLY;

    -- Clean up
    DROP TABLE #FilteredData;
END

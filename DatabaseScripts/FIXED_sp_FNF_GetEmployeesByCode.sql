USE [HRMS]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

ALTER PROCEDURE [dbo].[sp_FNF_GetEmployeesByCode]
    @SearchEcode      NVARCHAR(50)  = NULL,
    @TopRows          INT           = NULL,  -- Keep for backward compatibility but don't use
    @GlobalSearch     NVARCHAR(100) = NULL,
    @FromDate         DATETIME      = NULL,
    @ToDate           DATETIME      = NULL,
    @Page             INT           = 1,
    @PageSize         INT           = 20000
AS
BEGIN
    SET NOCOUNT ON;

    -- Validate pagination parameters
    IF @Page < 1 SET @Page = 1;
    
    -- If you want "whole record", call with @PageSize = 0 (or < 1)
    IF @PageSize IS NULL OR @PageSize < 1
    BEGIN
        SET @Page = 1;
        SET @PageSize = 2147483647; -- return all rows
    END

    DECLARE @q NVARCHAR(50) = NULLIF(LTRIM(RTRIM(@SearchEcode)), '');
    DECLARE @GlobalSearchQuery NVARCHAR(100) = NULLIF(LTRIM(RTRIM(@GlobalSearch)), '');

    -- Filter for employees who left in the last 1 year (365 days)
    DECLARE @OneYearAgo DATE = DATEADD(YEAR, -1, GETDATE());

    -- Optional date range (ONLY apply if user passes it)
    DECLARE @EffFromDate DATE = CASE WHEN @FromDate IS NULL THEN NULL ELSE CONVERT(DATE, @FromDate) END;
    DECLARE @EffToDate   DATE = CASE WHEN @ToDate   IS NULL THEN NULL ELSE CONVERT(DATE, @ToDate)   END;

    IF (@EffFromDate IS NOT NULL AND @EffToDate IS NOT NULL AND @EffFromDate > @EffToDate)
    BEGIN
        DECLARE @tmp DATE = @EffFromDate;
        SET @EffFromDate = @EffToDate;
        SET @EffToDate = @tmp;
    END

    -- Create a temporary table to hold filtered data for both count and pagination
    CREATE TABLE #FilteredData (
        EmployeeId BIGINT,
        EmployeeCode NVARCHAR(20),
        Name NVARCHAR(50),
        Department NVARCHAR(255),
        Designation NVARCHAR(255),
        DateOfJoining DATETIME2(0),
        DateOfLeaving DATETIME2(0),
        IsFNFCompleted BIT,
        UnpaidSalaryAmount DECIMAL(18,2),
        UnpaidSalaryDays INT,
        UnpaidSalaryMonth NVARCHAR(50),
        ResignationType NVARCHAR(50),
        ResignationDate DATETIME2(0),
        SeparationLastDay DATETIME2(0),
        ManagerApproved BIT,
        HRApproved BIT,
        ResignationAttachment NVARCHAR(MAX),
        RowNum INT
    );

    -- Insert filtered data into temporary table with row numbers
    INSERT INTO #FilteredData
    SELECT
        e.EmployeeId,
        e.Ecode AS EmployeeCode,
        e.[FULL NAME] AS Name,
        ISNULL(d.DepartmentName, '') AS Department,
        ISNULL(g.DesignationName, '') AS Designation,
        TRY_CONVERT(DATETIME2(0), e.[JOINING DATE]) AS DateOfJoining,
        TRY_CONVERT(DATETIME2(0), e.[DateOfLeft])   AS DateOfLeaving,
        ISNULL(e.IsFNFCompleted, 0) AS IsFNFCompleted,
        0 AS UnpaidSalaryAmount,
        0 AS UnpaidSalaryDays,
        NULL AS UnpaidSalaryMonth,
        ISNULL(rt.ResignationTypeName, '') AS ResignationType,
        TRY_CONVERT(DATETIME2(0), ts.ResignationDate) AS ResignationDate,
        TRY_CONVERT(DATETIME2(0), ts.LastDay)         AS SeparationLastDay,
        ISNULL(ts.IsApprovedByManager, 0) AS ManagerApproved,
        ISNULL(ts.IsApprovedByHR, 0)      AS HRApproved,
        a.Attachment AS ResignationAttachment,
        ROW_NUMBER() OVER (
            ORDER BY
                COALESCE(
                    NULLIF(CONVERT(date, TRY_CONVERT(DATETIME2(0), ts.LastDay)), '0001-01-01'),
                    NULLIF(CONVERT(date, TRY_CONVERT(DATETIME2(0), ts.ResignationDate)), '0001-01-01'),
                    NULLIF(CONVERT(date, TRY_CONVERT(DATETIME2(0), e.[DateOfLeft])), '0001-01-01'),
                    NULLIF(CONVERT(date, TRY_CONVERT(DATETIME2(0), e.[JOINING DATE])), '0001-01-01'),
                    CONVERT(date, '19000101')
                ) DESC
        ) AS RowNum
    FROM dbo.tblEmployee e
    LEFT JOIN dbo.tblEmployeeSepration ts ON ts.EmployeeId = e.EmployeeId
    LEFT JOIN dbo.tblDepartment d ON d.DepartmentId = e.DepartmentId
    LEFT JOIN dbo.tblDesignation g ON g.DesignationId = e.DesignationId
    LEFT JOIN dbo.tblResignationType rt ON rt.ResignationTypeId = ts.ResignationTypeId
    LEFT JOIN (
        -- One attachment per employee (same as your export reference)
        SELECT
            er.EmployeeId,
            MAX(er.Attachment) AS Attachment
        FROM dbo.EmployeeResignationChecklistResponse er
        WHERE er.Attachment IS NOT NULL
        GROUP BY er.EmployeeId
    ) a ON a.EmployeeId = e.EmployeeId
    WHERE
        -- ✅ Your requirement
        ISNULL(e.IsStore, 0) = 0
        AND ISNULL(e.IsActive, 0) = 0
        AND TRY_CONVERT(DATETIME2(0), e.[DateOfLeft]) IS NOT NULL
        AND TRY_CONVERT(DATETIME2(0), e.[DateOfLeft]) >= @OneYearAgo

        -- Optional DateOfLeft range filter ONLY if provided
        AND (
            @EffFromDate IS NULL
            OR (TRY_CONVERT(DATETIME2(0), e.[DateOfLeft]) IS NOT NULL AND CONVERT(DATE, TRY_CONVERT(DATETIME2(0), e.[DateOfLeft])) >= @EffFromDate)
        )
        AND (
            @EffToDate IS NULL
            OR (TRY_CONVERT(DATETIME2(0), e.[DateOfLeft]) IS NOT NULL AND CONVERT(DATE, TRY_CONVERT(DATETIME2(0), e.[DateOfLeft])) <= @EffToDate)
        )

        -- Optional employee code filter
        AND (@q IS NULL OR e.Ecode LIKE '%' + @q + '%')

        -- Optional global search filter
        AND (
            @GlobalSearchQuery IS NULL OR
            e.Ecode LIKE '%' + @GlobalSearchQuery + '%' OR
            e.[FULL NAME] LIKE '%' + @GlobalSearchQuery + '%' OR
            ISNULL(d.DepartmentName, '') LIKE '%' + @GlobalSearchQuery + '%' OR
            ISNULL(g.DesignationName, '') LIKE '%' + @GlobalSearchQuery + '%' OR
            ISNULL(rt.ResignationTypeName, '') LIKE '%' + @GlobalSearchQuery + '%'
        );

    -- First return the total count of filtered records
    SELECT COUNT(*) AS TotalCount FROM #FilteredData;

    -- Then return the paginated data
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
    WHERE RowNum BETWEEN (@Page - 1) * @PageSize + 1 AND @Page * @PageSize
    ORDER BY RowNum;

    -- Clean up
    DROP TABLE #FilteredData;
END
GO

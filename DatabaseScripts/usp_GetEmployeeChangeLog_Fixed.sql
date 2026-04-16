-- =============================================
-- Author:		Fixed Simple Pagination Version
-- Create date: 
-- Description:	Get Employee Change Log with Simple Pagination - Fixed
-- =============================================
CREATE OR ALTER PROCEDURE [dbo].[usp_GetEmployeeChangeLog_Simple]
(    
    @Ecode NVARCHAR(50),   -- pass Ecode here (e.g. 'v42801')    
    @PageNumber INT = 1,    -- Page number (1-based)
    @PageSize INT = 50,     -- Number of records per page
    @TotalRecords INT OUTPUT -- Total number of records (output parameter)
)    
AS    
BEGIN    
    SET NOCOUNT ON;    
    SET LOCK_TIMEOUT 60000; -- 60 seconds lock timeout
    SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;
    
    DECLARE @Offset INT = (@PageNumber - 1) * @PageSize;
    
    -- Create temp table for results
    CREATE TABLE #ChangeLogResults (
        Ecode NVARCHAR(50),
        ColumnName NVARCHAR(100),
        OldValue NVARCHAR(MAX),
        NewValue NVARCHAR(MAX),
        VersionLabel NVARCHAR(20),
        ChangedBy NVARCHAR(100),
        ChangedOn DATETIME,
        RowNum INT IDENTITY(1,1)
    );
    
    -- Get all history records for the employee and find changes
    ;WITH EmployeeHistory AS (
        SELECT 
            EmployeeId,
            Ecode,
            FirstName,
            LastName,
            [EMAIL ADDRESS],
            MOBILE,
            ValidFrom,
            ValidTo,
            UpdatedBy,
            UpdatedOn,
            ROW_NUMBER() OVER (PARTITION BY EmployeeId ORDER BY ValidFrom) AS VersionNum
        FROM [HRMS].[dbo].[tblEmployee_History] WITH (NOLOCK)
        WHERE Ecode = @Ecode
    ),
    CurrentEmployee AS (
        SELECT 
            EmployeeId,
            Ecode,
            FirstName,
            LastName,
            [EMAIL ADDRESS],
            MOBILE,
            UpdatedBy,
            UpdatedOn
        FROM [HRMS].[dbo].[tblEmployee] WITH (NOLOCK)
        WHERE Ecode = @Ecode
    )
    
    -- Insert changes between consecutive history versions
    INSERT INTO #ChangeLogResults (Ecode, ColumnName, OldValue, NewValue, VersionLabel, ChangedBy, ChangedOn)
    SELECT 
        h1.Ecode,
        'FirstName' AS ColumnName,
        h1.FirstName AS OldValue,
        h2.FirstName AS NewValue,
        'v' + CAST(h2.VersionNum AS NVARCHAR(10)) AS VersionLabel,
        h2.UpdatedBy AS ChangedBy,
        h2.UpdatedOn AS ChangedOn
    FROM EmployeeHistory h1
    INNER JOIN EmployeeHistory h2 ON h1.EmployeeId = h2.EmployeeId AND h1.VersionNum + 1 = h2.VersionNum
    WHERE ISNULL(h1.FirstName, '') <> ISNULL(h2.FirstName, '')
    
    UNION ALL
    
    SELECT 
        h1.Ecode,
        'LastName' AS ColumnName,
        h1.LastName AS OldValue,
        h2.LastName AS NewValue,
        'v' + CAST(h2.VersionNum AS NVARCHAR(10)) AS VersionLabel,
        h2.UpdatedBy AS ChangedBy,
        h2.UpdatedOn AS ChangedOn
    FROM EmployeeHistory h1
    INNER JOIN EmployeeHistory h2 ON h1.EmployeeId = h2.EmployeeId AND h1.VersionNum + 1 = h2.VersionNum
    WHERE ISNULL(h1.LastName, '') <> ISNULL(h2.LastName, '')
    
    UNION ALL
    
    SELECT 
        h1.Ecode,
        'EMAIL ADDRESS' AS ColumnName,
        h1.[EMAIL ADDRESS] AS OldValue,
        h2.[EMAIL ADDRESS] AS NewValue,
        'v' + CAST(h2.VersionNum AS NVARCHAR(10)) AS VersionLabel,
        h2.UpdatedBy AS ChangedBy,
        h2.UpdatedOn AS ChangedOn
    FROM EmployeeHistory h1
    INNER JOIN EmployeeHistory h2 ON h1.EmployeeId = h2.EmployeeId AND h1.VersionNum + 1 = h2.VersionNum
    WHERE ISNULL(h1.[EMAIL ADDRESS], '') <> ISNULL(h2.[EMAIL ADDRESS], '')
    
    UNION ALL
    
    SELECT 
        h1.Ecode,
        'MOBILE' AS ColumnName,
        h1.MOBILE AS OldValue,
        h2.MOBILE AS NewValue,
        'v' + CAST(h2.VersionNum AS NVARCHAR(10)) AS VersionLabel,
        h2.UpdatedBy AS ChangedBy,
        h2.UpdatedOn AS ChangedOn
    FROM EmployeeHistory h1
    INNER JOIN EmployeeHistory h2 ON h1.EmployeeId = h2.EmployeeId AND h1.VersionNum + 1 = h2.VersionNum
    WHERE ISNULL(h1.MOBILE, '') <> ISNULL(h2.MOBILE, '')
    
    UNION ALL
    
    -- Compare latest history with current employee data
    SELECT 
        h.Ecode,
        'FirstName' AS ColumnName,
        h.FirstName AS OldValue,
        e.FirstName AS NewValue,
        'vLatest' AS VersionLabel,
        e.UpdatedBy AS ChangedBy,
        e.UpdatedOn AS ChangedOn
    FROM EmployeeHistory h
    INNER JOIN CurrentEmployee e ON h.EmployeeId = e.EmployeeId
    WHERE h.VersionNum = (SELECT MAX(VersionNum) FROM EmployeeHistory WHERE EmployeeId = h.EmployeeId)
      AND ISNULL(h.FirstName, '') <> ISNULL(e.FirstName, '')
    
    UNION ALL
    
    SELECT 
        h.Ecode,
        'LastName' AS ColumnName,
        h.LastName AS OldValue,
        e.LastName AS NewValue,
        'vLatest' AS VersionLabel,
        e.UpdatedBy AS ChangedBy,
        e.UpdatedOn AS ChangedOn
    FROM EmployeeHistory h
    INNER JOIN CurrentEmployee e ON h.EmployeeId = e.EmployeeId
    WHERE h.VersionNum = (SELECT MAX(VersionNum) FROM EmployeeHistory WHERE EmployeeId = h.EmployeeId)
      AND ISNULL(h.LastName, '') <> ISNULL(e.LastName, '')
    
    UNION ALL
    
    SELECT 
        h.Ecode,
        'EMAIL ADDRESS' AS ColumnName,
        h.[EMAIL ADDRESS] AS OldValue,
        e.[EMAIL ADDRESS] AS NewValue,
        'vLatest' AS VersionLabel,
        e.UpdatedBy AS ChangedBy,
        e.UpdatedOn AS ChangedOn
    FROM EmployeeHistory h
    INNER JOIN CurrentEmployee e ON h.EmployeeId = e.EmployeeId
    WHERE h.VersionNum = (SELECT MAX(VersionNum) FROM EmployeeHistory WHERE EmployeeId = h.EmployeeId)
      AND ISNULL(h.[EMAIL ADDRESS], '') <> ISNULL(e.[EMAIL ADDRESS], '')
    
    UNION ALL
    
    SELECT 
        h.Ecode,
        'MOBILE' AS ColumnName,
        h.MOBILE AS OldValue,
        e.MOBILE AS NewValue,
        'vLatest' AS VersionLabel,
        e.UpdatedBy AS ChangedBy,
        e.UpdatedOn AS ChangedOn
    FROM EmployeeHistory h
    INNER JOIN CurrentEmployee e ON h.EmployeeId = e.EmployeeId
    WHERE h.VersionNum = (SELECT MAX(VersionNum) FROM EmployeeHistory WHERE EmployeeId = h.EmployeeId)
      AND ISNULL(h.MOBILE, '') <> ISNULL(e.MOBILE, '');
    
    -- Get total count
    SELECT @TotalRecords = COUNT(*) FROM #ChangeLogResults;
    
    -- Return paginated results
    SELECT 
        clr.Ecode,
        clr.ColumnName,
        clr.OldValue,
        clr.NewValue,
        clr.VersionLabel,
        ISNULL(emp.[FULL NAME], clr.ChangedBy) AS ChangedBy,
        clr.ChangedOn
    FROM #ChangeLogResults clr
    LEFT JOIN [HRMS].[dbo].[tblEmployee] emp WITH (NOLOCK)
        ON emp.EmployeeId = clr.ChangedBy
    ORDER BY clr.Ecode, clr.ColumnName, clr.ChangedOn DESC
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
    
    DROP TABLE #ChangeLogResults;
END;

-- =============================================
-- Author:		Simple Pagination Version
-- Create date: 
-- Description:	Get Employee Change Log with Simple Pagination
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
    SET LOCK_TIMEOUT 30000; -- 30 seconds lock timeout
    
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
    
    -- Insert history changes
    INSERT INTO #ChangeLogResults (Ecode, ColumnName, OldValue, NewValue, VersionLabel, ChangedBy, ChangedOn)
    SELECT DISTINCT
        h1.Ecode,
        'FirstName' AS ColumnName,
        h1.FirstName AS OldValue,
        h2.FirstName AS NewValue,
        'v' + CAST(ROW_NUMBER() OVER (PARTITION BY h1.EmployeeId ORDER BY h1.ValidFrom) AS NVARCHAR(10)) AS VersionLabel,
        h2.UpdatedBy AS ChangedBy,
        h2.UpdatedOn AS ChangedOn
    FROM [HRMS].[dbo].[tblEmployee_History] h1 WITH (NOLOCK)
    JOIN [HRMS].[dbo].[tblEmployee_History] h2 WITH (NOLOCK)
        ON h1.EmployeeId = h2.EmployeeId 
       AND h1.ValidTo = h2.ValidFrom
    WHERE h1.Ecode = @Ecode
      AND ISNULL(h1.FirstName, '') <> ISNULL(h2.FirstName, '')
    
    UNION ALL
    
    SELECT DISTINCT
        h1.Ecode,
        'LastName' AS ColumnName,
        h1.LastName AS OldValue,
        h2.LastName AS NewValue,
        'v' + CAST(ROW_NUMBER() OVER (PARTITION BY h1.EmployeeId ORDER BY h1.ValidFrom) AS NVARCHAR(10)) AS VersionLabel,
        h2.UpdatedBy AS ChangedBy,
        h2.UpdatedOn AS ChangedOn
    FROM [HRMS].[dbo].[tblEmployee_History] h1 WITH (NOLOCK)
    JOIN [HRMS].[dbo].[tblEmployee_History] h2 WITH (NOLOCK)
        ON h1.EmployeeId = h2.EmployeeId 
       AND h1.ValidTo = h2.ValidFrom
    WHERE h1.Ecode = @Ecode
      AND ISNULL(h1.LastName, '') <> ISNULL(h2.LastName, '')
    
    UNION ALL
    
    SELECT DISTINCT
        h1.Ecode,
        'EMAIL ADDRESS' AS ColumnName,
        h1.[EMAIL ADDRESS] AS OldValue,
        h2.[EMAIL ADDRESS] AS NewValue,
        'v' + CAST(ROW_NUMBER() OVER (PARTITION BY h1.EmployeeId ORDER BY h1.ValidFrom) AS NVARCHAR(10)) AS VersionLabel,
        h2.UpdatedBy AS ChangedBy,
        h2.UpdatedOn AS ChangedOn
    FROM [HRMS].[dbo].[tblEmployee_History] h1 WITH (NOLOCK)
    JOIN [HRMS].[dbo].[tblEmployee_History] h2 WITH (NOLOCK)
        ON h1.EmployeeId = h2.EmployeeId 
       AND h1.ValidTo = h2.ValidFrom
    WHERE h1.Ecode = @Ecode
      AND ISNULL(h1.[EMAIL ADDRESS], '') <> ISNULL(h2.[EMAIL ADDRESS], '')
    
    UNION ALL
    
    SELECT DISTINCT
        h1.Ecode,
        'MOBILE' AS ColumnName,
        h1.MOBILE AS OldValue,
        h2.MOBILE AS NewValue,
        'v' + CAST(ROW_NUMBER() OVER (PARTITION BY h1.EmployeeId ORDER BY h1.ValidFrom) AS NVARCHAR(10)) AS VersionLabel,
        h2.UpdatedBy AS ChangedBy,
        h2.UpdatedOn AS ChangedOn
    FROM [HRMS].[dbo].[tblEmployee_History] h1 WITH (NOLOCK)
    JOIN [HRMS].[dbo].[tblEmployee_History] h2 WITH (NOLOCK)
        ON h1.EmployeeId = h2.EmployeeId 
       AND h1.ValidTo = h2.ValidFrom
    WHERE h1.Ecode = @Ecode
      AND ISNULL(h1.MOBILE, '') <> ISNULL(h2.MOBILE, '')
    
    UNION ALL
    
    -- Live comparison (latest values)
    SELECT DISTINCT
        h.Ecode,
        'FirstName' AS ColumnName,
        h.FirstName AS OldValue,
        e.FirstName AS NewValue,
        'vLatest' AS VersionLabel,
        e.UpdatedBy AS ChangedBy,
        e.UpdatedOn AS ChangedOn
    FROM [HRMS].[dbo].[tblEmployee_History] h WITH (NOLOCK)
    INNER JOIN [HRMS].[dbo].[tblEmployee] e WITH (NOLOCK)
        ON e.EmployeeId = h.EmployeeId
    WHERE h.Ecode = @Ecode
      AND h.ValidTo = '9999-12-31'
      AND ISNULL(h.FirstName, '') <> ISNULL(e.FirstName, '')
    
    UNION ALL
    
    SELECT DISTINCT
        h.Ecode,
        'LastName' AS ColumnName,
        h.LastName AS OldValue,
        e.LastName AS NewValue,
        'vLatest' AS VersionLabel,
        e.UpdatedBy AS ChangedBy,
        e.UpdatedOn AS ChangedOn
    FROM [HRMS].[dbo].[tblEmployee_History] h WITH (NOLOCK)
    INNER JOIN [HRMS].[dbo].[tblEmployee] e WITH (NOLOCK)
        ON e.EmployeeId = h.EmployeeId
    WHERE h.Ecode = @Ecode
      AND h.ValidTo = '9999-12-31'
      AND ISNULL(h.LastName, '') <> ISNULL(e.LastName, '')
    
    UNION ALL
    
    SELECT DISTINCT
        h.Ecode,
        'EMAIL ADDRESS' AS ColumnName,
        h.[EMAIL ADDRESS] AS OldValue,
        e.[EMAIL ADDRESS] AS NewValue,
        'vLatest' AS VersionLabel,
        e.UpdatedBy AS ChangedBy,
        e.UpdatedOn AS ChangedOn
    FROM [HRMS].[dbo].[tblEmployee_History] h WITH (NOLOCK)
    INNER JOIN [HRMS].[dbo].[tblEmployee] e WITH (NOLOCK)
        ON e.EmployeeId = h.EmployeeId
    WHERE h.Ecode = @Ecode
      AND h.ValidTo = '9999-12-31'
      AND ISNULL(h.[EMAIL ADDRESS], '') <> ISNULL(e.[EMAIL ADDRESS], '')
    
    UNION ALL
    
    SELECT DISTINCT
        h.Ecode,
        'MOBILE' AS ColumnName,
        h.MOBILE AS OldValue,
        e.MOBILE AS NewValue,
        'vLatest' AS VersionLabel,
        e.UpdatedBy AS ChangedBy,
        e.UpdatedOn AS ChangedOn
    FROM [HRMS].[dbo].[tblEmployee_History] h WITH (NOLOCK)
    INNER JOIN [HRMS].[dbo].[tblEmployee] e WITH (NOLOCK)
        ON e.EmployeeId = h.EmployeeId
    WHERE h.Ecode = @Ecode
      AND h.ValidTo = '9999-12-31'
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

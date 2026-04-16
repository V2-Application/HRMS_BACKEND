-- =============================================
-- Author:		Ultra Fast Pagination Version
-- Create date: 
-- Description:	Get Employee Change Log with Ultra Fast Pagination
-- =============================================
CREATE OR ALTER PROCEDURE [dbo].[usp_GetEmployeeChangeLog_Fast]
(    
    @Ecode NVARCHAR(50),   -- pass Ecode here (e.g. 'v42801')    
    @PageNumber INT = 1,    -- Page number (1-based)
    @PageSize INT = 50,     -- Number of records per page
    @TotalRecords INT OUTPUT -- Total number of records (output parameter)
)    
AS    
BEGIN    
    SET NOCOUNT ON;    
    SET LOCK_TIMEOUT 30000; 
    
    -- Simple approach: Get recent changes only for performance
    DECLARE @DateCutoff DATETIME = DATEADD(DAY, -30, GETDATE()); -- Last 30 days only
    
    -- Get total count quickly
    SELECT @TotalRecords = COUNT(*)
    FROM (
        SELECT 1 AS ChangeRecord
        FROM [HRMS].[dbo].[tblEmployee_History] h1 WITH (NOLOCK)
        WHERE h1.Ecode = @Ecode 
          AND h1.UpdatedOn >= @DateCutoff
          AND EXISTS (
              SELECT 1 
              FROM [HRMS].[dbo].[tblEmployee_History] h2 WITH (NOLOCK)
              WHERE h2.EmployeeId = h1.EmployeeId
                AND h2.ValidFrom = h1.ValidTo
                AND (
                    ISNULL(h1.FirstName, '') <> ISNULL(h2.FirstName, '')
                    OR ISNULL(h1.LastName, '') <> ISNULL(h2.LastName, '')
                    OR ISNULL(h1.[EMAIL ADDRESS], '') <> ISNULL(h2.[EMAIL ADDRESS], '')
                    OR ISNULL(h1.MOBILE, '') <> ISNULL(h2.MOBILE, '')
                )
          )
    ) AS Changes;
    
    -- Get paginated results
    ;WITH RecentChanges AS (
        SELECT 
            h1.Ecode,
            'FirstName' AS ColumnName,
            h1.FirstName AS OldValue,
            h2.FirstName AS NewValue,
            'v' + CAST(DENSE_RANK() OVER (PARTITION BY h1.EmployeeId ORDER BY h1.ValidFrom) AS NVARCHAR(10)) AS VersionLabel,
            h2.UpdatedBy AS ChangedBy,
            h2.UpdatedOn AS ChangedOn
        FROM [HRMS].[dbo].[tblEmployee_History] h1 WITH (NOLOCK)
        INNER JOIN [HRMS].[dbo].[tblEmployee_History] h2 WITH (NOLOCK)
            ON h2.EmployeeId = h1.EmployeeId
            AND h2.ValidFrom = h1.ValidTo
        WHERE h1.Ecode = @Ecode 
          AND h1.UpdatedOn >= @DateCutoff
          AND ISNULL(h1.FirstName, '') <> ISNULL(h2.FirstName, '')
        
        UNION ALL
        
        SELECT 
            h1.Ecode,
            'LastName' AS ColumnName,
            h1.LastName AS OldValue,
            h2.LastName AS NewValue,
            'v' + CAST(DENSE_RANK() OVER (PARTITION BY h1.EmployeeId ORDER BY h1.ValidFrom) AS NVARCHAR(10)) AS VersionLabel,
            h2.UpdatedBy AS ChangedBy,
            h2.UpdatedOn AS ChangedOn
        FROM [HRMS].[dbo].[tblEmployee_History] h1 WITH (NOLOCK)
        INNER JOIN [HRMS].[dbo].[tblEmployee_History] h2 WITH (NOLOCK)
            ON h2.EmployeeId = h1.EmployeeId
            AND h2.ValidFrom = h1.ValidTo
        WHERE h1.Ecode = @Ecode 
          AND h1.UpdatedOn >= @DateCutoff
          AND ISNULL(h1.LastName, '') <> ISNULL(h2.LastName, '')
        
        UNION ALL
        
        SELECT 
            h1.Ecode,
            'EMAIL ADDRESS' AS ColumnName,
            h1.[EMAIL ADDRESS] AS OldValue,
            h2.[EMAIL ADDRESS] AS NewValue,
            'v' + CAST(DENSE_RANK() OVER (PARTITION BY h1.EmployeeId ORDER BY h1.ValidFrom) AS NVARCHAR(10)) AS VersionLabel,
            h2.UpdatedBy AS ChangedBy,
            h2.UpdatedOn AS ChangedOn
        FROM [HRMS].[dbo].[tblEmployee_History] h1 WITH (NOLOCK)
        INNER JOIN [HRMS].[dbo].[tblEmployee_History] h2 WITH (NOLOCK)
            ON h2.EmployeeId = h1.EmployeeId
            AND h2.ValidFrom = h1.ValidTo
        WHERE h1.Ecode = @Ecode 
          AND h1.UpdatedOn >= @DateCutoff
          AND ISNULL(h1.[EMAIL ADDRESS], '') <> ISNULL(h2.[EMAIL ADDRESS], '')
        
        UNION ALL
        
        SELECT 
            h1.Ecode,
            'MOBILE' AS ColumnName,
            h1.MOBILE AS OldValue,
            h2.MOBILE AS NewValue,
            'v' + CAST(DENSE_RANK() OVER (PARTITION BY h1.EmployeeId ORDER BY h1.ValidFrom) AS NVARCHAR(10)) AS VersionLabel,
            h2.UpdatedBy AS ChangedBy,
            h2.UpdatedOn AS ChangedOn
        FROM [HRMS].[dbo].[tblEmployee_History] h1 WITH (NOLOCK)
        INNER JOIN [HRMS].[dbo].[tblEmployee_History] h2 WITH (NOLOCK)
            ON h2.EmployeeId = h1.EmployeeId
            AND h2.ValidFrom = h1.ValidTo
        WHERE h1.Ecode = @Ecode 
          AND h1.UpdatedOn >= @DateCutoff
          AND ISNULL(h1.MOBILE, '') <> ISNULL(h2.MOBILE, '')
    )
    
    SELECT 
        rc.Ecode,
        rc.ColumnName,
        rc.OldValue,
        rc.NewValue,
        rc.VersionLabel,
        ISNULL(emp.[FULL NAME], rc.ChangedBy) AS ChangedBy,
        rc.ChangedOn
    FROM RecentChanges rc
    LEFT JOIN [HRMS].[dbo].[tblEmployee] emp WITH (NOLOCK)
        ON emp.EmployeeId = rc.ChangedBy
    ORDER BY rc.ChangedOn DESC
    OFFSET (@PageNumber - 1) * @PageSize ROWS 
    FETCH NEXT @PageSize ROWS ONLY;
    
END;

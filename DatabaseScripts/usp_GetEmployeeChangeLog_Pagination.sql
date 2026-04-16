-- =============================================
-- Author:		Updated for Pagination
-- Create date: 
-- Description:	Get Employee Change Log with Pagination
-- =============================================
CREATE PROCEDURE [dbo].[usp_GetEmployeeChangeLog] --   V47565
(    
    @Ecode NVARCHAR(50),   -- pass Ecode here (e.g. 'v42801')    
    @PageNumber INT = 1,    -- Page number (1-based)
    @PageSize INT = 50,     -- Number of records per page
    @TotalRecords INT OUTPUT -- Total number of records (output parameter)
)    
AS    
BEGIN    
    SET NOCOUNT ON;    
    
    DECLARE     
        @sql      NVARCHAR(MAX) = N'',    
        @colsHist NVARCHAR(MAX) = N'',    
        @colsLive NVARCHAR(MAX) = N'',
        @sqlCount NVARCHAR(MAX) = N'',
        @Offset INT = (@PageNumber - 1) * @PageSize;    
    
    --------------------------------------------------------    
    -- 1) Build VALUES list for HistoryChanges (h1 vs h2)    
    --------------------------------------------------------    
    SELECT @colsHist =     
        STRING_AGG(    
            CAST(    
                '    (''' + REPLACE(C.COLUMN_NAME, '''', '''''') + ''', ' +    
                'CONVERT(NVARCHAR(MAX), h1.' + QUOTENAME(C.COLUMN_NAME) + '), ' +    
                'CONVERT(NVARCHAR(MAX), h2.' + QUOTENAME(C.COLUMN_NAME) + '))'    
            AS NVARCHAR(MAX)),    
            ',' + CHAR(13)    
        )    
    FROM INFORMATION_SCHEMA.COLUMNS C    
    WHERE LOWER(C.TABLE_NAME) = 'tblemployee'    
      AND C.COLUMN_NAME NOT IN ('ValidFrom', 'ValidTo');    
    
    --------------------------------------------------------    
    -- 2) Build VALUES list for LiveComparison (h vs e)    
    --------------------------------------------------------    
    SELECT @colsLive =     
        STRING_AGG(    
            CAST(    
                '    (''' + REPLACE(C.COLUMN_NAME, '''', '''''') + ''', ' +    
                'CONVERT(NVARCHAR(MAX), h.' + QUOTENAME(C.COLUMN_NAME) + '), ' +    
                'CONVERT(NVARCHAR(MAX), e.' + QUOTENAME(C.COLUMN_NAME) + '))'    
            AS NVARCHAR(MAX)),    
            ',' + CHAR(13)    
        )    
    FROM INFORMATION_SCHEMA.COLUMNS C    
    WHERE LOWER(C.TABLE_NAME) = 'tblemployee'    
      AND C.COLUMN_NAME NOT IN ('ValidFrom', 'ValidTo');    
    
    --------------------------------------------------------    
    -- 3) Build COUNT SQL to get total records    
    --------------------------------------------------------    
    SET @sqlCount = N'    
;WITH HistoryChanges AS (    
    SELECT    
        h1.EmployeeId,    
        h1.[Ecode],    
        c.ColumnName,    
        c.OldValue,    
        c.NewValue,    
        h2.UpdatedBy AS ChangedBy,    
        h2.UpdatedOn AS ChangedOn,    
        ROW_NUMBER() OVER (    
            PARTITION BY h1.EmployeeId, c.ColumnName    
            ORDER BY h1.ValidFrom    
        ) AS VersionNumber    
    FROM [HRMS].[dbo].[tblEmployee_History] h1 WITH (NOLOCK)    
    JOIN [HRMS].[dbo].[tblEmployee_History] h2 WITH (NOLOCK)    
        ON h1.EmployeeId = h2.EmployeeId     
       AND h1.ValidTo     = h2.ValidFrom    
    CROSS APPLY    
    (    
        VALUES    
' + @colsHist + '    
    ) AS c(ColumnName, OldValue, NewValue)    
    WHERE ISNULL(c.OldValue, '''') <> ISNULL(c.NewValue, '''')    
      AND h1.[Ecode] = @Ecode    
),    
LatestHistory AS (    
    SELECT     
        h.EmployeeId,    
        h.[Ecode],    
        MAX(h.ValidTo) AS LastValidTo    
    FROM [HRMS].[dbo].[tblEmployee_History] h WITH (NOLOCK)    
    WHERE h.[Ecode] = @Ecode    
    GROUP BY h.EmployeeId, h.[Ecode]    
),    
LiveComparison AS (    
    SELECT     
        h.EmployeeId,    
        h.[Ecode],    
        c.ColumnName,    
        c.OldValue,         -- OldValue = last history value    
        c.NewValue,         -- NewValue = live table value    
        e.UpdatedBy AS ChangedBy,    
        e.UpdatedOn AS ChangedOn,    
        NULL AS VersionNumber    
    FROM [HRMS].[dbo].[tblEmployee_History] h WITH (NOLOCK)    
    INNER JOIN LatestHistory lh    
        ON lh.EmployeeId = h.EmployeeId     
       AND lh.LastValidTo = h.ValidTo    
    INNER JOIN [HRMS].[dbo].[tblEmployee] e WITH (NOLOCK)    
        ON e.EmployeeId = h.EmployeeId    
    CROSS APPLY    
    (    
        VALUES    
' + @colsLive + '    
    ) AS c(ColumnName, OldValue, NewValue)    
)    
    
SELECT COUNT(*) AS TotalCount    
FROM (    
    -- All history versions: v1, v2, ...    
    SELECT    
        H.Ecode,    
        H.ColumnName,    
        H.OldValue,    
        H.NewValue,    
        H.VersionNumber AS VersionOrder,    
        H.ChangedBy,    
        H.ChangedOn    
    FROM HistoryChanges H    
    
    UNION ALL    
    
    -- One live snapshot per column: vLatest    
    SELECT    
        L.Ecode,    
        L.ColumnName,    
        L.OldValue,    
        L.NewValue,    
        999999 AS VersionOrder,    
        L.ChangedBy,    
        L.ChangedOn    
    FROM LiveComparison L    
) X;';    
    
    --------------------------------------------------------    
    -- 4) Build final dynamic SQL with pagination (with NOLOCK)    
    --------------------------------------------------------    
    SET @sql = N'    
;WITH HistoryChanges AS (    
    SELECT    
        h1.EmployeeId,    
        h1.[Ecode],    
        c.ColumnName,    
        c.OldValue,    
        c.NewValue,    
        h2.UpdatedBy AS ChangedBy,    
        h2.UpdatedOn AS ChangedOn,    
        ROW_NUMBER() OVER (    
            PARTITION BY h1.EmployeeId, c.ColumnName    
            ORDER BY h1.ValidFrom    
        ) AS VersionNumber    
    FROM [HRMS].[dbo].[tblEmployee_History] h1 WITH (NOLOCK)    
    JOIN [HRMS].[dbo].[tblEmployee_History] h2 WITH (NOLOCK)    
        ON h1.EmployeeId = h2.EmployeeId     
       AND h1.ValidTo     = h2.ValidFrom    
    CROSS APPLY    
    (    
        VALUES    
' + @colsHist + '    
    ) AS c(ColumnName, OldValue, NewValue)    
    WHERE ISNULL(c.OldValue, '''') <> ISNULL(c.NewValue, '''')    
      AND h1.[Ecode] = @Ecode    
),    
LatestHistory AS (    
    SELECT     
        h.EmployeeId,    
        h.[Ecode],    
        MAX(h.ValidTo) AS LastValidTo    
    FROM [HRMS].[dbo].[tblEmployee_History] h WITH (NOLOCK)    
    WHERE h.[Ecode] = @Ecode    
    GROUP BY h.EmployeeId, h.[Ecode]    
),    
LiveComparison AS (    
    SELECT     
        h.EmployeeId,    
        h.[Ecode],    
        c.ColumnName,    
        c.OldValue,         -- OldValue = last history value    
        c.NewValue,         -- NewValue = live table value    
        e.UpdatedBy AS ChangedBy,    
        e.UpdatedOn AS ChangedOn,    
        NULL AS VersionNumber    
    FROM [HRMS].[dbo].[tblEmployee_History] h WITH (NOLOCK)    
    INNER JOIN LatestHistory lh    
        ON lh.EmployeeId = h.EmployeeId     
       AND lh.LastValidTo = h.ValidTo    
    INNER JOIN [HRMS].[dbo].[tblEmployee] e WITH (NOLOCK)    
        ON e.EmployeeId = h.EmployeeId    
    CROSS APPLY    
    (    
        VALUES    
' + @colsLive + '    
    ) AS c(ColumnName, OldValue, NewValue)    
),    
AllData AS (    
    SELECT     
        X.Ecode,    
        X.ColumnName,    
        X.OldValue,    
        X.NewValue,    
        CASE     
            WHEN X.VersionOrder = 999999 THEN ''vLatest''    
            ELSE ''v'' + CAST(X.VersionOrder AS NVARCHAR(10))    
        END AS VersionLabel,    
        COALESCE(Emp.[FULL NAME], CONVERT(NVARCHAR(50), X.ChangedBy)) AS ChangedBy,    
        X.ChangedOn,    
        ROW_NUMBER() OVER (ORDER BY X.Ecode, X.ColumnName, X.VersionOrder DESC) AS RowNum    
    FROM (    
        -- All history versions: v1, v2, ...    
        SELECT    
            H.Ecode,    
            H.ColumnName,    
            H.OldValue,    
            H.NewValue,    
            H.VersionNumber AS VersionOrder,    
            H.ChangedBy,    
            H.ChangedOn    
        FROM HistoryChanges H    
    
        UNION ALL    
    
        -- One live snapshot per column: vLatest    
        SELECT    
            L.Ecode,    
            L.ColumnName,    
            L.OldValue,    
            L.NewValue,    
            999999 AS VersionOrder,    
            L.ChangedBy,    
            L.ChangedOn    
        FROM LiveComparison L    
    ) X    
    LEFT JOIN [HRMS].[dbo].[tblEmployee] Emp WITH (NOLOCK)    
        ON Emp.EmployeeId = X.ChangedBy    
)    
    
SELECT     
    Ecode,    
    ColumnName,    
    OldValue,    
    NewValue,    
    VersionLabel,    
    ChangedBy,    
    ChangedOn    
FROM AllData    
WHERE RowNum > @Offset AND RowNum <= (@Offset + @PageSize)    
ORDER BY Ecode, ColumnName, ChangedOn DESC;';    
    
    --------------------------------------------------------    
    -- 5) Get total records count    
    --------------------------------------------------------    
    DECLARE @CountTable TABLE (TotalCount INT);    
    INSERT INTO @CountTable    
    EXEC sp_executesql @sqlCount,    
        N'@Ecode NVARCHAR(50)',    
        @Ecode = @Ecode;    
    
    SET @TotalRecords = (SELECT TOP 1 TotalCount FROM @CountTable);    
    
    --------------------------------------------------------    
    -- 6) Execute paginated query    
    --------------------------------------------------------    
    EXEC sp_executesql @sql,    
        N'@Ecode NVARCHAR(50), @Offset INT, @PageSize INT',    
        @Ecode = @Ecode,    
        @Offset = @Offset,    
        @PageSize = @PageSize;    
END;

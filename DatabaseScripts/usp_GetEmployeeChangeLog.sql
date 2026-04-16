-- =============================================
-- Author:		Simplified - No Pagination
-- Create date: 
-- Description:	Get Employee Change Log - Returns all records for given Ecode
-- =============================================
ALTER PROCEDURE [dbo].[usp_GetEmployeeChangeLog]
(
    @Ecode NVARCHAR(50)                 -- e.g. 'v42801'
)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE
        @sql       NVARCHAR(MAX) = N'',
        @colsHist  NVARCHAR(MAX) = N'',
        @colsLive  NVARCHAR(MAX) = N'';

    ------------------------------------------------------------------------------
    -- Build VALUES list for HistoryChanges (h1 vs h2)
    -- Using sys.columns is faster than INFORMATION_SCHEMA for SQL Server
    ------------------------------------------------------------------------------
    SELECT @colsHist =
        STRING_AGG(
            CAST(
                '    (''' + REPLACE(c.name, '''', '''''') + ''', ' +
                'CONVERT(NVARCHAR(MAX), h1.' + QUOTENAME(c.name) + '), ' +
                'CONVERT(NVARCHAR(MAX), h2.' + QUOTENAME(c.name) + '))'
            AS NVARCHAR(MAX)),
            ',' + CHAR(13)
        )
    FROM sys.columns c
    WHERE c.object_id = OBJECT_ID(N'HRMS.dbo.tblEmployee')
      AND c.name NOT IN ('ValidFrom','ValidTo');

    ------------------------------------------------------------------------------
    -- Build VALUES list for LiveComparison (h vs e)
    ------------------------------------------------------------------------------
    SELECT @colsLive =
        STRING_AGG(
            CAST(
                '    (''' + REPLACE(c.name, '''', '''''') + ''', ' +
                'CONVERT(NVARCHAR(MAX), h.' + QUOTENAME(c.name) + '), ' +
                'CONVERT(NVARCHAR(MAX), e.' + QUOTENAME(c.name) + '))'
            AS NVARCHAR(MAX)),
            ',' + CHAR(13)
        )
    FROM sys.columns c
    WHERE c.object_id = OBJECT_ID(N'HRMS.dbo.tblEmployee')
      AND c.name NOT IN ('ValidFrom','ValidTo');

    ------------------------------------------------------------------------------
    -- DATA query - Returns all records
    ------------------------------------------------------------------------------
    SET @sql = N'
;WITH HistoryBase AS (
    SELECT *
    FROM HRMS.dbo.tblEmployee_History WITH (NOLOCK)
    WHERE Ecode = @Ecode
),
HistoryChanges AS (
    SELECT
        h1.EmployeeId,
        h1.Ecode,
        c.ColumnName,
        c.OldValue,
        c.NewValue,
        h2.UpdatedBy AS ChangedBy,
        h2.UpdatedOn AS ChangedOn,
        ROW_NUMBER() OVER (
            PARTITION BY h1.EmployeeId, c.ColumnName
            ORDER BY h1.ValidFrom
        ) AS VersionNumber
    FROM HistoryBase h1
    JOIN HistoryBase h2
      ON h1.EmployeeId = h2.EmployeeId
     AND h1.ValidTo    = h2.ValidFrom
    CROSS APPLY (
        VALUES
' + @colsHist + N'
    ) AS c(ColumnName, OldValue, NewValue)
    WHERE ISNULL(c.OldValue, '''') <> ISNULL(c.NewValue, '''')
),
LatestHistory AS (
    SELECT
        EmployeeId,
        Ecode,
        MAX(ValidTo) AS LastValidTo
    FROM HistoryBase
    GROUP BY EmployeeId, Ecode
),
LiveComparison AS (
    SELECT
        h.EmployeeId,
        h.Ecode,
        c.ColumnName,
        c.OldValue,
        c.NewValue,
        e.UpdatedBy AS ChangedBy,
        e.UpdatedOn AS ChangedOn,
        NULL AS VersionNumber
    FROM HistoryBase h
    INNER JOIN LatestHistory lh
      ON lh.EmployeeId = h.EmployeeId
     AND lh.LastValidTo = h.ValidTo
    INNER JOIN HRMS.dbo.tblEmployee e WITH (NOLOCK)
      ON e.EmployeeId = h.EmployeeId
    CROSS APPLY (
        VALUES
' + @colsLive + N'
    ) AS c(ColumnName, OldValue, NewValue)
    WHERE ISNULL(c.OldValue, '''') <> ISNULL(c.NewValue, '''')  -- IMPORTANT FIX
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
        X.ChangedOn
    FROM (
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
    LEFT JOIN HRMS.dbo.tblEmployee Emp WITH (NOLOCK)
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
ORDER BY ChangedOn DESC, ColumnName ASC
OPTION (RECOMPILE);
';

    ------------------------------------------------------------------------------
    -- Execute query - Returns all records
    ------------------------------------------------------------------------------
    EXEC sp_executesql
        @sql,
        N'@Ecode NVARCHAR(50)',
        @Ecode = @Ecode;
END;
GO

-- =============================================
-- FIX for usp_GetEmployeeChangeLog (the proc the Employee Change Log page actually calls).
-- The Ecode column on tblEmployee/_History is VARCHAR(20) but the proc parameter was
-- NVARCHAR(50). The implicit conversion forced a FULL SCAN of the ~75M-row history table on
-- every Ecode filter (~9s each; ~116s total on prod). Comparing as VARCHAR lets the existing
-- Ecode index SEEK. No logic / output change. Definition-only; no data touched.
-- =============================================
CREATE OR ALTER PROCEDURE [dbo].[usp_GetEmployeeChangeLog]
(
    @Ecode NVARCHAR(50)                 -- e.g. 'v42801'
)
AS
BEGIN
    SET NOCOUNT ON;

    -- Match the underlying VARCHAR(20) column so the Ecode index is used (no NVARCHAR-conversion scan).
    DECLARE @EcodeV VARCHAR(20) = @Ecode;

    DECLARE
        @sql       NVARCHAR(MAX) = N'',
        @colsHist  NVARCHAR(MAX) = N'',
        @colsLive  NVARCHAR(MAX) = N'';

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
    WHERE ISNULL(c.OldValue, '''') <> ISNULL(c.NewValue, '''')
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

    EXEC sp_executesql
        @sql,
        N'@Ecode VARCHAR(20)',
        @Ecode = @EcodeV;
END;

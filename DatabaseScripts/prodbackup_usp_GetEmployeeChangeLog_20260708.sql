-- =============================================
-- FIX for usp_GetEmployeeChangeLog (the proc the Employee Change Log page actually calls).
-- The Ecode column on tblEmployee/_History is VARCHAR(20) but the proc parameter was
-- NVARCHAR(50). The implicit conversion forced a FULL SCAN of the ~75M-row history table on
-- every Ecode filter (~9s each; ~116s total on prod). Comparing as VARCHAR lets the existing
-- Ecode index SEEK. No logic / output change. Definition-only; no data touched.
-- =============================================
CREATE   PROCEDURE [dbo].[usp_GetEmployeeChangeLog]
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
                'CONVERT(NVARCHAR(MAX), h1.' + QUOTENAME(CASE WHEN c.name = 'LastUpdatedBy' THEN 'UpdatedBy' ELSE c.name END) + '), ' +
                'CONVERT(NVARCHAR(MAX), h2.' + QUOTENAME(CASE WHEN c.name = 'LastUpdatedBy' THEN 'UpdatedBy' ELSE c.name END) + '))'
            AS NVARCHAR(MAX)),
            ',' + CHAR(13)
        )
    FROM sys.columns c
    WHERE c.object_id = OBJECT_ID(N'HRMS.dbo.tblEmployee')
      AND c.name NOT IN ('ValidFrom','ValidTo','UpdatedOn','CreatedBy','CreatedOn','DeletedBy','DeletedOn');

    SELECT @colsLive =
        STRING_AGG(
            CAST(
                '    (''' + REPLACE(c.name, '''', '''''') + ''', ' +
                'CONVERT(NVARCHAR(MAX), h.' + QUOTENAME(CASE WHEN c.name = 'LastUpdatedBy' THEN 'UpdatedBy' ELSE c.name END) + '), ' +
                'CONVERT(NVARCHAR(MAX), e.' + QUOTENAME(CASE WHEN c.name = 'LastUpdatedBy' THEN 'UpdatedBy' ELSE c.name END) + '))'
            AS NVARCHAR(MAX)),
            ',' + CHAR(13)
        )
    FROM sys.columns c
    WHERE c.object_id = OBJECT_ID(N'HRMS.dbo.tblEmployee')
      AND c.name NOT IN ('ValidFrom','ValidTo','UpdatedOn','CreatedBy','CreatedOn','DeletedBy','DeletedOn');

    SET @colsHist = @colsHist + N',' + CHAR(13) + N'    (''LocationName'', CONVERT(NVARCHAR(MAX), h1.[LocationId]), CONVERT(NVARCHAR(MAX), h2.[LocationId]))';
    SET @colsLive = @colsLive + N',' + CHAR(13) + N'    (''LocationName'', CONVERT(NVARCHAR(MAX), h.[LocationId]), CONVERT(NVARCHAR(MAX), e.[LocationId]))';

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
    a.Ecode,
    a.ColumnName,
    OldValue = COALESCE(
        CASE a.ColumnName
            WHEN ''LocationId'' THEN CONVERT(NVARCHAR(MAX), (SELECT TOP 1 cur.LocationId FROM HRMS.dbo.tblLocation cur WITH (NOLOCK) WHERE ISNULL(cur.IsDeleted,0)=0 AND cur.STCode = COALESCE((SELECT TOP 1 x.STCode FROM HRMS.dbo.tblLocation x WITH (NOLOCK) WHERE x.LocationId = TRY_CONVERT(INT, a.OldValue)),(SELECT TOP 1 h.STCode FROM HRMS.dbo.tblLocation_History h WITH (NOLOCK) WHERE h.LocationId = TRY_CONVERT(INT, a.OldValue) ORDER BY h.ValidTo DESC))))
            WHEN ''LocationName'' THEN COALESCE((SELECT TOP 1 x.LocationName + '' ('' + x.STCode + '')'' FROM HRMS.dbo.tblLocation x WITH (NOLOCK) WHERE x.LocationId = TRY_CONVERT(INT, a.OldValue)),(SELECT TOP 1 h.LocationName + '' ('' + h.STCode + '')'' FROM HRMS.dbo.tblLocation_History h WITH (NOLOCK) WHERE h.LocationId = TRY_CONVERT(INT, a.OldValue) ORDER BY h.ValidTo DESC))
            WHEN ''DepartmentId''    THEN (SELECT TOP 1 x.DepartmentName  FROM HRMS.dbo.tblDepartment x  WITH (NOLOCK) WHERE x.DepartmentId  = TRY_CONVERT(INT,    a.OldValue))
            WHEN ''DesignationId''   THEN (SELECT TOP 1 x.DesignationName FROM HRMS.dbo.tblDesignation x WITH (NOLOCK) WHERE x.DesignationId = TRY_CONVERT(INT,    a.OldValue))
            WHEN ''CompanyId''       THEN (SELECT TOP 1 x.CompanyName     FROM HRMS.dbo.tblCompany x     WITH (NOLOCK) WHERE x.CompanyId     = TRY_CONVERT(INT,    a.OldValue))
            WHEN ''ReportHeadEcode'' THEN (SELECT TOP 1 x.[FULL NAME] + '' ('' + x.Ecode + '')'' FROM HRMS.dbo.tblEmployee x WITH (NOLOCK) WHERE x.Ecode = a.OldValue)
            WHEN ''UpdatedBy''       THEN COALESCE((SELECT TOP 1 x.[FULL NAME] FROM HRMS.dbo.tblEmployee x WITH (NOLOCK) WHERE x.EmployeeId = TRY_CONVERT(BIGINT, a.OldValue)),(SELECT TOP 1 x.[FULL NAME] FROM HRMS.dbo.tblEmployee x WITH (NOLOCK) WHERE x.Ecode = a.OldValue),(SELECT TOP 1 x.[FULL NAME] FROM HRMS.dbo.tblEmployee x WITH (NOLOCK) WHERE x.[EMAIL ADDRESS] = a.OldValue))
            WHEN ''LastUpdatedBy''   THEN COALESCE((SELECT TOP 1 x.[FULL NAME] FROM HRMS.dbo.tblEmployee x WITH (NOLOCK) WHERE x.EmployeeId = TRY_CONVERT(BIGINT, a.OldValue)),(SELECT TOP 1 x.[FULL NAME] FROM HRMS.dbo.tblEmployee x WITH (NOLOCK) WHERE x.Ecode = a.OldValue),(SELECT TOP 1 x.[FULL NAME] FROM HRMS.dbo.tblEmployee x WITH (NOLOCK) WHERE x.[EMAIL ADDRESS] = a.OldValue))
            WHEN ''CreatedBy''       THEN COALESCE((SELECT TOP 1 x.[FULL NAME] FROM HRMS.dbo.tblEmployee x WITH (NOLOCK) WHERE x.EmployeeId = TRY_CONVERT(BIGINT, a.OldValue)),(SELECT TOP 1 x.[FULL NAME] FROM HRMS.dbo.tblEmployee x WITH (NOLOCK) WHERE x.Ecode = a.OldValue),(SELECT TOP 1 x.[FULL NAME] FROM HRMS.dbo.tblEmployee x WITH (NOLOCK) WHERE x.[EMAIL ADDRESS] = a.OldValue))
            WHEN ''DeletedBy''       THEN COALESCE((SELECT TOP 1 x.[FULL NAME] FROM HRMS.dbo.tblEmployee x WITH (NOLOCK) WHERE x.EmployeeId = TRY_CONVERT(BIGINT, a.OldValue)),(SELECT TOP 1 x.[FULL NAME] FROM HRMS.dbo.tblEmployee x WITH (NOLOCK) WHERE x.Ecode = a.OldValue),(SELECT TOP 1 x.[FULL NAME] FROM HRMS.dbo.tblEmployee x WITH (NOLOCK) WHERE x.[EMAIL ADDRESS] = a.OldValue))
            ELSE NULL
        END, a.OldValue),
    NewValue = COALESCE(
        CASE a.ColumnName
            WHEN ''LocationId'' THEN CONVERT(NVARCHAR(MAX), (SELECT TOP 1 cur.LocationId FROM HRMS.dbo.tblLocation cur WITH (NOLOCK) WHERE ISNULL(cur.IsDeleted,0)=0 AND cur.STCode = COALESCE((SELECT TOP 1 x.STCode FROM HRMS.dbo.tblLocation x WITH (NOLOCK) WHERE x.LocationId = TRY_CONVERT(INT, a.NewValue)),(SELECT TOP 1 h.STCode FROM HRMS.dbo.tblLocation_History h WITH (NOLOCK) WHERE h.LocationId = TRY_CONVERT(INT, a.NewValue) ORDER BY h.ValidTo DESC))))
            WHEN ''LocationName'' THEN COALESCE((SELECT TOP 1 x.LocationName + '' ('' + x.STCode + '')'' FROM HRMS.dbo.tblLocation x WITH (NOLOCK) WHERE x.LocationId = TRY_CONVERT(INT, a.NewValue)),(SELECT TOP 1 h.LocationName + '' ('' + h.STCode + '')'' FROM HRMS.dbo.tblLocation_History h WITH (NOLOCK) WHERE h.LocationId = TRY_CONVERT(INT, a.NewValue) ORDER BY h.ValidTo DESC))
            WHEN ''DepartmentId''    THEN (SELECT TOP 1 x.DepartmentName  FROM HRMS.dbo.tblDepartment x  WITH (NOLOCK) WHERE x.DepartmentId  = TRY_CONVERT(INT,    a.NewValue))
            WHEN ''DesignationId''   THEN (SELECT TOP 1 x.DesignationName FROM HRMS.dbo.tblDesignation x WITH (NOLOCK) WHERE x.DesignationId = TRY_CONVERT(INT,    a.NewValue))
            WHEN ''CompanyId''       THEN (SELECT TOP 1 x.CompanyName     FROM HRMS.dbo.tblCompany x     WITH (NOLOCK) WHERE x.CompanyId     = TRY_CONVERT(INT,    a.NewValue))
            WHEN ''ReportHeadEcode'' THEN (SELECT TOP 1 x.[FULL NAME] + '' ('' + x.Ecode + '')'' FROM HRMS.dbo.tblEmployee x WITH (NOLOCK) WHERE x.Ecode = a.NewValue)
            WHEN ''UpdatedBy''       THEN COALESCE((SELECT TOP 1 x.[FULL NAME] FROM HRMS.dbo.tblEmployee x WITH (NOLOCK) WHERE x.EmployeeId = TRY_CONVERT(BIGINT, a.NewValue)),(SELECT TOP 1 x.[FULL NAME] FROM HRMS.dbo.tblEmployee x WITH (NOLOCK) WHERE x.Ecode = a.NewValue),(SELECT TOP 1 x.[FULL NAME] FROM HRMS.dbo.tblEmployee x WITH (NOLOCK) WHERE x.[EMAIL ADDRESS] = a.NewValue))
            WHEN ''LastUpdatedBy''   THEN COALESCE((SELECT TOP 1 x.[FULL NAME] FROM HRMS.dbo.tblEmployee x WITH (NOLOCK) WHERE x.EmployeeId = TRY_CONVERT(BIGINT, a.NewValue)),(SELECT TOP 1 x.[FULL NAME] FROM HRMS.dbo.tblEmployee x WITH (NOLOCK) WHERE x.Ecode = a.NewValue),(SELECT TOP 1 x.[FULL NAME] FROM HRMS.dbo.tblEmployee x WITH (NOLOCK) WHERE x.[EMAIL ADDRESS] = a.NewValue))
            WHEN ''CreatedBy''       THEN COALESCE((SELECT TOP 1 x.[FULL NAME] FROM HRMS.dbo.tblEmployee x WITH (NOLOCK) WHERE x.EmployeeId = TRY_CONVERT(BIGINT, a.NewValue)),(SELECT TOP 1 x.[FULL NAME] FROM HRMS.dbo.tblEmployee x WITH (NOLOCK) WHERE x.Ecode = a.NewValue),(SELECT TOP 1 x.[FULL NAME] FROM HRMS.dbo.tblEmployee x WITH (NOLOCK) WHERE x.[EMAIL ADDRESS] = a.NewValue))
            WHEN ''DeletedBy''       THEN COALESCE((SELECT TOP 1 x.[FULL NAME] FROM HRMS.dbo.tblEmployee x WITH (NOLOCK) WHERE x.EmployeeId = TRY_CONVERT(BIGINT, a.NewValue)),(SELECT TOP 1 x.[FULL NAME] FROM HRMS.dbo.tblEmployee x WITH (NOLOCK) WHERE x.Ecode = a.NewValue),(SELECT TOP 1 x.[FULL NAME] FROM HRMS.dbo.tblEmployee x WITH (NOLOCK) WHERE x.[EMAIL ADDRESS] = a.NewValue))
            ELSE NULL
        END, a.NewValue),
    a.VersionLabel,
    a.ChangedBy,
    a.ChangedOn
FROM AllData a
ORDER BY a.ChangedOn DESC, a.ColumnName ASC
OPTION (RECOMPILE);
';

    EXEC sp_executesql
        @sql,
        N'@Ecode VARCHAR(20)',
        @Ecode = @EcodeV;
END;


-- ============================================================================
-- BULK RECOVERY: orphan LocationIds in tblEmployee -> tblLocation
--
-- Prerequisite: dbo.ExcelLocationStaging_20260522 must already be populated
-- with 542 rows from LocationCodeUploader_20260521_095824.xlsx
-- (run _Excel_LocationStaging_Inserts.sql FIRST).
--
-- Pipeline:
--   1. Map each orphan LocationId -> dominant STCode using the latest
--      ATTENDANCE_PUNCH_DETAIL row per employee, then majority-vote
--      across employees at that LocationId.
--   2. Match that STCode to a row in dbo.ExcelLocationStaging_20260522.
--   3. Snapshot dbo.tblLocation as tblLocation_PreBulkRecover_yyyyMMdd_HHmm.
--   4. SYSTEM_VERSIONING OFF, IDENTITY_INSERT ON, INSERT.
--   5. Skip orphans where STCode would collide with an existing tblLocation row.
--   6. Re-enable temporal versioning.
--   7. Verify: orphan count before/after, employees re-linked.
--
-- Caveats:
--   - Excel has no geo coordinates. Restored rows will have
--     NULL StoreLat/StoreLong/AllowedRadiusMeters -> a *second* error
--     ("Office location coordinates not configured") will hit on geo punch
--     attempts until coordinates are filled in manually.
--   - Best-effort mapping. Orphans where no employee has ever punched, or
--     where the dominant STCode is not in the Excel, are skipped.
-- ============================================================================
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF OBJECT_ID('dbo.ExcelLocationStaging_20260522') IS NULL
BEGIN
    RAISERROR('ExcelLocationStaging_20260522 not loaded. Run _Excel_LocationStaging_Inserts.sql first.', 16, 1);
    RETURN;
END;
GO

-- ----------------------------------------------------------------------------
-- Build mapping in temp tables (so we can print previews + reuse in INSERT).
-- ----------------------------------------------------------------------------
IF OBJECT_ID('tempdb..#orphans')    IS NOT NULL DROP TABLE #orphans;
IF OBJECT_ID('tempdb..#emp_st')     IS NOT NULL DROP TABLE #emp_st;
IF OBJECT_ID('tempdb..#loc_st')     IS NOT NULL DROP TABLE #loc_st;
IF OBJECT_ID('tempdb..#toInsert')   IS NOT NULL DROP TABLE #toInsert;

-- Orphan LocationIds = referenced by tblEmployee but missing from tblLocation
SELECT DISTINCT e.LocationId
INTO #orphans
FROM dbo.tblEmployee e
WHERE e.LocationId IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.tblLocation l WHERE l.LocationId = e.LocationId);

-- Per-employee most recent STCode from ATTENDANCE_PUNCH_DETAIL (immutable history).
;WITH ranked AS (
    SELECT
        a.EmployeeId,
        a.STCode,
        rn = ROW_NUMBER() OVER (PARTITION BY a.EmployeeId ORDER BY a.AttendanceDate DESC)
    FROM dbo.ATTENDANCE_PUNCH_DETAIL a
    WHERE a.STCode IS NOT NULL AND a.STCode <> ''
)
SELECT EmployeeId, STCode
INTO #emp_st
FROM ranked
WHERE rn = 1;

-- Per-orphan-LocationId, pick the dominant STCode across its employees.
;WITH cnt AS (
    SELECT e.LocationId, s.STCode, c = COUNT(*),
           rn = ROW_NUMBER() OVER (PARTITION BY e.LocationId ORDER BY COUNT(*) DESC)
    FROM dbo.tblEmployee e
    INNER JOIN #orphans o ON o.LocationId = e.LocationId
    INNER JOIN #emp_st   s ON s.EmployeeId = e.EmployeeId
    GROUP BY e.LocationId, s.STCode
)
SELECT LocationId, STCode
INTO #loc_st
FROM cnt
WHERE rn = 1;

-- Final to-insert set: join with Excel staging, skip STCode collisions.
SELECT
    l.LocationId,
    UPPER(LTRIM(RTRIM(s.LocCode)))  AS STCode,
    s.LocationName,
    s.OpeningDate,
    s.Status
INTO #toInsert
FROM #loc_st l
INNER JOIN dbo.ExcelLocationStaging_20260522 s
    ON UPPER(LTRIM(RTRIM(s.LocCode))) = UPPER(LTRIM(RTRIM(l.STCode)))
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.tblLocation cur
    WHERE UPPER(LTRIM(RTRIM(cur.STCode))) = UPPER(LTRIM(RTRIM(s.LocCode)))
);

-- ----------------------------------------------------------------------------
-- Preview counts (BEFORE writing anything).
-- ----------------------------------------------------------------------------
PRINT '--- Preview ---';
SELECT
    OrphansTotal           = (SELECT COUNT(*) FROM #orphans),
    OrphansMappedToStCode  = (SELECT COUNT(*) FROM #loc_st),
    OrphansMatchedInExcel  = (SELECT COUNT(*) FROM #loc_st l WHERE EXISTS (SELECT 1 FROM dbo.ExcelLocationStaging_20260522 s WHERE UPPER(LTRIM(RTRIM(s.LocCode))) = UPPER(LTRIM(RTRIM(l.STCode))))),
    OrphansToInsert        = (SELECT COUNT(*) FROM #toInsert),
    OrphansSkippedStCodeCollision = (
        SELECT COUNT(*) FROM #loc_st l
        INNER JOIN dbo.ExcelLocationStaging_20260522 s ON UPPER(LTRIM(RTRIM(s.LocCode))) = UPPER(LTRIM(RTRIM(l.STCode)))
        WHERE EXISTS (SELECT 1 FROM dbo.tblLocation cur WHERE UPPER(LTRIM(RTRIM(cur.STCode))) = UPPER(LTRIM(RTRIM(s.LocCode))))
    ),
    EmployeesUnblocked     = (SELECT COUNT(*) FROM dbo.tblEmployee WHERE LocationId IN (SELECT LocationId FROM #toInsert)),
    ActiveEmployeesUnblocked = (SELECT COUNT(*) FROM dbo.tblEmployee WHERE IsActive=1 AND LocationId IN (SELECT LocationId FROM #toInsert));

PRINT '--- Sample of rows to insert (first 10) ---';
SELECT TOP 10 LocationId, STCode, LocationName, OpeningDate, Status FROM #toInsert ORDER BY LocationId;

-- ----------------------------------------------------------------------------
-- Snapshot tblLocation before writing.
-- ----------------------------------------------------------------------------
DECLARE @SnapTable SYSNAME = CONCAT('tblLocation_PreBulkRecover_', FORMAT(SYSUTCDATETIME(),'yyyyMMdd_HHmm'));
DECLARE @sqlSnap NVARCHAR(MAX) = CONCAT('SELECT * INTO dbo.', QUOTENAME(@SnapTable), ' FROM dbo.tblLocation;');
EXEC sp_executesql @sqlSnap;
PRINT CONCAT('Snapshot created: dbo.', @SnapTable);

-- ----------------------------------------------------------------------------
-- Write block.
-- ----------------------------------------------------------------------------
BEGIN TRANSACTION;

    -- Pre-check: no LocationId in #toInsert is already in tblLocation
    IF EXISTS (
        SELECT 1 FROM #toInsert t INNER JOIN dbo.tblLocation cur ON cur.LocationId = t.LocationId
    )
    BEGIN
        ROLLBACK TRANSACTION;
        RAISERROR('Pre-check failed: some target LocationIds are already occupied.', 16, 1);
        RETURN;
    END;

    ALTER TABLE dbo.tblLocation SET (SYSTEM_VERSIONING = OFF);
    SET IDENTITY_INSERT dbo.tblLocation ON;

    INSERT INTO dbo.tblLocation
        (LocationId, STCode, LocationName, StateId, CreatedOn,
         LocationCategoryId, ZoneId, RegionId, ClusterId, IsActive,
         StoreLong, StoreLat, AllowedRadiusMeters, IsDeleted, OpeningDate,
         IsAllowOvertimePayment, ADDRESS, LocationType, StateIdForMinWage,
         IsGeofenceEnabled, IsEsicEnabled)
    SELECT
         t.LocationId,
         t.STCode,
         LEFT(t.LocationName, 255),
         NULL,                                  -- StateId (NULL; Excel only has state name, not id)
         SYSUTCDATETIME(),                      -- CreatedOn = now (we lost the original)
         NULL, NULL, NULL, NULL,                -- LocationCategoryId, ZoneId, RegionId, ClusterId (NULL; only names in Excel)
         CASE WHEN UPPER(t.Status) IN (N'ACTIVE',N'A',N'1') THEN 1 ELSE 0 END, -- IsActive
         NULL, NULL, NULL,                      -- StoreLong, StoreLat, AllowedRadiusMeters (must be filled in manually)
         0,                                     -- IsDeleted
         LEFT(t.OpeningDate, 50),
         0,                                     -- IsAllowOvertimePayment
         NULL,                                  -- ADDRESS
         NULL,                                  -- LocationType
         NULL,                                  -- StateIdForMinWage
         0,                                     -- IsGeofenceEnabled
         0                                      -- IsEsicEnabled
    FROM #toInsert t;

    DECLARE @inserted INT = @@ROWCOUNT;
    PRINT CONCAT('Rows inserted into tblLocation: ', @inserted);

    SET IDENTITY_INSERT dbo.tblLocation OFF;
    ALTER TABLE dbo.tblLocation
        SET (SYSTEM_VERSIONING = ON
             (HISTORY_TABLE = dbo.tblLocation_History, DATA_CONSISTENCY_CHECK = OFF));

COMMIT TRANSACTION;

-- ----------------------------------------------------------------------------
-- Verification.
-- ----------------------------------------------------------------------------
PRINT '--- Post-restore verification ---';
SELECT
    OrphanLocationIdsRemaining = (
        SELECT COUNT(DISTINCT e.LocationId)
        FROM dbo.tblEmployee e
        WHERE e.LocationId IS NOT NULL
          AND NOT EXISTS (SELECT 1 FROM dbo.tblLocation l WHERE l.LocationId = e.LocationId)
    ),
    ActiveEmpsStillOrphaned = (
        SELECT COUNT(*) FROM dbo.tblEmployee e
        WHERE e.IsActive = 1
          AND e.LocationId IS NOT NULL
          AND NOT EXISTS (SELECT 1 FROM dbo.tblLocation l WHERE l.LocationId = e.LocationId)
    );

PRINT '--- Sample of newly-restored rows ---';
SELECT TOP 10 LocationId, STCode, LocationName, IsActive, IsDeleted, CreatedOn
FROM dbo.tblLocation
WHERE LocationId IN (SELECT LocationId FROM #toInsert)
ORDER BY LocationId;
GO

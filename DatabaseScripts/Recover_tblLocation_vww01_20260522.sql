-- ============================================================================
-- SINGLE-STORE RECOVERY: STCode = 'vww01'
--
-- Background:
--   'vww01' is missing from dbo.tblLocation (~55-57 employees in tblEmployee
--   point at its LocationId, but the master row is gone after the 2026-05-21
--   wipe). This script:
--     1. Reports the orphan LocationId those employees still reference.
--     2. Surveys every source that may still have 'vww01': temporal history
--        + every known backup table.
--     3. ONLY IF you uncomment the INSERT block: snapshots tblLocation, then
--        inserts vww01 back with its original LocationId (IDENTITY_INSERT).
--
-- This script is read-only as written. The write block is wrapped and
-- commented out — you MUST review the source survey output and uncomment
-- it after confirming which source row to use.
--
-- No DELETEs anywhere. No employee remap needed (we reuse the orphan ID).
-- ============================================================================
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

DECLARE @StCode NVARCHAR(10) = 'vww01';

-- ----------------------------------------------------------------------------
-- A) Is vww01 currently present in tblLocation?
-- ----------------------------------------------------------------------------
PRINT '--- A) Current state in dbo.tblLocation ---';
SELECT LocationId, STCode, LocationName, IsActive, IsDeleted, CreatedOn
FROM dbo.tblLocation
WHERE STCode = @StCode;

-- ----------------------------------------------------------------------------
-- B) Orphan LocationId held by employees who think they are at vww01.
--     This is the ID we want to restore (preserves the FK link).
-- ----------------------------------------------------------------------------
PRINT '--- B) Orphan LocationId held by employees ---';
SELECT
    e.LocationId            AS OrphanLocationId,
    COUNT(*)                AS EmployeeCount,
    SUM(CASE WHEN e.IsActive = 1 THEN 1 ELSE 0 END) AS ActiveEmployees
FROM dbo.tblEmployee e
WHERE e.LocationId IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.tblLocation l WHERE l.LocationId = e.LocationId)
  AND EXISTS (
      -- bind orphan ID to vww01 via any backup/history that still has the link
      SELECT 1 FROM dbo.tblLocation FOR SYSTEM_TIME ALL h
      WHERE h.LocationId = e.LocationId AND h.STCode = @StCode
      UNION ALL SELECT 1 FROM dbo.tblLocationBackup           b WHERE b.LocationId = e.LocationId AND b.STCode = @StCode
      UNION ALL SELECT 1 FROM dbo.tblLocation_2025_06_13      b WHERE b.LocationId = e.LocationId AND b.STCode = @StCode
      UNION ALL SELECT 1 FROM dbo.tblLOcation_BAckup12062025  b WHERE b.LocationId = e.LocationId AND b.STCode = @StCode
      UNION ALL SELECT 1 FROM dbo.tblLocation_Backup18092025  b WHERE b.LocationId = e.LocationId AND b.STCode = @StCode
      UNION ALL SELECT 1 FROM dbo.tblLocationBAckup120620251508 b WHERE b.LocationId = e.LocationId AND b.STCode = @StCode
      UNION ALL SELECT 1 FROM dbo.Location_BAckup_11062025    b WHERE b.LocationId = e.LocationId AND b.STCode = @StCode
  )
GROUP BY e.LocationId;

-- ----------------------------------------------------------------------------
-- C) Source survey — every row for vww01 we can still find, with a freshness
--    score. Pick the one with the most recent CreatedOn / fullest data.
-- ----------------------------------------------------------------------------
PRINT '--- C) vww01 candidates across history + backups ---';

;WITH src AS (
    SELECT 'tblLocation_History (temporal ALL)' AS Source,
           h.LocationId, h.STCode, h.LocationName, h.StateId, h.LocationCategoryId,
           h.ZoneId, h.RegionId, h.ClusterId, h.IsActive, h.IsDeleted,
           h.StoreLong, h.StoreLat, h.AllowedRadiusMeters, h.OpeningDate,
           h.ADDRESS, h.LocationType, h.IsGeofenceEnabled, h.IsEsicEnabled,
           h.CreatedOn
    FROM dbo.tblLocation FOR SYSTEM_TIME ALL h
    WHERE h.STCode = @StCode

    UNION ALL SELECT 'tblLocationBackup',           LocationId, STCode, LocationName, StateId, LocationCategoryId, ZoneId, RegionId, ClusterId, IsActive, IsDeleted, StoreLong, StoreLat, AllowedRadiusMeters, OpeningDate, ADDRESS, LocationType, IsGeofenceEnabled, IsEsicEnabled, CreatedOn FROM dbo.tblLocationBackup           WHERE STCode = @StCode
    UNION ALL SELECT 'tblLocation_2025_06_13',      LocationId, STCode, LocationName, StateId, LocationCategoryId, ZoneId, RegionId, ClusterId, IsActive, IsDeleted, StoreLong, StoreLat, AllowedRadiusMeters, OpeningDate, ADDRESS, LocationType, IsGeofenceEnabled, IsEsicEnabled, CreatedOn FROM dbo.tblLocation_2025_06_13      WHERE STCode = @StCode
    UNION ALL SELECT 'tblLOcation_BAckup12062025',  LocationId, STCode, LocationName, StateId, LocationCategoryId, ZoneId, RegionId, ClusterId, IsActive, IsDeleted, StoreLong, StoreLat, AllowedRadiusMeters, OpeningDate, ADDRESS, LocationType, IsGeofenceEnabled, IsEsicEnabled, CreatedOn FROM dbo.tblLOcation_BAckup12062025  WHERE STCode = @StCode
    UNION ALL SELECT 'tblLocation_Backup18092025',  LocationId, STCode, LocationName, StateId, LocationCategoryId, ZoneId, RegionId, ClusterId, IsActive, IsDeleted, StoreLong, StoreLat, AllowedRadiusMeters, OpeningDate, ADDRESS, LocationType, IsGeofenceEnabled, IsEsicEnabled, CreatedOn FROM dbo.tblLocation_Backup18092025  WHERE STCode = @StCode
    UNION ALL SELECT 'tblLocationBAckup120620251508', LocationId, STCode, LocationName, StateId, LocationCategoryId, ZoneId, RegionId, ClusterId, IsActive, IsDeleted, StoreLong, StoreLat, AllowedRadiusMeters, OpeningDate, ADDRESS, LocationType, IsGeofenceEnabled, IsEsicEnabled, CreatedOn FROM dbo.tblLocationBAckup120620251508 WHERE STCode = @StCode
    UNION ALL SELECT 'Location_BAckup_11062025',    LocationId, STCode, LocationName, StateId, LocationCategoryId, ZoneId, RegionId, ClusterId, IsActive, IsDeleted, StoreLong, StoreLat, AllowedRadiusMeters, OpeningDate, ADDRESS, LocationType, IsGeofenceEnabled, IsEsicEnabled, CreatedOn FROM dbo.Location_BAckup_11062025    WHERE STCode = @StCode
)
SELECT * FROM src ORDER BY CreatedOn DESC, Source;

-- ============================================================================
-- D) WRITE BLOCK (commented out). After reviewing section C, fill in the
--    @LocationId / @LocationName / etc. from the chosen source row and
--    uncomment. Keep the snapshot step.
-- ============================================================================
/*
DECLARE @LocationId          INT           = <<FROM B: orphan LocationId>>;
DECLARE @LocationName        NVARCHAR(255) = N'<<from chosen source>>';
DECLARE @StateId             INT           = <<...>>;
DECLARE @LocationCategoryId  INT           = <<...>>;
DECLARE @ZoneId              INT           = <<...>>;
DECLARE @RegionId            INT           = <<...>>;
DECLARE @ClusterId           INT           = <<...>>;
DECLARE @IsActive            BIT           = 1;
DECLARE @StoreLong           DECIMAL(18,8) = <<...>>;
DECLARE @StoreLat            DECIMAL(18,8) = <<...>>;
DECLARE @AllowedRadiusMeters INT           = <<...>>;
DECLARE @IsDeleted           BIT           = 0;
DECLARE @OpeningDate         NVARCHAR(50)  = <<...>>;
DECLARE @ADDRESS             NVARCHAR(MAX) = <<...>>;
DECLARE @LocationType        NVARCHAR(100) = <<...>>;
DECLARE @StateIdForMinWage   INT           = <<...>>;
DECLARE @IsGeofenceEnabled   BIT           = <<...>>;
DECLARE @IsEsicEnabled       BIT           = <<...>>;

-- Snapshot tblLocation before the write (per memory rule).
DECLARE @SnapTable SYSNAME = CONCAT('tblLocation_PreVww01Recover_', FORMAT(SYSUTCDATETIME(),'yyyyMMdd_HHmm'));
DECLARE @sql NVARCHAR(MAX) = CONCAT('SELECT * INTO dbo.', QUOTENAME(@SnapTable), ' FROM dbo.tblLocation;');
EXEC sp_executesql @sql;
PRINT CONCAT('Snapshot created: dbo.', @SnapTable);

BEGIN TRANSACTION;

    -- Abort if vww01 has reappeared since we checked.
    IF EXISTS (SELECT 1 FROM dbo.tblLocation WHERE STCode = @StCode)
    BEGIN
        ROLLBACK TRANSACTION;
        RAISERROR('vww01 already exists in tblLocation. Aborting.', 16, 1);
        RETURN;
    END;

    -- Abort if @LocationId is taken by something else.
    IF EXISTS (SELECT 1 FROM dbo.tblLocation WHERE LocationId = @LocationId)
    BEGIN
        ROLLBACK TRANSACTION;
        RAISERROR('LocationId already occupied by a different row. Aborting.', 16, 1);
        RETURN;
    END;

    SET IDENTITY_INSERT dbo.tblLocation ON;
    INSERT INTO dbo.tblLocation
        (LocationId, STCode, LocationName, StateId, CreatedOn,
         LocationCategoryId, ZoneId, RegionId, ClusterId, IsActive,
         StoreLong, StoreLat, AllowedRadiusMeters, IsDeleted, OpeningDate,
         IsAllowOvertimePayment, ADDRESS, LocationType, StateIdForMinWage,
         IsGeofenceEnabled, IsEsicEnabled)
    VALUES
        (@LocationId, @StCode, @LocationName, @StateId, SYSUTCDATETIME(),
         @LocationCategoryId, @ZoneId, @RegionId, @ClusterId, @IsActive,
         @StoreLong, @StoreLat, @AllowedRadiusMeters, @IsDeleted, @OpeningDate,
         NULL, @ADDRESS, @LocationType, @StateIdForMinWage,
         @IsGeofenceEnabled, @IsEsicEnabled);
    SET IDENTITY_INSERT dbo.tblLocation OFF;

COMMIT TRANSACTION;

-- Verify: row back, no employees orphaned at that LocationId.
SELECT * FROM dbo.tblLocation WHERE STCode = @StCode;

SELECT COUNT(*) AS EmployeesNowLinkedToVww01
FROM dbo.tblEmployee
WHERE LocationId = @LocationId;
*/

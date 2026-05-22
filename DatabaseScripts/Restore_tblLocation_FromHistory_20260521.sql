-- ============================================================================
-- LOCATION MASTER RESTORE - point-in-time = 2026-05-14 12:00 UTC
--
-- Background:
--   tblLocation was mass-wiped at 2026-05-14 12:20 UTC (1,062 history rows
--   written in 20 seconds). At 12:00 UTC, just before the wipe, there
--   were 457 live rows. After the wipe only 2 rows remained, leaving
--   50,814 employees with FK-orphaned LocationId. Today's REPLACE-ALL
--   upload added 544 new rows with different IDs (665-1208), which did
--   not fix the orphans.
--
-- This script restores tblLocation to its 2026-05-14 12:00 UTC state:
--   - Capture the live snapshot via FOR SYSTEM_TIME AS OF.
--   - Disable FKs + temporal versioning.
--   - Remap 4 employees that were pointed at the today-only LocationIds.
--   - DELETE today's 544 new rows, INSERT 457 pre-wipe rows with original IDs.
--   - Re-enable temporal versioning + FKs.
--
-- Idempotent: a second run with already-restored data is a no-op as long
-- as the snapshot timestamp is unchanged.
-- ============================================================================
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

DECLARE @RestoreTo DATETIME2(7) = '2026-05-14T12:00:00';
DECLARE @SnapshotCount INT;

-- Step 1: materialize pre-wipe snapshot (requires versioning still ON).
IF OBJECT_ID('tempdb..#restoreData') IS NOT NULL DROP TABLE #restoreData;

SELECT
    LocationId, STCode, LocationName, StateId, CreatedOn,
    LocationCategoryId, ZoneId, RegionId, ClusterId, IsActive,
    StoreLong, StoreLat, AllowedRadiusMeters, IsDeleted, OpeningDate,
    IsAllowOvertimePayment, ADDRESS, LocationType, StateIdForMinWage,
    IsGeofenceEnabled, IsEsicEnabled
INTO #restoreData
FROM dbo.tblLocation FOR SYSTEM_TIME AS OF @RestoreTo;

SELECT @SnapshotCount = COUNT(*) FROM #restoreData;
PRINT CONCAT('Snapshot rows captured: ', @SnapshotCount);

IF @SnapshotCount < 100
BEGIN
    RAISERROR('Snapshot returned suspiciously few rows. Aborting.', 16, 1);
    RETURN;
END;

BEGIN TRANSACTION;

-- Step 2: disable FK constraints (same set the upload code touches).
ALTER TABLE dbo.StoreRoutingTransaction NOCHECK CONSTRAINT [FK__StoreRout__Locat__1C281490];
ALTER TABLE dbo.StoreRoutingTransaction NOCHECK CONSTRAINT [FK__StoreRout__Locat__24BD5A91];
ALTER TABLE dbo.StoreRoutingTransaction NOCHECK CONSTRAINT [FK__StoreRout__Locat__2799C73C];
ALTER TABLE dbo.StoreRoutingTransaction NOCHECK CONSTRAINT [FK__StoreRout__Locat__2C5E7C59];

-- Step 3: disable temporal versioning so we can DELETE + IDENTITY_INSERT.
ALTER TABLE dbo.tblLocation SET (SYSTEM_VERSIONING = OFF);

-- Step 4: remap employees that pointed at today-only LocationIds.
UPDATE dbo.tblEmployee SET LocationId = 207  WHERE EmployeeId = 140833;
UPDATE dbo.tblEmployee SET LocationId = NULL WHERE EmployeeId IN (143808, 143809, 143810);

-- Step 5: wipe live + restore from snapshot.
DELETE FROM dbo.tblLocation;

SET IDENTITY_INSERT dbo.tblLocation ON;
INSERT INTO dbo.tblLocation
    (LocationId, STCode, LocationName, StateId, CreatedOn,
     LocationCategoryId, ZoneId, RegionId, ClusterId, IsActive,
     StoreLong, StoreLat, AllowedRadiusMeters, IsDeleted, OpeningDate,
     IsAllowOvertimePayment, ADDRESS, LocationType, StateIdForMinWage,
     IsGeofenceEnabled, IsEsicEnabled)
SELECT
     LocationId, STCode, LocationName, StateId, CreatedOn,
     LocationCategoryId, ZoneId, RegionId, ClusterId, IsActive,
     StoreLong, StoreLat, AllowedRadiusMeters, IsDeleted, OpeningDate,
     IsAllowOvertimePayment, ADDRESS, LocationType, StateIdForMinWage,
     IsGeofenceEnabled, IsEsicEnabled
FROM #restoreData;
SET IDENTITY_INSERT dbo.tblLocation OFF;

-- Step 6: re-enable temporal versioning.
ALTER TABLE dbo.tblLocation SET (SYSTEM_VERSIONING = ON (HISTORY_TABLE = dbo.tblLocation_History, DATA_CONSISTENCY_CHECK = OFF));

-- Step 7: re-enable FKs.
ALTER TABLE dbo.StoreRoutingTransaction WITH NOCHECK CHECK CONSTRAINT [FK__StoreRout__Locat__1C281490];
ALTER TABLE dbo.StoreRoutingTransaction WITH NOCHECK CHECK CONSTRAINT [FK__StoreRout__Locat__24BD5A91];
ALTER TABLE dbo.StoreRoutingTransaction WITH NOCHECK CHECK CONSTRAINT [FK__StoreRout__Locat__2799C73C];
ALTER TABLE dbo.StoreRoutingTransaction WITH NOCHECK CHECK CONSTRAINT [FK__StoreRout__Locat__2C5E7C59];

COMMIT TRANSACTION;
GO

-- Post-restore verification.
SELECT
    (SELECT COUNT(*) FROM dbo.tblLocation) AS RestoredRows,
    (SELECT COUNT(*) FROM dbo.tblEmployee e
       WHERE e.LocationId IS NOT NULL
         AND NOT EXISTS (SELECT 1 FROM dbo.tblLocation l WHERE l.LocationId = e.LocationId))
    AS OrphanedEmployeeRefs;
GO

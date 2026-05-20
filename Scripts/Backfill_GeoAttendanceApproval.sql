-- =============================================================================
-- Category: GeoAttendance (one-time backfill, re-runnable)
-- =============================================================================
-- Backfill GeoAttendanceApproval rows for every (EmployeeId, PunchDate) that
-- exists in AttendanceRecord but has no scaffold row in GeoAttendanceApproval.
--
-- Context: GeoAttendanceApproval rows are inserted *lazily* by
-- usp_ApproveGeoAttendance the first time a manager/master/superadmin acts
-- on a record. The original 2-level-approval migration seeded rows for
-- everything that existed at the time, but every punch ingested AFTER that
-- migration goes un-scaffolded until someone acts on it. The geofence export
-- LEFT JOINs onto this table, so those rows render with blank Manager/Master/
-- Final status. This script scaffolds those rows as Pending (StatusId=4).
--
-- Re-runnable: WHERE NOT EXISTS guard prevents duplicates.
-- =============================================================================
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

DECLARE @InsertedRows INT;

INSERT INTO dbo.GeoAttendanceApproval
    (EmployeeId, PunchDate,
     ManagerApprovalStatusId, MasterApprovalStatusId, FinalStatusId,
     CreatedOn)
SELECT
    sub.EmployeeId,
    sub.PunchDate,
    -- Preserve any pre-existing AttendanceRecord status (1=Approved, 2=Rejected)
    -- so we don't downgrade rows that were already actioned at the record level.
    CASE sub.StatusId WHEN 1 THEN 1 WHEN 2 THEN 2 ELSE 4 END,
    CASE sub.StatusId WHEN 1 THEN 1 WHEN 2 THEN 2 ELSE 4 END,
    sub.StatusId,
    SYSUTCDATETIME()
FROM (
    SELECT
        ar.EmployeeId,
        CONVERT(DATE, ar.PunchTimeUtc) AS PunchDate,
        MIN(ar.StatusId)               AS StatusId
    FROM dbo.AttendanceRecord ar
    GROUP BY ar.EmployeeId, CONVERT(DATE, ar.PunchTimeUtc)
) sub
WHERE NOT EXISTS (
    SELECT 1
    FROM dbo.GeoAttendanceApproval ga
    WHERE ga.EmployeeId = sub.EmployeeId
      AND ga.PunchDate  = sub.PunchDate
);

SET @InsertedRows = @@ROWCOUNT;
PRINT CONCAT('Backfill complete. Rows inserted: ', @InsertedRows);
GO

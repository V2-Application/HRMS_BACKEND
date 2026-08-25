/*
    Delete_LocationDesignationPolicy_Aug26_20260823.sql   (NOT YET APPLIED)

    Request: delete the Aug-26 weekly-off data shown on /attendance-add-weekly-off
    (PROD: 192.168.151.28\hrms, database HRMS).

    Which table
    -----------
    The screen (Location / Designation / Total Attendance From-To / Weekly Off /
    For Which Weeks, month picker 2026-08, "Total Records: 318") is backed by

        dbo.tblLocationDesignationPolicy   WHERE [Month-Year] = 'Aug-26'   -> 318 rows

    which matches the on-screen count exactly. Ids 1393..1710.
    (NOT BudgetWeekoffMaster - that table has no month column and a different shape.)

    What the 318 rows are
    ---------------------
        106 rows  IsActive = 1, IsDeleted = 0   <- the CURRENT Aug-26 policy set
                                                   (all created 2026-08-23 23:36:40)
        212 rows  IsActive = 0, IsDeleted = 0   <- superseded earlier versions,
                                                   created 2026-08-12 .. 2026-08-23
        318 total, all created by user 117665. None are already soft-deleted.

    Related: dbo.tblLocationDesignationPolicyHistory holds 212 Aug-26 rows (the
    audit trail of the superseded versions). This script does NOT touch it -
    see the optional block at the bottom if the history should go too.
    dbo.tblLocationDesignationPolicyMonth has no Aug-26 rows.

    !! IMPACT !!
    ------------
    After this runs the Add Weekly Off screen shows 0 records for 2026-08, and
    the 106 active Aug-26 weekly-off policies are gone. Any attendance/payroll
    logic reading this policy for August will fall back to whatever its no-policy
    path is. If the intent is to REPLACE the month, re-enter or use the screen's
    "Copy filtered data to selected month" straight afterwards.

    Rollback: restore from the backup table created in step 1 (block at bottom).
    LocationDesignationPolicyId is IDENTITY, so the restore uses IDENTITY_INSERT.
*/

SET XACT_ABORT ON;
BEGIN TRAN;

/* ---------- 1. full-row backup of exactly what will be deleted ---------- */
IF OBJECT_ID('dbo.tblLocationDesignationPolicy_DelBak_Aug26_20260823') IS NOT NULL
    THROW 50001, 'Backup table tblLocationDesignationPolicy_DelBak_Aug26_20260823 already exists - review it before re-running.', 1;

SELECT *
INTO dbo.tblLocationDesignationPolicy_DelBak_Aug26_20260823
FROM dbo.tblLocationDesignationPolicy
WHERE [Month-Year] = 'Aug-26';

DECLARE @n INT = (SELECT COUNT(*) FROM dbo.tblLocationDesignationPolicy_DelBak_Aug26_20260823);
PRINT CONCAT('rows backed up / to delete: ', @n, '  (expected 318)');

IF @n NOT BETWEEN 1 AND 600
BEGIN
    ROLLBACK TRAN;
    THROW 50002, 'Unexpected row count - nothing deleted. Re-check the scope before applying.', 1;
END

/* ---------- 2. the delete ---------- */
DELETE p
FROM dbo.tblLocationDesignationPolicy p
JOIN dbo.tblLocationDesignationPolicy_DelBak_Aug26_20260823 b
     ON b.LocationDesignationPolicyId = p.LocationDesignationPolicyId;

PRINT CONCAT('rows deleted: ', @@ROWCOUNT);

COMMIT TRAN;

/* ---------- 3. verification ---------- */
SELECT COUNT(*) AS RemainingAug26Rows
FROM dbo.tblLocationDesignationPolicy WHERE [Month-Year] = 'Aug-26';        -- expect 0

SELECT [Month-Year] AS MonthYear, COUNT(*) AS Rows                          -- Aug-26 should be absent
FROM dbo.tblLocationDesignationPolicy GROUP BY [Month-Year] ORDER BY MonthYear DESC;

/* ---------------- SOFT-DELETE ALTERNATIVE (instead of step 2) ------------
   Reversible and matches the app's own IsDeleted flag; the screen stops
   showing the rows but nothing leaves the table.

UPDATE p SET p.IsDeleted = 1, p.IsActive = 0, p.UpdatedOn = GETDATE(), p.UpdatedBy = 'seat-cleanup-20260823'
FROM dbo.tblLocationDesignationPolicy p
WHERE p.[Month-Year] = 'Aug-26';
--------------------------------------------------------------------------- */

/* ---------------- OPTIONAL: also clear the Aug-26 audit history ----------
SELECT * INTO dbo.tblLocationDesignationPolicyHistory_DelBak_Aug26_20260823
FROM dbo.tblLocationDesignationPolicyHistory WHERE [Month-Year] = 'Aug-26';   -- 212 rows
DELETE FROM dbo.tblLocationDesignationPolicyHistory WHERE [Month-Year] = 'Aug-26';
--------------------------------------------------------------------------- */

/* ---------------- ROLLBACK (run only if needed) --------------------------
SET IDENTITY_INSERT dbo.tblLocationDesignationPolicy ON;
INSERT INTO dbo.tblLocationDesignationPolicy
    (LocationCategoryId, LocationCategoryName, DesignationId, DesignationName,
     TotalAttendanceFrom, TotalAttendance, WeeklyOff, LocationDesignationPolicyId,
     ForWhichWeeks, [Month-Year], IsActive, IsDeleted, CreatedBy, CreatedOn,
     UpdatedBy, UpdatedOn, TotalAttendanceTo, isActiveBy, isActiveOn)
SELECT
     LocationCategoryId, LocationCategoryName, DesignationId, DesignationName,
     TotalAttendanceFrom, TotalAttendance, WeeklyOff, LocationDesignationPolicyId,
     ForWhichWeeks, [Month-Year], IsActive, IsDeleted, CreatedBy, CreatedOn,
     UpdatedBy, UpdatedOn, TotalAttendanceTo, isActiveBy, isActiveOn
FROM dbo.tblLocationDesignationPolicy_DelBak_Aug26_20260823;
SET IDENTITY_INSERT dbo.tblLocationDesignationPolicy OFF;
-- then, once verified:
-- DROP TABLE dbo.tblLocationDesignationPolicy_DelBak_Aug26_20260823;
--------------------------------------------------------------------------- */

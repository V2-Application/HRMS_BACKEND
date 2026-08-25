/*
    Delete_BudgetWeekoffMaster_Aug2026_20260823.sql   (NOT YET APPLIED)

    Request: delete the Aug-26 data from the weekly off table (PROD:
    192.168.151.28\hrms, database HRMS).

    Which table
    -----------
    Five tables carry "week off". Only ONE holds dated Aug-2026 data:

        BudgetWeekoffMaster                        8,923 rows -> 475 are Aug-2026   <-- target
        EcodeWiseWeekOffMapping                      140 rows -> MONTH is only 'Oct-25' / 'Nov-25'
        BudgetedWeekOffPolicyMaster                   19 rows -> policy master, no date column
        BudgetWeekoffMaster_07112025               3,012 rows -> old backup table, not touched
        LocationDesignationWeeklyOffHolidayMaster      0 rows

    Which rows
    ----------
    "Aug-26" is read as AUGUST 2026 (the month), matching the Jul26 / Jun26
    convention used elsewhere in this database.
        IF_Joining_Date >= '2026-08-01' AND < '2026-09-01'   ->  475 rows, Id 11348..11935

    Rows exist for 2026-08-01 through 2026-08-25 only (19 rows per day, nothing
    generated yet for 26-31 Aug). NOTE: there are ZERO rows dated exactly
    2026-08-26, so if the intent was the single calendar date 26-Aug rather than
    the month, this script is not what you want - stop and say so.

    Spread across 5 locations / 11 designations:
        RH01 HO2 250 | RD04 HO 125 | RH02 HO 50 | Universal 25 | DH24 DC 25

    !! IMPACT - READ BEFORE RUNNING !!
    ----------------------------------
    BudgetWeekoffMaster feeds payroll and week-off calculation:
        sp_CalculateEmployeePayroll_PT_LWF (+ Dev/nik/Working variants)
        fn_GetEmployeeWeekOffs, prc_GetEmployeeWeekOffs, usp_GenerateWeekOffCalendar
    Deleting the current month's rows mid-cycle will change August week-off and
    payroll output for those 5 locations until the calendar is regenerated
    (usp_GenerateWeekOffCalendar). Do not run this during a payroll run.

    Rollback: restore from the backup table created in step 1 (block at bottom).
    Id is an IDENTITY column, so the restore uses IDENTITY_INSERT.
*/

SET XACT_ABORT ON;
BEGIN TRAN;

/* ---------- 1. full-row backup of exactly what will be deleted ---------- */
IF OBJECT_ID('dbo.BudgetWeekoffMaster_DelBak_Aug2026_20260823') IS NOT NULL
    THROW 50001, 'Backup table BudgetWeekoffMaster_DelBak_Aug2026_20260823 already exists - review it before re-running.', 1;

SELECT *
INTO dbo.BudgetWeekoffMaster_DelBak_Aug2026_20260823
FROM dbo.BudgetWeekoffMaster
WHERE IF_Joining_Date >= '2026-08-01' AND IF_Joining_Date < '2026-09-01';

DECLARE @n INT = (SELECT COUNT(*) FROM dbo.BudgetWeekoffMaster_DelBak_Aug2026_20260823);
PRINT CONCAT('rows backed up / to delete: ', @n, '  (expected 475)');

IF @n NOT BETWEEN 1 AND 800
BEGIN
    ROLLBACK TRAN;
    THROW 50002, 'Unexpected row count - nothing deleted. Re-check the scope before applying.', 1;
END

/* ---------- 2. the delete ---------- */
DELETE m
FROM dbo.BudgetWeekoffMaster m
JOIN dbo.BudgetWeekoffMaster_DelBak_Aug2026_20260823 b ON b.Id = m.Id;

PRINT CONCAT('rows deleted: ', @@ROWCOUNT);

COMMIT TRAN;

/* ---------- 3. verification ---------- */
SELECT COUNT(*) AS RemainingAug2026Rows
FROM dbo.BudgetWeekoffMaster
WHERE IF_Joining_Date >= '2026-08-01' AND IF_Joining_Date < '2026-09-01';   -- expect 0

SELECT COUNT(*) AS TotalRowsLeft FROM dbo.BudgetWeekoffMaster;               -- expect 8448

SELECT FORMAT(IF_Joining_Date,'yyyy-MM') AS Month, COUNT(*) AS Rows          -- Aug-2026 should be absent
FROM dbo.BudgetWeekoffMaster GROUP BY FORMAT(IF_Joining_Date,'yyyy-MM') ORDER BY Month DESC;

/* ---------------- ROLLBACK (run only if needed) ----------------
SET IDENTITY_INSERT dbo.BudgetWeekoffMaster ON;
INSERT INTO dbo.BudgetWeekoffMaster
    (Id, IF_Joining_Date, LocationCode, LocationName, DesignationId, DesignationName,
     SatCount, AllowedSaturdays, AllowedSundays, TotalWeekOffs)
SELECT
     Id, IF_Joining_Date, LocationCode, LocationName, DesignationId, DesignationName,
     SatCount, AllowedSaturdays, AllowedSundays, TotalWeekOffs
FROM dbo.BudgetWeekoffMaster_DelBak_Aug2026_20260823;
SET IDENTITY_INSERT dbo.BudgetWeekoffMaster OFF;
-- then, once verified:
-- DROP TABLE dbo.BudgetWeekoffMaster_DelBak_Aug2026_20260823;
--------------------------------------------------------------- */

/*
    Delete_BGTSeats_Dept47_Planning_20260822.sql   (NOT YET APPLIED)

    Request: delete the BGT seats of department PLANNING from BGTSEATMaster (prod).

    Scope resolved from prod on 2026-08-22
    --------------------------------------
    Four departments match '%PLANNING%', but only ONE has any seat rows:

        DepartmentId 47  'PLANNING'          -> 44 active seat rows   <-- deleted by this script
        DepartmentId 380 'AREA PLANNING'     -> 0 rows (dept inactive)
        DepartmentId 415 'PLANNING ALLOCATION' -> 0 rows (dept inactive)
        DepartmentId 41  'RETAIL PLANNING'   -> 0 rows (dept inactive)

    All 44 rows are at ONE store, RH01 (HO-NEW), across 8 designations:
        SR. EXECUTIVE 20, EXECUTIVE 10, MANAGEMENT TRAINEE 6, ASST. MANAGER 3,
        SR. MANAGER 2, MANAGER 1, UPC ST -PLANNING-HEAD 1, JR. EXECUTIVE 1
    There are no ACTIVE=0 rows for dept 47, so 44 is the whole department.

    !! IMPACT - READ BEFORE RUNNING !!
    ----------------------------------
    Deleting these rows sets the budget for RH01 + dept 47 to ZERO. After the
    pool-level seat check applied on 2026-08-21, a zero budget means:

      * 183 active candidates currently in flight for dept PLANNING will be
        refused at joining with "No BGT budget seat is defined for this
        Store / Department / Designation."
      * 24 active employees in dept 47 will sit in a department with no budget,
        and every BGT-vs-actual report will show RH01 PLANNING as 0 budgeted
        against 24 actual.

    Only run this if the budget is genuinely being withdrawn or a corrected
    seat file is being re-uploaded immediately afterwards. If the intent is to
    REPLACE the seats, load the new file first (or in the same window), so
    recruitment is never left at zero.

    Rollback: restore from the backup table created in step 1 (see block at the
    bottom). Id is an IDENTITY column, so the restore uses IDENTITY_INSERT.
*/

SET XACT_ABORT ON;
BEGIN TRAN;

/* ---------- 1. full-row backup of exactly what will be deleted ---------- */
IF OBJECT_ID('dbo.BGTSEATMaster_DelBak_Dept47_Planning_20260822') IS NOT NULL
    THROW 50001, 'Backup table BGTSEATMaster_DelBak_Dept47_Planning_20260822 already exists - review it before re-running.', 1;

SELECT *
INTO dbo.BGTSEATMaster_DelBak_Dept47_Planning_20260822
FROM dbo.BGTSEATMaster
WHERE DEPT_SNO = '47';

DECLARE @n INT = (SELECT COUNT(*) FROM dbo.BGTSEATMaster_DelBak_Dept47_Planning_20260822);
PRINT CONCAT('rows backed up / to delete: ', @n, '  (expected 44)');

IF @n NOT BETWEEN 1 AND 100
BEGIN
    ROLLBACK TRAN;
    THROW 50002, 'Unexpected row count - nothing deleted. Re-check the scope before applying.', 1;
END

/* ---------- 2. the delete ---------- */
DELETE m
FROM dbo.BGTSEATMaster m
JOIN dbo.BGTSEATMaster_DelBak_Dept47_Planning_20260822 b ON b.Id = m.Id;

PRINT CONCAT('rows deleted: ', @@ROWCOUNT);

COMMIT TRAN;

/* ---------- 3. verification: expect zero rows left for dept 47 ---------- */
SELECT COUNT(*) AS RemainingDept47Rows FROM dbo.BGTSEATMaster WHERE DEPT_SNO = '47';

-- RH01 (HO-NEW) is LocationId 313. Expect SeatBudget 0, IsAvailable 0 and the
-- "No BGT budget seat is defined" message for every PLANNING designation now.
EXEC dbo.usp_CheckCandidateSeatAvailability @LocationId = 313, @DepartmentId = 47,
     @DesignationId = 9, @Salary = NULL;   -- SR. EXECUTIVE (the largest block: 20 of the 44 seats)

/* ---------------- ROLLBACK (run only if needed) ----------------
SET IDENTITY_INSERT dbo.BGTSEATMaster ON;
INSERT INTO dbo.BGTSEATMaster
    (Id, LOC_CODE, DEPT_SNO, DEPARTMENT, DESG_SNO, DESIGNATION, SEAT_MASTER_NO,
     SALARY_BGT, ORG_CHART, REPORTING_MANAGER, ACTIVE, SubDepartment1, SubDepartment2, SubDepartment3)
SELECT
     Id, LOC_CODE, DEPT_SNO, DEPARTMENT, DESG_SNO, DESIGNATION, SEAT_MASTER_NO,
     SALARY_BGT, ORG_CHART, REPORTING_MANAGER, ACTIVE, SubDepartment1, SubDepartment2, SubDepartment3
FROM dbo.BGTSEATMaster_DelBak_Dept47_Planning_20260822;
SET IDENTITY_INSERT dbo.BGTSEATMaster OFF;
-- then, once verified:
-- DROP TABLE dbo.BGTSEATMaster_DelBak_Dept47_Planning_20260822;
--------------------------------------------------------------- */

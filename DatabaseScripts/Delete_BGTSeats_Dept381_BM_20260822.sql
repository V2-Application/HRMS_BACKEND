/*
    Delete_BGTSeats_Dept381_BM_20260822.sql   (NOT YET APPLIED)

    Request: delete the BGT seats of department B&M from BGTSEATMaster (PROD:
    192.168.151.28\hrms, database HRMS).

    Scope resolved from prod on 2026-08-22
    --------------------------------------
    Exactly one department matches: DepartmentId 381 'B&M'.
    218 seat rows, ALL ACTIVE=1 (no inactive rows), across 3 stores / 11 designations:

        RH01 HO-NEW  : EXECUTIVE 83, SR. EXECUTIVE 58, MANAGER 14, ASST. MANAGER 8,
                       MANAGEMENT 2, SR. MANAGER 2, JR. EXECUTIVE 2,
                       MANAGEMENT TRAINEE 1, BUYER 1, LAB INCHARGE 1, MERCHANT 1   = 173
        RH02 Central : EXECUTIVE 23                                                =  23
        RD04 HO-OLD  : EXECUTIVE 11, JR. EXECUTIVE 7, ASST. MANAGER 2, SR. EXEC 2   =  22
                                                                          total   = 218

    !! IMPACT - READ BEFORE RUNNING !!
    ----------------------------------
    This takes the B&M budget to ZERO at all three locations. With the pool-level
    seat check live (applied 2026-08-21):

      * 804 active candidates are in flight for B&M - all will be refused at
        joining with "No BGT budget seat is defined for this Store / Department /
        Designation."  That is the largest in-flight pipeline of any department
        touched so far; confirm with recruitment before running.
      * 185 active B&M employees will sit against zero budget, so BGT-vs-actual
        reports read -185 until a replacement file is uploaded.

    Note: B&M seats have been deleted and re-uploaded repeatedly (backups exist
    from 2026-07-25, and RH01-only slices on 08-18 and 08-19). If this is another
    replace cycle, upload the new file in the same window so recruitment is never
    left at zero.

    Rollback: restore from the backup table created in step 1 (block at bottom).
    Id is an IDENTITY column, so the restore uses IDENTITY_INSERT.
*/

SET XACT_ABORT ON;
BEGIN TRAN;

/* ---------- 1. full-row backup of exactly what will be deleted ---------- */
IF OBJECT_ID('dbo.BGTSEATMaster_DelBak_Dept381_BM_20260822') IS NOT NULL
    THROW 50001, 'Backup table BGTSEATMaster_DelBak_Dept381_BM_20260822 already exists - review it before re-running.', 1;

SELECT *
INTO dbo.BGTSEATMaster_DelBak_Dept381_BM_20260822
FROM dbo.BGTSEATMaster
WHERE DEPT_SNO = '381';

DECLARE @n INT = (SELECT COUNT(*) FROM dbo.BGTSEATMaster_DelBak_Dept381_BM_20260822);
PRINT CONCAT('rows backed up / to delete: ', @n, '  (expected 218)');

IF @n NOT BETWEEN 1 AND 400
BEGIN
    ROLLBACK TRAN;
    THROW 50002, 'Unexpected row count - nothing deleted. Re-check the scope before applying.', 1;
END

/* ---------- 2. the delete ---------- */
DELETE m
FROM dbo.BGTSEATMaster m
JOIN dbo.BGTSEATMaster_DelBak_Dept381_BM_20260822 b ON b.Id = m.Id;

PRINT CONCAT('rows deleted: ', @@ROWCOUNT);

COMMIT TRAN;

/* ---------- 3. verification ---------- */
SELECT COUNT(*) AS RemainingDept381Rows FROM dbo.BGTSEATMaster WHERE DEPT_SNO = '381';

-- RH01 (HO-NEW) is LocationId 313. Expect SeatBudget 0, IsAvailable 0 and the
-- "No BGT budget seat is defined" message.
EXEC dbo.usp_CheckCandidateSeatAvailability @LocationId = 313, @DepartmentId = 381,
     @DesignationId = 4, @Salary = NULL;   -- EXECUTIVE (the largest block: 83 of the 218 seats)

/* ---------------- ROLLBACK (run only if needed) ----------------
SET IDENTITY_INSERT dbo.BGTSEATMaster ON;
INSERT INTO dbo.BGTSEATMaster
    (Id, LOC_CODE, DEPT_SNO, DEPARTMENT, DESG_SNO, DESIGNATION, SEAT_MASTER_NO,
     SALARY_BGT, ORG_CHART, REPORTING_MANAGER, ACTIVE, SubDepartment1, SubDepartment2, SubDepartment3)
SELECT
     Id, LOC_CODE, DEPT_SNO, DEPARTMENT, DESG_SNO, DESIGNATION, SEAT_MASTER_NO,
     SALARY_BGT, ORG_CHART, REPORTING_MANAGER, ACTIVE, SubDepartment1, SubDepartment2, SubDepartment3
FROM dbo.BGTSEATMaster_DelBak_Dept381_BM_20260822;
SET IDENTITY_INSERT dbo.BGTSEATMaster OFF;
-- then, once verified:
-- DROP TABLE dbo.BGTSEATMaster_DelBak_Dept381_BM_20260822;
--------------------------------------------------------------- */

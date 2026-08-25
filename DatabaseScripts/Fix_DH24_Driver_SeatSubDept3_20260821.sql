/*
    Fix_DH24_Driver_SeatSubDept3_20260821.sql   (NOT YET APPLIED)

    Purpose
    -------
    DH24 (FRKH-NGR-RDC) / DC-OPS-V2 (661) / DRIVER (72): the vacancy grid shows
    193 budgeted seats vs 112 active employees = 81 vacant, but joining fails
    with the BGT / seat-not-available error because the seat check matches on the
    EXACT SubDept1+2+3 bucket and the budget is split like this:

        SD1 CLA & LOGISTICS / SD2 FRKH-NGR-RDC / SD3 FRKH-NGR-RDC : 100 seats, 102 emps  -> FULL (over by 2)
        SD1 CLA & LOGISTICS / SD2 FRKH-NGR-RDC / SD3 (blank)      :  93 seats,   2 emps  -> 91 free, unreachable
        SD1 SCM / SD2 CLA & LOGISTICS / SD3 CLA & LOGISTICS       :   0 seats,   8 emps

    The 93 rows were uploaded to BGTSEATMaster without SubDepartment3, while every
    DRIVER employee at that store carries SubDepartment3 = FRKH-NGR-RDC. Budget and
    headcount therefore land in different buckets for the same job, and the 91 free
    seats cannot be selected from the candidate form (SubDept3 cannot be left blank).

    What this does
    --------------
    Stamps SubDepartment3 = 'FRKH-NGR-RDC' on those 93 active seat rows only, so the
    budget bucket matches where the employees actually sit:
        193 seats vs 102 employees -> 91 free -> joining allowed.

    Scope: DH24 + dept 661 + desig 72 + SD1 'CLA & LOGISTICS' + SD2 'FRKH-NGR-RDC'
           + SD3 blank + ACTIVE. Expected row count: 93. No other store is touched.

    This fixes DH24 only. The same fault (seat rows with a blank SubDepartment3)
    affects 1,991 active seat rows prod-wide, and 4,256 of 15,893 active employees
    currently sit in a sub-dept bucket with zero budgeted seats. The general fix is
    Fix_SeatCheck_PoolLevel_20260821.sql (check at Store+Dept+Designation, the same
    key the vacancy grid uses).
*/

SET XACT_ABORT ON;
BEGIN TRAN;

-- 1. before/after backup of exactly the rows being changed (keep for rollback)
IF OBJECT_ID('dbo.BGTSEATMaster_SD3Fix_Backup_20260821') IS NOT NULL
    THROW 50001, 'Backup table BGTSEATMaster_SD3Fix_Backup_20260821 already exists - review it before re-running.', 1;

SELECT Id, SEAT_MASTER_NO, LOC_CODE, DEPT_SNO, DESG_SNO,
       SubDepartment1, SubDepartment2, SubDepartment3 AS SubDepartment3_Old,
       ACTIVE, GETDATE() AS CapturedOn
INTO dbo.BGTSEATMaster_SD3Fix_Backup_20260821
FROM dbo.BGTSEATMaster
WHERE ISNULL(ACTIVE,1) = 1
  AND UPPER(LTRIM(RTRIM(LOC_CODE))) = 'DH24'
  AND DEPT_SNO = '661'
  AND DESG_SNO = '72'
  AND UPPER(LTRIM(RTRIM(SubDepartment1))) = 'CLA & LOGISTICS'
  AND UPPER(LTRIM(RTRIM(SubDepartment2))) = 'FRKH-NGR-RDC'
  AND ISNULL(LTRIM(RTRIM(SubDepartment3)),'') = '';

DECLARE @n INT = (SELECT COUNT(*) FROM dbo.BGTSEATMaster_SD3Fix_Backup_20260821);
PRINT CONCAT('rows to update: ', @n, ' (expected 93)');

IF @n NOT BETWEEN 1 AND 200
BEGIN
    ROLLBACK TRAN;
    THROW 50002, 'Unexpected row count - nothing changed. Re-check the filter before applying.', 1;
END

-- 2. the fix
UPDATE m
SET m.SubDepartment3 = 'FRKH-NGR-RDC'
FROM dbo.BGTSEATMaster m
JOIN dbo.BGTSEATMaster_SD3Fix_Backup_20260821 b ON b.Id = m.Id;

PRINT CONCAT('rows updated: ', @@ROWCOUNT);

COMMIT TRAN;

/* 3. verification - expect 193 seats / 102 emps / 91 free in the single bucket */
SELECT ISNULL(UPPER(LTRIM(RTRIM(SubDepartment1))),'') SD1,
       ISNULL(UPPER(LTRIM(RTRIM(SubDepartment2))),'') SD2,
       ISNULL(UPPER(LTRIM(RTRIM(SubDepartment3))),'') SD3,
       COUNT(*) Seats
FROM dbo.BGTSEATMaster
WHERE ISNULL(ACTIVE,1)=1 AND UPPER(LTRIM(RTRIM(LOC_CODE)))='DH24' AND DEPT_SNO='661' AND DESG_SNO='72'
GROUP BY ISNULL(UPPER(LTRIM(RTRIM(SubDepartment1))),''), ISNULL(UPPER(LTRIM(RTRIM(SubDepartment2))),''), ISNULL(UPPER(LTRIM(RTRIM(SubDepartment3))),'');

EXEC dbo.usp_CheckCandidateSeatAvailability @LocationId=2438, @DepartmentId=661,
     @SubDepartmentId1=847, @SubDepartmentId2=975, @SubDepartmentId3=976,
     @DesignationId=72, @Salary=20000;   -- expect IsAvailable = 1

/* ---------------- ROLLBACK (run only if needed) ----------------
UPDATE m SET m.SubDepartment3 = b.SubDepartment3_Old
FROM dbo.BGTSEATMaster m
JOIN dbo.BGTSEATMaster_SD3Fix_Backup_20260821 b ON b.Id = m.Id;
DROP TABLE dbo.BGTSEATMaster_SD3Fix_Backup_20260821;
--------------------------------------------------------------- */

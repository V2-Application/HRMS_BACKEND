/*
    Fix_fn_CanCreateCandidate_EmployeesOnly_20260824.sql   (NOT YET APPLIED)
    PROD: 192.168.151.28\hrms, database HRMS

    Rule (confirmed 2026-08-24): a seat is occupied by an ACTIVE EMPLOYEE only.
    Candidates in the pipeline never consume a budgeted seat.

    Audit of the four seat gates against that rule:
        usp_CheckCandidateSeatAvailability  employees only          OK (fixed 2026-08-21)
        fn_IsVacancyShorter                 employees only          OK (fixed 2026-08-21)
        fn_IsVacancyShorterForEmployee      employees only          OK (fixed 2026-08-21)
        fn_CanCreateCandidate               COUNTS CANDIDATES       <-- fixed here

    The old body counted rows in dbo.Candidate for the same Location/Dept/Desig
    and refused when candidates >= seats, so a store with free seats but a busy
    pipeline was blocked ("BGT not available"), and employees who actually
    occupied seats were never counted at all.

    This version:
      * counts ACTIVE, non-deleted employees at the same STCode + Department +
        Designation (same pool key as the other three gates),
      * keeps the signature (VARCHAR LocationId/DeptId/DesgId) so callers bind
        unchanged,
      * trims/uppercases LOC_CODE like the other gates do,
      * still returns 0 when the location is invalid or no seat exists.

    Previous definition captured below in the ROLLBACK block.
*/

ALTER FUNCTION dbo.fn_CanCreateCandidate
(
    @LocationId VARCHAR(50),   -- coming from frontend (tblLocation.LocationId as text)
    @DeptId     VARCHAR(50),
    @DesgId     VARCHAR(50)
)
RETURNS BIT
AS
BEGIN
    DECLARE @LocCode   VARCHAR(50);
    DECLARE @SeatCount INT = 0;
    DECLARE @EmpCount  INT = 0;
    DECLARE @LocIdInt  BIGINT = TRY_CAST(@LocationId AS BIGINT);

    IF (@LocIdInt IS NULL)
        RETURN 0;

    SELECT @LocCode = STCode
    FROM dbo.tblLocation
    WHERE LocationId = @LocIdInt
      AND ISNULL(IsDeleted,0) = 0;

    IF (@LocCode IS NULL)
        RETURN 0;

    -- budgeted seats for the pool
    SELECT @SeatCount = COUNT(*)
    FROM dbo.BGTSEATMaster
    WHERE UPPER(LTRIM(RTRIM(LOC_CODE))) = UPPER(LTRIM(RTRIM(@LocCode)))
      AND DEPT_SNO = @DeptId
      AND DESG_SNO = @DesgId
      AND ISNULL(ACTIVE,1) = 1;

    IF (@SeatCount = 0)
        RETURN 0;

    -- occupancy = ACTIVE EMPLOYEES only (candidates are NOT counted)
    SELECT @EmpCount = COUNT(*)
    FROM dbo.tblEmployee e
    JOIN dbo.tblLocation l ON l.LocationId = e.LocationId
    WHERE UPPER(LTRIM(RTRIM(l.STCode))) = UPPER(LTRIM(RTRIM(@LocCode)))
      AND CAST(e.DepartmentId  AS VARCHAR(50)) = @DeptId
      AND CAST(e.DesignationId AS VARCHAR(50)) = @DesgId
      AND e.IsActive = 1
      AND ISNULL(e.IsDeleted,0) = 0;

    IF (@EmpCount < @SeatCount)
        RETURN 1;

    RETURN 0;
END

/* ---------------- VERIFICATION (run after applying) ----------------
-- HJ10 (LocationId 70) / RETAIL OPERATIONS 570:
--   NAPS LOBM 1445 -> 2 seats, 0 employees  -> expect 1
--   LOBM       96  -> 12 seats, 11 employees -> expect 1
--   CASHIER    27  -> 4 seats, 4 employees   -> expect 0
--   NAPS SG  1446  -> 0 seats                -> expect 0
SELECT dbo.fn_CanCreateCandidate('70','570','1445') AS NAPS_LOBM,
       dbo.fn_CanCreateCandidate('70','570','96')   AS LOBM,
       dbo.fn_CanCreateCandidate('70','570','27')   AS CASHIER,
       dbo.fn_CanCreateCandidate('70','570','1446') AS NAPS_SG;
--------------------------------------------------------------------- */

/* ---------------- ROLLBACK: original definition (captured from prod 2026-08-24)
ALTER FUNCTION dbo.fn_CanCreateCandidate
(
    @LocationId VARCHAR(50),
    @DeptId     VARCHAR(50),
    @DesgId     VARCHAR(50)
)
RETURNS BIT
AS
BEGIN
    DECLARE @LocCode   VARCHAR(50);
    DECLARE @SeatCount INT = 0;
    DECLARE @CandCount INT = 0;
    DECLARE @LocIdInt BIGINT;
    SET @LocIdInt = TRY_CAST(@LocationId AS BIGINT);
    IF (@LocIdInt IS NULL) RETURN 0;
    SELECT @LocCode = STCode FROM tblLocation WHERE LocationId = @LocIdInt AND ISNULL(IsDeleted,0) = 0;
    IF (@LocCode IS NULL) RETURN 0;
    SELECT @SeatCount = COUNT(*) FROM BGTSEATMaster
     WHERE UPPER(LOC_CODE) = UPPER(@LocCode) AND DEPT_SNO = @DeptId AND DESG_SNO = @DesgId AND ISNULL(ACTIVE,1) = 1;
    IF (@SeatCount = 0) RETURN 0;
    SELECT @CandCount = COUNT(*) FROM Candidate c
     WHERE c.[LOCATION] = @LocationId AND c.DEPARTMENT = @DeptId AND c.DESIGNATION = @DesgId
       AND c.IsActive = 1 AND ISNULL(c.IsDeleted,0) = 0;
    IF (@CandCount < @SeatCount) RETURN 1;
    RETURN 0;
END
--------------------------------------------------------------------- */

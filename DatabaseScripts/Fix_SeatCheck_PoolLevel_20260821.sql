/*
    Fix_SeatCheck_PoolLevel_20260821.sql
    APPLIED TO PROD 2026-08-21 14:06 (verified: DH24/661/72 -> 193 seats, 112 emps,
    81 vacant, IsAvailable = 1 for every sub-dept combo).

    Problem
    -------
    usp_CheckCandidateSeatAvailability decides availability on an EXACT
    Store + Dept + SubDept1 + SubDept2 + SubDept3 + Designation bucket, while
    every budget/vacancy report the business reads (proc_Openings,
    fn_IsVacancyShorter, the vacant-seat grid) pools at
    Store + Dept + Designation. The two disagree constantly:

      DH24 (FRKH-NGR-RDC) / DC-OPS-V2 / DRIVER -- 193 active seats, 112 active
      employees => the report shows 81 vacant, but the sub-dept buckets are

        SD1 CLA & LOGISTICS / SD2 FRKH-NGR-RDC / SD3 FRKH-NGR-RDC : 100 seats, 102 emps -> BLOCKED (over-filled)
        SD1 CLA & LOGISTICS / SD2 FRKH-NGR-RDC / SD3 (blank)      :  93 seats,   2 emps -> the free seats live here
        SD1 SCM / SD2 CLA & LOGISTICS / SD3 CLA & LOGISTICS       :   0 seats,   8 emps -> "No BGT budget seat is defined"

    Prod-wide: 4,256 of 15,893 active employees (27%) sit in a sub-dept bucket
    that has ZERO budgeted seats, because 1,991 active seat rows carry a blank
    SubDepartment3 and tblSubDepartment holds many duplicate names per
    department (dept 661 alone has 22 rows named 'FRKH-NGR-RDC').

    Fix
    ---
    Decide availability at the SAME granularity the budget is reported at:
    LOC_CODE + DEPT_SNO + DESG_SNO. Sub-department names are still returned for
    display, but they no longer veto the hire. Column list and order are
    unchanged -> no caller changes needed.

    Exemptions (new store <= 60 days, UPC store) and the salary-vs-SALARY_BGT
    guard are kept exactly as they are today.

    Previous definition: BACKUP_usp_CheckCandidateSeatAvailability_Original_20260821.sql
    plus Fix_SeatCheck_CountEmployeesOnly_20260821.sql (employees-only occupancy).
*/

ALTER PROCEDURE dbo.usp_CheckCandidateSeatAvailability
(
    @LocationId INT,
    @DepartmentId INT,
    @SubDepartmentId1 INT = NULL,
    @SubDepartmentId2 INT = NULL,
    @SubDepartmentId3 INT = NULL,
    @DesignationId INT,
    @Salary DECIMAL(18,2) = NULL,
    @ExcludeCandidateId BIGINT = NULL
)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @STCode NVARCHAR(50) = (SELECT STCode FROM tblLocation WHERE LocationId = @LocationId);
    DECLARE @LocationIdStr VARCHAR(50) = CAST(@LocationId AS VARCHAR(50));
    DECLARE @DepartmentIdStr VARCHAR(50) = CAST(@DepartmentId AS VARCHAR(50));
    DECLARE @DesignationIdStr VARCHAR(50) = CAST(@DesignationId AS VARCHAR(50));

    DECLARE @SubDept1Name NVARCHAR(255) = (SELECT LTRIM(RTRIM(SubDepartmentName)) FROM tblSubDepartment WHERE SubDepartmentId = @SubDepartmentId1);
    DECLARE @SubDept2Name NVARCHAR(255) = (SELECT LTRIM(RTRIM(SubDepartmentName)) FROM tblSubDepartment WHERE SubDepartmentId = @SubDepartmentId2);
    DECLARE @SubDept3Name NVARCHAR(255) = (SELECT LTRIM(RTRIM(SubDepartmentName)) FROM tblSubDepartment WHERE SubDepartmentId = @SubDepartmentId3);

    DECLARE @DepartmentName NVARCHAR(255) = (SELECT DepartmentName FROM tblDepartment WHERE DepartmentId = @DepartmentId);
    DECLARE @DesignationName NVARCHAR(255) = (SELECT DesignationName FROM tblDesignation WHERE DesignationId = @DesignationId);
    DECLARE @LocationName NVARCHAR(255) = (SELECT LocationName FROM tblLocation WHERE LocationId = @LocationId);

    IF @STCode IS NULL
    BEGIN
        SELECT
            0 AS SeatBudget, 0 AS FilledByEmployees, 0 AS FilledByCandidates, 0 AS Occupied, 0 AS Vacancy,
            CAST(NULL AS DECIMAL(18,2)) AS MaxBudgetedSalary, CAST(0 AS BIT) AS IsAvailable,
            @LocationName AS LocationName, @DepartmentName AS DepartmentName,
            @SubDept1Name AS SubDepartmentName1, @SubDept2Name AS SubDepartmentName2, @SubDept3Name AS SubDepartmentName3,
            @DesignationName AS DesignationName,
            'Invalid location.' AS Message;
        RETURN;
    END

    -- Exemption check: new store (opened <= 60 days ago) or UPC store (IsActive = 0/NULL).
    DECLARE @LocIsActive BIT, @OpeningDateRaw NVARCHAR(50), @DaysSinceOpening INT = NULL;
    SELECT @LocIsActive = IsActive, @OpeningDateRaw = OpeningDate FROM tblLocation WHERE LocationId = @LocationId;

    IF TRY_CONVERT(date, @OpeningDateRaw) IS NOT NULL
        SET @DaysSinceOpening = DATEDIFF(day, TRY_CONVERT(date, @OpeningDateRaw), GETDATE());

    IF ISNULL(@LocIsActive,0) = 0 OR (@DaysSinceOpening IS NOT NULL AND @DaysSinceOpening <= 60)
    BEGIN
        SELECT
            0 AS SeatBudget, 0 AS FilledByEmployees, 0 AS FilledByCandidates, 0 AS Occupied, 0 AS Vacancy,
            CAST(NULL AS DECIMAL(18,2)) AS MaxBudgetedSalary, CAST(1 AS BIT) AS IsAvailable,
            @LocationName AS LocationName, @DepartmentName AS DepartmentName,
            @SubDept1Name AS SubDepartmentName1, @SubDept2Name AS SubDepartmentName2, @SubDept3Name AS SubDepartmentName3,
            @DesignationName AS DesignationName,
            CASE
                WHEN ISNULL(@LocIsActive,0) = 0 THEN 'Store is UPC (not yet active) -- no budget restriction applies.'
                ELSE 'Store opened within the last 60 days -- no budget restriction applies.'
            END AS Message;
        RETURN;
    END

    /* ---------- budget pool: Store + Department + Designation (sub-dept NOT part of the key) ---------- */
    DECLARE @SeatBudget INT = 0, @MaxSalaryBgt DECIMAL(18,2) = NULL;

    SELECT @SeatBudget = COUNT(*), @MaxSalaryBgt = MAX(SALARY_BGT)
    FROM BGTSEATMaster
    WHERE ISNULL(ACTIVE,1) = 1
      AND UPPER(LTRIM(RTRIM(LOC_CODE))) = UPPER(@STCode)
      AND DEPT_SNO = @DepartmentIdStr
      AND DESG_SNO = @DesignationIdStr;

    DECLARE @EmpCount INT = 0;

    SELECT @EmpCount = COUNT(*)
    FROM tblEmployee e
    LEFT JOIN tblLocation loc ON e.LocationId = loc.LocationId
    WHERE e.IsActive = 1 AND ISNULL(e.IsDeleted,0) = 0
      AND UPPER(LTRIM(RTRIM(loc.STCode))) = UPPER(@STCode)
      AND e.DepartmentId = @DepartmentId
      AND e.DesignationId = @DesignationId;

    -- candidates in the same pool: reported for visibility, NOT counted as occupancy (2026-08-21)
    DECLARE @CandCount INT = 0;

    SELECT @CandCount = COUNT(*)
    FROM Candidate c
    WHERE ISNULL(c.IsActive,1) = 1 AND ISNULL(c.IsDeleted,0) = 0
      AND (@ExcludeCandidateId IS NULL OR c.Id <> @ExcludeCandidateId)
      AND LTRIM(RTRIM(c.LOCATION)) = @LocationIdStr
      AND LTRIM(RTRIM(c.DEPARTMENT)) = @DepartmentIdStr
      AND LTRIM(RTRIM(c.DESIGNATION)) = @DesignationIdStr;

    DECLARE @Occupied INT = @EmpCount;
    DECLARE @Vacancy INT = @SeatBudget - @Occupied;

    DECLARE @IsAvailable BIT = CASE
        WHEN @SeatBudget > 0
             AND @Occupied < @SeatBudget
             AND (@Salary IS NULL OR @MaxSalaryBgt IS NULL OR @Salary <= @MaxSalaryBgt)
        THEN 1 ELSE 0 END;

    DECLARE @Message NVARCHAR(500) =
        CASE
            WHEN @SeatBudget = 0 THEN 'No BGT budget seat is defined for this Store / Department / Designation. Please increase the budget (BGT Seat Master) before hiring.'
            WHEN @Occupied >= @SeatBudget THEN 'All budgeted seats for this Store / Department / Designation are already filled by active employees. Please increase the budget (BGT Seat Master) before hiring.'
            WHEN @Salary IS NOT NULL AND @MaxSalaryBgt IS NOT NULL AND @Salary > @MaxSalaryBgt THEN 'Offered gross salary exceeds the budgeted salary for this seat.'
            ELSE NULL
        END;

    SELECT
        @SeatBudget AS SeatBudget, @EmpCount AS FilledByEmployees, @CandCount AS FilledByCandidates,
        @Occupied AS Occupied, @Vacancy AS Vacancy, @MaxSalaryBgt AS MaxBudgetedSalary, @IsAvailable AS IsAvailable,
        @LocationName AS LocationName, @DepartmentName AS DepartmentName,
        @SubDept1Name AS SubDepartmentName1, @SubDept2Name AS SubDepartmentName2, @SubDept3Name AS SubDepartmentName3,
        @DesignationName AS DesignationName,
        @Message AS Message;
END

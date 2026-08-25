-- New, purely additive stored proc (no existing object touched, no data changed).
--
-- "Freeze the budget": a candidate can only be hired into a Store + Department +
-- Sub-Department(1/2/3) + Designation combination if there is an unfilled BGT
-- (budget) seat for it. A seat is considered filled by either an active employee
-- OR another active/in-pipeline candidate already occupying it (so two recruiters
-- can't both "claim" the same last open seat).
--
-- Used by:
--   - CandidateController.CheckSeatAvailability (on-select check while filling the form)
--   - CandidateService.UpdateData (hard block on submit, new candidate)
--   - CandidateService.CandidateInitiate (hard block on approval-stage progression)
--
-- Exemptions (2026-08-11): the freeze does NOT apply — hiring is unrestricted — when:
--   1) The store opened <= 60 days ago (new stores need to staff up before BGT catches up), or
--   2) The store is "UPC" (tblLocation.IsActive = 0/NULL — see LocationCodeMaster.jsx's
--      statusLabel: Active = IsActive true, everything else is labelled UPC).

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
                WHEN ISNULL(@LocIsActive,0) = 0 THEN 'Store is UPC (not yet active) — no budget restriction applies.'
                ELSE 'Store opened within the last 60 days — no budget restriction applies.'
            END AS Message;
        RETURN;
    END

    DECLARE @SeatBudget INT = 0, @MaxSalaryBgt DECIMAL(18,2) = NULL;

    SELECT @SeatBudget = COUNT(*), @MaxSalaryBgt = MAX(SALARY_BGT)
    FROM BGTSEATMaster
    WHERE ISNULL(ACTIVE,1) = 1
      AND UPPER(LTRIM(RTRIM(LOC_CODE))) = UPPER(@STCode)
      AND DEPT_SNO = @DepartmentIdStr
      AND DESG_SNO = @DesignationIdStr
      AND ISNULL(UPPER(LTRIM(RTRIM(SubDepartment1))),'') = ISNULL(UPPER(@SubDept1Name),'')
      AND ISNULL(UPPER(LTRIM(RTRIM(SubDepartment2))),'') = ISNULL(UPPER(@SubDept2Name),'')
      AND ISNULL(UPPER(LTRIM(RTRIM(SubDepartment3))),'') = ISNULL(UPPER(@SubDept3Name),'');

    DECLARE @EmpCount INT = 0;

    SELECT @EmpCount = COUNT(*)
    FROM tblEmployee e
    LEFT JOIN tblLocation loc ON e.LocationId = loc.LocationId
    LEFT JOIN tblSubDepartment se1 ON se1.SubDepartmentId = e.SubDepartmentId1
    LEFT JOIN tblSubDepartment se2 ON se2.SubDepartmentId = e.SubDepartmentId2
    LEFT JOIN tblSubDepartment se3 ON se3.SubDepartmentId = e.SubDepartmentId3
    WHERE e.IsActive = 1 AND ISNULL(e.IsDeleted,0) = 0
      AND UPPER(LTRIM(RTRIM(loc.STCode))) = UPPER(@STCode)
      AND e.DepartmentId = @DepartmentId
      AND e.DesignationId = @DesignationId
      AND ISNULL(UPPER(LTRIM(RTRIM(se1.SubDepartmentName))),'') = ISNULL(UPPER(@SubDept1Name),'')
      AND ISNULL(UPPER(LTRIM(RTRIM(se2.SubDepartmentName))),'') = ISNULL(UPPER(@SubDept2Name),'')
      AND ISNULL(UPPER(LTRIM(RTRIM(se3.SubDepartmentName))),'') = ISNULL(UPPER(@SubDept3Name),'');

    DECLARE @CandCount INT = 0;

    SELECT @CandCount = COUNT(*)
    FROM Candidate c
    WHERE ISNULL(c.IsActive,1) = 1 AND ISNULL(c.IsDeleted,0) = 0
      AND (@ExcludeCandidateId IS NULL OR c.Id <> @ExcludeCandidateId)
      AND LTRIM(RTRIM(c.LOCATION)) = @LocationIdStr
      AND LTRIM(RTRIM(c.DEPARTMENT)) = @DepartmentIdStr
      AND LTRIM(RTRIM(c.DESIGNATION)) = @DesignationIdStr
      AND ISNULL(c.SubDepartmentId1,0) = ISNULL(@SubDepartmentId1,0)
      AND ISNULL(c.SubDepartmentId2,0) = ISNULL(@SubDepartmentId2,0)
      AND ISNULL(c.SubDepartmentId3,0) = ISNULL(@SubDepartmentId3,0);

    DECLARE @Occupied INT = @EmpCount;   -- employees only; candidates reported but NOT counted (2026-08-21)
    DECLARE @Vacancy INT = @SeatBudget - @Occupied;

    DECLARE @IsAvailable BIT = CASE
        WHEN @SeatBudget > 0
             AND @Occupied < @SeatBudget
             AND (@Salary IS NULL OR @MaxSalaryBgt IS NULL OR @Salary <= @MaxSalaryBgt)
        THEN 1 ELSE 0 END;

    DECLARE @Message NVARCHAR(500) =
        CASE
            WHEN @SeatBudget = 0 THEN 'No BGT budget seat is defined for this Store / Department / Sub-Department / Designation combination. Please increase the budget (BGT Seat Master) before hiring.'
            WHEN @Occupied >= @SeatBudget THEN 'All budgeted seats for this Store / Department / Sub-Department / Designation are already filled by active employees. Please increase the budget (BGT Seat Master) before hiring.'
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


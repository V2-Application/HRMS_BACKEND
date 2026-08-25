/*
    Rollback_SeatVacancy_To_PreToday_20260821.sql   (NOT APPLIED)

    Restores the three pre-2026-08-21 definitions exactly as captured from PROD:
      dbo.fn_IsVacancyShorterForEmployee   (EmpCount < Vacancy AND @Salary <= SALARY_BGT)
      dbo.fn_IsVacancyShorter              (same)
      dbo.usp_CheckCandidateSeatAvailability (sub-dept-exact, candidates counted as occupancy)

    WARNING - this re-introduces the two blockers found today:
      1. 14,323 of 16,520 active seats have SALARY_BGT = NULL, and "@Salary <= NULL"
         is UNKNOWN, never TRUE -> those seats can never be filled at any salary.
      2. "EmpCount < Vacancy" reduces to EmpCount < SeatBudget/2 -> demands the pool
         be less than half staffed rather than merely having a free seat.
    Sources: BACKUP_VacancyFunctions_Original_20260821.sql,
             BACKUP_usp_CheckCandidateSeatAvailability_Original_20260821.sql
*/

/* ================= ORIGINAL: dbo.fn_IsVacancyShorterForEmployee  (captured from PROD 2026-08-21) ================= */
ALTER FUNCTION dbo.fn_IsVacancyShorterForEmployee
(
    @LocId   Int,  
    @DeptId    INT,
    @DesigId   INT,
	@Salary decimal(18,2)
)
RETURNS BIT
AS
BEGIN
    DECLARE @Result BIT = 0;
	 Declare @ST_CODE nvarchar(10);
	  Select @ST_CODE=STCode from tblLocation(NOLOCK)
	  where LocationId=@LocId

    ;WITH emp AS (
        SELECT
            b.STCode        AS LOC_CODE,
            d.DepartmentId  AS DEPT_SNO,
            c.DesignationId AS DESG_SNO
        FROM HRMS.dbo.tblEmployee a WITH (NOLOCK)
        LEFT JOIN HRMS.dbo.tblLocation    b WITH (NOLOCK) ON a.LocationId    = b.LocationId
        LEFT JOIN HRMS.dbo.tblDesignation c WITH (NOLOCK) ON a.DesignationId = c.DesignationId
        LEFT JOIN HRMS.dbo.tblDepartment  d WITH (NOLOCK) ON a.DepartmentId  = d.DepartmentId
        WHERE a.IsActive = 1
    ),
    emp_counts AS (
        SELECT
            LOC_CODE, DEPT_SNO, DESG_SNO,
            COUNT(*) AS EmpCount
        FROM emp
        GROUP BY LOC_CODE, DEPT_SNO, DESG_SNO
    ),
    seat_counts AS (
        SELECT
            m.LOC_CODE, m.DEPT_SNO, m.DESG_SNO,
			MAX(m.SALARY_BGT) SALARY_BGT,
            COUNT(*) AS SeatBudget
        FROM HRMS.dbo.BGTSEATMaster m WITH (NOLOCK)
        WHERE m.ACTIVE = 1
        GROUP BY m.LOC_CODE, m.DEPT_SNO, m.DESG_SNO
    ),
    vacancy_counts AS (
        SELECT
            s.LOC_CODE,
            s.DEPT_SNO,
            s.DESG_SNO,
			s.SALARY_BGT,
            s.SeatBudget,
            ISNULL(e.EmpCount, 0) AS EmpCount,
            (s.SeatBudget - ISNULL(e.EmpCount, 0)) AS Vacancy
        FROM seat_counts s
        LEFT JOIN emp_counts e
            ON  e.LOC_CODE = s.LOC_CODE
            AND e.DEPT_SNO = s.DEPT_SNO
            AND e.DESG_SNO = s.DESG_SNO
        WHERE (s.SeatBudget - ISNULL(e.EmpCount, 0)) > 0
    )
    SELECT @Result = CASE 
                        WHEN a.EmpCount < a.Vacancy  and @Salary<=a.SALARY_BGT
                        THEN 1 ELSE 0 
                     END
    FROM vacancy_counts a
    WHERE a.LOC_CODE = @ST_CODE
      AND a.DEPT_SNO = @DeptId
      AND a.DESG_SNO = @DesigId;

    RETURN ISNULL(@Result, 0); -- if no row found, default to 0 (False)
END;

GO

/* ================= ORIGINAL: dbo.fn_IsVacancyShorter  (captured from PROD 2026-08-21) ================= */
ALTER FUNCTION dbo.fn_IsVacancyShorter  
(  
    @LocId   Int,  
    @DeptId    INT,  
    @DesigId   INT ,
	@Salary decimal(18,2)
)  
RETURNS BIT  
AS  
BEGIN  
    DECLARE @Result BIT = 0;  
  Declare @ST_CODE nvarchar(10);
  Select @ST_CODE=STCode from tblLocation(NOLOCK)
  where LocationId=@LocId

  IF @ST_CODE is NULL
  BEGIN
	Return 0;
  END
    ;WITH emp AS (  
        SELECT  
            b.STCode        AS LOC_CODE,  
            d.DepartmentId  AS DEPT_SNO,  
            c.DesignationId AS DESG_SNO  
        FROM HRMS.dbo.tblEmployee a WITH (NOLOCK)  
        LEFT JOIN HRMS.dbo.tblLocation    b WITH (NOLOCK) ON a.LocationId    = b.LocationId  
        LEFT JOIN HRMS.dbo.tblDesignation c WITH (NOLOCK) ON a.DesignationId = c.DesignationId  
        LEFT JOIN HRMS.dbo.tblDepartment  d WITH (NOLOCK) ON a.DepartmentId  = d.DepartmentId  
        WHERE a.IsActive = 1  
    ),  
    emp_counts AS (  
        SELECT  
            LOC_CODE, DEPT_SNO, DESG_SNO,  
            COUNT(*) AS EmpCount  
        FROM emp  
        GROUP BY LOC_CODE, DEPT_SNO, DESG_SNO  
    ),  
    seat_counts AS (  
        SELECT  
            m.LOC_CODE, m.DEPT_SNO, m.DESG_SNO
			,MAX(m.SALARY_BGT) SALARY_BGT,  
            COUNT(*) AS SeatBudget  
        FROM HRMS.dbo.BGTSEATMaster m WITH (NOLOCK)  
        WHERE m.ACTIVE = 1  
        GROUP BY m.LOC_CODE, m.DEPT_SNO, m.DESG_SNO 
		
    ),  
    vacancy_counts AS (  
        SELECT  
            s.LOC_CODE,  
            s.DEPT_SNO,  
            s.DESG_SNO,  
			s.SALARY_BGT,
            s.SeatBudget,  
            ISNULL(e.EmpCount, 0) AS EmpCount,  
            (s.SeatBudget - ISNULL(e.EmpCount, 0)) AS Vacancy  
        FROM seat_counts s  
        LEFT JOIN emp_counts e  
            ON  e.LOC_CODE = s.LOC_CODE  
            AND e.DEPT_SNO = s.DEPT_SNO  
            AND e.DESG_SNO = s.DESG_SNO  
        WHERE (s.SeatBudget - ISNULL(e.EmpCount, 0)) > 0  
    )  
    SELECT @Result = CASE   
                        WHEN a.EmpCount < CEILING(a.Vacancy * 1.3) and @Salary<=a.SALARY_BGT
                        THEN 1 ELSE 0   
                     END  
    FROM vacancy_counts a  
    WHERE a.LOC_CODE = @ST_CODE  
      AND a.DEPT_SNO = @DeptId  
      AND a.DESG_SNO = @DesigId;  
  
    RETURN ISNULL(@Result, 0); -- if no row found, default to 0 (False)  
END;  
GO


/* ORIGINAL dbo.usp_CheckCandidateSeatAvailability - captured from PROD 2026-08-21 */
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

    DECLARE @Occupied INT = @EmpCount + @CandCount;
    DECLARE @Vacancy INT = @SeatBudget - @Occupied;

    DECLARE @IsAvailable BIT = CASE
        WHEN @SeatBudget > 0
             AND @Occupied < @SeatBudget
             AND (@Salary IS NULL OR @MaxSalaryBgt IS NULL OR @Salary <= @MaxSalaryBgt)
        THEN 1 ELSE 0 END;

    DECLARE @Message NVARCHAR(500) =
        CASE
            WHEN @SeatBudget = 0 THEN 'No BGT budget seat is defined for this Store / Department / Sub-Department / Designation combination. Please increase the budget (BGT Seat Master) before hiring.'
            WHEN @Occupied >= @SeatBudget THEN 'All budgeted seats for this Store / Department / Sub-Department / Designation are already filled or reserved. Please increase the budget (BGT Seat Master) before hiring.'
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

GO

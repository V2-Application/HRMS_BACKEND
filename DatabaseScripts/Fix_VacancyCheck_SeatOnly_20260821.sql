/*
    Fix_VacancyCheck_SeatOnly_20260821.sql

    Vacancy check becomes SEAT-ONLY: a joining is allowed whenever the
    LOC_CODE + DEPT_SNO + DESG_SNO pool has at least one unoccupied seat.

    Removed from both functions:
      1. "@Salary <= a.SALARY_BGT"
         87% of active seats (14,323 / 16,520) have SALARY_BGT = NULL, and
         "@Salary <= NULL" is UNKNOWN -- never TRUE -- so those seats could
         never be filled at any salary.
      2. "a.EmpCount < a.Vacancy"  (and the *1.3 variant)
         Since Vacancy = SeatBudget - EmpCount, this reduced to
         EmpCount < SeatBudget/2, i.e. it demanded the pool be less than
         half staffed rather than merely having a free seat.

    The vacancy_counts CTE already filters (SeatBudget - EmpCount) > 0, so
    "a.Vacancy > 0" is the complete seat-availability test.

    @Salary is RETAINED in both signatures (unused) so existing callers
    continue to bind without change.

    Original definitions: BACKUP_VacancyFunctions_Original_20260821.sql
*/

ALTER FUNCTION dbo.fn_IsVacancyShorterForEmployee
(
    @LocId   Int,
    @DeptId    INT,
    @DesigId   INT,
	@Salary decimal(18,2)   -- retained for signature compatibility; no longer used
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
                        WHEN a.Vacancy > 0          -- seat availability only
                        THEN 1 ELSE 0
                     END
    FROM vacancy_counts a
    WHERE a.LOC_CODE = @ST_CODE
      AND a.DEPT_SNO = @DeptId
      AND a.DESG_SNO = @DesigId;

    RETURN ISNULL(@Result, 0); -- if no row found, default to 0 (False)
END;
GO

ALTER FUNCTION dbo.fn_IsVacancyShorter
(
    @LocId   Int,
    @DeptId    INT,
    @DesigId   INT ,
	@Salary decimal(18,2)   -- retained for signature compatibility; no longer used
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
                        WHEN a.Vacancy > 0          -- seat availability only
                        THEN 1 ELSE 0
                     END
    FROM vacancy_counts a
    WHERE a.LOC_CODE = @ST_CODE
      AND a.DEPT_SNO = @DeptId
      AND a.DESG_SNO = @DesigId;

    RETURN ISNULL(@Result, 0); -- if no row found, default to 0 (False)
END;
GO

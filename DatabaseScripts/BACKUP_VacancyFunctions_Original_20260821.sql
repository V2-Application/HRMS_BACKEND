/* ================= ORIGINAL: dbo.fn_IsVacancyShorterForEmployee  (captured from PROD 2026-08-21) ================= */
CREATE FUNCTION dbo.fn_IsVacancyShorterForEmployee
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
CREATE FUNCTION dbo.fn_IsVacancyShorter  
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


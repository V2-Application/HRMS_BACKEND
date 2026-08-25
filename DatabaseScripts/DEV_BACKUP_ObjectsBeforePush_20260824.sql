/* ================= DEV ORIGINAL: usp_CheckCandidateSeatAvailability (captured 2026-08-24) ================= */
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
-- Exemptions (2026-08-11): the freeze does NOT apply - hiring is unrestricted - when:
--   1) The store opened <= 60 days ago (new stores need to staff up before BGT catches up), or
--   2) The store is "UPC" (tblLocation.IsActive = 0/NULL - see LocationCodeMaster.jsx's
--      statusLabel: Active = IsActive true, everything else is labelled UPC).

CREATE   PROCEDURE dbo.usp_CheckCandidateSeatAvailability
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
                WHEN ISNULL(@LocIsActive,0) = 0 THEN 'Store is UPC (not yet active) - no budget restriction applies.'
                ELSE 'Store opened within the last 60 days - no budget restriction applies.'
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
/* ================= DEV ORIGINAL: fn_IsVacancyShorter (captured 2026-08-24) ================= */
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
/* ================= DEV ORIGINAL: fn_IsVacancyShorterForEmployee (captured 2026-08-24) ================= */
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
/* ================= DEV ORIGINAL: fn_CanCreateCandidate (captured 2026-08-24) ================= */
CREATE   FUNCTION dbo.fn_CanCreateCandidate  
(  
    @LocationId VARCHAR(50),   -- coming from frontend  
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
  
    -- Convert LocationId to BIGINT for tblLocation  
    SET @LocIdInt = TRY_CAST(@LocationId AS BIGINT);  
  
    -- ? Invalid location  
    IF (@LocIdInt IS NULL)  
        RETURN 0;  
  
    -- Get STCode  
    SELECT @LocCode = STCode  
    FROM tblLocation  
    WHERE LocationId = @LocIdInt  
      AND ISNULL(IsDeleted,0) = 0;  
  
    IF (@LocCode IS NULL)  
        RETURN 0;  
  
    -- Count ACTIVE seats  
    SELECT @SeatCount = COUNT(*)  
    FROM BGTSEATMaster  
    WHERE UPPER(LOC_CODE) = UPPER(@LocCode)  
      AND DEPT_SNO = @DeptId  
      AND DESG_SNO = @DesgId  
      AND ISNULL(ACTIVE,1) = 1;  
  
    -- ? No seats  
    IF (@SeatCount = 0)  
        RETURN 0;  
  
    -- ? Count ACTIVE candidates (NOT employees)  
    SELECT @CandCount = COUNT(*)  
    FROM Candidate c  
    WHERE c.[LOCATION]   = @LocationId  
      AND c.DEPARTMENT = @DeptId  
      AND c.DESIGNATION= @DesgId  
      AND c.IsActive = 1  
      AND ISNULL(c.IsDeleted,0) = 0;  
  
    -- ? Allow only if new candidate will NOT exceed seats  
    IF (@CandCount < @SeatCount)  
        RETURN 1;  
  
    -- ? Seats full  
    RETURN 0;  
END  
GO
/* ================= DEV ORIGINAL: GetEmployeeDetailsforexcel_Ishu (captured 2026-08-24) ================= */
CREATE PROCEDURE [dbo].[GetEmployeeDetailsforexcel_Ishu]              
    @IsActive BIT = 1,              
    @AllEmployee BIT = 0,              
    @CompanyId INT = 0              
AS              
BEGIN              
    SET NOCOUNT ON;              
      ;WITH LastPunch AS    
(    
    SELECT     
        x.ECode,    
        MAX(x.AttendanceDate) AS LastPunchDate    
    FROM dbo.tbl_fn_GetMonthlyPunchesRange_productionnewnick_test x    
    WHERE x.ValidPunchCount >= 1  
    GROUP BY x.ECode    
),    
Separation AS    
(    
    SELECT     
        EmpId,    
        MAX(UpdatedOn) AS SeparationDate    
    FROM tblEmployeeActiveInActiveHistories    
    WHERE ActionPerformed = 'False'    
    GROUP BY EmpId    
),    
Attachments AS    
(    
    SELECT     
        EmployeeId,    
        MAX(Attachment) AS Attachment    
    FROM HRMS.dbo.EmployeeResignationChecklistResponse    
    WHERE Attachment IS NOT NULL    
    GROUP BY EmployeeId    
)    
    SELECT                  
        e.Ecode AS [Employee Code],                
        e.AOCode AS [AO Code],                
          
        'E-'+l.STCode+'-'+TRY_CAST(d.DepartmentId AS VARCHAR(50))+'-'+TRY_CAST(dg.DesignationId AS VARCHAR(50))+'-'+                  
            CASE                   
                WHEN e.CompanyId = 1 THEN RIGHT(e.Ecode, LEN(e.Ecode) - 1)          -- remove 'V'                  
                WHEN e.CompanyId = 2 THEN RIGHT(e.Ecode, LEN(e.Ecode) - 3)         -- remove 'V2S'                  
                WHEN e.CompanyId = 3 THEN RIGHT(e.Ecode, LEN(e.Ecode) - 2)         -- remove 'PT'                  
                ELSE e.Ecode                  
            END AS [LocBasedECode],                  
          
        e.[FULL NAME] AS [Name of Employee],                  
        l.LocationName AS [Posted Location],                  
        l.LocationName AS [Joined Location],                  
        e.[GENDER] AS [Sex],                  
        REPLACE(CONVERT(VARCHAR(9), e.[DOB], 6),' ','-') AS [D.O.B.],              
          
        d.DepartmentName AS [Department],                  
        dg.DesignationName AS [Designation],                  
          
        sm.ShiftName AS [Shift Name],                
          
        e.[PLACE OF BIRTH] AS [Home Town],                  
        REPLACE(CONVERT(VARCHAR(9), e.DOJ, 6),' ','-') AS [D.O.J.],               
        REPLACE(CONVERT(VARCHAR(9), sep.SeparationDate, 6),' ','-') AS [D.O.L.], 
        REPLACE(CONVERT(VARCHAR(9), e.DateOfResignation, 6),' ','-') AS [Resignation Date],               
          
        COALESCE(NULLIF(NULLIF(e.[BANK NAME], ''), 'NA'), NULLIF(c.[BANK NAME], ''), 'NA') AS [Name of Bank],                  
        COALESCE(NULLIF(NULLIF(e.[A/C NO], ''), 'NA'), NULLIF(c.[A/C NO], ''), 'NA') AS [A/c No.],                  
        COALESCE(NULLIF(NULLIF(e.[BANK IFSC CODE], ''), 'NA'), NULLIF(c.[BANK IFSC CODE], ''), 'NA') AS [IFSC Code],                  
          
        e.[PERMANENT ADDRESS] AS [Permanent Addess],                  
        e.[PRESENT ADDRESS] AS [Present Address],                  
        e.[MOBILE] AS [Mob No.],                  
        e.MOBILE2 AS [Phone No.],                  
        e.[EMAIL ADDRESS] AS [Email Id],                  
        e.[AADHAR NO] AS [Aadhar No.],                  
        e.[PAN NO] AS [PAN No.],                  
          
        COALESCE(NULLIF(NULLIF(e.[HIGHEST QUALIFICATION], ''), 'NA'), NULLIF(c.[HIGHEST QUALIFICATION], ''), 'NA') AS [Qualification],                  
          
        e.[FATHER'S NAME] AS [Father's Name],                  
        e.[MOTHER'S NAME] AS [Mothers Name],                  
        e.[MARITIAL STATUS] AS [Marital Status],                  
          
        e.ReportHeadEcode AS [Reporting Head ECode],                  
        rh.[FULL NAME] AS [Reporting Head Name],                  
          
        COALESCE(NULLIF(NULLIF(e.[FAMILY MEMBER Relation], ''), 'NA'), NULLIF(c.[FAMILY MEMBER Relation], ''), 'NA') AS [Relation],                 
        COALESCE(                  
            NULLIF(REPLACE(CONVERT(VARCHAR(9), e.[FAMILY MEMBER DOB], 6),' ','-'), ''),                   
            NULLIF(REPLACE(CONVERT(VARCHAR(9), c.[FAMILY MEMBER DOB], 6),' ','-'), ''),                   
            'NA'                  
        ) AS [CHILD DOB],                  
          
        COALESCE(NULLIF(NULLIF(e.[COMPANY 1], ''), 'NA'), NULLIF(c.[COMPANY 1], ''), 'NA') AS [Company],                  
          
        /* ? Gross Salary = Basic + DA + CCA + Special Allowance + Extra Allowance + HRA */              
        COALESCE(              
            CONVERT(VARCHAR(50), NULLIF(gsE.GrossSalaryCalc, 0)),              
            CONVERT(VARCHAR(50), NULLIF(gsC.GrossSalaryCalc, 0)),              
            'NA'              
        ) AS [Gross Salary],              
          
        COALESCE(                  
            NULLIF(NULLIF(CAST(e.[In Hand Salary] AS VARCHAR), '0'), ''),                   
            NULLIF(NULLIF(CAST(c.[In Hand Salary] AS VARCHAR), '0'), ''),                   
            'NA'                  
        ) AS [Joining Salary],                  
          
        COALESCE(                  
            NULLIF(NULLIF(CAST(e.[LAST CTC(ANNUAL)] AS VARCHAR), '0'), ''),                   
            NULLIF(NULLIF(CAST(c.[LAST CTC(ANNUAL)] AS VARCHAR), '0'), ''),                   
            'NA'                  
        ) AS [Annual CTC],                  
          
        'NA' AS [Sal Structure],                  
        REPLACE(CONVERT(VARCHAR(9), COALESCE(e.DOJ, c.[JOINING DATE]), 6),' ','-') AS [D.O.J.Group],                  
        COALESCE(e.PFApplicable, c.PFApplicable, NULL) AS [P.F. Applicable?],                  
        'NA' AS [P.F. No.],                  
        'NA' AS [Previous P.F. No.],                  
        COALESCE(e.ESICApplicable, c.ESICApplicable, NULL) AS [E.S.I. Applicable?],                  
          
        COALESCE(                  
            NULLIF(NULLIF(CAST(e.ESICNO AS VARCHAR), ''), ''),                   
            NULLIF(NULLIF(CAST(c.[PREV. EST NO.] AS VARCHAR), ''), ''),                   
            'NA'                  
        ) AS [ESIC_NO],                  
          
        'NA' AS [E.S.I. No.],                  
        COALESCE(NULLIF(NULLIF(e.[UAN NO], ''), 'NA'), NULLIF(c.[UAN NO], ''), 'NA') AS [Universal A/c Number],                  
          
        COALESCE(                  
            NULLIF(NULLIF(CAST(e.BasicSalary AS VARCHAR), '0'), ''),                   
            NULLIF(NULLIF(CAST(c.BasicSalary AS VARCHAR), '0'), ''),                   
            'NA'                  
        ) AS [Basic Salary],                  
          
        COALESCE(                  
            NULLIF(NULLIF(CAST(e.DA AS VARCHAR), '0'), ''),                   
            NULLIF(NULLIF(CAST(c.DA AS VARCHAR), '0'), ''),                   
            'NA'                  
        ) AS [D.A.],                  
          
        COALESCE(                  
            NULLIF(NULLIF(CAST(e.HRA AS VARCHAR), '0'), ''),                   
            NULLIF(NULLIF(CAST(c.HRA AS VARCHAR), '0'), ''),                   
            'NA'                  
        ) AS [H.R.A.],                  
          
        COALESCE(                  
            NULLIF(NULLIF(CAST(e.CCA AS VARCHAR), '0'), ''),                   
            NULLIF(NULLIF(CAST(c.CCA AS VARCHAR), '0'), ''),                   
            'NA'                  
        ) AS [C.C.A.],                  
          
        COALESCE(                  
            NULLIF(NULLIF(CAST(e.SpecialAllowance AS VARCHAR), '0'), ''),                   
            NULLIF(NULLIF(CAST(c.SpecialAllowance AS VARCHAR), '0'), ''),                   
            'NA'                  
        ) AS [Special Allowance],                  
          
        COALESCE(                  
            NULLIF(NULLIF(CAST(e.ExtraAllowance AS VARCHAR), '0'), ''),                   
            NULLIF(NULLIF(CAST(c.ExtraAllowance AS VARCHAR), '0'), ''),                   
            'NA'                  
        ) AS [Extra Allowance],                  
          
        COALESCE(                  
            NULLIF(NULLIF(CAST(e.monthlyGrossCTC AS VARCHAR), '0'), ''),                   
            NULLIF(NULLIF(CAST(c.monthlyGrossCTC AS VARCHAR), '0'), ''),                   
          'NA'                  
        ) AS [MONTHLY CTC.],                  
          
        COALESCE(                  
            NULLIF(NULLIF(CAST(e.annuallyNetCTC AS VARCHAR), '0'), ''),                   
            NULLIF(NULLIF(CAST(c.annuallyNetCTC AS VARCHAR), '0'), ''),                   
            'NA'                  
        ) AS [Annually Net CTC],                  
          
        COALESCE(                  
            NULLIF(NULLIF(CAST(e.SalaryExpectation AS VARCHAR), '0'), ''),                   
            NULLIF(NULLIF(CAST(c.SalaryExpectation AS VARCHAR), '0'), ''),                   
            'NA'                  
        ) AS [Salary Expectation],                  
          
        0 AS [Conveyance],                  
        0 AS [Medical Allowance],                  
        0 AS [Incentive],                  
        0 AS [Fooding Allowance],                  
        0 AS [Leave Encashment],                  
        0 AS [Medical Reim],                  
        0 AS [Lta],        
  CASE WHEN ISNULL(e.BonusApplicable, N'No') IN (N'Ctc', N'Stat', N'Yes') THEN 'Yes' ELSE 'No' END AS [Bonus/Ex-Gratia],                  
        0 AS [Cca],                  
        0 AS [P.Tax],           
        0 AS [L.W.F.],                  
        0 AS [Inc.Paid],                  
        0 AS [Tds],                  
        0 AS [Esi],                  
        0 AS [Recovery],                  
        0 AS [Cash Short],                  
        0 AS [Diesel Deduction],                  
        0 AS [Penalty],                  
        0 AS [Lwf],                  
        0 AS [Medical],                  
          
        ISNULL(TRY_CONVERT(DECIMAL(18,2), e.Reimbersment), 0)            AS [Reimbersment],          
        ISNULL(TRY_CONVERT(DECIMAL(18,2), e.Fuel_and_Maintainence), 0)   AS [Fuel & Maintenance],          
        ISNULL(TRY_CONVERT(DECIMAL(18,2), e.Books_and_Periodicals), 0)   AS [Books & Periodicals],          
        ISNULL(TRY_CONVERT(DECIMAL(18,2), e.[Professional Attire]), 0)   AS [Professional Attire],          
        ISNULL(TRY_CONVERT(DECIMAL(18,2), e.[Driver Wages]), 0)          AS [Driver Wages],          
        ISNULL(TRY_CONVERT(DECIMAL(18,2), e.[Meal Voucher]), 0)          AS [Meal Voucher],          
        ISNULL(TRY_CONVERT(DECIMAL(18,2), e.[Mobile Bill]), 0)           AS [Mobile Bill],          
          
        0 AS [Gross Salary_2],                  
        0 AS [Employeer PF],                  
        0 AS [Employeer ESI],                  
        0 AS [AC NO.2],                  
        0 AS [AC NO.21],                  
        0 AS [GRATITY],                  
        0 AS [T.D.S.],                  
        'NA' AS [Payment Mode],                  
        'NA' AS [Passport No.],                  
        'NA' AS [FATHER DOB],                  
        'NA' AS [MOTHER DOB],                  
        'NA' AS [SPOUSE DOB],                  
        'NA' AS [CHILD NAME_2],                  
        'NA' AS [Relation_2],                  
        'NA' AS [CHILD DOB_2],                  
        'NA' AS [LIC No.],                  
        'NA' AS [P.A.policy no.],                  
        'NA' AS [Mediclaim No.],                  
        'NA' AS [Fooding Details],                  
        'NA' AS [Accomodation Details],                  
        'NA' AS [Desig. Band],                  
        'NA' AS [Annual Gross],                  
        'NA' AS [Hold Salary ?],                  
        'NA' AS [Hold Reason/Remark],                  
        'NA' AS [Reimbursement A/c No.],                  
        'NA' AS [Reimbursement Bank],                  
        'NA' AS [Notice Days],                  
     'NA' AS [Date of Confirmation],                  
        'NA' AS [branch],                  
        'NA' AS [empstatus],                  
        'NA' AS [trfreason],                  
        'NA' AS [trfrdate],                  
        'NA' AS [trfremark],                  
        'NA' AS [senior],                  
        'NA' AS [junior],                  
        'NA' AS [icustomer],                  
        'NA' AS [hod],                  
        'NA' AS [rmanager],                        'NA' AS [jbname],                  
        'NA' AS [jobprofile],                  
        'NA' AS [subdesig],                  
        'NA' AS [sdsgrade],                  
          
        l.STCODE AS [states],      
      
        -- ? CHANGE HERE: Always show Active/Separated based on e.IsActive      
        CASE       
            WHEN ISNULL(e.IsActive,0) = 0 THEN 'Separated'      
            ELSE 'Active'      
        END AS EmployeeStatus,      
      
        e.IsStore AS [Is Store],                  
        er.EmployeeRoleId AS [Employee Role ID],                  
        COALESCE(r.RoleName, 'Employee') AS [Role Name],            
        cc.[FULL NAME] +' ('+cc.Ecode+')' as CreatedBy,              
        uu.[FULL NAME] +' ('+uu.Ecode+')' as UpdatedBy,    
       REPLACE(CONVERT(VARCHAR(9), sep.SeparationDate, 6),' ','-') AS [Separation Date],
REPLACE(CONVERT(VARCHAR(9), lp.LastPunchDate, 6),' ','-') AS [Last Punch Date],   
    
CASE    
WHEN rt.Attachment IS NOT NULL    
THEN CONCAT('https://v2parivar.v2retail.com:9987/', rt.Attachment)    
ELSE NULL    
END AS [Attachment Link],    
         CASE    
        WHEN ISNULL(fp.Status,'') = 'Paid'    
            THEN 'UTR Exists'    
        ELSE 'UTR Pending'    
    END AS [UTR Status],    
       fp.ChequeNo AS [UTR / Cheque Number],  
        e.UpdatedOn            
          
    FROM tblEmployee e                  
        LEFT JOIN tblDepartment d ON d.DepartmentId = e.DepartmentId                  
        LEFT JOIN tblDesignation dg ON dg.DesignationId = e.DesignationId                  
        LEFT JOIN tblLocation l ON l.LocationId = e.LocationId                  
        LEFT JOIN tblEmployee rh ON rh.Ecode = e.ReportHeadEcode            
        LEFT JOIN Candidate c ON c.id = e.CandidateId AND e.CandidateId IS NOT NULL AND e.CandidateId > 0                
        --LEFT JOIN tblEmployee (NOLOCK) uu ON TRY_CAST(e.UpdatedBy AS INT) = TRY_CAST(uu.EmployeeId AS INT)              
        LEFT JOIN tblEmployee uu    
ON uu.EmployeeId = TRY_CAST(e.UpdatedBy AS INT)    
        --LEFT JOIN tblEmployee (NOLOCK) cc ON TRY_CAST(e.CreatedBy AS INT) = TRY_CAST(cc.EmployeeId AS INT)              
      LEFT JOIN tblEmployee cc    
ON cc.EmployeeId = TRY_CAST(e.CreatedBy AS INT)    
      LEFT JOIN fnf_header fh    
    ON fh.EmployeeId = e.EmployeeId    
    LEFT JOIN Separation sep    
ON sep.EmpId = CAST(e.EmployeeId AS NVARCHAR(50))    
LEFT JOIN LastPunch lp    
ON lp.ECode = e.Ecode    
LEFT JOIN Attachments rt    
ON rt.EmployeeId = e.EmployeeId    
LEFT JOIN fnf_payment fp    
    ON fp.FNFId = fh.FNFId    
        
    
        OUTER APPLY (              
            SELECT CAST(            
                ISNULL(TRY_CONVERT(DECIMAL(18,2), e.BasicSalary), 0) +              
                ISNULL(TRY_CONVERT(DECIMAL(18,2), e.DA), 0) +              
                ISNULL(TRY_CONVERT(DECIMAL(18,2), e.CCA), 0) +              
                ISNULL(TRY_CONVERT(DECIMAL(18,2), e.SpecialAllowance), 0) +              
                ISNULL(TRY_CONVERT(DECIMAL(18,2), e.ExtraAllowance), 0) +              
                ISNULL(TRY_CONVERT(DECIMAL(18,2), e.HRA), 0)              
            AS DECIMAL(18,2)) AS GrossSalaryCalc              
        ) gsE              
          
        OUTER APPLY (              
            SELECT CAST(              
                ISNULL(TRY_CONVERT(DECIMAL(18,2), c.BasicSalary), 0) +              
                ISNULL(TRY_CONVERT(DECIMAL(18,2), c.DA), 0) +              
                ISNULL(TRY_CONVERT(DECIMAL(18,2), c.CCA), 0) +              
                ISNULL(TRY_CONVERT(DECIMAL(18,2), c.SpecialAllowance), 0) +              
                ISNULL(TRY_CONVERT(DECIMAL(18,2), c.ExtraAllowance), 0) +              
                ISNULL(TRY_CONVERT(DECIMAL(18,2), c.HRA), 0)              
            AS DECIMAL(18,2)) AS GrossSalaryCalc              
        ) gsC              
          
        LEFT JOIN tblShiftMaster sm ON sm.ShiftID = e.ShiftID                
        LEFT JOIN tblEmployeeRole er ON er.EmployeeId = e.EmployeeId                  
        LEFT JOIN tblRole r ON r.RoleId = er.RoleId                  
          
    WHERE                  
        (@AllEmployee = 1 OR e.IsActive = @IsActive)                
        AND (@CompanyId = 0 OR e.CompanyId = @CompanyId)                
          
    ORDER BY                  
        e.EmployeeId DESC;                  
END

GO
/* ================= DEV ORIGINAL: usp_InsertEmployeeAfterInitiate01 (captured 2026-08-24) ================= */

-- =========================================================================
-- SP 2: usp_InsertEmployeeAfterInitiate01
-- =========================================================================
CREATE   PROCEDURE [dbo].[usp_InsertEmployeeAfterInitiate01]
      @CandidateId bigint,
      @FirstName NVARCHAR(100),
      @MiddleName NVARCHAR(100),
      @LastName NVARCHAR(100),
      @EMAIL_ADDRESS NVARCHAR(100),
      @MOBILE NVARCHAR(20),
      @DepartmentId INT = NULL,
      @DesignationId INT = NULL,
      @LocationId INT = NULL,
      @DOJ DATETIME = NULL,
      @PasswordHash NVARCHAR(255),
      @UpdatedBy NVARCHAR(100) = NULL,
      @TITLE NVARCHAR(50) = NULL,
      @FATHER_S_NAME NVARCHAR(100) = NULL,
      @MOTHER_S_NAME NVARCHAR(100) = NULL,
      @DOB DATE = NULL,
      @GENDER NVARCHAR(10) = NULL,
      @GROSS_SALARY DECIMAL(18, 2) = NULL,
      @UAN_NO NVARCHAR(50) = NULL,
      @PAN_NO NVARCHAR(50) = NULL,
      @AADHAR_NO NVARCHAR(50) = NULL,
      @NAME_ON_ADHAR NVARCHAR(100) = NULL,
      @PLACE_OF_BIRTH NVARCHAR(100) = NULL,
      @PRESENT_ADDRESS NVARCHAR(255) = NULL,
      @PRESENT_ADDRESS_PIN_CODE NVARCHAR(10) = NULL,
      @PERMANENT_ADDRESS NVARCHAR(255) = NULL,
      @PERMANENT_ADDRESS_PIN_CODE NVARCHAR(10) = NULL,
      @APPLICANT_CODE NVARCHAR(50) = NULL,
      @WEEKLY_OFF NVARCHAR(50) = NULL,
      @MARITIAL_STATUS NVARCHAR(20) = NULL,
      @ISRELATIVEINCOMPANY BIT = NULL,
      @NATIONALITY NVARCHAR(50) = NULL,
      @RELIGION NVARCHAR(50) = NULL,
      @BANK_NAME NVARCHAR(100) = NULL,
      @A_C_NO NVARCHAR(50) = NULL,
      @BANK_IFSC_CODE NVARCHAR(20) = NULL,
      @REFERENCE1__OF_LAST_3_COMPANY NVARCHAR(100) = NULL,
      @CONTACT1_OF_LAST_3_COMPANY NVARCHAR(20) = NULL,
      @REFERENCE2__OF_LAST_3_COMPANY1 NVARCHAR(100) = NULL,
      @CONTACT2_OF_LAST_3_COMPANY1 NVARCHAR(20) = NULL,
      @REFERENCE3__OF_LAST_3_COMPANY11 NVARCHAR(100) = NULL,
      @CONTACT3_OF_LAST_3_COMPANY11 NVARCHAR(20) = NULL,
      @REFERENCE4__OF_LAST_3_COMPANY11 NVARCHAR(100) = NULL,
      @CONTACT4_OF_LAST_3_COMPANY11 NVARCHAR(20) = NULL,
      @REFERENCE5__OF_LAST_3_COMPANY111 NVARCHAR(100) = NULL,
      @CONTACT5_OF_LAST_3_COMPANY111 NVARCHAR(20) = NULL,
      @HIGHEST_QUALIFICATION NVARCHAR(100) = NULL,
      @BENEFICIARY_ADDRESS NVARCHAR(255) = NULL,
      @REFERENCE NVARCHAR(255) = NULL,
      @CreatedOn DATETIME = NULL,
      @CreatedBy NVARCHAR(100),
      @IsActive BIT = 1,
      @IsDeleted BIT = 0,
      @IsSalarySlipUploaded BIT = 0,
      @IsBankStatementUploaded BIT = 0,
      @IsPrevOfferLetterUploaded BIT = 0,
      @IsPassportPhotoUploaded BIT = 0,
      @IsPanAttachmentUploaded BIT = 0,
      @IsAadharAttachmentUploaded BIT = 0,
      @IsBankPassbookAttachmentUpoaded BIT = 0,
      @IsEducationAttachmentUploaded BIT = 0,
      @StatusId INT = NULL,
      @ApplicantId NVARCHAR(50) = NULL,
      @BasicSalary DECIMAL(18, 2) = NULL,
      @HRA DECIMAL(18, 2) = NULL,
      @CCA DECIMAL(18, 2) = NULL,
      @SpecialAllowance DECIMAL(18, 2) = NULL,
      @DA DECIMAL(18, 2) = NULL,
      @ExtraAllowance DECIMAL(18, 2) = NULL,
      @monthlyGrossCTC DECIMAL(18, 2) = NULL,
      @annuallyNetCTC DECIMAL(18, 2) = NULL,
      @IsResumeUploaded BIT = 0,
      @TotalExperience DECIMAL(18, 2) = NULL,
      @SalaryExpectation DECIMAL(18, 2) = NULL,
      @AdditionalInfoApplicant NVARCHAR(MAX) = NULL,
      @Agreement BIT = 0,
      @IsApplicant BIT = 1,
      @IsApplicantApproved BIT = 0,
      @PFApplicable BIT = 1,
      @BonusApplicable NVARCHAR(10) = 'No',
      @ESICApplicable BIT = 1,
      @CompanyId INT,
      @ESICNO NVARCHAR(100),
      @MaritalStatus NVARCHAR(100),
      @HusbandName NVARCHAR(100),
      @PreferredLocation NVARCHAR(100),
      @ReportHeadEcode NVARCHAR(50) = NULL,
      @ShiftId INT = NULL,
      @NewEcode NVARCHAR(50) OUTPUT
  AS
  BEGIN
      SET NOCOUNT ON;
      SET @ShiftId = COALESCE(NULLIF(@ShiftId, 0), 1);

      -----------------------------------------------------------------
      -- VALIDATION: Check if Candidate already exists in tblEmployee
      -----------------------------------------------------------------
      IF EXISTS (
          SELECT 1
          FROM tblEmployee WITH (NOLOCK)
          WHERE CandidateId = @CandidateId
      )
      BEGIN
          RAISERROR('Candidate already initiated.', 16, 1);
          RETURN;
      END
      -----------------------------------------------------------------

      DECLARE @Prefix NVARCHAR(10), @LastEcode NVARCHAR(50), @NextNumber INT;
      DECLARE @PadLength INT = 5;  -- default 5 digits

      -- Determine prefix
      SET @Prefix = CASE @CompanyId
                      WHEN 1 THEN 'V'
                      WHEN 2 THEN 'V2S'
                      WHEN 3 THEN 'PT'
                      WHEN 4 THEN 'CT'
                      WHEN 6 THEN 'E'           -- Aquatica
                    END;

      -- Aquatica uses 4-digit padding (E0001..E9999)
      IF @CompanyId = 6
          SET @PadLength = 4;

      -- Get latest Ecode
      SELECT TOP 1 @LastEcode = Ecode
      FROM tblEmployee (NOLOCK)
      WHERE Ecode LIKE @Prefix + '%'
        AND CompanyId = @CompanyId
      ORDER BY EmployeeId DESC;

      -- Extract number
      IF @LastEcode IS NOT NULL
      BEGIN
          DECLARE @NumPart NVARCHAR(10) = SUBSTRING(@LastEcode, LEN(@Prefix) + 1, LEN(@LastEcode));
          SET @NextNumber = TRY_CAST(@NumPart AS INT) + 1;
      END
      ELSE
      BEGIN
          IF @CompanyId = 1
              SET @NextNumber = 1;
          ELSE IF @CompanyId = 2
              SET @NextNumber = 2701;
          ELSE IF @CompanyId = 3
              SET @NextNumber = 1;
          ELSE IF @CompanyId = 4
              SET @NextNumber = 1;
          ELSE IF @CompanyId = 6
              SET @NextNumber = 1;          -- Aquatica starts at E0001
      END;

      -- Generate new Ecode
      IF @CompanyId = 2
      BEGIN
          SET @NewEcode = @Prefix + CAST(@NextNumber AS VARCHAR(4));
      END
      ELSE
      BEGIN
          SET @NewEcode = @Prefix + RIGHT(REPLICATE('0', @PadLength) + CAST(@NextNumber AS VARCHAR), @PadLength);
      END;

      DECLARE @FULL_NAME NVARCHAR(255) = LTRIM(RTRIM(
          COALESCE(@FirstName + ' ', '') + COALESCE(@MiddleName + ' ', '') + COALESCE(@LastName, '')
      ));

      -- Carry the candidate's sub-departments (levels 1/2/3) onto the new employee record.
      DECLARE @SubDept1 INT, @SubDept2 INT, @SubDept3 INT;
      SELECT @SubDept1 = SubDepartmentId1, @SubDept2 = SubDepartmentId2, @SubDept3 = SubDepartmentId3
      FROM dbo.Candidate WITH (NOLOCK) WHERE Id = @CandidateId;

      IF NOT EXISTS (
          SELECT 1
          FROM tblEmployee (NOLOCK)
          WHERE Ecode = @NewEcode
            AND CompanyId = @CompanyId
      )
      BEGIN
          INSERT INTO tblEmployee(
              CandidateId, [FULL NAME], FirstName, MiddleName, LastName, [EMAIL ADDRESS], MOBILE,
              DepartmentId, DesignationId, LocationId, SubDepartmentId1, SubDepartmentId2, SubDepartmentId3, DOJ, Ecode, LastUpdatedBy,
              PasswordHash, PasswordSalt, UpdatedBy, UpdatedOn, TITLE, [FATHER'S NAME],
              [MOTHER'S NAME], DOB, GENDER, [GROSS SALARY], [UAN NO], [PAN NO], [AADHAR NO], [NAME ON ADHAR],
              [PLACE OF BIRTH], [PRESENT ADDRESS], [PRESENT ADDRESS PIN CODE], [PERMANENT ADDRESS],
              [PERMANENT ADDRESS PIN CODE], [APPLICANT CODE], [WEEKLY OFF], [MARITIAL STATUS], ISRELATIVEINCOMPANY,
              NATIONALITY, RELIGION, [BANK NAME], [A/C NO], [BANK IFSC CODE], [REFERENCE1  OF LAST 3 COMPANY],
              [CONTACT1 OF LAST 3 COMPANY], [REFERENCE2  OF LAST 3 COMPANY1], [CONTACT2 OF LAST 3 COMPANY1],
              [REFERENCE3  OF LAST 3 COMPANY11], [CONTACT3 OF LAST 3 COMPANY11], [REFERENCE4  OF LAST 3 COMPANY11],
              [CONTACT4 OF LAST 3 COMPANY11], [REFERENCE5  OF LAST 3 COMPANY111], [CONTACT5 OF LAST 3 COMPANY111],
              [HIGHEST QUALIFICATION], BENEFICIARY_ADDRESS, REFERENCE, CreatedOn, CreatedBy, IsActive,
              IsDeleted, IsSalarySlipUploaded, IsBankStatementUploaded, IsPrevOfferLetterUploaded,
              IsPassportPhotoUploaded, IsPanAttachmentUploaded, IsAadharAttachmentUploaded,
              IsBankPassbookAttachmentUpoaded, IsEducationAttachmentUploaded, StatusId, ApplicantId,
              BasicSalary, HRA, CCA, SpecialAllowance, DA, ExtraAllowance, monthlyGrossCTC, annuallyNetCTC,
              IsResumeUploaded, TotalExperience, SalaryExpectation, AdditionalInfoApplicant, Agreement,
              IsApplicant, IsApplicantApproved, PFApplicable, BonusApplicable, ESICApplicable,
              CompanyId, ESICNO, [Husband Name], PreferredLocation, ReportHeadEcode, ShiftID
          )
          VALUES (
              @CandidateId, @FULL_NAME, @FirstName, @MiddleName, @LastName, @EMAIL_ADDRESS, @MOBILE,
              @DepartmentId, @DesignationId, @LocationId, @SubDept1, @SubDept2, @SubDept3, ISNULL(@DOJ, GETDATE()), @NewEcode, @UpdatedBy,
              @PasswordHash, NULL, @UpdatedBy, GETDATE(), @TITLE, @FATHER_S_NAME, @MOTHER_S_NAME, @DOB, @GENDER,
              @GROSS_SALARY, @UAN_NO, @PAN_NO, @AADHAR_NO, @NAME_ON_ADHAR, @PLACE_OF_BIRTH, @PRESENT_ADDRESS,
              @PRESENT_ADDRESS_PIN_CODE, @PERMANENT_ADDRESS, @PERMANENT_ADDRESS_PIN_CODE, @APPLICANT_CODE,
              @WEEKLY_OFF, @MARITIAL_STATUS, @ISRELATIVEINCOMPANY, @NATIONALITY, @RELIGION, @BANK_NAME,
              @A_C_NO, @BANK_IFSC_CODE, @REFERENCE1__OF_LAST_3_COMPANY, @CONTACT1_OF_LAST_3_COMPANY,
              @REFERENCE2__OF_LAST_3_COMPANY1, @CONTACT2_OF_LAST_3_COMPANY1, @REFERENCE3__OF_LAST_3_COMPANY11,
              @CONTACT3_OF_LAST_3_COMPANY11, @REFERENCE4__OF_LAST_3_COMPANY11, @CONTACT4_OF_LAST_3_COMPANY11,
              @REFERENCE5__OF_LAST_3_COMPANY111, @CONTACT5_OF_LAST_3_COMPANY111, @HIGHEST_QUALIFICATION,
              @BENEFICIARY_ADDRESS, @REFERENCE, ISNULL(@CreatedOn, GETDATE()), @CreatedBy, @IsActive, @IsDeleted,
              @IsSalarySlipUploaded, @IsBankStatementUploaded, @IsPrevOfferLetterUploaded, @IsPassportPhotoUploaded,
              @IsPanAttachmentUploaded, @IsAadharAttachmentUploaded, @IsBankPassbookAttachmentUpoaded,
              @IsEducationAttachmentUploaded, @StatusId, @ApplicantId, @BasicSalary, @HRA, @CCA, @SpecialAllowance,
              @DA, @ExtraAllowance, @monthlyGrossCTC, @annuallyNetCTC, @IsResumeUploaded, @TotalExperience,
              @SalaryExpectation, @AdditionalInfoApplicant, @Agreement, @IsApplicant, @IsApplicantApproved,
              @PFApplicable, @BonusApplicable, @ESICApplicable,
              @CompanyId, @ESICNO, @HusbandName, @PreferredLocation, @ReportHeadEcode, @ShiftId
          );

          DECLARE @NewEmployeeId BIGINT;
          SET @NewEmployeeId = SCOPE_IDENTITY();

          INSERT INTO tblEmployeeRole (EmployeeId, RoleId, AssignedOn, AssignedBy, LastUpdatedBy, LastUpdatedOn)
          VALUES (@NewEmployeeId, 3, GETDATE(), 'System', 'System', GETDATE());
      END
      ELSE
      BEGIN
          SET @NewEcode = '';
      END;
  END;


GO

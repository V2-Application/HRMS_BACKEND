-- =============================================================================
-- COMBINED PRODUCTION APPLY SCRIPT
-- Branch:    pulkit_changes1
-- Source:    dev DB 192.168.151.27\KARMA / HRMS
-- Target:    prod DB 192.168.151.28\hrms / HRMS
-- Generated: 2026-05-14 12:50:06
-- Idempotent: every object uses CREATE OR ALTER. Safe to re-run.
--
-- PRE-FLIGHT (run on prod BEFORE this script):
--   SELECT COUNT(*) FROM sys.objects WHERE name='tbl_fn_GetMonthlyPunchesRange_productionnewnick_test' AND type='U';
--   If 0 -> create or rename references first (see _PROD_APPLY.md section 0.1).
--
-- RUN:
--   sqlcmd -S 192.168.151.28\hrms -d HRMS -U <user> -P <pwd> -C -b -i Scripts\_PROD_APPLY_ALL.sql
-- =============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

-- #############################################################################
-- STEP 1 / 9 -- Regularize -- file: SPs_Regularize.sql
-- #############################################################################
PRINT '>> Applying: STEP 1 / 9 -- Regularize -- file: SPs_Regularize.sql';
GO

-- -----------------------------------------------------------------------------
-- dbo.sp_GetRegularizeRequests
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE sp_GetRegularizeRequests
    @StartDate DATE,
    @EndDate DATE,
    @ManagerEcode VARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        e.Ecode,
        e.[FULL NAME],
        e.FirstName,
        e.LastName,
        r.RequestDate,
        r.Status,
        r.Reason,
        s.StatusName,
        rs.[FULL NAME] AS ReportingManagerName,
        r.EmployeeRemarks,
        r.StatusId,
        r.PunchIn,
        r.PunchOut
    FROM tblAttendanceRegularizationRequest r 
        INNER JOIN tblEmployee e ON e.EmployeeId = r.EmployeeId
        INNER JOIN tblStatus s ON s.StatusId = r.StatusId
        INNER JOIN tblEmployee rs ON rs.Ecode = e.ReportHeadEcode
        INNER JOIN tblEmployeeMultiPunches p ON p.UserID = e.Ecode 
            AND p.PunchDate = r.RequestDate
            AND p.IsRegularize = 1
    WHERE r.RequestDate >= @StartDate 
        AND r.RequestDate <= @EndDate
        AND (@ManagerEcode IS NULL OR e.ReportHeadEcode = @ManagerEcode);
END;
GO

-- -----------------------------------------------------------------------------
-- dbo.sp_GetRegularizeRequestsBulk
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE sp_GetRegularizeRequestsBulk
    @MonthYear VARCHAR(10) = NULL,       -- e.g. 'May-2025'
    @StartDate DATE = NULL,
    @EndDate DATE = NULL,
    @ManagerEcode VARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- Derive StartDate and EndDate from MonthYear if provided
    IF @MonthYear IS NOT NULL AND (@StartDate IS NULL OR @EndDate IS NULL)
    BEGIN
        BEGIN TRY
            SET @StartDate = CONVERT(DATE, '01-' + @MonthYear, 113); -- Format: dd-MMM-yyyy
            SET @EndDate = EOMONTH(@StartDate);
        END TRY
        BEGIN CATCH
            RAISERROR('Invalid MonthYear format. Use format like May-2025.', 16, 1);
            RETURN;
        END CATCH
    END

    -- Validate that we now have start and end dates
    IF @StartDate IS NULL OR @EndDate IS NULL
    BEGIN
        RAISERROR('StartDate and EndDate must be provided, or MonthYear must be valid.', 16, 1);
        RETURN;
    END

    SELECT 
        e.Ecode,
        e.[FULL NAME],
        e.FirstName,
        e.LastName,
        r.RequestDate,
        r.Reason,
        s.StatusName,
        rs.[FULL NAME] AS ReportingManagerName,
        r.EmployeeRemarks,
        r.PunchIn,
        r.PunchOut
    FROM tblAttendanceRegularizationRequest r 
        INNER JOIN tblEmployee e ON e.EmployeeId = r.EmployeeId
        INNER JOIN tblStatus s ON s.StatusId = r.StatusId
        INNER JOIN tblEmployee rs ON rs.Ecode = e.ReportHeadEcode
        INNER JOIN tblEmployeeMultiPunches p ON p.UserID = e.Ecode 
            AND p.PunchDate = r.RequestDate
            AND p.IsRegularize = 1
    WHERE r.RequestDate BETWEEN @StartDate AND @EndDate
        AND (@ManagerEcode IS NULL OR e.ReportHeadEcode = @ManagerEcode);
END;
GO

-- -----------------------------------------------------------------------------
-- dbo.usp_GetAttendanceRegularization
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE usp_GetAttendanceRegularization 
--'Nov-25'
    @MonthYear VARCHAR(10)   -- Format: MMM-yy (e.g., 'Nov-25')
AS
BEGIN
    SET NOCOUNT ON;

    ------------------------------------------------------------
    -- Convert MMM-yy into numeric Month & Year
    ------------------------------------------------------------
    DECLARE @Month INT, @Year INT;

    SELECT 
        @Month = MONTH(CONVERT(DATE, '01-' + @MonthYear, 106)),
        @Year  = YEAR(CONVERT(DATE, '01-' + @MonthYear, 106));

    ------------------------------------------------------------
    -- Main Query
    ------------------------------------------------------------
    SELECT 
        b.Ecode,
        COALESCE(b.[FULL NAME], b.FirstName + b.MiddleName + b.LastName) AS EmpName,
        h.STCode,h.LocationName,
        i.DepartmentName,j.DesignationName,
        a.[RequestDate],
        a.[Reason],
        f.Ecode AS RM_ECODE,
        COALESCE(f.[FULL NAME], f.FirstName + f.MiddleName + f.LastName) AS ReportManagerName,
        a.[PunchIn],
        a.[PunchOut],
        c.StatusName,
        a.[FileUrl],
        a.[PunchTypeId],
        g.RequestTypeName,
        a.[EmployeeRemarks],
        d.StatusName AS ManagerStatus,
        a.[ManagerApprovalOn],
        a.[ManagerRemarks],
        e.StatusName AS [LpApprovalStatus],
        a.[LpApprovalOn],
        a.[LpRemarks]
    FROM tblAttendanceRegularizationRequest a
    LEFT JOIN tblEmployee b ON a.EmployeeId = b.EmployeeId
    LEFT JOIN tblLocation h ON h.LocationId = b.LocationId
    LEFT JOIN tblStatus c ON c.StatusId = a.StatusId
    LEFT JOIN tblStatus d ON d.StatusId = a.ManagerApprovalStatusId
    LEFT JOIN tblStatus e ON e.StatusId = a.LpApprovalStatusId
    LEFT JOIN tblEmployee f ON f.EmployeeId = a.ReportingManagerId
    LEFT JOIN tblRequestTypes g ON a.RequestTypeId = g.RequestTypeId
    LEFT JOIN tblDepartment i ON b.DepartmentId = i.DepartmentId
    LEFT JOIN tblDesignation j ON b.DesignationId = j.DesignationId
    WHERE 
        MONTH(a.RequestDate) = @Month
        AND YEAR(a.RequestDate) = @Year
    order by a.RequestDate,b.Ecode
END
GO

PRINT '<< Done:     STEP 1 / 9 -- Regularize -- file: SPs_Regularize.sql';
GO

-- #############################################################################
-- STEP 2 / 9 -- BulkInactivate -- file: SPs_BulkInactivate.sql
-- #############################################################################
PRINT '>> Applying: STEP 2 / 9 -- BulkInactivate -- file: SPs_BulkInactivate.sql';
GO

-- -----------------------------------------------------------------------------
-- dbo.sp_GetEmployeeEffectiveLeavingDate
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_GetEmployeeEffectiveLeavingDate
    @EmployeeId BIGINT  
AS  
BEGIN  
    SET NOCOUNT ON;  
  
    SELECT   
        COALESCE(  
            p.LastValidPunchDate,  
            TRY_CONVERT(DATETIME2(0), e.[DateOfLeft])  
        ) AS EffectiveDateOfLeaving  
    FROM dbo.tblEmployee e  
    OUTER APPLY    
    (    
        SELECT MAX(x.AttendanceDate) AS LastValidPunchDate    
        FROM dbo.tbl_fn_GetMonthlyPunchesRange_productionnewnick_test x    
        WHERE x.ECode = e.Ecode    
          AND TRY_CAST(x.TotalWorkingMinutes AS time) >= '04:30'  
    ) p  
    WHERE e.EmployeeId = @EmployeeId;  
END
GO

PRINT '<< Done:     STEP 2 / 9 -- BulkInactivate -- file: SPs_BulkInactivate.sql';
GO

-- #############################################################################
-- STEP 3 / 9 -- Payroll -- file: SPs_Payroll.sql
-- #############################################################################
PRINT '>> Applying: STEP 3 / 9 -- Payroll -- file: SPs_Payroll.sql';
GO

-- -----------------------------------------------------------------------------
-- dbo.sp_CalculateEmployeePayroll
-- -----------------------------------------------------------------------------
-- [sp_CalculateEmployeePayroll] '52398','14','Jun-25','50000.00','4'            
CREATE OR ALTER PROCEDURE [dbo].[sp_CalculateEmployeePayroll]             
    @EmployeeId INT,            
    @Attendance Decimal(18,2),            
    @Month VARCHAR(6),            
    @Salary DECIMAL(18,2)=0.0,            
    @ExtraDays DECIMAL(18,2) = 0.0,  
 @IsActive Bit  
AS            
BEGIN            
    SET NOCOUNT ON;            
    begin try        
    -- Declare variables for calculations            
    DECLARE @BudgetMonthDays INT;            
    DECLARE @WeeklyOff DECIMAL(18,2);            
    DECLARE @RemainingDays DECIMAL(18,2);            
    DECLARE @CompOffUsed DECIMAL(18,2) = 0;            
    DECLARE @EarnedLeaveUsed DECIMAL(18,2) = 0;            
    DECLARE @CasualLeaveUsed DECIMAL(18,2) = 0;            
    DECLARE @LeaveAdjust DECIMAL(18,2) = 0;            
    DECLARE @AbsentDays DECIMAL(18,2) = 0;            
    DECLARE @EarnedLeaveAccrued DECIMAL(18,2);            
    DECLARE @CasualLeaveAccrued DECIMAL(18,2);            
    DECLARE @PayableDays DECIMAL(18,2);            
    DECLARE @Payroll DECIMAL(18,2);            
    DECLARE @EmployeeName VARCHAR(100);            
    DECLARE @Ecode VARCHAR(50);            
    DECLARE @DesignationId INT;            
    DECLARE @EmployeeLeaveBalanceId INT;            
    DECLARE @MonthName VARCHAR(3);            
    DECLARE @Year INT;            
    DECLARE @CompOffBalance DECIMAL(18,2);            
    DECLARE @EarnedLeaveBalance DECIMAL(18,2);            
    DECLARE @CasualLeaveBalance DECIMAL(18,2);            
 Declare @PF DECIMAL(18,2);            
 DECLARE @ESIC DECIMAL(18,2);    
 Declare @Tds decimal(18,2);    
 Declare @PTax decimal(18,2);    
 Declare @Loan decimal(18,2);    
 Declare @CashShort decimal(18,2);    
 Declare @DieselDeduction decimal(18,2);    
 Declare @Penality decimal(18,2);    
 Declare @Lwf decimal(18,2);    
 Declare @LwfEmployeer decimal(18,2);  
    
 Declare @INCENTIVE Decimal(18,2);    
 Declare @ARREAR Decimal(18,2);    
 Declare @OVERTIME Decimal(18,2);    
 Declare @FOODINGALLOWANCE Decimal(18,2);    
 Declare @MOBILEBILL Decimal(18,2);    
 Declare @State nvarchar(100);
 Declare @MonthNo INT;            
 Declare @LocationCategoryId nvarchar(100);      
 Declare @IsExtraDaysApplicable bit=0;    
 Declare @BasicSalary decimal(18,2),@DOJ datetime,@GrossEarnings decimal(18,2),@DOL datetime,@IsBonusApplicable nvarchar(10),@BasicSalaryCalc decimal(18,2);  
    -- Calculate BudgetMonthDays for the given month            
    SET @MonthName = LEFT(@Month, 3);            
 Set @MonthNo = CASE @MonthName             
            WHEN 'Jan' THEN 1 WHEN 'Feb' THEN 2 WHEN 'Mar' THEN 3             
            WHEN 'Apr' THEN 4 WHEN 'May' THEN 5 WHEN 'Jun' THEN 6             
            WHEN 'Jul' THEN 7 WHEN 'Aug' THEN 8 WHEN 'Sep' THEN 9             
            WHEN 'Oct' THEN 10 WHEN 'Nov' THEN 11 WHEN 'Dec' THEN 12             
        END;            
            
    SET @Year = 2000 + CAST(RIGHT(@Month, 2) AS INT);            
    SET @BudgetMonthDays = DAY(EOMONTH(DATEFROMPARTS(@Year,             
        CASE @MonthName             
            WHEN 'Jan' THEN 1 WHEN 'Feb' THEN 2 WHEN 'Mar' THEN 3             
            WHEN 'Apr' THEN 4 WHEN 'May' THEN 5 WHEN 'Jun' THEN 6             
            WHEN 'Jul' THEN 7 WHEN 'Aug' THEN 8 WHEN 'Sep' THEN 9             
            WHEN 'Oct' THEN 10 WHEN 'Nov' THEN 11 WHEN 'Dec' THEN 12             
        END, 1)));            
             
            
    -- Get WeeklyOff from tblLocationDesignationPolicy            
 Select @Ecode=Ecode, @LocationCategoryId= b.STCode,@DesignationId= c.DesignationId,@IsExtraDaysApplicable=IsNULL(IsExtraDayApplicable,0),  
 --@BasicSalary=ISNULL(try_cast(BasicSalary as decimal),0),  
 @DOJ = ISNULL(try_cast(DOJ as datetime),GETDATE()),  
 @DOL  = try_cast(DateOfLeft as datetime),  
 @IsBonusApplicable = ISNULL(BonusApplicable,'No')  
 --@GrossEarnings=ISNULL(try_cast([GROSS SALARY] as decimal),0),@Reimbursement=ISNULL(try_cast(Reimbersment as decimal),0)  
 from tblEmployee a (NOLOCK)      
 Left Join tblLocation b (NOLOCK) on a.LocationId=b.LocationId      
 Left Join tblDesignation c (NOLOCK) on a.DesignationId=c.DesignationId      
 where EmployeeId=@EmployeeId      
 --where Ecode='RTNR65'      


 --Select @State=State from 
 --StoreStateLinking
 --where [ST-CD]=@LocationCategoryId


 --Print 'For ECOde : RTNR65, '+'LocationCategoryId : '+@LocationCategoryId      
      
 DECLARE @Matched BIT = 0;      
      
 -- First attempt: Location + Designation      
 SELECT TOP 1 @WeeklyOff = WeeklyOff, @Matched = 1      
 FROM tblLocationDesignationPolicy      
 WHERE LocationCategoryId = @LocationCategoryId      
   AND DesignationId = @DesignationId      
   AND CAST(TotalAttendance AS INT) <= @Attendance      
 ORDER BY CAST(TotalAttendance AS INT) DESC;      
      
 -- Second attempt: Location only      
 IF @Matched = 0      
 BEGIN      
  SELECT TOP 1 @WeeklyOff = WeeklyOff, @Matched = 1      
  FROM tblLocationDesignationPolicy      
  WHERE LocationCategoryId = @LocationCategoryId      
    AND DesignationId IS NULL      
    AND CAST(TotalAttendance AS INT) <= @Attendance      
  ORDER BY CAST(TotalAttendance AS INT) DESC;      
 END      
      
 -- Third attempt: Universal      
 IF @Matched = 0      
 BEGIN      
  SELECT TOP 1 @WeeklyOff = WeeklyOff      
  FROM tblLocationDesignationPolicy      
  WHERE LocationCategoryId = 'Universal'      
    AND CAST(TotalAttendance AS INT) <= @Attendance      
  ORDER BY CAST(TotalAttendance AS INT) DESC;      
 END      
      
 -- Handle attendance = 0      
 IF @Attendance = 0 or @WeeklyOff is NULL      
  SET @WeeklyOff = 0;      
      
      
    --SELECT TOP 1 @WeeklyOff = WeeklyOff            
    --FROM (            
    --    SELECT             
    --        WeeklyOff,            
    --        TotalAttendance,            
    --        CAST(            
    --            CASE             
    --                WHEN CHARINDEX('-', TotalAttendance) > 0             
    --                THEN LEFT(TotalAttendance, CHARINDEX('-', TotalAttendance) - 1)            
    --                ELSE TotalAttendance            
    --            END AS INT) AS LowerBound,            
    --        CAST(            
    --            CASE             
    --                WHEN CHARINDEX('-', TotalAttendance) > 0             
    --                THEN SUBSTRING(TotalAttendance, CHARINDEX('-', TotalAttendance) + 1, LEN(TotalAttendance))            
    --                ELSE TotalAttendance            
    --            END AS INT) AS UpperBound            
    --    FROM tblLocationDesignationPolicy            
    --    WHERE LocationCategoryId = 1 -- Assuming HO            
    --        --AND DesignationId = (SELECT TOP 1 DesignationId FROM tblEmployee WHERE EmployeeId = @EmployeeId)            
    --) AS Policy            
    --WHERE @Attendance >= LowerBound            
    --  AND (            
    --      @Attendance <= UpperBound            
    --      OR UpperBound = (            
    --          SELECT MAX(            
    --              CAST(            
    --                  CASE             
    --                      WHEN CHARINDEX('-', TotalAttendance) > 0             
    --                      THEN SUBSTRING(TotalAttendance, CHARINDEX('-', TotalAttendance) + 1, LEN(TotalAttendance))            
    --                      ELSE TotalAttendance            
    --                  END AS INT))            
    --          FROM tblLocationDesignationPolicy            
    --          WHERE LocationCategoryId = 1            
    --            --AND DesignationId = (SELECT DesignationId FROM tblEmployee WHERE EmployeeId = @EmployeeId)            
    --)            
    --   )            
    --ORDER BY UpperBound DESC;            
            
    -- Get Employee details and EmployeeLeaveBalanceId            
    SELECT             
        @EmployeeName = [FULL NAME],            
        @Ecode = Ecode,            
        @DesignationId = DesignationId            
    FROM tblEmployee            
    WHERE EmployeeId = @EmployeeId;            
            
  SELECT @EmployeeLeaveBalanceId = EmployeeLeaveBalanceId      
    FROM tblEmployeeLeaveBalance      
    WHERE EmployeeId = @EmployeeId and MONTH=@Month;          
            
    -- Validation checks            
    --IF @WeeklyOff IS NULL            
    --BEGIN            
    --    RAISERROR ('No matching policy found for the given attendance and employee.', 16, 1);            
    --    RETURN;            
    --END            
            
    --IF @EmployeeName IS NULL OR @Ecode IS NULL OR @DesignationId IS NULL            
    --BEGIN            
    --    RAISERROR ('Employee details not found for EmployeeId %d.', 16, 1, @EmployeeId);            
    --    RETURN;            
    --END            
            
    --IF @EmployeeLeaveBalanceId IS NULL            
    --BEGIN            
    --    RAISERROR ('Employee leave balance not found for EmployeeId %d.', 16, 1, @EmployeeId);            
    --    RETURN;            
    --END            
          
    -- Show opening leave balances            
SELECT             
    @CompOffBalance = ISNULL((        
        SELECT CompOffBalance         
        FROM tblEmployeeLeaveBalance         
        WHERE EmployeeLeaveBalanceId = @EmployeeLeaveBalanceId        
    ), 0),        
        
    @EarnedLeaveBalance = ISNULL((        
        SELECT EarnedLeaveBalance         
        FROM tblEmployeeLeaveBalance         
        WHERE EmployeeLeaveBalanceId = @EmployeeLeaveBalanceId        
    ), 0),        
        
    @CasualLeaveBalance = ISNULL((        
        SELECT CasualLeaveBalance         
        FROM tblEmployeeLeaveBalance         
        WHERE EmployeeLeaveBalanceId = @EmployeeLeaveBalanceId        
    ), 0);        
           
            
    SELECT             
        @CompOffBalance AS OpeningCompOffBalance,            
        @EarnedLeaveBalance AS OpeningEarnedLeaveBalance,            
        @CasualLeaveBalance AS OpeningCasualLeaveBalance;         
            
    ---- Credit leaves based on attendance and weekly off            
    --SET @EarnedLeaveAccrued = ((@Attendance + @WeeklyOff) / @BudgetMonthDays) * 1.25;            
    --SET @CasualLeaveAccrued = ((@Attendance + @WeeklyOff) / @BudgetMonthDays) * 0.58;            
  -- Credit leaves based on attendance and weekly off caping            
        
    -- Update tblEmployeeLeaveBalance with credited leaves            
    UPDATE tblEmployeeLeaveBalance            
    SET             
        EarnedLeaveBalance = EarnedLeaveBalance ,            
        --EarnedLeaveAcquired = EarnedLeaveAcquired + @EarnedLeaveAccrued,            
        CasualLeaveBalance = CasualLeaveBalance,            
        --CasualLeaveAcquired = CasualLeaveAcquired + @CasualLeaveAccrued,            
        LastCreditedMonth = DATEFROMPARTS(@Year,             
            CASE @MonthName             
                WHEN 'Jan' THEN 1 WHEN 'Feb' THEN 2 WHEN 'Mar' THEN 3             
                WHEN 'Apr' THEN 4 WHEN 'May' THEN 5 WHEN 'Jun' THEN 6             
                WHEN 'Jul' THEN 7 WHEN 'Aug' THEN 8 WHEN 'Sep' THEN 9             
                WHEN 'Oct' THEN 10 WHEN 'Nov' THEN 11 WHEN 'Dec' THEN 12             
            END, 1),            
        LastUpdatedOn = GETDATE()            
    WHERE EmployeeLeaveBalanceId = @EmployeeLeaveBalanceId;            
            
    -- Calculate remaining days            
    SET @RemainingDays = @BudgetMonthDays - (@Attendance- @ExtraDays) - @WeeklyOff ;       
 If @RemainingDays<0    
  Set @RemainingDays=0    
    set @AbsentDays=@RemainingDays    
    IF @RemainingDays > 0            
    BEGIN            
        -- Get leave balances after crediting            
        SELECT             
    @CompOffBalance = ISNULL((        
        SELECT CompOffBalance         
        FROM tblEmployeeLeaveBalance         
        WHERE EmployeeLeaveBalanceId = @EmployeeLeaveBalanceId        
    ), 0),        
        
    @EarnedLeaveBalance = ISNULL((        
        SELECT EarnedLeaveBalance         
        FROM tblEmployeeLeaveBalance         
        WHERE EmployeeLeaveBalanceId = @EmployeeLeaveBalanceId        
    ), 0),        
        
    @CasualLeaveBalance = ISNULL((        
        SELECT CasualLeaveBalance         
        FROM tblEmployeeLeaveBalance         
        WHERE EmployeeLeaveBalanceId = @EmployeeLeaveBalanceId        
    ), 0);        
;            
            
   --     -- Deduct from CompOffBalance            
   --     IF @RemainingDays > 0 AND @CompOffBalance > 0            
   --     BEGIN            
   --         SET @CompOffUsed = CASE WHEN @RemainingDays <= @CompOffBalance THEN @RemainingDays ELSE @CompOffBalance END;            
   --         SET @RemainingDays = @RemainingDays - @CompOffUsed;            
   --     END            
   ---- Deduct from CasualLeaveBalance            
   --     IF @RemainingDays > 0 AND @CasualLeaveBalance > 0            
   --     BEGIN            
   --         SET @CasualLeaveUsed = CASE WHEN @RemainingDays <= @CasualLeaveBalance THEN @RemainingDays ELSE @CasualLeaveBalance END;            
   --         SET @RemainingDays = @RemainingDays - @CasualLeaveUsed;            
   --     END          
   --     -- Deduct from EarnedLeaveBalance            
   --     IF @RemainingDays > 0 AND @EarnedLeaveBalance > 0            
   --     BEGIN            
   --         SET @EarnedLeaveUsed = CASE WHEN @RemainingDays <= @EarnedLeaveBalance THEN @RemainingDays ELSE @EarnedLeaveBalance END;            
   --         SET @RemainingDays = @RemainingDays - @EarnedLeaveUsed;            
   --     END            
   -- Deduct from CompOffBalance            
IF @RemainingDays > 0 AND @CompOffBalance > 0            
BEGIN            
    DECLARE @AdjustedCompOffBalance DECIMAL(5,2);            
    SET @AdjustedCompOffBalance = FLOOR(@CompOffBalance * 2) / 2.0;            
            
    SET @CompOffUsed =             
        CASE             
            WHEN @RemainingDays <= @AdjustedCompOffBalance             
                THEN @RemainingDays             
            ELSE @AdjustedCompOffBalance             
        END;            
            
    SET @RemainingDays = @RemainingDays - @CompOffUsed;            
END            
            
-- Deduct from CasualLeaveBalance            
IF @RemainingDays > 0 AND @CasualLeaveBalance > 0            
BEGIN            
    DECLARE @AdjustedCasualLeaveBalance DECIMAL(18,2);           
    SET @AdjustedCasualLeaveBalance = FLOOR(@CasualLeaveBalance * 2) / 2.0;            
            
    SET @CasualLeaveUsed =             
        CASE             
            WHEN @RemainingDays <= @AdjustedCasualLeaveBalance             
                THEN @RemainingDays             
            ELSE @AdjustedCasualLeaveBalance             
        END;            
            
    SET @RemainingDays = @RemainingDays - @CasualLeaveUsed;            
END            
            
-- Deduct from EarnedLeaveBalance            
IF @RemainingDays > 0 AND @EarnedLeaveBalance > 0            
BEGIN            
    DECLARE @AdjustedEarnedLeaveBalance DECIMAL(18,2);            
    SET @AdjustedEarnedLeaveBalance = FLOOR(@EarnedLeaveBalance * 2) / 2.0;            
            
    SET @EarnedLeaveUsed =             
        CASE             
            WHEN @RemainingDays <= @AdjustedEarnedLeaveBalance             
                THEN @RemainingDays             
            ELSE @AdjustedEarnedLeaveBalance             
        END;            
            
    SET @RemainingDays = @RemainingDays - @EarnedLeaveUsed;            
END            
            
                   
            
        -- Calculate LeaveAdjust (total leaves deducted)            
        SET @LeaveAdjust = @CompOffUsed + @EarnedLeaveUsed + @CasualLeaveUsed;            
                 
    END      
    ELSE            
    BEGIN            
        SET @LeaveAdjust = 0;            
        --SET @AbsentDays = 0;            
    END            
            SET @EarnedLeaveAccrued =  (case when (((@Attendance+@LeaveAdjust + @WeeklyOff) / @BudgetMonthDays) * 1.25)>1.25 then 1.25 else (((@Attendance+@LeaveAdjust + @WeeklyOff) / @BudgetMonthDays) * 1.25) end) ;            
    SET @CasualLeaveAccrued = (case when (((@Attendance+@LeaveAdjust + @WeeklyOff) / @BudgetMonthDays) * 0.58)>0.58 then 0.58 else (((@Attendance+@LeaveAdjust + @WeeklyOff) / @BudgetMonthDays) * 0.58) end);            
            
        -- Remaining days are absent            
        --SET @AbsentDays = @RemainingDays;            
            
        -- Update tblEmployeeLeaveBalance with debited leaves            
        UPDATE tblEmployeeLeaveBalance            
        SET             
            CompOffBalance = CompOffBalance - @CompOffUsed,            
            CompOffUsed = CompOffUsed + @CompOffUsed,            
   CasualLeaveBalance =( CasualLeaveBalance - @CasualLeaveUsed)+@CasualLeaveAccrued,            
CasualLeaveUsed = CasualLeaveUsed + @CasualLeaveUsed,            
            EarnedLeaveBalance = (EarnedLeaveBalance - @EarnedLeaveUsed)+@EarnedLeaveAccrued,            
            EarnedLeaveUsed = EarnedLeaveUsed + @EarnedLeaveUsed,            
                   EarnedLeaveAcquired =  @EarnedLeaveAccrued ,            
            
        CasualLeaveAcquired =  @CasualLeaveAccrued,        
            LastUpdatedOn = GETDATE(),            
   used=used+(@CompOffUsed+@CasualLeaveUsed+@EarnedLeaveUsed)            
        WHERE EmployeeLeaveBalanceId = @EmployeeLeaveBalanceId;   
    -- Calculate payable days and payroll            
    SET @PayableDays = @Attendance + @WeeklyOff + @LeaveAdjust;            
    SET @Payroll = (@Salary / @BudgetMonthDays) * @PayableDays;            
            
            
              
  DECLARE @PFValue DECIMAL(18,2);            
              
  DECLARE @ESICValue DECIMAL(18,2);            
  --  -- Call PF procedure (example)            
    --EXEC dbo.usp_UpsertEmpPFData @Ecode = @Ecode, @Month = @MOnth, @Year = @Year;            
             
-- with tablea as(            
--SELECT  [EmployeeAttendancePayrollId]              
--      ,[Month]              
--   ,YEAR            
--      ,[LocationCategoryId]              
--      ,[DesignationId]              
--      ,[EmployeeName]              
--      ,[Ecode]              
--      ,[EmployeeId],              
--   Attendance,              
--   extradays 'weeklyoffpresent',              
--   weeklyoff 'Actual_Weekly_Off',              
--   (select top 1 Used from tblEmployeeLeaveBalance b where b.EmployeeId=a.EmployeeId) 'leave_availed',              
--      (Attendance-extradays+(select top 1 Used from tblEmployeeLeaveBalance b where b.EmployeeId=a.EmployeeId)+weeklyoff) 'InitialPaybledays'  ,            
--   DAY(EOMONTH(GETDATE())) AS TotalDaysInMonth,            
--   (CASE         --        WHEN (Attendance-extradays+(select top 1 Used from tblEmployeeLeaveBalance b where b.EmployeeId=a.EmployeeId)+weeklyoff) < (DAY(EOMONTH(GETDATE()))) THEN (Attendance-extradays+(select top 1 Used from tblEmployeeLeaveBalance bwhere b.EmployeeId=a.EmployeeId)+weeklyoff)             
--        ELSE (DAY(EOMONTH(GETDATE())))             
--    END) AS 'Payble_Days'            
--  FROM [HRMS].[dbo].[tblEmployeeAttendancePayrollCalculation] a             
-- ),            
-- finalInitPaybleDays as (            
-- select *,(weeklyoffpresent+(CASE             
--        WHEN (InitialPaybledays-Payble_Days) > 0 THEN (InitialPaybledays-Payble_Days)             
--        ELSE 0             
--    END)) 'EXTRA_DAYS' from tablea)            
            
-- MERGE dbo.tbl_calculatePaybledays AS Target            
--USING (            
--    Select             
-- Month,Year,[LocationCategoryId],[DesignationId],[EmployeeName],[Ecode],[EmployeeId],Attendance,weeklyoffpresent,Actual_Weekly_Off,leave_availed,InitialPaybledays,TotalDaysInMonth            
-- ,Payble_Days,EXTRA_DAYS            
-- from finalInitPaybleDays            
--) AS Source            
--ON Target.Ecode = Source.Ecode AND Target.[Month] = Source.[Month] AND Target.[Year] = Source.[Year]            
--WHEN MATCHED THEN            
--    UPDATE SET             
--        Target.LocationCategoryId = Source.LocationCategoryId,            
--        Target.DesignationId = Source.DesignationId,            
--        Target.EmployeeName = Source.EmployeeName,            
--        Target.EmployeeId = Source.EmployeeId,            
--        Target.Attendance = Source.Attendance,            
--        Target.weeklyoffpresent = Source.weeklyoffpresent,            
--        Target.Actual_Weekly_Off = Source.Actual_Weekly_Off,            
--        Target.leave_availed = Source.leave_availed,            
--        Target.InitialPaybledays = Source.InitialPaybledays,            
--        Target.TotalDaysInMonth = Source.TotalDaysInMonth,            
--        Target.Payble_Days = Source.Payble_Days,            
--        Target.EXTRA_DAYS = Source.EXTRA_DAYS            
--WHEN NOT MATCHED THEN            
    INSERT into tbl_calculatePaybledays (            
        [Month],            
        [Year],            
        LocationCategoryId,            
        DesignationId,            
        EmployeeName,            
        Ecode,            
        EmployeeId,            
        Attendance,            
        weeklyoffpresent,            
        Actual_Weekly_Off,            
        leave_availed,            
        InitialPaybledays,            
        TotalDaysInMonth,            
        Payble_Days,            
        EXTRA_DAYS,            
  Absent,  
  Status  
    )            
 Values(            
  @Month,            
  @Year,            
  1,            
  @DesignationId,            
  @EmployeeName,            
  @Ecode,            
  @EmployeeId,            
  @Attendance,       
  --weeklyoff    
  isnull(@ExtraDays,0),        
  --actualweeklyoff    
  isnull(@WeeklyOff,0),       
  --leaveavailed    
  (SELECT ISNULL(          
    (SELECT TOP 1 Used           
     FROM tblEmployeeLeaveBalance b           
     WHERE b.EmployeeId = @EmployeeId and MONTH=@Month),           
    0) ),            
 --init payble days    
  isnull((@Attendance-@ExtraDays+isnull((SELECT ISNULL(        
    (SELECT TOP 1 Used         
     FROM tblEmployeeLeaveBalance b         
     WHERE b.EmployeeId = @EmployeeId and MONTH=@Month),         
    0        
)),0)+isnull(@WeeklyOff,0)),0),         
--total days in month    
   @BudgetMonthDays,           
   --payble days    
 
 (CASE             
        WHEN isnull((@Attendance-@ExtraDays+(SELECT ISNULL(        
    (SELECT TOP 1 Used         
     FROM tblEmployeeLeaveBalance b         
     WHERE b.EmployeeId = @EmployeeId and MONTH=@Month),         
    0        
))+isnull(@WeeklyOff,0)),0) < (@BudgetMonthDays) THEN isnull((@Attendance-@ExtraDays+(SELECT ISNULL(        
    (SELECT TOP 1 Used         
     FROM tblEmployeeLeaveBalance b         
     WHERE b.EmployeeId = @EmployeeId and MONTH=@Month),         
    0        
))+isnull(@WeeklyOff,0)),0)             
        ELSE (@BudgetMonthDays)             
    END),
	

 --extra days    
 --case when @IsExtraDaysApplicable=0    
 --then 0    
 --else    
  isnull((@ExtraDays+(CASE             
        WHEN ((@Attendance-@ExtraDays+(SELECT ISNULL(        
    (SELECT TOP 1 Used         
     FROM tblEmployeeLeaveBalance b         
     WHERE b.EmployeeId = @EmployeeId and MONTH=@Month),         
    0        
))+@WeeklyOff)-(CASE             
        WHEN (@Attendance-@ExtraDays+(SELECT ISNULL(        
    (SELECT TOP 1 Used         
     FROM tblEmployeeLeaveBalance b         
     WHERE b.EmployeeId = @EmployeeId and MONTH=@Month),         
    0        
))+@WeeklyOff) < (@BudgetMonthDays) THEN (@Attendance-@ExtraDays+(SELECT ISNULL(        
    (SELECT TOP 1 Used         
     FROM tblEmployeeLeaveBalance b         
     WHERE b.EmployeeId = @EmployeeId and MONTH=@Month),         
    0        
))+@WeeklyOff)             
        ELSE (@BudgetMonthDays)             
    END)) > 0 THEN ((@Attendance-@ExtraDays+(SELECT ISNULL(        
    (SELECT TOP 1 Used         
     FROM tblEmployeeLeaveBalance b         
     WHERE b.EmployeeId = @EmployeeId and MONTH=@Month),         
    0        
))+@WeeklyOff)-(CASE             
        WHEN (@Attendance-@ExtraDays+(SELECT ISNULL(        
    (SELECT TOP 1 Used         
     FROM tblEmployeeLeaveBalance b         
     WHERE b.EmployeeId = @EmployeeId and MONTH=@Month),         
    0        
))+@WeeklyOff) < (@BudgetMonthDays) THEN (@Attendance-@ExtraDays+(SELECT ISNULL(        
    (SELECT TOP 1 Used         
     FROM tblEmployeeLeaveBalance b         
     WHERE b.EmployeeId = @EmployeeId and MONTH=@Month),         
    0        
))+@WeeklyOff)             
        ELSE (@BudgetMonthDays)             
    END))             
        ELSE 0             
    END))  ,0)  
 --end    
 ,            
            
 isnull(@AbsentDays  ,0),  
 @IsActive  
 )            
 --    Select             
 --Month,Year,[LocationCategoryId],[DesignationId],[EmployeeName],[Ecode],[EmployeeId],Attendance,weeklyoffpresent,Actual_Weekly_Off,leave_availed,InitialPaybledays,TotalDaysInMonth            
 --,Payble_Days,EXTRA_DAYS            
 --from finalInitPaybleDays            
    --VALUES (            
    --    Source.[Month],            
    --    Source.[Year],            
    --    Source.LocationCategoryId,            
    --    Source.DesignationId,            
    --    Source.EmployeeName,            
    --    Source.Ecode,            
    --    Source.EmployeeId,            
    --    Source.Attendance,            
    --    Source.weeklyoffpresent,            
    --    Source.Actual_Weekly_Off,            
    --    Source.leave_availed,            
    --    Source.InitialPaybledays,            
    --    Source.TotalDaysInMonth,            
    --    Source.Payble_Days,            
    --    Source.EXTRA_DAYS            
    --);            
            
 ;WITH tablea AS (              
  SELECT *               
  FROM tblEmployee   
  where   
  --IsActive=1   
  --and   
  EmployeeId=@EmployeeId              
 ),              
              
tableb AS (              
    SELECT               
    a.ecode,              
      
    CASE WHEN b.TotalDaysInMonth IS NULL OR b.TotalDaysInMonth = 0       
         THEN 0       
         ELSE (a.BasicSalary / b.TotalDaysInMonth) * b.Payble_Days       
    END AS BasicSalary,              
      
    CASE WHEN b.TotalDaysInMonth IS NULL OR b.TotalDaysInMonth = 0       
         THEN 0       
         ELSE (a.HRA / b.TotalDaysInMonth) * b.Payble_Days       
    END AS HRA,              
      
    CASE WHEN b.TotalDaysInMonth IS NULL OR b.TotalDaysInMonth = 0       
         THEN 0       
         ELSE (a.CCA / b.TotalDaysInMonth) * b.Payble_Days       
    END AS CCA,              
      
    CASE WHEN b.TotalDaysInMonth IS NULL OR b.TotalDaysInMonth = 0       
         THEN 0       
         ELSE (a.SpecialAllowance / b.TotalDaysInMonth) * b.Payble_Days       
    END AS SpecialAllowance,              
      
    CASE WHEN b.TotalDaysInMonth IS NULL OR b.TotalDaysInMonth = 0       
         THEN 0       
         ELSE (a.DA / b.TotalDaysInMonth) * b.Payble_Days       
    END AS DA,              
      
    CASE WHEN b.TotalDaysInMonth IS NULL OR b.TotalDaysInMonth = 0       
         THEN 0       
         ELSE (a.Reimbersment / b.TotalDaysInMonth) * b.Payble_Days       
    END AS Reimbersment,          
      
    CASE WHEN b.TotalDaysInMonth IS NULL OR b.TotalDaysInMonth = 0       
         THEN 0       
         ELSE (a.Fuel_and_Maintainence / b.TotalDaysInMonth) * b.Payble_Days       
    END AS Fuel_and_Maintainence,          
      
    CASE WHEN b.TotalDaysInMonth IS NULL OR b.TotalDaysInMonth = 0       
         THEN 0       
         ELSE (a.Books_and_Periodicals / b.TotalDaysInMonth) * b.Payble_Days       
    END AS Books_and_Periodicals,          
      
    CASE WHEN b.TotalDaysInMonth IS NULL OR b.TotalDaysInMonth = 0       
         THEN 0       
         ELSE ([Professional Attire] / b.TotalDaysInMonth) * b.Payble_Days       
    END AS [Professional Attire],          
      
    CASE WHEN b.TotalDaysInMonth IS NULL OR b.TotalDaysInMonth = 0       
         THEN 0       
         ELSE ([Driver Wages] / b.TotalDaysInMonth) * b.Payble_Days       
    END AS [Driver Wages],          
      
    CASE WHEN b.TotalDaysInMonth IS NULL OR b.TotalDaysInMonth = 0       
         THEN 0       
         ELSE ([Mobile Bill] / b.TotalDaysInMonth) * b.Payble_Days       
    END AS [Mobile Bill],          
      
    CASE WHEN b.TotalDaysInMonth IS NULL OR b.TotalDaysInMonth = 0       
         THEN 0       
         ELSE ([Meal Voucher] / b.TotalDaysInMonth) * b.Payble_Days       
    END AS [Meal Voucher]      
    FROM tablea a              
    LEFT JOIN tbl_calculatePaybledays b ON a.ecode = b.ecode   and b.Month=@Month            
    --WHERE a.ecode = 'RTN106'              
)
,              
tableDeduction AS(              
 Select PF,ESIC,ECode,STCode,MONTH,Year from               
 tblEmployeeDeductions             
 where ECode=@Ecode      and  Month=@Month 
 --where ECode='RTN106' and STCode='RH01' and MONTH=6 and Year=2025              
),              
MonthlyGrossCTC AS (              
    SELECT               
        tb.ecode,              
        BasicSalary,              
        HRA,              
        CCA,              
        SpecialAllowance,              
        DA,              
  ISNULL([Reimbersment],0) AS Reimbursement,          
  ISNULL(Fuel_and_Maintainence,0) AS Fuel_and_Maintainence,          
  ISNULL(Books_and_Periodicals,0) AS Books_and_Periodicals,          
  ISNULL([Professional Attire],0) AS [Professional Attire],          
  ISNULL([Driver Wages],0) AS [Driver Wages],          
  ISNULL([Mobile Bill],0) AS [Mobile Bill],          
  ISNULL([Meal Voucher],0) AS [Meal Voucher],          
  ISNULL(td.PF,0) [PF],              
  ISNULL(td.ESIC,0) [ESIC],              
        --(BasicSalary + HRA + CCA + SpecialAllowance + DA-(ISNULL(td.PF,0)+ISNULL(td.ESIC,0))) AS MonthlyGrossCTC,              
  (IsNULL(BasicSalary,0) + IsNULL(HRA,0) + ISNULL(CCA,0) + ISNULL(SpecialAllowance,0) + ISNULL(DA,0) + ISNULL([Reimbersment],0)) AS MonthlyGrossCTC,              
        @Month AS [Month]              
    FROM tableb tb              
 Left JOIn tableDeduction td on tb.ecode=td.ECode           
 
),              
              
ExtraDayAllowance AS (      
    SELECT               
        a.ecode,      
  Case when @IsExtraDaysApplicable=0    
  then 0    
  else    
        CASE       
            WHEN b.TotalDaysInMonth IS NULL OR b.TotalDaysInMonth = 0 THEN 0      
            ELSE       
                CAST((      
                    (isnull(a.BasicSalary,0) + isnull(a.HRA,0) + isnull(a.CCA,0) + isnull(a.SpecialAllowance,0) + isnull(a.DA+a.Reimbersment,0))       
                    / b.TotalDaysInMonth      
                ) * isnull(b.Extra_Days,0) AS decimal(10, 2))      
        END    
  end    
    
  AS Extra_Day_Allowance,      
      
        '' AS Incentive,              
        '' AS Arrears              
      
    FROM tablea a              
    LEFT JOIN tbl_calculatePaybledays b ON a.ecode = b.ecode   and b.Month=@Month           
    -- WHERE a.ecode = 'RTN106'              
)      
  ,            
finalMonthSalary as(            
SELECT               
    a.*,              
    b.Extra_Day_Allowance,              
    b.Incentive,              
    b.Arrears              
FROM MonthlyGrossCTC a              
LEFT JOIN ExtraDayAllowance b ON a.ecode = b.ecode     
)            
MERGE dbo.tbl_Month_salary AS Target            
USING finalMonthSalary AS Source            
ON Target.ecode = Source.ecode AND Target.Month = Source.Month            
            
WHEN MATCHED THEN             
    UPDATE SET             
        Target.BasicSalary = Source.BasicSalary,            
        Target.HRA = Source.HRA,            
        Target.CCA = Source.CCA,            
        Target.SpecialAllowance = Source.SpecialAllowance,            
        Target.DA = Source.DA,            
        Target.Reimbersment = Source.Reimbursement,            
  Target.Fuel_and_Maintainence = Source.Fuel_and_Maintainence,          
  Target.Books_and_Periodicals = Source.Books_and_Periodicals,          
  Target.[Professional Attire] = Source.[Professional Attire],          
  Target.[Driver Wages] = Source.[Driver Wages],          
  Target.[Mobile Bill] = Source.[Mobile Bill],          
  Target.[Meal Voucher] = Source.[Meal Voucher],          
        Target.monthlyGrossCTC = Source.MonthlyGrossCTC,            
        Target.Extra_day_allowence = Source.Extra_Day_Allowance,            
        Target.Incentive = Source.Incentive,            
        Target.Arrers = Source.Arrears,            
  Target.PF = Source.PF,            
  Target.ESIC = Source.ESIC            
            
WHEN NOT MATCHED THEN             
    INSERT (            
        ecode,            
        BasicSalary,            
        HRA,            
        CCA,            
        SpecialAllowance,            
        DA,            
        Reimbersment,            
  Fuel_and_Maintainence,          
  [Books_and_Periodicals],          
  [Professional Attire],          
  [Driver Wages],          
  [Mobile Bill],          
  [Meal Voucher],          
        monthlyGrossCTC,            
        MONTH,            
        Extra_day_allowence,            
        Incentive,            
        Arrers,            
  PF,            
  ESIC            
    )            
    VALUES (            
        Source.ecode,            
        Source.BasicSalary,            
        Source.HRA,            
        Source.CCA,            
        Source.SpecialAllowance,            
        Source.DA,            
        Source.Reimbursement,            
  Source.Fuel_and_Maintainence,           
  Source.[Books_and_Periodicals],           
  Source.[Professional Attire],           
  Source.[Driver Wages],           
  Source.[Mobile Bill],           
  Source.[Meal Voucher],           
        Source.MonthlyGrossCTC,            
        Source.Month,            
        Source.Extra_Day_Allowance,            
        Source.Incentive,            
        Source.Arrears,            
  Source.PF,            
  Source.ESIC            
    );            

  Declare @MonthGrossSalaryEmp decimal(18,2);

--  Select @MonthGrossSalaryEmp=monthlyGrossCTC
--  from tbl_Month_salary where ecode=@Ecode and MONTH=@Month

--  Select @PTax=FinalPtRate from vw_PTPolicyMaster
--  where State=@State and SlabMin<=@MonthGrossSalaryEmp and SlabMax>=@MonthGrossSalaryEmp

--  SELECT 
--  @Lwf=
--    MAX(
--        CASE 
--            WHEN Employee IS NOT NULL 
--                THEN CASE 
--                        WHEN (Employee / 100.0) * ISNULL(@MonthGrossSalaryEmp,0) > ISNULL(EmployeeMax, 0) 
--                        THEN (Employee / 100.0) * ISNULL(@MonthGrossSalaryEmp,0)
--                        ELSE ISNULL(EmployeeMax, 0) 
--                     END
--            ELSE ISNULL(EmployeeMax, 0)
--        END
--    ) ,
    
--    @LwfEmployeer=MAX(
--        CASE 
--            WHEN Employeer IS NOT NULL 
--                THEN CASE 
--                        WHEN (Employeer / 100.0) * ISNULL(@MonthGrossSalaryEmp,0) > ISNULL(EmployeerMax, 0) 
--                        THEN (Employeer / 100.0) * ISNULL(@MonthGrossSalaryEmp,0) 
--                        ELSE ISNULL(EmployeerMax, 0) 
--                     END
--            ELSE ISNULL(EmployeerMax, 0)
--        END
--    ) 
--FROM 
--    LWFPolicyMaster
--WHERE 
--    State = @State;





 EXEC dbo.usp_UpsertEmpPFData             
    @Ecode = @Ecode,             
    @Month = @Month,             
    @Year = @Year,            
    @PF = @PFValue OUTPUT  -- pass OUTPUT            
            
 EXEC dbo.usp_UpsertEmpESICData             
    @Ecode = @Ecode,             
    @Month = @Month,             
    @Year = @Year,            
    @ESIC = @ESICValue OUTPUT       
     
Select @PTax=PTax,@Lwf=Lwf,@Tds=TDS,@Loan=Loan,@CashShort=CashShort,@DieselDeduction=DieselDeduction,@Penality=Penality from EmpTDSTable    
where E_CODE=@Ecode and MTH=@Month;
    
Select @INCENTIVE= [Incentive]    
      ,@ARREAR=[ARREAR]    
      ,@OVERTIME= [Overtime]    
      ,@FOODINGALLOWANCE =[Fooding_Allowance]    
      ,@MOBILEBILL = [Mobile_Bill]    
  FROM [HRMS].[dbo].[tblPayments]    
  where E_CODE=@Ecode and MONTH=@Month    
    
UPDATE tblEmployeeDeductions    
SET   
    TDS = ISNULL(@Tds,0),  
    PTax = ISNULL(@PTax,0),  
    Loan = ISNULL(@Loan,0),  
    CashShort = ISNULL(@CashShort,0),  
    DieselDeduction = ISNULL(@DieselDeduction,0),  
    Penality =ISNULL( @Penality,0),  
    Lwf = ISNULL(@Lwf,0),  
	LwfEmployeer = ISNULL(@LwfEmployeer,0),
    TotalDeductions =ISNULL(@ESICValue,0)+ ISNULL(@PFValue,0)+ISNULL(@Tds, 0) + ISNULL(@PTax, 0) + ISNULL(@Loan, 0) + ISNULL(@CashShort, 0) + ISNULL(@DieselDeduction, 0) + ISNULL(@Penality, 0) + ISNULL(@Lwf, 0)  
WHERE   
    ECode = @Ecode   
    AND [MONTH] = @Month;  
   
           
          
 UPDATE dbo.tbl_Month_salary            
SET [monthlyGrossCTC(afterDeduction)] = monthlyGrossCTC - try_cast(IsNuLL(@PFValue,0) as decimal) - try_cast(ISNULL(@ESICValue,0) as decimal) - try_cast(ISNULL(@Tds,0) as decimal)- try_cast(ISNULL(@PTax,0) as decimal)-try_cast(ISNULL(@Loan,0) as decimal)-
  
 try_cast(ISNULL(@CashShort,0) as decimal)- try_cast(ISNULL(@DieselDeduction,0) as decimal)- try_cast(ISNULL(@Penality,0) as decimal) - try_cast(ISNULL(@Lwf,0) as decimal) + try_cast(ISNULL(@INCENTIVE,0) as decimal) + try_cast(ISNULL(@ARREAR,0) as decimal
  
) + try_cast(ISNULL(@OVERTIME,0) as decimal)+ try_cast(ISNULL(@FOODINGALLOWANCE,0) as decimal)+ try_cast(ISNULL(@MOBILEBILL,0) as decimal)  + ISNULL(Extra_day_allowence,0)  
, PF=@PFValue,ESIC=@ESICValue,TDS=@Tds,PTax=@PTax,Loan=@Loan,CashShort=@CashShort,DieselDeduction=@DieselDeduction,Penality=@Penality,Lwf=@Lwf,Incentive=@INCENTIVE    
      ,Arrers=@ARREAR    
      ,[Overtime]=@OVERTIME    
      ,[Fooding_Allowance]=@FOODINGALLOWANCE    
      ,[Mobile_Bill]   =@MOBILEBILL    
WHERE ecode = @Ecode AND [Month] = @Month ;     
  
  
Select   
@BasicSalary=ISNULL(try_cast([BasicSalary(Bud.)] as decimal),0),  
@BasicSalaryCalc = ISNULL(try_cast([BasicSalary(Actual)] as decimal),0),  
@GrossEarnings = ISNULL(try_cast([Monthly Gross CTC(Actual)] as decimal),0)  
from   
vw_Emp_Attendance_Format (NOLOCK)  
where Ecode=@Ecode and [Month-Year]=@Month;  
  
Declare @MonthGratuity decimal(18,2)=dbo.fn_CalculateGratuity(@DOJ, @DOL, @Month, @BasicSalary);  
MERGE BonusAndGratutityOpening AS Target  
USING (SELECT @ECode AS ECode, @Month AS Month) AS Source  
    ON Target.ECode = Source.ECode AND Target.Month = Source.Month  
  
WHEN MATCHED THEN  
    UPDATE SET   
        ActualGratuity = -ISNULL(TRY_CAST(Gratuity AS DECIMAL), 0)   
                         + @MonthGratuity,  
        ActualBonus =   
            CASE   
                WHEN @IsBonusApplicable = 'Ctc'  THEN (@GrossEarnings / 12)
                WHEN @IsBonusApplicable = 'Stat' THEN (@BasicSalaryCalc * 0.0833)
                ELSE 0  
            END,  
  ClosingGratuity = @MonthGratuity  
  
WHEN NOT MATCHED THEN  
    INSERT (ECode, Month, Gratuity, Bonus, ActualGratuity, ActualBonus,ClosingGratuity)  
    VALUES (  
        @ECode,   
        @Month,   
        0,  
        0,  
        @MonthGratuity,   
        CASE   
                WHEN @IsBonusApplicable = 'Ctc'  THEN (@GrossEarnings / 12)
                WHEN @IsBonusApplicable = 'Stat' THEN (@BasicSalaryCalc * 0.0833)
                ELSE 0  
            END,  
  @MonthGratuity  
    );  
  
DECLARE @NextMonth VARCHAR(7);  
SET @NextMonth =   
    UPPER(LEFT(FORMAT(DATEADD(MONTH, 1, TRY_CAST('01-' + @Month AS DATE)), 'MMM'), 1)) +  
    LOWER(SUBSTRING(FORMAT(DATEADD(MONTH, 1, TRY_CAST('01-' + @Month AS DATE)), 'MMM'), 2, 2)) +  
    '-' +  
    RIGHT(FORMAT(DATEADD(MONTH, 1, TRY_CAST('01-' + @Month AS DATE)), 'yy'), 2);  
  
  
MERGE BonusAndGratutityOpening AS Target  
USING (SELECT @ECode AS ECode, @NextMonth AS Month) AS Source  
    ON Target.ECode = Source.ECode AND Target.Month = Source.Month  
  
WHEN MATCHED THEN  
    UPDATE SET   
        Gratuity = @MonthGratuity,  
        Bonus = 0,  
  ActualGratuity=0,  
  ActualBonus=0,  
  ClosingGratuity=0  
  
  
WHEN NOT MATCHED THEN  
    INSERT (ECode, Month, Gratuity, Bonus,ActualGratuity,ActualBonus,ClosingGratuity)  
    VALUES (@ECode, @NextMonth, @MonthGratuity, 0,0,0,0);  
  
  
  
    -- Select the inserted record and leave details for verification            
    --SELECT             
    --    Month,            
    --    LocationCategoryId,            
    --    DesignationId,            
    --    EmployeeName,            
    --    Ecode,            
    --    EmployeeId,            
    --    Attendance,            
    --    EmployeeLeaveBalanceId,            
    --    WeeklyOff,            
    --    ExtraDays,            
    --    Payroll,            
    --    Salary,            
    --    LeaveAdjust            
              
    --FROM tblEmployeeAttendancePayrollCalculation            
    --WHERE EmployeeId = @EmployeeId AND Month = @Month;            
            
    --SELECT             
    --    @EarnedLeaveAccrued AS EarnedLeaveAccrued,            
    --    @CasualLeaveAccrued AS CasualLeaveAccrued,            
    --    @CompOffUsed AS CompOffUsed,            
    --    @EarnedLeaveUsed AS EarnedLeaveUsed,            
    --    @CasualLeaveUsed AS CasualLeaveUsed,            
    --    @LeaveAdjust AS LeaveAdjust,            
    --    @AbsentDays AS AbsentDays,            
    --    @PayableDays AS PayableDays,            
    --    @Payroll AS PayableSalary;         
 END TRY        
    BEGIN CATCH        
        -- Handle the error and show passed parameter values        
        DECLARE @ErrorMessage NVARCHAR(4000);        
        DECLARE @ErrorSeverity INT;        
        DECLARE @ErrorState INT;        
        
        SET @ErrorMessage = ERROR_MESSAGE();        
        SET @ErrorSeverity = ERROR_SEVERITY();        
        SET @ErrorState = ERROR_STATE();        
        
        PRINT 'Error occurred in sp_CalculateEmployeePayroll';        
        PRINT 'Parameters:';        
        PRINT 'EmployeeId: ' + CAST(@EmployeeId AS VARCHAR);        
        PRINT 'Attendance: ' + CAST(@Attendance AS VARCHAR);        
        PRINT 'Month: ' + @Month;        
        PRINT 'Salary: ' + CAST(@Salary AS VARCHAR);        
        PRINT 'ExtraDays: ' + CAST(@ExtraDays AS VARCHAR);        
        PRINT 'Error Message: ' + @ErrorMessage;        
        
        -- Optionally re-throw the error        
        RAISERROR (@ErrorMessage, @ErrorSeverity, @ErrorState);        
    END CATCH        
END;   
  
  
--180
GO

-- -----------------------------------------------------------------------------
-- dbo.sp_CalculateEmployeePayroll_PT_LWF_Dev
-- -----------------------------------------------------------------------------
-- [sp_CalculateEmployeePayroll] '52398','14','Jun-25','50000.00','4'                                                                      
CREATE OR ALTER PROCEDURE [dbo].[sp_CalculateEmployeePayroll_PT_LWF_Dev] @EmployeeId INT,             
@Attendance Decimal(18, 2),             
@NC_Attendance Decimal(18, 2),             
@Month VARCHAR(6),             
@Salary DECIMAL(18, 2)= 0.0,             
@ExtraDays DECIMAL(18, 2) = 0.0,             
@IsActive Bit,             
@IsForFNF bit = 0 AS BEGIN             
SET             
  NOCOUNT ON;            
begin try -- Declare variables for calculations    
DECLARE @Gender VARCHAR(100);  
  
DECLARE @CycleStartDate date;            
DECLARE @CycleEndDate date;            
DECLARE @ExtraDaysUsed DECIMAL(18, 2) = 0;            
DECLARE @BudgetMonthDays INT;            
DECLARE @WeeklyOff DECIMAL(18, 2);            
DECLARE @WeeklyOffBud DECIMAL(18, 2);            
DECLARE @RemainingDays DECIMAL(18, 2);            
DECLARE @CompOffUsed DECIMAL(18, 2) = 0;            
DECLARE @EarnedLeaveUsed DECIMAL(18, 2) = 0;            
DECLARE @CasualLeaveUsed DECIMAL(18, 2) = 0;            
DECLARE @LeaveAdjust DECIMAL(18, 2) = 0;            
DECLARE @AbsentDays DECIMAL(18, 2) = 0;            
DECLARE @EarnedLeaveAccrued DECIMAL(18, 2);            
DECLARE @CasualLeaveAccrued DECIMAL(18, 2);            
DECLARE @PayableDays DECIMAL(18, 2);            
DECLARE @Payroll DECIMAL(18, 2);            
DECLARE @EmployeeName VARCHAR(100);            
DECLARE @Ecode VARCHAR(50);            
DECLARE @DesignationId INT;            
DECLARE @EmployeeLeaveBalanceId INT;            
DECLARE @MonthName VARCHAR(3);            
DECLARE @Year INT;            
DECLARE @CompOffBalance DECIMAL(18, 2);            
DECLARE @EarnedLeaveBalance DECIMAL(18, 2);            
DECLARE @CasualLeaveBalance DECIMAL(18, 2);            
Declare @PF DECIMAL(18, 2);            
DECLARE @ESIC DECIMAL(18, 2);            
Declare @Tds decimal(18, 2);            
Declare @PTax decimal(18, 2);            
Declare @Loan decimal(18, 2);            
Declare @CashShort decimal(18, 2);            
Declare @DieselDeduction decimal(18, 2);            
Declare @Penality decimal(18, 2);            
Declare @Lwf decimal(18, 2);            
Declare @LwfEmployeer decimal(18, 2);            
Declare @BgtTotalWeekOffs int;            
Declare @INCENTIVE Decimal(18, 2);            
Declare @ARREAR Decimal(18, 2);            
Declare @OVERTIME Decimal(18, 2);            
Declare @FOODINGALLOWANCE Decimal(18, 2);            
Declare @MOBILEBILL Decimal(18, 2);            
Declare @State nvarchar(100);            
Declare @MonthNo INT;            
Declare @UsedLeaves decimal(18, 2);            
Declare @LocationCategoryId nvarchar(100);            
Declare @LocationCategoryType nvarchar(100);            
Declare @IsExtraDaysApplicable bit = 0;            
Declare @BasicSalary decimal(18, 2),             
@DOJ datetime,             
@GrossEarnings decimal(18, 2),             
@DOL datetime,             
@IsBonusApplicable nvarchar(10),             
@BasicSalaryCalc decimal(18, 2);            
Declare @WeekdaysHolidayCount int = 0;            
Declare @FinalSatHolidayCount int = 0;            
Declare @IsStore bit;            
DECLARE @DepartmentId INT;            
DECLARE @IsNAPS BIT = 0;            
--DECLARE @SalaryCapDays INT = 25;                                            
--  Update tblEmployee                                                  
--Set PFApplicable=1                                                  
--where Ecode=@Ecode                                                
--UPDATE tblEmployee                                                
--SET ESICApplicable = CASE WHEN MonthlyGrossCTC <= 21000 THEN 1 ELSE 0 END                                                  
--WHERE Ecode = @Ecode                                                
--Update tblEmployee                                                  
--Set PFApplicable=1                                                  
--where Ecode=@Ecode and Ecode NOt IN (Select Ecode from EcodesForWhichNoPFNoESIC)                                                  
--Update tblEmployee                                                  
--Set PFApplicable=0                                      
--where Ecode=@Ecode and Ecode IN (Select Ecode from EcodesForWhichNoPFNoESIC)                                                  
---- If not in override → apply salary rule                                                  
--UPDATE tblEmployee                                                  
--SET ESICApplicable = CASE WHEN MonthlyGrossCTC <= 21000 THEN 1 ELSE 0 END                                                  
--WHERE Ecode = @Ecode                                                  
--  AND Ecode NOT IN (SELECT Ecode FROM EcodesForWhichNoPFNoESIC);                                                  
---- If in override → force ESIC = 0                                                  
--UPDATE tblEmployee                                                  
--SET ESICApplicable = 0                                                  
--WHERE Ecode = @Ecode                                                  
--  AND Ecode IN (SELECT Ecode FROM EcodesForWhichNoPFNoESIC);                                                  
---- PF ESIC Desuctions                                                  
--    DECLARE @AllowPF BIT = 0,                                                    
--        @AllowESIC BIT = 0,                                                    
--        @GrossSalary DECIMAL(18,2);                                    
---- Fetch override (if exists)                                                    
--SELECT                                                     
--    @AllowPF = IsPFShouldDeduct,                                                    
--    @AllowESIC = IsESICShouldDeduct                                                    
--FROM EcodesForWhichNoPFNoESIC                                                    
--WHERE Ecode = @Ecode;                                                    
---- If no row exists → treat both as NOT allowed                                                    
--SET @AllowPF = ISNULL(@AllowPF, 0);                                                    
--SET @AllowESIC = ISNULL(@AllowESIC, 0);                                                    
------ Get employee salary                                                    
----SELECT @GrossSalary = [GROSS SALARY]                                                    
----FROM tblEmployee                                                    
----WHERE Ecode = @Ecode;                                                    
--PRINT('AllowPF')                                            
--PRINT(@AllowPF)                                                    
--PRINT('AllowESIC')                                                    
--PRINT(@AllowESIC)                                                    
---- PF LOGIC                                                    
--IF @AllowPF = 1                                                    
--BEGIN                                                    
--    PRINT('Updating PF as 1')                                                    
--    UPDATE tblEmployee                                                    
--    SET PFApplicable = 1                                                    
--    WHERE Ecode = @Ecode;                                                    
--END                                   
--ELSE                                                     
--BEGIN                                                    
--PRINT('Updating PF as 0')                                                    
--    UPDATE tblEmployee                                                    
--    SET PFApplicable = 0                
--    WHERE Ecode = @Ecode;                                                    
--END                                                    
---- ESIC LOGIC                                                    
--IF @AllowESIC = 1                                                    
--BEGIN                                                    
--        PRINT('Updating ESIC as 1')                                                    
--    UPDATE tblEmployee                                                    
--    SET ESICApplicable = CASE WHEN [GROSS SALARY] <= 21000 THEN 1 ELSE 0 END                                         
--    WHERE Ecode = @Ecode;                                                    
--END                                                    
--ELSE                                                    
--BEGIN                                                    
--        PRINT('Updating ESIC as 0')                                                    
--    UPDATE tblEmployee                                                    
--    SET ESICApplicable = 0                                                    
--    WHERE Ecode = @Ecode;                                                    
--END                                          
----                                                  
Set             
  @ExtraDays =(            
    case when (            
      Select             
        1             
      from             
        tblEmployee a             
        Left join tblLocation b on a.LocationId = b.LocationId             
        Left Join tblDesignation c on a.DesignationId = c.DesignationId             
      where             
        (            
          b.LocationType IN (4)             
          OR b.STCode IN ('RH02')            
        ) --  where b.STCode NOT IN ('RH01','RD04','RH02')                                                       
        --and b.STCode Not LIKE 'D%'                   
        and a.EmployeeId = @EmployeeId            
    ) = 1 then 0 when (            
      Select             
        1             
      from             
        tblEmployee a             
        Left join tblLocation b on a.LocationId = b.LocationId             
        Left Join tblDesignation c on a.DesignationId = c.DesignationId             
      where             
        c.DesignationId IN(72, 1265)             
        and a.LocationId != 313             
        and a.EmployeeId = @EmployeeId            
    ) = 1 then 0 else @ExtraDays end            
  );            
print(@ExtraDays) DECLARE @MonthStart date;            
DECLARE @MonthEnd date;            
DECLARE @PolicyType varchar(20);            
DECLARE @CustomType varchar(20);            
DECLARE @FromDay int;            
DECLARE @ToDay int;            
-- Parse @Month like 'Dec-21'                      
SET             
  @MonthName = LEFT(@Month, 3);            
SET             
  @MonthNo = CASE @MonthName WHEN 'Jan' THEN 1 WHEN 'Feb' THEN 2 WHEN 'Mar' THEN 3 WHEN 'Apr' THEN 4 WHEN 'May' THEN 5 WHEN 'Jun' THEN 6 WHEN 'Jul' THEN 7 WHEN 'Aug' THEN 8 WHEN 'Sep' THEN 9 WHEN 'Oct' THEN 10 WHEN 'Nov' THEN 11 WHEN 'Dec' THEN 12 END;  
  
    
       
        
         
SET             
  @Year = 2000 + CAST(            
    RIGHT(@Month, 2) AS int            
  );            
SET             
  @MonthStart = DATEFROMPARTS(@Year, @MonthNo, 1);            
SET             
  @MonthEnd = EOMONTH(@MonthStart);            
-- Pick applicable policy (latest effective <= month start)                      
SELECT             
  TOP 1 @PolicyType = [Type],             
  @CustomType = [Custom_type],             
  @FromDay = [From],             
  @ToDay = [To]             
FROM             
  dbo.SalaryCyclePolicy             
WHERE             
  EffectiveDate <= @MonthStart             
ORDER BY       
  EffectiveDate DESC;            
-- Defaults                      
SET             
  @PolicyType = ISNULL(@PolicyType, 'Monthly');            
SET             
  @CustomType = ISNULL(@CustomType, '');            
IF (@PolicyType = 'Monthly') BEGIN -- Calendar month                      
SET             
  @CycleStartDate = @MonthStart;            
SET             
  @CycleEndDate = @MonthEnd;          END ELSE IF (            
  @PolicyType = 'Custom'             
  AND @CustomType = 'Same'            
) BEGIN -- Same month From/To (cap for short months)                      
DECLARE @CurrMonthLastDay int = DAY(@MonthEnd);            
SET             
  @CycleStartDate = DATEFROMPARTS(            
    YEAR(@MonthStart),             
    MONTH(@MonthStart),             
    CASE WHEN @FromDay > @CurrMonthLastDay THEN @CurrMonthLastDay ELSE @FromDay END            
  );            
SET       
  @CycleEndDate = DATEFROMPARTS(            
    YEAR(@MonthStart),             
    MONTH(@MonthStart),             
    CASE WHEN @ToDay > @CurrMonthLastDay THEN @CurrMonthLastDay ELSE @ToDay END            
  );            
END ELSE IF (            
  @PolicyType = 'Custom'             
  AND @CustomType = 'Previous'            
) BEGIN -- Previous month From -> Current month To (cap for short months)                      
DECLARE @PrevMonthStart date = DATEADD(MONTH, -1, @MonthStart);            
DECLARE @PrevMonthLastDay int = DAY(            
  EOMONTH(@PrevMonthStart)            
);            
set             
  @CurrMonthLastDay = DAY(@MonthEnd);            
SET             
  @CycleStartDate = DATEFROMPARTS(            
    YEAR(@PrevMonthStart),             
    MONTH(@PrevMonthStart),             
    CASE WHEN @FromDay > @PrevMonthLastDay THEN @PrevMonthLastDay ELSE @FromDay END            
  );            
SET             
  @CycleEndDate = DATEFROMPARTS(            
    YEAR(@MonthStart),             
    MONTH(@MonthStart),             
    CASE WHEN @ToDay > @CurrMonthLastDay THEN @CurrMonthLastDay ELSE @ToDay END            
 );            
END ELSE BEGIN -- Fallback                      
SET             
  @CycleStartDate = @MonthStart;            
SET             
  @CycleEndDate = @MonthEnd;            
END;            
PRINT 'Entered cycle logic';            
-- PRINT CYCLE                      
PRINT 'PolicyType     = ' + ISNULL(@PolicyType, '');            
PRINT 'CustomType     = ' + ISNULL(@CustomType, '');            
PRINT 'FromDay        = ' + ISNULL(            
  CAST(            
    @FromDay as varchar(10)            
  ),             
  ''            
);            
PRINT 'ToDay          = ' + ISNULL(            
  CAST(            
    @ToDay as varchar(10)            
  ),             
  ''            
);            
PRINT 'CycleStartDate = ' + CONVERT(            
  varchar(10),             
  @CycleStartDate,             
  120            
);            
PRINT 'CycleEndDate   = ' + CONVERT(            
  varchar(10),             
  @CycleEndDate,             
  120            
);            
exec [usp_GenerateWeekOffCalendar] @Month,             
@CycleStartDate,             
@CycleEndDate -- Inclusive day count            
SET @BudgetMonthDays = DATEDIFF( day, @CycleStartDate, DATEADD(day, 1, @CycleEndDate) );          
--DECLARE @DOJ date;          
DECLARE @TBudgetMonthDays int;          
DECLARE @DateOfLeft date;          
DECLARE @EffectiveStart date;          
DECLARE @EffectiveEnd date;          
          
SELECT           
    @DOJ = DOJ,          
    @DateOfLeft = DateOfLeft          
FROM tblEmployee          
WHERE EmployeeId =@EmployeeId;          
          
-- Default full cycle days          
SET @BudgetMonthDays = DATEDIFF(DAY, @CycleStartDate, DATEADD(DAY, 1, @CycleEndDate));          
          
-- Adjust start date          
SET @EffectiveStart =           
    CASE           
        WHEN @DOJ IS NOT NULL AND @DOJ > @CycleStartDate THEN @DOJ          
        ELSE @CycleStartDate          
    END;          
          
-- Adjust end date          
SET @EffectiveEnd =          
 CASE           
        WHEN @DateOfLeft IS NOT NULL AND @DateOfLeft < @CycleEndDate THEN @DateOfLeft          
        ELSE @CycleEndDate          
    END;          
          
-- Final payable / budget days          
IF @EffectiveStart > @EffectiveEnd          
BEGIN          
    SET @tBudgetMonthDays = 0;          
END          
ELSE          
BEGIN          
    SET @tBudgetMonthDays = DATEDIFF(DAY, @EffectiveStart, DATEADD(DAY, 1, @EffectiveEnd));          
END;          
          
PRINT 'DOJ             = ' + ISNULL(CONVERT(varchar(10), @DOJ, 120), '');          
PRINT 'DateOfLeft      = ' + ISNULL(CONVERT(varchar(10), @DateOfLeft, 120), '');          
PRINT 'EffectiveStart  = ' + ISNULL(CONVERT(varchar(10), @EffectiveStart, 120), '');          
PRINT 'EffectiveEnd    = ' + ISNULL(CONVERT(varchar(10), @EffectiveEnd, 120), '');          
PRINT 'BudgetMonthDays = ' + CAST(@tBudgetMonthDays AS varchar(10));           
--   SET @BudgetMonthDays=25;                                              
DECLARE @satCount INT;            
SELECT             
  @satCount = dbo.SatCountFromMonth(            
    @Month, @CycleStartDate, @CycleEndDate            
  );            
-- Get WeeklyOff from tblLocationDesignationPolicy                                                                      
Select             
  @Ecode = Ecode,             
  @LocationCategoryType = d.LocationTypeName,             
  @LocationCategoryId = b.STCode,             
  @DesignationId = c.DesignationId,             
  @IsExtraDaysApplicable = case when a.LocationId = 313 then 1 when a.LocationId = 328 then 1 else IsNULL(IsExtraDayApplicable, 0) end,             
  --@BasicSalary=ISNULL(try_cast(BasicSalary as decimal),0),                                                            
  @DOJ = ISNULL(            
    try_cast(DOJ as datetime),             
    GETDATE()            
  ),             
  @DOL = try_cast(DateOfLeft as datetime),             
  @IsBonusApplicable = ISNULL(BonusApplicable, 'No'),             
  @DepartmentId = a.DepartmentId,             
  --@GrossEarnings=ISNULL(try_cast([GROSS SALARY] as decimal),0),@Reimbursement=ISNULL(try_cast(Reimbersment as decimal),0)                                       
  --@IsNAPS = CASE                                   
  --               WHEN UPPER(LTRIM(RTRIM(ISNULL(c.DesignationName, '')))) LIKE 'NAPS%'                                   
  --               THEN 1 ELSE 0                                   
  --             END                                  
  @IsNAPS = CASE WHEN NULLIF(            
    LTRIM(            
      RTRIM(            
        ISNULL(a.AOCode, '')            
      )            
    ),             
    ''            
  ) IS NOT NULL THEN 1 ELSE 0 END,             
  @IsForFNF = CASE WHEN ISNULL(a.IsActive, 1) = 0 THEN 1 ELSE @IsForFNF END ,  
  @Gender=a.GENDER  
FROM             
  tblEmployee a WITH (NOLOCK)             
  LEFT JOIN tblLocation b WITH (NOLOCK) ON a.LocationId = b.LocationId             
  LEFT JOIN tblDesignation c WITH (NOLOCK) ON a.DesignationId = c.DesignationId             
  LEFT JOIN tblLocationType d WITH (NOLOCK) ON b.LocationType = d.Id             
WHERE             
  a.EmployeeId = @EmployeeId;            
--  SET @IsNAPS = CASE WHEN @DepartmentId = 35 THEN 1 ELSE 0 END;                                                  
--where Ecode='RTNR65'               
IF (@IsNAPS = 0) BEGIN             
UPDATE             
  tblEmployee             
SET             
  PFApplicable = 1             
WHERE             
  Ecode = @Ecode;            
UPDATE             
  tblEmployee             
SET             
  ESICApplicable = CASE WHEN (            
    (            
      ISNULL(BasicSalary, 0)+ ISNULL(HRA, 0)+ ISNULL(CCA, 0)+ ISNULL(DA, 0)+ ISNULL(ExtraAllowance, 0)+ ISNULL(SpecialAllowance, 0)            
    )            
  ) <= 21000 THEN 1 ELSE 0 END             
WHERE             
  Ecode = @Ecode;            
END ELSE BEGIN             
UPDATE             
  tblEmployee             
SET             
  PFApplicable = 0,         
  ESICApplicable = 0             
WHERE             
  Ecode = @Ecode;            
END --Update tblEmployee                                                  
--Set PFApplicable=1                                                
--where Ecode=@Ecode                                                
--UPDATE tblEmployee                          
--SET ESICApplicable = CASE WHEN  COALESCE([GROSS SALARY],monthlyGrossCTC,0) <= 21000 THEN 1 ELSE 0 END                                                  
--WHERE Ecode = @Ecode                                               
-- Update tblEmployee                                                  
--Set PFApplicable=1                                                  
--where Ecode=@Ecode and Ecode NOt IN (Select Ecode from EcodesForWhichNoPFNoESIC)                                   
--Update tblEmployee                                                  
--Set PFApplicable=0                                                  
--where Ecode=@Ecode and Ecode IN (Select Ecode from EcodesForWhichNoPFNoESIC)                                                  
--UPDATE tblEmployee                                                  
--SET ESICApplicable = CASE WHEN MonthlyGrossCTC <= 21000 THEN 1 ELSE 0 END                                        
--WHERE Ecode = @Ecode                                                  
--  AND Ecode NOT IN (SELECT Ecode FROM EcodesForWhichNoPFNoESIC);                                                  
--UPDATE tblEmployee                                                  
--SET ESICApplicable = 0                                                  
--WHERE Ecode = @Ecode                      
--  AND Ecode IN (SELECT Ecode FROM EcodesForWhichNoPFNoESIC);                             
Select             
  @WeekdaysHolidayCount = WeekdaysHolidayCount,             
  @FinalSatHolidayCount = FinalSatHolidayCount             
from             
  ufn_GetEmpHolidayCounts_ForMonth(            
    @Ecode, @Month, @CycleStartDate, @CycleEndDate            
  )             
Set             
  @WeekdaysHolidayCount =(            
    case when (            
      Select             
        1             
      from             
        tblEmployee a             
        Left join tblLocation b on a.LocationId = b.LocationId             
        Left Join tblDesignation c on a.DesignationId = c.DesignationId             
      where             
        c.DesignationId IN(72, 1265)             
        and a.LocationId != 313             
        and a.EmployeeId = @EmployeeId            
    ) = 1 then 0 else @WeekdaysHolidayCount end            
  );            
Set             
  @FinalSatHolidayCount =(            
    case when (            
      Select             
        1             
      from             
        tblEmployee a             
        Left join tblLocation b on a.LocationId = b.LocationId             
        Left Join tblDesignation c on a.DesignationId = c.DesignationId             
      where             
        c.DesignationId IN(72, 1265)             
        and a.LocationId != 313             
        and a.EmployeeId = @EmployeeId            
    ) = 1 then 0 else @FinalSatHolidayCount end            
  );            
Set             
  @Attendance = @Attendance + ISNULL(@WeekdaysHolidayCount, 0) + ISNULL(@FinalSatHolidayCount, 0)             
Select             
  @State = State             
from             
  StoreStateLinking             
where             
  [ST-CD] = @LocationCategoryId --PRINT('State : '+@State)                                                          
  PRINT('STATE') --PRINT(@State)                                                          
  --Print 'For ECOde : RTNR65, '+'LocationCategoryId : '+@LocationCategoryId                                                               
  DECLARE @weekendCount INT;            
SELECT             
  @weekendCount = dbo.WeekendCountFromMonth(            
    @Month, @CycleStartDate, @CycleEndDate            
  );            
DECLARE @Matched BIT = 0;     
IF (            
  @LocationCategoryId = 'DH24'             
  AND @DesignationId IN (72, 1265)            
) BEGIN             
SET             
  @WeeklyOff = 0;            
SET             
  @Matched = 1;            
-- Mark as matched so no further checks happen                                                          
END IF (            
  @LocationCategoryId != 'RH01'             
  AND @DesignationId IN (72, 1265)            
) BEGIN             
SET             
  @WeeklyOff = 0;            
SET             
  @Matched = 1;            
-- Mark as matched so no further checks happen                                                          
END --  else IF @Ecode IN (                                                      
--    'V26669','V09157','V12071','V08591','V30858','V26553',                                                      
--    'V00577','V36231','V36215','V01668','V03952','V36217',                             
--    'V30839'                                  
--    --,'V16380'                                                      
--    ,'V2S176','V2S1702','V2S267','V38638'                                               
--    --'V09740','V2S209','V2S263'                                                      
--    )                                                      
--    begin                                          
--        SELECT TOP 1 @WeeklyOff = WeeklyOff                                                                
--          FROM tblLocationDesignationPolicy                                                                
--          WHERE LocationCategoryId = 'Universal'                                                                
--            AND CAST(TotalAttendance AS INT) <= @Attendance+ISNULL(@WeekdaysHolidayCount,0) + ISNULL(@FinalSatHolidayCount,0)                                                 
--          ORDER BY CAST(TotalAttendance AS INT) DESC;          
--         Set @Matched=1;                                                      
--    end                                                      
--    else IF @Ecode IN (                                                      
--    'V16380',                                                      
--    'V09740','V2S209','V2S263'                                                      
--    )                                                      
--    begin                                                       
--       SELECT TOP 1                                                           
--    @WeeklyOff = WeeklyOff,                                                           
--    @Matched = 1                                                          
--FROM tblLocationDesignationPolicy                                            
--WHERE LocationCategoryId = @LocationCategoryType                                                          
--  AND ForWhichWeeks = @weekendCount                                                          
--  AND DesignationId IS NULL                 
--  AND CAST(TotalAttendance AS INT) <= @Attendance+ISNULL(@WeekdaysHolidayCount,0) + ISNULL(@FinalSatHolidayCount,0)                                                          
--ORDER BY CAST(TotalAttendance AS INT) DESC;                                                      
--         Set @Matched=1;                                                      
--    end                                                      
--Init attempt : Ecode Wise Mapping                                                        
IF @Matched = 0 begin             
SELECT             
  TOP 1 @WeeklyOff = WeeklyOff,             
  @Matched = 1             
FROM             
  EcodeWiseWeekOffMapping             
WHERE             
  Ecode = @Ecode             
  and MONTH = @Month --AND TRY_CAST(TotalAttendance AS DECIMAL(18,2)) <= @NC_Attendance+ISNULL(@WeekdaysHolidayCount,0) + ISNULL(@FinalSatHolidayCount,0)                                                                  
  AND TRY_CAST(            
    TotalAttendance AS DECIMAL(18, 2)            
  ) <= @NC_Attendance             
ORDER BY             
  TRY_CAST(            
    TotalAttendance AS DECIMAL(18, 2)            
  ) DESC;            
print('Ecode Week WIse Mapping') end -- First attempt: Location + Designation                                                               
IF @Matched = 0 BEGIN print('1st attempt')             
SELECT             
  TOP 1 @WeeklyOff = WeeklyOff,             
  @Matched = 1             
FROM             
  tblLocationDesignationPolicy             
WHERE             
  LocationCategoryId = @LocationCategoryId             
  AND DesignationId = @DesignationId             
  and [Month-Year] = @Month --AND TRY_CAST(TotalAttendance AS DECIMAL(18,2)) <= @NC_Attendance+ISNULL(@WeekdaysHolidayCount,0) + ISNULL(@FinalSatHolidayCount,0)                  
  AND TRY_CAST(            
    TotalAttendance AS DECIMAL(18, 2)            
  ) <= @NC_Attendance             
ORDER BY             
  TRY_CAST(            
    TotalAttendance AS DECIMAL(18, 2)            
  ) DESC;            
end -- Second attempt: Location only                                                          
--location only                                                    
IF @Matched = 0 BEGIN             
SELECT             
  TOP 1 @WeeklyOff = WeeklyOff,             
  @Matched = 1             
FROM             
  tblLocationDesignationPolicy             
WHERE             
  LocationCategoryId = @LocationCategoryId             
  and [Month-Year] = @Month             
  AND DesignationId IS NULL --AND TRY_CAST(TotalAttendance AS DECIMAL(18,2)) <= @NC_Attendance+ISNULL(@WeekdaysHolidayCount,0) + ISNULL(@FinalSatHolidayCount,0)                                                          
  AND TRY_CAST(            
    TotalAttendance AS DECIMAL(18, 2)            
  ) <= @NC_Attendance             
ORDER BY             
  TRY_CAST(            
    TotalAttendance AS DECIMAL(18, 2)            
  ) DESC;            
PRINt('2nd Attempt : Loc only') END --location type                   
IF @Matched = 0 BEGIN             
SELECT             
  TOP 1 @WeeklyOff = WeeklyOff,             
  @Matched = 1             
FROM             
  tblLocationDesignationPolicy             
WHERE             
  LocationCategoryId = @LocationCategoryType             
  and [Month-Year] = @Month             
  AND ForWhichWeeks = CASE WHEN @LocationCategoryType = 'HO' THEN @weekendCount WHEN @LocationCategoryType IN ('DC', 'HUB') THEN @satCount ELSE @weekendCount -- default fallback                                            
  END             
  AND DesignationId IS NULL --AND TRY_CAST(TotalAttendance AS DECIMAL(18,2)) <= @NC_Attendance+ISNULL(@WeekdaysHolidayCount,0) + ISNULL(@FinalSatHolidayCount,0)                      
  AND TRY_CAST(            
    TotalAttendance AS DECIMAL(18, 2)            
  ) <= @NC_Attendance             
ORDER BY             
  TRY_CAST(            
    TotalAttendance AS DECIMAL(18, 2)            
  ) DESC;            
PRINt('2nd Attempt : Loc Type only') Print(@LocationCategoryType) Print(@weekendCount) Print(@satCount) PRINt('2nd Attempt : Loc only') END -- Third attempt: Universal                                                                  
IF @Matched = 0 BEGIN print('3rd attempt')             
SELECT             
  TOP 1 @WeeklyOff = WeeklyOff             
FROM             
  tblLocationDesignationPolicy             
WHERE             
  LocationCategoryId = 'Universal'             
  and [Month-Year] = @Month --AND TRY_CAST(TotalAttendance AS DECIMAL(18,2)) <= @NC_Attendance+ISNULL(@WeekdaysHolidayCount,0) + ISNULL(@FinalSatHolidayCount,0)                  
  AND TRY_CAST(            
    TotalAttendance AS DECIMAL(18, 2)            
  ) <= @NC_Attendance             
ORDER BY             
  TRY_CAST(            
    TotalAttendance AS DECIMAL(18, 2)            
  ) DESC;            
END PRINT('WEEKLY OFF') PRINT(@WeeklyOff) --SELECT @BgtTotalWeekOffs = TotalWeekOffs                                                           
--FROM BudgetWeekoffMaster                                                    
--WHERE If_Joining_Date = @DOJ                                                           
--  AND LocationCode = @LocationCategoryId                                                           
-- AND DesignationId = @DesignationId;                                                          
SELECT             
  @BgtTotalWeekOffs = TotalWeekOffs             
FROM             
  dbo.fn_GetEmployeeWeekOffsByEcode(@Month, @Ecode);            
PRINT('BGT WEEKLY OFF') PRINT(@BgtTotalWeekOffs)             
SET             
  @WeeklyOff = CASE WHEN @BgtTotalWeekOffs < @WeeklyOff THEN @BgtTotalWeekOffs ELSE @WeeklyOff END;            
PRINT('END WEEKLY OFF') PRINT(@WeeklyOff) -- Handle attendance = 0                                                                
IF @Attendance = 0             
or @WeeklyOff is NULL             
SET             
  @WeeklyOff = 0;            
--SELECT TOP 1 @WeeklyOff = WeeklyOff                                                                      
--FROM (                                                                      
--    SELECT                                                                       
--        WeeklyOff,                                                    
--        TotalAttendance,                                   
--        CAST(                                                                      
--            CASE                                                                       
--                WHEN CHARINDEX('-', TotalAttendance) > 0                                                
--                THEN LEFT(TotalAttendance, CHARINDEX('-', TotalAttendance) - 1)                                                                      
--                ELSE TotalAttendance                                                                
--            END AS INT) AS LowerBound,                                                                      
--        CAST(                                                                      
--            CASE                                                                       
--                WHEN CHARINDEX('-', TotalAttendance) > 0                                                                       
--                THEN SUBSTRING(TotalAttendance, CHARINDEX('-', TotalAttendance) + 1, LEN(TotalAttendance))                                             
--                ELSE TotalAttendance                                                                      
--            END AS INT) AS UpperBound                                                                      
--    FROM tblLocationDesignationPolicy                                                                      
--    WHERE LocationCategoryId = 1 -- Assuming HO                                                                      
--        --AND DesignationId = (SELECT TOP 1 DesignationId FROM tblEmployee WHERE EmployeeId = @EmployeeId)                                                                      
--) AS Policy                                            
--WHERE @Attendance >= LowerBound                                                                      
--  AND (                                                                      
--      @Attendance <= UpperBound                                                                      
--      OR UpperBound = (                                                                      
--          SELECT MAX(                                                                      
--             CAST(                         
--                  CASE                                                                       
--                      WHEN CHARINDEX('-', TotalAttendance) > 0                                                 
--                      THEN SUBSTRING(TotalAttendance, CHARINDEX('-', TotalAttendance) + 1, LEN(TotalAttendance))    
--                      ELSE TotalAttendance                                                                      
--                  END AS INT))                                                                      
--          FROM tblLocationDesignationPolicy                                                
--          WHERE LocationCategoryId = 1                                                                      
--            --AND DesignationId = (SELECT DesignationId FROM tblEmployee WHERE EmployeeId = @EmployeeId)                                                            
--)                                                                      
--   )                                                                      
--ORDER BY UpperBound DESC;                                           
-- Get Employee details and EmployeeLeaveBalanceId                                                                      
SELECT             
  @EmployeeName = [FULL NAME],             
  @Ecode = Ecode,             
  @DesignationId = DesignationId             
FROM             
  tblEmployee             
WHERE             
  EmployeeId = @EmployeeId;            
SELECT             
  @EmployeeLeaveBalanceId = EmployeeLeaveBalanceId             
FROM             
  tblEmployeeLeaveBalance      
WHERE             
  EmployeeId = @EmployeeId             
  and MONTH = @Month;            
-- Validation checks                                                                      
--IF @WeeklyOff IS NULL                                                                      
--BEGIN                                           
--    RAISERROR ('No matching policy found for the given attendance and employee.', 16, 1);                                                                      
--    RETURN;                                                                      
--END                                                                      
--IF @EmployeeName IS NULL OR @Ecode IS NULL OR @DesignationId IS NULL                                                                      
--BEGIN                                                                      
--    RAISERROR ('Employee details not found for EmployeeId %d.', 16, 1, @EmployeeId);                                                                      
--    RETURN;                                                                      
--END                                                                    
--IF @EmployeeLeaveBalanceId IS NULL                                                                      
--BEGIN                                                                      
--    RAISERROR ('Employee leave balance not found for EmployeeId %d.', 16, 1, @EmployeeId);                                                                      
--    RETURN;                                                                      
--END                                                                      
-- Show opening leave balances                                                                      
SELECT             
  @CompOffBalance = ISNULL(            
    (            
      SELECT             
        CompOffBalance             
      FROM             
        tblEmployeeLeaveBalance             
      WHERE             
        EmployeeLeaveBalanceId = @EmployeeLeaveBalanceId            
    ),             
    0            
  ),             
  @EarnedLeaveBalance = ISNULL(            
    (            
      SELECT             
        EarnedLeaveBalance             
      FROM             
        tblEmployeeLeaveBalance             
      WHERE             
        EmployeeLeaveBalanceId = @EmployeeLeaveBalanceId            
    ),             
    0            
  ),             
  @CasualLeaveBalance = ISNULL(            
    (            
      SELECT             
        CasualLeaveBalance             
      FROM             
        tblEmployeeLeaveBalance             
      WHERE             
        EmployeeLeaveBalanceId = @EmployeeLeaveBalanceId            
    ),             
    0            
  );            
--SELECT                                                                       
--    @CompOffBalance AS OpeningCompOffBalance,                                                   
--    @EarnedLeaveBalance AS OpeningEarnedLeaveBalance,                                    
--    @CasualLeaveBalance AS OpeningCasualLeaveBalance;                                                                   
---- Credit leaves based on attendance and weekly off                                                                      
--SET @EarnedLeaveAccrued = ((@Attendance + @WeeklyOff) / @BudgetMonthDays) * 1.25;                                                                      
--SET @CasualLeaveAccrued = ((@Attendance + @WeeklyOff) / @BudgetMonthDays) * 0.58;                                                                      
-- Credit leaves based on attendance and weekly off caping                                                                      
-- Update tblEmployeeLeaveBalance with credited leaves                                                                      
UPDATE             
  tblEmployeeLeaveBalance             
SET             
  EarnedLeaveBalance = EarnedLeaveBalance,             
  --EarnedLeaveAcquired = EarnedLeaveAcquired + @EarnedLeaveAccrued,                                                                      
  CasualLeaveBalance = CasualLeaveBalance,             
  --CasualLeaveAcquired = CasualLeaveAcquired + @CasualLeaveAccrued,                                                                      
  LastCreditedMonth = DATEFROMPARTS(            
    @Year, CASE @MonthName WHEN 'Jan' THEN 1 WHEN 'Feb' THEN 2 WHEN 'Mar' THEN 3 WHEN 'Apr' THEN 4 WHEN 'May' THEN 5 WHEN 'Jun' THEN 6 WHEN 'Jul' THEN 7 WHEN 'Aug' THEN 8 WHEN 'Sep' THEN 9 WHEN 'Oct' THEN 10 WHEN 'Nov' THEN 11 WHEN 'Dec' THEN 12 END,    
  
    
      
        
         
    1            
  ),             
  LastUpdatedOn = GETDATE()             
WHERE             
  EmployeeLeaveBalanceId = @EmployeeLeaveBalanceId;            
-- Calculate remaining days                                                                      
SET             
  @RemainingDays = @BudgetMonthDays - (@Attendance - @ExtraDays) - @WeeklyOff;            
PRINT('@Attendance') PRINT(@Attendance) PRINT('@ExtraDays') PRINT(@ExtraDays) PRINT('@WeeklyOff') PRINT(@WeeklyOff) -- SET @RemainingDays = 25 - (@Attendance- @ExtraDays) - @WeeklyOff                                               
PRINT('@RemainingDays') PRINT(@RemainingDays) If @RemainingDays < 0             
Set             
  @RemainingDays = 0             
set             
  @AbsentDays = @RemainingDays IF (@IsNAPS = 1) BEGIN -- No leave usage at all                   
SET             
  @CompOffUsed = 0;            
SET             
  @CasualLeaveUsed = 0;            
SET             
  @EarnedLeaveUsed = 0;            
SET             
  @LeaveAdjust = 0;            
-- No accrual and no tblEmployeeLeaveBalance update                  
SET             
  @EarnedLeaveAccrued = 0;            
SET             
  @CasualLeaveAccrued = 0;            
-- No leave availed             --SET @UsedLeave = 0;                   
END ELSE BEGIN IF @RemainingDays > 0 BEGIN IF (@IsForFNF = 0) BEGIN -- Get leave balances after crediting                                                             
SELECT             
  @CompOffBalance = ISNULL(            
    (            
      SELECT             
        CompOffBalance             
      FROM             
        tblEmployeeLeaveBalance             
      WHERE             
        EmployeeLeaveBalanceId = @EmployeeLeaveBalanceId            
    ),             
    0            
  ),             
  @EarnedLeaveBalance = ISNULL(            
    (            
      SELECT             
        EarnedLeaveBalance             
      FROM             
        tblEmployeeLeaveBalance             
      WHERE             
        EmployeeLeaveBalanceId = @EmployeeLeaveBalanceId            
    ),             
    0            
  ),             
  @CasualLeaveBalance = ISNULL(            
    (            
      SELECT             
        CasualLeaveBalance             
      FROM             
        tblEmployeeLeaveBalance             
      WHERE             
        EmployeeLeaveBalanceId = @EmployeeLeaveBalanceId            
    ),             
    0            
  );            
--     -- Deduct from CompOffBalance                                                                      
--     IF @RemainingDays > 0 AND @CompOffBalance > 0                                                                      
--   BEGIN                                                                      
--         SET @CompOffUsed = CASE WHEN @RemainingDays <= @CompOffBalance THEN @RemainingDays ELSE @CompOffBalance END;                                                                      
--         SET @RemainingDays = @RemainingDays - @CompOffUsed;                                                                      
--     END                                                                      
---- Deduct from CasualLeaveBalance                                
--     IF @RemainingDays > 0 AND @CasualLeaveBalance > 0                                                                      
--     BEGIN                                                                      
--         SET @CasualLeaveUsed = CASE WHEN @RemainingDays <= @CasualLeaveBalance THEN @RemainingDays ELSE @CasualLeaveBalance END;                        
--         SET @RemainingDays = @RemainingDays - @CasualLeaveUsed;                                                                    
--     END                                                                    
--     -- Deduct from EarnedLeaveBalance                                                                      
--     IF @RemainingDays > 0 AND @EarnedLeaveBalance > 0                                                                      
--     BEGIN                                                                      
--        SET @EarnedLeaveUsed = CASE WHEN @RemainingDays <= @EarnedLeaveBalance THEN @RemainingDays ELSE @EarnedLeaveBalance END;                                                                      
--         SET @RemainingDays = @RemainingDays - @EarnedLeaveUsed;                                                                      
--     END                                                                      
-- Deduct from CompOffBalance                                
IF @RemainingDays > 0             
AND @ExtraDays > 0 BEGIN DECLARE @ExtraUsed DECIMAL(18, 2);            
SET             
  @ExtraUsed = CASE WHEN @RemainingDays <= @ExtraDays THEN @RemainingDays ELSE @ExtraDays END;            
SET             
  @RemainingDays = @RemainingDays - @ExtraUsed;            
SET             
  @ExtraDays = @ExtraDays - @ExtraUsed;            
SET             
  @ExtraDaysUsed = @ExtraDaysUsed + @ExtraUsed;            
-- track used                   
END IF @RemainingDays > 0             
AND @CompOffBalance > 0 BEGIN DECLARE @AdjustedCompOffBalance DECIMAL(5, 2);            
SET             
  @AdjustedCompOffBalance = FLOOR(@CompOffBalance * 2) / 2.0;            
SET             
  @CompOffUsed = CASE WHEN @RemainingDays <= @AdjustedCompOffBalance THEN @RemainingDays ELSE @AdjustedCompOffBalance END;            
SET             
  @RemainingDays = @RemainingDays - @CompOffUsed;            
END -- Deduct from CasualLeaveBalance                                                                      
IF @RemainingDays > 0             
AND @CasualLeaveBalance > 0 BEGIN DECLARE @AdjustedCasualLeaveBalance DECIMAL(18, 2);            
SET             
  @AdjustedCasualLeaveBalance = FLOOR(@CasualLeaveBalance * 2) / 2.0;            
SET             
  @CasualLeaveUsed = CASE WHEN @RemainingDays <= @AdjustedCasualLeaveBalance THEN @RemainingDays ELSE @AdjustedCasualLeaveBalance END;            
SET             
  @RemainingDays = @RemainingDays - @CasualLeaveUsed;            
END -- Deduct from EarnedLeaveBalance                                                                      
IF @RemainingDays > 0             
AND @EarnedLeaveBalance > 0             
and @IsForFNF = 0 BEGIN DECLARE @AdjustedEarnedLeaveBalance DECIMAL(18, 2);            
SET             
  @AdjustedEarnedLeaveBalance = FLOOR(@EarnedLeaveBalance * 2) / 2.0;            
SET             
  @EarnedLeaveUsed = CASE WHEN @RemainingDays <= @AdjustedEarnedLeaveBalance THEN @RemainingDays ELSE @AdjustedEarnedLeaveBalance END;            
SET             
  @RemainingDays = @RemainingDays - @EarnedLeaveUsed;            
END -- Calculate LeaveAdjust (total leaves deducted)                                                                      
SET             
  @LeaveAdjust = @CompOffUsed + @EarnedLeaveUsed + @CasualLeaveUsed;            
END end ELSE BEGIN             
SET             
  @CompOffUsed = 0;            
SET             
  @CasualLeaveUsed = 0;            
SET             
  @EarnedLeaveUsed = 0;            
SET             
  @LeaveAdjust = 0;            
-- SET @LeaveAdjust = 0;                                                                      
--SET @AbsentDays = 0;                                                                      
END             
SET             
  @EarnedLeaveAccrued = (            
    case when (            
      (            
        (            
          @Attendance + @LeaveAdjust + @WeeklyOff            
        ) / @BudgetMonthDays            
      ) * 1.25            
    )> 1.25 then 1.25 else (            
      (            
        (            
          @Attendance + @LeaveAdjust + @WeeklyOff            
        ) / @BudgetMonthDays            
      ) * 1.25            
    ) end            
  );            
SET             
  @CasualLeaveAccrued = (            
    case when (            
      (            
        (            
          @Attendance + @LeaveAdjust + @WeeklyOff            
        ) / @BudgetMonthDays            
      ) * 0.58            
    )> 0.58 then 0.58 else (            
      (            
        (            
          @Attendance + @LeaveAdjust + @WeeklyOff            
        ) / @BudgetMonthDays            
      ) * 0.58            
    ) end            
  );            
-- Remaining days are absent                                                                      
--SET @AbsentDays = @RemainingDays;                                                                      
-- Update tblEmployeeLeaveBalance with debited leaves                                                              
UPDATE             
  tblEmployeeLeaveBalance             
SET             
  CompOffBalance = CompOffBalance - @CompOffUsed,             
  CompOffUsed = CompOffUsed + @CompOffUsed,             
  CasualLeaveBalance =(            
    CasualLeaveBalance - @CasualLeaveUsed            
  )+ @CasualLeaveAccrued,             
  CasualLeaveUsed = CasualLeaveUsed + @CasualLeaveUsed,             
  EarnedLeaveBalance = (            
    EarnedLeaveBalance - @EarnedLeaveUsed            
  )+ @EarnedLeaveAccrued,             
  EarnedLeaveUsed = EarnedLeaveUsed + @EarnedLeaveUsed,             
  EarnedLeaveAcquired = @EarnedLeaveAccrued,             
  CasualLeaveAcquired = @CasualLeaveAccrued,             
  LastUpdatedOn = GETDATE(),             
  used = used +(            
    @CompOffUsed + @CasualLeaveUsed + @EarnedLeaveUsed            
  )             
WHERE             
  EmployeeLeaveBalanceId = @EmployeeLeaveBalanceId;            
-- Calculate payable days and payroll                                                                      
SET             
  @PayableDays = @Attendance + @WeeklyOff + @LeaveAdjust;            
  SET @PayableDays =      CASE          WHEN @PayableDays > @tBudgetMonthDays THEN @tBudgetMonthDays     WHEN @PayableDays > @tBudgetMonthDays THEN @tBudgetMonthDays         WHEN @PayableDays < 0 THEN 0         ELSE @PayableDays     END;          
          
--if(@PayableDays>25)                                            
--begin                                            
--set @PayableDays =25                                            
--set @ExtraDays+=(@PayableDays-25)                                            
--end                                            
SET             
  @Payroll = (@Salary / @BudgetMonthDays) * @PayableDays;            
Select             
  @UsedLeaves = Used             
from             
  tblEmployeeLeaveBalance             
where             
  ECODE = @Ecode             
  and MONTH = @Month end DECLARE @PFValue DECIMAL(18, 2);            
DECLARE @ESICValue DECIMAL(18, 2);            
--  -- Call PF procedure (example)                                                                      
--EXEC dbo.usp_UpsertEmpPFData @Ecode = @Ecode, @Month = @MOnth, @Year = @Year;                                                                      
-- with tablea as(                                            
--SELECT  [EmployeeAttendancePayrollId]                                                                        
--      ,[Month]                                                                        
--   ,YEAR                                                                      
--      ,[LocationCategoryId]                                                     
--      ,[DesignationId]                                                        --      ,[EmployeeName]                              
--      ,[Ecode]                                                     
--      ,[EmployeeId],                                                                        
--   Attendance,                                            
--   extradays 'weeklyoffpresent',                                                                        
--   weeklyoff 'Actual_Weekly_Off',                                                             
--   (select top 1 Used from tblEmployeeLeaveBalance b where b.EmployeeId=a.EmployeeId) 'leave_availed',                                                                        
--      (Attendance-extradays+(select top 1 Used from tblEmployeeLeaveBalance b where b.EmployeeId=a.EmployeeId)+weeklyoff) 'InitialPaybledays'  ,                                                                      
--   DAY(EOMONTH(GETDATE())) AS TotalDaysInMonth,                                                             
--   (CASE         --        WHEN (Attendance-extradays+(select top 1 Used from tblEmployeeLeaveBalance b where b.EmployeeId=a.EmployeeId)+weeklyoff) < (DAY(EOMONTH(GETDATE()))) THEN (Attendance-extradays+(select top 1 Used from tblEmployeeLeaveBalance b
  
     
     
         
         
             
--here b.EmployeeId=a.EmployeeId)+weeklyoff)                                                                       
--     ELSE (DAY(EOMONTH(GETDATE())))                                                                       
--    END) AS 'Payble_Days'                                                                      
--  FROM [HRMS].[dbo].[tblEmployeeAttendancePayrollCalculation] a                                                                       
-- ),                                                                      
-- finalInitPaybleDays as (                                                                   
-- select *,(weeklyoffpresent+(CASE                                                                       
--        WHEN (InitialPaybledays-Payble_Days) > 0 THEN (InitialPaybledays-Payble_Days)                                                                       
--        ELSE 0                                                                       
--    END)) 'EXTRA_DAYS' from tablea)                                                                      
-- MERGE dbo.tbl_calculatePaybledays AS Target                  
--USING (                                                                      
--    Select                                                                       
-- Month,Year,[LocationCategoryId],[DesignationId],[EmployeeName],[Ecode],[EmployeeId],Attendance,weeklyoffpresent,Actual_Weekly_Off,leave_availed,InitialPaybledays,TotalDaysInMonth                                                                      
-- ,Payble_Days,EXTRA_DAYS                                                                      
-- from finalInitPaybleDays                                                                      
--) AS Source                                                                      
--ON Target.Ecode = Source.Ecode AND Target.[Month] = Source.[Month] AND Target.[Year] = Source.[Year]                                                                      
--WHEN MATCHED THEN                                    
--    UPDATE SET                                                                       
--        Target.LocationCategoryId = Source.LocationCategoryId,                                                                      
--        Target.DesignationId = Source.DesignationId,                                                                      
--        Target.EmployeeName = Source.EmployeeName,                                                                      
--        Target.EmployeeId = Source.EmployeeId,                                                                      
--        Target.Attendance = Source.Attendance,                                                                      
--        Target.weeklyoffpresent = Source.weeklyoffpresent,                                                                      
--        Target.Actual_Weekly_Off = Source.Actual_Weekly_Off,                                                                      
--  Target.leave_availed = Source.leave_availed,                                                  
--        Target.InitialPaybledays = Source.InitialPaybledays,                                                                      
--        Target.TotalDaysInMonth = Source.TotalDaysInMonth,                                                                      
--        Target.Payble_Days = Source.Payble_Days,                                                                      
--        Target.EXTRA_DAYS = Source.EXTRA_DAYS                                                                      
--WHEN NOT MATCHED THEN                                                                  
Print('Attendance') Print(@Attendance) Print(@ExtraDays) Print(@WeeklyOff) Print(@BudgetMonthDays) --------------------------------------------------------                                                      
-- 1. LEAVE USED                                                      
DECLARE @UsedLeave DECIMAL(18, 2) = (            
  SELECT             
    TOP 1 ISNULL(Used, 0)             
  FROM             
    tblEmployeeLeaveBalance (NOLOCK)             
  WHERE             
    EmployeeId = @EmployeeId             
    AND MONTH = @Month            
);            
PRINT('@UsedLeave') Print(@UsedLeave) --------------------------------------------------------                                                      
-- 2. INITIAL LWP CALCULATION                                         
DECLARE @LWP DECIMAL(18, 2) = @AbsentDays - @UsedLeave;            
PRINT('@LWP') Print(@LWP) --summary                                                          
--of extra days                                                          
--leave_used = SELECT ISNULL((SELECT TOP 1 Used                                                           
--                            FROM tblEmployeeLeaveBalance b                                                           
--                            WHERE b.EmployeeId = @EmployeeId AND MONTH = @Month), 0)            
--total_attendance = @Attendance - @ExtraDays + leave_used + @WeeklyOff                                                          
---- Compare with budgeted month days                                                          
--IF total_attendance < @BudgetMonthDays THEN                                                          
--    return total_attendance                                                          
--ELSE                                                          
--    return @BudgetMonthDays                                                          
--extra days                                                              
--case when @IsExtraDaysApplicable=0                                                              
--then 0                                                              
--else                                                              
-- 3. EXTRA DAYS CALCULATION (AS BEFORE)                                                      
DECLARE @BaseValue DECIMAL(18, 2) = @Attendance - @ExtraDays + @UsedLeave + @WeeklyOff;            
PRINT('@BaseValue') Print(@BaseValue) DECLARE @CappedValue DECIMAL(18, 2) = --CASE WHEN @BaseValue < 25                                                      
--         THEN @BaseValue                                                      
--         ELSE 25                          
--    END;                                             
CASE WHEN @BaseValue < @tBudgetMonthDays THEN @BaseValue ELSE @tBudgetMonthDays END;            
PRINT('@CappedValue') Print(@CappedValue) DECLARE @ExtraDaysFinal DECIMAL(18, 2) = ISNULL(            
  @ExtraDays + CASE WHEN (@BaseValue - @CappedValue) > 0 THEN (@BaseValue - @CappedValue) ELSE 0 END,             
  0            
);            
PRINT('@ExtraDaysFinal') Print(@ExtraDaysFinal) DECLARE @PayableDaysFinal DECIMAL(18, 2) = (            
  --CASE              
  --       WHEN ISNULL(@BaseValue,0) < 25                                                       
  --        THEN ISNULL(@BaseValue,0)                                                      
  --       ELSE 25                                                      
  --   END                                              
  CASE WHEN ISNULL(@BaseValue, 0) < @tBudgetMonthDays THEN ISNULL(@BaseValue, 0) ELSE @tBudgetMonthDays END            
);            
PRINT('@PayableDaysFinal') Print(@PayableDaysFinal) Declare @AdjustedDays decimal(18, 2)= 0;            
PRINT('@AdjustedDays') Print(@AdjustedDays) --------------------------------------------------------                                                      
-- 4. LWP ↔ EXTRA DAYS ADJUSTMENT                                                      
--    (APPLICABLE ONLY WHEN LOCATION ≠ 'HO')                                                      
Declare @LocationType nvarchar(50);            
Select             
  @LocationType = c.LocationTypeName             
from             
  tblEmployee (NOLOCK) a             
  Left Join tblLocation (NOLOCK) b on a.LocationId = b.LocationId             
  Left Join tblLocationType (NOLOCK) c on b.LocationType = c.Id             
where             
  Ecode = @Ecode IF (@LocationType <> 'HO') BEGIN IF (@ExtraDaysFinal > @LWP) BEGIN -- ExtraDays > LWP → reduce ExtraDays, make LWP = 0                                                      
Set             
  @AdjustedDays =(@AbsentDays - @UsedLeave);            
SET             
  @PayableDaysFinal = CASE WHEN ISNULL((@PayableDaysFinal + (@AbsentDays - @UsedLeave)), 0) < @tBudgetMonthDays THEN ISNULL((@PayableDaysFinal + (@AbsentDays - @UsedLeave)), 0) ELSE @tBudgetMonthDays END    ;        
SET             
  @ExtraDaysFinal = @ExtraDaysFinal - @LWP;            
SET             
  @LWP = 0;            
Print('Not HO, LWP less') END ELSE BEGIN -- LWP > ExtraDays → reduce LWP, ExtraDays = 0             
Set             
  @AdjustedDays = @ExtraDaysFinal;            
SET             
  @PayableDaysFinal = @PayableDaysFinal ;--+ @ExtraDaysFinal;            
SET             
  @LWP = @LWP - @ExtraDaysFinal;            
SET             
  @ExtraDaysFinal = 0;            
Print('Not HO, Extra days less') END END ELSE BEGIN ----------------------------------------------------                                                      
-- 5. HO CASE                                                      
-- LWP = Absent - used, ExtraDays remains same                                                      
SET             
  @LWP = @AbsentDays - @UsedLeave;            
Print('HO') PRINT(@PayableDaysFinal) -- @ExtraDaysFinal stays unchanged                                                      
END;            
--end                                                        
PRINT('Changed') PRINT('@AdjustedDays') Print(@AdjustedDays) PRINT('@PayableDaysFinal') Print(@PayableDaysFinal) PRINT('@LWP') Print(@LWP) PRINT('@ExtraDaysFinal') Print(@ExtraDaysFinal) INSERT into tbl_calculatePaybledays (            
  [Month], [Year], LocationCategoryId,             
  DesignationId, EmployeeName, Ecode,             
  EmployeeId, Attendance, weeklyoffpresent,             
  Actual_Weekly_Off, leave_availed,             
  InitialPaybledays, TotalDaysInMonth,             
  Payble_Days, EXTRA_DAYS, ExtraDaysUsed,             
  Absent, Status, WeekHolidays, SatHolidays,             
  NC_Attendance, LWP, AdjustedDays            
)             
Values             
  (            
    @Month,             
    @Year,             
    1,             
    @DesignationId,             
    @EmployeeName,             
    @Ecode,             
    @EmployeeId,             
    @Attendance - ISNULL(@WeekdaysHolidayCount, 0) - ISNULL(@FinalSatHolidayCount, 0),             
    -- substracting beacuse i have added both above  to add efct of these also                                                          
    --weeklyoff                                                              
    isnull(@ExtraDays, 0),             
    --actualweeklyoff                                                         
    isnull(@WeeklyOff, 0),             
    CASE WHEN @IsNAPS = 1 THEN 0 ELSE (            
      SELECT             
        ISNULL(            
          (            
            SELECT             
              TOP 1 Used             
            FROM             
              tblEmployeeLeaveBalance b             
            WHERE             
              b.EmployeeId = @EmployeeId             
              and MONTH = @Month            
          ),             
          0            
        )            
    ) END,             
    --leaveavailed                                                              
    --init payble days                                                              
    isnull(@BaseValue, 0),             
    --total days in month                                                              
    @BudgetMonthDays,             
    --payble days                                                              
    @PayableDaysFinal,             
    @ExtraDaysFinal,             
    ISNULL(@ExtraDaysUsed, 0),             
    isnull(@AbsentDays, 0),             
    @IsActive,             
    ISNULL(@WeekdaysHolidayCount, 0),             
    ISNULL(@FinalSatHolidayCount, 0),             
    ISNULL(@NC_Attendance, 0),             
    ISNULL(@LWP, 0),             
    ISNULL(@AdjustedDays, 0)            
  ) --    Select                                                      
  --Month,Year,[LocationCategoryId],[DesignationId],[EmployeeName],[Ecode],[EmployeeId],Attendance,weeklyoffpresent,Actual_Weekly_Off,leave_availed,InitialPaybledays,TotalDaysInMonth                                                                      
 --,Payble_Days,EXTRA_DAYS                                         
  --from finalInitPaybleDays                                                                      
  --VALUES (                                                                      
  --    Source.[Month],                                                                      
  --    Source.[Year],                      --    Source.LocationCategoryId,                                                                      
  --    Source.DesignationId,                                                       
  --    Source.EmployeeName,                                                                      
  --    Source.Ecode,                                                         
  --  Source.EmployeeId,                                            
  --    Source.Attendance,                                           
  --    Source.weeklyoffpresent,                                                   
  --    Source.Actual_Weekly_Off,                                                                      
  --    Source.leave_availed,                                                                      
  --    Source.InitialPaybledays,                                                                
  --    Source.TotalDaysInMonth,                                                                      
  --    Source.Payble_Days,                                     
  --    Source.EXTRA_DAYS                                                                      
  --);                                                                      
  ;            
WITH tablea AS (            
  SELECT             
    *             
  FROM             
    tblEmployee             
  where             
    --IsActive=1                                                             
    --and                                                             
    EmployeeId = @EmployeeId            
),             
tableb AS (            
  SELECT             
    a.ecode,             
    CASE WHEN b.TotalDaysInMonth IS NULL             
    OR b.TotalDaysInMonth = 0 THEN 0 ELSE (            
      a.BasicSalary / b.TotalDaysInMonth            
    ) * b.Payble_Days END AS BasicSalary,             
    CASE WHEN b.TotalDaysInMonth IS NULL             
    OR b.TotalDaysInMonth = 0 THEN 0 ELSE (a.HRA / b.TotalDaysInMonth) * b.Payble_Days END AS HRA,             
    CASE WHEN b.TotalDaysInMonth IS NULL             
    OR b.TotalDaysInMonth = 0 THEN 0 ELSE (a.CCA / b.TotalDaysInMonth) * b.Payble_Days END AS CCA,             
    CASE WHEN b.TotalDaysInMonth IS NULL             
    OR b.TotalDaysInMonth = 0 THEN 0 ELSE (            
      a.SpecialAllowance / b.TotalDaysInMonth            
    ) * b.Payble_Days END AS SpecialAllowance,             
    CASE WHEN b.TotalDaysInMonth IS NULL             
    OR b.TotalDaysInMonth = 0 THEN 0 ELSE (a.DA / b.TotalDaysInMonth) * b.Payble_Days END AS DA,             
    CASE WHEN b.TotalDaysInMonth IS NULL             
    OR b.TotalDaysInMonth = 0 THEN 0 ELSE (            
      a.Reimbersment / b.TotalDaysInMonth            
    ) * b.Payble_Days END AS Reimbersment,             
    CASE WHEN b.TotalDaysInMonth IS NULL             
    OR b.TotalDaysInMonth = 0 THEN 0 ELSE (            
      a.Fuel_and_Maintainence / b.TotalDaysInMonth            
    ) * b.Payble_Days END AS Fuel_and_Maintainence,             
    CASE WHEN b.TotalDaysInMonth IS NULL             
    OR b.TotalDaysInMonth = 0 THEN 0 ELSE (            
      a.Books_and_Periodicals / b.TotalDaysInMonth            
    ) * b.Payble_Days END AS Books_and_Periodicals,             
    CASE WHEN b.TotalDaysInMonth IS NULL             
    OR b.TotalDaysInMonth = 0 THEN 0 ELSE (            
      [Professional Attire] / b.TotalDaysInMonth            
    ) * b.Payble_Days END AS [Professional Attire],             
    CASE WHEN b.TotalDaysInMonth IS NULL             
    OR b.TotalDaysInMonth = 0 THEN 0 ELSE (            
      [Driver Wages] / b.TotalDaysInMonth            
    ) * b.Payble_Days END AS [Driver Wages],             
    CASE WHEN b.TotalDaysInMonth IS NULL             
    OR b.TotalDaysInMonth = 0 THEN 0 ELSE (            
      [Mobile Bill] / b.TotalDaysInMonth            
    ) * b.Payble_Days END AS [Mobile Bill],             
    CASE WHEN b.TotalDaysInMonth IS NULL             
    OR b.TotalDaysInMonth = 0 THEN 0 ELSE (            
      [Meal Voucher] / b.TotalDaysInMonth            
    ) * b.Payble_Days END AS [Meal Voucher]             
  FROM             
    tablea a             
    LEFT JOIN tbl_calculatePaybledays b ON a.ecode = b.ecode             
    and b.Month = @Month --WHERE a.ecode = 'RTN106'                                                                        
    ),             
tableDeduction AS(            
  Select             
    PF,             
    ESIC,             
    ECode,             
    STCode,             
    MONTH,             
    Year             
  from             
    tblEmployeeDeductions             
  where             
    ECode = @Ecode             
    and Month = @Month --where ECode='RTN106' and STCode='RH01' and MONTH=6 and Year=2025                                                                        
    ),             
MonthlyGrossCTC AS (            
  SELECT             
    tb.ecode,             
    BasicSalary,             
    HRA,             
    CCA,             
    SpecialAllowance,             
    DA,             
    ISNULL([Reimbersment], 0) AS Reimbursement,             
    ISNULL(Fuel_and_Maintainence, 0) AS Fuel_and_Maintainence,             
    ISNULL(Books_and_Periodicals, 0) AS Books_and_Periodicals,             
    ISNULL([Professional Attire], 0) AS [Professional Attire],             
    ISNULL([Driver Wages], 0) AS [Driver Wages],             
    ISNULL([Mobile Bill], 0) AS [Mobile Bill],             
    ISNULL([Meal Voucher], 0) AS [Meal Voucher],             
    ISNULL(td.PF, 0) [PF],             
    ISNULL(td.ESIC, 0) [ESIC],             
    --(BasicSalary + HRA + CCA + SpecialAllowance + DA-(ISNULL(td.PF,0)+ISNULL(td.ESIC,0))) AS MonthlyGrossCTC,                                                                        
    (            
      IsNULL(BasicSalary, 0) + IsNULL(HRA, 0) + ISNULL(CCA, 0) + ISNULL(SpecialAllowance, 0) + ISNULL(DA, 0) + ISNULL([Reimbersment], 0)            
    ) AS MonthlyGrossCTC,             
    @Month AS [Month]             
  FROM             
    tableb tb             
Left JOIn tableDeduction td on tb.ecode = td.ECode            
),             
ExtraDayAllowance AS (            
  SELECT             
    a.ecode,             
    Case when @IsExtraDaysApplicable = 0 then 0 else CASE WHEN b.TotalDaysInMonth IS NULL             
    OR b.TotalDaysInMonth = 0 THEN 0 ELSE CAST(            
      (            
        (            
          isnull(a.BasicSalary, 0) + isnull(a.HRA, 0) + isnull(a.CCA, 0) + isnull(a.SpecialAllowance, 0) + isnull(a.DA + a.Reimbersment, 0)            
        ) / b.TotalDaysInMonth            
      ) * isnull(b.Extra_Days, 0) AS decimal(10, 2)            
    ) END end AS Extra_Day_Allowance,             
    '' AS Incentive,             
    '' AS Arrears             
  FROM             
    tablea a             
    LEFT JOIN tbl_calculatePaybledays b ON a.ecode = b.ecode             
    and b.Month = @Month -- WHERE a.ecode = 'RTN106'                                                                     
 ),             
finalMonthSalary as(            
  SELECT             
    a.*,             
    b.Extra_Day_Allowance,             
    b.Incentive,             
    b.Arrears             
  FROM             
    MonthlyGrossCTC a             
    LEFT JOIN ExtraDayAllowance b ON a.ecode = b.ecode            
) MERGE dbo.tbl_Month_salary AS Target USING finalMonthSalary AS Source ON Target.ecode = Source.ecode             
AND Target.Month = Source.Month WHEN MATCHED THEN             
UPDATE             
SET             
  Target.BasicSalary = Source.BasicSalary,             
  Target.HRA = Source.HRA,             
  Target.CCA = Source.CCA,             
  Target.SpecialAllowance = Source.SpecialAllowance,             
  Target.DA = Source.DA,             
  Target.Reimbersment = Source.Reimbursement,             
  Target.Fuel_and_Maintainence = Source.Fuel_and_Maintainence,             
  Target.Books_and_Periodicals = Source.Books_and_Periodicals,             
  Target.[Professional Attire] = Source.[Professional Attire],             
  Target.[Driver Wages] = Source.[Driver Wages],             
  Target.[Mobile Bill] = Source.[Mobile Bill],             
  Target.[Meal Voucher] = Source.[Meal Voucher],             
  Target.monthlyGrossCTC = Source.MonthlyGrossCTC,             
  Target.Extra_day_allowence = Source.Extra_Day_Allowance,             
  Target.Incentive = Source.Incentive,             
  Target.Arrers = Source.Arrears,             
  Target.PF = Source.PF,             
  Target.ESIC = Source.ESIC WHEN NOT MATCHED THEN INSERT (            
    ecode, BasicSalary, HRA, CCA, SpecialAllowance,             
   DA, Reimbersment, Fuel_and_Maintainence,             
    [Books_and_Periodicals], [Professional Attire],             
    [Driver Wages], [Mobile Bill], [Meal Voucher],             
    monthlyGrossCTC, MONTH, Extra_day_allowence,             
    Incentive, Arrers, PF, ESIC            
  )             
VALUES             
  (            
    Source.ecode, Source.BasicSalary,             
    Source.HRA, Source.CCA, Source.SpecialAllowance,             
    Source.DA, Source.Reimbursement,             
    Source.Fuel_and_Maintainence, Source.[Books_and_Periodicals],             
    Source.[Professional Attire], Source.[Driver Wages],             
    Source.[Mobile Bill], Source.[Meal Voucher],             
    Source.MonthlyGrossCTC, Source.Month,             
    Source.Extra_Day_Allowance, Source.Incentive,             
    Source.Arrears, Source.PF, Source.ESIC            
  );            
Declare @MonthGrossSalaryEmp decimal(18, 2);            
Select             
  @MonthGrossSalaryEmp = try_cast(            
    monthlyGrossCTC as decimal(18, 2)            
  )             
from             
  tbl_Month_salary             
where             
  ecode = @Ecode             
  and MONTH = @Month Print(@MonthGrossSalaryEmp)             
SELECT             
  @Tds = TDS,             
  @Loan = Loan,             
  @CashShort = CashShort,             
  @DieselDeduction = DieselDeduction,             
  @Penality = Penality             
FROM             
  EmpTDSTable             
WHERE             
  E_CODE = @Ecode             
  AND MTH = @Month;            
SELECT             
  @INCENTIVE = [Incentive],            
  @ARREAR = [ARREAR],             
  @OVERTIME = [Overtime],             
  @FOODINGALLOWANCE = [Fooding_Allowance],             
  @MOBILEBILL = [Mobile_Bill]             
FROM             
  [HRMS].[dbo].[tblPayments]             
WHERE             
E_CODE = @Ecode             
  AND MONTH = @Month;            
IF (@IsNAPS = 0) BEGIN      

SELECT TOP 1 @PTax = FinalPtRate 
FROM vw_PTPolicyMaster a
WHERE a.State = @State   
  AND a.SlabMin <= @MonthGrossSalaryEmp   
  AND a.SlabMax >= @MonthGrossSalaryEmp
  AND (
        -- Maharashtra Female: allow Female-specific slabs
        (@State = 'Maharashtra' AND @Gender = 'Female' AND a.Gender = 'Female')
        -- Everyone else: only generic (non-Female) slabs
     OR (a.Gender IS NULL OR a.Gender = '')
  );   
   PRINT('PTAX')
   Print(@PTax)             
SELECT             
  @Lwf = MAX(            
    CASE WHEN State = 'Haryana' THEN -- Percentage calculation for Haryana with Max check                                                          
    CASE WHEN EmployeeMax IS NULL THEN (            
      (            
        ISNULL(Employee, 0) / 100.0            
      ) * ISNULL(@MonthGrossSalaryEmp, 0)            
    ) WHEN (            
      (            
        ISNULL(Employee, 0) / 100.0            
      ) * ISNULL(@MonthGrossSalaryEmp, 0)            
    ) > EmployeeMax THEN EmployeeMax ELSE (            
      (            
        ISNULL(Employee, 0) / 100.0            
      ) * ISNULL(@MonthGrossSalaryEmp, 0)            
 ) END / CASE WHEN Frequency = 'Monthly' THEN 1 WHEN Frequency = 'Half-yearly' THEN 6 WHEN Frequency = 'Yearly' THEN 12 ELSE 1 END ELSE -- Direct value for all other states                                                          
    CASE WHEN EmployeeMax IS NULL THEN ISNULL(Employee, 0) WHEN ISNULL(Employee, 0) > EmployeeMax THEN EmployeeMax ELSE ISNULL(Employee, 0) END / CASE WHEN Frequency = 'Monthly' THEN 1 WHEN Frequency = 'Half-yearly' THEN 6 WHEN Frequency = 'Yearly' THEN  
 
    
      
        
          
12 ELSE 1 END END            
  ),             
  @LwfEmployeer = MAX(            
    CASE WHEN State = 'Haryana' THEN -- Percentage calculation for Haryana with Max check                                                          
    CASE WHEN EmployeerMax IS NULL THEN (            
      (            
        ISNULL(Employeer, 0) / 100.0            
      ) * ISNULL(@MonthGrossSalaryEmp, 0)            
    ) WHEN (            
      (            
        ISNULL(Employeer, 0) / 100.0            
      ) * ISNULL(@MonthGrossSalaryEmp, 0)            
    ) > EmployeerMax THEN EmployeerMax ELSE (            
      (            
        ISNULL(Employeer, 0) / 100.0            
      ) * ISNULL(@MonthGrossSalaryEmp, 0)            
    ) END / CASE WHEN Frequency = 'Monthly' THEN 1 WHEN Frequency = 'Half-yearly' THEN 6 WHEN Frequency = 'Yearly' THEN 12 ELSE 1 END ELSE -- Direct value for all other states                                                      
    CASE WHEN EmployeerMax IS NULL THEN ISNULL(Employeer, 0) WHEN ISNULL(Employeer, 0) > EmployeerMax THEN EmployeerMax ELSE ISNULL(Employeer, 0) END / CASE WHEN Frequency = 'Monthly' THEN 1 WHEN Frequency = 'Half-yearly' THEN 6 WHEN Frequency = 'Yearly' 
  
    
      
        
          
THEN 12 ELSE 1 END END            
  )             
FROM             
  LWFPolicyMaster             
WHERE             
  State = @State;            
PRINT('LWF') PRINT(@Lwf) PRINT('LwfEmployeer') PRINT(@LwfEmployeer) -- SELECT                                                           
--    @Lwf = MAX(                                                          
--        CASE                                                           
--            WHEN Employee IS NOT NULL THEN                                                          
--                CASE                                                           
--                    WHEN ISNULL(Employee,0) > ISNULL(EmployeeMax, 0)                                                           
--                        THEN ISNULL(EmployeeMax,0)                                                          
--                        ELSE ISNULL(Employee, 0)                                                          
--                END                                                           
--           / CASE                      
-- WHEN Frequency = 'Monthly' THEN 1                                                           
--     WHEN Frequency = 'Half-yearly' THEN 6                                                           
--                    WHEN Frequency = 'Yearly' THEN 12                                                           
--                    ELSE 1 -- default                                                          
--                  END                                                          
--            ELSE ISNULL(EmployeeMax, 0)             
--        END                                              
--    ),                                                          
--    @LwfEmployeer = MAX(                                                          
--        CASE                                          
--            WHEN Employeer IS NOT NULL THEN                                                          
--                CASE                                                           
--                    WHEN ISNULL(Employeer,0) > ISNULL(EmployeerMax, 0)                                                           
--                   THEN ISNULL(EmployeerMax,0)                                                          
--                        ELSE ISNULL(Employeer, 0)                                                          
--        END                                                           
--                / CASE                                                           
--                    WHEN Frequency = 'Monthly' THEN 1                                                     --                    WHEN Frequency = 'Half-yearly' THEN 6                                                           
--                    WHEN Frequency = 'Yearly' THEN 12                                                           
--                    ELSE 1 -- default                                                      
--                  END                                                          
--            ELSE ISNULL(EmployeerMax, 0)                                                           
--        END                                                    
--    )                                                          
--FROM LWFPolicyMaster                                  --WHERE State = @State;                                                          
--Print(@Lwf +' '+@LwfEmployeer)                                                          
EXEC dbo.usp_UpsertEmpPFData @Ecode = @Ecode,             
@Month = @Month,             
@Year = @Year,             
@PF = @PFValue OUTPUT -- pass OUTPUT                                                                      
EXEC dbo.usp_UpsertEmpESICData @Ecode = @Ecode,             
@Month = @Month,             
@Year = @Year,             
@ESIC = @ESICValue OUTPUT             
Select             
  --@PTax=PTax,@Lwf=Lwf,                                                          
  @Tds = TDS,             
  @Loan = Loan,             
  @CashShort = CashShort,             
  @DieselDeduction = DieselDeduction,             
  @Penality = Penality             
from             
  EmpTDSTable             
where             
  E_CODE = @Ecode             
  and MTH = @Month;            
Select             
  @INCENTIVE = [Incentive],             
  @ARREAR = [ARREAR],             
  @OVERTIME = [Overtime],             
  @FOODINGALLOWANCE = [Fooding_Allowance],             
  @MOBILEBILL = [Mobile_Bill]             
FROM             
  [HRMS].[dbo].[tblPayments]             
where             
  E_CODE = @Ecode             
  and MONTH = @Month             
UPDATE             
  tblEmployeeDeductions             
SET             
  TDS = ISNULL(@Tds, 0),             
  PTax = ISNULL(@PTax, 0),             
  Loan = ISNULL(@Loan, 0),             
  CashShort = ISNULL(@CashShort, 0),             
  DieselDeduction = ISNULL(@DieselDeduction, 0),             
  Penality = ISNULL(@Penality, 0),             
  Lwf = ISNULL(@Lwf, 0),             
  LwfEmployeer = ISNULL(@LwfEmployeer, 0),             
  TotalDeductions = ISNULL(@ESICValue, 0)+ ISNULL(@PFValue, 0)+ ISNULL(@Tds, 0) + ISNULL(@PTax, 0) + ISNULL(@Loan, 0) + ISNULL(@CashShort, 0) + ISNULL(@DieselDeduction, 0) + ISNULL(@Penality, 0) + ISNULL(@Lwf, 0)             
WHERE             
  ECode = @Ecode             
  AND [MONTH] = @Month;            
UPDATE             
  dbo.tbl_Month_salary             
SET             
  [monthlyGrossCTC(afterDeduction)] = monthlyGrossCTC - try_cast(            
    IsNuLL(@PFValue, 0) as decimal            
  ) - try_cast(            
    ISNULL(@ESICValue, 0) as decimal            
  ) - try_cast(            
    ISNULL(@Tds, 0) as decimal            
  )- try_cast(            
    ISNULL(@PTax, 0) as decimal            
  )- try_cast(            
    ISNULL(@Loan, 0) as decimal            
  ) - try_cast(            
    ISNULL(@CashShort, 0) as decimal            
  )- try_cast(            
    ISNULL(@DieselDeduction, 0) as decimal            
  )- try_cast(            
    ISNULL(@Penality, 0) as decimal            
  ) - try_cast(            
    ISNULL(@Lwf, 0) as decimal            
  ) + try_cast(            
    ISNULL(@INCENTIVE, 0) as decimal            
  ) + try_cast(            
    ISNULL(@ARREAR, 0) as decimal            
) + try_cast(            
    ISNULL(@OVERTIME, 0) as decimal            
  )+ try_cast(            
    ISNULL(@FOODINGALLOWANCE, 0) as decimal            
  )+ try_cast(            
    ISNULL(@MOBILEBILL, 0) as decimal            
  ) + ISNULL(Extra_day_allowence, 0),             
  PF = @PFValue,             
  ESIC = @ESICValue,             
  TDS = @Tds,             
  PTax = @PTax,             
  Loan = @Loan,             
  CashShort = @CashShort,             
  DieselDeduction = @DieselDeduction,             
  Penality = @Penality,             
  Lwf = @Lwf,             
  Incentive = @INCENTIVE,             
  Arrers = @ARREAR,             
  [Overtime] = @OVERTIME,             
  [Fooding_Allowance] = @FOODINGALLOWANCE,             
  [Mobile_Bill] = @MOBILEBILL             
WHERE             
  ecode = @Ecode             
  AND [Month] = @Month;            
END ELSE BEGIN -- NAPS: ONLY CashShort deduction, everything else zero      -- Keep CashShort as fetched (default to 0 if null)                 
SET @CashShort = ISNULL(@CashShort, 0);                 
-- Force all other deductions to 0                
SET @PFValue = 0;              
SET @ESICValue =0;            
SET             
  @PTax = 0;            
SET             
  @Lwf = 0;            
SET             
  @LwfEmployeer = 0;            
SET             
  @Tds = 0;            
SET             
  @Loan = 0;            
SET             
  @DieselDeduction = 0;            
SET             
  @Penality = 0;            
exec [dbo].[sp_MergeEmployeeDeduction] @ecode,             
@Month,             
@Year,             
@Tds,             
@PTax,             
@Loan,             
@CashShort,             
@DieselDeduction,             
@Penality,             
@Lwf --@ECODE NVARCHAR(50),     --@MONTH NVARCHAR(10),     --@YEAR INT,     --@TDS DECIMAL(18,2) = NULL,     --@PTax DECIMAL(18,2) = NULL,     --@Loan DECIMAL(18,2) = NULL,     --@CashShort DECIMAL(18,2) = NULL,     --@DieselDeduction DECIMAL(18, 2) = NULL,             
--@Penality DECIMAL(18,2) = NULL,     --@Lwf DECIMAL(18,2) = NULL     ------------------------------------------------------------     -- Update tblEmployeeDeductions (only CashShort counts)     --------------------------------            
UPDATE tblEmployeeDeductions     SET         PF = 0,         ESIC = 0,         TDS = 0,         PTax = 0,         Loan = 0,         CashShort = @CashShort,             
DieselDeduction = 0,             
Penality = 0,             
Lwf = 0,             
LwfEmployeer = 0,             
TotalDeductions = @CashShort             
WHERE             
  ECode = @Ecode             
  AND [MONTH] = @Month;            
UPDATE dbo.tbl_Month_salary     SET         PF = 0,         ESIC = 0,         TDS = 0,                     
PTax = 0,             
Loan = 0,             
CashShort = @CashShort,             
DieselDeduction = 0,             
Penality = 0,             
Lwf = 0,             
Incentive = ISNULL(@INCENTIVE, 0),             
Arrers = ISNULL(@ARREAR, 0),             
[Overtime] = ISNULL(@OVERTIME, 0),             
[Fooding_Allowance] = ISNULL(@FOODINGALLOWANCE, 0),             
[Mobile_Bill] = ISNULL(@MOBILEBILL, 0),             
[monthlyGrossCTC(afterDeduction)] = monthlyGrossCTC - ISNULL(@CashShort, 0) + ISNULL(Extra_day_allowence, 0) + ISNULL(            
  TRY_CAST(            
    @INCENTIVE as decimal(18, 2)            
  ),             
  0            
) + ISNULL(            
  TRY_CAST(            
    @ARREAR as decimal(18, 2)            
  ),             
  0            
) + ISNULL(            
  TRY_CAST(            
    @OVERTIME as decimal(18, 2)         
  ),             
  0            
) + ISNULL(            
  TRY_CAST(            
    @FOODINGALLOWANCE as decimal(18, 2)            
  ),             
  0            
) + ISNULL(            
  TRY_CAST(            
    @MOBILEBILL as decimal(18, 2)            
  ),             
  0            
)             
WHERE             
  ecode = @Ecode             
  AND [Month] = @Month;            
END --ELSE                                  
--BEGIN                                  
--    -- NAPS: no deductions at all                                
--  SET @PFValue = 0;  SET @ESICValue = 0;                                  
--    SET @PTax = 0;     SET @Lwf = 0;  SET @LwfEmployeer = 0;                                  
--    SET @Tds = 0;      SET @Loan = 0; SET @CashShort = 0;                                  
--    SET @DieselDeduction = 0; SET @Penality = 0;                                  
--    -- set net = gross (+ additions you already allow)                                  
--    UPDATE dbo.tbl_Month_salary                               
--    SET                                  
--      PF = 0, ESIC = 0, TDS = 0, PTax = 0, Loan = 0, CashShort = 0,                                  
--      DieselDeduction = 0, Penality = 0, Lwf = 0,                                  
--      [monthlyGrossCTC(afterDeduction)] =                 
--            monthlyGrossCTC                                  
--          + ISNULL(Extra_day_allowence,0)                                  
--          + ISNULL(TRY_CAST(@INCENTIVE as decimal(18,2)),0)                                  
--          + ISNULL(TRY_CAST(@ARREAR as decimal(18,2)),0)               
--          + ISNULL(TRY_CAST(@OVERTIME as decimal(18,2)),0)                                  
--          + ISNULL(TRY_CAST(@FOODINGALLOWANCE as decimal(18,2)),0)                                  
--          + ISNULL(TRY_CAST(@MOBILEBILL as decimal(18,2)),0)                                  
--    WHERE ecode = @Ecode AND [Month] = @Month;                                  
--END                                  
PRINT('@PFValue') PRINT(@PFValue) PRINT('@ESICValue') PRINT(@ESICValue)             
Select             
  @BasicSalary = ISNULL(            
    try_cast(            
      [BasicSalary(Bud.)] as decimal            
    ),             
    0            
  ),             
  @BasicSalaryCalc = ISNULL(            
    try_cast(            
      [BasicSalary(Actual)] as decimal            
    ),             
    0            
  ),             
  @GrossEarnings = ISNULL(            
    try_cast(            
      [Monthly Gross CTC(Actual)] as decimal            
    ),             
    0            
  )             
from             
  vw_Emp_Attendance_Format (NOLOCK)             
where             
  Ecode = @Ecode             
  and [Month-Year] = @Month;            
Declare @MonthGratuity decimal(18, 2)= dbo.fn_CalculateGratuity(@DOJ, @DOL, @Month, @BasicSalary);            
MERGE BonusAndGratutityOpening AS Target USING (            
  SELECT             
    @ECode AS ECode,             
    @Month AS Month            
) AS Source ON Target.ECode = Source.ECode             
AND Target.Month = Source.Month WHEN MATCHED THEN             
UPDATE             
SET             
  ActualGratuity = - ISNULL(            
    TRY_CAST(Gratuity AS DECIMAL),             
    0            
  ) + @MonthGratuity,             
  ActualBonus = CASE WHEN @IsBonusApplicable = 'Ctc' THEN (@GrossEarnings / 12) WHEN @IsBonusApplicable = 'Stat' THEN (@BasicSalaryCalc * 0.0833) ELSE 0 END,             
  ClosingGratuity = @MonthGratuity WHEN NOT MATCHED THEN INSERT (            
    ECode, Month, Gratuity, Bonus, ActualGratuity,             
    ActualBonus, ClosingGratuity            
  )             
VALUES             
  (            
    @ECode,             
    @Month,             
    0,             
    0,             
    @MonthGratuity,             
    CASE WHEN @IsBonusApplicable = 'Ctc' THEN (@GrossEarnings / 12) WHEN @IsBonusApplicable = 'Stat' THEN (@BasicSalaryCalc * 0.0833) ELSE 0 END,             
    @MonthGratuity            
  );            
DECLARE @NextMonth VARCHAR(7);            
SET             
  @NextMonth = UPPER(            
    LEFT(            
      FORMAT(            
        DATEADD(            
          MONTH,             
          1,             
          TRY_CAST('01-' + @Month AS DATE)            
        ),             
        'MMM'            
      ),             
      1            
    )            
  ) + LOWER(            
    SUBSTRING(            
      FORMAT(            
        DATEADD(            
          MONTH,             
          1,             
          TRY_CAST('01-' + @Month AS DATE)            
        ),             
        'MMM'            
      ),             
      2,             
      2            
    )            
  ) + '-' + RIGHT(            
    FORMAT(            
      DATEADD(            
        MONTH,             
        1,             
        TRY_CAST('01-' + @Month AS DATE)            
      ),             
      'yy'            
    ),             
    2            
  );            
MERGE BonusAndGratutityOpening AS Target USING (            
  SELECT             
    @ECode AS ECode,             
    @NextMonth AS Month            
) AS Source ON Target.ECode = Source.ECode             
AND Target.Month = Source.Month WHEN MATCHED THEN             
UPDATE             
SET             
  Gratuity = @MonthGratuity,             
  Bonus = 0,             
  ActualGratuity = 0,             
  ActualBonus = 0,             
  ClosingGratuity = 0 WHEN NOT MATCHED THEN INSERT (            
    ECode, Month, Gratuity, Bonus, ActualGratuity,             
    ActualBonus, ClosingGratuity            
  )             
VALUES             
  (            
    @ECode, @NextMonth, @MonthGratuity,             
    0, 0, 0, 0            
  );            
Exec [usp_ProcessBonusAndPayments] @Month,             
@Ecode,             
'Salary Run' --EXEC dbo.prc_snapshot_vw_emp_attendance @Ecode = @Ecode, @Month = @Month;                                                          
-- Select the inserted record and leave details for verification                                                                      
--SELECT                                                                       
--    Month,                                                                      
--    LocationCategoryId,                                        
--    DesignationId,                                                                      
--    EmployeeName,                                                        
--    Ecode,                                                                      
--    EmployeeId,                                                                      
--    Attendance,                                                  
--    EmployeeLeaveBalanceId,                                                                      
--    WeeklyOff,                          --    ExtraDays,                                                                    
--    Payroll,                                                                      
--    Salary,                                                                      
--    LeaveAdjust                                                                      
--FROM tblEmployeeAttendancePayrollCalculation                                                                      
--WHERE EmployeeId = @EmployeeId AND Month = @Month;                                                                      
--SELECT                                                        
--    @EarnedLeaveAccrued AS EarnedLeaveAccrued,                                                                      
--    @CasualLeaveAccrued AS CasualLeaveAccrued,                                                                      
--    @CompOffUsed AS CompOffUsed,                                                                      
--    @EarnedLeaveUsed AS EarnedLeaveUsed,        
--    @CasualLeaveUsed AS CasualLeaveUsed,                                                                      
--    @LeaveAdjust AS LeaveAdjust,                                                                      
--    @AbsentDays AS AbsentDays,                                            
--    @PayableDays AS PayableDays,                                                                      
--    @Payroll AS PayableSalary;                                                                   
END TRY BEGIN CATCH -- Handle the error and show passed parameter values                                                                  
DECLARE @ErrorMessage NVARCHAR(4000);            
DECLARE @ErrorSeverity INT;            
DECLARE @ErrorState INT;            
SET             
  @ErrorMessage = ERROR_MESSAGE();            
SET             
  @ErrorSeverity = ERROR_SEVERITY();            
SET             
  @ErrorState = ERROR_STATE();            
PRINT 'Error occurred in sp_CalculateEmployeePayroll';            
PRINT 'Parameters:';            
PRINT 'EmployeeId: ' + CAST(@EmployeeId AS VARCHAR);            
PRINT 'Attendance: ' + CAST(@Attendance AS VARCHAR);            
PRINT 'Month: ' + @Month;            
PRINT 'Salary: ' + CAST(@Salary AS VARCHAR);            
PRINT 'ExtraDays: ' + CAST(@ExtraDays AS VARCHAR);            
PRINT 'Error Message: ' + @ErrorMessage;            
-- Optionally re-throw the error                                                                  
RAISERROR (            
  @ErrorMessage, @ErrorSeverity, @ErrorState            
);            
END CATCH END;
GO

-- -----------------------------------------------------------------------------
-- dbo.sp_GetPayrollSummary
-- -----------------------------------------------------------------------------
-- (Unchanged, included for reference)
CREATE OR ALTER PROCEDURE sp_GetPayrollSummary
    @StartDate DATE,
    @EndDate DATE,
    @PageNumber INT = 1,
    @PageSize INT = 10
AS
BEGIN
    SET NOCOUNT ON;

    -- Declare variables for totals
    DECLARE @TotalPayableSalary DECIMAL(18,2) = 0;
    DECLARE @TotalGivenToBank DECIMAL(18,2) = 0;
    DECLARE @TotalPaidByBank DECIMAL(18,2) = 0;
    DECLARE @TotalReturnByBank DECIMAL(18,2) = 0;
    DECLARE @TotalDifferencePayableMinusGiven DECIMAL(18,2) = 0;
    DECLARE @TotalDifferencePayableMinusPaid DECIMAL(18,2) = 0;
    DECLARE @TotalDifferencePayableMinusReturned DECIMAL(18,2) = 0;
    DECLARE @TotalDifferenceGivenMinusPaid DECIMAL(18,2) = 0;
    DECLARE @TotalDifferenceGivenMinusReturned DECIMAL(18,2) = 0;

    -- Main query with pagination
   WITH PayrollData AS (
    SELECT 
        l.LocationName,
        l.STCode,
        e.Ecode,
        e.MonthYear,
        ISNULL(TRY_CAST(e.Payable_Salary AS DECIMAL(18,2)), 0) AS Payable_Salary,
        ISNULL(TRY_CAST(t.BankTransfer AS DECIMAL(18,2)), 0) AS GiventoBank,
        ISNULL(TRY_CAST(p.PaidByBank AS DECIMAL(18,2)), 0) AS PaidByBank,
        ISNULL(TRY_CAST(r.ReturnByBank AS DECIMAL(18,2)), 0) AS ReturnByBank,
        (ISNULL(TRY_CAST(e.Payable_Salary AS DECIMAL(18,2)), 0) - ISNULL(TRY_CAST(t.BankTransfer AS DECIMAL(18,2)), 0)) AS DifferencePayableMinusGiven,
        (ISNULL(TRY_CAST(e.Payable_Salary AS DECIMAL(18,2)), 0) - ISNULL(TRY_CAST(p.PaidByBank AS DECIMAL(18,2)), 0)) AS DifferencePayableMinusPaid,
        (ISNULL(TRY_CAST(e.Payable_Salary AS DECIMAL(18,2)), 0) - ISNULL(TRY_CAST(r.ReturnByBank AS DECIMAL(18,2)), 0)) AS DifferencePayableMinusReturned,
        (ISNULL(TRY_CAST(t.BankTransfer AS DECIMAL(18,2)), 0) - ISNULL(TRY_CAST(p.PaidByBank AS DECIMAL(18,2)), 0)) AS DifferenceGivenMinusPaid,
        (ISNULL(TRY_CAST(t.BankTransfer AS DECIMAL(18,2)), 0) - ISNULL(TRY_CAST(r.ReturnByBank AS DECIMAL(18,2)), 0)) AS DifferenceGivenMinusReturned
    FROM EmployeePayroll e
    LEFT JOIN tblLocation l ON l.locationid = e.location
    LEFT JOIN tblBankTransfer t ON e.Ecode = t.ecode
    LEFT JOIN tblPaidByBank p ON p.Ecode = e.Ecode
    LEFT JOIN tblReturnByBank r ON r.Ecode = e.Ecode
    WHERE e.MonthYear BETWEEN @StartDate AND @EndDate
)
    SELECT *
    FROM PayrollData
    ORDER BY MonthYear, Ecode
    OFFSET (@PageNumber - 1) * @PageSize ROWS
    FETCH NEXT @PageSize ROWS ONLY;

    -- Calculate totals
    SELECT 
        @TotalPayableSalary = SUM(ISNULL(TRY_CAST(e.Payable_Salary AS DECIMAL(18,2)), 0)),
        @TotalGivenToBank = SUM(ISNULL(TRY_CAST(t.BankTransfer AS DECIMAL(18,2)), 0)),
        @TotalPaidByBank = SUM(ISNULL(TRY_CAST(p.PaidByBank AS DECIMAL(18,2)), 0)),
        @TotalReturnByBank = SUM(ISNULL(TRY_CAST(r.ReturnByBank AS DECIMAL(18,2)), 0)),
        @TotalDifferencePayableMinusGiven = SUM(ISNULL(TRY_CAST(e.Payable_Salary AS DECIMAL(18,2)), 0) - ISNULL(TRY_CAST(t.BankTransfer AS DECIMAL(18,2)), 0)),
        @TotalDifferencePayableMinusPaid = SUM(ISNULL(TRY_CAST(e.Payable_Salary AS DECIMAL(18,2)), 0) - ISNULL(TRY_CAST(p.PaidByBank AS DECIMAL(18,2)), 0)),
        @TotalDifferencePayableMinusReturned = SUM(ISNULL(TRY_CAST(e.Payable_Salary AS DECIMAL(18,2)), 0) - ISNULL(TRY_CAST(r.ReturnByBank AS DECIMAL(18,2)), 0)),
        @TotalDifferenceGivenMinusPaid = SUM(ISNULL(TRY_CAST(t.BankTransfer AS DECIMAL(18,2)), 0) - ISNULL(TRY_CAST(p.PaidByBank AS DECIMAL(18,2)), 0)),
        @TotalDifferenceGivenMinusReturned = SUM(ISNULL(TRY_CAST(t.BankTransfer AS DECIMAL(18,2)), 0) - ISNULL(TRY_CAST(r.ReturnByBank AS DECIMAL(18,2)), 0))
    FROM EmployeePayroll e
    LEFT JOIN tblBankTransfer t ON e.Ecode = t.ecode
    LEFT JOIN tblPaidByBank p ON p.Ecode = e.Ecode
    LEFT JOIN tblReturnByBank r ON r.Ecode = e.Ecode
    WHERE e.MonthYear BETWEEN @StartDate AND @EndDate;

    -- Return totals
    SELECT 
        ISNULL(@TotalPayableSalary, 0) AS TotalPayableSalary,
        ISNULL(@TotalGivenToBank, 0) AS TotalGivenToBank,
        ISNULL(@TotalPaidByBank, 0) AS TotalPaidByBank,
        ISNULL(@TotalReturnByBank, 0) AS TotalReturnByBank,
        ISNULL(@TotalDifferencePayableMinusGiven, 0) AS TotalDifferencePayableMinusGiven,
        ISNULL(@TotalDifferencePayableMinusPaid, 0) AS TotalDifferencePayableMinusPaid,
        ISNULL(@TotalDifferencePayableMinusReturned, 0) AS TotalDifferencePayableMinusReturned,
        ISNULL(@TotalDifferenceGivenMinusPaid, 0) AS TotalDifferenceGivenMinusPaid,
        ISNULL(@TotalDifferenceGivenMinusReturned, 0) AS TotalDifferenceGivenMinusReturned;
END;
GO

PRINT '<< Done:     STEP 3 / 9 -- Payroll -- file: SPs_Payroll.sql';
GO

-- #############################################################################
-- STEP 4 / 9 -- Bonus -- file: SPs_Bonus.sql
-- #############################################################################
PRINT '>> Applying: STEP 4 / 9 -- Bonus -- file: SPs_Bonus.sql';
GO

-- -----------------------------------------------------------------------------
-- dbo.usp_ProcessBonusAndPayments
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[usp_ProcessBonusAndPayments]  
(  
      @Month     NVARCHAR(20),   -- e.g. 'Nov-2025' or '2025-11'  
      @Ecode     NVARCHAR(20),   -- specific Ecode  
      @CreatedBy NVARCHAR(100)   -- e.g. 'system' or login name  
)  
AS  
BEGIN  
    SET NOCOUNT ON;  
      DECLARE @PolicyId NVARCHAR(50);  
    ------------------------------------------------------------  
    -- GET CURRENT POLICY FOR THIS ECODE  
    ------------------------------------------------------------  
    SELECT @PolicyId = BonusProvisioningPolicyMaster  
    FROM EcodeWiseBonusProvisioningPolicyMapping (NOLOCK)  
    WHERE Ecode = @Ecode  
      AND IsActive = 1  
      AND IsDeleted = 0;  
      ------------------------------------------------------------  
    -- VALIDATE: Ecode Must Have Policy Mapped  
    ------------------------------------------------------------  
    IF @PolicyId IS NULL  
    BEGIN  
        --RAISERROR('No Bonus Policy defined for the given Ecode.', 16, 1);  
        RETURN;  
    END  
  
    ------------------------------------------------------------  
    -- CONDITIONAL CLEANUP BEFORE RECALCULATION  
    -- If policy is C6B/2366  -> clear from AdditionalPaymentHold  
    -- Else                   -> reset Bonus in tblPayments  
    ------------------------------------------------------------  
    IF @PolicyId IN ('C6BDAC6D-6EC3-F011-B1EA-8C84747E00C5',  
                     '2366FC08-6EC3-F011-B1EA-8C84747E00C5')  
    BEGIN  
        -- For C6B / 2366 policies, Bonus lives in tblPayments.  
        -- So remove any old record from AdditionalPaymentHold.  
        UPDATE AdditionalPaymentHold  
        SET Bonus = 0,  
            ExGratia = 0,  
            UpdatedOn = GETDATE(),  
            UpdatedBy = @CreatedBy  
        WHERE Ecode = @Ecode  
          AND [Month] = @Month;  
    END  
    ELSE if @PolicyId IN (  
                'C4BDAC6D-6EC3-F011-B1EA-8C84747E00C5',  
                '2166FC08-6EC3-F011-B1EA-8C84747E00C5',  
                '2266FC08-6EC3-F011-B1EA-8C84747E00C5',  
                'C5BDAC6D-6EC3-F011-B1EA-8C84747E00C5'  
              )  
    BEGIN  
        -- For other policies, Bonus lives in AdditionalPaymentHold.  
        -- So clear Bonus from tblPayments for this Ecode+Month.  
        UPDATE tblPayments  
        SET Bonus = 0  
        WHERE E_CODE = @Ecode  
          AND [MONTH] = @Month;  
    END  
  
    ------------------------------------------------------------  
    -- 1) MERGE INTO AdditionalPaymentHold (4 policies)  
    ------------------------------------------------------------  
    ;WITH BonusSource AS  
    (  
        SELECT  
              emp.Ecode,  
              @Month AS [Month],  
  
              Bonus = CASE   
                  WHEN map.BonusProvisioningPolicyMaster IN (  
                           'C4BDAC6D-6EC3-F011-B1EA-8C84747E00C5',  
                           '2166FC08-6EC3-F011-B1EA-8C84747E00C5'  
                       )  
                  THEN CASE WHEN emp.BasicSalary <= 21000   
                            THEN ROUND(emp.BasicSalary * 0.0833, 2)  
                            ELSE 0 END  
  
                  WHEN map.BonusProvisioningPolicyMaster IN (  
                           '2266FC08-6EC3-F011-B1EA-8C84747E00C5',  
                           'C5BDAC6D-6EC3-F011-B1EA-8C84747E00C5'  
                       )  
                  THEN CASE WHEN emp.BasicSalary <= 21000  
                            THEN ROUND(emp.BasicSalary * 0.0833, 2)  
                            ELSE 0 END  
  
                  ELSE 0  
              END,  
  
              ExGratia = CASE   
                  WHEN map.BonusProvisioningPolicyMaster IN (  
                           'C4BDAC6D-6EC3-F011-B1EA-8C84747E00C5',  
                           '2166FC08-6EC3-F011-B1EA-8C84747E00C5'  
                       )  
                  THEN CASE WHEN emp.BasicSalary <= 21000  
                            THEN 0  
                            ELSE ROUND(emp.BasicSalary * 0.0833, 2) END  
  
                  WHEN map.BonusProvisioningPolicyMaster IN (  
                           '2266FC08-6EC3-F011-B1EA-8C84747E00C5',  
                           'C5BDAC6D-6EC3-F011-B1EA-8C84747E00C5'  
                       )  
                  THEN CASE WHEN emp.BasicSalary <= 21000  
                            THEN ROUND(emp.[GROSS SALARY] * 0.0833, 2) - ROUND(emp.BasicSalary * 0.0833, 2)  
                            ELSE ROUND(emp.[GROSS SALARY] * 0.0833, 2) END  
  
                  ELSE 0  
              END,  
  
              @CreatedBy AS CreatedBy,  
              @CreatedBy AS UpdatedBy  
  
        FROM EcodeWiseBonusProvisioningPolicyMapping map  
        LEFT JOIN tblEmployee emp ON emp.Ecode = map.Ecode  
        LEFT JOIN BonusProvisioningPolicyMaster bpm ON bpm.Id = map.BonusProvisioningPolicyMaster  
        WHERE map.IsActive = 1  
          AND map.IsDeleted = 0  
          AND bpm.IsActive = 1  
          AND bpm.IsDeleted = 0  
          AND map.BonusProvisioningPolicyMaster IN (  
                'C4BDAC6D-6EC3-F011-B1EA-8C84747E00C5',  
                '2166FC08-6EC3-F011-B1EA-8C84747E00C5',  
                '2266FC08-6EC3-F011-B1EA-8C84747E00C5',  
                'C5BDAC6D-6EC3-F011-B1EA-8C84747E00C5'  
              )  
          AND map.Ecode = @Ecode  
    )  
    MERGE AdditionalPaymentHold AS tgt  
    USING BonusSource AS src  
        ON tgt.Ecode = src.Ecode  
       AND tgt.[Month] = src.[Month]  
       AND tgt.IsDeleted = 0  
    WHEN MATCHED THEN  
        UPDATE SET  
            tgt.Bonus = src.Bonus,  
            tgt.ExGratia = src.ExGratia,  
            tgt.UpdatedBy = src.UpdatedBy,  
            tgt.UpdatedOn = GETDATE(),  
            tgt.IsActive = 1  
    WHEN NOT MATCHED BY TARGET THEN  
        INSERT (Ecode, [Month], Bonus, ExGratia, CreatedBy, CreatedOn, UpdatedBy, UpdatedOn, IsActive, IsDeleted)  
        VALUES (src.Ecode, src.[Month], src.Bonus, src.ExGratia, src.CreatedBy, GETDATE(), src.UpdatedBy, GETDATE(), 1, 0);  
  
    ------------------------------------------------------------  
    -- 2) MERGE INTO tblPayments (2 policies: C6B..., 2366...)  
    ------------------------------------------------------------  
  
    ;WITH PaymentSource AS  
    (  
        SELECT  
              emp.Ecode AS E_CODE,  
              @Month AS [MONTH],  
              Bonus = CASE WHEN emp.BasicSalary > 21000  
                           THEN ROUND(emp.[GROSS SALARY] * 0.0833, 2)  
                           ELSE 0 END  
        FROM EcodeWiseBonusProvisioningPolicyMapping map  
        LEFT JOIN tblEmployee emp ON emp.Ecode = map.Ecode  
        LEFT JOIN BonusProvisioningPolicyMaster bpm ON bpm.Id = map.BonusProvisioningPolicyMaster  
        WHERE map.IsActive = 1  
          AND map.IsDeleted = 0  
          AND bpm.IsActive = 1  
          AND bpm.IsDeleted = 0  
          AND map.BonusProvisioningPolicyMaster IN (  
                'C6BDAC6D-6EC3-F011-B1EA-8C84747E00C5',  
                '2366FC08-6EC3-F011-B1EA-8C84747E00C5'  
              )  
          AND emp.BasicSalary > 21000  
          AND map.Ecode = @Ecode  
    )  
    MERGE tblPayments AS tgt  
    USING PaymentSource AS src  
        ON tgt.E_CODE = src.E_CODE  
       AND tgt.[MONTH] = src.[MONTH]  
    WHEN MATCHED THEN  
        UPDATE SET tgt.Bonus = src.Bonus  
    WHEN NOT MATCHED BY TARGET THEN  
        INSERT (E_CODE, Incentive, ARREAR, Overtime, Fooding_Allowance, Mobile_Bill, [MONTH], Bonus)  
        VALUES (src.E_CODE, 0, 0, 0, 0, 0, src.[MONTH], src.Bonus);  
  
END;
GO

-- -----------------------------------------------------------------------------
-- dbo.usp_ProcessBonusAndPayments_MonthWise_Dev
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[usp_ProcessBonusAndPayments_MonthWise_Dev]
    @Month     NVARCHAR(20),
    @CreatedBy NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    -- Pre-load policy + employee data for all active mapped employees
    IF OBJECT_ID('tempdb..#PolicyMap') IS NOT NULL DROP TABLE #PolicyMap;
    SELECT map.Ecode, map.BonusProvisioningPolicyMaster AS PolicyId,
           emp.BasicSalary, emp.[GROSS SALARY] AS GrossSalary
    INTO #PolicyMap
    FROM EcodeWiseBonusProvisioningPolicyMapping map WITH (NOLOCK)
    INNER JOIN tblEmployee emp WITH (NOLOCK) ON emp.Ecode=map.Ecode
    INNER JOIN BonusProvisioningPolicyMaster bpm WITH (NOLOCK) ON bpm.Id=map.BonusProvisioningPolicyMaster
    WHERE map.IsActive=1 AND map.IsDeleted=0 AND bpm.IsActive=1 AND bpm.IsDeleted=0;
    CREATE INDEX IX_PolicyMap_Ecode    ON #PolicyMap (Ecode);
    CREATE INDEX IX_PolicyMap_PolicyId ON #PolicyMap (PolicyId);

    -- Cleanup: clear AdditionalPaymentHold for C6B/2366 employees
    UPDATE aph SET aph.Bonus=0, aph.ExGratia=0, aph.UpdatedOn=GETDATE(), aph.UpdatedBy=@CreatedBy
    FROM AdditionalPaymentHold aph
    INNER JOIN #PolicyMap pm ON pm.Ecode=aph.Ecode
    WHERE aph.[Month]=@Month
      AND pm.PolicyId IN ('C6BDAC6D-6EC3-F011-B1EA-8C84747E00C5','2366FC08-6EC3-F011-B1EA-8C84747E00C5');

    -- Cleanup: clear tblPayments.Bonus for C4B/2166/2266/C5B employees
    UPDATE tp SET tp.Bonus=0
    FROM tblPayments tp
    INNER JOIN #PolicyMap pm ON pm.Ecode=tp.E_CODE
    WHERE tp.[MONTH]=@Month
      AND pm.PolicyId IN (
          'C4BDAC6D-6EC3-F011-B1EA-8C84747E00C5','2166FC08-6EC3-F011-B1EA-8C84747E00C5',
          '2266FC08-6EC3-F011-B1EA-8C84747E00C5','C5BDAC6D-6EC3-F011-B1EA-8C84747E00C5');

    -- Merge AdditionalPaymentHold (C4B/2166/2266/C5B policies)
    ;WITH BonusSource AS (
        SELECT pm.Ecode, @Month AS [Month],
            CASE WHEN pm.PolicyId IN (
                     'C4BDAC6D-6EC3-F011-B1EA-8C84747E00C5','2166FC08-6EC3-F011-B1EA-8C84747E00C5',
                     '2266FC08-6EC3-F011-B1EA-8C84747E00C5','C5BDAC6D-6EC3-F011-B1EA-8C84747E00C5')
                 THEN CASE WHEN pm.BasicSalary<=21000 THEN ROUND(pm.BasicSalary*0.0833,2) ELSE 0 END
                 ELSE 0 END AS Bonus,
            CASE WHEN pm.PolicyId IN ('C4BDAC6D-6EC3-F011-B1EA-8C84747E00C5','2166FC08-6EC3-F011-B1EA-8C84747E00C5')
                 THEN CASE WHEN pm.BasicSalary<=21000 THEN 0 ELSE ROUND(pm.BasicSalary*0.0833,2) END
                 WHEN pm.PolicyId IN ('2266FC08-6EC3-F011-B1EA-8C84747E00C5','C5BDAC6D-6EC3-F011-B1EA-8C84747E00C5')
                 THEN CASE WHEN pm.BasicSalary<=21000
                           THEN ROUND(pm.GrossSalary*0.0833,2)-ROUND(pm.BasicSalary*0.0833,2)
                           ELSE ROUND(pm.GrossSalary*0.0833,2) END
                 ELSE 0 END AS ExGratia,
            @CreatedBy AS CreatedBy, @CreatedBy AS UpdatedBy
        FROM #PolicyMap pm
        WHERE pm.PolicyId IN (
            'C4BDAC6D-6EC3-F011-B1EA-8C84747E00C5','2166FC08-6EC3-F011-B1EA-8C84747E00C5',
            '2266FC08-6EC3-F011-B1EA-8C84747E00C5','C5BDAC6D-6EC3-F011-B1EA-8C84747E00C5')
    )
    MERGE AdditionalPaymentHold AS tgt
    USING BonusSource AS src ON tgt.Ecode=src.Ecode AND tgt.[Month]=src.[Month] AND tgt.IsDeleted=0
    WHEN MATCHED THEN UPDATE SET
        tgt.Bonus=src.Bonus, tgt.ExGratia=src.ExGratia,
        tgt.UpdatedBy=src.UpdatedBy, tgt.UpdatedOn=GETDATE(), tgt.IsActive=1
    WHEN NOT MATCHED BY TARGET THEN INSERT
        (Ecode,[Month],Bonus,ExGratia,CreatedBy,CreatedOn,UpdatedBy,UpdatedOn,IsActive,IsDeleted)
    VALUES (src.Ecode,src.[Month],src.Bonus,src.ExGratia,src.CreatedBy,GETDATE(),src.UpdatedBy,GETDATE(),1,0);

    -- Merge tblPayments (C6B/2366 policies)
    ;WITH PaymentSource AS (
        SELECT pm.Ecode AS E_CODE, @Month AS [MONTH],
               ROUND(pm.GrossSalary*0.0833,2) AS Bonus
        FROM #PolicyMap pm
        WHERE pm.PolicyId IN ('C6BDAC6D-6EC3-F011-B1EA-8C84747E00C5','2366FC08-6EC3-F011-B1EA-8C84747E00C5')
          AND pm.BasicSalary>21000
    )
    MERGE tblPayments AS tgt
    USING PaymentSource AS src ON tgt.E_CODE=src.E_CODE AND tgt.[MONTH]=src.[MONTH]
    WHEN MATCHED THEN UPDATE SET tgt.Bonus=src.Bonus
    WHEN NOT MATCHED BY TARGET THEN INSERT
        (E_CODE,Incentive,ARREAR,Overtime,Fooding_Allowance,Mobile_Bill,[MONTH],Bonus)
    VALUES (src.E_CODE,0,0,0,0,0,src.[MONTH],src.Bonus);

    DROP TABLE IF EXISTS #PolicyMap;
END;
GO

-- -----------------------------------------------------------------------------
-- dbo.usp_ExportEmployeeBonusGratuity
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[usp_ExportEmployeeBonusGratuity]  
(  
    @SearchTerm NVARCHAR(100) = '',  
    @Ecode NVARCHAR(20) = NULL,
    @PageNumber INT = 0,
    @PageSize INT = 0,
    @TotalEmployees INT OUTPUT,
    @CurrentPageNumber INT OUTPUT
)  
AS  
BEGIN  
    SET NOCOUNT ON;  

    -- 1) Filter and calculate basic data into a temp table
    SELECT   
        e.Ecode AS [Employee Code],  
        e.[Full Name] AS [Employee Name],  
        e.GENDER,
        e.DOB,
        e.DOJ,
        e.MOBILE,
        e.[EMAIL ADDRESS],
        d.DepartmentName,
        de.DesignationName,
        e.[FATHER'S NAME],
        e.ReportHeadEcode,
        rh.[FULL NAME] AS ReportingHeadName,
        s.ShiftName,
        l.LocationName,
        stat.StateName,
        FORMAT(GETDATE(), 'MMM-yyyy') AS [Month],  
        SUM(CASE   
                WHEN YEAR(dt) = YEAR(GETDATE())   
                 AND MONTH(dt) = MONTH(GETDATE())  
                THEN ISNULL(b.ActualBonus, 0)  
                ELSE 0  
            END) AS [Current Month Bonus],  
        SUM(ISNULL(b.ActualBonus, 0)) AS [Total Bonus],  
        SUM(CASE   
                WHEN YEAR(dt) = YEAR(GETDATE())   
                 AND MONTH(dt) = MONTH(GETDATE())  
                THEN ISNULL(b.Gratuity, 0)  
                ELSE 0  
            END) AS [Current Month Gratuity],  
        SUM(ISNULL(b.ClosingGratuity, 0)) AS [Total Gratuity]
    INTO #FinalResults
    FROM tblEmployee e  
    INNER JOIN (  
        SELECT *,  
               TRY_CONVERT(date, '01-' + [Month], 106) AS dt  
        FROM BonusAndGratutityOpening  
    ) b ON e.Ecode = b.ECode  
    INNER JOIN tblEmployee rh ON rh.Ecode = e.ReportHeadEcode
    INNER JOIN tblDepartment d ON e.DepartmentId = d.DepartmentId
    INNER JOIN tblDesignation de ON de.DesignationId = e.DesignationId
    INNER JOIN tblShiftMaster s ON s.ShiftID = e.ShiftID
    INNER JOIN tblLocation l ON e.LocationId = l.LocationId
    INNER JOIN tblState stat ON stat.StateId = l.StateId
    WHERE   
        e.IsActive = 1   
        AND e.IsDeleted = 0  
        AND (@Ecode IS NULL OR e.Ecode = @Ecode)  
        AND (@SearchTerm = '' OR   
             e.Ecode LIKE '%' + @SearchTerm + '%' OR  
             e.[Full Name] LIKE '%' + @SearchTerm + '%')  
        AND b.dt BETWEEN DATEFROMPARTS(YEAR(GETDATE()) - 1, 10, 1)    
                    AND EOMONTH(GETDATE())  
    GROUP BY   
        e.Ecode,  
        e.[Full Name],  
        e.GENDER,
        e.DOB,
        e.DOJ,
        e.MOBILE,
        e.[EMAIL ADDRESS],
        d.DepartmentName,
        de.DesignationName,
        e.[FATHER'S NAME],
        e.ReportHeadEcode,
        rh.[FULL NAME],
        s.ShiftName,
        l.LocationName,
        stat.StateName;

    -- 2) Set outputs
    SELECT @TotalEmployees = COUNT(*) FROM #FinalResults;
    SET @CurrentPageNumber = @PageNumber;

    -- 3) Return results based on pagination
    IF @PageNumber = 0 AND @PageSize = 0
    BEGIN
        SELECT * FROM #FinalResults ORDER BY [Employee Code];
    END
    ELSE
    BEGIN
        SELECT * FROM #FinalResults
        ORDER BY [Employee Code]
        OFFSET (@PageNumber - 1) * @PageSize ROWS
        FETCH NEXT @PageSize ROWS ONLY;
    END

    DROP TABLE #FinalResults;
END
GO

-- -----------------------------------------------------------------------------
-- dbo.USP_GENERATE_EMP_GRATUITY_BONUS
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[USP_GENERATE_EMP_GRATUITY_BONUS]
AS
BEGIN
    SET NOCOUNT ON;

    DROP TABLE IF EXISTS ##TEMPDATA;

    DECLARE @COL NVARCHAR(MAX) = N'',
            @SQL NVARCHAR(MAX) = N'',
            @SELECTCOLNAME NVARCHAR(MAX) = N'';

    ;WITH BASE_CTE AS
    (
        SELECT DISTINCT
            ISNULL(LOC.STCode,'') AS [Location CODE],
            ISNULL(LocationName,'') AS [LOCATION],
            ISNULL(StateName ,'') AS [STATE],
            [Employee Code],
            ISNULL([Name of Employee],'') AS [NAME],
            ISNULL(Sex ,'') AS [GENDER],
            CASE WHEN [D.O.J.] IS NULL THEN '' ELSE CONVERT(VARCHAR(10), [D.O.J.], 120) END AS [JOINING DATE],
            ISNULL([Mob No.],'') AS [MOBILE NO.],
            CASE WHEN [D.O.L.] IS NULL THEN '' ELSE CONVERT(VARCHAR(10), [D.O.L.], 120) END AS [LEAVING DATE],
            ISNULL(EMP.Department,'') AS [DEPARTMENT],
            ISNULL(EMP.Designation,'') AS [DESIGNATION],
            CASE WHEN [Is Active] = 1 THEN 'ACTIVE' ELSE 'NOT ACTIVE' END AS [STATUS]
        FROM HRMS.dbo.NEW_EmployeeViewWithExp EMP
        LEFT JOIN HRMS.[dbo].[LOCATIONMASTER] LOC
            ON EMP.STATES = LOC.STCODE
        WHERE [Is Active] = 1
          AND ([Employee Code] LIKE 'V%' OR [Employee Code] LIKE 'N%')
          AND NOT ([Employee Code] LIKE 'V2S%' AND LOC.STCode = 'DB01')
    ),
    cte_final AS
    (
        SELECT DISTINCT
            EMP.*,
            ISNULL([BONUS B/F FROM LAST MTH],'') AS [BONUS B/F FROM LAST MTH],
            ISNULL([BONUS EARNED],'') AS [BONUS EARNED],
            ISNULL([BONUS C/F FROM NEXT MTH],'') AS [BONUS C/F FROM NEXT MTH],
            ISNULL([GRATUITY B/F FROM LAST MTH],'') AS [GRATUITY B/F FROM LAST MTH],
            ISNULL([GRATUITY EARNED],'') AS [GRATUITY EARNED],
            ISNULL([GRATUITY C/F FROM NEXT MTH],'') AS [GRATUITY C/F FROM NEXT MTH],
            [MONTH]
        FROM BASE_CTE EMP
        LEFT JOIN hrms.dbo.VW_SALARY_FORMAT SAL
            ON EMP.[Employee Code] = SAL.[E.CODE]
    ),
    CTE_UNPIVOT AS
    (
        SELECT *,
               'BONUS B/F FROM LAST MTH_' + UPPER(LEFT([MONTH], 3)) + '_20' + RIGHT([MONTH], 2) AS PARTICULARS_NAME,
               ISNULL(TRY_CAST([BONUS B/F FROM LAST MTH] AS NUMERIC(18,2)),0) AS VALUE
        FROM cte_final

        UNION ALL
        SELECT *,
               'BONUS EARNED_' + UPPER(LEFT([MONTH], 3)) + '_20' + RIGHT([MONTH], 2),
               ISNULL(TRY_CAST([BONUS EARNED] AS NUMERIC(18,2)),0)
        FROM cte_final

        UNION ALL
        SELECT *,
               'BONUS C/F FROM NEXT MTH_' + UPPER(LEFT([MONTH], 3)) + '_20' + RIGHT([MONTH], 2),
               ISNULL(TRY_CAST([BONUS C/F FROM NEXT MTH] AS NUMERIC(18,2)),0)
        FROM cte_final

        UNION ALL
        SELECT *,
               'GRATUITY B/F FROM LAST MTH_' + UPPER(LEFT([MONTH], 3)) + '_20' + RIGHT([MONTH], 2),
               ISNULL(TRY_CAST([GRATUITY B/F FROM LAST MTH] AS NUMERIC(18,2)),0)
        FROM cte_final

        UNION ALL
        SELECT *,
               'GRATUITY EARNED_' + UPPER(LEFT([MONTH], 3)) + '_20' + RIGHT([MONTH], 2),
               ISNULL(TRY_CAST([GRATUITY EARNED] AS NUMERIC(18,2)),0)
        FROM cte_final

        UNION ALL
        SELECT *,
               'GRATUITY C/F FROM NEXT MTH_' + UPPER(LEFT([MONTH], 3)) + '_20' + RIGHT([MONTH], 2),
               ISNULL(TRY_CAST([GRATUITY C/F FROM NEXT MTH] AS NUMERIC(18,2)),0)
        FROM cte_final
    )
    SELECT *
    INTO ##TEMPDATA
    FROM CTE_UNPIVOT;

    /* ===== Build column list safely as NVARCHAR(MAX) ===== */

    SELECT @COL =
        STRING_AGG(
            CAST('ISNULL(' + QUOTENAME(PARTICULARS_NAME) + ', 0) AS ' + QUOTENAME(PARTICULARS_NAME) AS NVARCHAR(MAX)),
            N', '
        )
    WITHIN GROUP
    (
        ORDER BY
            TRY_CAST('01-' + REPLACE(RIGHT(PARTICULARS_NAME, 8), '_', '-') AS DATE),
            CASE
                WHEN PARTICULARS_NAME LIKE 'BONUS B/F FROM LAST MTH%' THEN 1
                WHEN PARTICULARS_NAME LIKE 'BONUS EARNED%' THEN 2
                WHEN PARTICULARS_NAME LIKE 'BONUS C/F FROM NEXT MTH%' THEN 3
                WHEN PARTICULARS_NAME LIKE 'GRATUITY B/F FROM LAST MTH%' THEN 4
                WHEN PARTICULARS_NAME LIKE 'GRATUITY EARNED%' THEN 5
                WHEN PARTICULARS_NAME LIKE 'GRATUITY C/F FROM NEXT MTH%' THEN 6
                ELSE 99
            END
    )
    FROM (SELECT DISTINCT PARTICULARS_NAME FROM ##TEMPDATA) AS OrderedCols;

    SELECT @SELECTCOLNAME =
        STRING_AGG(
            CAST(QUOTENAME(PARTICULARS_NAME) AS NVARCHAR(MAX)),
            N', '
        )
    WITHIN GROUP
    (
        ORDER BY
            TRY_CAST('01-' + REPLACE(RIGHT(PARTICULARS_NAME, 8), '_', '-') AS DATE),
            CASE
                WHEN PARTICULARS_NAME LIKE 'BONUS B/F FROM LAST MTH%' THEN 1
                WHEN PARTICULARS_NAME LIKE 'BONUS EARNED%' THEN 2
                WHEN PARTICULARS_NAME LIKE 'BONUS C/F FROM NEXT MTH%' THEN 3
                WHEN PARTICULARS_NAME LIKE 'GRATUITY B/F FROM LAST MTH%' THEN 4
                WHEN PARTICULARS_NAME LIKE 'GRATUITY EARNED%' THEN 5
                WHEN PARTICULARS_NAME LIKE 'GRATUITY C/F FROM NEXT MTH%' THEN 6
                ELSE 99
            END
    )
    FROM (SELECT DISTINCT PARTICULARS_NAME FROM ##TEMPDATA) AS OrderedCols;

    /* ===== Dynamic Pivot ===== */

    SET @SQL = N'
SELECT
    ROW_NUMBER() OVER (ORDER BY [Location CODE], [Employee Code]) AS [S.No],
    [Location CODE], [LOCATION], [STATE], [Employee Code], [NAME],
    [GENDER], [JOINING DATE], [MOBILE NO.], [LEAVING DATE],
    [DEPARTMENT], [DESIGNATION], [STATUS], ' + @COL + N'
FROM
(
    SELECT
        [Location CODE], [LOCATION], [STATE], [Employee Code], [NAME],
        [GENDER], [JOINING DATE], [MOBILE NO.], [LEAVING DATE],
        [DEPARTMENT], [DESIGNATION], [STATUS],
        PARTICULARS_NAME, ISNULL(VALUE, 0) AS VALUE
    FROM ##TEMPDATA
) AS src
PIVOT
(
    SUM(VALUE) FOR PARTICULARS_NAME IN (' + @SELECTCOLNAME + N')
) AS pvt;';

    EXEC sp_executesql @SQL;
END
GO

-- -----------------------------------------------------------------------------
-- dbo.usp_GetEmployeeFinalBonus
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.usp_GetEmployeeFinalBonus
(
    @Ecode NVARCHAR(20)
)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE
        @PolicyId NVARCHAR(50),
        @JoiningDate DATE,
        @StartMonth DATE,
        @EndMonth DATE,
        @FinalBonus DECIMAL(18,2) = 0,
        @BonusStartMonth NVARCHAR(10) = NULL,
        @BonusEndMonth NVARCHAR(10) = NULL,
        @Remarks NVARCHAR(200) = NULL;

    /* ================= POLICY ================= */
    SELECT @PolicyId = BonusProvisioningPolicyMaster
    FROM EcodeWiseBonusProvisioningPolicyMapping
    WHERE Ecode = @Ecode
      AND IsActive = 1
      AND IsDeleted = 0;

    IF @PolicyId IS NULL
    BEGIN
        SELECT
            @Ecode AS Ecode,
            NULL AS BonusStartMonth,
            NULL AS BonusEndMonth,
            0 AS FinalBonus,
            'No Policy Defined' AS Remarks;
        RETURN;
    END

    /* ================= JOINING DATE ================= */
    SELECT @JoiningDate = DOJ
    FROM tblEmployee
    WHERE Ecode = @Ecode;

    /* ================= LAST PUNCH MONTH ================= */
    SELECT
        @EndMonth = DATEFROMPARTS(
                        YEAR(MAX(PunchDate)),
                        MONTH(MAX(PunchDate)),
                        1
                    )
    FROM tblEmployeeMultiPunches
    WHERE UserID = @Ecode
      AND (
            CAST(PARSENAME(TotalHours,2) AS INT) * 60 +
            CAST(PARSENAME(TotalHours,1) AS INT)
          ) >= 270;

    IF @EndMonth IS NULL
    BEGIN
        SELECT
            @Ecode AS Ecode,
            NULL AS BonusStartMonth,
            NULL AS BonusEndMonth,
            0 AS FinalBonus,
            'No valid punch data' AS Remarks;
        RETURN;
    END

    /* ================= START MONTH (LAST OCT LOGIC) ================= */
    IF MONTH(@EndMonth) >= 10
        SET @StartMonth = DATEFROMPARTS(YEAR(@EndMonth), 10, 1);
    ELSE
        SET @StartMonth = DATEFROMPARTS(YEAR(@EndMonth) - 1, 10, 1);

    /* ================= BONUS CALCULATION ================= */
    ;WITH MonthRange AS
    (
        SELECT @StartMonth AS M
        UNION ALL
        SELECT DATEADD(MONTH, 1, M)
        FROM MonthRange
        WHERE M < @EndMonth
    )
    SELECT
        @FinalBonus = SUM(
            CASE
                WHEN @PolicyId = '2166FC08-6EC3-F011-B1EA-8C84747E00C5'
                     AND ISNULL(a.TOTAL_PRESENT,0) < 30 THEN 0

                WHEN @PolicyId = '2266FC08-6EC3-F011-B1EA-8C84747E00C5'
                     AND ISNULL(a.TOTAL_PRESENT,0) = 0 THEN 0

                WHEN sal.[BasicSalary(Bud.)] <= 21000
                    THEN sal.[BasicSalary(Actual)] * 0.0833

                ELSE
                    sal.[Monthly Gross CTC(Actual After Deduction AND AddONS)] * 0.0833
            END
        )
    FROM MonthRange m
    LEFT JOIN EmpAttendanceMaster a
        ON a.E_CODE = @Ecode
       AND a.[MONTH] = FORMAT(m.M, 'MMM-yy')
    LEFT JOIN vw_Emp_Attendance_Format sal
        ON sal.Ecode = @Ecode
       AND sal.[Month-Year] = FORMAT(m.M, 'MMM-yy')
    OPTION (MAXRECURSION 0);

    SET @BonusStartMonth = FORMAT(@StartMonth, 'MMM-yy');
    SET @BonusEndMonth   = FORMAT(@EndMonth, 'MMM-yy');

    /* ================= FINAL OUTPUT ================= */
    SELECT
        @Ecode AS Ecode,
        @BonusStartMonth AS BonusStartMonth,
        @BonusEndMonth AS BonusEndMonth,
        ISNULL(@FinalBonus, 0) AS FinalBonus,
        NULL AS Remarks;
END;
GO

-- -----------------------------------------------------------------------------
-- dbo.usp_GetEmployeeBonus
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.usp_GetEmployeeBonus
(
    @Month NVARCHAR(20),   -- e.g. 'Nov-2025'
    @Ecode NVARCHAR(20)
)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @PolicyId NVARCHAR(50);

    ------------------------------------------------------------
    -- Get Active Policy for Ecode
    ------------------------------------------------------------
    SELECT @PolicyId = BonusProvisioningPolicyMaster
    FROM EcodeWiseBonusProvisioningPolicyMapping WITH (NOLOCK)
    WHERE Ecode = @Ecode
      AND IsActive = 1
      AND IsDeleted = 0;

    IF @PolicyId IS NULL
    BEGIN
        SELECT 
            @Ecode AS Ecode,
            @Month AS [Month],
            0 AS Bonus,
            0 AS ExGratia,
            'No Policy Mapped' AS Remarks;
        RETURN;
    END

    ------------------------------------------------------------
    -- Calculate Bonus / ExGratia
    ------------------------------------------------------------
    SELECT
        emp.Ecode,
        @Month AS [Month],
        Bonus =
            CASE
                -- Policies where bonus based on Basic <= 21000
                WHEN @PolicyId IN (
                        'C4BDAC6D-6EC3-F011-B1EA-8C84747E00C5',
                        '2166FC08-6EC3-F011-B1EA-8C84747E00C5',
                        '2266FC08-6EC3-F011-B1EA-8C84747E00C5',
                        'C5BDAC6D-6EC3-F011-B1EA-8C84747E00C5'
                     )
                THEN
                    CASE
                        WHEN emp.BasicSalary <= 21000
                            THEN ROUND(emp.BasicSalary * 0.0833, 2)
                        ELSE 0
                    END

                -- C6B / 2366 policies
                WHEN @PolicyId IN (
                        'C6BDAC6D-6EC3-F011-B1EA-8C84747E00C5',
                        '2366FC08-6EC3-F011-B1EA-8C84747E00C5'
                     )
                THEN
                    CASE
                        WHEN emp.BasicSalary > 21000
                            THEN ROUND(emp.[GROSS SALARY] * 0.0833, 2)
                        ELSE 0
                    END
                ELSE 0
            END,

        ExGratia =
            CASE
                WHEN @PolicyId IN (
                        'C4BDAC6D-6EC3-F011-B1EA-8C84747E00C5',
                        '2166FC08-6EC3-F011-B1EA-8C84747E00C5'
                     )
                THEN
                    CASE
                        WHEN emp.BasicSalary > 21000
                            THEN ROUND(emp.BasicSalary * 0.0833, 2)
                        ELSE 0
                    END

                WHEN @PolicyId IN (
                        '2266FC08-6EC3-F011-B1EA-8C84747E00C5',
                        'C5BDAC6D-6EC3-F011-B1EA-8C84747E00C5'
                     )
                THEN
                    CASE
                        WHEN emp.BasicSalary <= 21000
                            THEN ROUND(emp.[GROSS SALARY] * 0.0833, 2)
                                 - ROUND(emp.BasicSalary * 0.0833, 2)
                        ELSE ROUND(emp.[GROSS SALARY] * 0.0833, 2)
                    END
                ELSE 0
            END,

        @PolicyId AS PolicyId
    FROM tblEmployee emp
    WHERE emp.Ecode = @Ecode;
END
GO

-- -----------------------------------------------------------------------------
-- dbo.GETEMPBONUSLIST
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE GETEMPBONUSLIST  
AS  
BEGIN  
 select B.E_Code,E.FirstName + ' ' + E.LastName AS FullName,B.Date AS BonusDate,B.Amount,B.Acc_Number,B.UTR from tblBonus_Upload B  
 left join tblEmployee E on B.E_Code=E.Ecode  
END
GO

-- -----------------------------------------------------------------------------
-- dbo.usp_ProcessRetentionBonus
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.usp_ProcessRetentionBonus
(
      @ECode      VARCHAR(20)       -- e.g. 'E001'
    , @MonthToken VARCHAR(7)        -- format MMM-YY, e.g. 'Jan-25'
)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ProcessMonth DATE;
    DECLARE @MonthStr     VARCHAR(11);

    -- Convert 'Jan-25' -> '01 Jan 25' (dd mon yy) then to DATE
    SET @MonthStr = '01 ' + REPLACE(@MonthToken, '-', ' ');  -- '01 Jan 25'
    SET @ProcessMonth = TRY_CONVERT(DATE, @MonthStr, 6);     -- style 6 = dd mon yy

    IF @ProcessMonth IS NULL
    BEGIN
        RAISERROR('Invalid Month format. Expected MMM-YY, e.g. Jan-25.', 16, 1);
        RETURN;
    END;

    ;WITH RB AS
    (
        SELECT
              rb.ECode
            , rb.BonusAmount
            , rb.RetentionStart
            , rb.RetentionEnd
            , TotalMonths =
                DATEDIFF(
                    MONTH,
                    DATEFROMPARTS(YEAR(rb.RetentionStart), MONTH(rb.RetentionStart), 1),
                    DATEFROMPARTS(YEAR(rb.RetentionEnd),   MONTH(rb.RetentionEnd),   1)
                ) + 1
        FROM dbo.tblRetentionBonus rb
        WHERE rb.Accepted = 1
          AND rb.IsActive = 1
          AND rb.IsDeleted = 0
          AND rb.ECode = @ECode
          AND @ProcessMonth BETWEEN 
                DATEFROMPARTS(YEAR(rb.RetentionStart), MONTH(rb.RetentionStart), 1)
            AND DATEFROMPARTS(YEAR(rb.RetentionEnd),   MONTH(rb.RetentionEnd),   1)
    ),
    FinalRB AS
    (
        -- If multiple retention letters overlap, sum their monthly bonus
        SELECT
              ECode
            , @MonthToken AS MonthToken
            , SUM(CAST(BonusAmount / NULLIF(TotalMonths, 0) AS DECIMAL(18,2))) AS MonthlyRetentionBonus
        FROM RB
        GROUP BY ECode
    )
    MERGE dbo.AdditionalPaymentHold AS T
    USING FinalRB AS S
       ON  T.Ecode = S.ECode
       AND T.[Month] = S.MonthToken        -- Month stored as MMM-YY
    WHEN MATCHED THEN
        UPDATE SET
              T.RetentionBonus = S.MonthlyRetentionBonus
            , T.UpdatedOn      = GETDATE()
            , T.UpdatedBy      = 'RetentionBonus_Auto'
    WHEN NOT MATCHED BY TARGET THEN
        INSERT (
              Ecode
            , [Month]
            , Bonus
            , ExGratia
            , CreatedBy
            , CreatedOn
            , UpdatedBy
            , UpdatedOn
            , IsActive
            , IsDeleted
            , GratuityMonthlyProvision
            , RetentionBonus
        )
        VALUES (
              S.ECode
            , S.MonthToken
            , 0                        -- Bonus
            , 0                        -- ExGratia
            , 'RetentionBonus_Auto'
            , GETDATE()
            , NULL
            , NULL
            , 1                        -- IsActive
            , 0                        -- IsDeleted
            , 0                        -- GratuityMonthlyProvision
            , S.MonthlyRetentionBonus
        );
END;
GO

-- -----------------------------------------------------------------------------
-- dbo.vw_Bonus_Gratuity
-- -----------------------------------------------------------------------------

/****** Object:  View [dbo].[vw_Bonus_Gratuity]    Script Date: 08-07-2025 15:39:54 ******/
--SET ANSI_NULLS ON
--GO

--SET QUOTED_IDENTIFIER ON
--GO
--Select BonusApplicable from tblEmployee where Ecode='V18426'
--Truncate table BonusAndGratutityOpening
--Select * from [vw_Bonus_Gratuity]
CREATE OR ALTER VIEW [dbo].[vw_Bonus_Gratuity] as
Select a.ECode,a.Month,Gratuity,Bonus,b.[BasicSalary(Bud.)],b.[BasicSalary(Actual)],
CASE 
    WHEN c.DOJ IS NULL OR c.DOJ > GETDATE() THEN 0
    ELSE 
        dbo.fn_GetMonthPortion(c.DOJ, c.DateOfLeft, a.Month)
END AS [Months],
c.DOJ,
ISNULL(try_cast(c.DateOfLeft as nvarchar(50)),'') DateOfLeft,
b.[Monthly Gross CTC(Actual)] as 'Gross(with Reimbursement)',

ActualGratuity,ActualBonus,case when ISNULL(BonusApplicable,0)=0 then 'Not Applicable' else 'Applicable' end 'IsBonusApplicable'
from BonusAndGratutityOpening a (NOLOCK) 
Left Join vw_Emp_Attendance_Format b (Nolock) on a.ECode=b.Ecode
Left Join tblEmployee c (NOLOCK) on a.ECode=c.Ecode
--where ECode = 'v00025'

--Select [BasicSalary(Actual)] from vw_Emp_Attendance_Format where Ecode='v00025'
--Select Doj from tblEmployee where Ecode = 'v00025'
--GO
GO

PRINT '<< Done:     STEP 4 / 9 -- Bonus -- file: SPs_Bonus.sql';
GO

-- #############################################################################
-- STEP 5 / 9 -- Inactive Reports -- file: SPs_InactiveReports.sql
-- #############################################################################
PRINT '>> Applying: STEP 5 / 9 -- Inactive Reports -- file: SPs_InactiveReports.sql';
GO

-- -----------------------------------------------------------------------------
-- dbo.sp_Report_InactiveEmployees_NoDuesNotSubmitted
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE sp_Report_InactiveEmployees_NoDuesNotSubmitted
as
begin
	set nocount on;

	select
		e.Ecode,
		e.[FULL NAME] as 'Full Name',
		e.GENDER,
		e.DOJ,
		e.MOBILE,
		e.[EMAIL ADDRESS] as 'Email',
		e.[FATHER'S NAME] as 'Father Name',
		e.ReportHeadEcode,
		rm.[FULL NAME] as 'Reporthead Name',
		sm.ShiftName,
		loc.LocationName,
		e.DateOfLeft
	from tblEmployee e
		left join tblDepartment dept with (nolock) on e.DepartmentId = dept.DepartmentId
		left join tblDesignation desg with (nolock) on e.DesignationId = desg.DesignationId
		left join tblLocation loc with (nolock) on e.LocationId = loc.LocationId
		left join tblEmployee rm on e.ReportHeadEcode = rm.Ecode
		left join tblShiftMaster sm on e.ShiftID = sm.ShiftID
		left join EmployeeResignationChecklistResponse res on e.EmployeeId = res.EmployeeId
	where
		res.EmployeeResignationChecklistMasterId = 5
		and res.Attachment is null
		and e.IsActive = 0
end
GO

-- -----------------------------------------------------------------------------
-- dbo.sp_ReportInactiveEmployeesWithFNF
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_ReportInactiveEmployeesWithFNF]
(
    @Months INT = 2
)
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH InactiveChange AS
    (
        SELECT
            h.EmployeeId,
            h.ValidFrom AS WentInactiveOn,
            LAG(h.IsActive) OVER
            (
                PARTITION BY h.EmployeeId
                ORDER BY h.ValidFrom
            ) AS PreviousStatus,
            h.IsActive
        FROM HRMS.dbo.tblEmployee_History h
        WHERE h.ValidFrom >= DATEADD(MONTH, -@Months, GETDATE())
    )

    SELECT
        e.Ecode,
        e.[FULL NAME] AS EmployeeName,
        ic.WentInactiveOn,
        
        CASE
            WHEN fp.Status = 'Paid'
                THEN 'UTR Exists'
            ELSE 'UTR Pending'
        END AS UTRStatus,
        fp.ChequeNo AS ChequeNumber,
        CASE
            WHEN rt.Attachment IS NOT NULL
                THEN CONCAT('https://v2parivar.v2retail.com:9987/', rt.Attachment)
            ELSE NULL
        END AS AttachmentLink

    FROM HRMS.dbo.tblEmployee e

    LEFT JOIN InactiveChange ic
        ON ic.EmployeeId = e.EmployeeId
        AND ic.PreviousStatus = 1
        AND ic.IsActive = 0

    LEFT JOIN HRMS.dbo.fnf_header fh
        ON fh.EmployeeId = e.EmployeeId

    LEFT JOIN HRMS.dbo.fnf_payment fp
        ON fp.FNFId = fh.FNFId

    OUTER APPLY
    (
        SELECT TOP 1 Attachment
        FROM HRMS.dbo.EmployeeResignationChecklistResponse r
        WHERE r.EmployeeId = e.EmployeeId
        AND r.Attachment IS NOT NULL
        ORDER BY r.EmployeeId
    ) rt

    WHERE e.IsActive = 0
    ORDER BY ic.WentInactiveOn DESC, e.Ecode;

END

--select * from fnf_payment
GO

-- -----------------------------------------------------------------------------
-- dbo.sp_ReportActiveInEmpMasterinActiveHRMS
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE sp_ReportActiveInEmpMasterinActiveHRMS  
as  
begin  
set nocount on;  
  
SELECT   
    e.EmployeeId,  
    e.Ecode,  
    e.[FULL NAME] AS EmployeeName,  
    e.GENDER,  
    e.DOJ,  
    e.MOBILE,  
    e.[EMAIL ADDRESS],  
  
    d.DepartmentName,  
    de.DesignationName,  
  
    e.ReportHeadEcode,  
    rh.[FULL NAME] AS ReportingHeadName,  
  
    s.ShiftName,  
    l.LocationName,  
    st.StateName,  
  
    eam.E_CODE AS HRMSEcode  
FROM tblEmployee e  
  
LEFT JOIN EmpAttendanceMaster eam  
    ON eam.E_CODE = e.Ecode  
  
LEFT JOIN tblEmployee rh  
    ON rh.Ecode = e.ReportHeadEcode  
  
LEFT JOIN tblDepartment d  
    ON d.DepartmentId = e.DepartmentId  
  
LEFT JOIN tblDesignation de  
    ON de.DesignationId = e.DesignationId  
  
LEFT JOIN tblShiftMaster s  
    ON s.ShiftID = e.ShiftID  
  
LEFT JOIN tblLocation l  
    ON l.LocationId = e.LocationId  
  
LEFT JOIN tblState st  
    ON st.StateId = l.StateId  
  
WHERE   
    e.IsActive = 1  
    AND e.IsDeleted = 0  
    AND eam.E_CODE IS NULL    
ORDER BY e.Ecode  
end  
  
;
GO

-- -----------------------------------------------------------------------------
-- dbo.sp_ReportActiveInHRMSinActiveEmpMaster
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE sp_ReportActiveInHRMSinActiveEmpMaster  
as  
begin  
set nocount on;  
  
SELECT   
    e.EmployeeId,  
    e.Ecode,  
    e.[FULL NAME] AS EmployeeName,  
    e.GENDER,  
    e.DOJ,  
    e.MOBILE,  
    e.[EMAIL ADDRESS],  
  
    d.DepartmentName,  
    de.DesignationName,  
  
    e.ReportHeadEcode,  
    rh.[FULL NAME] AS ReportingHeadName,  
  
    s.ShiftName,  
    l.LocationName,  
    st.StateName,  
  
    eam.IsActive AS HRMSStatus  
FROM tblEmployee e  
  
INNER JOIN EmpAttendanceMaster eam  
    ON eam.E_CODE = e.Ecode  
  
LEFT JOIN tblEmployee rh  
    ON rh.Ecode = e.ReportHeadEcode  
  
LEFT JOIN tblDepartment d  
    ON d.DepartmentId = e.DepartmentId  
  
LEFT JOIN tblDesignation de  
    ON de.DesignationId = e.DesignationId  
  
LEFT JOIN tblShiftMaster s  
    ON s.ShiftID = e.ShiftID  
  
LEFT JOIN tblLocation l  
    ON l.LocationId = e.LocationId  
  
LEFT JOIN tblState st  
    ON st.StateId = l.StateId  
  
WHERE   
    e.IsActive = 1  
    AND e.IsDeleted = 0  
    AND eam.IsActive = 0  
ORDER BY e.Ecode  
  
end
GO

-- -----------------------------------------------------------------------------
-- dbo.sp_ReportNoResignationApprovalStillInactive
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE sp_ReportNoResignationApprovalStillInactive
as
begin
set nocount on;

select 
	 e.EmployeeId,
	 e.Ecode,
	 e.[FULL NAME] as EmployeeName,
	 desg.DesignationName,
	 s.ShiftName,
	 dept.DepartmentName,
	 st.StateName,
	 l.LocationName,
	 e.ReportHeadEcode,
	 rh.[Full Name] as ReportHeadName,
	 e.GENDER,
	 e.DOJ,
	 e.MOBILE,
	 e.[EMAIL ADDRESS]
	from tblEmployee e

	left join tblEmployee rh on
	e.EmployeeId = rh.EmployeeId

	left join tblShiftMaster s on
	s.ShiftID = e.ShiftID

	left join tblDesignation desg on
	desg.DesignationId = e.DesignationId
	
	left join tblDepartment dept on
	dept.DepartmentId = e.DepartmentId

	left join tblLocation l on
	l.LocationId = e.LocationId

	left join tblState st on
	st.StateId = l.StateId

	left join tblEmployeeSepration sp on
	sp.EmployeeId = e.EmployeeId

	where (sp.IsApprovedByManager = 0 or sp.IsApprovedByHR = 0) 
	and e.IsActive =0

	order by e.ecode
end
GO

-- -----------------------------------------------------------------------------
-- dbo.sp_ReportInactiveStillWorking
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_ReportInactiveStillWorking  
AS  
BEGIN  
    SET NOCOUNT ON;  

    BEGIN TRY  

        DECLARE @Today DATE = CAST(GETDATE() AS DATE);  
        DECLARE @Last3DaysStart DATE = DATEADD(DAY, -3, @Today);  

        SELECT   
            e.Ecode,
            e.[FULL NAME]        AS FullName,
            e.GENDER,
            e.DOB,
            e.DOJ,
            e.MOBILE,
            e.[EMAIL ADDRESS],
            d.DepartmentName,
            de.DesignationName,
            e.[FATHER'S NAME],
            e.ReportHeadEcode,
            rh.[FULL NAME]       AS ReportingHeadName,
            s.ShiftName,
            l.LocationName,
            stat.StateName,
            e.DateOfLeft,
            e.IsActive,
            p.LastValidPunchDate AS LastPunch

        FROM dbo.tblEmployee e  

        -- Get Last Valid Punch
        OUTER APPLY  
        (  
            SELECT MAX(x.AttendanceDate) AS LastValidPunchDate  
            FROM dbo.tbl_fn_GetMonthlyPunchesRange_productionnewnick_test x  
            WHERE x.ECode = e.Ecode  
              AND TRY_CAST(x.TotalWorkingMinutes AS TIME) >= '04:30'  
        ) p  

        -- Self Join for Reporting Head
        LEFT JOIN dbo.tblEmployee rh 
            ON rh.Ecode = e.ReportHeadEcode

        INNER JOIN dbo.tblDepartment d 
            ON e.DepartmentId = d.DepartmentId

        INNER JOIN dbo.tblDesignation de 
            ON de.DesignationId = e.DesignationId

        INNER JOIN dbo.tblShiftMaster s 
            ON s.ShiftID = e.ShiftID

        INNER JOIN dbo.tblLocation l 
            ON l.LocationId = e.LocationId

        INNER JOIN dbo.tblState stat 
            ON stat.StateId = l.StateId

        WHERE   
            e.IsActive = 0  
            AND p.LastValidPunchDate IS NOT NULL  
            AND p.LastValidPunchDate >= @Last3DaysStart  
            AND p.LastValidPunchDate <= @Today  

        ORDER BY p.LastValidPunchDate DESC;

    END TRY  
    BEGIN CATCH  
        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();  
        RAISERROR(@ErrorMessage, 16, 1);  
    END CATCH  
END
GO

-- -----------------------------------------------------------------------------
-- dbo.sp_GetInactiveEmployees_LastPunch_LastUpdate
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_GetInactiveEmployees_LastPunch_LastUpdate
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        e.EmployeeId,
        e.Ecode AS EmployeeCode,
        e.[FULL NAME] AS EmployeeName,

        e.[JOINING DATE] AS DateOfJoining,
        e.[DateOfLeft] AS DateOfLeaving,

        e.IsActive,
        ISNULL(e.IsFNFCompleted, 0) AS IsFNFCompleted,

        -- ✅ LAST PUNCH FROM ATTENDANCE
        lp.LastPunchDate,

        -- ✅ LAST UPDATED INFO FROM EMPLOYEE TABLE
        e.LastUpdatedBy,
        e.UpdatedOn AS LastUpdatedDate,

        -- OPTIONAL MASTER DATA
        ISNULL(d.DepartmentName, '') AS Department,
        ISNULL(g.DesignationName, '') AS Designation

    FROM dbo.tblEmployee e

    LEFT JOIN dbo.tblDepartment d 
        ON d.DepartmentId = e.DepartmentId

    LEFT JOIN dbo.tblDesignation g 
        ON g.DesignationId = e.DesignationId

    -- ✅ LAST PUNCH DATE PER EMPLOYEE (BY ECODE)
    LEFT JOIN (
        SELECT
            t.ECode,
            MAX(t.AttendanceDate) AS LastPunchDate
        FROM dbo.tbl_fn_GetMonthlyPunchesRange_productionnewnick_test t
        WHERE
              ISNULL(t.IsOnLeave,0) = 1
           OR ISNULL(t.IsRegularize,0) = 1
           OR ISNULL(t.ValidPunchCount,0) > 0
           OR TRY_CAST(NULLIF(LTRIM(RTRIM(ISNULL(t.TotalWorkingMinutes,''))), '') AS TIME) > '00:00:00'
           OR TRY_CAST(NULLIF(LTRIM(RTRIM(ISNULL(t.PunchIn,''))), '') AS TIME) > '00:00:00'
           OR TRY_CAST(NULLIF(LTRIM(RTRIM(ISNULL(t.PunchOut,''))), '') AS TIME) > '00:00:00'
        GROUP BY t.ECode
    ) lp 
        ON lp.ECode = e.Ecode

    WHERE
        ISNULL(e.IsStore, 0) <> 1
        AND ISNULL(e.IsActive, 0) = 0;   -- ✅ ONLY INACTIVE EMPLOYEES

END
GO

-- -----------------------------------------------------------------------------
-- dbo.sp_GetInactiveEmployeesWithLastPunch
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_GetInactiveEmployeesWithLastPunch
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @TwoMonthsAgo DATE = DATEADD(YEAR, -1, GETDATE());

    SELECT 
        e.EmployeeId,
        e.Ecode AS EmployeeCode,
        e.[FULL NAME] AS Name,
        ISNULL(d.DepartmentName, '') AS Department,
        ISNULL(g.DesignationName, '') AS Designation,
        e.[JOINING DATE] AS DateOfJoining,
        e.[DateOfLeft] AS DateOfLeaving,
        ISNULL(e.IsFNFCompleted, 0) AS IsFNFCompleted,

        0 AS UnpaidSalaryAmount,
        0 AS UnpaidSalaryDays,
        NULL AS UnpaidSalaryMonth,

        ISNULL(rt.ResignationTypeName, '') AS ResignationType,
        ts.ResignationDate,
        ts.LastDay AS SeparationLastDay,

        lp.LastPunchDate,   -- ✅ LAST PUNCH DATE

        ISNULL(ts.IsApprovedByManager, 0) AS ManagerApproved,
        ISNULL(ts.IsApprovedByHR, 0) AS HRApproved,
        r.Attachment AS ResignationAttachment

    FROM dbo.tblEmployee e
    LEFT JOIN dbo.tblEmployeeSepration ts 
        ON ts.EmployeeId = e.EmployeeId
    LEFT JOIN dbo.tblDepartment d 
        ON d.DepartmentId = e.DepartmentId
    LEFT JOIN dbo.tblDesignation g 
        ON g.DesignationId = e.DesignationId
    LEFT JOIN dbo.tblResignationType rt 
        ON rt.ResignationTypeId = ts.ResignationTypeId

    -- ✅ LAST PUNCH DATE BY ECODE
    LEFT JOIN (
        SELECT
            t.ECode,
            MAX(t.AttendanceDate) AS LastPunchDate
        FROM dbo.tbl_fn_GetMonthlyPunchesRange_productionnewnick_test t
        WHERE
              ISNULL(t.IsOnLeave,0) = 1
           OR ISNULL(t.IsRegularize,0) = 1
           OR ISNULL(t.ValidPunchCount,0) > 0
           OR TRY_CAST(NULLIF(LTRIM(RTRIM(ISNULL(t.TotalWorkingMinutes,''))), '') AS TIME) > '00:00:00'
           OR TRY_CAST(NULLIF(LTRIM(RTRIM(ISNULL(t.PunchIn,''))), '') AS TIME) > '00:00:00'
           OR TRY_CAST(NULLIF(LTRIM(RTRIM(ISNULL(t.PunchOut,''))), '') AS TIME) > '00:00:00'
        GROUP BY t.ECode
    ) lp 
        ON lp.ECode = e.Ecode

    LEFT JOIN (
        SELECT TOP 1 er.EmployeeId, er.Attachment
        FROM dbo.EmployeeResignationChecklistResponse er
        WHERE er.Attachment IS NOT NULL
        GROUP BY er.EmployeeId, er.Attachment
    ) r 
        ON r.EmployeeId = e.EmployeeId

    WHERE 
        ISNULL(e.IsStore, 0) <> 1 
        AND ISNULL(e.IsActive, 0) = 0
        AND e.[DateOfLeft] IS NOT NULL
        AND e.[DateOfLeft] < @TwoMonthsAgo;

END
GO

PRINT '<< Done:     STEP 5 / 9 -- Inactive Reports -- file: SPs_InactiveReports.sql';
GO

-- #############################################################################
-- STEP 6 / 9 -- FNF -- file: SPs_FNF.sql
-- #############################################################################
PRINT '>> Applying: STEP 6 / 9 -- FNF -- file: SPs_FNF.sql';
GO

-- -----------------------------------------------------------------------------
-- dbo.sp_FNF_BulkUpload
-- -----------------------------------------------------------------------------

-- Create enhanced procedure
CREATE OR ALTER PROCEDURE dbo.sp_FNF_BulkUpload
(
    @JsonData nvarchar(max),                    -- JSON array from API (Excel rows)
    @CreatedBy nvarchar(200) = 'System',       -- optional, caller can override
    @DuplicateEcodes nvarchar(max) OUTPUT,      -- JSON array of duplicate Ecodes from input
    @AlreadyDoneEcodes nvarchar(max) OUTPUT,    -- JSON array of Ecodes with FNF already completed
    @ProcessedCount int OUTPUT,                 -- Number of successfully processed records
    @TotalRecords int OUTPUT                    -- Total number of records in input
)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;  -- auto-rollback on runtime errors

    -- Initialize output parameters
    SET @DuplicateEcodes = NULL;
    SET @AlreadyDoneEcodes = NULL;
    SET @ProcessedCount = 0;
    SET @TotalRecords = 0;

    BEGIN TRY
        BEGIN TRAN;

        --------------------------------------------------------------------
        -- 1. Temp table to hold incoming rows (types aligned to DB)
        --------------------------------------------------------------------
        CREATE TABLE #Upload
        (
            RowNo                int IDENTITY(1,1) PRIMARY KEY,
            EmployeeId           bigint NULL,
            Ecode                nvarchar(50)      NOT NULL,
            FNFDate              date              NULL,
            DateOfLeaving        date              NULL,

            -- Additions (FNF_Additions)
            UnpaidSalaryAmount   decimal(18,2)     NULL,
            Rate                 decimal(18,2)     NULL,
            Days                 decimal(18,2)     NULL,  -- CHANGED: decimal
            SalaryMonth          nvarchar(100)     NULL,
            Bonus                decimal(18,2)     NULL,
            BonusPeriodFrom      date              NULL,
            BonusPeriodTill      date              NULL,
            Gratuity             decimal(18,2)     NULL,
            CalculatedAs         nvarchar(400)     NULL,
            E_LeaveAmount        decimal(18,2)     NULL,
            ELDays               decimal(18,2)     NULL, -- CHANGED: decimal
            NoticeSalary         decimal(18,2)     NULL,
            OtherAddition1       decimal(18,2)     NULL,
            OtherAddition2       decimal(18,2)     NULL,
            OtherAddition3       decimal(18,2)     NULL,
            OtherAddition4       decimal(18,2)     NULL,

            -- Deductions (FNF_Deductions)
            LoanBalance          decimal(18,2)     NULL,
            AdvanceBalance       decimal(18,2)     NULL,
            OtherDeduction1      decimal(18,2)     NULL,
            OtherDeduction2      decimal(18,2)     NULL,
            OtherDeduction3      decimal(18,2)     NULL,
            OtherDeduction4      decimal(18,2)     NULL,
            TotalPayable         decimal(18,2)     NULL,
            TDS                  decimal(18,2)     NULL,
            NetPayable           decimal(18,2)     NULL,
            DepositOn            decimal(18,2)     NULL, -- decimal, as per schema

            -- Payment (FNF_Payment)
            SendForPaymentAmount decimal(18,2)     NULL,
            AmountPaid           decimal(18,2)     NULL,
            PaymentStatus        nvarchar(100)     NULL,
            ChequeNo             nvarchar(100)     NULL,
            ChequeDate           date              NULL,
            PaymentVoucherNo     nvarchar(100)     NULL,
            PaymentRemarks       nvarchar(1000)    NULL
        );

        --------------------------------------------------------------------
        -- 2. Insert Excel rows into temp table (from JSON)
        --------------------------------------------------------------------
        INSERT INTO #Upload
        (
            Ecode,
            FNFDate,
            DateOfLeaving,
            UnpaidSalaryAmount,
            Rate,
            Days,
            SalaryMonth,
            Bonus,
            BonusPeriodFrom,
            BonusPeriodTill,
            Gratuity,
            CalculatedAs,
            E_LeaveAmount,
            ELDays,
            NoticeSalary,
            OtherAddition1,
            OtherAddition2,
            OtherAddition3,
            OtherAddition4,
            LoanBalance,
            AdvanceBalance,
            OtherDeduction1,
            OtherDeduction2,
            OtherDeduction3,
            OtherDeduction4,
            TotalPayable,
            TDS,
            NetPayable,
            DepositOn,
            SendForPaymentAmount,
            AmountPaid,
            PaymentStatus,
            ChequeNo,
            ChequeDate,
            PaymentVoucherNo,
            PaymentRemarks
        )
        SELECT
            Ecode,
            FNFDate,
            DateOfLeaving,
            UnpaidSalaryAmount,
            Rate,
            Days,
            SalaryMonth,
            Bonus,
            BonusPeriodFrom,
            BonusPeriodTill,
            Gratuity,
            CalculatedAs,
            E_LeaveAmount,
            ELDays,
            NoticeSalary,
            OtherAddition1,
            OtherAddition2,
            OtherAddition3,
            OtherAddition4,
            LoanBalance,
            AdvanceBalance,
            OtherDeduction1,
            OtherDeduction2,
            OtherDeduction3,
            OtherDeduction4,
            TotalPayable,
            TDS,
            NetPayable,
            DepositOn,
            SendForPaymentAmount,
            AmountPaid,
            PaymentStatus,
            ChequeNo,
            ChequeDate,
            PaymentVoucherNo,
            PaymentRemarks
        FROM OPENJSON(@JsonData)
        WITH
        (
            Ecode                nvarchar(50)     '$.Ecode',
            FNFDate              date             '$.FNFDate',
            DateOfLeaving        date             '$.DateOfLeaving',

            UnpaidSalaryAmount   decimal(18,2)    '$.UnpaidSalaryAmount',
            Rate                 decimal(18,2)    '$.Rate',
            Days                 decimal(18,2)    '$.Days',        -- CHANGED: decimal
            SalaryMonth          nvarchar(100)    '$.SalaryMonth',
            Bonus                decimal(18,2)    '$.Bonus',
            BonusPeriodFrom      date             '$.BonusPeriodFrom',
            BonusPeriodTill      date             '$.BonusPeriodTill',
            Gratuity             decimal(18,2)    '$.Gratuity',
            CalculatedAs         nvarchar(400)    '$.CalculatedAs',
            E_LeaveAmount        decimal(18,2)    '$.E_LeaveAmount',
            ELDays               decimal(18,2)    '$.ELDays',      -- CHANGED: decimal
            NoticeSalary         decimal(18,2)    '$.NoticeSalary',
            OtherAddition1       decimal(18,2)    '$.OtherAddition1',
            OtherAddition2       decimal(18,2)    '$.OtherAddition2',
            OtherAddition3       decimal(18,2)    '$.OtherAddition3',
            OtherAddition4       decimal(18,2)    '$.OtherAddition4',

            LoanBalance          decimal(18,2)    '$.LoanBalance',
            AdvanceBalance       decimal(18,2)    '$.AdvanceBalance',
            OtherDeduction1      decimal(18,2)    '$.OtherDeduction1',
            OtherDeduction2      decimal(18,2)    '$.OtherDeduction2',
            OtherDeduction3      decimal(18,2)    '$.OtherDeduction3',
            OtherDeduction4      decimal(18,2)    '$.OtherDeduction4',
            TotalPayable         decimal(18,2)    '$.TotalPayable',
            TDS                  decimal(18,2)    '$.TDS',
            NetPayable           decimal(18,2)    '$.NetPayable',
            DepositOn            decimal(18,2)    '$.DepositOn',

            SendForPaymentAmount decimal(18,2)    '$.SendForPaymentAmount',
            AmountPaid           decimal(18,2)    '$.AmountPaid',
            PaymentStatus        nvarchar(100)    '$.PaymentStatus',
            ChequeNo             nvarchar(100)    '$.ChequeNo',
            ChequeDate           date             '$.ChequeDate',
            PaymentVoucherNo     nvarchar(100)    '$.PaymentVoucherNo',
            PaymentRemarks       nvarchar(1000)   '$.PaymentRemarks'
        );

        -- Set total records count
        SET @TotalRecords = @@ROWCOUNT;

        --------------------------------------------------------------------
        -- 3. Find duplicate Ecodes within the input data
        --------------------------------------------------------------------
        ;WITH DuplicateEcodes AS (
            SELECT Ecode
            FROM #Upload
            GROUP BY Ecode
            HAVING COUNT(*) > 1
        )
        SELECT @DuplicateEcodes = (SELECT Ecode FROM DuplicateEcodes FOR JSON PATH);

        --------------------------------------------------------------------
        -- 4. Resolve EmployeeId from Ecode (NOLOCK on master table)
        --------------------------------------------------------------------
        UPDATE u
        SET u.EmployeeId = e.EmployeeId
        FROM #Upload u
        LEFT JOIN dbo.tblEmployee e WITH (NOLOCK)
            ON e.Ecode = u.Ecode;

        -- Find Ecodes that don't exist in tblEmployee
        DECLARE @InvalidEcodes nvarchar(max);
        ;WITH InvalidEcodes AS (
            SELECT Ecode
            FROM #Upload
            WHERE EmployeeId IS NULL
        )
        SELECT @InvalidEcodes = (SELECT Ecode FROM InvalidEcodes FOR JSON PATH);

        -- If there are invalid Ecodes, include them in duplicate output and remove from processing
        IF @InvalidEcodes IS NOT NULL
        BEGIN
            IF @DuplicateEcodes IS NULL
                SET @DuplicateEcodes = @InvalidEcodes;
            ELSE
                SET @DuplicateEcodes = @DuplicateEcodes + SUBSTRING(@InvalidEcodes, 2, LEN(@InvalidEcodes) - 1);
        END

        --------------------------------------------------------------------
        -- 5. Find Ecodes with FNF already completed
        --------------------------------------------------------------------
        ;WITH AlreadyDoneEcodes AS (
            SELECT DISTINCT u.Ecode
            FROM #Upload u
            JOIN dbo.FNF_Header h WITH (NOLOCK)
                ON h.EmployeeId = u.EmployeeId
            WHERE u.EmployeeId IS NOT NULL
        )
        SELECT @AlreadyDoneEcodes = (SELECT Ecode FROM AlreadyDoneEcodes FOR JSON PATH);

        --------------------------------------------------------------------
        -- 6. Filter out invalid, duplicate, and already done records from processing
        --------------------------------------------------------------------
        DELETE u
        FROM #Upload u
        WHERE u.EmployeeId IS NULL  -- Invalid Ecodes
           OR EXISTS (
               SELECT 1 
               FROM #Upload u2 
               WHERE u2.Ecode = u.Ecode 
               AND u2.RowNo < u.RowNo
           )  -- Keep only first occurrence of duplicates
           OR EXISTS (
               SELECT 1 
               FROM dbo.FNF_Header h WITH (NOLOCK)
               WHERE h.EmployeeId = u.EmployeeId
           );  -- Already done

        --------------------------------------------------------------------
        -- 7. Insert into FNF_Header and capture FNFId mapping
        --------------------------------------------------------------------
        CREATE TABLE #MapFNF
        (
            EmployeeId bigint PRIMARY KEY,
            FNFId      bigint NOT NULL
        );

        INSERT INTO dbo.FNF_Header
        (
            EmployeeId,
            CreatedBy,
            CreatedOn
        )
        OUTPUT
            inserted.EmployeeId,
            inserted.FNFId
        INTO #MapFNF (EmployeeId, FNFId)
        SELECT
            u.EmployeeId,
            @CreatedBy,
            GETDATE()
        FROM #Upload u;

        -- Set processed count
        SET @ProcessedCount = @@ROWCOUNT;

        --------------------------------------------------------------------
        -- 8. Insert into FNF_Additions (CAST Days / ELDays to int)
        --------------------------------------------------------------------
        INSERT INTO dbo.FNF_Additions
        (
            FNFId,
            EmployeeId,
            FNFDate,
            DateOfLeaving,
            UnpaidSalaryAmount,
            Rate,
            Days,
            SalaryMonth,
            Bonus,
            BonusPeriodFrom,
            BonusPeriodTill,
            Gratuity,
            CalculatedAs,
            E_LeaveAmount,
            ELDays,
            NoticeSalary,
            OtherAddition1,
            OtherAddition2,
            OtherAddition3,
            OtherAddition4
        )
        SELECT
            m.FNFId,
            u.EmployeeId,
            u.FNFDate,
            u.DateOfLeaving,
            u.UnpaidSalaryAmount,
            u.Rate,
            CASE WHEN u.Days  IS NULL THEN NULL ELSE CAST(u.Days  AS int) END,
            u.SalaryMonth,
            u.Bonus,
            u.BonusPeriodFrom,
            u.BonusPeriodTill,
            u.Gratuity,
            u.CalculatedAs,
            u.E_LeaveAmount,
            CASE WHEN u.ELDays IS NULL THEN NULL ELSE CAST(u.ELDays AS int) END,
            u.NoticeSalary,
            u.OtherAddition1,
            u.OtherAddition2,
            u.OtherAddition3,
            u.OtherAddition4
        FROM #Upload u
        JOIN #MapFNF m ON m.EmployeeId = u.EmployeeId;

        --------------------------------------------------------------------
        -- 9. Insert into FNF_Deductions
        --------------------------------------------------------------------
        INSERT INTO dbo.FNF_Deductions
        (
            FNFId,
            EmployeeId,
            LoanBalance,
            AdvanceBalance,
            OtherDeduction1,
            OtherDeduction2,
            OtherDeduction3,
            OtherDeduction4,
            TotalPayable,
            TDS,
            NetPayable,
            DepositOn
        )
        SELECT
            m.FNFId,
            u.EmployeeId,
            u.LoanBalance,
            u.AdvanceBalance,
            u.OtherDeduction1,
            u.OtherDeduction2,
            u.OtherDeduction3,
            u.OtherDeduction4,
            u.TotalPayable,
            u.TDS,
            u.NetPayable,
            u.DepositOn
        FROM #Upload u
        JOIN #MapFNF m ON m.EmployeeId = u.EmployeeId;

        --------------------------------------------------------------------
        -- 10. Insert into FNF_Payment (only when data present)
        --------------------------------------------------------------------
        INSERT INTO dbo.FNF_Payment
        (
            FNFId,
            SendForPaymentAmount,
            Remarks,
            ChequeNo,
            ChequeDate,
            Status,
            AmountPaid,
            PaymentVoucherNo,
            CreatedOn,
            CreatedBy
        )
        SELECT
            m.FNFId,
            u.SendForPaymentAmount,
            u.PaymentRemarks,
            u.ChequeNo,
            u.ChequeDate,
            u.PaymentStatus,
            u.AmountPaid,
            u.PaymentVoucherNo,
            GETDATE(),
            @CreatedBy
        FROM #Upload u
        JOIN #MapFNF m ON m.EmployeeId = u.EmployeeId
        WHERE
            u.SendForPaymentAmount IS NOT NULL
            OR u.AmountPaid IS NOT NULL
            OR u.PaymentStatus IS NOT NULL
            OR u.ChequeNo IS NOT NULL
            OR u.ChequeDate IS NOT NULL
            OR u.PaymentVoucherNo IS NOT NULL
            OR u.PaymentRemarks IS NOT NULL;

        --------------------------------------------------------------------
        -- 11. Commit
        --------------------------------------------------------------------
        COMMIT TRAN;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRAN;

        -- Bubble original error to C#
        THROW;
    END CATCH
END;
GO

-- -----------------------------------------------------------------------------
-- dbo.sp_FNF_GetAccountsList
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_FNF_GetAccountsList
    @Search nvarchar(100) = NULL,         -- ecode/name search
    @FromDate date = NULL,                -- filter by FNFDate
    @ToDate date = NULL,
    @PaymentStatus nvarchar(50) = NULL,   -- latest payment status
    @Page int = 1,
    @PageSize int = 20
AS
BEGIN
    SET NOCOUNT ON;

    WITH Base AS
    (
        SELECT *
        FROM dbo.vw_FNF_AccountsList
        WHERE (@Search IS NULL OR @Search = ''
               OR Ecode LIKE @Search + '%'
               OR EmployeeName LIKE '%' + @Search + '%')
          AND (@FromDate IS NULL OR FNFDate >= @FromDate)
          AND (@ToDate   IS NULL OR FNFDate <= @ToDate)
          AND (@PaymentStatus IS NULL OR PaymentStatus = @PaymentStatus)
    )
    SELECT COUNT(1) AS TotalCount FROM Base;

    ;WITH Base2 AS
    (
        SELECT *, ROW_NUMBER() OVER(ORDER BY FNFDate DESC, FNFId DESC) AS rn
        FROM dbo.vw_FNF_AccountsList
        WHERE (@Search IS NULL OR @Search = ''
               OR Ecode LIKE @Search + '%'
               OR EmployeeName LIKE '%' + @Search + '%')
          AND (@FromDate IS NULL OR FNFDate >= @FromDate)
          AND (@ToDate   IS NULL OR FNFDate <= @ToDate)
          AND (@PaymentStatus IS NULL OR PaymentStatus = @PaymentStatus)
    )
    SELECT *
    FROM Base2
    WHERE rn BETWEEN ((@Page-1)*@PageSize + 1) AND (@Page*@PageSize)
    ORDER BY rn;
END
GO

-- -----------------------------------------------------------------------------
-- dbo.sp_FNF_GetAccountsList_Paid
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_FNF_GetAccountsList_Paid  
    @Search        nvarchar(100) = NULL,   -- ecode/name search  
    @FromDate      date = NULL,            -- filter by FNFDate  
    @ToDate        date = NULL,  
    @PaymentStatus nvarchar(50) = NULL,    -- kept for compatibility; unpaid view has NULL status  
    @Page          int = 1,  
    @PageSize      int = 20  
AS  
BEGIN  
    SET NOCOUNT ON;  
  
    -- Safety defaults  
    IF @Page < 1 SET @Page = 1;  
    IF @PageSize < 1 SET @PageSize = 20;  
  
    ;WITH Base AS  
    (  
        SELECT *  
        FROM dbo.vw_FNF_AccountsList_Paid  
        WHERE (@Search IS NULL OR @Search = ''  
               OR Ecode LIKE @Search + '%'  
               OR EmployeeName LIKE '%' + @Search + '%')  
          AND (@FromDate IS NULL OR FNFDate >= @FromDate)  
          AND (@ToDate   IS NULL OR FNFDate <= @ToDate)  
  
          -- Optional: since this view is "unpaid", PaymentStatus is NULL  
          -- If caller passes PaymentStatus, only match when they pass NULL/''  
          AND (  
                @PaymentStatus IS NULL OR @PaymentStatus = ''  
                OR PaymentStatus = @PaymentStatus  
              )  
    )  
    SELECT COUNT(1) AS TotalCount  
    FROM Base;  
  
    ;WITH Base2 AS  
    (  
        SELECT *,  
               ROW_NUMBER() OVER(ORDER BY FNFDate DESC, FNFId DESC) AS rn  
        FROM dbo.vw_FNF_AccountsList_Paid  
        WHERE (@Search IS NULL OR @Search = ''  
               OR Ecode LIKE @Search + '%'  
               OR EmployeeName LIKE '%' + @Search + '%')  
          AND (@FromDate IS NULL OR FNFDate >= @FromDate)  
          AND (@ToDate   IS NULL OR FNFDate <= @ToDate)  
          AND (  
                @PaymentStatus IS NULL OR @PaymentStatus = ''  
                OR PaymentStatus = @PaymentStatus  
              )  
    )  
    SELECT *  
    FROM Base2  
    WHERE rn BETWEEN ((@Page-1)*@PageSize + 1) AND (@Page*@PageSize)  
    ORDER BY rn;  
END
GO

-- -----------------------------------------------------------------------------
-- dbo.sp_FNF_GetAccountsList_Unpaid
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_FNF_GetAccountsList_Unpaid
    @Search        nvarchar(100) = NULL,   -- ecode/name search
    @FromDate      date = NULL,            -- filter by FNFDate
    @ToDate        date = NULL,
    @PaymentStatus nvarchar(50) = NULL,    -- kept for compatibility; unpaid view has NULL status
    @Page          int = 1,
    @PageSize      int = 20
AS
BEGIN
    SET NOCOUNT ON;

    -- Safety defaults
    IF @Page < 1 SET @Page = 1;
    IF @PageSize < 1 SET @PageSize = 20;

    ;WITH Base AS
    (
        SELECT *
        FROM dbo.vw_FNF_AccountsList_Unpaid
        WHERE (@Search IS NULL OR @Search = ''
               OR Ecode LIKE @Search + '%'
               OR EmployeeName LIKE '%' + @Search + '%')
          AND (@FromDate IS NULL OR FNFDate >= @FromDate)
          AND (@ToDate   IS NULL OR FNFDate <= @ToDate)

          -- Optional: since this view is "unpaid", PaymentStatus is NULL
          -- If caller passes PaymentStatus, only match when they pass NULL/''
          AND (
                @PaymentStatus IS NULL OR @PaymentStatus = ''
                OR PaymentStatus = @PaymentStatus
              )
    )
    SELECT COUNT(1) AS TotalCount
    FROM Base;

    ;WITH Base2 AS
    (
        SELECT *,
               ROW_NUMBER() OVER(ORDER BY FNFDate DESC, FNFId DESC) AS rn
        FROM dbo.vw_FNF_AccountsList_Unpaid
        WHERE (@Search IS NULL OR @Search = ''
               OR Ecode LIKE @Search + '%'
               OR EmployeeName LIKE '%' + @Search + '%')
          AND (@FromDate IS NULL OR FNFDate >= @FromDate)
          AND (@ToDate   IS NULL OR FNFDate <= @ToDate)
          AND (
                @PaymentStatus IS NULL OR @PaymentStatus = ''
                OR PaymentStatus = @PaymentStatus
              )
    )
    SELECT *
    FROM Base2
    WHERE rn BETWEEN ((@Page-1)*@PageSize + 1) AND (@Page*@PageSize)
    ORDER BY rn;
END
GO

-- -----------------------------------------------------------------------------
-- dbo.vw_FNF_AccountsList
-- -----------------------------------------------------------------------------
CREATE OR ALTER VIEW [dbo].[vw_FNF_AccountsList]  
AS  
SELECT  
    h.FNFId,  
    h.EmployeeId,  
    e.Ecode,  
    ISNULL(e.[FULL NAME], CONCAT(ISNULL(e.FirstName,''),' ',  
                                 ISNULL(NULLIF(e.MiddleName,''),''),  
                                 CASE WHEN ISNULL(e.LastName,'')<>'' THEN ' '+e.LastName ELSE '' END)) AS EmployeeName,  
     desg.DesignationName,
    e.[PAN NO] as PanNo,
    e.[JOINING DATE] as JoiningDate,
    dept.DepartmentName,
    l.LocationName,
    l.STCode,
    e.[BANK NAME] as BankName,
    e.[A/C NO] as AccountNo,
    e.[BANK IFSC CODE] As IFSC,
    sn.paybledays,
    sn.[Month-Year],
    sn.[PF(Total)] as PF,
    sn.[ESIC(Total)] as ESIC,
    sn.PTax,
    a.FNFDate,  
    a.DateOfLeaving,  
  
    -- Additions  
    a.UnpaidSalaryAmount,  
    a.Rate,  
    a.Days,  
    a.SalaryMonth,  
    a.Bonus,  
    a.BonusPeriodFrom,  
    a.BonusPeriodTill,  
    a.Gratuity,  
    a.CalculatedAs,  
    a.E_LeaveAmount,  
    a.ELDays,  
    a.NoticeSalary,  
    a.OtherAddition1,  
    a.OtherAddition2,  
    a.OtherAddition3,  
    a.OtherAddition4,  
  
    -- Deductions  
    d.LoanBalance,  
    d.AdvanceBalance,  
    d.OtherDeduction1,  
    d.OtherDeduction2,  
    d.OtherDeduction3,  
    d.OtherDeduction4,  
    d.TotalPayable,  
    d.TDS,  
    d.NetPayable,  
    d.DepositOn,  
  
    -- Totals  
    CAST(ISNULL(a.UnpaidSalaryAmount,0)  
       + ISNULL(a.Bonus,0)  
       + ISNULL(a.Gratuity,0)  
       + ISNULL(a.E_LeaveAmount,0)  
       + ISNULL(a.NoticeSalary,0)  
       + ISNULL(a.OtherAddition1,0)  
       + ISNULL(a.OtherAddition2,0)  
       + ISNULL(a.OtherAddition3,0)  
       + ISNULL(a.OtherAddition4,0) AS decimal(18,2)) AS TotalAdditions,  
  
    CAST(ISNULL(d.LoanBalance,0)  
       + ISNULL(d.AdvanceBalance,0)  
       + ISNULL(d.OtherDeduction1,0)  
       + ISNULL(d.OtherDeduction2,0)  
       + ISNULL(d.OtherDeduction3,0)  
       + ISNULL(d.OtherDeduction4,0)  
       + ISNULL(d.TDS,0) AS decimal(18,2)) AS TotalDeductions,  
  
    CAST(  
        (ISNULL(a.UnpaidSalaryAmount,0)+ISNULL(a.Bonus,0)+ISNULL(a.Gratuity,0)+ISNULL(a.E_LeaveAmount,0)+ISNULL(a.NoticeSalary,0)  
         +ISNULL(a.OtherAddition1,0)+ISNULL(a.OtherAddition2,0)+ISNULL(a.OtherAddition3,0)+ISNULL(a.OtherAddition4,0))  
        -  
        (ISNULL(d.LoanBalance,0)+ISNULL(d.AdvanceBalance,0)+ISNULL(d.OtherDeduction1,0)+ISNULL(d.OtherDeduction2,0)  
         +ISNULL(d.OtherDeduction3,0)+ISNULL(d.OtherDeduction4,0)+ISNULL(d.TDS,0))  
        AS decimal(18,2)) AS NetAmount,  
  
    -- Latest payment  
    p.SendForPaymentAmount,  
    p.AmountPaid,  
    p.Status AS PaymentStatus,  
    p.ChequeNo,  
    p.ChequeDate,  
    p.PaymentVoucherNo,  
    p.Remarks AS PaymentRemarks,  
  
    -- ✅ New nullable field  
    r.Attachment AS ResignationAttachment  
  
FROM dbo.FNF_Header h  
LEFT JOIN dbo.tblEmployee e    ON e.EmployeeId = h.EmployeeId  
inner join dbo.tblDesignation desg on e.DesignationId = desg.DesignationId
inner join dbo.tblDepartment dept on e.DepartmentId = dept.DepartmentId
inner join dbo.tblLocation l on e.LocationId = l.LocationId
LEFT JOIN dbo.FNF_Additions a  ON a.FNFId = h.FNFId
inner JOIN dbo.FNF_Payment p ON p.FNFId = h.FNFId
OUTER APPLY
(
    SELECT TOP 1 *
    FROM EmpAttendanceViewSnapshot s
    WHERE s.Ecode = e.Ecode
      AND s.[Month-Year] = a.SalaryMonth
) sn
LEFT JOIN dbo.FNF_Deductions d ON d.FNFId = h.FNFId  
   
  
OUTER APPLY (  
    SELECT TOP 1 er.Attachment  
    FROM dbo.EmployeeResignationChecklistResponse er  
  WHERE TRY_CAST(er.EmployeeId AS BIGINT) = h.EmployeeId  
  AND er.Attachment IS NOT NULL  
    ORDER BY  
        ISNULL(er.LastUpdatedOn, er.CreatedOn) DESC,  
        er.EmployeeResignationChecklistResponseId DESC  
) r;
GO

-- -----------------------------------------------------------------------------
-- dbo.vw_FNF_AccountsList_Paid
-- -----------------------------------------------------------------------------
CREATE OR ALTER VIEW [dbo].[vw_FNF_AccountsList_Paid]
AS
SELECT
    h.FNFId,
    h.EmployeeId,
    e.Ecode,
    ISNULL(e.[FULL NAME], CONCAT(ISNULL(e.FirstName,''),' ',
                                 ISNULL(NULLIF(e.MiddleName,''),''),
                                 CASE WHEN ISNULL(e.LastName,'')<>'' THEN ' '+e.LastName ELSE '' END)) AS EmployeeName,
    desg.DesignationName,
    e.[PAN NO] as PanNo,
    e.[JOINING DATE] as JoiningDate,
    dept.DepartmentName,
    l.LocationName,
    l.STCode,
    e.[BANK NAME] as BankName,
    e.[A/C NO] as AccountNo,
    e.[BANK IFSC CODE] As IFSC,
	e.[GROSS SALARY],
	e.BasicSalary,

    sn.paybledays,
    sn.[Month-Year],
    sn.[PF(Total)] as PF,
    sn.[ESIC(Total)] as ESIC,
    sn.PTax,

    a.FNFDate,
    a.DateOfLeaving,

    -- Additions
    a.UnpaidSalaryAmount,
    a.Rate,
    a.Days,
    a.SalaryMonth,
    a.Bonus,
    a.BonusPeriodFrom,
    a.BonusPeriodTill,
    a.Gratuity,
    a.CalculatedAs,
    a.E_LeaveAmount,
    a.ELDays,
    a.NoticeSalary,
    a.OtherAddition1,
    a.OtherAddition2,
    a.OtherAddition3,
    a.OtherAddition4,

    -- Deductions
    d.LoanBalance,
    d.AdvanceBalance,
    d.OtherDeduction1,
    d.OtherDeduction2,
    d.OtherDeduction3,
    d.OtherDeduction4,
    d.TotalPayable,
    d.TDS,
    d.NetPayable,
    d.DepositOn,

    -- Totals
    CAST(ISNULL(a.UnpaidSalaryAmount,0)
       + ISNULL(a.Bonus,0)
       + ISNULL(a.Gratuity,0)
       + ISNULL(a.E_LeaveAmount,0)
       + ISNULL(a.NoticeSalary,0)
       + ISNULL(a.OtherAddition1,0)
       + ISNULL(a.OtherAddition2,0)
       + ISNULL(a.OtherAddition3,0)
       + ISNULL(a.OtherAddition4,0) AS decimal(18,2)) AS TotalAdditions,

    CAST(ISNULL(d.LoanBalance,0)
       + ISNULL(d.AdvanceBalance,0)
       + ISNULL(d.OtherDeduction1,0)
       + ISNULL(d.OtherDeduction2,0)
       + ISNULL(d.OtherDeduction3,0)
       + ISNULL(d.OtherDeduction4,0)
       + ISNULL(d.TDS,0) AS decimal(18,2)) AS TotalDeductions,

    CAST(
        (ISNULL(a.UnpaidSalaryAmount,0)+ISNULL(a.Bonus,0)+ISNULL(a.Gratuity,0)+ISNULL(a.E_LeaveAmount,0)+ISNULL(a.NoticeSalary,0)
         +ISNULL(a.OtherAddition1,0)+ISNULL(a.OtherAddition2,0)+ISNULL(a.OtherAddition3,0)+ISNULL(a.OtherAddition4,0))
        -
        (ISNULL(d.LoanBalance,0)+ISNULL(d.AdvanceBalance,0)+ISNULL(d.OtherDeduction1,0)+ISNULL(d.OtherDeduction2,0)
         +ISNULL(d.OtherDeduction3,0)+ISNULL(d.OtherDeduction4,0)+ISNULL(d.TDS,0))
        AS decimal(18,2)) AS NetAmount,

    -- No payment yet => these will be NULL
    CAST(p.SendForPaymentAmount AS decimal(18,2)) AS SendForPaymentAmount,
    CAST(p.AmountPaid AS decimal(18,2)) AS AmountPaid,
    CAST(p.Status AS varchar(50))   AS PaymentStatus,
    CAST(p.ChequeNo AS varchar(50))   AS ChequeNo,
    CAST(p.ChequeDate AS date)          AS ChequeDate,
    CAST(p.PaymentVoucherNo AS varchar(50))   AS PaymentVoucherNo,
    CAST(p.Remarks AS varchar(max))  AS PaymentRemarks,

    -- Resignation attachment (same logic)
    r.Attachment AS ResignationAttachment

FROM dbo.FNF_Header h
LEFT JOIN dbo.tblEmployee e		ON e.EmployeeId = h.EmployeeId
LEFT JOIN dbo.tblDesignation desg  ON e.DesignationId = desg.DesignationId
LEFT JOIN dbo.tblDepartment dept   ON e.DepartmentId = dept.DepartmentId
LEFT JOIN dbo.tblLocation l        ON e.LocationId = l.LocationId
INNER JOIN dbo.FNF_Additions a      ON a.FNFId = h.FNFId
INNER JOIN dbo.FNF_Deductions d		ON d.FNFId = h.FNFId
INNER JOIN dbo.FNF_Payment p		ON p.FNFId = h.FNFId

OUTER APPLY
(
    SELECT TOP 1 *
    FROM EmpAttendanceViewSnapshot s
    WHERE s.Ecode = e.Ecode
      AND s.[Month-Year] = a.SalaryMonth
) sn

OUTER APPLY
(
    SELECT TOP 1 er.Attachment
    FROM dbo.EmployeeResignationChecklistResponse er
    WHERE TRY_CAST(er.EmployeeId AS BIGINT) = h.EmployeeId
      AND er.Attachment IS NOT NULL
    ORDER BY
        ISNULL(er.LastUpdatedOn, er.CreatedOn) DESC,
        er.EmployeeResignationChecklistResponseId DESC
) r

WHERE
    NULLIF(LTRIM(RTRIM(e.Ecode)), '') IS NOT NULL
    and
    (NULLIF(LTRIM(RTRIM(p.ChequeNo)), '') IS NOT NULL
    OR NULLIF(LTRIM(RTRIM(p.PaymentVoucherNo)), '') IS NOT NULL);
GO

-- -----------------------------------------------------------------------------
-- dbo.vw_FNF_AccountsList_Unpaid
-- -----------------------------------------------------------------------------
CREATE OR ALTER VIEW [dbo].[vw_FNF_AccountsList_Unpaid]
AS
SELECT
    h.FNFId,
    h.EmployeeId,
    e.Ecode,
    ISNULL(e.[FULL NAME], CONCAT(ISNULL(e.FirstName,''),' ',
                                 ISNULL(NULLIF(e.MiddleName,''),''),
                                 CASE WHEN ISNULL(e.LastName,'')<>'' THEN ' '+e.LastName ELSE '' END)) AS EmployeeName,
    desg.DesignationName,
    e.[PAN NO] as PanNo,
    e.[JOINING DATE] as JoiningDate,
    dept.DepartmentName,
    l.LocationName,
    l.STCode,
    e.[BANK NAME] as BankName,
    e.[A/C NO] as AccountNo,
    e.[BANK IFSC CODE] As IFSC,
	e.[GROSS SALARY],
	e.BasicSalary,

    sn.paybledays,
    sn.[Month-Year],
    sn.[PF(Total)] as PF,
    sn.[ESIC(Total)] as ESIC,
    sn.PTax,

    a.FNFDate,
    a.DateOfLeaving,

    -- Additions
    a.UnpaidSalaryAmount,
    a.Rate,
    a.Days,
    a.SalaryMonth,
    a.Bonus,
    a.BonusPeriodFrom,
    a.BonusPeriodTill,
    a.Gratuity,
    a.CalculatedAs,
    a.E_LeaveAmount,
    a.ELDays,
    a.NoticeSalary,
    a.OtherAddition1,
    a.OtherAddition2,
    a.OtherAddition3,
    a.OtherAddition4,

    -- Deductions
    d.LoanBalance,
    d.AdvanceBalance,
    d.OtherDeduction1,
    d.OtherDeduction2,
    d.OtherDeduction3,
    d.OtherDeduction4,
    d.TotalPayable,
    d.TDS,
    d.NetPayable,
    d.DepositOn,

    -- Totals
    CAST(ISNULL(a.UnpaidSalaryAmount,0)
       + ISNULL(a.Bonus,0)
       + ISNULL(a.Gratuity,0)
       + ISNULL(a.E_LeaveAmount,0)
       + ISNULL(a.NoticeSalary,0)
       + ISNULL(a.OtherAddition1,0)
       + ISNULL(a.OtherAddition2,0)
       + ISNULL(a.OtherAddition3,0)
       + ISNULL(a.OtherAddition4,0) AS decimal(18,2)) AS TotalAdditions,

    CAST(ISNULL(d.LoanBalance,0)
       + ISNULL(d.AdvanceBalance,0)
       + ISNULL(d.OtherDeduction1,0)
       + ISNULL(d.OtherDeduction2,0)
       + ISNULL(d.OtherDeduction3,0)
       + ISNULL(d.OtherDeduction4,0)
       + ISNULL(d.TDS,0) AS decimal(18,2)) AS TotalDeductions,

    CAST(
        (ISNULL(a.UnpaidSalaryAmount,0)+ISNULL(a.Bonus,0)+ISNULL(a.Gratuity,0)+ISNULL(a.E_LeaveAmount,0)+ISNULL(a.NoticeSalary,0)
         +ISNULL(a.OtherAddition1,0)+ISNULL(a.OtherAddition2,0)+ISNULL(a.OtherAddition3,0)+ISNULL(a.OtherAddition4,0))
        -
        (ISNULL(d.LoanBalance,0)+ISNULL(d.AdvanceBalance,0)+ISNULL(d.OtherDeduction1,0)+ISNULL(d.OtherDeduction2,0)
         +ISNULL(d.OtherDeduction3,0)+ISNULL(d.OtherDeduction4,0)+ISNULL(d.TDS,0))
        AS decimal(18,2)) AS NetAmount,

    -- No payment yet => these will be NULL
    CAST(NULL AS decimal(18,2)) AS SendForPaymentAmount,
    CAST(NULL AS decimal(18,2)) AS AmountPaid,
    CAST(NULL AS varchar(50))   AS PaymentStatus,
    CAST(NULL AS varchar(50))   AS ChequeNo,
    CAST(NULL AS date)          AS ChequeDate,
    CAST(NULL AS varchar(50))   AS PaymentVoucherNo,
    CAST(NULL AS varchar(max))  AS PaymentRemarks,

    -- Resignation attachment (same logic)
    r.Attachment AS ResignationAttachment

FROM dbo.FNF_Header h
LEFT JOIN dbo.tblEmployee e    ON e.EmployeeId = h.EmployeeId
INNER JOIN dbo.tblDesignation desg ON e.DesignationId = desg.DesignationId
INNER JOIN dbo.tblDepartment dept  ON e.DepartmentId = dept.DepartmentId
INNER JOIN dbo.tblLocation l       ON e.LocationId = l.LocationId
LEFT JOIN dbo.FNF_Additions a      ON a.FNFId = h.FNFId
LEFT JOIN dbo.FNF_Payment p        ON p.FNFId = h.FNFId

OUTER APPLY
(
    SELECT TOP 1 *
    FROM EmpAttendanceViewSnapshot s
    WHERE s.Ecode = e.Ecode
      AND s.[Month-Year] = a.SalaryMonth
) sn

LEFT JOIN dbo.FNF_Deductions d ON d.FNFId = h.FNFId

OUTER APPLY
(
    SELECT TOP 1 er.Attachment
    FROM dbo.EmployeeResignationChecklistResponse er
    WHERE TRY_CAST(er.EmployeeId AS BIGINT) = h.EmployeeId
      AND er.Attachment IS NOT NULL
    ORDER BY
        ISNULL(er.LastUpdatedOn, er.CreatedOn) DESC,
        er.EmployeeResignationChecklistResponseId DESC
) r

WHERE p.FNFId IS NULL
   OR (
        ISNULL(LTRIM(RTRIM(p.ChequeNo)), '') = ''
    AND ISNULL(LTRIM(RTRIM(p.PaymentVoucherNo)), '') = ''
      )
GO

-- -----------------------------------------------------------------------------
-- dbo.sp_FNF_GetEmployeesByCode
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_FNF_GetEmployeesByCode]   
    @SearchEcode      NVARCHAR(50)  = NULL,  
    @TopRows          INT           = NULL,  -- backward compatibility (unused)  
    @GlobalSearch     NVARCHAR(100) = NULL,  
    @FromDate         DATETIME      = NULL,  
    @ToDate           DATETIME      = NULL,  
    @Page             INT           = 1,  
    @PageSize         INT           = 20000  
AS  
BEGIN  
    SET NOCOUNT ON;  
  
    IF @Page < 1 SET @Page = 1;  
  
    -- If you want "whole record", call with @PageSize = 0 (or < 1)  
    IF @PageSize IS NULL OR @PageSize < 1  
    BEGIN  
        SET @Page = 1;  
        SET @PageSize = 2147483647; -- return all rows  
    END  
  
    DECLARE @q NVARCHAR(50) = NULLIF(LTRIM(RTRIM(@SearchEcode)), '');  
    DECLARE @GlobalSearchQuery NVARCHAR(100) = NULLIF(LTRIM(RTRIM(@GlobalSearch)), '');  
  
    -- NOTE: now we are filtering by "EffectiveDateOfLeft" (last valid punch day) not tblEmployee.DateOfLeft  
    DECLARE @OneYearAgo DATE = DATEADD(YEAR, -1, GETDATE());  
  
    DECLARE @EffFromDate DATE = CASE WHEN @FromDate IS NULL THEN NULL ELSE CONVERT(DATE, @FromDate) END;  
    DECLARE @EffToDate   DATE = CASE WHEN @ToDate   IS NULL THEN NULL ELSE CONVERT(DATE, @ToDate)   END;  
  
    IF (@EffFromDate IS NOT NULL AND @EffToDate IS NOT NULL AND @EffFromDate > @EffToDate)  
    BEGIN  
        DECLARE @tmp DATE = @EffFromDate;  
        SET @EffFromDate = @EffToDate;  
        SET @EffToDate = @tmp;  
    END  
  
    CREATE TABLE #FilteredData (  
        EmployeeId BIGINT,  
        EmployeeCode NVARCHAR(20),  
        Name NVARCHAR(50),  
        Department NVARCHAR(255),  
        Designation NVARCHAR(255),  
  
        DOJ DATETIME2(0),  
  
        -- ✅ DateOfLeaving = last valid punch day (fallback to tblEmployee.DateOfLeft if no punch)  
        DateOfLeaving DATETIME2(0),  
  
        IsFNFCompleted BIT,  
        UnpaidSalaryAmount DECIMAL(18,2),  
        UnpaidSalaryDays INT,  
        UnpaidSalaryMonth NVARCHAR(50),  
        ResignationType NVARCHAR(50),  
        ResignationDate DATETIME2(0),  
        SeparationLastDay DATETIME2(0),  
        ManagerApproved BIT,  
        HRApproved BIT,  
        ResignationAttachment NVARCHAR(MAX),  
        RowNum INT  
    );  
  
    INSERT INTO #FilteredData  
    SELECT  
        e.EmployeeId,  
        e.Ecode AS EmployeeCode,  
        e.[FULL NAME] AS Name,  
        ISNULL(d.DepartmentName, '') AS Department,  
        ISNULL(g.DesignationName, '') AS Designation,  
  
        -- ✅ DOJ = COALESCE(DOJ, [JOINING DATE])  
        COALESCE(  
            TRY_CONVERT(DATETIME2(0), NULLIF(LTRIM(RTRIM(CONVERT(NVARCHAR(50), e.DOJ))), '')),  
            TRY_CONVERT(DATETIME2(0), NULLIF(LTRIM(RTRIM(CONVERT(NVARCHAR(50), e.[JOINING DATE]))), ''))  
        ) AS DOJ,  
  
        -- ✅ DateOfLeaving = Last Valid Punch Day (ignore bad rows), else fallback to tblEmployee.DateOfLeft  
        COALESCE(p.LastValidPunchDate, TRY_CONVERT(DATETIME2(0), e.[DateOfLeft])) AS DateOfLeaving,  
  
        ISNULL(e.IsFNFCompleted, 0) AS IsFNFCompleted,  
        0 AS UnpaidSalaryAmount,  
        0 AS UnpaidSalaryDays,  
        NULL AS UnpaidSalaryMonth,  
        ISNULL(rt.ResignationTypeName, '') AS ResignationType,  
        TRY_CONVERT(DATETIME2(0), ts.ResignationDate) AS ResignationDate,  
        TRY_CONVERT(DATETIME2(0), ts.LastDay)         AS SeparationLastDay,  
        ISNULL(ts.IsApprovedByManager, 0) AS ManagerApproved,  
        ISNULL(ts.IsApprovedByHR, 0)      AS HRApproved,  
        a.Attachment AS ResignationAttachment,  
  
        ROW_NUMBER() OVER (  
            ORDER BY  
                COALESCE(  
                    -- keep your old priority  
                    CONVERT(date, TRY_CONVERT(DATETIME2(0), ts.LastDay)),  
                    CONVERT(date, TRY_CONVERT(DATETIME2(0), ts.ResignationDate)),  
  
                    -- ✅ use effective leaving date (punch-based) for ordering  
                    CONVERT(date, COALESCE(p.LastValidPunchDate, TRY_CONVERT(DATETIME2(0), e.[DateOfLeft]))),  
  
                CONVERT(date, COALESCE(  
                        TRY_CONVERT(DATETIME2(0), NULLIF(LTRIM(RTRIM(CONVERT(NVARCHAR(50), e.DOJ))), '')),  
                        TRY_CONVERT(DATETIME2(0), NULLIF(LTRIM(RTRIM(CONVERT(NVARCHAR(50), e.[JOINING DATE]))), ''))  
                    )),  
                    CONVERT(date, '19000101')  
                ) DESC  
        ) AS RowNum  
    FROM dbo.tblEmployee e  
  
    -- ✅ Last punch day per employee (ONLY function/table you use in FNF SP)  
    OUTER APPLY  
    (  
        SELECT  
            MAX(x.AttendanceDate) AS LastValidPunchDate  
        FROM dbo.tbl_fn_GetMonthlyPunchesRange_productionnewnick_test x  
        WHERE x.ECode = e.Ecode  
          AND TRY_CAST(x.TotalWorkingMinutes AS time) >= '04:30'  -- ✅ ignore bad rows  
    ) p  
  
    LEFT JOIN dbo.tblEmployeeSepration ts ON ts.EmployeeId = e.EmployeeId  
    LEFT JOIN dbo.tblDepartment d ON d.DepartmentId = e.DepartmentId  
    LEFT JOIN dbo.tblDesignation g ON g.DesignationId = e.DesignationId  
    LEFT JOIN dbo.tblResignationType rt ON rt.ResignationTypeId = ts.ResignationTypeId  
    LEFT JOIN (  
        SELECT  
            er.EmployeeId,  
            MAX(er.Attachment) AS Attachment  
        FROM dbo.EmployeeResignationChecklistResponse er  
        WHERE er.Attachment IS NOT NULL  
        GROUP BY er.EmployeeId  
    ) a ON a.EmployeeId = e.EmployeeId  
    WHERE  
        ISNULL(e.IsStore, 0) = 0  
        AND ISNULL(e.IsActive, 0) = 0  
  
        -- ✅ filter based on effective leaving date (Punch date, else DateOfLeft)  
        AND COALESCE(p.LastValidPunchDate, TRY_CONVERT(DATETIME2(0), e.[DateOfLeft])) IS NOT NULL  
        AND CONVERT(date, COALESCE(p.LastValidPunchDate, TRY_CONVERT(DATETIME2(0), e.[DateOfLeft]))) >= @OneYearAgo  
  
        -- Optional range filter on effective leaving date  
        AND (  
            @EffFromDate IS NULL  
            OR CONVERT(date, COALESCE(p.LastValidPunchDate, TRY_CONVERT(DATETIME2(0), e.[DateOfLeft]))) >= @EffFromDate  
        )  
        AND (  
            @EffToDate IS NULL  
            OR CONVERT(date, COALESCE(p.LastValidPunchDate, TRY_CONVERT(DATETIME2(0), e.[DateOfLeft]))) <= @EffToDate  
        )

        AND NOT EXISTS
        (
            SELECT 1
            FROM dbo.FNF_Header fh
            WHERE fh.EmployeeId = e.EmployeeId
        )
  
        AND (@q IS NULL OR e.Ecode LIKE '%' + @q + '%')  
  
        AND (  
            @GlobalSearchQuery IS NULL OR  
            e.Ecode LIKE '%' + @GlobalSearchQuery + '%' OR  
            e.[FULL NAME] LIKE '%' + @GlobalSearchQuery + '%' OR  
            ISNULL(d.DepartmentName, '') LIKE '%' + @GlobalSearchQuery + '%' OR  
            ISNULL(g.DesignationName, '') LIKE '%' + @GlobalSearchQuery + '%' OR  
            ISNULL(rt.ResignationTypeName, '') LIKE '%' + @GlobalSearchQuery + '%'  
        );  
  
    SELECT COUNT(*) AS TotalCount FROM #FilteredData;  
  
    SELECT  
        EmployeeId,  
        EmployeeCode,  
        Name,  
        Department,  
        Designation,  
        DOJ,  
        DateOfLeaving,   -- ✅ now punch-based  
        IsFNFCompleted,  
        UnpaidSalaryAmount,  
        UnpaidSalaryDays,  
        UnpaidSalaryMonth,  
        ResignationType,  
        ResignationDate,  
        SeparationLastDay,  
        ManagerApproved,  
        HRApproved,  
        ResignationAttachment  
    FROM #FilteredData  
    WHERE RowNum BETWEEN (@Page - 1) * @PageSize + 1 AND @Page * @PageSize  
    ORDER BY RowNum DESC;  
  
    DROP TABLE #FilteredData;  
END
GO

-- -----------------------------------------------------------------------------
-- dbo.sp_FNF_GetEmployeesByCodeForExport
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_FNF_GetEmployeesByCodeForExport]
AS
BEGIN
    SET NOCOUNT ON;

    IF OBJECT_ID('tempdb..#FilteredData') IS NOT NULL
        DROP TABLE #FilteredData;

    -- Use DATETIME2 to avoid out-of-range errors (DATETIME has 1753 limitation)
    CREATE TABLE #FilteredData (
        EmployeeId BIGINT,
        EmployeeCode NVARCHAR(20),
        Name NVARCHAR(50),
        Department NVARCHAR(255),
        Designation NVARCHAR(255),

        -- ✅ Updated: joining + leaving aligned with your latest logic
        DateOfJoining DATETIME2(0),
        DateOfLeaving DATETIME2(0),   -- ✅ now = Last valid attendance date (fallback to DateOfLeft)

        IsFNFCompleted BIT,
        UnpaidSalaryAmount DECIMAL(18,2),
        UnpaidSalaryDays INT,
        UnpaidSalaryMonth NVARCHAR(50),
        ResignationType NVARCHAR(50),
        ResignationDate DATETIME2(0),
        SeparationLastDay DATETIME2(0),
        ManagerApproved BIT,
        HRApproved BIT,
        ResignationAttachment NVARCHAR(MAX),

        -- (optional internal, not selected outside)
        LastValidPunchDate DATE
    );

    ;WITH Attachments AS
    (
        SELECT
            er.EmployeeId,
            MAX(er.Attachment) AS Attachment
        FROM dbo.EmployeeResignationChecklistResponse er
        WHERE er.Attachment IS NOT NULL
        GROUP BY er.EmployeeId
    ),
    Emp AS
    (
        SELECT
            e.EmployeeId,
            e.Ecode,
            e.[FULL NAME] AS FullName,
            e.DepartmentId,
            e.DesignationId,
            e.IsFNFCompleted,
            e.IsStore,
            e.IsActive,
            e.[DateOfLeft],

            -- Safely parse both potential joining fields (same as your logic)
            TRY_CONVERT(DATETIME2(0), NULLIF(LTRIM(RTRIM(CONVERT(NVARCHAR(50), e.DOJ))), ''))            AS DOJ_Parsed,
            TRY_CONVERT(DATETIME2(0), NULLIF(LTRIM(RTRIM(CONVERT(NVARCHAR(50), e.[JOINING DATE]))), '')) AS JoiningDate_Parsed
        FROM dbo.tblEmployee e
    )
    INSERT INTO #FilteredData
    (
        EmployeeId, EmployeeCode, Name, Department, Designation,
        DateOfJoining, DateOfLeaving, IsFNFCompleted,
        UnpaidSalaryAmount, UnpaidSalaryDays, UnpaidSalaryMonth,
        ResignationType, ResignationDate, SeparationLastDay,
        ManagerApproved, HRApproved, ResignationAttachment,
        LastValidPunchDate
    )
    SELECT
        e.EmployeeId,
        e.Ecode AS EmployeeCode,
        e.FullName AS Name,
        ISNULL(d.DepartmentName, '') AS Department,
        ISNULL(g.DesignationName, '') AS Designation,

        -- ✅ DOJ: Prefer DOJ, else [JOINING DATE] (keep production-safe parsing)
        COALESCE(
            NULLIF(CONVERT(date, e.DOJ_Parsed), '1900-01-01'),
            NULLIF(CONVERT(date, e.JoiningDate_Parsed), '1900-01-01')
        ) AS DateOfJoining,

        -- ✅ DateOfLeaving: Last valid attendance day (ignore bad rows), else fallback to tblEmployee.DateOfLeft
        CONVERT(DATETIME2(0), COALESCE(p.LastValidPunchDate, TRY_CONVERT(date, e.[DateOfLeft]))) AS DateOfLeaving,

        ISNULL(e.IsFNFCompleted, 0) AS IsFNFCompleted,
        0 AS UnpaidSalaryAmount,
        0 AS UnpaidSalaryDays,
        NULL AS UnpaidSalaryMonth,

        ISNULL(rt.ResignationTypeName, '') AS ResignationType,
        TRY_CONVERT(DATETIME2(0), ts.ResignationDate) AS ResignationDate,
        TRY_CONVERT(DATETIME2(0), ts.LastDay) AS SeparationLastDay,

        ISNULL(ts.IsApprovedByManager, 0) AS ManagerApproved,
        ISNULL(ts.IsApprovedByHR, 0) AS HRApproved,

        a.Attachment AS ResignationAttachment,

        p.LastValidPunchDate
    FROM Emp e

    -- ✅ ONLY table/function you are using in production for punch logic
    OUTER APPLY
    (
        SELECT MAX(x.AttendanceDate) AS LastValidPunchDate
        FROM dbo.tbl_fn_GetMonthlyPunchesRange_productionnewnick_test x
        WHERE x.ECode = e.Ecode
          AND TRY_CAST(x.TotalWorkingMinutes AS time) >= '04:30'  -- ✅ ignore bad rows
    ) p

    LEFT JOIN dbo.tblEmployeeSepration ts ON ts.EmployeeId = e.EmployeeId
    LEFT JOIN dbo.tblDepartment d ON d.DepartmentId = e.DepartmentId
    LEFT JOIN dbo.tblDesignation g ON g.DesignationId = e.DesignationId
    LEFT JOIN dbo.tblResignationType rt ON rt.ResignationTypeId = ts.ResignationTypeId
    LEFT JOIN Attachments a ON a.EmployeeId = e.EmployeeId
    WHERE
        ISNULL(e.IsStore, 0) <> 1
        AND ISNULL(e.IsActive, 0) = 0

        -- ✅ must have a leaving date by your NEW definition:
        -- punch date (preferred) OR DateOfLeft (fallback)
        AND COALESCE(p.LastValidPunchDate, TRY_CONVERT(date, e.[DateOfLeft])) IS NOT NULL;

    -- Final output ordered by your priority (unchanged)
    SELECT
        EmployeeId,
        EmployeeCode,
        Name,
        Department,
        Designation,
        DateOfJoining,
        DateOfLeaving,
        IsFNFCompleted,
        UnpaidSalaryAmount,
        UnpaidSalaryDays,
        UnpaidSalaryMonth,
        ResignationType,
        ResignationDate,
        SeparationLastDay,
        ManagerApproved,
        HRApproved,
        ResignationAttachment
    FROM #FilteredData
    ORDER BY
        COALESCE(
            NULLIF(CONVERT(date, SeparationLastDay), '0001-01-01'),
            NULLIF(CONVERT(date, ResignationDate),    '0001-01-01'),
            NULLIF(CONVERT(date, DateOfLeaving),      '0001-01-01'),
            NULLIF(CONVERT(date, DateOfJoining),      '0001-01-01'),
            CONVERT(date, '19000101')
        ) DESC;

    DROP TABLE #FilteredData;
END
GO

-- -----------------------------------------------------------------------------
-- dbo.sp_FNF_GetFnfDetailsByEcode
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_FNF_GetFnfDetailsByEcode]  
(  
    @Ecode NVARCHAR(50)  
)  
AS  
BEGIN  
    SET NOCOUNT ON;  
  
    DECLARE  
        @JoiningDate DATE,  
        @LastPunchMonthDate DATE,  
        @LastValidAttendanceDate DATE,  
        @LastPunchMonth NVARCHAR(10),  
        @LastPunchMonthDays DECIMAL(18,2) = 0,  
        @DaysInMonth INT = 0,  
        @BasicSal DECIMAL(18,2) = 0,  
        @GrossSalary DECIMAL(18,2) = 0,  
        @Rate DECIMAL(18,2) = 0,  
  
        @ELDays DECIMAL(18,2) = 0,  
        @ELAmount DECIMAL(18,2) = 0,  
  
        @UnpaidAmount DECIMAL(18,2) = 0,  
  
        @FinalBonus DECIMAL(18,2) = 0,  
        @BonusStartMonth NVARCHAR(10),  
        @BonusEndMonth NVARCHAR(10),  
        @BonusRemarks NVARCHAR(200),  
  
        @YearsServed DECIMAL(10,2) = 0,  
        @GratuityAmount DECIMAL(18,2) = 0,  
        @Remarks NVARCHAR(200) = NULL,  
  
        @EmployeeId BIGINT,  
  
        -- ✅ EL month = last punch month - 1  
        @ELMonthKey NVARCHAR(10),  
        @ELMonthDate DATE;  
  
    /* ================= EMPLOYEE ================= */  
    SELECT  
        @EmployeeId = e.EmployeeId,  
        @JoiningDate = CONVERT(date, COALESCE(  
            TRY_CONVERT(datetime2(0), e.DOJ),  
            TRY_CONVERT(datetime2(0), e.[JOINING DATE])  
        )),  
        @BasicSal = ISNULL(e.BasicSalary,0),  
        @GrossSalary = ISNULL(e.[GROSS SALARY],0)  
    FROM dbo.tblEmployee e  
    WHERE e.Ecode = @Ecode;  
  
    /* ================= LAST VALID ATTENDANCE DATE (THIS IS YOUR LASTDAY) ================= */  
    SELECT  
        @LastValidAttendanceDate = MAX(AttendanceDate)  
    FROM dbo.tbl_fn_GetMonthlyPunchesRange_productionnewnick_test  
    WHERE ECode = @Ecode  
      AND TRY_CAST(TotalWorkingMinutes AS time) >= '04:30';  
  
    IF @LastValidAttendanceDate IS NOT NULL  
    BEGIN  
        SET @LastPunchMonthDate = DATEFROMPARTS(  
            YEAR(@LastValidAttendanceDate),  
            MONTH(@LastValidAttendanceDate),  
            1  
        );  
  
        SET @LastPunchMonth = FORMAT(@LastPunchMonthDate,'MMM-yy');  
        SET @DaysInMonth = DAY(EOMONTH(@LastPunchMonthDate));  
  
        /* ================= LAST PUNCH MONTH DAYS ================= */  
        BEGIN TRY  
            SELECT  
              @LastPunchMonthDays = ISNULL(SUM(  
                  CASE  
                      WHEN TRY_CAST(TotalWorkingMinutes AS time) >= '08:30' THEN 1.0  
                      WHEN TRY_CAST(TotalWorkingMinutes AS time) >= '04:30' THEN 0.5  
                      ELSE 0.0  
                  END  
              ), 0.0)  
            FROM dbo.tbl_fn_GetMonthlyPunchesRange_productionnewnick_test  
            WHERE ECode = @Ecode  
              AND AttendanceDate >= @LastPunchMonthDate  
              AND AttendanceDate <  DATEADD(MONTH, 1, @LastPunchMonthDate)  
              AND TRY_CAST(TotalWorkingMinutes AS time) >= '04:30';  
        END TRY  
        BEGIN CATCH  
            SET @LastPunchMonthDays = 0;  
        END CATCH  
  
        SET @Rate = @GrossSalary / NULLIF(@DaysInMonth,0);  
  
        /* ================= EL (From EmpLeaveClosingBalance, Month = LastPunchMonth - 1) ================= */  
        SET @ELMonthDate = DATEADD(MONTH, -1, @LastPunchMonthDate);  
        SET @ELMonthKey  = FORMAT(@ELMonthDate, 'MMM-yy');  
  
        SELECT  
            @ELDays = ISNULL([EL Closing], 0)  
        FROM dbo.EmpLeaveClosingBalance  
        WHERE ECODE = @Ecode  
          AND [MONTH] = @ELMonthKey;  
  
        SET @ELAmount = (@BasicSal / 30.0) * @ELDays;  
  
        /* ================= YEARS SERVED & GRATUITY ================= */  
        DECLARE @YearsRaw DECIMAL(10,4) =  
            CASE  
                WHEN @JoiningDate IS NULL THEN 0  
                ELSE DATEDIFF(DAY, @JoiningDate, @LastPunchMonthDate) / 365.0  
            END;  
  
        SET @YearsServed = FLOOR(@YearsRaw);  
  
        IF @YearsServed >= 5  
            SET @GratuityAmount = (@BasicSal * 15.0 / 26.0) * @YearsServed;  
        ELSE  
            SET @GratuityAmount = 0;  
  
        /* ================= UNPAID AMOUNT ================= */  
        DECLARE  
            @UnpaidMonthDate DATE = DATEADD(MONTH,-1,@LastPunchMonthDate),  
            @UnpaidMonthKey NVARCHAR(10);  
  
        SET @UnpaidMonthKey = FORMAT(@UnpaidMonthDate,'MMM-yy');  
  
        /* ----------------------------------------------------------------
           OLD LOGIC (COMMENTED) - Unpaid was coming from attendance view
        ----------------------------------------------------------------- */  
        /*
        DECLARE  
            @UnpaidMonthStart DATE,  
            @UnpaidMonthEnd DATE,  
            @UnpaidMonthKey NVARCHAR(10);  
  
        SET @UnpaidMonthStart = DATEFROMPARTS(YEAR(@UnpaidMonthDate), MONTH(@UnpaidMonthDate), 1);  
        SET @UnpaidMonthEnd   = EOMONTH(@UnpaidMonthStart);  
        SET @UnpaidMonthKey   = FORMAT(@UnpaidMonthDate,'MMM-yy');  
  
        IF NOT EXISTS  
        (  
            SELECT 1  
            FROM dbo.tblPaidByBank  
            WHERE Ecode = @Ecode  
              AND [Date] >= @UnpaidMonthStart  
              AND [Date] <= @UnpaidMonthEnd  
        )  
        BEGIN  
            SELECT  
                @UnpaidAmount = ISNULL([Monthly Gross CTC(Actual After Deduction AND AddONS)],0)  
            FROM dbo.vw_Emp_Attendance_Format  
            WHERE Ecode = @Ecode  
              AND [Month-Year] = @UnpaidMonthKey;  
        END  
        */
  
        /* ----------------------------------------------------------------
           NEW LOGIC - Using GivenToBank and PaidByBank (production safe)
           UnpaidAmount = SUM(GivenToBank.BankTransfer) - SUM(PaidByBank.BankTransfer)
           NOTE: BankTransfer is VARCHAR in prod, so convert safely.
        ----------------------------------------------------------------- */  
        DECLARE @GivenAmount DECIMAL(18,2) = 0;  
        DECLARE @PaidAmount  DECIMAL(18,2) = 0;  
  
        SELECT
            @GivenAmount =
                ISNULL(SUM(
                    ISNULL(
                        TRY_CONVERT(DECIMAL(18,2), NULLIF(LTRIM(RTRIM(g.BankTransfer)), '')),
                        0
                    )
                ), 0)
        FROM dbo.GivenToBank g
        WHERE g.Ecode = @Ecode
          AND g.[Month] = @UnpaidMonthKey
          AND ISNULL(g.IsActive,1) = 1
          AND ISNULL(g.IsDeleted,0) = 0;

        SELECT
            @PaidAmount =
                ISNULL(SUM(
                    ISNULL(
                        TRY_CONVERT(DECIMAL(18,2), NULLIF(LTRIM(RTRIM(p.BankTransfer)), '')),
                        0
                    )
                ), 0)
        FROM dbo.PaidByBank p
        WHERE p.Ecode = @Ecode
          AND p.[Month] = @UnpaidMonthKey
          AND ISNULL(p.IsActive,1) = 1
          AND ISNULL(p.IsDeleted,0) = 0;
  
        SET @UnpaidAmount = (@GivenAmount - @PaidAmount);  
    END  
    ELSE  
    BEGIN  
        SET @Remarks = 'No valid attendance found';  
        SET @LastPunchMonth = NULL;  
        SET @LastPunchMonthDays = 0;  
        SET @Rate = 0;  
        SET @YearsServed = 0;  
        SET @GratuityAmount = 0;  
        SET @ELDays = 0;  
        SET @ELAmount = 0;  
        SET @ELMonthKey = NULL;  
        SET @UnpaidAmount = 0;  
    END  
  
    /* ================= BONUS ================= */  
    DECLARE @B TABLE  
    (  
        Ecode NVARCHAR(20),  
        BonusStartMonth NVARCHAR(10),  
        BonusEndMonth NVARCHAR(10),  
        FinalBonus DECIMAL(18,2),  
        Remarks NVARCHAR(200)  
    );  
  
    BEGIN TRY  
        IF TRY_CAST(@Ecode AS BIGINT) IS NOT NULL  
        BEGIN  
            INSERT INTO @B  
            EXEC dbo.usp_GetEmployeeFinalBonus @Ecode;  
        END  
        ELSE  
        BEGIN  
            INSERT INTO @B (Ecode, FinalBonus, Remarks)  
            VALUES (@Ecode, 0, 'Skipped: Ecode non-numeric, incompatible with Bonus SP');  
        END  
    END TRY  
    BEGIN CATCH  
        INSERT INTO @B (Ecode, FinalBonus, Remarks)  
        VALUES (@Ecode, 0, 'Error in Bonus SP: ' + ERROR_MESSAGE());  
    END CATCH  
  
    SELECT  
        @FinalBonus = FinalBonus,  
        @BonusStartMonth = BonusStartMonth,  
        @BonusEndMonth = BonusEndMonth,  
        @BonusRemarks = Remarks  
    FROM @B;  
  
    /* ================= FINAL OUTPUT ================= */  
    SELECT TOP 1  
        e.Ecode,  
        e.[FULL NAME] AS EmployeeName,  
  
        CONVERT(date, COALESCE(  
            TRY_CONVERT(datetime2(0), e.DOJ),  
            TRY_CONVERT(datetime2(0), e.[JOINING DATE])  
        )) AS DOJ,  
  
        @LastValidAttendanceDate AS LastDay,  
        CAST(0 AS INT) AS NoticePeriod,  
  
        rt.ResignationTypeName,  
        es.ResignationDate,  
        ISNULL(@Remarks, es.Remarks) AS Remarks,  
  
        @LastPunchMonth AS LastPunchMonth,  
        @LastPunchMonthDays AS LastPunchMonthDays,  
        @Rate AS Rate,  
  
        @ELDays AS EarnedLeaveDays,  
        @ELAmount AS EarnedLeaveAmount,  
  
        @UnpaidAmount AS UnpaidAmount,  
  
        @FinalBonus AS FinalBonus,  
        @BonusStartMonth AS BonusStartMonth,  
        @BonusEndMonth AS BonusEndMonth,  
        @BonusRemarks AS BonusRemarks,  
  
        @YearsServed AS YearsServed,  
        @GratuityAmount AS GratuityAmount,  
  
        r.Attachment AS ResignationAttachment  
    FROM dbo.tblEmployee e  
    LEFT JOIN dbo.tblEmployeeSepration es ON es.EmployeeId = e.EmployeeId  
    LEFT JOIN dbo.tblResignationType rt ON rt.ResignationTypeId = es.ResignationTypeId  
    OUTER APPLY  
    (  
        SELECT TOP 1 er.Attachment  
        FROM dbo.EmployeeResignationChecklistResponse er  
        WHERE TRY_CAST(er.EmployeeId AS BIGINT) IS NOT NULL  
          AND TRY_CAST(er.EmployeeId AS BIGINT) = e.EmployeeId  
          AND er.Attachment IS NOT NULL  
        ORDER BY  
            ISNULL(er.LastUpdatedOn, er.CreatedOn) DESC,  
            er.EmployeeResignationChecklistResponseId DESC  
    ) r  
    WHERE e.Ecode = @Ecode;  
END
GO

-- -----------------------------------------------------------------------------
-- dbo.sp_FNF_GetFnfDetailsByEcodeByGautam
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_FNF_GetFnfDetailsByEcodeByGautam]  
(  
    @Ecode NVARCHAR(50)  
)  
AS  
BEGIN  
    SET NOCOUNT ON;  
    SET XACT_ABORT ON;  
  
    BEGIN TRY  
  
    /* =========================================================  
       DECLARATIONS  
    ========================================================= */  
    DECLARE   
        @EmployeeId BIGINT,  
        @JoiningDate DATE,  
        @LastValidAttendanceDate DATE,  
        @LastPunchMonthDate DATE,  
        @LastPunchMonth NVARCHAR(10),  
        @DaysInMonth INT = 0,  
  
        @BasicSal DECIMAL(18,2) = 0,  
        @GrossSalary DECIMAL(18,2) = 0,  
        @Rate DECIMAL(18,2) = 0,  
        @LastPunchMonthDays DECIMAL(18,2) = 0,  
  
        @ELMonthDate DATE,  
        @ELMonthKey NVARCHAR(10),  
        @ELDays DECIMAL(18,2) = 0,  
        @ELAmount DECIMAL(18,2) = 0,  
  
        @UnpaidMonthDate DATE,  
        @UnpaidMonthKey NVARCHAR(10),  
        @UnpaidAmount DECIMAL(18,2) = 0,  
        @GivenAmount DECIMAL(18,2) = 0,  
        @PaidAmount DECIMAL(18,2) = 0,  
  
        @YearsServed INT = 0,  
        @GratuityAmount DECIMAL(18,2) = 0,  
  
        @FinalBonus DECIMAL(18,2) = 0,  
        @BonusStartMonth NVARCHAR(10),  
        @BonusEndMonth NVARCHAR(10),  
        @BonusEndDate DATE,  
        @FinancialStartDate DATE,  
        @BonusRemarks NVARCHAR(200),  
  
        @Remarks NVARCHAR(200) = NULL,  
        @FinalBonus2 DECIMAL(18,2) = 0, 
        @UTRExists BIT = 0;
  
  
    /* =========================================================  
       EMPLOYEE DETAILS  
    ========================================================= */  
    SELECT  
        @EmployeeId = e.EmployeeId,  
        @JoiningDate = CONVERT(date, COALESCE(  
                                TRY_CONVERT(datetime2, e.DOJ),  
                                TRY_CONVERT(datetime2, e.[JOINING DATE])  
                           )),  
        @BasicSal = ISNULL(e.BasicSalary,0),  
        @GrossSalary = ISNULL(e.[GROSS SALARY],0)  
    FROM dbo.tblEmployee e  
    WHERE e.Ecode = @Ecode;  
  
    IF @EmployeeId IS NULL  
    BEGIN  
        RAISERROR('Invalid Ecode',16,1);  
        RETURN;  
    END  
  
  
    /* =========================================================  
       LAST VALID ATTENDANCE  
    ========================================================= */  
    SELECT   
        @LastValidAttendanceDate = MAX(AttendanceDate)  
    FROM dbo.tbl_fn_GetMonthlyPunchesRange_productionnewnick_test  
    WHERE ECode = @Ecode  
      AND TRY_CAST(TotalWorkingMinutes AS time) >= '04:30';  
  
    IF @LastValidAttendanceDate IS NOT NULL  
    BEGIN  
        SET @LastPunchMonthDate = DATEFROMPARTS(  
                                        YEAR(@LastValidAttendanceDate),  
                                        MONTH(@LastValidAttendanceDate),  
                                        1);  
  
        SET @LastPunchMonth = FORMAT(@LastPunchMonthDate,'MMM-yy');  
        SET @DaysInMonth = DAY(EOMONTH(@LastPunchMonthDate));  
  
        SELECT   
            @LastPunchMonthDays = ISNULL(SUM(  
                CASE  
                    WHEN TRY_CAST(TotalWorkingMinutes AS time) >= '08:30' THEN 1  
                    WHEN TRY_CAST(TotalWorkingMinutes AS time) >= '04:30' THEN 0.5  
                    ELSE 0  
                END  
            ),0)  
        FROM dbo.tbl_fn_GetMonthlyPunchesRange_productionnewnick_test  
        WHERE ECode = @Ecode  
          AND AttendanceDate >= @LastPunchMonthDate  
          AND AttendanceDate < DATEADD(MONTH,1,@LastPunchMonthDate)  
          AND TRY_CAST(TotalWorkingMinutes AS time) >= '04:30';  
  
        SET @Rate = @GrossSalary / NULLIF(@DaysInMonth,0);  
  
        /* EL */  
        SET @ELMonthDate = DATEADD(MONTH,-1,@LastPunchMonthDate);  
        SET @ELMonthKey = FORMAT(@ELMonthDate,'MMM-yy');  
  
        SELECT @ELDays = ISNULL([EL Closing],0)  
        FROM dbo.EmpLeaveClosingBalance  
        WHERE Ecode = @Ecode  
          AND [MONTH] = @ELMonthKey;  
  
        SET @ELAmount = (@BasicSal / 30.0) * @ELDays;  
  
        /* Gratuity */  
        SET @YearsServed =  
            FLOOR(DATEDIFF(DAY,@JoiningDate,@LastPunchMonthDate)/365.0);  
  
        IF @YearsServed >= 5  
            SET @GratuityAmount = (@BasicSal * 15.0 / 26.0) * @YearsServed;  
  
        /* Unpaid */  
        SET @UnpaidMonthDate = DATEADD(MONTH,-1,@LastPunchMonthDate);  
        SET @UnpaidMonthKey = FORMAT(@UnpaidMonthDate,'MMM-yy');  
  
        SELECT @GivenAmount = ISNULL(SUM(  
                TRY_CONVERT(decimal(18,2),g.BankTransfer)),0)  
        FROM dbo.GivenToBank g  
        WHERE g.Ecode = @Ecode  
          AND g.[Month] = @UnpaidMonthKey  
          AND ISNULL(g.IsActive,1)=1  
          AND ISNULL(g.IsDeleted,0)=0;  
  
        SELECT @PaidAmount = ISNULL(SUM(  
                TRY_CONVERT(decimal(18,2),p.BankTransfer)),0)  
        FROM dbo.PaidByBank p  
        WHERE p.Ecode = @Ecode  
          AND p.[Month] = @UnpaidMonthKey  
          AND ISNULL(p.IsActive,1)=1  
          AND ISNULL(p.IsDeleted,0)=0;  
  
        SET @UnpaidAmount = @GivenAmount - @PaidAmount;  
    END  
    ELSE  
    BEGIN  
        SET @Remarks = 'No valid attendance found';  
    END  
  
  
    /* =========================================================  
       BONUS SECTION (UTR RULE APPLIED)  
    ========================================================= */  
  
    DECLARE @B TABLE  
    (  
        Ecode NVARCHAR(20),  
        BonusStartMonth NVARCHAR(10),  
        BonusEndMonth NVARCHAR(10),  
        FinalBonus DECIMAL(18,2),  
        Remarks NVARCHAR(200)  
    );  
  
    INSERT INTO @B  
    (  
        Ecode,  
        BonusStartMonth,  
        BonusEndMonth,  
        FinalBonus,  
        Remarks  
    )  
    EXEC dbo.usp_GetEmployeeFinalBonus @Ecode;  
  
    SELECT   
        @BonusStartMonth = BonusStartMonth,  
        @BonusEndMonth   = BonusEndMonth,  
        @BonusRemarks    = Remarks
    FROM @B; 
    
    SELECT
    @FinalBonus2 = ISNULL(SUM(TRY_CONVERT(DECIMAL(18,2), ActualBonus)), 0)
FROM BonusAndGratutityOpening
WHERE Ecode = @Ecode
  AND TRY_CONVERT(date, '01-' + [Month], 106)
      BETWEEN DATEFROMPARTS(YEAR(GETDATE()) - 1, 10, 1)
          AND EOMONTH(GETDATE());
  
    SET @BonusEndDate = TRY_CONVERT(date,'01-'+@BonusEndMonth,106);  
  
    IF EXISTS  
    (  
        SELECT 1  
        FROM dbo.tblBonus_Upload  
        WHERE E_Code = @Ecode  
          AND ISNULL(isactive,1)=1  
          AND ISNULL(isdeleted,0)=0  
          AND UTR IS NOT NULL  
          AND LTRIM(RTRIM(UTR)) <> ''  
    )  
        SET @UTRExists = 1;  
  
    IF @UTRExists = 1 AND @BonusEndDate IS NOT NULL  
    BEGIN  
        SET @FinancialStartDate =  
            DATEFROMPARTS(  
                CASE   
                    WHEN MONTH(@BonusEndDate) >= 10  
                        THEN YEAR(@BonusEndDate)  
                    ELSE YEAR(@BonusEndDate) - 1  
                END,  
                10,  
                1  
            );  
  
        SELECT  
            @FinalBonus = ISNULL(SUM(ISNULL(ActualBonus,0)),0)  
        FROM dbo.BonusAndGratutityOpening  
        WHERE ECode = @Ecode  
          AND TRY_CONVERT(date,'01-'+[Month],106)  
                BETWEEN @FinancialStartDate AND @BonusEndDate;  
    END  
    ELSE  
    BEGIN  
        SELECT  
            @FinalBonus = ISNULL(SUM(ISNULL(ActualBonus,0)),0)  
        FROM dbo.BonusAndGratutityOpening  
        WHERE ECode = @Ecode;  
    END  
  
  
    /* =========================================================  
       FINAL OUTPUT (EXACT SAME STRUCTURE)  
    ========================================================= */  
    SELECT TOP 1      
        e.Ecode,      
        e.[FULL NAME] AS EmployeeName,      
        @JoiningDate AS DOJ,      
        @LastValidAttendanceDate AS LastDay,      
        CAST(0 AS DECIMAL(18,2)) AS NoticePeriod,  
        rt.ResignationTypeName,  
        es.ResignationDate,  
        ISNULL(@Remarks, es.Remarks) AS Remarks,  
        @LastPunchMonth AS LastPunchMonth,  
        CAST(@LastPunchMonthDays AS DECIMAL(18,2)) AS LastPunchMonthDays,  
        CAST(@Rate AS DECIMAL(18,2)) AS Rate,  
        CAST(@ELDays AS DECIMAL(18,2)) AS EarnedLeaveDays,  
        CAST(@ELAmount AS DECIMAL(18,2)) AS EarnedLeaveAmount,  
        CAST(@UnpaidAmount AS DECIMAL(18,2)) AS UnpaidAmount,  
        CAST(@FinalBonus2 AS DECIMAL(18,2)) AS FinalBonus,
        @BonusStartMonth AS BonusStartMonth,  
@BonusEndMonth AS BonusEndMonth,  
        @BonusRemarks AS BonusRemarks,  
        CAST(@YearsServed AS DECIMAL(18,2)) AS YearsServed,  
        CAST(@GratuityAmount AS DECIMAL(18,2)) AS GratuityAmount,  
        r.Attachment AS ResignationAttachment  
    FROM dbo.tblEmployee e      
    LEFT JOIN dbo.tblEmployeeSepration es   
        ON es.EmployeeId = e.EmployeeId      
    LEFT JOIN dbo.tblResignationType rt   
        ON rt.ResignationTypeId = es.ResignationTypeId      
    OUTER APPLY      
    (      
        SELECT TOP 1 er.Attachment      
        FROM dbo.EmployeeResignationChecklistResponse er      
        WHERE TRY_CAST(er.EmployeeId AS BIGINT) = e.EmployeeId      
          AND er.Attachment IS NOT NULL      
        ORDER BY ISNULL(er.LastUpdatedOn, er.CreatedOn) DESC  
    ) r      
    WHERE e.Ecode = @Ecode;  
  
    END TRY  
    BEGIN CATCH  
        SELECT ERROR_NUMBER() AS ErrorNumber,  
               ERROR_MESSAGE() AS ErrorMessage;  
    END CATCH  
END
GO

-- -----------------------------------------------------------------------------
-- dbo.sp_FnfPendingToProcessing
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_FnfPendingToProcessing]      
    @EmployeeId BIGINT = NULL      
AS      
BEGIN      
    SET NOCOUNT ON;      
      
    BEGIN TRY      

        -- Validate EmployeeId
        IF @EmployeeId IS NULL
        BEGIN
            THROW 50001, 'EmployeeId cannot be NULL.', 1;
        END

        INSERT INTO [dbo].[FNF_Processing] (EmployeeId)
        VALUES (@EmployeeId);
      
        -- If the query is successful, return success message      
        SELECT 1 AS Success, 'Employee moved to processing successfully.' AS Message;      
    
    END TRY      
    BEGIN CATCH      
        -- Capture the error message      
        SELECT 0 AS Success, ERROR_MESSAGE() AS Message;      
    END CATCH      
END
GO

-- -----------------------------------------------------------------------------
-- dbo.sp_ReportFnfMultipleRequest
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_ReportFnfMultipleRequest]
AS
BEGIN
    SET NOCOUNT ON;
    ;WITH cte AS (
        SELECT
        fh.Employeeid,
        count(*) as request_count 
        FROM 
            FNF_Header fh 
        GROUP BY 
            fh.EmployeeId 
        HAVING count(*) > 1
    )
    SELECT
        e.EmployeeId,
        e.Ecode,
        e.[FULL NAME] AS EmployeeName,
        e.ESICNO,
        e.GENDER,
        e.DOJ,
        e.MOBILE,
        e.[EMAIL ADDRESS],
        d.DepartmentName,
        de.DesignationName,
        e.ReportHeadEcode,
        rh.[FULL NAME] AS ReportingHeadName,
        s.ShiftName,
        l.LocationName,
        stat.StateName
    FROM 
        cte 
    LEFT JOIN 
        tblEmployee e 
    ON 
        cte.EmployeeId=e.EmployeeId

    LEFT JOIN tblEmployee rh
    ON rh.Ecode = e.ReportHeadEcode

    INNER JOIN tblDepartment d
    ON e.DepartmentId = d.DepartmentId

    INNER JOIN tblDesignation de
        ON de.DesignationId = e.DesignationId

    INNER JOIN tblShiftMaster s
        ON s.ShiftID = e.ShiftID

    INNER JOIN tblLocation l
        ON l.LocationId = e.LocationId

    INNER JOIN tblState stat
        ON stat.StateId = l.StateId
END
GO

-- -----------------------------------------------------------------------------
-- dbo.SaveFNFPaymentData
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.SaveFNFPaymentData
    @FNFId BIGINT = NULL,
    @SendForPaymentAmount DECIMAL = NULL,
    @Remarks NVARCHAR(500) = NULL,
    @ChequeNo NVARCHAR(50) = NULL,
    @ChequeDate DATE = NULL,
    @Status NVARCHAR(50) = NULL,
    @AmountPaid DECIMAL = NULL,
    @PaymentVoucherNo NVARCHAR(50) = NULL,
    @CreatedBy NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- Validate FNFId
    IF @FNFId IS NULL
    BEGIN
        THROW 50001, 'FNFId cannot be NULL.', 1;
        RETURN;
    END

    BEGIN TRY
        INSERT INTO dbo.FNF_Payment
        (
            FNFId,
            SendForPaymentAmount,
            Remarks,
            ChequeNo,
            ChequeDate,
            Status,
            AmountPaid,
            PaymentVoucherNo,
            CreatedBy
        )
        VALUES
        (
            @FNFId,
            @SendForPaymentAmount,
            @Remarks,
            @ChequeNo,
            @ChequeDate,
            @Status,
            @AmountPaid,
            @PaymentVoucherNo,
            @CreatedBy
        );

        SELECT 1 AS Success, 'Insert completed' AS Message;
    END TRY
    BEGIN CATCH
        SELECT 
            0 AS Success,
            ERROR_MESSAGE() AS Message;
    END CATCH
END
GO

-- -----------------------------------------------------------------------------
-- dbo.usp_GetFNFDetailsByCreatedOnAman
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.usp_GetFNFDetailsByCreatedOnAman
    @CreatedOn DATE
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        emp.Ecode, 
        emp.[FULL NAME],
        fa.E_LeaveAmount,
        fa.UnpaidSalaryAmount,
        fa.ELDays, 
        fa.Gratuity, 
        fa.Bonus,
        fa.Rate, 
        fd.NetPayable,
        eh.ValidFrom AS [Date Of Leaving],
        fa.DateOfLeaving AS LastPunchDate,
        emp.DOJ,
        DATEDIFF(DAY, emp.DOJ, fa.DateOfLeaving) AS [Tenure(Days)], 
        fa.Days AS [Payable Days],
        emp.[GROSS SALARY],
        emp.BasicSalary,
        sn.[EarnedLeaveBalance],
        sn.[paybledays]
    FROM FNF_Header fh
    JOIN tblEmployee emp 
        ON emp.EmployeeId = fh.EmployeeId 
    LEFT JOIN FNF_Additions fa 
        ON fa.FNFId = fh.FNFId 
    LEFT JOIN FNF_Deductions fd 
        ON fh.FNFId = fd.FNFId
    OUTER APPLY (
        SELECT TOP 1 
            eh.ValidFrom
        FROM tblEmployee_History eh
        WHERE eh.EmployeeId = emp.EmployeeId 
          AND eh.IsActive = 0
        ORDER BY eh.CreatedOn DESC
    ) eh
    OUTER APPLY (
        SELECT TOP (1) 
            s.*
        FROM dbo.EmpAttendanceViewSnapshot s WITH (NOLOCK)
        WHERE s.Ecode = emp.Ecode
          AND s.[Month] = FORMAT(fa.DateOfLeaving, 'MMM-yy')
        ORDER BY s.ID DESC
    ) sn
    WHERE fh.CreatedOn >= @CreatedOn;
END;
GO

-- -----------------------------------------------------------------------------
-- dbo.usp_UpdateFNFPaymentStatus
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[usp_UpdateFNFPaymentStatus]
    @FNFId BIGINT,
    @Status NVARCHAR(50),
    @Remarks NVARCHAR(500)
AS
BEGIN
    SET NOCOUNT ON;

    -- Validate status
    IF (@Status NOT IN ('Pending', 'Transfered', 'Rejected'))
    BEGIN
        RAISERROR('Invalid Status value. Allowed values: PENDING, Transfered, Rejected.', 16, 1);
        RETURN;
    END

    UPDATE [dbo].[FNF_Payment]
    SET 
        [Status] = @Status,
        [Remarks] = @Remarks
    WHERE [FNFId] = @FNFId;
END
GO

PRINT '<< Done:     STEP 6 / 9 -- FNF -- file: SPs_FNF.sql';
GO


-- #############################################################################
-- STEP 7 / 9 -- SuperAdmin Regularize Export -- file: SPs_Regularize.sql
-- #############################################################################
PRINT '>> Applying: STEP 7 / 9 -- usp_GetAttendanceRegularizationByRange';
GO

-- -----------------------------------------------------------------------------
-- dbo.usp_GetAttendanceRegularizationByRange
-- SuperAdmin export: filter by date range and optional status filters.
-- @Status          -> overall request StatusName  (Approved / Pending / Rejected)
-- @ManagerStatus   -> manager approval StatusName (Approved / Pending / Rejected)
-- @LpStatus        -> LP approval StatusName      (Approved / Pending / Rejected)
-- Combine @ManagerStatus='Approved' + @LpStatus='Pending' to get
-- "Approved by Manager, Pending by LP".
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.usp_GetAttendanceRegularizationByRange
    @StartDate     DATE,
    @EndDate       DATE,
    @Status        VARCHAR(50) = NULL,
    @ManagerStatus VARCHAR(50) = NULL,
    @LpStatus      VARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        b.Ecode,
        COALESCE(b.[FULL NAME], b.FirstName + b.MiddleName + b.LastName) AS EmpName,
        h.STCode, h.LocationName,
        i.DepartmentName, j.DesignationName,
        a.[RequestDate],
        a.[Reason],
        f.Ecode AS RM_ECODE,
        COALESCE(f.[FULL NAME], f.FirstName + f.MiddleName + f.LastName) AS ReportManagerName,
        a.[PunchIn],
        a.[PunchOut],
        c.StatusName,
        a.[FileUrl],
        a.[PunchTypeId],
        g.RequestTypeName,
        a.[EmployeeRemarks],
        d.StatusName AS ManagerStatus,
        a.[ManagerApprovalOn],
        a.[ManagerRemarks],
        e.StatusName AS [LpApprovalStatus],
        a.[LpApprovalOn],
        a.[LpRemarks]
    FROM tblAttendanceRegularizationRequest a
    LEFT JOIN tblEmployee b      ON a.EmployeeId = b.EmployeeId
    LEFT JOIN tblLocation h      ON h.LocationId = b.LocationId
    LEFT JOIN tblStatus c        ON c.StatusId = a.StatusId
    LEFT JOIN tblStatus d        ON d.StatusId = a.ManagerApprovalStatusId
    LEFT JOIN tblStatus e        ON e.StatusId = a.LpApprovalStatusId
    LEFT JOIN tblEmployee f      ON f.EmployeeId = a.ReportingManagerId
    LEFT JOIN tblRequestTypes g  ON a.RequestTypeId = g.RequestTypeId
    LEFT JOIN tblDepartment i    ON b.DepartmentId = i.DepartmentId
    LEFT JOIN tblDesignation j   ON b.DesignationId = j.DesignationId
    WHERE
        a.RequestDate >= @StartDate
        AND a.RequestDate <= @EndDate
        AND (@Status        IS NULL OR @Status        = '' OR c.StatusName = @Status)
        AND (@ManagerStatus IS NULL OR @ManagerStatus = '' OR d.StatusName = @ManagerStatus)
        AND (@LpStatus      IS NULL OR @LpStatus      = '' OR e.StatusName = @LpStatus)
    ORDER BY a.RequestDate, b.Ecode;
END
GO

PRINT '<< Done:     STEP 7 / 9 -- usp_GetAttendanceRegularizationByRange';
GO


-- #############################################################################
-- STEP 8 / 9 -- SuperAdmin Geofence Export -- file: SPs_GeoAttendance.sql
-- #############################################################################
PRINT '>> Applying: STEP 8 / 9 -- usp_GetGeoAttendanceByRange';
GO

-- -----------------------------------------------------------------------------
-- dbo.usp_GetGeoAttendanceByRange
-- SuperAdmin export: geofence/geo-attendance approvals for a date range with
-- optional status filters.
--   @FinalStatus    -> tblStatus.StatusName for FinalStatusId
--   @ManagerStatus  -> tblStatus.StatusName for ManagerApprovalStatusId
--   @MasterStatus   -> tblStatus.StatusName for MasterApprovalStatusId
-- One row per (Employee, PunchDate). Includes punch counts and approval trail.
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.usp_GetGeoAttendanceByRange
    @StartDate     DATE,
    @EndDate       DATE,
    @FinalStatus   VARCHAR(50) = NULL,
    @ManagerStatus VARCHAR(50) = NULL,
    @MasterStatus  VARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH PunchAgg AS
    (
        SELECT
            ar.EmployeeId,
            CONVERT(DATE, ar.PunchTimeUtc) AS PunchDate,
            COUNT(*)                                          AS PunchCount,
            SUM(CASE WHEN ar.PunchType = 1 THEN 1 ELSE 0 END) AS PunchInCount,
            SUM(CASE WHEN ar.PunchType = 2 THEN 1 ELSE 0 END) AS PunchOutCount,
            MIN(ar.PunchTimeUtc)                              AS FirstPunchUtc,
            MAX(ar.PunchTimeUtc)                              AS LastPunchUtc
        FROM dbo.AttendanceRecord ar
        WHERE CONVERT(DATE, ar.PunchTimeUtc) BETWEEN @StartDate AND @EndDate
        GROUP BY ar.EmployeeId, CONVERT(DATE, ar.PunchTimeUtc)
    )
    SELECT
        e.Ecode,
        COALESCE(e.[FULL NAME],
                 NULLIF(LTRIM(RTRIM(
                    ISNULL(e.FirstName, N'') + N' ' + ISNULL(e.LastName, N'')
                 )), N''),
                 N'Unknown') AS EmployeeName,
        d.DepartmentName,
        des.DesignationName,
        loc.LocationName,
        loc.STCode,
        rh.Ecode             AS ReportingManagerEcode,
        COALESCE(rh.[FULL NAME],
                 NULLIF(LTRIM(RTRIM(
                    ISNULL(rh.FirstName, N'') + N' ' + ISNULL(rh.LastName, N'')
                 )), N''),
                 N'') AS ReportingManagerName,
        pa.PunchDate,
        pa.PunchCount,
        pa.PunchInCount,
        pa.PunchOutCount,
        pa.FirstPunchUtc,
        pa.LastPunchUtc,
        sm.StatusName  AS ManagerStatus,
        ga.ManagerApproverId,
        ga.ManagerApprovalOn,
        ga.ManagerRemarks,
        sms.StatusName AS MasterStatus,
        ga.MasterApproverId,
        ga.MasterApprovalOn,
        ga.MasterRemarks,
        sf.StatusName  AS FinalStatus
    FROM PunchAgg pa
    INNER JOIN dbo.tblEmployee e        ON e.EmployeeId = pa.EmployeeId
    LEFT JOIN dbo.GeoAttendanceApproval ga
        ON ga.EmployeeId = pa.EmployeeId AND ga.PunchDate = pa.PunchDate
    LEFT JOIN dbo.tblStatus sm          ON sm.StatusId = ga.ManagerApprovalStatusId
    LEFT JOIN dbo.tblStatus sms         ON sms.StatusId = ga.MasterApprovalStatusId
    LEFT JOIN dbo.tblStatus sf          ON sf.StatusId = ga.FinalStatusId
    LEFT JOIN dbo.tblDepartment d       ON d.DepartmentId = e.DepartmentId
    LEFT JOIN dbo.tblDesignation des    ON des.DesignationId = e.DesignationId
    LEFT JOIN dbo.tblLocation loc       ON loc.LocationId = e.LocationId
    LEFT JOIN dbo.tblEmployee rh        ON rh.Ecode = e.ReportheadEcode
    WHERE
        (@FinalStatus   IS NULL OR @FinalStatus   = '' OR sf.StatusName  = @FinalStatus)
        AND (@ManagerStatus IS NULL OR @ManagerStatus = '' OR sm.StatusName  = @ManagerStatus)
        AND (@MasterStatus  IS NULL OR @MasterStatus  = '' OR sms.StatusName = @MasterStatus)
    ORDER BY pa.PunchDate DESC, e.Ecode;
END
GO

PRINT '<< Done:     STEP 8 / 9 -- usp_GetGeoAttendanceByRange';
GO


-- #############################################################################
-- STEP 9 / 9 -- Applicant List (adds StatusName column) -- file: SPs_ApplicantList.sql
-- #############################################################################
PRINT '>> Applying: STEP 9 / 9 -- sp_GetApplicantListNew01 (adds StatusName)';
GO

-- -----------------------------------------------------------------------------
-- dbo.sp_GetApplicantListNew01
-- Adds StatusName column (joined from tblStatus) so the exported Excel shows
-- the human-readable applicant status. The existing StatusId column is
-- preserved for backward compatibility with consumers that already use it.
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_GetApplicantListNew01
(
    @PageNumber INT = 1,
    @PageSize INT = 10,
    @StatusId INT = 0,
    @SearchTerm NVARCHAR(200) = NULL,
    @RoleId INT = NULL,
    @EmployeeId INT = NULL
)
AS
BEGIN
    SET NOCOUNT ON;
    SET ARITHABORT ON;

    DECLARE @Offset INT = (@PageNumber - 1) * @PageSize;
    SET @SearchTerm = LTRIM(RTRIM(ISNULL(@SearchTerm, '')));

    ---------------------------------------------------------
    -- HEADER
    ---------------------------------------------------------
    SELECT
        COUNT(*) AS TotalRecords,
        SUM(CASE WHEN StatusId = 4 THEN 1 ELSE 0 END) AS PendingCount
    FROM Candidate
    WHERE IsApplicant = 1
      AND IsActive = 1
      AND IsDeleted = 0
      AND (@StatusId = 0 OR StatusId = @StatusId);

    ---------------------------------------------------------
    -- PAGED RESULT
    ---------------------------------------------------------
    ;WITH BaseData AS
    (
        SELECT *
        FROM Candidate c
        WHERE
            c.IsApplicant = 1
            AND c.IsActive = 1
            AND c.IsDeleted = 0
            AND (@StatusId = 0 OR c.StatusId = @StatusId)
            AND (
                @SearchTerm = '' OR
                c.[FIRST NAME] LIKE '%' + @SearchTerm + '%' OR
                c.[MIDDLE NAME] LIKE '%' + @SearchTerm + '%' OR
                c.[LAST NAME] LIKE '%' + @SearchTerm + '%' OR
                c.[EMP CODE] LIKE '%' + @SearchTerm + '%' OR
                c.MOBILE LIKE '%' + @SearchTerm + '%'
            )
    )

    SELECT
        c.Id AS ID,
        c.[FIRST NAME] AS FirstName,
        c.[MIDDLE NAME] AS MiddleName,
        c.[LAST NAME] AS LastName,
        c.MOBILE AS Phone,
        c.[EMAIL ADDRESS] AS Email,
        CASE WHEN c.StatusId = 2 THEN 1 ELSE 0 END AS IsReopenAllowed,
        c.DESIGNATION AS Designation,
        c.DOB,
        c.StatusId,
        s.StatusName AS Status,
        d.DesignationName,
        CONCAT(l.STCode,'-',l.LocationName) AS LocationName,
        c.[POSITION HELD IN PREVIOUS COMPANY] AS PositionHeldInPreviousCompany,
        c.[EMP CODE] AS ApplicantCode,
        c.IsApplicant,

        docs.ResumeLink,
        docs.OfferLetterLink,

        ir.InterviewRounds,
        ir.Type,
        ir.CurrentRound,
        ir.LastInterviewDateTime,
        ir.LastScheduleId,
        ir.FinalResult,
        ir.IsStatus,

        c.IsActive,
        c.IsDeleted,
        cr.[FULL NAME] + ' (' + cr.Ecode + ')' AS CreatedBy,
        up.[FULL NAME] + ' (' + up.Ecode + ')' AS UpdatedBy,
        c.CreatedOn,
        c.UpdatedOn,
        c.CreatedOn AS DateOfApply,

        c.[WORK LOCATION],
        c.[APPLICANT CODE],
        c.[COMPANY 1],
        c.[COMPANY 2],
        c.[COMPANY 3],
        c.[In Hand Salary],
        c.[LAST CTC(ANNUAL)],

        e.TotalIndustryExperience_yrs,
        e.TotalRetailExperience_yrs,

        c.CurrentLocation,
        c.PreferredLocation,
        c.StateId,
        st.StateName,
        c.NoticePeriod

    FROM BaseData c

    LEFT JOIN tblDesignation d
        ON TRY_CAST(c.DESIGNATION AS INT) = d.DesignationId

    LEFT JOIN tblLocation l
        ON TRY_CAST(c.LOCATION AS INT) = l.LocationId

    LEFT JOIN tblExperience e
        ON e.CID = c.Id

    LEFT JOIN tblEmployee cr
        ON TRY_CAST(c.CreatedBy AS INT) = cr.EmployeeId

    LEFT JOIN tblEmployee up
        ON TRY_CAST(c.UpdatedBy AS INT) = up.EmployeeId

    LEFT JOIN StateMasterWithMinWages st
        ON c.StateId = st.Id

    LEFT JOIN tblStatus s
        ON s.StatusId = c.StatusId

    OUTER APPLY
    (
        SELECT
            MAX(CASE WHEN FileType='Resume' THEN FilePath END) AS ResumeLink,
            MAX(CASE WHEN FileType='OfferLetter' THEN FilePath END) AS OfferLetterLink
        FROM CanidateDocs
        WHERE CId = c.Id AND IsDeleted = 0
    ) docs

    OUTER APPLY
    (
        SELECT
            (
                SELECT r.RoundId,
                       r.ScheduleId,
                       s.InterviewLocation,
                       s.InterviewDateTime,
                       ISNULL(r.Status,'') AS Status
                FROM tblInterviewRounds r
                JOIN tblScheduleInterview s
                    ON r.ScheduleId = s.ScheduleId
                WHERE s.ApplicantId = c.Id
                  AND s.IsActive = 1
                  AND s.IsDeleted = 0
                FOR JSON PATH
            ) AS InterviewRounds,

            (SELECT TOP 1 s.InterviewLocation
             FROM tblScheduleInterview s
             WHERE s.ApplicantId = c.Id
             ORDER BY s.ScheduleId DESC) AS Type,

            (SELECT ISNULL(MAX(r.RoundId),0)
             FROM tblInterviewRounds r
             JOIN tblScheduleInterview s
                ON r.ScheduleId = s.ScheduleId
             WHERE s.ApplicantId = c.Id) AS CurrentRound,

            (SELECT TOP 1 CONVERT(VARCHAR(19),s.InterviewDateTime,120)
             FROM tblScheduleInterview s
             WHERE s.ApplicantId = c.Id
             ORDER BY s.InterviewDateTime DESC) AS LastInterviewDateTime,

            (SELECT TOP 1 s.ScheduleId
             FROM tblScheduleInterview s
             WHERE s.ApplicantId = c.Id
             ORDER BY s.ScheduleId DESC) AS LastScheduleId,

            '' AS FinalResult,
            0 AS IsStatus
    ) ir

    ORDER BY c.Id DESC
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY

    OPTION (RECOMPILE);
END
GO

PRINT '<< Done:     STEP 9 / 9 -- sp_GetApplicantListNew01 (with StatusName)';
GO


-- =============================================================================
-- POST-APPLY VERIFICATION (run after this script):
-- =============================================================================
PRINT 'Verifying object presence...';
GO
SELECT s.name + '.' + o.name AS [Object], o.type_desc, o.modify_date
FROM sys.objects o JOIN sys.schemas s ON s.schema_id = o.schema_id
WHERE o.name IN (
    'sp_GetRegularizeRequests','sp_GetRegularizeRequestsBulk','usp_GetAttendanceRegularization',
    'usp_GetAttendanceRegularizationByRange','usp_GetGeoAttendanceByRange','sp_GetApplicantListNew01',
    'sp_GetEmployeeEffectiveLeavingDate',
    'sp_CalculateEmployeePayroll','sp_CalculateEmployeePayroll_PT_LWF_Dev','sp_GetPayrollSummary',
    'usp_ProcessBonusAndPayments','usp_ProcessBonusAndPayments_MonthWise_Dev','usp_ExportEmployeeBonusGratuity',
    'USP_GENERATE_EMP_GRATUITY_BONUS','usp_GetEmployeeFinalBonus','usp_GetEmployeeBonus','GETEMPBONUSLIST',
    'usp_ProcessRetentionBonus','vw_Bonus_Gratuity',
    'sp_Report_InactiveEmployees_NoDuesNotSubmitted','sp_ReportInactiveEmployeesWithFNF',
    'sp_ReportActiveInEmpMasterinActiveHRMS','sp_ReportActiveInHRMSinActiveEmpMaster',
    'sp_ReportNoResignationApprovalStillInactive','sp_ReportInactiveStillWorking',
    'sp_GetInactiveEmployees_LastPunch_LastUpdate','sp_GetInactiveEmployeesWithLastPunch',
    'sp_FNF_BulkUpload','sp_FNF_GetAccountsList','sp_FNF_GetAccountsList_Paid','sp_FNF_GetAccountsList_Unpaid',
    'vw_FNF_AccountsList','vw_FNF_AccountsList_Paid','vw_FNF_AccountsList_Unpaid',
    'sp_FNF_GetEmployeesByCode','sp_FNF_GetEmployeesByCodeForExport',
    'sp_FNF_GetFnfDetailsByEcode','sp_FNF_GetFnfDetailsByEcodeByGautam',
    'sp_FnfPendingToProcessing','sp_ReportFnfMultipleRequest',
    'SaveFNFPaymentData','usp_GetFNFDetailsByCreatedOnAman','usp_UpdateFNFPaymentStatus'
)
ORDER BY o.modify_date DESC;
GO

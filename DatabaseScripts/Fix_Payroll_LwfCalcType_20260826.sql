

-- -----------------------------------------------------------------------------
-- dbo.sp_CalculateEmployeePayroll_PT_LWF_Dev
-- -----------------------------------------------------------------------------
-- [sp_CalculateEmployeePayroll] '52398','14','Jun-25','50000.00','4'                                                                      
ALTER   PROCEDURE [dbo].[sp_CalculateEmployeePayroll_PT_LWF_Dev] @EmployeeId INT,             
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
---- If not in override  apply salary rule                                                  
--UPDATE tblEmployee                                                  
--SET ESICApplicable = CASE WHEN MonthlyGrossCTC <= 21000 THEN 1 ELSE 0 END                                                  
--WHERE Ecode = @Ecode                                                  
--  AND Ecode NOT IN (SELECT Ecode FROM EcodesForWhichNoPFNoESIC);                                                  
---- If in override  force ESIC = 0                                                  
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
---- If no row exists  treat both as NOT allowed                                                    
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

  /* FALLBACK (added 2026-08-25): StoreStateLinking is a legacy table that the
     Store-State mapping page does NOT maintain - that page writes
     tblLocation.StateId. A store mapped only through the UI (e.g. DM03
     GWALIOR-HUB, StateId 269 = MADHYA PRADESH) resolved to NULL here, so PTax,
     LWF and every other state-driven deduction was silently skipped. */
  IF @State IS NULL
     SELECT @State = s.StateName
     FROM tblLocation l WITH (NOLOCK)
     JOIN tblState  s WITH (NOLOCK) ON s.StateId = l.StateId
     WHERE l.STCode = @LocationCategoryId;
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
-- 4. LWP  EXTRA DAYS ADJUSTMENT                                                      
--    (APPLICABLE ONLY WHEN LOCATION ? 'HO')                                                      
Declare @LocationType nvarchar(50);            
Select             
  @LocationType = c.LocationTypeName             
from             
  tblEmployee (NOLOCK) a             
  Left Join tblLocation (NOLOCK) b on a.LocationId = b.LocationId             
  Left Join tblLocationType (NOLOCK) c on b.LocationType = c.Id             
where             
  Ecode = @Ecode IF (@LocationType <> 'HO') BEGIN IF (@ExtraDaysFinal > @LWP) BEGIN -- ExtraDays > LWP  reduce ExtraDays, make LWP = 0                                                      
Set             
  @AdjustedDays =(@AbsentDays - @UsedLeave);            
SET             
  @PayableDaysFinal = CASE WHEN ISNULL((@PayableDaysFinal + (@AbsentDays - @UsedLeave)), 0) < @tBudgetMonthDays THEN ISNULL((@PayableDaysFinal + (@AbsentDays - @UsedLeave)), 0) ELSE @tBudgetMonthDays END    ;        
SET             
  @ExtraDaysFinal = @ExtraDaysFinal - @LWP;            
SET             
  @LWP = 0;            
Print('Not HO, LWP less') END ELSE BEGIN -- LWP > ExtraDays  reduce LWP, ExtraDays = 0             
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
PRINT('Changed') PRINT('@AdjustedDays') Print(@AdjustedDays) PRINT('@PayableDaysFinal') Print(@PayableDaysFinal) PRINT('@LWP') Print(@LWP) PRINT('@ExtraDaysFinal') Print(@ExtraDaysFinal) 
/* ---------------------------------------------------------------------------
   FIX 2026-08-26: this INSERT was UNGUARDED. On any re-run of a month the
   employee already had a tbl_calculatePaybledays row, so a SECOND row was
   created. The MERGE into tbl_Month_salary further below builds its source
   from this table joined on ecode+month, so two rows became two source rows
   for one target row and the MERGE threw:
       "The MERGE statement attempted to UPDATE or DELETE the same row
        more than once."
   The CATCH at the end of this proc swallowed that error, so the salary run
   reported SUCCESS while silently skipping everything after the MERGE --
   including the PTax lookup, LWF, and all the deduction writes. That is why
   the first run of a month produced PTax but every re-run produced zero.

   Refresh the existing row instead of appending a new one. No row is ever
   deleted; an existing row is updated in place, and only a genuinely new
   ecode/month gets an INSERT.
   --------------------------------------------------------------------------- */
IF EXISTS (
  SELECT 1 FROM tbl_calculatePaybledays
  WHERE Ecode = @Ecode AND [Month] = @Month
)
  UPDATE tbl_calculatePaybledays
  SET
    [Year]             = @Year,
    LocationCategoryId = 1,
    DesignationId      = @DesignationId,
    EmployeeName       = @EmployeeName,
    EmployeeId         = @EmployeeId,
    Attendance         = @Attendance - ISNULL(@WeekdaysHolidayCount, 0) - ISNULL(@FinalSatHolidayCount, 0),
    weeklyoffpresent   = isnull(@ExtraDays, 0),
    Actual_Weekly_Off  = isnull(@WeeklyOff, 0),
    leave_availed      = CASE WHEN @IsNAPS = 1 THEN 0 ELSE (
                           SELECT ISNULL((
                             SELECT TOP 1 Used
                             FROM tblEmployeeLeaveBalance b
                             WHERE b.EmployeeId = @EmployeeId AND MONTH = @Month
                           ), 0)
                         ) END,
    InitialPaybledays  = isnull(@BaseValue, 0),
    TotalDaysInMonth   = @BudgetMonthDays,
    Payble_Days        = @PayableDaysFinal,
    EXTRA_DAYS         = @ExtraDaysFinal,
    ExtraDaysUsed      = ISNULL(@ExtraDaysUsed, 0),
    Absent             = isnull(@AbsentDays, 0),
    [Status]           = @IsActive,
    WeekHolidays       = ISNULL(@WeekdaysHolidayCount, 0),
    SatHolidays        = ISNULL(@FinalSatHolidayCount, 0),
    NC_Attendance      = ISNULL(@NC_Attendance, 0),
    LWP                = ISNULL(@LWP, 0),
    AdjustedDays       = ISNULL(@AdjustedDays, 0)
  WHERE Ecode = @Ecode AND [Month] = @Month
ELSE
  INSERT into tbl_calculatePaybledays (            
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
  /* PTax slab base = gross ACTUALLY PAYABLE, not fixed components only.
     V49362: fixed 25,000.00 + extra-day 403.23 = 25,403.23; Jharkhand nil
     band ends at 25,000, so he was slabbed one band low (0 instead of 100).
     Added 2026-08-25.

     CORRECTED 2026-08-26 -- the 2026-08-25 version was
         + try_cast(ISNULL(Incentive, 0) as decimal(18, 2))
     which is WRONG for these columns. Extra_day_allowence / Incentive / Arrers
     are VARCHAR, and the MERGE above writes Incentive and Arrers as EMPTY
     STRINGS (the CTE selects '' AS Incentive, '' AS Arrears). ISNULL('' , 0)
     returns '' (not NULL, because '' is not NULL), and try_cast('' as decimal)
     returns NULL -- which made the WHOLE sum NULL. A NULL slab base matches no
     PT slab, so @PTax stayed NULL and EVERY employee got zero PTax.
     NULLIF(...,'') turns the empty string into NULL first, then ISNULL(...,0)
     collapses it to zero, so a blank column contributes 0 instead of poisoning
     the total. Overtime is already decimal, so it only needs the outer ISNULL. */
  @MonthGrossSalaryEmp =
    ISNULL(try_cast(monthlyGrossCTC as decimal(18, 2)), 0)
  + ISNULL(try_cast(NULLIF(LTRIM(RTRIM(Extra_day_allowence)), '') as decimal(18, 2)), 0)
  + ISNULL(try_cast(NULLIF(LTRIM(RTRIM(Incentive)),           '') as decimal(18, 2)), 0)
  + ISNULL(try_cast(NULLIF(LTRIM(RTRIM(Arrers)),              '') as decimal(18, 2)), 0)
  + ISNULL(try_cast(Overtime as decimal(18, 2)), 0)
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
  /* LWF is driven entirely by the LWF policy page (LWFPolicyMaster). No state is
     named in this proc.

     CalcType (added 2026-08-26) says HOW to read the Employee / Employeer column:

       'Percent' -> the column is a PERCENTAGE of earned gross, capped by the Max.
                    Haryana: 0.2% of gross capped at Rs 35 (employee),
                             0.4% of gross capped at Rs 70 (employer),
                    so the cap starts biting at Rs 17,500 of gross:
                        10,000 -> 20 / 40      17,500 -> 35 / 70
                        15,000 -> 30 / 60      40,000 -> 35 / 70 (capped)

       'Flat'    -> the column is a FLAT rupee amount, capped by the Max when set.
                    Chandigarh 5/20, Goa 10/30, Punjab 5/20.

     History: the percentage maths existed originally but was hardcoded as
     CASE WHEN State = 'Haryana'. Removing that (2026-08-25) made Haryana flat and
     it deducted Rs 0.20; replacing 0.200 with a flat 35 (2026-08-26) then
     OVER-deducted everyone earning under Rs 17,500. CalcType restores the correct
     maths while keeping the policy page as the single source of truth -- a new
     percentage state now needs only a page edit, not a proc change.

     The result is divided by Frequency to reach a monthly figure. */
  @Lwf = MAX(
    CASE WHEN CalcType = 'Percent' THEN
           CASE WHEN EmployeeMax IS NULL
                     THEN (ISNULL(Employee, 0) / 100.0) * ISNULL(@MonthGrossSalaryEmp, 0)
                WHEN (ISNULL(Employee, 0) / 100.0) * ISNULL(@MonthGrossSalaryEmp, 0) > EmployeeMax
                     THEN EmployeeMax
                ELSE (ISNULL(Employee, 0) / 100.0) * ISNULL(@MonthGrossSalaryEmp, 0)
           END
         ELSE
           CASE WHEN EmployeeMax IS NULL THEN ISNULL(Employee, 0)
                WHEN ISNULL(Employee, 0) > EmployeeMax THEN EmployeeMax
                ELSE ISNULL(Employee, 0)
           END
    END / CASE WHEN Frequency = 'Monthly' THEN 1 WHEN Frequency = 'Half-yearly' THEN 6 WHEN Frequency = 'Yearly' THEN 12 ELSE 1 END
  ),
  @LwfEmployeer = MAX(
    CASE WHEN CalcType = 'Percent' THEN
           CASE WHEN EmployeerMax IS NULL
                     THEN (ISNULL(Employeer, 0) / 100.0) * ISNULL(@MonthGrossSalaryEmp, 0)
                WHEN (ISNULL(Employeer, 0) / 100.0) * ISNULL(@MonthGrossSalaryEmp, 0) > EmployeerMax
                     THEN EmployeerMax
                ELSE (ISNULL(Employeer, 0) / 100.0) * ISNULL(@MonthGrossSalaryEmp, 0)
           END
         ELSE
           CASE WHEN EmployeerMax IS NULL THEN ISNULL(Employeer, 0)
                WHEN ISNULL(Employeer, 0) > EmployeerMax THEN EmployeerMax
                ELSE ISNULL(Employeer, 0)
           END
    END / CASE WHEN Frequency = 'Monthly' THEN 1 WHEN Frequency = 'Half-yearly' THEN 6 WHEN Frequency = 'Yearly' THEN 12 ELSE 1 END
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




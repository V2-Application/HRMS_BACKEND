-- [sp_CalculateEmployeePayroll] '52398','14','Jun-25','50000.00','4'              
CREATE   PROCEDURE [dbo].[sp_CalculateEmployeePayroll_PT_LWF]               
    @EmployeeId INT,              
    @Attendance Decimal(18,2), 
    @NC_Attendance Decimal(18,2), 
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
 DECLARE @WeeklyOffBud DECIMAL(18,2);  
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
 Declare @BgtTotalWeekOffs int;  
 Declare @INCENTIVE Decimal(18,2);      
 Declare @ARREAR Decimal(18,2);      
 Declare @OVERTIME Decimal(18,2);      
 Declare @FOODINGALLOWANCE Decimal(18,2);      
 Declare @MOBILEBILL Decimal(18,2);      
 Declare @State nvarchar(100);  
 Declare @MonthNo INT;        
 Declare @UsedLeaves decimal(18,2);  
 Declare @LocationCategoryId nvarchar(100);     
 Declare @LocationCategoryType nvarchar(100);  
 Declare @IsExtraDaysApplicable bit=0;      
 Declare @BasicSalary decimal(18,2),@DOJ datetime,@GrossEarnings decimal(18,2),@DOL datetime,@IsBonusApplicable bit,@BasicSalaryCalc decimal(18,2);    
  
 Declare @WeekdaysHolidayCount int=0;  
 Declare @FinalSatHolidayCount int=0;  
 Declare @IsStore bit;  
  
 Set @ExtraDays=(case when (  
   Select   
  1  
   from tblEmployee a  
  Left join tblLocation b on a.LocationId=b.LocationId  
  where b.STCode NOT IN ('RH01','RD04') and b.STCode Not LIKE 'D%' and a.EmployeeId=@EmployeeId)  
  =1 then 0  
  else @ExtraDays  
  end);  
  
  print(@ExtraDays)  
  
  
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
              DECLARE @satCount INT;  
    SELECT @satCount = dbo.SatCountFromMonth(@Month);  
              
    -- Get WeeklyOff from tblLocationDesignationPolicy              
 Select @Ecode=Ecode,@LocationCategoryType=d.LocationTypeName, @LocationCategoryId= b.STCode,@DesignationId= c.DesignationId,@IsExtraDaysApplicable=IsNULL(IsExtraDayApplicable,0),    
 --@BasicSalary=ISNULL(try_cast(BasicSalary as decimal),0),    
 @DOJ = ISNULL(try_cast(DOJ as datetime),GETDATE()),    
 @DOL  = try_cast(DateOfLeft as datetime),    
 @IsBonusApplicable = ISNULL(BonusApplicable,0)    
 --@GrossEarnings=ISNULL(try_cast([GROSS SALARY] as decimal),0),@Reimbursement=ISNULL(try_cast(Reimbersment as decimal),0)    
 from tblEmployee a (NOLOCK)        
 Left Join tblLocation b (NOLOCK) on a.LocationId=b.LocationId        
 Left Join tblDesignation c (NOLOCK) on a.DesignationId=c.DesignationId    
 Left Join tblLocationType d (NOLOCK) on b.LocationType=d.Id   
 where EmployeeId=@EmployeeId    
   
 --where Ecode='RTNR65'        
  
 Select @WeekdaysHolidayCount=WeekdaysHolidayCount,@FinalSatHolidayCount=FinalSatHolidayCount from ufn_GetEmpHolidayCounts_ForMonth(@Ecode,@Month)  
 Set @Attendance = @Attendance+ISNULL(@WeekdaysHolidayCount,0) + ISNULL(@FinalSatHolidayCount,0)  
 Select @State=State from   
 StoreStateLinking  
 where [ST-CD]=@LocationCategoryId  
  
 --PRINT('State : '+@State)  
 PRINT('STATE')  
 PRINT(@State)  
  
 --Print 'For ECOde : RTNR65, '+'LocationCategoryId : '+@LocationCategoryId        
       DECLARE @weekendCount INT;  
        
   
 SELECT @weekendCount = dbo.WeekendCountFromMonth(@Month);  
 DECLARE @Matched BIT = 0;        
  
 IF (@LocationCategoryId = 'DH24' AND @DesignationId IN (72, 1265))  
 BEGIN  
    SET @WeeklyOff = 0;  
    SET @Matched = 1; -- Mark as matched so no further checks happen  
 END  
 -- First attempt: Location + Designation     
 IF @Matched = 0  
BEGIN  
print('1st attempt')  
 SELECT TOP 1 @WeeklyOff = WeeklyOff, @Matched = 1        
 FROM tblLocationDesignationPolicy        
 WHERE LocationCategoryId = @LocationCategoryId        
   AND DesignationId = @DesignationId        
   AND CAST(TotalAttendance AS INT) <= @NC_Attendance        
 ORDER BY CAST(TotalAttendance AS INT) DESC;        
 end  
 -- Second attempt: Location only        
IF @Matched = 0        
 BEGIN        
SELECT TOP 1   
    @WeeklyOff = WeeklyOff,   
    @Matched = 1  
FROM tblLocationDesignationPolicy  
WHERE LocationCategoryId = @LocationCategoryType  
  AND ForWhichWeeks =   
        CASE   
            WHEN @LocationCategoryType = 'HO' THEN @weekendCount  
            WHEN @LocationCategoryType IN ('DC', 'HUB') THEN @satCount  
            ELSE @weekendCount  -- default fallback  
        END  
  AND DesignationId IS NULL  
  AND CAST(TotalAttendance AS INT) <= @NC_Attendance  
ORDER BY CAST(TotalAttendance AS INT) DESC;  
Print(@LocationCategoryType)  
Print(@weekendCount)  
Print(@satCount)  
      PRINt('2nd Attempt : Loc only')  
 END      
        
 -- Third attempt: Universal        
 IF @Matched = 0        
 BEGIN        
 print('3rd attempt')  
  SELECT TOP 1 @WeeklyOff = WeeklyOff        
  FROM tblLocationDesignationPolicy        
  WHERE LocationCategoryId = 'Universal'        
    AND CAST(TotalAttendance AS INT) <= @NC_Attendance        
  ORDER BY CAST(TotalAttendance AS INT) DESC;        
 END        
  
 PRINT('WEEKLY OFF')  
 PRINT(@WeeklyOff)  
    --SELECT @BgtTotalWeekOffs = TotalWeekOffs   
    --FROM BudgetWeekoffMaster  
    --WHERE If_Joining_Date = @DOJ   
    --  AND LocationCode = @LocationCategoryId   
    --  AND DesignationId = @DesignationId;  
  
   SELECT @BgtTotalWeekOffs=TotalWeekOffs  
FROM dbo.fn_GetEmployeeWeekOffsByEcode(@Month, @Ecode);  
PRINT('BGT WEEKLY OFF')  
 PRINT(@BgtTotalWeekOffs)  
    SET @WeeklyOff = CASE   
                    WHEN  @BgtTotalWeekOffs<@WeeklyOff THEN @BgtTotalWeekOffs   
                    ELSE @WeeklyOff   
           END;  
  
PRINT('END WEEKLY OFF')  
 PRINT(@WeeklyOff)  
        
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
             
              
    --SELECT               
    --    @CompOffBalance AS OpeningCompOffBalance,              
    --    @EarnedLeaveBalance AS OpeningEarnedLeaveBalance,              
    --    @CasualLeaveBalance AS OpeningCasualLeaveBalance;           
              
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
      
 Select @UsedLeaves=Used from tblEmployeeLeaveBalance  
 where ECODE= @Ecode and MONTH=@Month  
              
                
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
--   (CASE         --        WHEN (Attendance-extradays+(select top 1 Used from tblEmployeeLeaveBalance b where b.EmployeeId=a.EmployeeId)+weeklyoff) < (DAY(EOMONTH(GETDATE()))) THEN (Attendance-extradays+(select top 1 Used from tblEmployeeLeaveBalance bw
--here b.EmployeeId=a.EmployeeId)+weeklyoff)               
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
Print('Attendance')  
Print(@Attendance)  
Print(@ExtraDays)  
Print(@WeeklyOff)  
Print(@BudgetMonthDays)  
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
  Status,  
  WeekHolidays,  
  SatHolidays  ,
  NC_Attendance
    )              
 Values(              
  @Month,              
  @Year,              
  1,              
  @DesignationId,              
  @EmployeeName,              
  @Ecode,              
  @EmployeeId,              
  @Attendance-ISNULL(@WeekdaysHolidayCount,0) - ISNULL(@FinalSatHolidayCount,0), -- substracting beacuse i have added both above  to add efct of these also  
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
   
    --summary  
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
 @IsActive ,  
 ISNULL(@WeekdaysHolidayCount,0),  
 ISNULL(@FinalSatHolidayCount,0),
 ISNULL(@NC_Attendance,0)
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
  
  Select @MonthGrossSalaryEmp=try_cast(monthlyGrossCTC as decimal(18,2))  
  from tbl_Month_salary where ecode=@Ecode and MONTH=@Month  
  Print(@MonthGrossSalaryEmp)  
  Select @PTax=FinalPtRate from vw_PTPolicyMaster  
  where State=@State and SlabMin<=@MonthGrossSalaryEmp and SlabMax>=@MonthGrossSalaryEmp  
  PRINT('PTAX')  
  Print(@PTax)  
 SELECT   
    @Lwf = MAX(  
        CASE   
            WHEN State = 'Haryana' THEN  -- Percentage calculation for Haryana with Max check  
               CASE   
     WHEN EmployeeMax IS NULL   
      THEN ((ISNULL(Employee, 0) / 100.0) * ISNULL(@MonthGrossSalaryEmp, 0))  
     WHEN ((ISNULL(Employee, 0) / 100.0) * ISNULL(@MonthGrossSalaryEmp, 0)) > EmployeeMax  
      THEN EmployeeMax  
     ELSE ((ISNULL(Employee, 0) / 100.0) * ISNULL(@MonthGrossSalaryEmp, 0))  
    END  
    / CASE   
     WHEN Frequency = 'Monthly' THEN 1   
     WHEN Frequency = 'Half-yearly' THEN 6   
     WHEN Frequency = 'Yearly' THEN 12   
     ELSE 1  
    END  
  
            ELSE  -- Direct value for all other states  
                CASE   
     WHEN EmployeeMax IS NULL   
      THEN ISNULL(Employee,0)  
     WHEN ISNULL(Employee,0) > EmployeeMax   
      THEN EmployeeMax  
     ELSE ISNULL(Employee,0)  
    END   
    /   
    CASE   
     WHEN Frequency = 'Monthly' THEN 1   
     WHEN Frequency = 'Half-yearly' THEN 6   
     WHEN Frequency = 'Yearly' THEN 12   
     ELSE 1  
    END  
  
        END  
    ),  
      
    @LwfEmployeer = MAX(  
        CASE   
            WHEN State = 'Haryana' THEN  -- Percentage calculation for Haryana with Max check  
               CASE   
     WHEN EmployeerMax IS NULL   
      THEN ((ISNULL(Employeer, 0) / 100.0) * ISNULL(@MonthGrossSalaryEmp, 0))  
     WHEN ((ISNULL(Employeer, 0) / 100.0) * ISNULL(@MonthGrossSalaryEmp, 0)) > EmployeerMax  
      THEN EmployeerMax  
     ELSE ((ISNULL(Employeer, 0) / 100.0) * ISNULL(@MonthGrossSalaryEmp, 0))  
    END  
    / CASE   
     WHEN Frequency = 'Monthly' THEN 1   
     WHEN Frequency = 'Half-yearly' THEN 6   
     WHEN Frequency = 'Yearly' THEN 12   
     ELSE 1  
    END  
  
            ELSE  -- Direct value for all other states  
                CASE   
     WHEN EmployeerMax IS NULL   
      THEN ISNULL(Employeer,0)  
     WHEN ISNULL(Employeer,0) > EmployeerMax   
      THEN EmployeerMax  
     ELSE ISNULL(Employeer,0)  
    END   
    /   
    CASE   
     WHEN Frequency = 'Monthly' THEN 1   
     WHEN Frequency = 'Half-yearly' THEN 6   
     WHEN Frequency = 'Yearly' THEN 12   
     ELSE 1  
    END  
  
        END  
    )  
FROM LWFPolicyMaster  
WHERE State = @State;  
PRINT('LWF')  
 PRINT(@Lwf)  
 PRINT('LwfEmployeer')  
 PRINT(@LwfEmployeer)  
-- SELECT   
--    @Lwf = MAX(  
--        CASE   
--            WHEN Employee IS NOT NULL THEN  
--                CASE   
--                    WHEN ISNULL(Employee,0) > ISNULL(EmployeeMax, 0)   
--                        THEN ISNULL(EmployeeMax,0)  
--                        ELSE ISNULL(Employee, 0)  
--                END   
--                / CASE   
--                    WHEN Frequency = 'Monthly' THEN 1   
--                    WHEN Frequency = 'Half-yearly' THEN 6   
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
--                        THEN ISNULL(EmployeerMax,0)  
--                        ELSE ISNULL(Employeer, 0)  
--                END   
--                / CASE   
--                    WHEN Frequency = 'Monthly' THEN 1   
--                    WHEN Frequency = 'Half-yearly' THEN 6   
--                    WHEN Frequency = 'Yearly' THEN 12   
--                    ELSE 1 -- default  
--                  END  
--            ELSE ISNULL(EmployeerMax, 0)   
--        END  
--    )  
--FROM LWFPolicyMaster  
--WHERE State = @State;  
  
  
  
  
  
 PRINT('Ishu')  
  
--Print(@Lwf +' '+@LwfEmployeer)  
  
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
       
Select   
--@PTax=PTax,@Lwf=Lwf,  
@Tds=TDS,@Loan=Loan,@CashShort=CashShort,@DieselDeduction=DieselDeduction,@Penality=Penality from EmpTDSTable      
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
SET [monthlyGrossCTC(afterDeduction)] = monthlyGrossCTC - try_cast(IsNuLL(@PFValue,0) as decimal) - try_cast(ISNULL(@ESICValue,0) as decimal) - try_cast(ISNULL(@Tds,0) as decimal)- try_cast(ISNULL(@PTax,0) as decimal)-try_cast(ISNULL(@Loan,0) as decimal)
-  
    
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
                WHEN @IsBonusApplicable = 1 THEN (@GrossEarnings / 12)    
                WHEN @BasicSalary <= 21000 THEN (@BasicSalaryCalc * 0.0833)    
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
            WHEN @IsBonusApplicable = 1 THEN (@GrossEarnings / 12)    
            WHEN @BasicSalary <= 21000 THEN (@BasicSalaryCalc * 0.0833)    
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
    
    
 --EXEC dbo.prc_snapshot_vw_emp_attendance @Ecode = @Ecode, @Month = @Month;  
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

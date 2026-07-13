
-- -----------------------------------------------------------------------------
-- dbo.sp_FNF_GetFnfDetailsByEcodeByGautam
-- -----------------------------------------------------------------------------
CREATE   PROCEDURE [dbo].[sp_FNF_GetFnfDetailsByEcodeByGautam]  
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
        /* ===== PER-DAY RATE over the 26th->25th PAY CYCLE (not calendar month) =====
           Ref = @LastValidAttendanceDate (last punch date). day>=26: 26th this..25th next; day<=25: 26th prev..25th this.
           @DaysInMonth is used ONLY for @Rate, so it now holds the pay-cycle day count. */
        DECLARE @__CycleStart DATE, @__CycleEnd DATE, @__Ref DATE = @LastValidAttendanceDate;
        IF DAY(@__Ref) >= 26
        BEGIN
            SET @__CycleStart = DATEFROMPARTS(YEAR(@__Ref), MONTH(@__Ref), 26);
            SET @__CycleEnd   = DATEFROMPARTS(YEAR(DATEADD(MONTH,1,@__Ref)), MONTH(DATEADD(MONTH,1,@__Ref)), 25);
        END
        ELSE
        BEGIN
            SET @__CycleStart = DATEFROMPARTS(YEAR(DATEADD(MONTH,-1,@__Ref)), MONTH(DATEADD(MONTH,-1,@__Ref)), 26);
            SET @__CycleEnd   = DATEFROMPARTS(YEAR(@__Ref), MONTH(@__Ref), 25);
        END
        SET @DaysInMonth = DATEDIFF(DAY, @__CycleStart, @__CycleEnd) + 1;
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


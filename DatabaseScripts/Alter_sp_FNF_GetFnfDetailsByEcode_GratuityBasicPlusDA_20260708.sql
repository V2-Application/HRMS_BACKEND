
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
        @DA DECIMAL(18,2) = 0,  
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
  
        -- âœ… EL month = last punch month - 1  
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
        @GrossSalary = ISNULL(e.[GROSS SALARY],0),
        @DA = ISNULL(e.DA,0)  
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
        /* ================= PER-DAY RATE over the 26th->25th PAY CYCLE (not calendar month) =================
           Reference = @LastValidAttendanceDate (last punch date from attendance).
           day >= 26 : cycle = 26th of this month .. 25th of next month
           day <= 25 : cycle = 26th of previous month .. 25th of this month
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
                /* ===== GRATUITY = ((Basic + DA) * 15 * Years) / 26 =====
           Years = full completed years, +1 ONLY if leftover > 6 months
           (5y5m->5, 5y6m->5, 5y6m+1d->6, 6y6m+1d->7). Eligible only if >= 5 full years (60 months).
           End of service = @LastValidAttendanceDate (last punch date). */
        DECLARE @__End DATE = @LastValidAttendanceDate;
        DECLARE @__FullYears INT =
            CASE WHEN @JoiningDate IS NULL THEN 0
                 ELSE DATEDIFF(YEAR, @JoiningDate, @__End)
                      - CASE WHEN (MONTH(@__End) < MONTH(@JoiningDate))
                                OR (MONTH(@__End) = MONTH(@JoiningDate) AND DAY(@__End) < DAY(@JoiningDate))
                             THEN 1 ELSE 0 END
            END;
        DECLARE @__GratYears INT = @__FullYears
            + CASE WHEN @JoiningDate IS NOT NULL
                    AND @__End > DATEADD(MONTH, 6, DATEADD(YEAR, @__FullYears, @JoiningDate))
                   THEN 1 ELSE 0 END;
        SET @YearsServed = @__GratYears;
        IF @__FullYears >= 5
            SET @GratuityAmount = ((@BasicSal + @DA) * 15.0 * @__GratYears) / 26.0;
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


ALTER PROCEDURE [dbo].[sp_FNF_GetFnfDetailsByEcode]     
(    
    @Ecode NVARCHAR(50)    
)    
AS    
BEGIN    
    SET NOCOUNT ON;    
    
    DECLARE    
        @JoiningDate DATE,    
        @LastPunchMonthDate DATE,    
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
        @Remarks NVARCHAR(200) = NULL;    
    
    /* ================= EMPLOYEE ================= */    
    SELECT    
        @JoiningDate = DOJ,    
        @BasicSal = ISNULL(BasicSalary,0),    
        @GrossSalary = ISNULL([GROSS SALARY],0)    
    FROM tblEmployee    
    WHERE Ecode = @Ecode;    
    
    /* ================= LAST PUNCH MONTH ================= */    
    SELECT    
        @LastPunchMonthDate =    
            MAX(DATEFROMPARTS(    
                2000 + CAST(SUBSTRING([MONTH],5,2) AS INT),    
                CASE LEFT([MONTH],3)    
                    WHEN 'Jan' THEN 1 WHEN 'Feb' THEN 2 WHEN 'Mar' THEN 3    
                    WHEN 'Apr' THEN 4 WHEN 'May' THEN 5 WHEN 'Jun' THEN 6    
                    WHEN 'Jul' THEN 7 WHEN 'Aug' THEN 8 WHEN 'Sep' THEN 9    
                    WHEN 'Oct' THEN 10 WHEN 'Nov' THEN 11 WHEN 'Dec' THEN 12    
                END,1))    
    FROM EmpAttendanceMaster    
    WHERE E_CODE = @Ecode AND IsActive = 1 AND IsDeleted = 0;    
    
    IF @LastPunchMonthDate IS NOT NULL    
    BEGIN    
        SET @LastPunchMonth = FORMAT(@LastPunchMonthDate,'MMM-yy');    
        SET @DaysInMonth = DAY(EOMONTH(@LastPunchMonthDate));    
    
        BEGIN TRY
            SELECT    
                @LastPunchMonthDays = SUM(    
                    CASE    
                        WHEN ValidPunchCount % 2 <> 0 THEN 0    
                        WHEN TRY_CAST(TotalWorkingMinutes AS TIME) > '08:30' THEN 1    
                        WHEN TRY_CAST(TotalWorkingMinutes AS TIME) > '04:30' THEN 0.5    
                        ELSE 0    
                    END)    
            FROM tbl_fn_GetMonthlyPunchesRange_productionnewnick_test    
            WHERE ECode = @Ecode    
              AND AttendanceDate >= @LastPunchMonthDate    
              AND AttendanceDate < DATEADD(MONTH,1,@LastPunchMonthDate);    
        END TRY
        BEGIN CATCH
             SET @LastPunchMonthDays = 0;
        END CATCH
    
        SET @Rate = @GrossSalary / NULLIF(@DaysInMonth,0);    
    
        SELECT @ELDays = ISNULL(EarnedLeaveBalance,0)    
        FROM vw_Emp_Attendance_Format    
        WHERE Ecode = @Ecode AND [Month-Year] = @LastPunchMonth;    
    
        SET @ELAmount = (@BasicSal / 30.0) * @ELDays;    
    
        DECLARE @YearsRaw DECIMAL(10,4) =    
            DATEDIFF(DAY, @JoiningDate, @LastPunchMonthDate) / 365.0;    
    
        SET @YearsServed = FLOOR(@YearsRaw);    
    
        IF @YearsServed >= 1    
            SET @GratuityAmount = (@BasicSal * 15.0 / 26.0) * @YearsServed;    
        ELSE    
            SET @GratuityAmount = 0;    
    
        /* ================= UNPAID AMOUNT ================= */    
        DECLARE    
            @UnpaidMonthDate DATE = DATEADD(MONTH,-1,@LastPunchMonthDate),    
            @UnpaidMonthStart DATE,    
            @UnpaidMonthEnd DATE,    
            @UnpaidMonthKey NVARCHAR(10);    
    
        SET @UnpaidMonthStart = DATEFROMPARTS(YEAR(@UnpaidMonthDate), MONTH(@UnpaidMonthDate), 1);    
        SET @UnpaidMonthEnd   = EOMONTH(@UnpaidMonthStart);    
        SET @UnpaidMonthKey   = FORMAT(@UnpaidMonthDate,'MMM-yy');    
    
        IF NOT EXISTS    
        (    
            SELECT 1    
            FROM tblPaidByBank    
            WHERE Ecode = @Ecode    
              AND [Date] >= @UnpaidMonthStart    
 AND [Date] <= @UnpaidMonthEnd    
        )    
        BEGIN    
            SELECT    
                @UnpaidAmount = ISNULL([Monthly Gross CTC(Actual After Deduction AND AddONS)],0)    
            FROM vw_Emp_Attendance_Format    
            WHERE Ecode = @Ecode    
              AND [Month-Year] = @UnpaidMonthKey;    
        END    
    END    
    ELSE    
        SET @Remarks = 'No attendance found';    
    
    /* ================= BONUS ================= */    
    DECLARE @B TABLE    
    (    
        Ecode NVARCHAR(20),    
        BonusStartMonth NVARCHAR(10),    
        BonusEndMonth NVARCHAR(10),    
        FinalBonus DECIMAL(18,2),    
        Remarks NVARCHAR(200)    
    );    
    
    /* 
       Updated Logic: Guard against Type Conversion Errors.
       The Bonus SP uses 'WHERE UserID = @Ecode' where UserID is likely BIGINT.
       If @Ecode is alphanumeric (e.g. 'RTNR92'), it causes 'Error converting data type nvarchar to bigint'.
       We skip the call for non-numeric Ecodes.
    */
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
        es.LastDay,    
        es.NoticePeriod,    
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
    
        -- ✅ NEW: nullable attachment    
        r.Attachment AS ResignationAttachment    
    
    FROM tblEmployee e    
    LEFT JOIN tblEmployeeSepration es ON es.EmployeeId = e.EmployeeId    
    LEFT JOIN tblResignationType rt ON rt.ResignationTypeId = es.ResignationTypeId    
    
    OUTER APPLY    
    (    
        SELECT TOP 1 er.Attachment    
        FROM dbo.EmployeeResignationChecklistResponse er    
        WHERE er.EmployeeId = e.EmployeeId    
          AND er.Attachment IS NOT NULL    
        ORDER BY    
            ISNULL(er.LastUpdatedOn, er.CreatedOn) DESC,    
            er.EmployeeResignationChecklistResponseId DESC    
    ) r    
    
    WHERE e.Ecode = @Ecode;    
END;

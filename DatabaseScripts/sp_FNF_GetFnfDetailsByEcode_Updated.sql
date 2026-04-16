-- Updated stored procedure with LastPunchMonthDays and Rate calculation
ALTER PROCEDURE [dbo].[sp_FNF_GetFnfDetailsByEcode] 
(  
    @Ecode NVARCHAR(50)  -- Employee code  
)  
AS  
BEGIN  
    SET NOCOUNT ON;  

    -- Variables for unpaid salary calculation  
    DECLARE @LastPunchMonth NVARCHAR(10) = NULL;  
    DECLARE @UnpaidMonth NVARCHAR(10) = NULL;  
    DECLARE @UnpaidAmount DECIMAL(18,2) = NULL;  
    DECLARE @MonthYear INT = NULL;  
    DECLARE @Year INT = NULL;  
    DECLARE @MonthName NVARCHAR(3) = NULL;  
    DECLARE @YearShort NVARCHAR(2) = NULL;  
    DECLARE @UnpaidMonthStartDate DATE = NULL;  
    DECLARE @UnpaidMonthEndDate DATE = NULL;  

    -- Variables for last punch month days and rate
    DECLARE @LastPunchMonthDays DECIMAL(18,2) = NULL;
    DECLARE @Rate DECIMAL(18,2) = NULL;
    DECLARE @LastPunchMonthDate DATE = NULL;
    DECLARE @DaysInLastPunchMonth INT = NULL;
    DECLARE @GrossSalary DECIMAL(18,2) = NULL;
    DECLARE @LastPunchMonthNum INT = NULL;
    DECLARE @LastPunchYear INT = NULL;

    -- Variables for Earned Leave (EL) days and amount
    DECLARE @ELDays DECIMAL(18,2) = 0;
    DECLARE @ELAmount DECIMAL(18,2) = 0;
    DECLARE @BasicSal DECIMAL(18,2) = 0;

    -- Find the last punch month from EmpAttendanceMaster  
    SELECT TOP 1 
        @LastPunchMonth = MONTH
    FROM EmpAttendanceMaster  
    WHERE E_CODE = @Ecode  
        AND IsActive = 1  
        AND IsDeleted = 0  
        AND MONTH IS NOT NULL  
    ORDER BY   
        CASE   
            WHEN MONTH LIKE 'Jan-%' THEN 1  
            WHEN MONTH LIKE 'Feb-%' THEN 2  
            WHEN MONTH LIKE 'Mar-%' THEN 3  
            WHEN MONTH LIKE 'Apr-%' THEN 4  
            WHEN MONTH LIKE 'May-%' THEN 5  
            WHEN MONTH LIKE 'Jun-%' THEN 6  
            WHEN MONTH LIKE 'Jul-%' THEN 7  
            WHEN MONTH LIKE 'Aug-%' THEN 8  
            WHEN MONTH LIKE 'Sep-%' THEN 9  
            WHEN MONTH LIKE 'Oct-%' THEN 10  
            WHEN MONTH LIKE 'Nov-%' THEN 11  
            WHEN MONTH LIKE 'Dec-%' THEN 12  
            ELSE 0  
        END DESC,  
        CAST(SUBSTRING(MONTH, 5, 2) AS INT) DESC,  
        CreatedOn DESC;  

    -- Calculate last punch month days and rate
    IF @LastPunchMonth IS NOT NULL  
    BEGIN  
        -- Parse the last punch month (format: MMM-YY, e.g., Nov-25)  
        SET @MonthName = LEFT(@LastPunchMonth, 3);  
        SET @YearShort = SUBSTRING(@LastPunchMonth, 5, 2);  
        SET @LastPunchYear = 2000 + CAST(@YearShort AS INT);  

        -- Convert month name to number  
        SET @LastPunchMonthNum = CASE @MonthName  
            WHEN 'Jan' THEN 1 WHEN 'Feb' THEN 2 WHEN 'Mar' THEN 3  
            WHEN 'Apr' THEN 4 WHEN 'May' THEN 5 WHEN 'Jun' THEN 6  
            WHEN 'Jul' THEN 7 WHEN 'Aug' THEN 8 WHEN 'Sep' THEN 9  
            WHEN 'Oct' THEN 10 WHEN 'Nov' THEN 11 WHEN 'Dec' THEN 12  
            ELSE 1  
        END;  

        -- Get the first day of the last punch month
        SET @LastPunchMonthDate = DATEFROMPARTS(@LastPunchYear, @LastPunchMonthNum, 1);
        
        -- Calculate number of days in the last punch month (e.g., Dec = 31, Feb = 28/29, etc.)
        SET @DaysInLastPunchMonth = DAY(EOMONTH(@LastPunchMonthDate));
        
        -- Calculate last punch month days from tbl_fn_GetMonthlyPunchesRange_productionnewnick_test
        -- Logic: TotalWorkingMinutes > 08:30 = 1 day, > 04:30 = 0.5 day, else 0
        -- Only consider days with even punch count (ValidPunchCount % 2 = 0), odd counts = 0
        -- TotalWorkingMinutes is stored as string "HH:MM" format
        SELECT @LastPunchMonthDays = ISNULL(SUM(
            CASE 
                -- If punch count is odd, consider as 0
                WHEN ValidPunchCount IS NULL OR ValidPunchCount % 2 != 0 THEN 0.0
                -- If punch count is even, apply time-based logic
                WHEN TRY_CAST(TotalWorkingMinutes AS TIME) > CAST('08:30:00' AS TIME) THEN 1.0
                WHEN TRY_CAST(TotalWorkingMinutes AS TIME) > CAST('04:30:00' AS TIME) THEN 0.5
                ELSE 0.0
            END
        ), 0)
        FROM tbl_fn_GetMonthlyPunchesRange_productionnewnick_test WITH (NOLOCK)
        WHERE ECode = @Ecode
            AND MONTH(AttendanceDate) = @LastPunchMonthNum
            AND YEAR(AttendanceDate) = @LastPunchYear
            AND TotalWorkingMinutes IS NOT NULL
            AND TotalWorkingMinutes != ''
            AND TotalWorkingMinutes != '00:00';

        -- Get GrossSalary from tblEmployee
        SELECT @GrossSalary = ISNULL(GROSS_SALARY, 0)
        FROM tblEmployee
        WHERE Ecode = @Ecode;

        -- Calculate rate: (LastPunchMonthDays / Days in month) * GrossSalary
        IF @DaysInLastPunchMonth > 0 AND @GrossSalary > 0 AND @LastPunchMonthDays > 0
        BEGIN
            SET @Rate = (@LastPunchMonthDays / CAST(@DaysInLastPunchMonth AS DECIMAL(18,2))) * @GrossSalary;
        END
        ELSE
        BEGIN
            SET @Rate = 0;
        END
    END  

    -- Calculate one month before the last punch month  
    IF @LastPunchMonth IS NOT NULL  
    BEGIN  
        -- Parse the month (format: MMM-YY, e.g., Nov-25)  
        SET @MonthName = LEFT(@LastPunchMonth, 3);  
        SET @YearShort = SUBSTRING(@LastPunchMonth, 5, 2);  
        SET @Year = 2000 + CAST(@YearShort AS INT);  

        -- Convert month name to number  
        DECLARE @MonthNum INT = CASE @MonthName  
            WHEN 'Jan' THEN 1 WHEN 'Feb' THEN 2 WHEN 'Mar' THEN 3  
            WHEN 'Apr' THEN 4 WHEN 'May' THEN 5 WHEN 'Jun' THEN 6  
            WHEN 'Jul' THEN 7 WHEN 'Aug' THEN 8 WHEN 'Sep' THEN 9  
            WHEN 'Oct' THEN 10 WHEN 'Nov' THEN 11 WHEN 'Dec' THEN 12  
            ELSE 1  
        END;  

        -- Calculate one month before  
        DECLARE @UnpaidMonthDate DATE = DATEFROMPARTS(@Year, @MonthNum, 1);  
        SET @UnpaidMonthDate = DATEADD(MONTH, -1, @UnpaidMonthDate);  
          
        SET @MonthNum = MONTH(@UnpaidMonthDate);  
        SET @Year = YEAR(@UnpaidMonthDate);  
        SET @YearShort = RIGHT(CAST(@Year AS NVARCHAR(4)), 2);  

        -- Convert back to month name format  
        SET @MonthName = CASE @MonthNum  
            WHEN 1 THEN 'Jan' WHEN 2 THEN 'Feb' WHEN 3 THEN 'Mar'  
            WHEN 4 THEN 'Apr' WHEN 5 THEN 'May' WHEN 6 THEN 'Jun'  
            WHEN 7 THEN 'Jul' WHEN 8 THEN 'Aug' WHEN 9 THEN 'Sep'  
            WHEN 10 THEN 'Oct' WHEN 11 THEN 'Nov' WHEN 12 THEN 'Dec'  
        END;  

        SET @UnpaidMonth = @MonthName + '-' + @YearShort;  
          
        -- Set date range for the unpaid month  
        SET @UnpaidMonthStartDate = DATEFROMPARTS(@Year, @MonthNum, 1);  
        SET @UnpaidMonthEndDate = DATEADD(DAY, -1, DATEADD(MONTH, 1, @UnpaidMonthStartDate));  

        -- Check if employee exists in tblPaidByBank for that month  
        IF EXISTS (  
            SELECT 1   
            FROM tblPaidByBank   
            WHERE Ecode = @Ecode   
                AND Date >= @UnpaidMonthStartDate   
                AND Date <= @UnpaidMonthEndDate  
        )  
        BEGIN  
            PRINT('Paid By Bank')
            -- If exists in tblPaidByBank, unpaid amount is 0  
            SET @UnpaidAmount = 0;  
        END  
        ELSE  
        BEGIN  
            PRINT('Unpaid amount')
            PRINT(@Ecode)
            PRINT(@UnpaidMonth)
            -- If not in tblPaidByBank, get unpaid amount for unpaid month
            SELECT 
                @UnpaidAmount = ISNULL([Monthly Gross CTC(Actual After Deduction AND AddONS)], 0)
            FROM vw_Emp_Attendance_Format
            WHERE Ecode = @Ecode
              AND ([Month-Year] = @UnpaidMonth OR MONTH = @UnpaidMonth);

            -- Get Earned Leave balance for LAST PUNCH month (e.g., Dec-25)
            -- so EL days come from the same month as LastPunchMonthDays / Rate
            SELECT 
                @ELDays = ISNULL(EarnedLeaveBalance, 0)
            FROM vw_Emp_Attendance_Format
            WHERE Ecode = @Ecode
              AND ([Month-Year] = @LastPunchMonth OR MONTH = @LastPunchMonth);
        END 
    END  

    -- Calculate EL amount = BasicSalary / 30 * EL days
    SELECT @BasicSal = ISNULL(BasicSalary, 0)
    FROM tblEmployee
    WHERE Ecode = @Ecode;

    IF @BasicSal > 0 AND @ELDays > 0
    BEGIN
        SET @ELAmount = (@BasicSal / 30.0) * @ELDays;
    END

    -- Main query to get FNF details  
    SELECT TOP 1  
        es.EmployeeId,  
        e.Ecode,  
        e.[FULL NAME] AS EmployeeName,  
        es.LastDay,  
        es.NoticePeriod,    
        rt.ResignationTypeName,  
        es.ResignationDate,  
        es.Remarks,   
        @UnpaidAmount      AS UnpaidAmount,  
        @LastPunchMonth    AS LastPunchMonth,  
        @UnpaidMonth       AS UnpaidMonth,
        @LastPunchMonthDays AS LastPunchMonthDays,
        @Rate              AS Rate,
        @ELDays            AS EarnedLeaveDays,
        @ELAmount          AS EarnedLeaveAmount
    FROM   
        tblEmployee e   
        Left join tblEmployeeSepration es ON es.EmployeeId = e.EmployeeId  
        LEFT JOIN tblResignationType rt ON es.ResignationTypeId = rt.ResignationTypeId  
    WHERE   
        e.Ecode = @Ecode  
    ORDER BY   
        es.ResignationDate DESC;  
END;


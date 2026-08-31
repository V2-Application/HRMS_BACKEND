  --select * from EmpAttendanceViewSnapshot where BatchNo='3827' and month='Feb-26'    
    
CREATE PROCEDURE [dbo].[prc_runecode_iterate_New_Dev]                   
    @MonthKey NVARCHAR(7),          -- Example: 'Sep-25'                  
    @EmployeeIds NVARCHAR(MAX) = NULL,          -- Example: 'V20322,V20323'                  
    @SkippedEcodesMsg NVARCHAR(MAX) OUTPUT      -- Will hold the skipped Ecode list                  
AS                  
BEGIN                  
    SET NOCOUNT ON;                  
                  
    DECLARE @EmployeeId NVARCHAR(20);                  
    DECLARE @BatchNo INT;                  
    DECLARE @SkippedEcodes NVARCHAR(MAX) = N'';                  
      Exec usp_MergeEmpAttendanceFromMonthlySummary_Single_Dev @MonthKey,null            
    -- =====================================================                  
    -- 1️⃣ Cleanup any existing cursor                  
    -- =====================================================                  
    IF CURSOR_STATUS('local','cur') >= -1                  
    BEGIN                  
        BEGIN TRY CLOSE cur; END TRY BEGIN CATCH END CATCH;                  
        BEGIN TRY DEALLOCATE cur; END TRY BEGIN CATCH END CATCH;                  
    END                  
                  
    -- =====================================================                  
    -- 2️⃣ Get new Batch Number                  
    -- =====================================================                  
    SELECT @BatchNo = ISNULL(MAX(BatchNo), 0) + 1                  
    FROM EmpAttendanceViewSnapshot;                  
                  
    PRINT 'New Batch Number: ' + CAST(@BatchNo AS NVARCHAR(10));                  
                  
    -- =====================================================                  
    -- 3️⃣ Prepare Employee List (if passed)                  
    -- =====================================================                  
    DECLARE @EmployeeTable TABLE (Ecode NVARCHAR(20));                  
                  
    IF @EmployeeIds IS NOT NULL                  
    BEGIN                  
        INSERT INTO @EmployeeTable (Ecode)                  
        SELECT TRIM(value)                  
        FROM STRING_SPLIT(@EmployeeIds, ',');                  
    END                  
                  
    -- =====================================================                  
    -- 4️⃣ Cursor for employees                  
    -- =====================================================                  
    DECLARE cur CURSOR LOCAL FAST_FORWARD FOR                  
    SELECT a.Ecode                  
    FROM tblEmployee AS a                  
    LEFT JOIN EMpAttendanceMaster AS b                  
        ON b.E_CODE = a.Ecode                  
       AND b.[MONTH] = @MonthKey                  
    WHERE                   
        (            
            @EmployeeIds IS NULL   -- No employee list → apply attendance logic            
            AND b.TOTAL_PRESENT > 0            
        )            
        OR            
        (            
            @EmployeeIds IS NOT NULL  -- Employee list passed → ignore attendance            
            AND a.Ecode IN (SELECT Ecode FROM @EmployeeTable)            
   --AND b.TOTAL_PRESENT > 0            
        )            
  and b.MONTH=@MonthKey            
  ;            
                 
                  
    OPEN cur;                  
                  
    FETCH NEXT FROM cur INTO @EmployeeId;                  
                  
    WHILE @@FETCH_STATUS = 0                  
    BEGIN                  
        DECLARE @ExistsInSnapshot BIT = 0,                  
                @ExistsInReturnByBank BIT = 0;                  
                  
        -- =====================================================                  
        -- 5️⃣ Check validation                  
        -- =====================================================                  
         SELECT @ExistsInSnapshot =                
          CASE                
            WHEN NOT EXISTS (                
                   SELECT 1                
                   FROM EmpAttendanceViewSnapshot     
                   WHERE Ecode = @EmployeeId AND [Month] = @MonthKey                
                 )                
              OR EXISTS (                
                   SELECT 1                
                   FROM (                
                     SELECT TOP (1) SalaryStatus                
                     FROM EmpAttendanceViewSnapshot                
                     WHERE Ecode = @EmployeeId AND [Month] = @MonthKey                
                     ORDER BY ID DESC                
                   ) t             
                   WHERE t.SalaryStatus IN (0,-1,5)                
                 )                
            THEN 1 ELSE 0                
          END;                
                
                  
        SELECT @ExistsInReturnByBank = CASE WHEN EXISTS (                  
            SELECT 1 FROM ReturnByBankNew                   
            WHERE Ecode = @EmployeeId AND [Month] = @MonthKey                  
        ) THEN 1 ELSE 0 END;             
        -- =====================================================                  
        -- 6️⃣ Decision: Process or Skip                  
        -- =====================================================             
  declare @totalattendance decimal(18,2);         
         
  select @totalattendance = TOTAL_PRESENT from EmpAttendanceMaster where          
  E_CODE = @EmployeeId AND MONTH = @MonthKey          
  PRINT(@totalattendance)        
        IF (@ExistsInSnapshot = 1 AND @totalattendance > 0)                  
        BEGIN                  
            PRINT 'Processing salary for Ecode: ' + @EmployeeId;                  
                  
            EXEC dbo.prc_runecode_iterate_wrapper_PT_LWF_Dev @EmployeeId, @MonthKey;                  
            EXEC dbo.[prc_snapshot_vw_emp_attendance_Dev] @EmployeeId, @MonthKey, @BatchNo;                  
        END                  
        ELSE                  
        BEGIN                  
            PRINT '❌ Skipping ' + @EmployeeId +                   
                  ' — Salary already processed and not in ReturnByBank.';                  
                  
            -- Append skipped Ecode to message                  
            SET @SkippedEcodes =                   
                @SkippedEcodes +                   
                CASE WHEN LEN(@SkippedEcodes) > 0 THEN ',' ELSE '' END +                   
                @EmployeeId;                  
        END                  
                  
        FETCH NEXT FROM cur INTO @EmployeeId;                  
    END                  
                  
    CLOSE cur;                  
    DEALLOCATE cur;                  
                  
    -- =====================================================                  
    -- 7️⃣ Prepare output message                  
    -- =====================================================                  
    IF LEN(@SkippedEcodes) > 0                  
    BEGIN                
        SET @SkippedEcodesMsg = 'Salary not processed for these Ecode(s): ' + @SkippedEcodes +' as Salary already processed and not in ReturnByBank';                  
    END                
    ELSE                
    BEGIN                
        DECLARE @EmployeeCount INT = 0;                
        SELECT @EmployeeCount = COUNT(*) FROM @EmployeeTable;                
        IF @EmployeeCount > 1                
            SET @SkippedEcodesMsg = 'All employees processed successfully.';                
        ELSE                
            SET @SkippedEcodesMsg = 'Employee Salary Processed Successfully.';                
    END                
END 
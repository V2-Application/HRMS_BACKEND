              
CREATE PROCEDURE dbo.usp_MergeEmpAttendanceFromMonthlySummary_Single_Dev   
--'Nov-25','V43312'            
    @MonthToken VARCHAR(7),      -- e.g. 'Sep-25' (MMM-YY)              
    @ECode      NVARCHAR(50)=NULL     -- target ECode              
AS              
BEGIN              
    SET NOCOUNT ON;              
        DECLARE @IsAllowed BIT = 0;  
  
-- Try to fetch allowed status  
SELECT   
    @IsAllowed = CASE WHEN ActionStatus = 'A' THEN 1 ELSE 0 END  
FROM EmpAttendanceMaster (NOLOCK)  
WHERE E_CODE = @ECode   
  AND MONTH = @MonthToken;  
  
-- If no record found, treat as allowed  
IF @@ROWCOUNT = 0  
BEGIN  
    SET @IsAllowed = 1;  -- No row = Allowed  
END  
  
-- If still not allowed, return  
IF ISNULL(@IsAllowed, 0) = 0  
BEGIN  
    RETURN;  
END  
     
              
    BEGIN TRY              
        BEGIN TRAN;              
      
    PRINT('Updating Attendance')  
    PRINT('sdsd'+'sdsdsd')  
        -- Safety: drop temp table if exists (leftover from prior runs)              
        IF OBJECT_ID('tempdb..#src') IS NOT NULL DROP TABLE #src;              
              
        -- Shape must match the SINGLE proc output              
        CREATE TABLE #src              
        (              
            Id                     INT,              
            E_CODE                 NVARCHAR(50),              
            [MONTH]                CHAR(7),              
            MACHINE                DECIMAL(9,2),              
            MANUAL                 DECIMAL(9,2),              
            GF DECIMAL(9,2),          
            TOTAL_PRESENT          DECIMAL(9,2),              
            NC_TOTAL_PRESENT          DECIMAL(9,2),              
            PRESENT_ON_WEEKLYOFF   DECIMAL(9,2),              
            IsActive               BIT,              
            IsDeleted              BIT,              
            CreatedOn              DATETIME2(3),              
            UpdatedOn              DATETIME2(3) NULL,              
            CreatedBy              NVARCHAR(100) NULL,              
            UpdatedBy              NVARCHAR(100) NULL,              
            STCode                 NVARCHAR(50) NULL,              
            DesignationName        NVARCHAR(200) NULL              
        );              
              
        INSERT INTO #src              
        EXEC dbo.[usp_GetMonthlyAttendanceSummary_WithStoreRules_Single_Dev]   
        --'Nov-25','V43312'              
             @MonthToken = @MonthToken,              
             @ECode      = @ECode;              
              
        MERGE dbo.EmpAttendanceMaster AS tgt
USING
(
    SELECT
        s.E_CODE,
        s.[MONTH],
        s.MACHINE,
        s.MANUAL,
        s.GF,
        s.TOTAL_PRESENT,
        s.NC_TOTAL_PRESENT,
        s.PRESENT_ON_WEEKLYOFF,
        s.IsActive,
        s.IsDeleted,
        s.CreatedOn,
        s.CreatedBy
    FROM #src AS s
) AS src
ON  tgt.E_CODE = src.E_CODE
AND tgt.[MONTH] = src.[MONTH]
AND tgt.actionstatus = 'A'

WHEN MATCHED THEN
    UPDATE SET
        tgt.MACHINE              = src.MACHINE,
        tgt.MANUAL               = src.MANUAL,
        tgt.GF                   = src.GF,
        tgt.TOTAL_PRESENT        = src.TOTAL_PRESENT,
        tgt.NC_TOTAL_PRESENT     = src.NC_TOTAL_PRESENT,
        tgt.PRESENT_ON_WEEKLYOFF = src.PRESENT_ON_WEEKLYOFF,
        tgt.IsActive             = src.IsActive,
        tgt.IsDeleted            = src.IsDeleted,
        tgt.UpdatedOn            = GETDATE(),
        tgt.UpdatedBy            = 'SalaryRun'

WHEN NOT MATCHED BY TARGET
     AND NOT EXISTS
     (
         SELECT 1
         FROM dbo.EmpAttendanceMaster x
         WHERE x.E_CODE = src.E_CODE
           AND x.[MONTH] = src.[MONTH]
     )
THEN
    INSERT
    (
        E_CODE,
        [MONTH],
        MACHINE,
        MANUAL,
        GF,
        TOTAL_PRESENT,
        NC_TOTAL_PRESENT,
        PRESENT_ON_WEEKLYOFF,
        IsActive,
        IsDeleted,
        CreatedOn,
        CreatedBy,
        UpdatedOn,
        UpdatedBy,
        actionstatus
    )
    VALUES
    (
        src.E_CODE,
        src.[MONTH],
        src.MACHINE,
        src.MANUAL,
        src.GF,
        src.TOTAL_PRESENT,
        src.NC_TOTAL_PRESENT,
        src.PRESENT_ON_WEEKLYOFF,
        src.IsActive,
        src.IsDeleted,
        src.CreatedOn,
        COALESCE(src.CreatedBy, 'SalaryRun'),
        GETDATE(),
        'SalaryRun',
        'A'
    );         
              
       -- Cleanup              
        DROP TABLE IF EXISTS #src;              
              
        COMMIT TRAN;              
    END TRY              
    BEGIN CATCH              
        IF XACT_STATE() <> 0 ROLLBACK TRAN;              
        IF OBJECT_ID('tempdb..#src') IS NOT NULL DROP TABLE #src;              
              
        DECLARE @ErrMsg NVARCHAR(4000) = ERROR_MESSAGE(),              
                @ErrNo  INT            = ERROR_NUMBER(),              
                @ErrSev INT            = ERROR_SEVERITY(),              
                @ErrSta INT           = ERROR_STATE(),              
                @ErrLin INT            = ERROR_LINE();              
        RAISERROR('usp_MergeEmpAttendanceFromMonthlySummary_Single failed (%d, line %d): %s',              
                  @ErrSev, @ErrSta, @ErrNo, @ErrLin, @ErrMsg);              
        RETURN;              
    END CATCH              
END 

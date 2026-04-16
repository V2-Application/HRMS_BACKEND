-- Stored Procedure: GetComprehensiveSalaryStatusList
-- Description: Gets comprehensive salary status list with pagination, optional Ecode search, and optional Month filter
-- Parameters: 
--   @Month NVARCHAR(10) - Optional, Format: 'MMM-YY' e.g., 'Jan-25'
--   @Ecode NVARCHAR(20) - Optional, Employee code to search for
--   @PageNumber INT - Page number (default: 1)
--   @PageSize INT - Page size (default: 50)
--   @TotalCount INT OUTPUT - Total count of records matching the criteria
-- Returns: Comprehensive salary status data with amounts from all related bank tables (paginated)

ALTER PROCEDURE GetComprehensiveSalaryStatusList  
    @Month NVARCHAR(10) = NULL,
    @Ecode NVARCHAR(20) = NULL,
    @PageNumber INT = 1,
    @PageSize INT = 50,
    @TotalCount INT OUTPUT
AS  
BEGIN  
    SET NOCOUNT ON;  

    -- Validate pagination parameters
    IF @PageNumber < 1 SET @PageNumber = 1;
    IF @PageSize < 1 OR @PageSize > 1000 SET @PageSize = 50;

    ;WITH Uploader_GTB AS 
(
    SELECT 
        ECode,
        FORMAT([Date], 'MMM-yy') AS MonthYear,
        SUM(TRY_CAST(BankTransfer AS DECIMAL(18,2))) AS UploadAmount
    FROM tblBankTransfer
    WHERE 
        (@Month IS NULL OR FORMAT([Date], 'MMM-yy') = @Month)
        AND (@Ecode IS NULL OR ECode LIKE '%' + @Ecode + '%')
    GROUP BY ECode, FORMAT([Date], 'MMM-yy')
),
Uploader_PBB AS 
(
    SELECT 
        ECode,
        FORMAT([Date], 'MMM-yy') AS MonthYear,
        SUM(TRY_CAST(PaidByBank AS DECIMAL(18,2))) AS UploadAmount
    FROM tblPaidByBank
    WHERE 
        (@Month IS NULL OR FORMAT([Date], 'MMM-yy') = @Month)
        AND (@Ecode IS NULL OR ECode LIKE '%' + @Ecode + '%')
    GROUP BY ECode, FORMAT([Date], 'MMM-yy')
),
Uploader_PIC AS 
(
    SELECT 
        E_CODE,
        [MONTH] AS MonthYear,
        SUM(TRY_CAST(AMOUNT AS DECIMAL(18,2))) AS UploadAmount
    FROM tblPaidInCash
    WHERE 
        (@Month IS NULL OR [MONTH] = @Month)
        AND (@Ecode IS NULL OR E_CODE LIKE '%' + @Ecode + '%')
    GROUP BY 
        E_CODE,
        [MONTH]
),
Uploader_RBB AS 
(
    SELECT 
        ECode,
        FORMAT([Date], 'MMM-yy') AS MonthYear,
        SUM(TRY_CAST(ReturnByBank AS DECIMAL(18,2))) AS UploadAmount
    FROM tblReturnByBank
    WHERE 
        (@Month IS NULL OR FORMAT([Date], 'MMM-yy') = @Month)
        AND (@Ecode IS NULL OR ECode LIKE '%' + @Ecode + '%')
    GROUP BY ECode, FORMAT([Date], 'MMM-yy')
),
FilteredData AS
(
    SELECT   
        e.ID AS Id,  
        e.Ecode,  
        e.Location_Code,  
        e.[Location Name],  
        e.[Employee Name],  
        e.[Month-Year],  
        e.[Monthly Gross CTC(Actual After Deduction AND AddONS)] AS PayableSalary,  

        -------------------------------------------------
        -- 1️⃣ GIVEN TO BANK (Uploader → Main)
        -------------------------------------------------
        ISNULL(gtb_u.UploadAmount, gtb.BankTransfer) AS GivenToBankAmount,

        -------------------------------------------------
        -- 2️⃣ PAID BY BANK (Uploader → Main)
        -------------------------------------------------
        ISNULL(pbb_u.UploadAmount, pbb.BankTransfer) AS PaidByBankAmount,

        -------------------------------------------------
        -- 3️⃣ PAID IN CASH (Uploader → Main)
        -------------------------------------------------
        ISNULL(pic_u.UploadAmount, pic.BankTransfer) AS PaidByCashAmount,

        -------------------------------------------------
        -- 4️⃣ RETURN BY BANK (Uploader → Main)
        -------------------------------------------------
        ISNULL(rbb_u.UploadAmount, rbb.BankTransfer) AS ReturnByBankAmount,

        -------------------------------------------------
        -- DIFFERENCE CALCULATION
        -------------------------------------------------
        (e.[Monthly Gross CTC(Actual After Deduction AND AddONS)] -   
            (
              ISNULL(CAST(ISNULL(pbb_u.UploadAmount, pbb.BankTransfer) AS DECIMAL(18,2)), 0) +   
              ISNULL(CAST(ISNULL(pic_u.UploadAmount, pic.BankTransfer) AS DECIMAL(18,2)), 0)
            )
        ) AS Difference,  

        e.SalaryStatus,  
        CONCAT('B_', e.[Month-Year], '_', FORMAT(e.ID, '0000')) AS BatchId,  

        CASE e.SalaryStatus  
            WHEN 2 THEN CONCAT('GTB_', FORMAT(e.ID, '00000000'))  
            WHEN 3 THEN CONCAT('PIC_', FORMAT(e.ID, '00000000'))  
            WHEN 4 THEN CONCAT('PBB_', FORMAT(e.ID, '00000000'))  
            WHEN 5 THEN CONCAT('RBB_', FORMAT(e.ID, '00000000'))  
            ELSE CONCAT('UNK_', FORMAT(e.ID, '00000000'))  
        END AS FormattedId,  

        e.RunAt  
    FROM EmpAttendanceViewSnapshot e  

    -------------------------------------------------
    -- LEFT JOIN ALL UPLOADER TABLES FIRST
    -------------------------------------------------

    LEFT JOIN Uploader_GTB gtb_u   
           ON gtb_u.ECode = e.Ecode 
          AND gtb_u.MonthYear = e.[Month-Year]

    LEFT JOIN Uploader_PBB pbb_u   
           ON pbb_u.ECode = e.Ecode
          AND pbb_u.MonthYear = e.[Month-Year]

    LEFT JOIN Uploader_PIC pic_u   
           ON pic_u.E_CODE = e.Ecode
          AND pic_u.MonthYear = e.[Month-Year]

    LEFT JOIN Uploader_RBB rbb_u   
           ON rbb_u.ECode = e.Ecode
          AND rbb_u.MonthYear = e.[Month-Year]

    -------------------------------------------------
    -- NOW JOIN MAIN TABLES AS FALLBACK
    -------------------------------------------------

    LEFT JOIN GivenToBank gtb  
           ON gtb.BatchId = e.ID   
          AND gtb.IsActive = 1   
          AND gtb.IsDeleted = 0  

    LEFT JOIN PaidByBank pbb  
           ON pbb.BatchId = e.ID   
          AND pbb.IsActive = 1   
          AND pbb.IsDeleted = 0  

    LEFT JOIN PaidInCash pic 
           ON pic.BatchId = e.ID   
          AND pic.IsActive = 1   
          AND pic.IsDeleted = 0  

    LEFT JOIN ReturnByBankNew rbb 
           ON rbb.BatchId = e.ID   
          AND rbb.IsActive = 1   
          AND rbb.IsDeleted = 0  

    WHERE 
        (@Month IS NULL OR e.[Month-Year] = @Month)
        AND (@Ecode IS NULL OR e.Ecode LIKE '%' + @Ecode + '%')
)
-- Store filtered data in table variable for reuse
SELECT 
    Id,
    Ecode,
    Location_Code,
    [Location Name] AS LocationName,
    [Employee Name] AS EmployeeName,
    [Month-Year] AS MonthYear,
    PayableSalary,
    GivenToBankAmount,
    PaidByBankAmount,
    PaidByCashAmount,
    ReturnByBankAmount,
    Difference,
    SalaryStatus,
    BatchId,
    FormattedId,
    RunAt
INTO #TempFilteredData
FROM FilteredData;

-- Get total count
SELECT @TotalCount = COUNT(*) FROM #TempFilteredData;

-- Get paginated results
SELECT 
    Id,
    Ecode,
    Location_Code,
    LocationName,
    EmployeeName,
    MonthYear,
    PayableSalary,
    GivenToBankAmount,
    PaidByBankAmount,
    PaidByCashAmount,
    ReturnByBankAmount,
    Difference,
    SalaryStatus,
    BatchId,
    FormattedId,
    RunAt
FROM #TempFilteredData
ORDER BY Ecode
OFFSET (@PageNumber - 1) * @PageSize ROWS
FETCH NEXT @PageSize ROWS ONLY;

-- Clean up
DROP TABLE #TempFilteredData;

END  
GO

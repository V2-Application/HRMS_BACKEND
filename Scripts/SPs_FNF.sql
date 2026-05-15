-- =============================================================================
-- Category: FNF
-- Source:   dev DB (192.168.151.27\KARMA / HRMS)
-- Generated: 2026-05-14 12:15:05
-- Objects rewritten as CREATE OR ALTER for safe re-run on production.
-- =============================================================================
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
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


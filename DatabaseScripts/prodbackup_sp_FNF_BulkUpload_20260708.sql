
-- -----------------------------------------------------------------------------
-- dbo.sp_FNF_BulkUpload
-- -----------------------------------------------------------------------------

-- Create enhanced procedure
CREATE   PROCEDURE dbo.sp_FNF_BulkUpload
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


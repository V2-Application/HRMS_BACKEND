-- =============================================================================
-- Alter dbo.sp_FNF_BulkUpload  (2026-07-07)
-- Completed-FNF uploader enhancement:
--   * NEW ecodes (no FNF_Header)              -> INSERT full FNF (as before) => Completed
--   * PROCESSED ecodes (FNF_Header, UNPAID)   -> UPDATE/upsert to Completed  => moved out of Processed
--   * COMPLETED ecodes (FNF_Header, PAID)     -> skipped, reported as already-done (duplicate)
--   * within-file duplicate ecodes / invalid ecodes -> reported, skipped
-- "PAID" matches vw_FNF_AccountsList_Unpaid: has FNF_Payment AND
--   (ChequeNo<>'' OR PaymentVoucherNo<>'' OR Status IN done-set).
-- Additive/non-destructive: only this proc changes. No table/data drops.
-- =============================================================================
CREATE   PROCEDURE dbo.sp_FNF_BulkUpload
(
    @JsonData          nvarchar(max),
    @CreatedBy         nvarchar(200) = 'System',
    @DuplicateEcodes   nvarchar(max) OUTPUT,   -- within-file dups + invalid ecodes
    @AlreadyDoneEcodes nvarchar(max) OUTPUT,   -- FNF already PAID (completed) -> skipped
    @ProcessedCount    int OUTPUT,             -- newly INSERTED FNFs
    @TotalRecords      int OUTPUT,             -- rows in the sheet
    @UpdatedCount      int = 0 OUTPUT          -- PROCESSED -> COMPLETED updates
)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @DuplicateEcodes   = NULL;
    SET @AlreadyDoneEcodes = NULL;
    SET @ProcessedCount    = 0;
    SET @TotalRecords      = 0;
    SET @UpdatedCount      = 0;

    BEGIN TRY
        BEGIN TRAN;

        --------------------------------------------------------------------
        -- 1. Temp table for incoming rows
        --------------------------------------------------------------------
        CREATE TABLE #Upload
        (
            RowNo                int IDENTITY(1,1) PRIMARY KEY,
            EmployeeId           bigint NULL,
            Ecode                nvarchar(50)      NOT NULL,
            FNFDate              date              NULL,
            DateOfLeaving        date              NULL,
            UnpaidSalaryAmount   decimal(18,2)     NULL,
            Rate                 decimal(18,2)     NULL,
            Days                 decimal(18,2)     NULL,
            SalaryMonth          nvarchar(100)     NULL,
            Bonus                decimal(18,2)     NULL,
            BonusPeriodFrom      date              NULL,
            BonusPeriodTill      date              NULL,
            Gratuity             decimal(18,2)     NULL,
            CalculatedAs         nvarchar(400)     NULL,
            E_LeaveAmount        decimal(18,2)     NULL,
            ELDays               decimal(18,2)     NULL,
            NoticeSalary         decimal(18,2)     NULL,
            OtherAddition1       decimal(18,2)     NULL,
            OtherAddition2       decimal(18,2)     NULL,
            OtherAddition3       decimal(18,2)     NULL,
            OtherAddition4       decimal(18,2)     NULL,
            LoanBalance          decimal(18,2)     NULL,
            AdvanceBalance       decimal(18,2)     NULL,
            OtherDeduction1      decimal(18,2)     NULL,
            OtherDeduction2      decimal(18,2)     NULL,
            OtherDeduction3      decimal(18,2)     NULL,
            OtherDeduction4      decimal(18,2)     NULL,
            TotalPayable         decimal(18,2)     NULL,
            TDS                  decimal(18,2)     NULL,
            NetPayable           decimal(18,2)     NULL,
            DepositOn            decimal(18,2)     NULL,
            SendForPaymentAmount decimal(18,2)     NULL,
            AmountPaid           decimal(18,2)     NULL,
            PaymentStatus        nvarchar(100)     NULL,
            ChequeNo             nvarchar(100)     NULL,
            ChequeDate           date              NULL,
            PaymentVoucherNo     nvarchar(100)     NULL,
            PaymentRemarks       nvarchar(1000)    NULL
        );

        --------------------------------------------------------------------
        -- 2. Load rows from JSON
        --------------------------------------------------------------------
        INSERT INTO #Upload
        ( Ecode, FNFDate, DateOfLeaving, UnpaidSalaryAmount, Rate, Days, SalaryMonth, Bonus,
          BonusPeriodFrom, BonusPeriodTill, Gratuity, CalculatedAs, E_LeaveAmount, ELDays, NoticeSalary,
          OtherAddition1, OtherAddition2, OtherAddition3, OtherAddition4, LoanBalance, AdvanceBalance,
          OtherDeduction1, OtherDeduction2, OtherDeduction3, OtherDeduction4, TotalPayable, TDS, NetPayable,
          DepositOn, SendForPaymentAmount, AmountPaid, PaymentStatus, ChequeNo, ChequeDate, PaymentVoucherNo, PaymentRemarks )
        SELECT
            Ecode, FNFDate, DateOfLeaving, UnpaidSalaryAmount, Rate, Days, SalaryMonth, Bonus,
            BonusPeriodFrom, BonusPeriodTill, Gratuity, CalculatedAs, E_LeaveAmount, ELDays, NoticeSalary,
            OtherAddition1, OtherAddition2, OtherAddition3, OtherAddition4, LoanBalance, AdvanceBalance,
            OtherDeduction1, OtherDeduction2, OtherDeduction3, OtherDeduction4, TotalPayable, TDS, NetPayable,
            DepositOn, SendForPaymentAmount, AmountPaid, PaymentStatus, ChequeNo, ChequeDate, PaymentVoucherNo, PaymentRemarks
        FROM OPENJSON(@JsonData)
        WITH
        (
            Ecode                nvarchar(50)     '$.Ecode',
            FNFDate              date             '$.FNFDate',
            DateOfLeaving        date             '$.DateOfLeaving',
            UnpaidSalaryAmount   decimal(18,2)    '$.UnpaidSalaryAmount',
            Rate                 decimal(18,2)    '$.Rate',
            Days                 decimal(18,2)    '$.Days',
            SalaryMonth          nvarchar(100)    '$.SalaryMonth',
            Bonus                decimal(18,2)    '$.Bonus',
            BonusPeriodFrom      date             '$.BonusPeriodFrom',
            BonusPeriodTill      date             '$.BonusPeriodTill',
            Gratuity             decimal(18,2)    '$.Gratuity',
            CalculatedAs         nvarchar(400)    '$.CalculatedAs',
            E_LeaveAmount        decimal(18,2)    '$.E_LeaveAmount',
            ELDays               decimal(18,2)    '$.ELDays',
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

        SET @TotalRecords = @@ROWCOUNT;

        --------------------------------------------------------------------
        -- 3. Resolve EmployeeId from Ecode
        --------------------------------------------------------------------
        UPDATE u SET u.EmployeeId = e.EmployeeId
        FROM #Upload u
        LEFT JOIN dbo.tblEmployee e WITH (NOLOCK) ON e.Ecode = u.Ecode;

        --------------------------------------------------------------------
        -- 4. Build @DuplicateEcodes = within-file duplicates + invalid (unknown) Ecodes,
        --    as a SINGLE valid JSON array (C# splits reason: duplicate vs unknown).
        --------------------------------------------------------------------
        ;WITH Flagged AS (
            SELECT Ecode FROM #Upload GROUP BY Ecode HAVING COUNT(*) > 1   -- repeated in sheet
            UNION
            SELECT Ecode FROM #Upload WHERE EmployeeId IS NULL             -- not in employee master
        )
        SELECT @DuplicateEcodes = (SELECT Ecode FROM Flagged FOR JSON PATH);

        --------------------------------------------------------------------
        -- 5. Drop invalid + within-file duplicate rows (keep first occurrence)
        --------------------------------------------------------------------
        DELETE u
        FROM #Upload u
        WHERE u.EmployeeId IS NULL
           OR EXISTS (SELECT 1 FROM #Upload u2 WHERE u2.Ecode = u.Ecode AND u2.RowNo < u.RowNo);

        --------------------------------------------------------------------
        -- 6. Classify employees that already have an FNF_Header.
        --    IsPaid=1 (any FNF paid) -> already completed (skip, report).
        --    IsPaid=0                -> Processed/unpaid -> UPDATE to completed.
        --------------------------------------------------------------------
        CREATE TABLE #Existing
        (
            EmployeeId bigint PRIMARY KEY,
            FNFId      bigint NOT NULL,   -- latest FNFId to attach/update
            IsPaid     bit    NOT NULL
        );

        INSERT INTO #Existing (EmployeeId, FNFId, IsPaid)
        SELECT h.EmployeeId,
               MAX(h.FNFId) AS FNFId,
               MAX(CASE WHEN ( ISNULL(LTRIM(RTRIM(p.ChequeNo)),'') <> ''
                            OR ISNULL(LTRIM(RTRIM(p.PaymentVoucherNo)),'') <> ''
                            OR ISNULL(LTRIM(RTRIM(p.Status)),'') IN ('Transfered','Transferred','Paid','Completed','Done') )
                        THEN 1 ELSE 0 END) AS IsPaid
        FROM dbo.FNF_Header h WITH (NOLOCK)
        JOIN #Upload u              ON u.EmployeeId = h.EmployeeId
        LEFT JOIN dbo.FNF_Payment p WITH (NOLOCK) ON p.FNFId = h.FNFId
        GROUP BY h.EmployeeId;

        -- Already-paid (completed) -> report + remove from processing
        ;WITH AlreadyDone AS (
            SELECT DISTINCT u.Ecode
            FROM #Upload u JOIN #Existing x ON x.EmployeeId = u.EmployeeId
            WHERE x.IsPaid = 1
        )
        SELECT @AlreadyDoneEcodes = (SELECT Ecode FROM AlreadyDone FOR JSON PATH);

        DELETE u FROM #Upload u
        JOIN #Existing x ON x.EmployeeId = u.EmployeeId
        WHERE x.IsPaid = 1;

        --------------------------------------------------------------------
        -- 7a. UPDATE path: Processed (existing, unpaid) -> Completed
        --------------------------------------------------------------------
        -- FNF_Additions: upsert (COALESCE keeps existing where sheet blank)
        UPDATE a
        SET a.FNFDate            = COALESCE(u.FNFDate, a.FNFDate),
            a.DateOfLeaving      = COALESCE(u.DateOfLeaving, a.DateOfLeaving),
            a.UnpaidSalaryAmount = COALESCE(u.UnpaidSalaryAmount, a.UnpaidSalaryAmount),
            a.Rate               = COALESCE(u.Rate, a.Rate),
            a.Days               = COALESCE(CASE WHEN u.Days IS NULL THEN NULL ELSE CAST(u.Days AS int) END, a.Days),
            a.SalaryMonth        = COALESCE(u.SalaryMonth, a.SalaryMonth),
            a.Bonus              = COALESCE(u.Bonus, a.Bonus),
            a.BonusPeriodFrom    = COALESCE(u.BonusPeriodFrom, a.BonusPeriodFrom),
            a.BonusPeriodTill    = COALESCE(u.BonusPeriodTill, a.BonusPeriodTill),
            a.Gratuity           = COALESCE(u.Gratuity, a.Gratuity),
            a.CalculatedAs       = COALESCE(u.CalculatedAs, a.CalculatedAs),
            a.E_LeaveAmount      = COALESCE(u.E_LeaveAmount, a.E_LeaveAmount),
            a.ELDays             = COALESCE(CASE WHEN u.ELDays IS NULL THEN NULL ELSE CAST(u.ELDays AS int) END, a.ELDays),
            a.NoticeSalary       = COALESCE(u.NoticeSalary, a.NoticeSalary),
            a.OtherAddition1     = COALESCE(u.OtherAddition1, a.OtherAddition1),
            a.OtherAddition2     = COALESCE(u.OtherAddition2, a.OtherAddition2),
            a.OtherAddition3     = COALESCE(u.OtherAddition3, a.OtherAddition3),
            a.OtherAddition4     = COALESCE(u.OtherAddition4, a.OtherAddition4)
        FROM dbo.FNF_Additions a
        JOIN #Existing x ON x.FNFId = a.FNFId
        JOIN #Upload   u ON u.EmployeeId = x.EmployeeId
        WHERE x.IsPaid = 0;

        -- FNF_Additions: insert when the processed FNF has no additions row yet
        INSERT INTO dbo.FNF_Additions
        ( FNFId, EmployeeId, FNFDate, DateOfLeaving, UnpaidSalaryAmount, Rate, Days, SalaryMonth, Bonus,
          BonusPeriodFrom, BonusPeriodTill, Gratuity, CalculatedAs, E_LeaveAmount, ELDays, NoticeSalary,
          OtherAddition1, OtherAddition2, OtherAddition3, OtherAddition4 )
        SELECT x.FNFId, u.EmployeeId, u.FNFDate, u.DateOfLeaving, u.UnpaidSalaryAmount, u.Rate,
               CASE WHEN u.Days IS NULL THEN NULL ELSE CAST(u.Days AS int) END, u.SalaryMonth, u.Bonus,
               u.BonusPeriodFrom, u.BonusPeriodTill, u.Gratuity, u.CalculatedAs, u.E_LeaveAmount,
               CASE WHEN u.ELDays IS NULL THEN NULL ELSE CAST(u.ELDays AS int) END, u.NoticeSalary,
               u.OtherAddition1, u.OtherAddition2, u.OtherAddition3, u.OtherAddition4
        FROM #Upload u
        JOIN #Existing x ON x.EmployeeId = u.EmployeeId AND x.IsPaid = 0
        WHERE NOT EXISTS (SELECT 1 FROM dbo.FNF_Additions a WHERE a.FNFId = x.FNFId);

        -- FNF_Deductions: upsert
        UPDATE d
        SET d.LoanBalance     = COALESCE(u.LoanBalance, d.LoanBalance),
            d.AdvanceBalance  = COALESCE(u.AdvanceBalance, d.AdvanceBalance),
            d.OtherDeduction1 = COALESCE(u.OtherDeduction1, d.OtherDeduction1),
            d.OtherDeduction2 = COALESCE(u.OtherDeduction2, d.OtherDeduction2),
            d.OtherDeduction3 = COALESCE(u.OtherDeduction3, d.OtherDeduction3),
            d.OtherDeduction4 = COALESCE(u.OtherDeduction4, d.OtherDeduction4),
            d.TotalPayable    = COALESCE(u.TotalPayable, d.TotalPayable),
            d.TDS             = COALESCE(u.TDS, d.TDS),
            d.NetPayable      = COALESCE(u.NetPayable, d.NetPayable),
            d.DepositOn       = COALESCE(u.DepositOn, d.DepositOn)
        FROM dbo.FNF_Deductions d
        JOIN #Existing x ON x.FNFId = d.FNFId
        JOIN #Upload   u ON u.EmployeeId = x.EmployeeId
        WHERE x.IsPaid = 0;

        INSERT INTO dbo.FNF_Deductions
        ( FNFId, EmployeeId, LoanBalance, AdvanceBalance, OtherDeduction1, OtherDeduction2, OtherDeduction3,
          OtherDeduction4, TotalPayable, TDS, NetPayable, DepositOn )
        SELECT x.FNFId, u.EmployeeId, u.LoanBalance, u.AdvanceBalance, u.OtherDeduction1, u.OtherDeduction2,
               u.OtherDeduction3, u.OtherDeduction4, u.TotalPayable, u.TDS, u.NetPayable, u.DepositOn
        FROM #Upload u
        JOIN #Existing x ON x.EmployeeId = u.EmployeeId AND x.IsPaid = 0
        WHERE NOT EXISTS (SELECT 1 FROM dbo.FNF_Deductions d WHERE d.FNFId = x.FNFId);

        -- FNF_Payment: upsert -> sets a done-status (moves record to Completed)
        UPDATE p
        SET p.SendForPaymentAmount = COALESCE(u.SendForPaymentAmount, p.SendForPaymentAmount),
            p.Remarks              = COALESCE(u.PaymentRemarks, p.Remarks),
            p.ChequeNo             = COALESCE(u.ChequeNo, p.ChequeNo),
            p.ChequeDate           = COALESCE(u.ChequeDate, p.ChequeDate),
            p.Status               = COALESCE(u.PaymentStatus, p.Status, 'Transfered'),
            p.AmountPaid           = COALESCE(u.AmountPaid, p.AmountPaid),
            p.PaymentVoucherNo     = COALESCE(u.PaymentVoucherNo, p.PaymentVoucherNo)
        FROM dbo.FNF_Payment p
        JOIN #Existing x ON x.FNFId = p.FNFId
        JOIN #Upload   u ON u.EmployeeId = x.EmployeeId
        WHERE x.IsPaid = 0;

        INSERT INTO dbo.FNF_Payment
        ( FNFId, SendForPaymentAmount, Remarks, ChequeNo, ChequeDate, Status, AmountPaid, PaymentVoucherNo, CreatedOn, CreatedBy )
        SELECT x.FNFId, u.SendForPaymentAmount, u.PaymentRemarks, u.ChequeNo, u.ChequeDate,
               COALESCE(u.PaymentStatus, 'Transfered'), u.AmountPaid, u.PaymentVoucherNo, GETDATE(), @CreatedBy
        FROM #Upload u
        JOIN #Existing x ON x.EmployeeId = u.EmployeeId AND x.IsPaid = 0
        WHERE NOT EXISTS (SELECT 1 FROM dbo.FNF_Payment p WHERE p.FNFId = x.FNFId);

        -- Count updated (processed -> completed)
        SELECT @UpdatedCount = COUNT(*) FROM #Existing WHERE IsPaid = 0;

        -- Remove updated rows so the INSERT (new) path only handles brand-new FNFs
        DELETE u FROM #Upload u JOIN #Existing x ON x.EmployeeId = u.EmployeeId;

        --------------------------------------------------------------------
        -- 7b. INSERT path: brand-new FNFs (no FNF_Header)
        --------------------------------------------------------------------
        CREATE TABLE #MapFNF ( EmployeeId bigint PRIMARY KEY, FNFId bigint NOT NULL );

        INSERT INTO dbo.FNF_Header (EmployeeId, CreatedBy, CreatedOn)
        OUTPUT inserted.EmployeeId, inserted.FNFId INTO #MapFNF (EmployeeId, FNFId)
        SELECT u.EmployeeId, @CreatedBy, GETDATE() FROM #Upload u;

        SET @ProcessedCount = @@ROWCOUNT;

        INSERT INTO dbo.FNF_Additions
        ( FNFId, EmployeeId, FNFDate, DateOfLeaving, UnpaidSalaryAmount, Rate, Days, SalaryMonth, Bonus,
          BonusPeriodFrom, BonusPeriodTill, Gratuity, CalculatedAs, E_LeaveAmount, ELDays, NoticeSalary,
          OtherAddition1, OtherAddition2, OtherAddition3, OtherAddition4 )
        SELECT m.FNFId, u.EmployeeId, u.FNFDate, u.DateOfLeaving, u.UnpaidSalaryAmount, u.Rate,
               CASE WHEN u.Days IS NULL THEN NULL ELSE CAST(u.Days AS int) END, u.SalaryMonth, u.Bonus,
               u.BonusPeriodFrom, u.BonusPeriodTill, u.Gratuity, u.CalculatedAs, u.E_LeaveAmount,
               CASE WHEN u.ELDays IS NULL THEN NULL ELSE CAST(u.ELDays AS int) END, u.NoticeSalary,
               u.OtherAddition1, u.OtherAddition2, u.OtherAddition3, u.OtherAddition4
        FROM #Upload u JOIN #MapFNF m ON m.EmployeeId = u.EmployeeId;

        INSERT INTO dbo.FNF_Deductions
        ( FNFId, EmployeeId, LoanBalance, AdvanceBalance, OtherDeduction1, OtherDeduction2, OtherDeduction3,
          OtherDeduction4, TotalPayable, TDS, NetPayable, DepositOn )
        SELECT m.FNFId, u.EmployeeId, u.LoanBalance, u.AdvanceBalance, u.OtherDeduction1, u.OtherDeduction2,
               u.OtherDeduction3, u.OtherDeduction4, u.TotalPayable, u.TDS, u.NetPayable, u.DepositOn
        FROM #Upload u JOIN #MapFNF m ON m.EmployeeId = u.EmployeeId;

        INSERT INTO dbo.FNF_Payment
        ( FNFId, SendForPaymentAmount, Remarks, ChequeNo, ChequeDate, Status, AmountPaid, PaymentVoucherNo, CreatedOn, CreatedBy )
        SELECT m.FNFId, u.SendForPaymentAmount, u.PaymentRemarks, u.ChequeNo, u.ChequeDate,
               COALESCE(u.PaymentStatus, 'Transfered'), u.AmountPaid, u.PaymentVoucherNo, GETDATE(), @CreatedBy
        FROM #Upload u JOIN #MapFNF m ON m.EmployeeId = u.EmployeeId
        WHERE u.SendForPaymentAmount IS NOT NULL OR u.AmountPaid IS NOT NULL OR u.PaymentStatus IS NOT NULL
           OR u.ChequeNo IS NOT NULL OR u.ChequeDate IS NOT NULL OR u.PaymentVoucherNo IS NOT NULL
           OR u.PaymentRemarks IS NOT NULL;

        COMMIT TRAN;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRAN;
        THROW;
    END CATCH
END;

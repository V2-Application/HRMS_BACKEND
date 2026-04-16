-- Update FNF_BulkUpload stored procedure to include CreatedBy and CreatedOn
-- Drop existing procedure
IF EXISTS (SELECT * FROM sys.objects WHERE type = 'P' AND name = 'sp_FNF_BulkUpload')
    DROP PROCEDURE dbo.sp_FNF_BulkUpload;
GO

-- Create updated procedure
CREATE PROCEDURE dbo.sp_FNF_BulkUpload
(
    @JsonData nvarchar(max),   -- JSON array from API (Excel rows)
    @CreatedBy nvarchar(100)    -- User who is performing the bulk upload
)
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRAN;

    BEGIN TRY
        --------------------------------------------------------------------
        -- 1. Temp table to hold incoming rows
        --------------------------------------------------------------------
        CREATE TABLE #Upload
        (
            RowNo               int IDENTITY(1,1) PRIMARY KEY,
            EmployeeId          int NULL,
            Ecode               nvarchar(50)      NOT NULL,
            FNFDate             date              NULL,
            DateOfLeaving       date              NULL,

            -- Additions
            UnpaidSalaryAmount  decimal(18,2)     NULL,
            Rate                decimal(18,2)     NULL,
            Days                decimal(18,2)     NULL,
            SalaryMonth         char(7)           NULL,
            Bonus               decimal(18,2)     NULL,
            BonusPeriodFrom     date              NULL,
            BonusPeriodTill     date              NULL,
            Gratuity            decimal(18,2)     NULL,
            CalculatedAs        nvarchar(100)     NULL,
            E_LeaveAmount       decimal(18,2)     NULL,
            ELDays              decimal(18,2)     NULL,
            NoticeSalary        decimal(18,2)     NULL,
            OtherAddition1      decimal(18,2)     NULL,
            OtherAddition2      decimal(18,2)     NULL,
            OtherAddition3      decimal(18,2)     NULL,
            OtherAddition4      decimal(18,2)     NULL,

            -- Deductions
            LoanBalance         decimal(18,2)     NULL,
            AdvanceBalance      decimal(18,2)     NULL,
            OtherDeduction1     decimal(18,2)     NULL,
            OtherDeduction2     decimal(18,2)     NULL,
            OtherDeduction3     decimal(18,2)     NULL,
            OtherDeduction4     decimal(18,2)     NULL,
            TotalPayable        decimal(18,2)     NULL,
            TDS                 decimal(18,2)     NULL,
            NetPayable          decimal(18,2)     NULL,
            DepositOn           date              NULL,

            -- Payment (optional)
            SendForPaymentAmount decimal(18,2)    NULL,
            AmountPaid          decimal(18,2)     NULL,
            PaymentStatus       nvarchar(50)      NULL,
            ChequeNo            nvarchar(50)      NULL,
            ChequeDate          date              NULL,
            PaymentVoucherNo    nvarchar(50)      NULL,
            PaymentRemarks      nvarchar(200)     NULL
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
            Ecode                nvarchar(50)    '$.Ecode',
            FNFDate              date            '$.FNFDate',
            DateOfLeaving        date            '$.DateOfLeaving',

            UnpaidSalaryAmount   decimal(18,2)   '$.UnpaidSalaryAmount',
            Rate                 decimal(18,2)   '$.Rate',
            Days                 decimal(18,2)   '$.Days',
            SalaryMonth          char(7)         '$.SalaryMonth',
            Bonus                decimal(18,2)   '$.Bonus',
            BonusPeriodFrom      date            '$.BonusPeriodFrom',
            BonusPeriodTill      date            '$.BonusPeriodTill',
            Gratuity             decimal(18,2)   '$.Gratuity',
            CalculatedAs         nvarchar(100)   '$.CalculatedAs',
            E_LeaveAmount        decimal(18,2)   '$.E_LeaveAmount',
            ELDays               decimal(18,2)   '$.ELDays',
            NoticeSalary         decimal(18,2)   '$.NoticeSalary',
            OtherAddition1       decimal(18,2)   '$.OtherAddition1',
            OtherAddition2       decimal(18,2)   '$.OtherAddition2',
            OtherAddition3       decimal(18,2)   '$.OtherAddition3',
            OtherAddition4       decimal(18,2)   '$.OtherAddition4',

            LoanBalance          decimal(18,2)   '$.LoanBalance',
            AdvanceBalance       decimal(18,2)   '$.AdvanceBalance',
            OtherDeduction1      decimal(18,2)   '$.OtherDeduction1',
            OtherDeduction2      decimal(18,2)   '$.OtherDeduction2',
            OtherDeduction3      decimal(18,2)   '$.OtherDeduction3',
            OtherDeduction4      decimal(18,2)   '$.OtherDeduction4',
            TotalPayable         decimal(18,2)   '$.TotalPayable',
            TDS                  decimal(18,2)   '$.TDS',
            NetPayable           decimal(18,2)   '$.NetPayable',
            DepositOn            date            '$.DepositOn',

            SendForPaymentAmount decimal(18,2)   '$.SendForPaymentAmount',
            AmountPaid           decimal(18,2)   '$.AmountPaid',
            PaymentStatus        nvarchar(50)    '$.PaymentStatus',
            ChequeNo             nvarchar(50)    '$.ChequeNo',
            ChequeDate           date            '$.ChequeDate',
            PaymentVoucherNo     nvarchar(50)    '$.PaymentVoucherNo',
            PaymentRemarks       nvarchar(200)   '$.PaymentRemarks'
        );

        --------------------------------------------------------------------
        -- 3. Resolve EmployeeId from Ecode (NOLOCK on master table)
        --------------------------------------------------------------------
        UPDATE u
        SET u.EmployeeId = e.EmployeeId
        FROM #Upload u
        LEFT JOIN dbo.tblEmployee e WITH (NOLOCK)
            ON e.Ecode = u.Ecode;

        -- Validation: Ecode must exist
        IF EXISTS (SELECT 1 FROM #Upload WHERE EmployeeId IS NULL)
        BEGIN
            RAISERROR('One or more Ecode values not found in tblEmployee.', 16, 1);
        END

        --------------------------------------------------------------------
        -- 4. Validation: if Ecode already has FNF_Header, block (FNF already done)
        --------------------------------------------------------------------
        IF EXISTS
        (
            SELECT 1
            FROM #Upload u
            JOIN dbo.FNF_Header h WITH (NOLOCK)
                ON h.EmployeeId = u.EmployeeId
        )
        BEGIN
            RAISERROR('FNF already done for one or more employees (duplicate Ecode).', 16, 1);
        END

        --------------------------------------------------------------------
        -- 5. Insert into FNF_Header and capture FNFId mapping
        --    NOTE: we map by EmployeeId, NOT RowNo (this fixes your error)
        --------------------------------------------------------------------
        CREATE TABLE #MapFNF
        (
            EmployeeId int PRIMARY KEY,
            FNFId      int NOT NULL
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
            GETUTCDATE()
        FROM #Upload u;

        --------------------------------------------------------------------
        -- 6. Insert into FNF_Additions
        --------------------------------------------------------------------
        INSERT INTO dbo.FNF_Additions
        (
            FNFId,
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
            u.FNFDate,
            u.DateOfLeaving,
            u.UnpaidSalaryAmount,
            u.Rate,
            u.Days,
            u.SalaryMonth,
            u.Bonus,
            u.BonusPeriodFrom,
            u.BonusPeriodTill,
            u.Gratuity,
            u.CalculatedAs,
            u.E_LeaveAmount,
            u.ELDays,
            u.NoticeSalary,
            u.OtherAddition1,
            u.OtherAddition2,
            u.OtherAddition3,
            u.OtherAddition4
        FROM #Upload u
        JOIN #MapFNF m ON m.EmployeeId = u.EmployeeId;

        --------------------------------------------------------------------
        -- 7. Insert into FNF_Deductions
        --------------------------------------------------------------------
        INSERT INTO dbo.FNF_Deductions
        (
            FNFId,
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
        -- 8. Insert into FNF_Payment (only when data present)
        --------------------------------------------------------------------
        INSERT INTO dbo.FNF_Payment
        (
            FNFId,
            SendForPaymentAmount,
            AmountPaid,
            Status,
            ChequeNo,
            ChequeDate,
            PaymentVoucherNo,
            Remarks
        )
        SELECT
            m.FNFId,
            u.SendForPaymentAmount,
            u.AmountPaid,
            u.PaymentStatus,
            u.ChequeNo,
            u.ChequeDate,
            u.PaymentVoucherNo,
            u.PaymentRemarks
        FROM #Upload u
        JOIN #MapFNF m ON m.EmployeeId = u.EmployeeId
        WHERE
            u.SendForPaymentAmount IS NOT NULL
            OR u.AmountPaid IS NOT NULL
            OR u.PaymentStatus IS NOT NULL;

        --------------------------------------------------------------------
        -- 9. Commit
        --------------------------------------------------------------------
        COMMIT TRAN;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRAN;

        DECLARE @ErrMsg nvarchar(4000), @ErrSeverity int, @ErrState int;
        SELECT
            @ErrMsg = ERROR_MESSAGE(),
            @ErrSeverity = ERROR_SEVERITY(),
            @ErrState = ERROR_STATE();

        RAISERROR(@ErrMsg, @ErrSeverity, @ErrState);
    END CATCH
END;
GO

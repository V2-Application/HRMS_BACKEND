/*
    Fix_LWFPolicy_Haryana_20260826.sql
    ----------------------------------
    PROBLEM
      dbo.LWFPolicyMaster holds Haryana as Employee = 0.200 / Employeer = 0.400. Those look
      like percentage-style figures, but the payroll proc treats the column as a RUPEE amount:

          MIN(Employee, EmployeeMax) / <frequency divisor>

      With Frequency = 'Monthly' the divisor is 1, so Haryana deducts Rs 0.20 per employee.
      Aug-26 was processed this way: 2,141 Haryana employees at Rs 0.20 = Rs 428.20 total,
      against an expected 2,141 x Rs 35 = Rs 74,935.

      This is data, not code. sp_CalculateEmployeePayroll_PT_LWF_Dev is already policy-driven
      (the Haryana hardcode was deliberately removed on 2026-08-25), and the other states prove
      the proc is correct: Punjab deducts Rs 5 and Goa Rs 10, both matching their policy rows.

    FIX
      Set the Haryana row to the real statutory amounts: employee 35, employer 70.

    NOTE
      This corrects the policy only. Aug-26 salary already has 0.20 written into
      dbo.tbl_Month_salary, so the payroll must be RE-RUN after this to pick up 35 / 70.

    Backup: dbo.LWFPolicyMaster_Bak_20260826 (full table copy, taken inside the transaction)
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID('dbo.LWFPolicyMaster_Bak_20260826') IS NOT NULL
BEGIN
    RAISERROR('Backup table dbo.LWFPolicyMaster_Bak_20260826 already exists - stopping so an earlier backup is not overwritten.', 16, 1);
    RETURN;
END

BEGIN TRANSACTION;

    -- 1. Full-table backup before touching anything.
    SELECT * INTO dbo.LWFPolicyMaster_Bak_20260826 FROM dbo.LWFPolicyMaster;

    -- 2. Guard: expect exactly one Haryana row, and expect it to still hold the bad value.
    DECLARE @rows int =
        (SELECT COUNT(*) FROM dbo.LWFPolicyMaster WHERE State LIKE '%Haryana%');
    IF @rows <> 1
    BEGIN
        DECLARE @msg varchar(200) = 'Expected exactly 1 Haryana row, found ' + CAST(@rows AS varchar(10)) + ' - rolling back.';
        RAISERROR(@msg, 16, 1);
        ROLLBACK TRANSACTION;
        RETURN;
    END

    PRINT '--- BEFORE ---';
    SELECT Id, State, Frequency, Employee, EmployeeMax, Employeer, EmployeerMax
    FROM dbo.LWFPolicyMaster WHERE State LIKE '%Haryana%';

    -- 3. Apply the statutory Haryana amounts.
    UPDATE dbo.LWFPolicyMaster
    SET Employee     = 35.00,
        EmployeeMax  = 35.00,
        Employeer    = 70.00,
        EmployeerMax = 70.00
    WHERE State LIKE '%Haryana%';

    PRINT '--- AFTER ---';
    SELECT Id, State, Frequency, Employee, EmployeeMax, Employeer, EmployeerMax
    FROM dbo.LWFPolicyMaster WHERE State LIKE '%Haryana%';

    -- 4. Re-run the proc's own LWF expression to confirm it now yields 35 / 70.
    PRINT '--- what the payroll proc will now compute for Haryana ---';
    SELECT  MAX(CASE WHEN EmployeeMax IS NULL THEN ISNULL(Employee,0)
                     WHEN ISNULL(Employee,0) > EmployeeMax THEN EmployeeMax
                     ELSE ISNULL(Employee,0) END
              / CASE WHEN Frequency='Monthly' THEN 1 WHEN Frequency='Half-yearly' THEN 6
                     WHEN Frequency='Yearly' THEN 12 ELSE 1 END) AS Lwf_Employee,
            MAX(CASE WHEN EmployeerMax IS NULL THEN ISNULL(Employeer,0)
                     WHEN ISNULL(Employeer,0) > EmployeerMax THEN EmployeerMax
                     ELSE ISNULL(Employeer,0) END
              / CASE WHEN Frequency='Monthly' THEN 1 WHEN Frequency='Half-yearly' THEN 6
                     WHEN Frequency='Yearly' THEN 12 ELSE 1 END) AS Lwf_Employer
    FROM dbo.LWFPolicyMaster WHERE State LIKE '%Haryana%';

COMMIT TRANSACTION;

PRINT 'Haryana LWF policy updated. Re-run the Aug-26 payroll to apply it to processed salary.';

/*  ROLLBACK (if ever needed)

    UPDATE p
    SET p.Employee     = b.Employee,
        p.EmployeeMax  = b.EmployeeMax,
        p.Employeer    = b.Employeer,
        p.EmployeerMax = b.EmployeerMax
    FROM dbo.LWFPolicyMaster p
    JOIN dbo.LWFPolicyMaster_Bak_20260826 b ON b.Id = p.Id
    WHERE p.State LIKE '%Haryana%';
*/

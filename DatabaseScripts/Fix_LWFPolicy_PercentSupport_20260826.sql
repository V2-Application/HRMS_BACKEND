/*
    Fix_LWFPolicy_PercentSupport_20260826.sql
    -----------------------------------------
    CORRECT RULE (confirmed by PULKIT 2026-08-26)
        Haryana LWF = 0.2% of gross, capped at Rs 35 (employee)
                      0.4% of gross, capped at Rs 70 (employer)

    HISTORY / WHY THIS SCRIPT EXISTS
      - The proc originally had  CASE WHEN State = 'Haryana' THEN <percentage> ELSE <flat> END.
        That maths was CORRECT, but it hardcoded one state inside the payroll proc.
      - On 2026-08-25 I removed that branch so the LWF policy page would be the single
        source of truth, which made every state flat. Haryana then deducted its raw
        column value (0.200) as rupees -> Rs 0.20 per employee.
      - On 2026-08-26 I "fixed" that by setting Haryana Employee = 35 / Employeer = 70
        flat. That produces the right number only when 0.2% of gross exceeds the cap;
        for anyone earning under Rs 17,500 it OVER-deducts (flat 35 instead of 0.2%).

    THE ACTUAL PROBLEM
      LWFPolicyMaster cannot express HOW a value should be read. Chandigarh/Goa/Punjab
      hold FLAT rupee amounts; Haryana holds a PERCENTAGE. Same column, two meanings.

    FIX
      1. Restore Haryana's real policy numbers: 0.200 / 35.00 / 0.400 / 70.00
      2. Add a CalcType column ('Flat' | 'Percent') so the policy page states which
         reading applies. Everything defaults to 'Flat'; only Haryana is 'Percent'.
      3. (separate script) the payroll proc reads CalcType instead of the state name,
         so no state is hardcoded and new percentage states need only a page edit.

    NOTHING IS DELETED OR TRUNCATED. One column is ADDED; two rows' values are UPDATEd.

    Backup: dbo.LWFPolicyMaster_Bak_20260826 already exists (pre-flat-35 values).
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

    -- Second backup, capturing the state as it is right now (flat 35/70).
    IF OBJECT_ID('dbo.LWFPolicyMaster_Bak_20260826_B') IS NULL
        SELECT * INTO dbo.LWFPolicyMaster_Bak_20260826_B FROM dbo.LWFPolicyMaster;

    -- 1. Add CalcType if it is not already there. Default 'Flat' preserves every
    --    existing state's behaviour; only Haryana changes meaning.
    IF COL_LENGTH('dbo.LWFPolicyMaster', 'CalcType') IS NULL
    BEGIN
        ALTER TABLE dbo.LWFPolicyMaster
            ADD CalcType nvarchar(10) NOT NULL
                CONSTRAINT DF_LWFPolicyMaster_CalcType DEFAULT ('Flat');
        PRINT 'CalcType column added (default Flat).';
    END
    ELSE
        PRINT 'CalcType already present - left alone.';

COMMIT TRANSACTION;
GO

BEGIN TRANSACTION;

    -- 2. Restore Haryana's real percentage policy and mark it as a percentage.
    UPDATE dbo.LWFPolicyMaster
    SET Employee     = 0.200,   -- 0.2 % of gross
        EmployeeMax  = 35.00,   -- capped at Rs 35
        Employeer    = 0.400,   -- 0.4 % of gross
        EmployeerMax = 70.00,   -- capped at Rs 70
        CalcType     = 'Percent'
    WHERE State LIKE '%Haryana%';

    -- 3. Everything else is an explicit flat amount.
    UPDATE dbo.LWFPolicyMaster
    SET CalcType = 'Flat'
    WHERE State NOT LIKE '%Haryana%';

    PRINT '--- LWFPolicyMaster after ---';
    SELECT Id, State, Frequency, CalcType, Employee, EmployeeMax, Employeer, EmployeerMax
    FROM dbo.LWFPolicyMaster
    ORDER BY CASE WHEN CalcType = 'Percent' THEN 0 ELSE 1 END, State;

COMMIT TRANSACTION;
GO

/*  ROLLBACK (if ever needed)

    UPDATE p
    SET p.Employee = b.Employee, p.EmployeeMax = b.EmployeeMax,
        p.Employeer = b.Employeer, p.EmployeerMax = b.EmployeerMax
    FROM dbo.LWFPolicyMaster p
    JOIN dbo.LWFPolicyMaster_Bak_20260826_B b ON b.Id = p.Id;
    -- and, if the column must go:
    -- ALTER TABLE dbo.LWFPolicyMaster DROP CONSTRAINT DF_LWFPolicyMaster_CalcType;
    -- ALTER TABLE dbo.LWFPolicyMaster DROP COLUMN CalcType;
*/

-- =============================================================================
-- PROD MIGRATION step 2 — finish swap blocked by IX_tblEmployee_Ecode
-- =============================================================================
-- Prior state on prod (from step 1):
--   - backup table tblEmployee_BonusApplicable_Backup_20260515 exists
--   - BonusApplicable (bit)  — still present, original values
--   - BonusApplicable_tmp (nvarchar(10)) — populated: 1->'Ctc', 0->'No', NULL->NULL
--   - Index IX_tblEmployee_Ecode INCLUDES BonusApplicable -> blocks DROP COLUMN
--
-- This step:
--   1) drops IX_tblEmployee_Ecode
--   2) drops the bit BonusApplicable column
--   3) renames BonusApplicable_tmp -> BonusApplicable
--   4) recreates IX_tblEmployee_Ecode with identical definition
--   5) verifies
-- =============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

PRINT '>> Step 1: dropping IX_tblEmployee_Ecode';
DROP INDEX IX_tblEmployee_Ecode ON dbo.tblEmployee;
GO

PRINT '>> Step 2: dropping old bit column BonusApplicable';
ALTER TABLE dbo.tblEmployee DROP COLUMN BonusApplicable;
GO

PRINT '>> Step 3: renaming BonusApplicable_tmp -> BonusApplicable';
EXEC sp_rename 'dbo.tblEmployee.BonusApplicable_tmp', 'BonusApplicable', 'COLUMN';
GO

PRINT '>> Step 4: recreating IX_tblEmployee_Ecode';
CREATE NONCLUSTERED INDEX IX_tblEmployee_Ecode
    ON dbo.tblEmployee (Ecode)
    INCLUDE (
        EmployeeId, [FULL NAME], DesignationId, LocationId,
        BasicSalary, HRA, CCA, DA, SpecialAllowance, ExtraAllowance,
        Reimbersment, Fuel_and_Maintainence, Books_and_Periodicals,
        [Professional Attire], [Driver Wages], [Mobile Bill], [Meal Voucher],
        IsActive, PFApplicable, ESICApplicable, BonusApplicable,
        DOJ, DateOfLeft, AOCode, DepartmentId, IsExtraDayApplicable
    );
GO

PRINT '>> Step 5: verification';
SELECT
    (SELECT DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS
     WHERE TABLE_NAME='tblEmployee' AND COLUMN_NAME='BonusApplicable') AS NewDataType,
    SUM(CASE WHEN BonusApplicable = N'Ctc' THEN 1 ELSE 0 END) AS CtcRows,
    SUM(CASE WHEN BonusApplicable = N'No'  THEN 1 ELSE 0 END) AS NoRows,
    SUM(CASE WHEN BonusApplicable IS NULL  THEN 1 ELSE 0 END) AS NullRows,
    COUNT(*) AS TotalRows
FROM dbo.tblEmployee;
GO

PRINT '>> Done';
GO

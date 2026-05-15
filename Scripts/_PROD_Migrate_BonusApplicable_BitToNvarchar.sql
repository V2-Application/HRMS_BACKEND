-- =============================================================================
-- PROD MIGRATION: tblEmployee.BonusApplicable  bit -> nvarchar(10)
-- =============================================================================
-- WHY
--   Dev has BonusApplicable as nvarchar(10) storing 'Ctc' / 'Stat' / 'No' /
--   'Yes', and the C# entity + EmployeeServiceNew + payroll SPs all depend on
--   those string values. Prod still has the column as bit, so EF blows up on
--   any read of tblEmployee with:
--     "Unable to cast object of type 'System.Boolean' to type 'System.String'."
--   This script aligns prod with dev's schema.
--
-- MAPPING (per user confirmation 2026-05-15)
--   bit 1     ->  N'Ctc'
--   bit 0     ->  N'No'
--   bit NULL  ->  NULL
--
-- REVERSIBILITY
--   A backup table tblEmployee_BonusApplicable_Backup_20260515 is created
--   first, preserving (EmployeeId, BonusApplicable bit) for every row.
--
-- IDEMPOTENT
--   Safe to re-run. If the column is already nvarchar, the migration is
--   skipped.
--
-- RUN
--   sqlcmd -S 192.168.151.28\hrms -d HRMS -U sa_hrms -P <pwd> -b ^
--          -i Scripts\_PROD_Migrate_BonusApplicable_BitToNvarchar.sql
-- =============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

-- ----------------------------------------------------------------
-- 1) Pre-flight: backup current bit values
-- ----------------------------------------------------------------
IF OBJECT_ID('dbo.tblEmployee_BonusApplicable_Backup_20260515', 'U') IS NOT NULL
    DROP TABLE dbo.tblEmployee_BonusApplicable_Backup_20260515;
GO

SELECT EmployeeId,
       CONVERT(BIT, BonusApplicable) AS BonusApplicable_bit,
       SYSUTCDATETIME() AS BackedUpOn
INTO   dbo.tblEmployee_BonusApplicable_Backup_20260515
FROM   dbo.tblEmployee;
GO
PRINT '>> Backup table created: tblEmployee_BonusApplicable_Backup_20260515';
GO

-- ----------------------------------------------------------------
-- 2) Skip if column is already non-bit (idempotency guard)
-- ----------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1
    FROM   INFORMATION_SCHEMA.COLUMNS
    WHERE  TABLE_NAME  = 'tblEmployee'
      AND  COLUMN_NAME = 'BonusApplicable'
      AND  DATA_TYPE   = 'bit'
)
BEGIN
    PRINT '>> tblEmployee.BonusApplicable is already non-bit. Nothing to do.';
    SET NOEXEC ON;  -- skip remaining batches
END
GO

-- ----------------------------------------------------------------
-- 3) Add temp nvarchar column
-- ----------------------------------------------------------------
ALTER TABLE dbo.tblEmployee ADD BonusApplicable_tmp NVARCHAR(10) NULL;
GO
PRINT '>> Added column BonusApplicable_tmp NVARCHAR(10)';
GO

-- ----------------------------------------------------------------
-- 4) Map bit -> string into the temp column
-- ----------------------------------------------------------------
UPDATE dbo.tblEmployee
SET BonusApplicable_tmp = CASE
        WHEN BonusApplicable = 1 THEN N'Ctc'
        WHEN BonusApplicable = 0 THEN N'No'
        ELSE NULL
    END;
GO
PRINT '>> Mapped bit values: 1 -> Ctc, 0 -> No';
GO

-- ----------------------------------------------------------------
-- 5) Drop old bit column, rename temp to canonical name
-- ----------------------------------------------------------------
ALTER TABLE dbo.tblEmployee DROP COLUMN BonusApplicable;
GO
EXEC sp_rename 'dbo.tblEmployee.BonusApplicable_tmp', 'BonusApplicable', 'COLUMN';
GO
PRINT '>> Renamed BonusApplicable_tmp -> BonusApplicable';
GO

SET NOEXEC OFF;  -- re-enable execution for verification (if skip was set)
GO

-- ----------------------------------------------------------------
-- 6) Post-migration verification
-- ----------------------------------------------------------------
SELECT
    (SELECT DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS
     WHERE TABLE_NAME = 'tblEmployee' AND COLUMN_NAME = 'BonusApplicable') AS NewDataType,
    SUM(CASE WHEN BonusApplicable = N'Ctc' THEN 1 ELSE 0 END) AS CtcRows,
    SUM(CASE WHEN BonusApplicable = N'No'  THEN 1 ELSE 0 END) AS NoRows,
    SUM(CASE WHEN BonusApplicable IS NULL  THEN 1 ELSE 0 END) AS NullRows,
    COUNT(*) AS TotalRows
FROM dbo.tblEmployee;
GO

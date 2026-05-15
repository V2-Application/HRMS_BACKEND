-- =============================================================================
-- PROD MIGRATION: Candidate.BonusApplicable  bit -> nvarchar(10)
-- Same reason as tblEmployee.BonusApplicable: dev has nvarchar, prod has bit,
-- C# entity expects string?, causes "Boolean -> String" cast in employee profile.
-- Mapping: 1 -> 'Ctc', 0 -> 'No', NULL -> NULL.
-- Reversibility: backup table Candidate_BonusApplicable_Backup_20260515.
-- =============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

IF OBJECT_ID('dbo.Candidate_BonusApplicable_Backup_20260515', 'U') IS NOT NULL
    DROP TABLE dbo.Candidate_BonusApplicable_Backup_20260515;
GO

SELECT Id,
       CONVERT(BIT, BonusApplicable) AS BonusApplicable_bit,
       SYSUTCDATETIME() AS BackedUpOn
INTO   dbo.Candidate_BonusApplicable_Backup_20260515
FROM   dbo.Candidate;
GO
PRINT '>> Backup created: Candidate_BonusApplicable_Backup_20260515';
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE  TABLE_NAME='Candidate' AND COLUMN_NAME='BonusApplicable' AND DATA_TYPE='bit'
)
BEGIN
    PRINT '>> Candidate.BonusApplicable is already non-bit. Nothing to do.';
    SET NOEXEC ON;
END
GO

ALTER TABLE dbo.Candidate ADD BonusApplicable_tmp NVARCHAR(10) NULL;
GO

UPDATE dbo.Candidate
SET BonusApplicable_tmp = CASE
        WHEN BonusApplicable = 1 THEN N'Ctc'
        WHEN BonusApplicable = 0 THEN N'No'
        ELSE NULL
    END;
GO

ALTER TABLE dbo.Candidate DROP COLUMN BonusApplicable;
GO
EXEC sp_rename 'dbo.Candidate.BonusApplicable_tmp', 'BonusApplicable', 'COLUMN';
GO

SET NOEXEC OFF;
GO

SELECT
    (SELECT DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS
     WHERE TABLE_NAME='Candidate' AND COLUMN_NAME='BonusApplicable') AS NewDataType,
    SUM(CASE WHEN BonusApplicable = N'Ctc' THEN 1 ELSE 0 END) AS CtcRows,
    SUM(CASE WHEN BonusApplicable = N'No'  THEN 1 ELSE 0 END) AS NoRows,
    SUM(CASE WHEN BonusApplicable IS NULL  THEN 1 ELSE 0 END) AS NullRows,
    COUNT(*) AS TotalRows
FROM dbo.Candidate;
GO
PRINT '>> Done';
GO

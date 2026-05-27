-- =============================================================================
-- Add MedicalCardUrl column to dbo.tblEmployee
-- Non-mandatory attachment URL for employee Medical Card document.
-- Idempotent: guarded with COL_LENGTH check.
-- Run on DEV ONLY (per user instruction). Do NOT run on prod without approval.
-- =============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

IF COL_LENGTH('dbo.tblEmployee', 'MedicalCardUrl') IS NULL
    ALTER TABLE dbo.tblEmployee ADD MedicalCardUrl NVARCHAR(500) NULL;
GO

PRINT '>> tblEmployee.MedicalCardUrl ready';
SELECT c.name, t.name AS dtype, c.max_length, c.is_nullable
FROM sys.columns c JOIN sys.types t ON t.user_type_id = c.user_type_id
WHERE c.object_id = OBJECT_ID('dbo.tblEmployee') AND c.name = 'MedicalCardUrl';
GO

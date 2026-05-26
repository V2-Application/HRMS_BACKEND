-- =============================================================================
-- Add audit + soft-delete columns to dbo.tblDesignation
-- Mirrors the columns already present on dbo.tblDepartment (lowercase isActive
-- / isDeleted to match existing convention).
-- Idempotent: each ALTER guarded by COL_LENGTH check.
-- Run on dev first; copy to prod when ready.
-- =============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

IF COL_LENGTH('dbo.tblDesignation', 'CreatedBy') IS NULL
    ALTER TABLE dbo.tblDesignation ADD CreatedBy BIGINT NULL;
GO

IF COL_LENGTH('dbo.tblDesignation', 'UpdatedOn') IS NULL
    ALTER TABLE dbo.tblDesignation ADD UpdatedOn DATETIME NULL;
GO

IF COL_LENGTH('dbo.tblDesignation', 'UpdatedBy') IS NULL
    ALTER TABLE dbo.tblDesignation ADD UpdatedBy BIGINT NULL;
GO

IF COL_LENGTH('dbo.tblDesignation', 'isActive') IS NULL
    ALTER TABLE dbo.tblDesignation ADD isActive BIT NOT NULL CONSTRAINT DF_tblDesignation_isActive DEFAULT (1) WITH VALUES;
GO

IF COL_LENGTH('dbo.tblDesignation', 'isDeleted') IS NULL
    ALTER TABLE dbo.tblDesignation ADD isDeleted BIT NOT NULL CONSTRAINT DF_tblDesignation_isDeleted DEFAULT (0) WITH VALUES;
GO

PRINT '>> tblDesignation audit columns ready';
SELECT c.name AS col, t.name AS dtype, c.is_nullable
FROM sys.columns c JOIN sys.types t ON t.user_type_id = c.user_type_id
WHERE c.object_id = OBJECT_ID('dbo.tblDesignation') ORDER BY c.column_id;
GO

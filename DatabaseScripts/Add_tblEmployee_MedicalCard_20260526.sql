-- =============================================================================
-- Create dbo.tblEmployee_MedicalCard
-- Per-card detail row (one PDF page = one card) parsed from the medical-card
-- PDFs referenced by tblEmployee.MedicalCardUrl. Multiple cards per employee
-- (employee + family members).
-- Idempotent: guarded by OBJECT_ID; columns can be added by re-running with
-- additional COL_LENGTH checks if extended later.
-- Run on DEV ONLY (per user instruction). Do NOT run on prod without approval.
-- =============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'dbo.tblEmployee_MedicalCard', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tblEmployee_MedicalCard
    (
        Id              INT IDENTITY(1,1) NOT NULL,
        EmployeeId      BIGINT       NOT NULL,
        Ecode           NVARCHAR(50) NOT NULL,
        CardOrder       INT          NOT NULL,        -- page order: 1..N
        UhidNo          NVARCHAR(50)  NULL,
        HolderName      NVARCHAR(200) NULL,
        Age             INT           NULL,
        Gender          CHAR(1)       NULL,            -- 'M' / 'F'
        PlanValidFrom   DATE          NULL,
        PlanValidTo     DATE          NULL,
        PolicyNo        NVARCHAR(100) NULL,
        Organisation    NVARCHAR(200) NULL,
        Insurer         NVARCHAR(200) NULL,            -- derived from UHID prefix (UIIC -> United India Insurance Co.)
        Tpa             NVARCHAR(200) NULL,            -- derived (FHPL -> Family Health Plan Ltd)
        SumAssured      DECIMAL(18,2) NULL,            -- manual entry; not present on the PDF
        SourcePdfUrl    NVARCHAR(500) NULL,
        RawText         NVARCHAR(MAX) NULL,
        CreatedOn       DATETIME2(0)  NOT NULL CONSTRAINT DF_tblEmployee_MedicalCard_CreatedOn DEFAULT (SYSUTCDATETIME()),
        CreatedBy       NVARCHAR(100) NULL,
        UpdatedOn       DATETIME2(0)  NULL,
        UpdatedBy       NVARCHAR(100) NULL,
        CONSTRAINT PK_tblEmployee_MedicalCard PRIMARY KEY (Id),
        CONSTRAINT UK_tblEmployee_MedicalCard_Emp_Order UNIQUE (EmployeeId, CardOrder)
    );

    CREATE INDEX IX_tblEmployee_MedicalCard_Ecode      ON dbo.tblEmployee_MedicalCard(Ecode);
    CREATE INDEX IX_tblEmployee_MedicalCard_EmployeeId ON dbo.tblEmployee_MedicalCard(EmployeeId);

    PRINT '>> Created dbo.tblEmployee_MedicalCard';
END
ELSE
BEGIN
    PRINT '>> dbo.tblEmployee_MedicalCard already exists; no change';
END
GO

SELECT c.name, t.name AS dtype, c.max_length, c.is_nullable
FROM sys.columns c JOIN sys.types t ON t.user_type_id = c.user_type_id
WHERE c.object_id = OBJECT_ID('dbo.tblEmployee_MedicalCard')
ORDER BY c.column_id;
GO

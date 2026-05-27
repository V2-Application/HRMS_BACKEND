-- =============================================================================
-- Add dbo.tblStatutoryRates
-- Configuration table for PF and ESI rates / wage ceilings / monthly caps.
-- Foundation only: NOT YET WIRED into payroll calculation. The current payroll
-- flow continues to consume pre-uploaded values from tblEmpPFDatum /
-- tblEmpESICDatum / tblEmployeeDeduction. A later change can teach
-- sp_CalculateEmployeePayroll (or an equivalent service) to apply these rates
-- and the Rs.1800 PF cap automatically.
-- Idempotent: guarded by OBJECT_ID + MERGE keyed on (RateCode, EffectiveFrom).
-- Run on DEV ONLY (per saved guidance). Do NOT run on prod without approval.
-- =============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'dbo.tblStatutoryRates', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tblStatutoryRates
    (
        Id                INT IDENTITY(1,1) NOT NULL,
        RateCode          NVARCHAR(50)  NOT NULL,    -- 'PF', 'PF_EMPLOYER', 'ESI_EMPLOYEE', 'ESI_EMPLOYER'
        DisplayName       NVARCHAR(200) NOT NULL,
        RatePercent       DECIMAL(7,4)  NOT NULL,    -- e.g., 12.0000, 0.7500, 3.2500
        WageCeiling       DECIMAL(18,2) NULL,        -- e.g., 15000.00 (PF), 21000.00 (ESI). NULL = no ceiling.
        MaxContribution   DECIMAL(18,2) NULL,        -- e.g., 1800.00 (PF). NULL when not capped.
        EffectiveFrom     DATE          NOT NULL,
        EffectiveTo       DATE          NULL,        -- NULL = currently in effect.
        Notes             NVARCHAR(500) NULL,
        IsActive          BIT           NOT NULL CONSTRAINT DF_tblStatutoryRates_IsActive DEFAULT (1),
        CreatedOn         DATETIME2(0)  NOT NULL CONSTRAINT DF_tblStatutoryRates_CreatedOn DEFAULT (SYSUTCDATETIME()),
        CreatedBy         NVARCHAR(100) NULL,
        UpdatedOn         DATETIME2(0)  NULL,
        UpdatedBy         NVARCHAR(100) NULL,
        CONSTRAINT PK_tblStatutoryRates PRIMARY KEY (Id),
        CONSTRAINT UK_tblStatutoryRates_Code_EffFrom UNIQUE (RateCode, EffectiveFrom)
    );

    CREATE INDEX IX_tblStatutoryRates_Active
        ON dbo.tblStatutoryRates (RateCode, IsActive, EffectiveFrom);

    PRINT '>> Created dbo.tblStatutoryRates';
END
ELSE
    PRINT '>> dbo.tblStatutoryRates already exists; no change';
GO

-- Seed canonical Indian statutory rates. Idempotent.
MERGE dbo.tblStatutoryRates AS tgt
USING (VALUES
    ('PF',           'Provident Fund (Employee)',  12.0000, 15000.00, 1800.00, CONVERT(date,'2014-09-01'), 'EPF Act: 12% of basic, wage ceiling Rs.15,000 -> max Rs.1,800/month'),
    ('PF_EMPLOYER',  'Provident Fund (Employer)',  12.0000, 15000.00, 1800.00, CONVERT(date,'2014-09-01'), 'Employer matching contribution; same cap as employee'),
    ('ESI_EMPLOYEE', 'ESIC (Employee share)',       0.7500, 21000.00, NULL,    CONVERT(date,'2019-07-01'), 'ESI Act: 0.75% of gross; coverage withdrawn when gross > Rs.21,000'),
    ('ESI_EMPLOYER', 'ESIC (Employer share)',       3.2500, 21000.00, NULL,    CONVERT(date,'2019-07-01'), 'ESI Act: 3.25% of gross; coverage withdrawn when gross > Rs.21,000')
) AS src (RateCode, DisplayName, RatePercent, WageCeiling, MaxContribution, EffectiveFrom, Notes)
ON tgt.RateCode = src.RateCode AND tgt.EffectiveFrom = src.EffectiveFrom
WHEN NOT MATCHED THEN
    INSERT (RateCode, DisplayName, RatePercent, WageCeiling, MaxContribution, EffectiveFrom, Notes, CreatedBy)
    VALUES (src.RateCode, src.DisplayName, src.RatePercent, src.WageCeiling, src.MaxContribution, src.EffectiveFrom, src.Notes, 'install_script');
GO

-- Verify
SELECT RateCode, DisplayName, RatePercent, WageCeiling, MaxContribution,
       EffectiveFrom, EffectiveTo, IsActive
FROM dbo.tblStatutoryRates
ORDER BY RateCode, EffectiveFrom;
GO

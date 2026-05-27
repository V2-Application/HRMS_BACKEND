-- =============================================================================
-- Add dbo.tblRoleAccessMatrix + seed two canonical roles (SuperAdmin, Employee)
-- Foundation only: NOT YET WIRED into authorization. Today access is decided by
-- RequirePageAccessAttribute + the existing Rbac stack; this table is a clean,
-- declarative matrix that future middleware (or a Roles-management UI) can
-- consume to gate features per role.
--
-- Two roles only, per requirement:
--   * SuperAdmin  -- full access (admin)
--   * Employee    -- self-service only (non-admin)
--
-- Idempotent: guarded by OBJECT_ID + MERGE keyed on natural keys.
-- Run on DEV ONLY (per saved guidance). Do NOT run on prod without approval.
-- =============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

-- ---------------------------------------------------------------------------
-- 1) Ensure the two canonical roles exist in dbo.tblRole.
--    tblRole already exists in the schema; we only upsert the two rows.
-- ---------------------------------------------------------------------------
MERGE dbo.tblRole AS tgt
USING (VALUES
    ('SuperAdmin', 'Full system access: manage roles, settings, payroll, audit logs',   1,   1),
    ('Employee',   'Self-service access: own profile, own payslip, attendance punch',   1, 100)
) AS src (RoleName, Description, IsActive, Priority)
ON tgt.RoleName = src.RoleName
WHEN NOT MATCHED THEN
    INSERT (RoleName, Description, IsActive, Priority, CreatedOn, CreatedBy)
    VALUES (src.RoleName, src.Description, src.IsActive, src.Priority, SYSUTCDATETIME(), 'install_script')
WHEN MATCHED AND (
        ISNULL(tgt.Description,'') <> src.Description
     OR ISNULL(tgt.IsActive,0)     <> src.IsActive
     OR ISNULL(tgt.Priority,-1)    <> src.Priority
    ) THEN
    UPDATE SET
        Description     = src.Description,
        IsActive        = src.IsActive,
        Priority        = src.Priority,
        LastUpdatedBy   = 'install_script';
GO

PRINT '>> tblRole upsert complete (SuperAdmin, Employee).';
GO

-- ---------------------------------------------------------------------------
-- 2) Create dbo.tblRoleAccessMatrix if it does not exist.
--    One row per (Role, FeatureKey); CRUD flags + a Notes column.
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.tblRoleAccessMatrix', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tblRoleAccessMatrix
    (
        Id            INT IDENTITY(1,1) NOT NULL,
        RoleId        INT           NOT NULL,
        FeatureKey    NVARCHAR(100) NOT NULL,    -- machine key, e.g. 'EMPLOYEE_MGMT'
        DisplayName   NVARCHAR(200) NOT NULL,    -- human label
        CanView       BIT           NOT NULL CONSTRAINT DF_tblRoleAccessMatrix_CanView   DEFAULT (0),
        CanCreate     BIT           NOT NULL CONSTRAINT DF_tblRoleAccessMatrix_CanCreate DEFAULT (0),
        CanEdit       BIT           NOT NULL CONSTRAINT DF_tblRoleAccessMatrix_CanEdit   DEFAULT (0),
        CanDelete     BIT           NOT NULL CONSTRAINT DF_tblRoleAccessMatrix_CanDelete DEFAULT (0),
        Notes         NVARCHAR(500) NULL,
        IsActive      BIT           NOT NULL CONSTRAINT DF_tblRoleAccessMatrix_IsActive  DEFAULT (1),
        CreatedOn     DATETIME2(0)  NOT NULL CONSTRAINT DF_tblRoleAccessMatrix_CreatedOn DEFAULT (SYSUTCDATETIME()),
        CreatedBy     NVARCHAR(100) NULL,
        UpdatedOn     DATETIME2(0)  NULL,
        UpdatedBy     NVARCHAR(100) NULL,
        CONSTRAINT PK_tblRoleAccessMatrix              PRIMARY KEY (Id),
        CONSTRAINT UK_tblRoleAccessMatrix_Role_Feature UNIQUE (RoleId, FeatureKey),
        CONSTRAINT FK_tblRoleAccessMatrix_Role         FOREIGN KEY (RoleId) REFERENCES dbo.tblRole(RoleId)
    );

    CREATE INDEX IX_tblRoleAccessMatrix_RoleId  ON dbo.tblRoleAccessMatrix (RoleId);
    CREATE INDEX IX_tblRoleAccessMatrix_Feature ON dbo.tblRoleAccessMatrix (FeatureKey);

    PRINT '>> Created dbo.tblRoleAccessMatrix';
END
ELSE
    PRINT '>> dbo.tblRoleAccessMatrix already exists; no schema change';
GO

-- ---------------------------------------------------------------------------
-- 3) Seed the access matrix. Idempotent (MERGE keyed on RoleId + FeatureKey).
--    Admin features = SuperAdmin only. Self-service features = both roles.
-- ---------------------------------------------------------------------------
DECLARE @RoleSuperAdmin INT = (SELECT RoleId FROM dbo.tblRole WHERE RoleName = 'SuperAdmin');
DECLARE @RoleEmployee   INT = (SELECT RoleId FROM dbo.tblRole WHERE RoleName = 'Employee');

IF @RoleSuperAdmin IS NULL OR @RoleEmployee IS NULL
BEGIN
    RAISERROR('Could not resolve SuperAdmin / Employee RoleId from dbo.tblRole', 16, 1);
    RETURN;
END

;WITH seed (RoleId, FeatureKey, DisplayName, CanView, CanCreate, CanEdit, CanDelete, Notes) AS
(
    -- ===== SuperAdmin: full access (admin) =====
    SELECT @RoleSuperAdmin, 'EMPLOYEE_MGMT',     'Employee Management',          1, 1, 1, 1, 'Create/edit/disable employees'           UNION ALL
    SELECT @RoleSuperAdmin, 'ROLE_MGMT',         'Role & Permission Management', 1, 1, 1, 1, 'Manage roles and this access matrix'     UNION ALL
    SELECT @RoleSuperAdmin, 'SALARY_CONFIG',     'Salary / PF / ESI Config',     1, 1, 1, 1, 'Statutory rates, components, ceilings'   UNION ALL
    SELECT @RoleSuperAdmin, 'PAYROLL_PROCESS',   'Payroll Processing',           1, 1, 1, 1, 'Run, lock, release monthly payroll'      UNION ALL
    SELECT @RoleSuperAdmin, 'LOCATION_MGMT',     'Location / Store Master',      1, 1, 1, 1, 'Upload, edit, deactivate stores'         UNION ALL
    SELECT @RoleSuperAdmin, 'REPORTS',           'Reports & Exports',            1, 0, 0, 0, 'All reports'                             UNION ALL
    SELECT @RoleSuperAdmin, 'AUDIT_LOGS',        'Audit Logs',                   1, 0, 0, 0, 'Read-only system audit trail'            UNION ALL
    SELECT @RoleSuperAdmin, 'SETTINGS',          'System Settings',              1, 0, 1, 0, 'Global config'                           UNION ALL
    SELECT @RoleSuperAdmin, 'OWN_PROFILE',       'Own Profile',                  1, 0, 1, 0, 'View/update own profile'                 UNION ALL
    SELECT @RoleSuperAdmin, 'OWN_PAYSLIP',       'Own Payslip',                  1, 0, 0, 0, 'View own payslip'                        UNION ALL
    SELECT @RoleSuperAdmin, 'ATTENDANCE_PUNCH',  'Attendance Punch',             1, 1, 0, 0, 'Mark own attendance'                     UNION ALL
    -- ===== Employee: self-service only (non-admin) =====
    SELECT @RoleEmployee,   'EMPLOYEE_MGMT',     'Employee Management',          0, 0, 0, 0, 'Denied'                                  UNION ALL
    SELECT @RoleEmployee,   'ROLE_MGMT',         'Role & Permission Management', 0, 0, 0, 0, 'Denied'                                  UNION ALL
    SELECT @RoleEmployee,   'SALARY_CONFIG',     'Salary / PF / ESI Config',     0, 0, 0, 0, 'Denied'                                  UNION ALL
    SELECT @RoleEmployee,   'PAYROLL_PROCESS',   'Payroll Processing',           0, 0, 0, 0, 'Denied'                                  UNION ALL
    SELECT @RoleEmployee,   'LOCATION_MGMT',     'Location / Store Master',      0, 0, 0, 0, 'Denied'                                  UNION ALL
    SELECT @RoleEmployee,   'REPORTS',           'Reports & Exports',            0, 0, 0, 0, 'Denied'                                  UNION ALL
    SELECT @RoleEmployee,   'AUDIT_LOGS',        'Audit Logs',                   0, 0, 0, 0, 'Denied'                                  UNION ALL
    SELECT @RoleEmployee,   'SETTINGS',          'System Settings',              0, 0, 0, 0, 'Denied'                                  UNION ALL
    SELECT @RoleEmployee,   'OWN_PROFILE',       'Own Profile',                  1, 0, 1, 0, 'Self-service: view/update own profile'   UNION ALL
    SELECT @RoleEmployee,   'OWN_PAYSLIP',       'Own Payslip',                  1, 0, 0, 0, 'Self-service: view own payslip'          UNION ALL
    SELECT @RoleEmployee,   'ATTENDANCE_PUNCH',  'Attendance Punch',             1, 1, 0, 0, 'Self-service: punch attendance'
)
MERGE dbo.tblRoleAccessMatrix AS tgt
USING seed AS src
   ON tgt.RoleId = src.RoleId AND tgt.FeatureKey = src.FeatureKey
WHEN NOT MATCHED THEN
    INSERT (RoleId, FeatureKey, DisplayName, CanView, CanCreate, CanEdit, CanDelete, Notes, CreatedBy)
    VALUES (src.RoleId, src.FeatureKey, src.DisplayName, src.CanView, src.CanCreate, src.CanEdit, src.CanDelete, src.Notes, 'install_script')
WHEN MATCHED AND (
        tgt.CanView      <> src.CanView
     OR tgt.CanCreate    <> src.CanCreate
     OR tgt.CanEdit      <> src.CanEdit
     OR tgt.CanDelete    <> src.CanDelete
     OR ISNULL(tgt.DisplayName,'') <> src.DisplayName
     OR ISNULL(tgt.Notes,'')       <> ISNULL(src.Notes,'')
    ) THEN
    UPDATE SET
        DisplayName = src.DisplayName,
        CanView     = src.CanView,
        CanCreate   = src.CanCreate,
        CanEdit     = src.CanEdit,
        CanDelete   = src.CanDelete,
        Notes       = src.Notes,
        UpdatedOn   = SYSUTCDATETIME(),
        UpdatedBy   = 'install_script';
GO

PRINT '>> tblRoleAccessMatrix seeded.';
GO

-- ---------------------------------------------------------------------------
-- 4) Verify
-- ---------------------------------------------------------------------------
SELECT r.RoleName, m.FeatureKey, m.DisplayName,
       m.CanView, m.CanCreate, m.CanEdit, m.CanDelete, m.Notes
FROM dbo.tblRoleAccessMatrix m
JOIN dbo.tblRole r ON r.RoleId = m.RoleId
WHERE r.RoleName IN ('SuperAdmin', 'Employee')
ORDER BY r.Priority, m.FeatureKey;
GO

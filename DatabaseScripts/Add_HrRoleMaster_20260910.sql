/* =============================================================================
   HR Role Master
   -----------------------------------------------------------------------------
   This is NOT the V2 Parivar portal role. dbo.tblRole holds the portal/RBAC
   roles (IT Superadmin, StoreHR, ...) that drive page access; those are not
   maintained by HR and must not be edited from the Masters screens.

   dbo.tblRoleMaster is a separate list the HR team creates and maintains from
   Masters -> Role Master, and it is what the "Role" field on the employee
   profile and the candidate page picks from. It appears in the employee master
   export as its own column, next to the existing portal "Role Name" column.

   Creates:
     dbo.tblRoleMaster              - the HR-maintained role list
     dbo.tblEmployee.RoleMasterId   - the employee's HR role   (nullable)
     dbo.Candidate.RoleMasterId     - the candidate's HR role  (nullable),
                                      copied onto the employee at conversion

   Both columns are nullable and no existing row is given a value, so every
   employee and candidate already on file reads as "no HR role set".

   dbo.Candidate is SYSTEM-VERSIONED: ALTER TABLE ... ADD propagates to
   dbo.Candidate_History automatically.

   CREATE / ADD only. Nothing is dropped, deleted or truncated. Re-runnable.
   ============================================================================= */

SET NOCOUNT ON;

/* ---- 1. the HR role list ------------------------------------------------ */
IF OBJECT_ID(N'dbo.tblRoleMaster', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.tblRoleMaster
    (
        RoleMasterId  int           IDENTITY(1,1) NOT NULL,
        RoleName      varchar(100)  NOT NULL,
        Description   varchar(500)  NULL,
        IsActive      bit           NOT NULL
            CONSTRAINT DF_tblRoleMaster_IsActive DEFAULT (1),
        CreatedBy     varchar(100)  NULL,
        CreatedOn     datetime      NOT NULL
            CONSTRAINT DF_tblRoleMaster_CreatedOn DEFAULT (GETDATE()),
        LastUpdatedBy varchar(100)  NULL,
        LastUpdatedOn datetime      NULL,
        CONSTRAINT PK_tblRoleMaster PRIMARY KEY CLUSTERED (RoleMasterId),
        CONSTRAINT UQ_tblRoleMaster_RoleName UNIQUE (RoleName)
    );
    PRINT 'Created dbo.tblRoleMaster';
END
ELSE
    PRINT 'dbo.tblRoleMaster already present - skipped';

GO

/* ---- 2. the employee's HR role ------------------------------------------ */
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'dbo.tblEmployee') AND name = N'RoleMasterId')
BEGIN
    ALTER TABLE dbo.tblEmployee ADD RoleMasterId int NULL;
    PRINT 'Added dbo.tblEmployee.RoleMasterId';
END
ELSE PRINT 'dbo.tblEmployee.RoleMasterId already present - skipped';

GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_tblEmployee_RoleMaster')
BEGIN
    ALTER TABLE dbo.tblEmployee WITH NOCHECK
        ADD CONSTRAINT FK_tblEmployee_RoleMaster
        FOREIGN KEY (RoleMasterId) REFERENCES dbo.tblRoleMaster (RoleMasterId);
    PRINT 'Added FK_tblEmployee_RoleMaster';
END
ELSE PRINT 'FK_tblEmployee_RoleMaster already present - skipped';

GO

/* ---- 3. the candidate's HR role ----------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'dbo.Candidate') AND name = N'RoleMasterId')
BEGIN
    ALTER TABLE dbo.Candidate ADD RoleMasterId int NULL;
    PRINT 'Added dbo.Candidate.RoleMasterId';
END
ELSE PRINT 'dbo.Candidate.RoleMasterId already present - skipped';

GO

-- No FK on Candidate: it is a temporal table and the history side would need the
-- same reference; the app validates the role exists before it writes anyway.

/* ---- Verification ------------------------------------------------------- */
SELECT c.name AS ColumnName, t.name AS DataType, c.is_nullable AS IsNullable
FROM sys.columns c JOIN sys.types t ON t.user_type_id = c.user_type_id
WHERE c.object_id = OBJECT_ID(N'dbo.tblRoleMaster') ORDER BY c.column_id;

SELECT 'tblEmployee' AS TableName,
       COUNT(*) AS Rows,
       SUM(CASE WHEN RoleMasterId IS NULL THEN 1 ELSE 0 END) AS NoHrRoleSet
FROM dbo.tblEmployee
UNION ALL
SELECT 'Candidate', COUNT(*), SUM(CASE WHEN RoleMasterId IS NULL THEN 1 ELSE 0 END)
FROM dbo.Candidate;

SELECT COUNT(*) AS HrRolesDefined FROM dbo.tblRoleMaster;

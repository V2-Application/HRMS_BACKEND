/* V2 Parivar Role field on employee + candidate  (DEV FIRST)
   ---------------------------------------------------------------------------
   The role field added earlier pointed at dbo.tblRoleMaster (an HR-created
   list). That was the wrong target: the field is meant to hold the V2 Parivar
   role -- SuperAdmin, IT Superadmin, StoreHR, Manager and so on -- which lives
   in dbo.tblRole.

   This script is ADDITIVE ONLY. It creates a new, clearly named column and
   leaves everything already in place untouched:
     - dbo.tblRoleMaster is NOT dropped
     - tblEmployee.RoleMasterId / Candidate.RoleMasterId are NOT dropped
   Both are safe to remove later (tblRoleMaster is empty and RoleMasterId is
   NULL for every row in dev and prod), but that is a separate decision.

   RECORD-ONLY by design: this column stores the selected role for reporting.
   It does NOT grant access. Real access continues to come from
   dbo.tblEmployeeRole via the Role Assignment page, and nothing in this script
   or the code that reads this column writes tblEmployeeRole.

   Candidate is a SYSTEM-VERSIONED temporal table, so ADD COLUMN propagates to
   Candidate_History automatically -- no separate history change is needed.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

------------------------------------------------------------------ tblEmployee
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.tblEmployee') AND name = 'V2ParivarRoleId')
BEGIN
    ALTER TABLE dbo.tblEmployee ADD V2ParivarRoleId int NULL;
    PRINT 'added tblEmployee.V2ParivarRoleId';
END
ELSE PRINT 'tblEmployee.V2ParivarRoleId already present';
GO

-- WITH NOCHECK: the column is NULL everywhere, and NOCHECK keeps the statement
-- from scanning all ~66k employee rows. New writes are still validated.
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_tblEmployee_V2ParivarRole')
BEGIN
    ALTER TABLE dbo.tblEmployee WITH NOCHECK
        ADD CONSTRAINT FK_tblEmployee_V2ParivarRole
        FOREIGN KEY (V2ParivarRoleId) REFERENCES dbo.tblRole (RoleId);
    PRINT 'added FK_tblEmployee_V2ParivarRole';
END
ELSE PRINT 'FK_tblEmployee_V2ParivarRole already present';
GO

-------------------------------------------------------------------- Candidate
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.Candidate') AND name = 'V2ParivarRoleId')
BEGIN
    ALTER TABLE dbo.Candidate ADD V2ParivarRoleId int NULL;
    PRINT 'added Candidate.V2ParivarRoleId (propagates to Candidate_History)';
END
ELSE PRINT 'Candidate.V2ParivarRoleId already present';
GO

-- No FK on Candidate: it is system-versioned, and the existing RoleMasterId
-- column was added the same way. Unknown ids are rejected in the service layer.

------------------------------------------------------------------ verification
SELECT 'tblEmployee' AS TableName, c.name AS ColumnName, t.name AS DataType, c.is_nullable
FROM sys.columns c JOIN sys.types t ON t.user_type_id = c.user_type_id
WHERE c.object_id = OBJECT_ID('dbo.tblEmployee') AND c.name IN ('V2ParivarRoleId','RoleMasterId')
UNION ALL
SELECT 'Candidate', c.name, t.name, c.is_nullable
FROM sys.columns c JOIN sys.types t ON t.user_type_id = c.user_type_id
WHERE c.object_id = OBJECT_ID('dbo.Candidate') AND c.name IN ('V2ParivarRoleId','RoleMasterId')
UNION ALL
SELECT 'Candidate_History', c.name, t.name, c.is_nullable
FROM sys.columns c JOIN sys.types t ON t.user_type_id = c.user_type_id
WHERE c.object_id = OBJECT_ID('dbo.Candidate_History') AND c.name IN ('V2ParivarRoleId','RoleMasterId')
ORDER BY TableName, ColumnName;

SELECT name AS ForeignKey, is_not_trusted
FROM sys.foreign_keys WHERE name = 'FK_tblEmployee_V2ParivarRole';

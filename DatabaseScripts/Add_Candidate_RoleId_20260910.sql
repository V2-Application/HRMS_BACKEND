/* =============================================================================
   Candidate: intended portal role
   -----------------------------------------------------------------------------
   The candidate page gets the same optional Role dropdown as the employee
   profile. A candidate has no employee row yet, so the pick is parked here and
   applied when the candidate is converted into an employee
   (CandidateService.CandidateApproval, right after usp_InsertEmployeeAfterInitiate01
   creates the employee).

   NULL means "not chosen", and the existing default applies at conversion -
   nothing about the current behaviour changes for candidates already on file.

   dbo.Candidate is a SYSTEM-VERSIONED temporal table: ALTER TABLE ... ADD
   propagates the column to dbo.Candidate_History automatically, so the history
   side needs no separate statement.

   ADD COLUMN only. Nothing is dropped, deleted or truncated. Re-runnable.
   ============================================================================= */

SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.Candidate', N'U') IS NULL
BEGIN
    RAISERROR('dbo.Candidate does not exist - aborting.', 16, 1);
    RETURN;
END

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'dbo.Candidate') AND name = N'RoleId')
BEGIN
    ALTER TABLE dbo.Candidate ADD RoleId int NULL;
    PRINT 'Added dbo.Candidate.RoleId';
END
ELSE
    PRINT 'dbo.Candidate.RoleId already present - skipped';

GO

-- Verification: the column on both the current and the history table, and that
-- no existing candidate row was given a value.
SELECT t.name AS TableName, c.name AS ColumnName, ty.name AS DataType, c.is_nullable AS IsNullable
FROM sys.columns c
JOIN sys.tables t ON t.object_id = c.object_id
JOIN sys.types ty ON ty.user_type_id = c.user_type_id
WHERE t.name IN (N'Candidate', N'Candidate_History') AND c.name = N'RoleId';

SELECT COUNT(*) AS TotalCandidates,
       SUM(CASE WHEN RoleId IS NULL THEN 1 ELSE 0 END) AS NoRoleChosen
FROM dbo.Candidate;

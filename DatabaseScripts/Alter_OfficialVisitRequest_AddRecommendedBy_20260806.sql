/*
    Official Visit -- add "Recommended By" (Ecode + Name).  2026-08-06

    Captured on the apply form: who recommended/endorsed the visit, separate from the applicant
    and the approving manager. Snapshotted at apply time (same convention as Ecode/EmployeeName
    on this table) rather than joined live, since it's a free entry, not derived from the
    applicant's own employee record.

    Additive only: ALTER TABLE ADD COLUMN (nullable), guarded so it only runs if the column is
    missing. No existing row's data is touched -- existing rows simply get NULL in the new
    columns. No DROP/DELETE/UPDATE anywhere.
*/
SET NOCOUNT ON;

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.tblOfficialVisitRequest') AND name = 'RecommendedByEcode'
)
BEGIN
    ALTER TABLE dbo.tblOfficialVisitRequest ADD RecommendedByEcode NVARCHAR(50) NULL;
    PRINT 'Added column RecommendedByEcode.';
END
ELSE PRINT 'Column RecommendedByEcode already exists -- left as-is.';

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.tblOfficialVisitRequest') AND name = 'RecommendedByName'
)
BEGIN
    ALTER TABLE dbo.tblOfficialVisitRequest ADD RecommendedByName NVARCHAR(200) NULL;
    PRINT 'Added column RecommendedByName.';
END
ELSE PRINT 'Column RecommendedByName already exists -- left as-is.';

/* Allow DocumentType = 'SupersededAttachment'   (DEV FIRST)
   ---------------------------------------------------------------------------
   Uploading an NOC now REPLACES the attachment that FNF shows as
   "Resignation Attachment" (EmployeeResignationChecklistResponse.Attachment),
   so every screen and report shows the newest document -- which is the
   behaviour that was asked for.

   That table has no IsDeleted column, so the overwrite would otherwise lose the
   previous file path for good. Before overwriting, the old path is written into
   dbo.tblEmployeeInActiveFiles as a soft-deleted row tagged
   'SupersededAttachment'. Nothing is shown from those rows -- every reader
   filters IsDeleted = 0 -- but the value stays recoverable.

   ADDITIVE ONLY: this widens an existing CHECK constraint. No row is deleted,
   truncated or modified.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF EXISTS (SELECT 1 FROM sys.check_constraints
           WHERE name = 'CK_tblEmployeeInActiveFiles_DocumentType')
BEGIN
    ALTER TABLE dbo.tblEmployeeInActiveFiles
        DROP CONSTRAINT CK_tblEmployeeInActiveFiles_DocumentType;
    PRINT 'dropped the old CHECK (constraint definition only - no data touched)';
END
GO

-- WITH NOCHECK: existing rows are NULL / 'Inactivation' / 'NOC', all still valid.
ALTER TABLE dbo.tblEmployeeInActiveFiles WITH NOCHECK
    ADD CONSTRAINT CK_tblEmployeeInActiveFiles_DocumentType
    CHECK (DocumentType IS NULL
        OR DocumentType IN ('Inactivation', 'NOC', 'SupersededAttachment'));
PRINT 'recreated CHECK allowing SupersededAttachment';
GO

SELECT cc.name AS ConstraintName, cc.definition
FROM sys.check_constraints cc
WHERE cc.name = 'CK_tblEmployeeInActiveFiles_DocumentType';

SELECT ISNULL(DocumentType, '(NULL)') AS DocumentType, COUNT(*) AS Rows_
FROM dbo.tblEmployeeInActiveFiles WITH (NOLOCK)
GROUP BY ISNULL(DocumentType, '(NULL)');

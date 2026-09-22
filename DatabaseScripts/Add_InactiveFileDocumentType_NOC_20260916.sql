/* NOC attachment for inactive employees  (DEV FIRST)
   ---------------------------------------------------------------------------
   Inactive-employee attachments already live in dbo.tblEmployeeInActiveFiles
   (6,164 rows on dev). Every row today is an attachment captured at the moment
   of inactivation, and nothing distinguishes one kind of document from another.

   To support an NOC upload we only need to be able to TAG a file, so this adds
   a single nullable column rather than a new table. That keeps every existing
   query working untouched: a row with DocumentType NULL or 'Inactivation' means
   exactly what it has always meant.

   ADDITIVE ONLY:
     - no table is dropped or truncated
     - no existing row is modified or deleted
     - the column is NULL for all 6,164 existing rows and is back-filled to
       'Inactivation' ONLY as a default for new rows, never by an UPDATE here

   A CHECK constraint keeps the values closed so the column cannot drift into
   free text. Add new document types to the constraint when they are needed.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

------------------------------------------------------- DocumentType column
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.tblEmployeeInActiveFiles')
                 AND name = 'DocumentType')
BEGIN
    -- Nullable, with a default for NEW rows only. Existing rows stay NULL,
    -- which every current query already treats as "an inactivation file".
    ALTER TABLE dbo.tblEmployeeInActiveFiles
        ADD DocumentType nvarchar(50) NULL
            CONSTRAINT DF_tblEmployeeInActiveFiles_DocumentType DEFAULT ('Inactivation');
    PRINT 'added tblEmployeeInActiveFiles.DocumentType';
END
ELSE PRINT 'tblEmployeeInActiveFiles.DocumentType already present';
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints
               WHERE name = 'CK_tblEmployeeInActiveFiles_DocumentType')
BEGIN
    -- WITH NOCHECK: existing rows are NULL and are explicitly allowed by the
    -- constraint anyway; NOCHECK simply avoids scanning them on creation.
    ALTER TABLE dbo.tblEmployeeInActiveFiles WITH NOCHECK
        ADD CONSTRAINT CK_tblEmployeeInActiveFiles_DocumentType
        CHECK (DocumentType IS NULL OR DocumentType IN ('Inactivation','NOC'));
    PRINT 'added CK_tblEmployeeInActiveFiles_DocumentType';
END
ELSE PRINT 'CK_tblEmployeeInActiveFiles_DocumentType already present';
GO

-- Lookups are always "this employee's files", and the NOC screen filters by
-- type, so index the pair.
-- NOT filtered: a filtered index predicate cannot use ISNULL(), and IsDeleted
-- is a nullable bit, so "live rows" cannot be expressed cleanly here. The table
-- is small (6k rows on dev) so an unfiltered index costs nothing.
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_tblEmployeeInActiveFiles_Emp_DocType'
                 AND object_id = OBJECT_ID('dbo.tblEmployeeInActiveFiles'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_tblEmployeeInActiveFiles_Emp_DocType
        ON dbo.tblEmployeeInActiveFiles (EmpId, DocumentType)
        INCLUDE (FilePath, CreatedOn, CreatedBy, IsDeleted);
    PRINT 'added IX_tblEmployeeInActiveFiles_Emp_DocType';
END
ELSE PRINT 'IX_tblEmployeeInActiveFiles_Emp_DocType already present';
GO

------------------------------------------------------------- verification
SELECT c.name AS ColumnName, t.name AS DataType, c.is_nullable,
       OBJECT_DEFINITION(c.default_object_id) AS DefaultValue
FROM sys.columns c JOIN sys.types t ON t.user_type_id = c.user_type_id
WHERE c.object_id = OBJECT_ID('dbo.tblEmployeeInActiveFiles') AND c.name = 'DocumentType';

SELECT COUNT(*) AS TotalRows,
       SUM(CASE WHEN DocumentType IS NULL THEN 1 ELSE 0 END) AS ExistingRowsLeftUntouched,
       SUM(CASE WHEN DocumentType = 'NOC' THEN 1 ELSE 0 END) AS NocRows
FROM dbo.tblEmployeeInActiveFiles WITH (NOLOCK);

/* =====================================================================
   PROD — backup of everything the Aug-2026 medical-card import will change.
   Generated 2026-09-07. Server 192.168.151.28\hrms, database HRMS.

   WHY: the import attaches the new Aditya Birla / Volo e-cards to 1,813
   employees. For each one it overwrites tblEmployee.MedicalCardUrl and
   REPLACES that employee's rows in tblEmployee_MedicalCard with the cards
   parsed out of the new PDF (that replace-per-employee is how the parser has
   always worked). The old PDFs stay on disk untouched, but the two tables
   above need a snapshot so the change can be undone.

   SCOPE: additive only. Two new bk_ tables are CREATED. Nothing is dropped,
   deleted or truncated, and no existing row is modified by this script.

   ROLLBACK, if ever needed (run both, in this order):

     UPDATE e SET e.MedicalCardUrl = b.MedicalCardUrl
     FROM dbo.tblEmployee e
     JOIN dbo.bk_tblEmployee_MedicalCardUrl_20260907 b ON b.EmployeeId = e.EmployeeId;

     DELETE c FROM dbo.tblEmployee_MedicalCard c
     WHERE EXISTS (SELECT 1 FROM dbo.bk_tblEmployee_MedicalCard_20260907 b
                   WHERE b.EmployeeId = c.EmployeeId);
     SET IDENTITY_INSERT dbo.tblEmployee_MedicalCard ON;
     INSERT INTO dbo.tblEmployee_MedicalCard
           (Id, EmployeeId, Ecode, CardOrder, UhidNo, HolderName, Age, Gender,
            PlanValidFrom, PlanValidTo, PolicyNo, Organisation, Insurer, Tpa,
            SumAssured, SourcePdfUrl, RawText, CreatedOn, CreatedBy, UpdatedOn, UpdatedBy)
     SELECT Id, EmployeeId, Ecode, CardOrder, UhidNo, HolderName, Age, Gender,
            PlanValidFrom, PlanValidTo, PolicyNo, Organisation, Insurer, Tpa,
            SumAssured, SourcePdfUrl, RawText, CreatedOn, CreatedBy, UpdatedOn, UpdatedBy
     FROM dbo.bk_tblEmployee_MedicalCard_20260907;
     SET IDENTITY_INSERT dbo.tblEmployee_MedicalCard OFF;
   ===================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @urlRows int, @cardRows int, @sumAssuredRows int;

BEGIN TRY
BEGIN TRAN;

/* ---------- 1) every employee's current MedicalCardUrl ---------- */
/* Whole-column snapshot rather than just the 1,813 in the archive: it is only
   ~2,200 populated rows, and it means a rollback cannot miss anyone. */
IF OBJECT_ID('dbo.bk_tblEmployee_MedicalCardUrl_20260907') IS NULL
    SELECT EmployeeId, Ecode, MedicalCardUrl
    INTO dbo.bk_tblEmployee_MedicalCardUrl_20260907
    FROM dbo.tblEmployee;

SELECT @urlRows = COUNT(*) FROM dbo.bk_tblEmployee_MedicalCardUrl_20260907;
PRINT 'bk_tblEmployee_MedicalCardUrl_20260907 rows: ' + CAST(@urlRows AS varchar(20));

/* ---------- 2) every existing parsed card row ---------- */
IF OBJECT_ID('dbo.bk_tblEmployee_MedicalCard_20260907') IS NULL
    SELECT *
    INTO dbo.bk_tblEmployee_MedicalCard_20260907
    FROM dbo.tblEmployee_MedicalCard;

SELECT @cardRows = COUNT(*) FROM dbo.bk_tblEmployee_MedicalCard_20260907;
PRINT 'bk_tblEmployee_MedicalCard_20260907 rows: ' + CAST(@cardRows AS varchar(20));

/* The hand-entered Sum Assured values are the only data here a user typed in
   rather than the parser producing, so call them out explicitly. */
SELECT @sumAssuredRows = COUNT(*) FROM dbo.bk_tblEmployee_MedicalCard_20260907 WHERE SumAssured IS NOT NULL;
PRINT '  ...of which carry a hand-entered SumAssured: ' + CAST(@sumAssuredRows AS varchar(20));

COMMIT TRAN;
PRINT 'DONE - COMMITTED (backup only, no existing data changed)';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRAN;
    PRINT 'ROLLED BACK - no changes applied: ' + ERROR_MESSAGE();
    THROW;
END CATCH
GO

/* ---------- verification ---------- */
SELECT 'bk_tblEmployee_MedicalCardUrl_20260907' AS BackupTable, COUNT(*) AS Rows_,
       SUM(CASE WHEN MedicalCardUrl IS NOT NULL THEN 1 ELSE 0 END) AS WithUrl
FROM dbo.bk_tblEmployee_MedicalCardUrl_20260907
UNION ALL
SELECT 'bk_tblEmployee_MedicalCard_20260907', COUNT(*),
       SUM(CASE WHEN SumAssured IS NOT NULL THEN 1 ELSE 0 END)
FROM dbo.bk_tblEmployee_MedicalCard_20260907;
-- expect: 2173 WithUrl, and 4311 card rows of which 4 carry a SumAssured

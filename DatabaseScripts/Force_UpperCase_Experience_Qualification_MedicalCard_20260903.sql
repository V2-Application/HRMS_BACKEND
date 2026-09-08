/* =====================================================================
   Force UPPERCASE on Experience / Qualification / Medical Card
   Generated 2026-09-03. Companion to Force_UpperCase_Candidate_Employee_20260903.sql
   (which covered Candidate + tblEmployee). Schemas verified identical on dev and prod.

   SAFETY: UPDATE-only, ONE transaction, plus three additive SELECT INTO backup
   tables. NOTHING is dropped, truncated or altered. Any error rolls it all back.

   NOT uppercased, and why:
     SourcePdfUrl - the medical-card PDF link. Case-sensitive on the web server,
                    so uppercasing it 404s every card PDF.
     RawText      - raw OCR text the card parser reads. Uppercasing risks breaking
                    any case-sensitive extraction of UHID / policy numbers.
     CreatedBy / UpdatedBy - audit values, consistent with the earlier script.

   ROLLBACK after commit, if ever needed (example for one table):
     UPDATE t SET t.[Name of Company] = b.[Name of Company],
                  t.[Work Location]   = b.[Work Location],
                  t.[Position Held]   = b.[Position Held]
     FROM dbo.tblExperience t
     JOIN dbo.bk_tblExperience_UpperCase_20260903 b ON b.Id = t.Id;
   ===================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @bkRows int;   -- PRINT cannot take a subquery, so count into a variable first

BEGIN TRY
BEGIN TRAN;

/* ---------- dbo.tblExperience ---------- */
IF OBJECT_ID('dbo.bk_tblExperience_UpperCase_20260903') IS NULL
    SELECT * INTO dbo.bk_tblExperience_UpperCase_20260903 FROM dbo.tblExperience;
SELECT @bkRows = COUNT(*) FROM dbo.bk_tblExperience_UpperCase_20260903;
PRINT 'backup dbo.bk_tblExperience_UpperCase_20260903 rows: ' + CAST(@bkRows AS varchar(20));

UPDATE dbo.tblExperience SET
    [Name of Company] = UPPER([Name of Company]),
    [Work Location]   = UPPER([Work Location]),
    [Position Held]   = UPPER([Position Held])
;
PRINT 'dbo.tblExperience uppercased: ' + CAST(@@ROWCOUNT AS varchar(20)) + ' rows over 3 columns';

/* ---------- dbo.tblQualification ---------- */
IF OBJECT_ID('dbo.bk_tblQualification_UpperCase_20260903') IS NULL
    SELECT * INTO dbo.bk_tblQualification_UpperCase_20260903 FROM dbo.tblQualification;
SELECT @bkRows = COUNT(*) FROM dbo.bk_tblQualification_UpperCase_20260903;
PRINT 'backup dbo.bk_tblQualification_UpperCase_20260903 rows: ' + CAST(@bkRows AS varchar(20));

UPDATE dbo.tblQualification SET
    [Education] = UPPER([Education]),
    [YOP]       = UPPER([YOP]),
    [Grade]     = UPPER([Grade]),
    [Type]      = UPPER([Type])
;
PRINT 'dbo.tblQualification uppercased: ' + CAST(@@ROWCOUNT AS varchar(20)) + ' rows over 4 columns';

/* ---------- dbo.tblEmployee_MedicalCard ---------- */
IF OBJECT_ID('dbo.bk_tblEmployee_MedicalCard_UpperCase_20260903') IS NULL
    SELECT * INTO dbo.bk_tblEmployee_MedicalCard_UpperCase_20260903 FROM dbo.tblEmployee_MedicalCard;
SELECT @bkRows = COUNT(*) FROM dbo.bk_tblEmployee_MedicalCard_UpperCase_20260903;
PRINT 'backup dbo.bk_tblEmployee_MedicalCard_UpperCase_20260903 rows: ' + CAST(@bkRows AS varchar(20));

UPDATE dbo.tblEmployee_MedicalCard SET
    [Ecode]        = UPPER([Ecode]),
    [UhidNo]       = UPPER([UhidNo]),
    [HolderName]   = UPPER([HolderName]),
    [Gender]       = UPPER([Gender]),
    [PolicyNo]     = UPPER([PolicyNo]),
    [Organisation] = UPPER([Organisation]),
    [Insurer]      = UPPER([Insurer]),
    [Tpa]          = UPPER([Tpa])
;
PRINT 'dbo.tblEmployee_MedicalCard uppercased: ' + CAST(@@ROWCOUNT AS varchar(20)) + ' rows over 8 columns';

COMMIT TRAN;
PRINT 'DONE - COMMITTED';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRAN;
    PRINT 'ROLLED BACK - no changes applied: ' + ERROR_MESSAGE();
    THROW;
END CATCH

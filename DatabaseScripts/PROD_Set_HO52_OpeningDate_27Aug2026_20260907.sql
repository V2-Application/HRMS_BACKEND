/* =====================================================================
   PROD — set the Opening Date of store HO52 (MALKANGIRI) to 27 Aug 2026
   Generated 2026-09-07. Server 192.168.151.28\hrms, database HRMS.

   Supersedes PROD_Fix_HO52_OpeningDate_20260906.sql, which set 1 Sept 2026
   on the information available then. Corrected date supplied 2026-09-07.

   VALUE HISTORY for LocationId 3107 / HO52:
       '46174'                 <- as uploaded: a raw Excel serial = 1 Jun 2026
       '9/1/2026 12:00:00 AM'  <- set 2026-09-06
       '8/27/2026 12:00:00 AM' <- this script

   FORMAT: '27/08/2026' as typed is dd/MM/yyyy, but tblLocation.OpeningDate
   is nvarchar and the ~700 other populated rows all use M/d/yyyy 12:00:00 AM
   (month first, e.g. '11/16/2025 12:00:00 AM'). Storing '27/08/2026' would
   make HO52 the only row in a different order and month-first parsers would
   choke on month 27. So 27 Aug 2026 is written as '8/27/2026 12:00:00 AM'.

   SCOPE: LocationId 3107 (HO52) ONLY.
   Still holding the raw serial 46174 (= 1 Jun 2026) from the same upload,
   untouched pending correct dates:
       3104  HF03  THRISSUR (HLITE MALL RD)   IsActive = 0
       3105  HK33  MANGALURU                  IsActive = 1   <- live
       3106  HK34  GADAG                      IsActive = 0

   SAFETY: one-row UPDATE in a single transaction, plus an additive backup
   of the value as it stands right now. NOTHING is dropped or truncated.
   The 20260906 backup table is left alone — it still holds the ORIGINAL
   '46174', so the upload-time value remains recoverable too.

   ROLLBACK to the pre-script value:
     UPDATE l SET l.OpeningDate = b.OpeningDate
     FROM dbo.tblLocation l
     JOIN dbo.bk_tblLocation_HO52_OpeningDate_20260907 b ON b.LocationId = l.LocationId;
   ===================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @bkRows int, @before nvarchar(100);

BEGIN TRY
BEGIN TRAN;

/* ---------- pre-flight ---------- */
IF NOT EXISTS (SELECT 1 FROM dbo.tblLocation WHERE LocationId = 3107 AND STCode = 'HO52')
    THROW 50011, 'LocationId 3107 is not HO52 on this server. Stop and re-check.', 1;

SELECT @before = OpeningDate FROM dbo.tblLocation WHERE LocationId = 3107;
PRINT 'value before: ' + ISNULL(@before, '(null)');

/* ---------- backup (additive) ---------- */
IF OBJECT_ID('dbo.bk_tblLocation_HO52_OpeningDate_20260907') IS NULL
    SELECT LocationId, STCode, LocationName, OpeningDate
    INTO dbo.bk_tblLocation_HO52_OpeningDate_20260907
    FROM dbo.tblLocation
    WHERE LocationId = 3107;

SELECT @bkRows = COUNT(*) FROM dbo.bk_tblLocation_HO52_OpeningDate_20260907;
PRINT 'backup dbo.bk_tblLocation_HO52_OpeningDate_20260907 rows: ' + CAST(@bkRows AS varchar(20));

/* ---------- the change ---------- */
UPDATE dbo.tblLocation
SET OpeningDate = '8/27/2026 12:00:00 AM'
WHERE LocationId = 3107 AND STCode = 'HO52';

PRINT 'tblLocation rows updated: ' + CAST(@@ROWCOUNT AS varchar(20));

COMMIT TRAN;
PRINT 'DONE - COMMITTED';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRAN;
    PRINT 'ROLLED BACK - no changes applied: ' + ERROR_MESSAGE();
    THROW;
END CATCH
GO

/* ---------- verification ---------- */
SELECT LocationId, STCode, LocationName, OpeningDate,
       CONVERT(varchar(30), TRY_CONVERT(datetime, OpeningDate, 101), 106) AS ParsedAs_dd_Mon_yyyy,
       IsActive
FROM dbo.tblLocation WHERE STCode = 'HO52';
-- expect: OpeningDate = 8/27/2026 12:00:00 AM   ->   ParsedAs = 27 Aug 2026

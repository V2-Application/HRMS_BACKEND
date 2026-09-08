/* =====================================================================
   PROD — set the Opening Date of store HO52 (MALKANGIRI) to 1 Sept 2026
   Generated 2026-09-06. Server 192.168.151.28\hrms, database HRMS.

   WHAT IS THERE NOW: tblLocation.OpeningDate is nvarchar, and HO52 holds
   the raw Excel serial '46174' — which decodes to 1 JUNE 2026, not a
   missing value. The grid shows "46174" because the uploader stored the
   spreadsheet's underlying number instead of the formatted date.

   FORMAT: the ~700 other populated rows use  M/d/yyyy 12:00:00 AM
   (e.g. '11/16/2025 12:00:00 AM', '5/11/2023 12:00:00 AM'), month first.
   So 1 Sept 2026 is written as '9/1/2026 12:00:00 AM' — writing a serial,
   or an ISO/dd-MM string, would leave HO52 inconsistent with every other
   store and could break whatever parses this column.

   SCOPE: LocationId 3107 (HO52) ONLY, as requested.
   NOT touched, but broken the same way — all from the same upload batch,
   all holding 46174 = 1 June 2026:
       3104  HF03  THRISSUR (HLITE MALL RD)   IsActive = 0
       3105  HK33  MANGALURU                  IsActive = 1
       3106  HK34  GADAG                      IsActive = 0
   Say the word and these get the same treatment with their correct dates.

   SAFETY: one-row UPDATE in a single transaction, plus an additive
   SELECT INTO backup. NOTHING is dropped or truncated.

   ROLLBACK after commit, if ever needed:
     UPDATE l SET l.OpeningDate = b.OpeningDate
     FROM dbo.tblLocation l
     JOIN dbo.bk_tblLocation_HO52_OpeningDate_20260906 b ON b.LocationId = l.LocationId;
   ===================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @bkRows int;

BEGIN TRY
BEGIN TRAN;

/* ---------- pre-flight ---------- */
IF NOT EXISTS (SELECT 1 FROM dbo.tblLocation WHERE LocationId = 3107 AND STCode = 'HO52')
    THROW 50010, 'LocationId 3107 is not HO52 on this server. Stop and re-check.', 1;

/* ---------- backup (additive) ---------- */
IF OBJECT_ID('dbo.bk_tblLocation_HO52_OpeningDate_20260906') IS NULL
    SELECT LocationId, STCode, LocationName, OpeningDate
    INTO dbo.bk_tblLocation_HO52_OpeningDate_20260906
    FROM dbo.tblLocation
    WHERE LocationId = 3107;

SELECT @bkRows = COUNT(*) FROM dbo.bk_tblLocation_HO52_OpeningDate_20260906;
PRINT 'backup dbo.bk_tblLocation_HO52_OpeningDate_20260906 rows: ' + CAST(@bkRows AS varchar(20));

/* ---------- the change ---------- */
UPDATE dbo.tblLocation
SET OpeningDate = '9/1/2026 12:00:00 AM'
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
SELECT LocationId, STCode, LocationName, OpeningDate, IsActive
FROM dbo.tblLocation WHERE STCode = 'HO52';
-- expect: OpeningDate = 9/1/2026 12:00:00 AM

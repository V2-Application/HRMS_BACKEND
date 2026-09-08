/* =====================================================================
   PROD — set the Opening Date of store HK33 (MANGALURU) to 31 Jul 2026
   Generated 2026-09-07. Server 192.168.151.28\hrms, database HRMS.

   Companion to PROD_Set_HO52_OpeningDate_27Aug2026_20260907.sql. HK33 came
   from the same Location Master upload and still holds the raw Excel serial
   '46174', which decodes to 1 JUNE 2026 — a real but wrong date, not a
   blank. HK33 is IsActive = 1, so this one is live.

   FORMAT: '31/07/2026' as typed is dd/MM/yyyy, but tblLocation.OpeningDate
   is nvarchar and the ~700 other populated rows all use M/d/yyyy 12:00:00 AM
   (month first, e.g. '11/16/2025 12:00:00 AM'). Storing '31/07/2026' would
   leave HK33 the only row in a different order and a month-first parser
   would see month 31. So 31 Jul 2026 is written '7/31/2026 12:00:00 AM'.

   SCOPE: LocationId 3105 (HK33) ONLY.
   Still holding the raw serial 46174 (= 1 Jun 2026), awaiting correct dates:
       3104  HF03  THRISSUR (HLITE MALL RD)   IsActive = 0
       3106  HK34  GADAG                      IsActive = 0

   SAFETY: one-row UPDATE in a single transaction, plus an additive backup
   of the value as it stands right now. NOTHING is dropped or truncated.

   ROLLBACK after commit, if ever needed:
     UPDATE l SET l.OpeningDate = b.OpeningDate
     FROM dbo.tblLocation l
     JOIN dbo.bk_tblLocation_HK33_OpeningDate_20260907 b ON b.LocationId = l.LocationId;
   ===================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @bkRows int, @before nvarchar(100);

BEGIN TRY
BEGIN TRAN;

/* ---------- pre-flight ---------- */
IF NOT EXISTS (SELECT 1 FROM dbo.tblLocation WHERE LocationId = 3105 AND STCode = 'HK33')
    THROW 50012, 'LocationId 3105 is not HK33 on this server. Stop and re-check.', 1;

SELECT @before = OpeningDate FROM dbo.tblLocation WHERE LocationId = 3105;
PRINT 'value before: ' + ISNULL(@before, '(null)');

/* ---------- backup (additive) ---------- */
IF OBJECT_ID('dbo.bk_tblLocation_HK33_OpeningDate_20260907') IS NULL
    SELECT LocationId, STCode, LocationName, OpeningDate
    INTO dbo.bk_tblLocation_HK33_OpeningDate_20260907
    FROM dbo.tblLocation
    WHERE LocationId = 3105;

SELECT @bkRows = COUNT(*) FROM dbo.bk_tblLocation_HK33_OpeningDate_20260907;
PRINT 'backup dbo.bk_tblLocation_HK33_OpeningDate_20260907 rows: ' + CAST(@bkRows AS varchar(20));

/* ---------- the change ---------- */
UPDATE dbo.tblLocation
SET OpeningDate = '7/31/2026 12:00:00 AM'
WHERE LocationId = 3105 AND STCode = 'HK33';

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

/* ---------- verification: both fixed stores, plus what is still outstanding ---------- */
SELECT LocationId, STCode, LocationName, OpeningDate,
       CONVERT(varchar(30), TRY_CONVERT(datetime, OpeningDate, 101), 106) AS ParsedAs_dd_Mon_yyyy,
       IsActive
FROM dbo.tblLocation
WHERE LocationId IN (3104, 3105, 3106, 3107)
ORDER BY LocationId;
-- expect: HK33 = 7/31/2026 -> 31 Jul 2026,  HO52 = 8/27/2026 -> 27 Aug 2026
--         HF03 and HK34 still showing the raw serial 46174

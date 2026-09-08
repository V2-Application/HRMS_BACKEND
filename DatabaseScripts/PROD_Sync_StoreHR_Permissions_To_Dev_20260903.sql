/* =====================================================================
   PROD — Sync the remaining StoreHR (RoleId 8) permissions to match dev
   Generated 2026-09-03. Run on 192.168.151.28\hrms, database HRMS.

   CONTEXT: a full row-by-row diff of RBACNode RoleId 8 between
   192.168.151.27\KARMA (dev) and prod found 27 differences. They fall
   into three groups:

     1. RBACNode Id 216 — Candidate List / Initiate.
        ALREADY PUSHED by PROD_Enable_StoreHR_Initiate_Action_20260903.sql.
        Not touched here.

     2. THE THREE ROWS THIS SCRIPT CHANGES (dev = granted, prod = denied):
              Id 181  Action     RefId 3   Edit             -> Employees Master
              Id 245  SubModule  RefId 27  Bgt Seat Master  -> Uploaders
              Id 8221 SubModule  RefId 88  Geofence Request -> Attendance

     3. 24 rows that exist ONLY ON PROD (Modules 24-27, SubModules
        123-138, Actions 158-161). Prod is AHEAD of dev on menu
        structure here — there is nothing to push, and this script does
        NOT delete or alter them.

   *** THIS WIDENS ACCESS FOR EVERY STORE HR USER ON PROD ***
   In particular Id 181 grants EDIT on Employees Master, i.e. store
   users become able to modify employee records. Explicitly requested
   2026-09-03 ("push all 3 as well") after the impact was set out.
   These three are UNRELATED to the Move-to-Pending feature.

   VERIFIED ON PROD BEFORE WRITING THIS SCRIPT:
     * all three nodes exist with RoleId = 8 and IsChecked = 0
     * every PARENT node is already IsChecked = 1, so flipping these
       three actually takes effect (a checked child under an unchecked
       parent would have changed nothing)
     * tblRbacNodeAccessLock has 0 rows — nothing blocks these flags

   SAFETY: three single-row UPDATEs in one transaction, plus an additive
   SELECT INTO backup of exactly those rows. NOTHING is dropped,
   truncated or altered. Any error rolls the whole thing back.

   ROLLBACK after commit, if ever needed:
     UPDATE n SET n.IsChecked = b.IsChecked
     FROM dbo.RBACNode n
     JOIN dbo.bk_RBACNode_StoreHrPermSync_20260903 b ON b.Id = n.Id;
   ===================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @bkRows int;   -- PRINT cannot take a subquery, so count into a variable first

BEGIN TRY
BEGIN TRAN;

/* ---------- pre-flight: refuse to run against unexpected nodes ---------- */
IF (SELECT COUNT(*) FROM dbo.RBACNode
    WHERE Id IN (181, 245, 8221) AND RoleId = 8
      AND ((Id = 181  AND NodeType = 'Action'    AND RefId = 3)
        OR (Id = 245  AND NodeType = 'SubModule' AND RefId = 27)
        OR (Id = 8221 AND NodeType = 'SubModule' AND RefId = 88))) <> 3
    THROW 50003, 'RBACNode Ids 181/245/8221 are not the expected StoreHR nodes on this server. Stop and re-diff before running.', 1;

/* ---------- backup (additive) ---------- */
IF OBJECT_ID('dbo.bk_RBACNode_StoreHrPermSync_20260903') IS NULL
    SELECT *
    INTO dbo.bk_RBACNode_StoreHrPermSync_20260903
    FROM dbo.RBACNode
    WHERE Id IN (181, 245, 8221);

SELECT @bkRows = COUNT(*) FROM dbo.bk_RBACNode_StoreHrPermSync_20260903;
PRINT 'backup dbo.bk_RBACNode_StoreHrPermSync_20260903 rows: ' + CAST(@bkRows AS varchar(20));

/* ---------- the change ---------- */
UPDATE dbo.RBACNode
SET IsChecked = 1,
    UpdatedOn = GETDATE(),
    UpdatedBy = 'Sync StoreHR perms to dev 20260903'
WHERE Id IN (181, 245, 8221) AND RoleId = 8 AND ISNULL(IsChecked, 0) = 0;

PRINT 'RBACNode rows updated (StoreHR perm sync): ' + CAST(@@ROWCOUNT AS varchar(20));

COMMIT TRAN;
PRINT 'DONE - COMMITTED';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRAN;
    PRINT 'ROLLED BACK - no changes applied: ' + ERROR_MESSAGE();
    THROW;
END CATCH
GO

/* ---------- verification: run after the commit ---------- */
SELECT n.Id, n.NodeType, n.RefId, CAST(n.IsChecked AS int) AS IsChecked, n.UpdatedBy
FROM dbo.RBACNode n
WHERE n.Id IN (181, 216, 245, 8221)
ORDER BY n.Id;
-- expect: IsChecked = 1 on all four (216 = the Initiate flag pushed earlier)

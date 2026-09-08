/* =====================================================================
   Enable the "Initiate" action on Candidate List for the StoreHR role
   Generated 2026-09-03.

   WHY: Store HR must be able to pull a REJECTED candidate back to Pending.
   The action icon on /candidate/form_list is gated by
   actionsMap?.initiate?.actionStatus, which comes from RBACNode. For
   RoleId 8 (StoreHR) the Initiate action (ActionMaster.Id = 19, SubModule 10
   "Candidate List") is IsChecked = 0, so no icon renders at all.

   Store HR still cannot approve or reject: the modal hides those options
   (revertToPendingOnly) AND CandidateService.CandidateInitiate refuses any
   status other than Pending from the StoreHR role. This flag only makes the
   icon reachable.

   SAFETY: single-row UPDATE inside one transaction, plus an additive
   SELECT INTO backup of the affected RBACNode rows. NOTHING is dropped,
   truncated or altered.

   ROLLBACK after commit, if ever needed:
     UPDATE n SET n.IsChecked = b.IsChecked
     FROM dbo.RBACNode n
     JOIN dbo.bk_RBACNode_StoreHrInitiate_20260903 b ON b.Id = n.Id;
   ===================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @bkRows int;   -- PRINT cannot take a subquery, so count into a variable first

BEGIN TRY
BEGIN TRAN;

/* ---------- backup (additive) ---------- */
IF OBJECT_ID('dbo.bk_RBACNode_StoreHrInitiate_20260903') IS NULL
    SELECT *
    INTO dbo.bk_RBACNode_StoreHrInitiate_20260903
    FROM dbo.RBACNode
    WHERE RoleId = 8 AND NodeType = 'Action' AND RefId = 19;

SELECT @bkRows = COUNT(*) FROM dbo.bk_RBACNode_StoreHrInitiate_20260903;
PRINT 'backup dbo.bk_RBACNode_StoreHrInitiate_20260903 rows: ' + CAST(@bkRows AS varchar(20));

/* ---------- the change ---------- */
UPDATE dbo.RBACNode
SET IsChecked = 1,
    UpdatedOn = GETDATE(),
    UpdatedBy = 'Enable StoreHR Move-to-Pending 20260903'
WHERE RoleId = 8 AND NodeType = 'Action' AND RefId = 19 AND ISNULL(IsChecked, 0) = 0;

PRINT 'RBACNode rows updated (StoreHR / Initiate): ' + CAST(@@ROWCOUNT AS varchar(20));

COMMIT TRAN;
PRINT 'DONE - COMMITTED';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRAN;
    PRINT 'ROLLED BACK - no changes applied: ' + ERROR_MESSAGE();
    THROW;
END CATCH

/* =====================================================================
   PROD — Enable the "Initiate" action on Candidate List for StoreHR
   Generated 2026-09-03. Run on 192.168.151.28\hrms, database HRMS.
   Dev twin: Enable_StoreHR_Initiate_Action_20260903.sql (already applied
   and verified on 192.168.151.27\KARMA).

   WHY: Store HR must be able to pull a REJECTED candidate back to Pending.
   The action icon on /candidate/form_list is gated by
   actionsMap?.initiate?.actionStatus, sourced from RBACNode. For RoleId 8
   (StoreHR) the Initiate action (ActionMaster.Id = 19, SubModule 10
   "Candidate List") is IsChecked = 0, so no icon renders at all.

   VERIFIED ON PROD BEFORE WRITING THIS SCRIPT:
     * tblRole            RoleId 8 = 'StoreHR', IsActive = 1
     * ActionMaster       Id 19 = 'Initiate', SubModuleId 10, IsActive = 1
     * RBACNode           Id 216 (RoleId 8 / Action / RefId 19), IsChecked = 0
     * tblRbacNodeAccessLock  0 rows — nothing blocks this flag
     * CandidateStatus_History.Remarks column EXISTS (the new code writes it)
   Object ids are identical to dev, so this touches exactly one row.

   SCOPE NOTE: prod currently has 333 rejected candidates in stores that
   have a StoreHR login (4,640 rejected overall). Every one of those 333
   becomes revertible by that store's HR the moment this is committed AND
   the new backend is deployed.

   THIS FLAG ALONE DOES NOT GRANT APPROVE/REJECT. Store HR still cannot
   approve or reject:
     * the icon only renders on candidates whose status is Rejected;
     * the modal hides Approve / Reject / Revoke for StoreHR;
     * CandidateService.CandidateInitiate returns 403 for any status other
       than Pending submitted by the StoreHR role.
   Until the new backend is deployed this flag makes the icon appear but
   the action returns 403 "Unauthorized role for this operation", so run
   this script TOGETHER WITH the backend deployment, not before it.

   SAFETY: single-row UPDATE inside one transaction, plus an additive
   SELECT INTO backup of the affected RBACNode row. NOTHING is dropped,
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

/* ---------- pre-flight: fail loudly rather than silently doing nothing ---------- */
IF NOT EXISTS (SELECT 1 FROM dbo.RBACNode WHERE Id = 216 AND RoleId = 8 AND NodeType = 'Action' AND RefId = 19)
    THROW 50001, 'RBACNode Id 216 is not the StoreHR/Initiate node on this server. Stop and re-check before running.', 1;

IF COL_LENGTH('dbo.CandidateStatus_History', 'Remarks') IS NULL
    THROW 50002, 'CandidateStatus_History.Remarks is missing. Apply Add_CandidateStatusHistory_Remarks_20260902.sql first, or the new approval code will fail on every rejection.', 1;

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
GO

/* ---------- verification: run after the commit ---------- */
SELECT Id, RoleId, NodeType, RefId, IsChecked, UpdatedBy, UpdatedOn
FROM dbo.RBACNode
WHERE RoleId = 8 AND NodeType = 'Action' AND RefId = 19;
-- expect: IsChecked = 1

SELECT DISTINCT 'fn_GetRbacHierarchyByRole' AS MenuSource, ActionName, ActionStatus
FROM dbo.fn_GetRbacHierarchyByRole(8) WHERE SubModuleName = 'Candidate List' AND ActionId = 19
UNION ALL
SELECT DISTINCT 'vw_RBACHierarchy', ActionName, ActionStatus
FROM dbo.vw_RBACHierarchy WHERE RoleId = 8 AND SubModuleName = 'Candidate List' AND ActionRefId = 19;
-- expect: ActionStatus = 1 from BOTH sources (login uses the function,
-- the SignalR permission refresh uses the view)

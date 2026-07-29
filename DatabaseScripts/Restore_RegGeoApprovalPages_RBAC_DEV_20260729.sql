/*
    DEV ONLY — restore approval-PAGE access that was over-unchecked:
      SubModule 19 'Regularize Request' and SubModule 88 'Geofence Request'
    back to their pre-uncheck state (from backup bk_RBACNode_RegGeoUncheck_20260729),
    so managers / LP / approvers can open the approval queues again.
    The employee Regularize button (Action 107) stays UNCHECKED (window-gated).
    Toggle only; no deletes.
*/
SET NOCOUNT ON;
BEGIN TRY
    BEGIN TRAN;

    UPDATE n
    SET n.IsChecked = b.IsChecked,
        n.UpdatedBy = 'rbac-restore-approval-20260729',
        n.UpdatedOn = GETDATE()
    FROM dbo.RBACNode n
    JOIN dbo.bk_RBACNode_RegGeoUncheck_20260729 b ON b.Id = n.Id
    WHERE b.NodeType = 'SubModule' AND b.RefId IN (19, 88);

    SELECT NodeType, RefId,
           SUM(CASE WHEN IsChecked=1 THEN 1 ELSE 0 END) AS CheckedRoles, COUNT(*) AS TotalRows
    FROM dbo.RBACNode
    WHERE (NodeType='SubModule' AND RefId IN (19,88)) OR (NodeType='Action' AND RefId=107)
    GROUP BY NodeType, RefId ORDER BY NodeType, RefId;

    COMMIT TRAN;
    PRINT 'Restored approval-page access (SubModule 19 & 88); Action 107 left unchecked.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT>0 ROLLBACK TRAN;
    PRINT 'ERROR: ' + ERROR_MESSAGE();
    THROW;
END CATCH

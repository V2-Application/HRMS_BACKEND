/*
    DEV ONLY — uncheck Regularize (Action 107), Regularize Request (SubModule 19),
    and Geofence Request (SubModule 88) from ALL roles in RBAC.
    Backup the affected RBACNode rows first; then toggle IsChecked 1->0 only.
    No deletes/truncates.
*/
SET NOCOUNT ON;
BEGIN TRY
    BEGIN TRAN;

    IF OBJECT_ID('dbo.bk_RBACNode_RegGeoUncheck_20260729','U') IS NULL
        SELECT * INTO dbo.bk_RBACNode_RegGeoUncheck_20260729
        FROM dbo.RBACNode
        WHERE (NodeType='Action'    AND RefId = 107)
           OR (NodeType='SubModule' AND RefId IN (19, 88));

    UPDATE dbo.RBACNode
    SET IsChecked = 0, UpdatedBy = 'rbac-uncheck-20260729', UpdatedOn = GETDATE()
    WHERE IsChecked = 1
      AND ( (NodeType='Action'    AND RefId = 107)
         OR (NodeType='SubModule' AND RefId IN (19, 88)) );

    SELECT NodeType, RefId,
           SUM(CASE WHEN IsChecked=1 THEN 1 ELSE 0 END) AS StillChecked,
           COUNT(*) AS TotalRows
    FROM dbo.RBACNode
    WHERE (NodeType='Action' AND RefId=107) OR (NodeType='SubModule' AND RefId IN (19,88))
    GROUP BY NodeType, RefId ORDER BY NodeType, RefId;

    COMMIT TRAN;
    PRINT 'Unchecked Regularize/RegRequest/GeoRequest from all roles (dev).';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT>0 ROLLBACK TRAN;
    PRINT 'ERROR: ' + ERROR_MESSAGE();
    THROW;
END CATCH

/*
    Fix for a bug in Add_OfficialVisit_NavModule_RBAC_20260806.sql -- 2026-08-06.

    That script looped over every role and did:
        SELECT @ModuleNodeId = Id FROM RBACNode WHERE RoleId=@RoleId AND NodeType='Module' AND RefId=@ModuleId;
        IF @ModuleNodeId IS NULL BEGIN INSERT ...; SET @ModuleNodeId = SCOPE_IDENTITY(); END

    SQL Server's "SELECT @var = col FROM ... WHERE ..." does NOT reset @var to NULL when zero
    rows match -- it silently retains whatever value @var already held. Since @ModuleNodeId was
    DECLAREd once outside the loop, only the FIRST role processed (RoleId=1, Admin) ever actually
    hit the "IS NULL" branch and got its own Module-level RBACNode row (Id 11999). Every
    subsequent role's SubModule rows were then wrongly inserted with ParentNodeId=11999 (Admin's
    node), instead of getting -- and being parented to -- a Module node of their own. This broke
    the permissions tree for every role except Admin, including IT Superadmin, which is why the
    "Official Visit" sidebar group did not render for them.

    This script fixes ALL affected roles (every role except RoleId=1, which already has its own
    correct Module node):
      1. For each affected role, INSERT the missing Module-level RBACNode row (RefId=27) --
         additive, mirrors what the original script should have inserted.
      2. UPDATE that role's existing 2-3 SubModule RBACNode rows (RefId 134/135/136) so their
         ParentNodeId points at the role's own new Module node instead of Admin's (11999).
         This is the one UPDATE in this script -- explicitly confirmed by the user (2026-08-06),
         scoped ONLY to the ParentNodeId column of the handful of SubModule rows this feature's
         own earlier script created today. No other column, row, or table is touched.

    Idempotent: safe to re-run (only acts on rows still pointing at the wrong parent).
*/
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;

BEGIN TRY
    BEGIN TRAN;

    DECLARE @ModuleId INT = (SELECT Id FROM dbo.ModuleMaster WHERE ModuleName='Official Visit' AND ISNULL(IsDeleted,0)=0);
    DECLARE @ApplySubId INT = (SELECT Id FROM dbo.SubModuleMaster WHERE ModuleId=@ModuleId AND SubModuleName='Official Visit' AND ISNULL(IsDeleted,0)=0);
    DECLARE @ApprovalSubId INT = (SELECT Id FROM dbo.SubModuleMaster WHERE ModuleId=@ModuleId AND SubModuleName='Official Visit Approval' AND ISNULL(IsDeleted,0)=0);
    DECLARE @AdminSubId INT = (SELECT Id FROM dbo.SubModuleMaster WHERE ModuleId=@ModuleId AND SubModuleName='Official Visit Admin' AND ISNULL(IsDeleted,0)=0);

    IF @ModuleId IS NULL OR @ApplySubId IS NULL OR @ApprovalSubId IS NULL OR @AdminSubId IS NULL
    BEGIN
        RAISERROR('Official Visit module/submodules not found -- run Add_OfficialVisit_NavModule_RBAC_20260806.sql first.', 16, 1);
    END

    DECLARE @RoleId INT, @NewModuleNodeId INT;
    DECLARE @Fixed TABLE (RoleId INT, NewModuleNodeId INT);

    DECLARE roleCur CURSOR LOCAL FAST_FORWARD FOR
        SELECT r.RoleId
        FROM dbo.tblRole r
        WHERE NOT EXISTS (
            SELECT 1 FROM dbo.RBACNode mn WHERE mn.RoleId = r.RoleId AND mn.NodeType='Module' AND mn.RefId=@ModuleId
        );
    OPEN roleCur; FETCH NEXT FROM roleCur INTO @RoleId;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @NewModuleNodeId = NULL;

        INSERT INTO dbo.RBACNode (RoleId, NodeType, RefId, ParentNodeId, IsChecked, CreatedBy, CreatedOn)
        VALUES (@RoleId, 'Module', @ModuleId, 0, 1, 'Admin', GETDATE());
        SET @NewModuleNodeId = SCOPE_IDENTITY();

        UPDATE dbo.RBACNode
        SET ParentNodeId = @NewModuleNodeId
        WHERE RoleId = @RoleId
          AND NodeType = 'SubModule'
          AND RefId IN (@ApplySubId, @ApprovalSubId, @AdminSubId);

        INSERT INTO @Fixed (RoleId, NewModuleNodeId) VALUES (@RoleId, @NewModuleNodeId);

        FETCH NEXT FROM roleCur INTO @RoleId;
    END
    CLOSE roleCur; DEALLOCATE roleCur;

    SELECT * FROM @Fixed ORDER BY RoleId;

    COMMIT TRAN;
    PRINT 'Fixed Official Visit Module-node parenting for all affected roles.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT 'ERROR: ' + ERROR_MESSAGE();
    THROW;
END CATCH

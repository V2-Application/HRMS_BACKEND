/*
    Store State Mapping page — RBAC seeding (run ONCE on PROD).

    Adds the "Store State Mapping" sub-department page under the Masters module
    and grants it to the IT Superadmin role ONLY. Idempotent and additive:
    it INSERTs only what is missing and never deletes/updates unrelated rows.

    Resolves Module/Role ids BY NAME (prod ids differ from dev), so do not
    hardcode. Safe to re-run.

    Route: /master/store-state-mapping   (matches routes.js + [RequirePageAccess])
    Sub-module name: 'Store State Mapping' (MUST match the _nav.js item name so
    the sidebar's buildMenuFromPermissions shows it).
*/
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;

BEGIN TRY
    BEGIN TRAN;

    DECLARE @ModuleId INT, @RoleId INT, @SubModuleId INT, @MastersNodeId INT;

    -- Masters module (ModuleMaster PK is Id; SubModuleMaster.ModuleId / RBACNode module RefId reference it)
    SELECT @ModuleId = Id FROM dbo.ModuleMaster WHERE ModuleName = 'Masters';
    IF @ModuleId IS NULL
    BEGIN
        RAISERROR('Masters module not found in ModuleMaster.', 16, 1);
        ROLLBACK TRAN; RETURN;
    END

    -- IT Superadmin role
    SELECT @RoleId = RoleId FROM dbo.tblRole WHERE RoleName = 'IT Superadmin';
    IF @RoleId IS NULL
    BEGIN
        RAISERROR('IT Superadmin role not found in tblRole.', 16, 1);
        ROLLBACK TRAN; RETURN;
    END

    -- 1) SubModule under Masters (reuse if present)
    SELECT @SubModuleId = Id FROM dbo.SubModuleMaster
     WHERE ModuleId = @ModuleId AND SubModuleName = 'Store State Mapping' AND ISNULL(IsDeleted,0) = 0;
    IF @SubModuleId IS NULL
    BEGIN
        INSERT INTO dbo.SubModuleMaster (ModuleId, SubModuleName, IsActive, IsDeleted, CreatedBy, CreatedOn)
        VALUES (@ModuleId, 'Store State Mapping', 1, 0, 'Admin', GETDATE());
        SET @SubModuleId = SCOPE_IDENTITY();
    END

    -- 2) Route -> SubModule
    IF NOT EXISTS (SELECT 1 FROM dbo.tblPageRouteMap WHERE RoutePath = '/master/store-state-mapping')
        INSERT INTO dbo.tblPageRouteMap (RoutePath, SubModuleId, IsActive, Notes, CreatedOn)
        VALUES ('/master/store-state-mapping', @SubModuleId, 1, 'Store->State mapping (IT Superadmin only)', SYSDATETIME());
    ELSE
        UPDATE dbo.tblPageRouteMap SET SubModuleId = @SubModuleId, IsActive = 1
         WHERE RoutePath = '/master/store-state-mapping';

    -- 3) IT Superadmin's Masters MODULE node (create if missing) — parent for the submodule grant
    SELECT @MastersNodeId = Id FROM dbo.RBACNode
     WHERE RoleId = @RoleId AND NodeType = 'Module' AND RefId = @ModuleId;
    IF @MastersNodeId IS NULL
    BEGIN
        INSERT INTO dbo.RBACNode (RoleId, NodeType, RefId, ParentNodeId, IsChecked, CreatedBy, CreatedOn)
        VALUES (@RoleId, 'Module', @ModuleId, 0, 1, 'Admin', GETDATE());
        SET @MastersNodeId = SCOPE_IDENTITY();
    END

    -- 4) SubModule grant for IT Superadmin only
    IF NOT EXISTS (SELECT 1 FROM dbo.RBACNode WHERE RoleId = @RoleId AND NodeType = 'SubModule' AND RefId = @SubModuleId)
        INSERT INTO dbo.RBACNode (RoleId, NodeType, RefId, ParentNodeId, IsChecked, CreatedBy, CreatedOn)
        VALUES (@RoleId, 'SubModule', @SubModuleId, @MastersNodeId, 1, 'Admin', GETDATE());
    ELSE
        UPDATE dbo.RBACNode SET IsChecked = 1, ParentNodeId = @MastersNodeId
         WHERE RoleId = @RoleId AND NodeType = 'SubModule' AND RefId = @SubModuleId;

    SELECT @ModuleId AS MastersModuleId, @RoleId AS ITSuperadminRoleId,
           @SubModuleId AS SubModuleId, @MastersNodeId AS MastersNodeId;

    COMMIT TRAN;
    PRINT 'Store State Mapping RBAC seeded for IT Superadmin.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT 'ERROR: ' + ERROR_MESSAGE();
    THROW;
END CATCH

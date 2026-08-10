/*
    Official Visit Admin page -- RBAC seeding.  2026-08-06

    Grants the "/official-visit-admin" page (list + Excel uploader + export) to IT Superadmin
    only. The self-service apply/history/approval pages ("/official-visit",
    "/official-visit-approval") deliberately get NO RBAC row here -- RequirePageAccessFilter
    fails OPEN when a route has no tblPageRouteMap row, so omitting them is what makes those
    pages open to all authenticated users (same mechanism the fail-open design already relies on
    everywhere else).

    Idempotent + INSERT-only: every step is skipped if it already exists. No UPDATE, no DELETE, no
    DROP anywhere in this script -- if a row already exists from a prior run, it is left exactly
    as-is (per the standing no-touch-existing-data rule), just reported via PRINT.

    Resolves Module/Role ids BY NAME (dev/prod ids differ) -- never hardcode.

    Route: /official-visit-admin   (must match routes.js + [RequirePageAccess])
    Sub-module name: 'Official Visit Admin' (MUST match the _nav.js item name so the sidebar's
    buildMenuFromPermissions shows it).
*/
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;

BEGIN TRY
    BEGIN TRAN;

    DECLARE @ModuleId INT, @RoleId INT, @SubModuleId INT, @MastersNodeId INT;

    SELECT @ModuleId = Id FROM dbo.ModuleMaster WHERE ModuleName = 'Masters';
    IF @ModuleId IS NULL
    BEGIN
        RAISERROR('Masters module not found in ModuleMaster.', 16, 1);
        ROLLBACK TRAN; RETURN;
    END

    SELECT @RoleId = RoleId FROM dbo.tblRole WHERE RoleName = 'IT Superadmin';
    IF @RoleId IS NULL
    BEGIN
        RAISERROR('IT Superadmin role not found in tblRole.', 16, 1);
        ROLLBACK TRAN; RETURN;
    END

    -- 1) SubModule under Masters (reuse if present, never re-created/updated)
    SELECT @SubModuleId = Id FROM dbo.SubModuleMaster
     WHERE ModuleId = @ModuleId AND SubModuleName = 'Official Visit Admin' AND ISNULL(IsDeleted,0) = 0;
    IF @SubModuleId IS NULL
    BEGIN
        INSERT INTO dbo.SubModuleMaster (ModuleId, SubModuleName, IsActive, IsDeleted, CreatedBy, CreatedOn)
        VALUES (@ModuleId, 'Official Visit Admin', 1, 0, 'Admin', GETDATE());
        SET @SubModuleId = SCOPE_IDENTITY();
        PRINT 'Created SubModuleMaster "Official Visit Admin".';
    END
    ELSE
        PRINT 'SubModuleMaster "Official Visit Admin" already exists -- left as-is.';

    -- 2) Route -> SubModule (insert only if missing; never updates an existing mapping)
    IF NOT EXISTS (SELECT 1 FROM dbo.tblPageRouteMap WHERE RoutePath = '/official-visit-admin')
    BEGIN
        INSERT INTO dbo.tblPageRouteMap (RoutePath, SubModuleId, IsActive, Notes, CreatedOn)
        VALUES ('/official-visit-admin', @SubModuleId, 1, 'Official Visit admin list/uploader/export (IT Superadmin only)', SYSDATETIME());
        PRINT 'Created tblPageRouteMap row for /official-visit-admin.';
    END
    ELSE
        PRINT 'tblPageRouteMap row for /official-visit-admin already exists -- left as-is.';

    -- 3) IT Superadmin's Masters MODULE node (create only if missing) -- parent for the submodule grant
    SELECT @MastersNodeId = Id FROM dbo.RBACNode
     WHERE RoleId = @RoleId AND NodeType = 'Module' AND RefId = @ModuleId;
    IF @MastersNodeId IS NULL
    BEGIN
        INSERT INTO dbo.RBACNode (RoleId, NodeType, RefId, ParentNodeId, IsChecked, CreatedBy, CreatedOn)
        VALUES (@RoleId, 'Module', @ModuleId, 0, 1, 'Admin', GETDATE());
        SET @MastersNodeId = SCOPE_IDENTITY();
        PRINT 'Created RBACNode Module row for IT Superadmin / Masters.';
    END
    ELSE
        PRINT 'RBACNode Module row for IT Superadmin / Masters already exists -- left as-is.';

    -- 4) SubModule grant for IT Superadmin only (insert only if missing; never flips an existing grant)
    IF NOT EXISTS (SELECT 1 FROM dbo.RBACNode WHERE RoleId = @RoleId AND NodeType = 'SubModule' AND RefId = @SubModuleId)
    BEGIN
        INSERT INTO dbo.RBACNode (RoleId, NodeType, RefId, ParentNodeId, IsChecked, CreatedBy, CreatedOn)
        VALUES (@RoleId, 'SubModule', @SubModuleId, @MastersNodeId, 1, 'Admin', GETDATE());
        PRINT 'Created RBACNode SubModule grant for IT Superadmin / Official Visit Admin.';
    END
    ELSE
        PRINT 'RBACNode SubModule grant already exists -- left as-is.';

    SELECT @ModuleId AS MastersModuleId, @RoleId AS ITSuperadminRoleId,
           @SubModuleId AS SubModuleId, @MastersNodeId AS MastersNodeId;

    COMMIT TRAN;
    PRINT 'Official Visit Admin RBAC seed complete.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT 'ERROR: ' + ERROR_MESSAGE();
    THROW;
END CATCH

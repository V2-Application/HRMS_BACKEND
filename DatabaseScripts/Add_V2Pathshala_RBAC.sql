/*
    V2 Pathshala Registrations page — RBAC seeding (IT Superadmin only).

    Creates a NEW top-level module "V2 Pathshala" with one sub-module
    "Registrations" (route /v2-pathshala/registrations) and grants it to the
    IT Superadmin role only. Idempotent + additive: inserts only what is
    missing, never deletes/updates unrelated rows.

    Names MUST match _nav.js exactly (Module 'V2 Pathshala', SubModule
    'Registrations') because the sidebar is built by buildMenuFromPermissions
    matching module -> submodule NAMES.
*/
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;

BEGIN TRY
    BEGIN TRAN;

    DECLARE @ModuleId INT, @RoleId INT, @SubModuleId INT, @ModuleNodeId INT;

    -- IT Superadmin role
    SELECT @RoleId = RoleId FROM dbo.tblRole WHERE RoleName = 'IT Superadmin';
    IF @RoleId IS NULL
    BEGIN
        RAISERROR('IT Superadmin role not found in tblRole.', 16, 1);
        ROLLBACK TRAN; RETURN;
    END

    -- 1) Module (reuse if present)
    SELECT @ModuleId = Id FROM dbo.ModuleMaster
     WHERE ModuleName = 'V2 Pathshala' AND ISNULL(IsDeleted,0) = 0;
    IF @ModuleId IS NULL
    BEGIN
        INSERT INTO dbo.ModuleMaster (ModuleName, IsActive, IsDeleted, CreatedBy, CreatedOn)
        VALUES ('V2 Pathshala', 1, 0, 'Admin', GETDATE());
        SET @ModuleId = SCOPE_IDENTITY();
    END

    -- 2) SubModule under it (reuse if present)
    SELECT @SubModuleId = Id FROM dbo.SubModuleMaster
     WHERE ModuleId = @ModuleId AND SubModuleName = 'Registrations' AND ISNULL(IsDeleted,0) = 0;
    IF @SubModuleId IS NULL
    BEGIN
        INSERT INTO dbo.SubModuleMaster (ModuleId, SubModuleName, IsActive, IsDeleted, CreatedBy, CreatedOn)
        VALUES (@ModuleId, 'Registrations', 1, 0, 'Admin', GETDATE());
        SET @SubModuleId = SCOPE_IDENTITY();
    END

    -- 3) Route -> SubModule
    IF NOT EXISTS (SELECT 1 FROM dbo.tblPageRouteMap WHERE RoutePath = '/v2-pathshala/registrations')
        INSERT INTO dbo.tblPageRouteMap (RoutePath, SubModuleId, IsActive, Notes, CreatedOn)
        VALUES ('/v2-pathshala/registrations', @SubModuleId, 1, 'V2 Pathshala registrations list (IT Superadmin only)', SYSDATETIME());
    ELSE
        UPDATE dbo.tblPageRouteMap SET SubModuleId = @SubModuleId, IsActive = 1
         WHERE RoutePath = '/v2-pathshala/registrations';

    -- 4) IT Superadmin's Module node (create if missing) — parent of the submodule grant
    SELECT @ModuleNodeId = Id FROM dbo.RBACNode
     WHERE RoleId = @RoleId AND NodeType = 'Module' AND RefId = @ModuleId;
    IF @ModuleNodeId IS NULL
    BEGIN
        INSERT INTO dbo.RBACNode (RoleId, NodeType, RefId, ParentNodeId, IsChecked, CreatedBy, CreatedOn)
        VALUES (@RoleId, 'Module', @ModuleId, 0, 1, 'Admin', GETDATE());
        SET @ModuleNodeId = SCOPE_IDENTITY();
    END
    ELSE
        UPDATE dbo.RBACNode SET IsChecked = 1 WHERE Id = @ModuleNodeId;

    -- 5) SubModule grant for IT Superadmin
    IF NOT EXISTS (SELECT 1 FROM dbo.RBACNode WHERE RoleId = @RoleId AND NodeType = 'SubModule' AND RefId = @SubModuleId)
        INSERT INTO dbo.RBACNode (RoleId, NodeType, RefId, ParentNodeId, IsChecked, CreatedBy, CreatedOn)
        VALUES (@RoleId, 'SubModule', @SubModuleId, @ModuleNodeId, 1, 'Admin', GETDATE());
    ELSE
        UPDATE dbo.RBACNode SET IsChecked = 1, ParentNodeId = @ModuleNodeId
         WHERE RoleId = @RoleId AND NodeType = 'SubModule' AND RefId = @SubModuleId;

    SELECT @ModuleId AS ModuleId, @SubModuleId AS SubModuleId, @ModuleNodeId AS ModuleNodeId, @RoleId AS RoleId;

    COMMIT TRAN;
    PRINT 'V2 Pathshala RBAC seeded for IT Superadmin.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT 'ERROR: ' + ERROR_MESSAGE();
    THROW;
END CATCH

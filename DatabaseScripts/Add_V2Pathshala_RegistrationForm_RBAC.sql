/*
    V2 Pathshala — "Registration Form" sub-module RBAC (IT Superadmin only).

    Adds a second sub-module "Registration Form" (route
    /v2-pathshala/registration-form) under the existing "V2 Pathshala" module
    and grants it to IT Superadmin only. Idempotent + additive.

    Name MUST match _nav.js exactly ('Registration Form') so the sidebar shows it.
*/
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;

BEGIN TRY
    BEGIN TRAN;

    DECLARE @ModuleId INT, @RoleId INT, @SubModuleId INT, @ModuleNodeId INT;

    SELECT @RoleId = RoleId FROM dbo.tblRole WHERE RoleName = 'IT Superadmin';
    IF @RoleId IS NULL BEGIN RAISERROR('IT Superadmin role not found.',16,1); ROLLBACK TRAN; RETURN; END

    SELECT @ModuleId = Id FROM dbo.ModuleMaster WHERE ModuleName = 'V2 Pathshala' AND ISNULL(IsDeleted,0)=0;
    IF @ModuleId IS NULL BEGIN RAISERROR('V2 Pathshala module not found — run Add_V2Pathshala_RBAC.sql first.',16,1); ROLLBACK TRAN; RETURN; END

    -- 1) SubModule (reuse if present)
    SELECT @SubModuleId = Id FROM dbo.SubModuleMaster
     WHERE ModuleId = @ModuleId AND SubModuleName = 'Registration Form' AND ISNULL(IsDeleted,0)=0;
    IF @SubModuleId IS NULL
    BEGIN
        INSERT INTO dbo.SubModuleMaster (ModuleId, SubModuleName, IsActive, IsDeleted, CreatedBy, CreatedOn)
        VALUES (@ModuleId, 'Registration Form', 1, 0, 'Admin', GETDATE());
        SET @SubModuleId = SCOPE_IDENTITY();
    END

    -- 2) Route -> SubModule
    IF NOT EXISTS (SELECT 1 FROM dbo.tblPageRouteMap WHERE RoutePath = '/v2-pathshala/registration-form')
        INSERT INTO dbo.tblPageRouteMap (RoutePath, SubModuleId, IsActive, Notes, CreatedOn)
        VALUES ('/v2-pathshala/registration-form', @SubModuleId, 1, 'V2 Pathshala registration form (IT Superadmin only)', SYSDATETIME());
    ELSE
        UPDATE dbo.tblPageRouteMap SET SubModuleId = @SubModuleId, IsActive = 1
         WHERE RoutePath = '/v2-pathshala/registration-form';

    -- 3) IT Superadmin's V2 Pathshala Module node (should already exist; create if missing)
    SELECT @ModuleNodeId = Id FROM dbo.RBACNode
     WHERE RoleId = @RoleId AND NodeType = 'Module' AND RefId = @ModuleId;
    IF @ModuleNodeId IS NULL
    BEGIN
        INSERT INTO dbo.RBACNode (RoleId, NodeType, RefId, ParentNodeId, IsChecked, CreatedBy, CreatedOn)
        VALUES (@RoleId, 'Module', @ModuleId, 0, 1, 'Admin', GETDATE());
        SET @ModuleNodeId = SCOPE_IDENTITY();
    END

    -- 4) SubModule grant
    IF NOT EXISTS (SELECT 1 FROM dbo.RBACNode WHERE RoleId = @RoleId AND NodeType = 'SubModule' AND RefId = @SubModuleId)
        INSERT INTO dbo.RBACNode (RoleId, NodeType, RefId, ParentNodeId, IsChecked, CreatedBy, CreatedOn)
        VALUES (@RoleId, 'SubModule', @SubModuleId, @ModuleNodeId, 1, 'Admin', GETDATE());
    ELSE
        UPDATE dbo.RBACNode SET IsChecked = 1, ParentNodeId = @ModuleNodeId
         WHERE RoleId = @RoleId AND NodeType = 'SubModule' AND RefId = @SubModuleId;

    SELECT @ModuleId AS ModuleId, @SubModuleId AS SubModuleId, @ModuleNodeId AS ModuleNodeId, @RoleId AS RoleId;

    COMMIT TRAN;
    PRINT 'V2 Pathshala Registration Form RBAC seeded for IT Superadmin.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT 'ERROR: ' + ERROR_MESSAGE();
    THROW;
END CATCH

/*
    User Access Control (per-employee custom access) — DEV ONLY.  2026-07-28

    Adds granular, PER-EMPLOYEE (Ecode) access grants that plain role-based RBAC
    cannot express today:
      * tblUserModuleAccess  -> which RBAC Modules/SubModules an ecode may open
      * tblUserStoreAccess   -> which Store Codes (STCode) an ecode may see
      * tblUserEcodeAccess   -> which other employees' (ecodes') data an ecode may see

    Plus RBAC seeding so the new admin page (/user-access-control) is reachable by
    the IT Superadmin role only, and shows in the sidebar (Module 'Access Control'
    -> SubModule 'User Access Control' — names MUST match _nav.js).

    Idempotent + additive: creates only what is missing. No drops/truncates.
*/
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;

BEGIN TRY
    BEGIN TRAN;

    ------------------------------------------------------------------
    -- 1) Grant tables (per Ecode)
    ------------------------------------------------------------------
    IF OBJECT_ID('dbo.tblUserModuleAccess','U') IS NULL
    BEGIN
        CREATE TABLE dbo.tblUserModuleAccess
        (
            Id           INT IDENTITY(1,1) PRIMARY KEY,
            Ecode        NVARCHAR(50)  NOT NULL,
            ModuleId     INT           NULL,
            SubModuleId  INT           NOT NULL,
            IsChecked    BIT           NOT NULL DEFAULT(1),
            CreatedBy    NVARCHAR(200) NULL,
            CreatedOn    DATETIME      NOT NULL DEFAULT(GETDATE()),
            UpdatedBy    NVARCHAR(200) NULL,
            UpdatedOn    DATETIME      NULL
        );
        CREATE UNIQUE INDEX UX_tblUserModuleAccess_Ecode_Sub
            ON dbo.tblUserModuleAccess (Ecode, SubModuleId);
    END

    IF OBJECT_ID('dbo.tblUserStoreAccess','U') IS NULL
    BEGIN
        CREATE TABLE dbo.tblUserStoreAccess
        (
            Id         INT IDENTITY(1,1) PRIMARY KEY,
            Ecode      NVARCHAR(50)  NOT NULL,
            StoreCode  NVARCHAR(50)  NOT NULL,
            CreatedBy  NVARCHAR(200) NULL,
            CreatedOn  DATETIME      NOT NULL DEFAULT(GETDATE())
        );
        CREATE UNIQUE INDEX UX_tblUserStoreAccess_Ecode_Store
            ON dbo.tblUserStoreAccess (Ecode, StoreCode);
    END

    IF OBJECT_ID('dbo.tblUserEcodeAccess','U') IS NULL
    BEGIN
        CREATE TABLE dbo.tblUserEcodeAccess
        (
            Id           INT IDENTITY(1,1) PRIMARY KEY,
            Ecode        NVARCHAR(50)  NOT NULL,   -- the subject user
            AllowedEcode NVARCHAR(50)  NOT NULL,   -- an ecode whose data they may access
            CreatedBy    NVARCHAR(200) NULL,
            CreatedOn    DATETIME      NOT NULL DEFAULT(GETDATE())
        );
        CREATE UNIQUE INDEX UX_tblUserEcodeAccess_Ecode_Allowed
            ON dbo.tblUserEcodeAccess (Ecode, AllowedEcode);
    END

    ------------------------------------------------------------------
    -- 2) RBAC seeding for the admin page itself (IT Superadmin only)
    ------------------------------------------------------------------
    DECLARE @ModuleId INT, @RoleId INT, @SubModuleId INT, @ModuleNodeId INT;

    SELECT @RoleId = RoleId FROM dbo.tblRole WHERE RoleName = 'IT Superadmin';
    IF @RoleId IS NULL
    BEGIN
        RAISERROR('IT Superadmin role not found in tblRole.', 16, 1);
        ROLLBACK TRAN; RETURN;
    END

    -- Module 'Access Control'
    SELECT @ModuleId = Id FROM dbo.ModuleMaster
     WHERE ModuleName = 'Access Control' AND ISNULL(IsDeleted,0) = 0;
    IF @ModuleId IS NULL
    BEGIN
        INSERT INTO dbo.ModuleMaster (ModuleName, IsActive, IsDeleted, CreatedBy, CreatedOn)
        VALUES ('Access Control', 1, 0, 'Admin', GETDATE());
        SET @ModuleId = SCOPE_IDENTITY();
    END

    -- SubModule 'User Access Control'
    SELECT @SubModuleId = Id FROM dbo.SubModuleMaster
     WHERE ModuleId = @ModuleId AND SubModuleName = 'User Access Control' AND ISNULL(IsDeleted,0) = 0;
    IF @SubModuleId IS NULL
    BEGIN
        INSERT INTO dbo.SubModuleMaster (ModuleId, SubModuleName, IsActive, IsDeleted, CreatedBy, CreatedOn)
        VALUES (@ModuleId, 'User Access Control', 1, 0, 'Admin', GETDATE());
        SET @SubModuleId = SCOPE_IDENTITY();
    END

    -- Route -> SubModule
    IF NOT EXISTS (SELECT 1 FROM dbo.tblPageRouteMap WHERE RoutePath = '/user-access-control')
        INSERT INTO dbo.tblPageRouteMap (RoutePath, SubModuleId, IsActive, Notes, CreatedOn)
        VALUES ('/user-access-control', @SubModuleId, 1, 'User Access Control (IT Superadmin only)', SYSDATETIME());
    ELSE
        UPDATE dbo.tblPageRouteMap SET SubModuleId = @SubModuleId, IsActive = 1
         WHERE RoutePath = '/user-access-control';

    -- IT Superadmin Module node (parent of the submodule grant)
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

    -- SubModule grant
    IF NOT EXISTS (SELECT 1 FROM dbo.RBACNode WHERE RoleId = @RoleId AND NodeType = 'SubModule' AND RefId = @SubModuleId)
        INSERT INTO dbo.RBACNode (RoleId, NodeType, RefId, ParentNodeId, IsChecked, CreatedBy, CreatedOn)
        VALUES (@RoleId, 'SubModule', @SubModuleId, @ModuleNodeId, 1, 'Admin', GETDATE());
    ELSE
        UPDATE dbo.RBACNode SET IsChecked = 1, ParentNodeId = @ModuleNodeId
         WHERE RoleId = @RoleId AND NodeType = 'SubModule' AND RefId = @SubModuleId;

    SELECT @ModuleId AS ModuleId, @SubModuleId AS SubModuleId, @ModuleNodeId AS ModuleNodeId, @RoleId AS RoleId;

    COMMIT TRAN;
    PRINT 'User Access Control tables + RBAC seeded (dev).';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT 'ERROR: ' + ERROR_MESSAGE();
    THROW;
END CATCH

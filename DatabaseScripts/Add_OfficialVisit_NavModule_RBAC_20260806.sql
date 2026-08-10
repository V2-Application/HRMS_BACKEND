/*
    Official Visit -- dedicated top-level sidebar group.  2026-08-06

    The sidebar's permission tree (state.auth.permissions, built by buildMenuFromPermissions in
    _nav.js) is driven purely by RBACNode's Module/SubModule tree -- it is a SEPARATE mechanism
    from tblPageRouteMap/[RequirePageAccess], which only gates the backend routes themselves.
    A CNavGroup only renders if its name matches a ModuleMaster row with a grant; the three
    Official Visit pages were previously grouped under the existing Attendance/Masters modules
    (see Add_OfficialVisit_RBAC_20260806.sql and Add_OfficialVisit_SelfService_RBAC_AllRoles_
    20260806.sql), which is why they rendered fine even without a module of their own.

    Per the user's request for a standalone "Official Visit" sidebar section, this script adds a
    NEW Module + 3 NEW SubModules (same display names) purely for nav grouping, and grants them:
      - "Official Visit" / "Official Visit Approval" -> every role (self-service)
      - "Official Visit Admin"                       -> IT Superadmin only

    Deliberately does NOT touch tblPageRouteMap -- backend enforcement keeps working exactly as
    before, off the original SubModule rows created in the earlier two scripts. This script only
    adds a second, nav-only SubModule per page purely for sidebar grouping; nothing existing is
    updated or removed.

    Idempotent + INSERT-only, no UPDATE/DELETE/DROP anywhere.
*/
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;

BEGIN TRY
    BEGIN TRAN;

    DECLARE @ModuleId INT;
    SELECT @ModuleId = Id FROM dbo.ModuleMaster WHERE ModuleName = 'Official Visit' AND ISNULL(IsDeleted,0) = 0;
    IF @ModuleId IS NULL
    BEGIN
        INSERT INTO dbo.ModuleMaster (ModuleName, IsActive, IsDeleted, CreatedBy, CreatedOn)
        VALUES ('Official Visit', 1, 0, 'Admin', GETDATE());
        SET @ModuleId = SCOPE_IDENTITY();
        PRINT 'Created ModuleMaster "Official Visit".';
    END
    ELSE PRINT 'ModuleMaster "Official Visit" already exists -- left as-is.';

    DECLARE @ApplySubId INT, @ApprovalSubId INT, @AdminSubId INT;

    SELECT @ApplySubId = Id FROM dbo.SubModuleMaster WHERE ModuleId=@ModuleId AND SubModuleName='Official Visit' AND ISNULL(IsDeleted,0)=0;
    IF @ApplySubId IS NULL
    BEGIN
        INSERT INTO dbo.SubModuleMaster (ModuleId, SubModuleName, IsActive, IsDeleted, CreatedBy, CreatedOn)
        VALUES (@ModuleId, 'Official Visit', 1, 0, 'Admin', GETDATE());
        SET @ApplySubId = SCOPE_IDENTITY();
        PRINT 'Created SubModuleMaster "Official Visit" (nav module).';
    END
    ELSE PRINT 'SubModuleMaster "Official Visit" (nav module) already exists -- left as-is.';

    SELECT @ApprovalSubId = Id FROM dbo.SubModuleMaster WHERE ModuleId=@ModuleId AND SubModuleName='Official Visit Approval' AND ISNULL(IsDeleted,0)=0;
    IF @ApprovalSubId IS NULL
    BEGIN
        INSERT INTO dbo.SubModuleMaster (ModuleId, SubModuleName, IsActive, IsDeleted, CreatedBy, CreatedOn)
        VALUES (@ModuleId, 'Official Visit Approval', 1, 0, 'Admin', GETDATE());
        SET @ApprovalSubId = SCOPE_IDENTITY();
        PRINT 'Created SubModuleMaster "Official Visit Approval" (nav module).';
    END
    ELSE PRINT 'SubModuleMaster "Official Visit Approval" (nav module) already exists -- left as-is.';

    SELECT @AdminSubId = Id FROM dbo.SubModuleMaster WHERE ModuleId=@ModuleId AND SubModuleName='Official Visit Admin' AND ISNULL(IsDeleted,0)=0;
    IF @AdminSubId IS NULL
    BEGIN
        INSERT INTO dbo.SubModuleMaster (ModuleId, SubModuleName, IsActive, IsDeleted, CreatedBy, CreatedOn)
        VALUES (@ModuleId, 'Official Visit Admin', 1, 0, 'Admin', GETDATE());
        SET @AdminSubId = SCOPE_IDENTITY();
        PRINT 'Created SubModuleMaster "Official Visit Admin" (nav module).';
    END
    ELSE PRINT 'SubModuleMaster "Official Visit Admin" (nav module) already exists -- left as-is.';

    -- Grant "Official Visit" + "Official Visit Approval" to EVERY role (self-service)
    DECLARE @RoleId INT, @ModuleNodeId INT;
    DECLARE roleCur CURSOR LOCAL FAST_FORWARD FOR SELECT RoleId FROM dbo.tblRole;
    OPEN roleCur; FETCH NEXT FROM roleCur INTO @RoleId;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SELECT @ModuleNodeId = Id FROM dbo.RBACNode WHERE RoleId=@RoleId AND NodeType='Module' AND RefId=@ModuleId;
        IF @ModuleNodeId IS NULL
        BEGIN
            INSERT INTO dbo.RBACNode (RoleId, NodeType, RefId, ParentNodeId, IsChecked, CreatedBy, CreatedOn)
            VALUES (@RoleId, 'Module', @ModuleId, 0, 1, 'Admin', GETDATE());
            SET @ModuleNodeId = SCOPE_IDENTITY();
        END

        IF NOT EXISTS (SELECT 1 FROM dbo.RBACNode WHERE RoleId=@RoleId AND NodeType='SubModule' AND RefId=@ApplySubId)
            INSERT INTO dbo.RBACNode (RoleId, NodeType, RefId, ParentNodeId, IsChecked, CreatedBy, CreatedOn)
            VALUES (@RoleId, 'SubModule', @ApplySubId, @ModuleNodeId, 1, 'Admin', GETDATE());

        IF NOT EXISTS (SELECT 1 FROM dbo.RBACNode WHERE RoleId=@RoleId AND NodeType='SubModule' AND RefId=@ApprovalSubId)
            INSERT INTO dbo.RBACNode (RoleId, NodeType, RefId, ParentNodeId, IsChecked, CreatedBy, CreatedOn)
            VALUES (@RoleId, 'SubModule', @ApprovalSubId, @ModuleNodeId, 1, 'Admin', GETDATE());

        -- "Official Visit Admin" -> IT Superadmin only
        IF EXISTS (SELECT 1 FROM dbo.tblRole WHERE RoleId=@RoleId AND RoleName='IT Superadmin')
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM dbo.RBACNode WHERE RoleId=@RoleId AND NodeType='SubModule' AND RefId=@AdminSubId)
                INSERT INTO dbo.RBACNode (RoleId, NodeType, RefId, ParentNodeId, IsChecked, CreatedBy, CreatedOn)
                VALUES (@RoleId, 'SubModule', @AdminSubId, @ModuleNodeId, 1, 'Admin', GETDATE());
        END

        FETCH NEXT FROM roleCur INTO @RoleId;
    END
    CLOSE roleCur; DEALLOCATE roleCur;

    SELECT @ModuleId AS OfficialVisitModuleId, @ApplySubId AS ApplySubId, @ApprovalSubId AS ApprovalSubId, @AdminSubId AS AdminSubId;

    COMMIT TRAN;
    PRINT 'Official Visit nav module + RBAC seeded (all roles for apply/approval, IT Superadmin for admin).';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT 'ERROR: ' + ERROR_MESSAGE();
    THROW;
END CATCH

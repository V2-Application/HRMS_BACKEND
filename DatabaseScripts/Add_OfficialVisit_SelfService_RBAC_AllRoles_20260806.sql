/*
    Official Visit self-service pages -- RBAC seeding for ALL roles.  2026-08-06

    Correction to the original plan: the backend endpoints (apply/my-requests/pending-for-
    manager/approve) deliberately have NO [RequirePageAccess] gate -- they stay genuinely open to
    any authenticated user regardless of this script. But the SIDEBAR only ever renders an item
    when buildMenuFromPermissions (_nav.js) finds a matching, TRUE permission entry for it -- a
    route with zero RBAC rows never appears in ANYONE's sidebar, even though the backend would
    have allowed it. So to make "/official-visit" and "/official-visit-approval" actually visible
    and navigable for every role, this script grants them to every row in tblRole.

    Placed under the existing "Attendance" module (Id resolved by name, not hardcoded).

    Idempotent + INSERT-only: every step is skipped if it already exists. No UPDATE, no DELETE, no
    DROP anywhere -- an existing row from a prior run is left exactly as-is, just reported via
    PRINT, per the standing no-touch-existing-data rule.

    Sub-module names ('Official Visit', 'Official Visit Approval') MUST exactly match the
    _nav.js item names so buildMenuFromPermissions renders them.
*/
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;

BEGIN TRY
    BEGIN TRAN;

    DECLARE @ModuleId INT;
    SELECT @ModuleId = Id FROM dbo.ModuleMaster WHERE ModuleName = 'Attendance';
    IF @ModuleId IS NULL
    BEGIN
        RAISERROR('Attendance module not found in ModuleMaster.', 16, 1);
        ROLLBACK TRAN; RETURN;
    END

    -- 1) The two SubModules (reuse if present, never re-created/updated)
    DECLARE @ApplySubId INT, @ApprovalSubId INT;

    SELECT @ApplySubId = Id FROM dbo.SubModuleMaster
     WHERE ModuleId = @ModuleId AND SubModuleName = 'Official Visit' AND ISNULL(IsDeleted,0) = 0;
    IF @ApplySubId IS NULL
    BEGIN
        INSERT INTO dbo.SubModuleMaster (ModuleId, SubModuleName, IsActive, IsDeleted, CreatedBy, CreatedOn)
        VALUES (@ModuleId, 'Official Visit', 1, 0, 'Admin', GETDATE());
        SET @ApplySubId = SCOPE_IDENTITY();
        PRINT 'Created SubModuleMaster "Official Visit".';
    END
    ELSE PRINT 'SubModuleMaster "Official Visit" already exists -- left as-is.';

    SELECT @ApprovalSubId = Id FROM dbo.SubModuleMaster
     WHERE ModuleId = @ModuleId AND SubModuleName = 'Official Visit Approval' AND ISNULL(IsDeleted,0) = 0;
    IF @ApprovalSubId IS NULL
    BEGIN
        INSERT INTO dbo.SubModuleMaster (ModuleId, SubModuleName, IsActive, IsDeleted, CreatedBy, CreatedOn)
        VALUES (@ModuleId, 'Official Visit Approval', 1, 0, 'Admin', GETDATE());
        SET @ApprovalSubId = SCOPE_IDENTITY();
        PRINT 'Created SubModuleMaster "Official Visit Approval".';
    END
    ELSE PRINT 'SubModuleMaster "Official Visit Approval" already exists -- left as-is.';

    -- 2) Routes -> SubModules (insert only if missing)
    IF NOT EXISTS (SELECT 1 FROM dbo.tblPageRouteMap WHERE RoutePath = '/official-visit')
    BEGIN
        INSERT INTO dbo.tblPageRouteMap (RoutePath, SubModuleId, IsActive, Notes, CreatedOn)
        VALUES ('/official-visit', @ApplySubId, 1, 'Official Visit apply/history (all roles)', SYSDATETIME());
        PRINT 'Created tblPageRouteMap row for /official-visit.';
    END
    ELSE PRINT 'tblPageRouteMap row for /official-visit already exists -- left as-is.';

    IF NOT EXISTS (SELECT 1 FROM dbo.tblPageRouteMap WHERE RoutePath = '/official-visit-approval')
    BEGIN
        INSERT INTO dbo.tblPageRouteMap (RoutePath, SubModuleId, IsActive, Notes, CreatedOn)
        VALUES ('/official-visit-approval', @ApprovalSubId, 1, 'Official Visit manager approval queue (all roles)', SYSDATETIME());
        PRINT 'Created tblPageRouteMap row for /official-visit-approval.';
    END
    ELSE PRINT 'tblPageRouteMap row for /official-visit-approval already exists -- left as-is.';

    -- 3) Grant BOTH submodules to EVERY role in tblRole (insert only what's missing per role)
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

        FETCH NEXT FROM roleCur INTO @RoleId;
    END
    CLOSE roleCur; DEALLOCATE roleCur;

    SELECT @ModuleId AS AttendanceModuleId, @ApplySubId AS OfficialVisitSubModuleId, @ApprovalSubId AS OfficialVisitApprovalSubModuleId;

    COMMIT TRAN;
    PRINT 'Official Visit self-service RBAC seeded for all roles.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT 'ERROR: ' + ERROR_MESSAGE();
    THROW;
END CATCH

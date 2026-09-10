/* =============================================================================
   Role Master page: RBAC registration (IT Superadmin only)
   -----------------------------------------------------------------------------
   Registers the new Masters -> Role Master page the same way the other
   IT-Superadmin-only pages are registered (see /regularize-access,
   /user-access-control, /master/store-state-mapping):

     1. SubModuleMaster row under the Masters module (ModuleId 9).
        The name MUST stay "Role Master": the sidebar builder matches a nav
        item's NAME against the permitted submodule names, so the React nav
        item and this row have to read identically.

     2. tblPageRouteMap row for /master/role -> that submodule, IsActive = 1.
        Without this row RequirePageAccess fails OPEN and everyone could reach
        the API.

     3. RBACNode SubModule row for RoleId 24 (IT Superadmin) ONLY, parented to
        that role's existing Masters module node. No other role gets a row, so
        no other role sees the menu item or passes the page guard.
        (Master / SuperAdmin still bypass at the API level - that admin bypass
        is pre-existing behaviour in PageAccessService, not something this
        script grants.)

   INSERT only, all guarded by NOT EXISTS. Nothing is updated, deleted or
   truncated. Re-runnable.
   ============================================================================= */

SET NOCOUNT ON;

DECLARE @MastersModuleId int = 9;      -- ModuleMaster: 'Masters'
DECLARE @ItSuperadminRoleId int = 24;  -- tblRole: 'IT Superadmin'
DECLARE @RoutePath nvarchar(200) = N'/master/role';
DECLARE @SubModuleName nvarchar(400) = N'Role Master';

IF NOT EXISTS (SELECT 1 FROM dbo.ModuleMaster WHERE Id = @MastersModuleId)
BEGIN
    RAISERROR('Masters module (Id 9) not found - aborting.', 16, 1);
    RETURN;
END

IF NOT EXISTS (SELECT 1 FROM dbo.tblRole WHERE RoleId = @ItSuperadminRoleId AND RoleName = 'IT Superadmin')
BEGIN
    RAISERROR('IT Superadmin role (RoleId 24) not found - aborting.', 16, 1);
    RETURN;
END

/* ---- 1. SubModule ------------------------------------------------------- */
DECLARE @SubModuleId int =
    (SELECT TOP 1 Id FROM dbo.SubModuleMaster
      WHERE ModuleId = @MastersModuleId AND SubModuleName = @SubModuleName);

IF @SubModuleId IS NULL
BEGIN
    INSERT INTO dbo.SubModuleMaster (ModuleId, SubModuleName, IsActive, IsDeleted, CreatedBy, CreatedOn)
    VALUES (@MastersModuleId, @SubModuleName, 1, 0, N'System', GETDATE());

    SET @SubModuleId = CAST(SCOPE_IDENTITY() AS int);
    PRINT CONCAT('Created SubModuleMaster "Role Master" with Id ', @SubModuleId);
END
ELSE
    PRINT CONCAT('SubModuleMaster "Role Master" already present (Id ', @SubModuleId, ') - skipped');

/* ---- 2. Page route map -------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.tblPageRouteMap WHERE RoutePath = @RoutePath)
BEGIN
    INSERT INTO dbo.tblPageRouteMap (RoutePath, SubModuleId, IsActive, Notes, CreatedOn)
    VALUES (@RoutePath, @SubModuleId, 1, N'Role Master (IT Superadmin only)', SYSDATETIME());
    PRINT 'Created tblPageRouteMap row for the Role Master page';
END
ELSE
    PRINT 'tblPageRouteMap row for the Role Master page already present - skipped';

/* ---- 3. RBAC node for IT Superadmin only -------------------------------- */
DECLARE @ParentNodeId int =
    (SELECT TOP 1 Id FROM dbo.RBACNode
      WHERE RoleId = @ItSuperadminRoleId AND NodeType = 'Module' AND RefId = @MastersModuleId);

IF NOT EXISTS (SELECT 1 FROM dbo.RBACNode
               WHERE RoleId = @ItSuperadminRoleId AND NodeType = 'SubModule' AND RefId = @SubModuleId)
BEGIN
    INSERT INTO dbo.RBACNode (RoleId, NodeType, RefId, ParentNodeId, IsChecked, CreatedBy, CreatedOn)
    VALUES (@ItSuperadminRoleId, 'SubModule', @SubModuleId, @ParentNodeId, 1, N'System', GETDATE());
    PRINT 'Granted the Role Master page to IT Superadmin';
END
ELSE
    PRINT 'IT Superadmin already has the Role Master node - skipped';

GO

-- Verification: the page, and exactly which roles hold it
SELECT s.Id AS SubModuleId, m.ModuleName, s.SubModuleName, s.IsActive
FROM dbo.SubModuleMaster s JOIN dbo.ModuleMaster m ON m.Id = s.ModuleId
WHERE s.SubModuleName = N'Role Master';

SELECT PageRouteId, RoutePath, SubModuleId, IsActive, Notes
FROM dbo.tblPageRouteMap WHERE RoutePath = N'/master/role';

SELECT r.RoleId, r.RoleName, n.NodeType, n.RefId, n.IsChecked
FROM dbo.RBACNode n JOIN dbo.tblRole r ON r.RoleId = n.RoleId
WHERE n.NodeType = 'SubModule'
  AND n.RefId = (SELECT TOP 1 Id FROM dbo.SubModuleMaster WHERE SubModuleName = N'Role Master')
ORDER BY r.RoleId;

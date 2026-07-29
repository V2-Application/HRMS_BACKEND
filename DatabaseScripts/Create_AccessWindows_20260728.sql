/*
    Regularize / Geofence Access Windows — DEV ONLY.  2026-07-28

    Admin-opened windows that let selected ecodes / STCodes have regularize (or
    geofence) requests for selected dates surface in the Manager & LP approval
    queues (OpenApprovals). One row per resolved date (a date-range or custom
    dates both expand to individual date rows).

    Target resolution:
      Ecode set, STCode null  -> that employee
      STCode set, Ecode null  -> all employees at that store
      both null               -> global (all)

    Plus RBAC seeding so both admin pages are reachable by IT Superadmin only
    (Module 'Access Control' already exists from the User Access Control page).
    Idempotent + additive. No drops/truncates.
*/
SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON; SET NOCOUNT ON;

BEGIN TRY
    BEGIN TRAN;

    IF OBJECT_ID('dbo.tblRegularizeAccessWindow','U') IS NULL
    BEGIN
        CREATE TABLE dbo.tblRegularizeAccessWindow
        (
            Id            INT IDENTITY(1,1) PRIMARY KEY,
            Ecode         NVARCHAR(50)  NULL,
            STCode        NVARCHAR(50)  NULL,
            AccessDate    DATE          NOT NULL,
            OpenApprovals BIT           NOT NULL DEFAULT(0),
            IsActive      BIT           NOT NULL DEFAULT(1),
            CreatedBy     NVARCHAR(200) NULL,
            CreatedOn     DATETIME      NOT NULL DEFAULT(GETDATE()),
            UpdatedBy     NVARCHAR(200) NULL,
            UpdatedOn     DATETIME      NULL
        );
        CREATE INDEX IX_RegAccessWin_Lookup ON dbo.tblRegularizeAccessWindow (AccessDate, IsActive, OpenApprovals) INCLUDE (Ecode, STCode);
    END

    IF OBJECT_ID('dbo.tblGeofenceAccessWindow','U') IS NULL
    BEGIN
        CREATE TABLE dbo.tblGeofenceAccessWindow
        (
            Id            INT IDENTITY(1,1) PRIMARY KEY,
            Ecode         NVARCHAR(50)  NULL,
            STCode        NVARCHAR(50)  NULL,
            AccessDate    DATE          NOT NULL,
            OpenApprovals BIT           NOT NULL DEFAULT(0),
            IsActive      BIT           NOT NULL DEFAULT(1),
            CreatedBy     NVARCHAR(200) NULL,
            CreatedOn     DATETIME      NOT NULL DEFAULT(GETDATE()),
            UpdatedBy     NVARCHAR(200) NULL,
            UpdatedOn     DATETIME      NULL
        );
        CREATE INDEX IX_GeoAccessWin_Lookup ON dbo.tblGeofenceAccessWindow (AccessDate, IsActive, OpenApprovals) INCLUDE (Ecode, STCode);
    END

    ------------------------------------------------------------------
    -- RBAC: two submodules under existing Module 'Access Control'
    ------------------------------------------------------------------
    DECLARE @RoleId INT, @ModuleId INT, @ModuleNodeId INT, @SubId INT;
    SELECT @RoleId = RoleId FROM dbo.tblRole WHERE RoleName = 'IT Superadmin';
    IF @RoleId IS NULL BEGIN RAISERROR('IT Superadmin role missing.',16,1); ROLLBACK TRAN; RETURN; END

    SELECT @ModuleId = Id FROM dbo.ModuleMaster WHERE ModuleName='Access Control' AND ISNULL(IsDeleted,0)=0;
    IF @ModuleId IS NULL
    BEGIN
        INSERT INTO dbo.ModuleMaster (ModuleName, IsActive, IsDeleted, CreatedBy, CreatedOn)
        VALUES ('Access Control', 1, 0, 'Admin', GETDATE());
        SET @ModuleId = SCOPE_IDENTITY();
    END

    SELECT @ModuleNodeId = Id FROM dbo.RBACNode WHERE RoleId=@RoleId AND NodeType='Module' AND RefId=@ModuleId;
    IF @ModuleNodeId IS NULL
    BEGIN
        INSERT INTO dbo.RBACNode (RoleId, NodeType, RefId, ParentNodeId, IsChecked, CreatedBy, CreatedOn)
        VALUES (@RoleId, 'Module', @ModuleId, 0, 1, 'Admin', GETDATE());
        SET @ModuleNodeId = SCOPE_IDENTITY();
    END

    -- helper via inline for each page
    DECLARE @pages TABLE (SubName NVARCHAR(200), Route NVARCHAR(200));
    INSERT INTO @pages VALUES ('Regularize Access','/regularize-access'), ('Geofence Access','/geofence-access');

    DECLARE @sn NVARCHAR(200), @rt NVARCHAR(200);
    DECLARE cur CURSOR LOCAL FAST_FORWARD FOR SELECT SubName, Route FROM @pages;
    OPEN cur; FETCH NEXT FROM cur INTO @sn, @rt;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SELECT @SubId = Id FROM dbo.SubModuleMaster WHERE ModuleId=@ModuleId AND SubModuleName=@sn AND ISNULL(IsDeleted,0)=0;
        IF @SubId IS NULL
        BEGIN
            INSERT INTO dbo.SubModuleMaster (ModuleId, SubModuleName, IsActive, IsDeleted, CreatedBy, CreatedOn)
            VALUES (@ModuleId, @sn, 1, 0, 'Admin', GETDATE());
            SET @SubId = SCOPE_IDENTITY();
        END

        IF NOT EXISTS (SELECT 1 FROM dbo.tblPageRouteMap WHERE RoutePath=@rt)
            INSERT INTO dbo.tblPageRouteMap (RoutePath, SubModuleId, IsActive, Notes, CreatedOn)
            VALUES (@rt, @SubId, 1, @sn + ' (IT Superadmin only)', SYSDATETIME());
        ELSE
            UPDATE dbo.tblPageRouteMap SET SubModuleId=@SubId, IsActive=1 WHERE RoutePath=@rt;

        IF NOT EXISTS (SELECT 1 FROM dbo.RBACNode WHERE RoleId=@RoleId AND NodeType='SubModule' AND RefId=@SubId)
            INSERT INTO dbo.RBACNode (RoleId, NodeType, RefId, ParentNodeId, IsChecked, CreatedBy, CreatedOn)
            VALUES (@RoleId, 'SubModule', @SubId, @ModuleNodeId, 1, 'Admin', GETDATE());
        ELSE
            UPDATE dbo.RBACNode SET IsChecked=1, ParentNodeId=@ModuleNodeId WHERE RoleId=@RoleId AND NodeType='SubModule' AND RefId=@SubId;

        FETCH NEXT FROM cur INTO @sn, @rt;
    END
    CLOSE cur; DEALLOCATE cur;

    COMMIT TRAN;
    PRINT 'Access-window tables + RBAC seeded (dev).';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT 'ERROR: ' + ERROR_MESSAGE();
    THROW;
END CATCH

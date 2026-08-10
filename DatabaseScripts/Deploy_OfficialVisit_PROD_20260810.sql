/*
    Official Visit -- consolidated PROD deploy. 2026-08-10.

    This is dev's Official Visit feature (table + RBAC), pushed to prod in its FINAL state
    (IT Superadmin only, both route-enforcement and nav-visibility) rather than replaying dev's
    history (which briefly opened it to all 37 roles before being restricted -- see
    project_official_visit_feature.md memory). Consolidates what were originally 5 separate dev
    scripts (Create_OfficialVisitRequest, Alter_..._AddRecommendedBy, Add_OfficialVisit_RBAC,
    Add_OfficialVisit_SelfService_RBAC_AllRoles [skipped -- superseded], Add_OfficialVisit_NavModule_RBAC)
    into one idempotent, IT-Superadmin-only pass.

    Idempotent + additive-only: CREATE TABLE IF NOT EXISTS, ALTER ADD COLUMN IF NOT EXISTS,
    INSERT ... IF NOT EXISTS everywhere. No DELETE/TRUNCATE/DROP/UPDATE of any pre-existing table.
*/
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;

BEGIN TRY
    BEGIN TRAN;

    -- ---------------------------------------------------------------
    -- 1. Table
    -- ---------------------------------------------------------------
    IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'tblOfficialVisitRequest' AND schema_id = SCHEMA_ID('dbo'))
    BEGIN
        CREATE TABLE dbo.tblOfficialVisitRequest (
            OfficialVisitRequestId  BIGINT IDENTITY(1,1) PRIMARY KEY,
            EmployeeId              BIGINT NOT NULL,
            Ecode                   NVARCHAR(50) NULL,
            EmployeeName            NVARCHAR(200) NULL,
            FromDate                DATE NOT NULL,
            ToDate                  DATE NOT NULL,
            NoOfDays                INT NOT NULL,
            Purpose                 NVARCHAR(500) NULL,
            VisitStoreCode          NVARCHAR(50) NULL,
            EmployeeRemarks         NVARCHAR(500) NULL,
            ReportingManagerId      BIGINT NULL,
            ManagerApprovalStatusId INT NULL,
            ManagerApproverId       BIGINT NULL,
            ManagerApprovalOn       DATETIME NULL,
            ManagerRemarks          NVARCHAR(500) NULL,
            SourceTypeId            INT NOT NULL DEFAULT 1,
            CreatedBy               NVARCHAR(100) NULL,
            CreatedOn               DATETIME NOT NULL DEFAULT GETDATE(),
            LastUpdatedBy           NVARCHAR(100) NULL,
            UpdatedOn               DATETIME NULL
        );
        CREATE INDEX IX_OfficialVisitRequest_EmployeeId ON dbo.tblOfficialVisitRequest(EmployeeId);
        CREATE INDEX IX_OfficialVisitRequest_Manager ON dbo.tblOfficialVisitRequest(ReportingManagerId, ManagerApprovalStatusId);
        CREATE INDEX IX_OfficialVisitRequest_Dates ON dbo.tblOfficialVisitRequest(FromDate, ToDate);
        PRINT 'Created dbo.tblOfficialVisitRequest.';
    END
    ELSE PRINT 'dbo.tblOfficialVisitRequest already exists -- left as-is.';

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tblOfficialVisitRequest') AND name = 'RecommendedByEcode')
        ALTER TABLE dbo.tblOfficialVisitRequest ADD RecommendedByEcode NVARCHAR(50) NULL;
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.tblOfficialVisitRequest') AND name = 'RecommendedByName')
        ALTER TABLE dbo.tblOfficialVisitRequest ADD RecommendedByName NVARCHAR(200) NULL;

    -- ---------------------------------------------------------------
    -- 2. Route-enforcement SubModules (under existing Attendance / Masters modules)
    -- ---------------------------------------------------------------
    DECLARE @AttendanceModuleId INT = (SELECT Id FROM dbo.ModuleMaster WHERE ModuleName = 'Attendance' AND ISNULL(IsDeleted,0) = 0);
    DECLARE @MastersModuleId   INT = (SELECT Id FROM dbo.ModuleMaster WHERE ModuleName = 'Masters'    AND ISNULL(IsDeleted,0) = 0);
    DECLARE @ITSuperadminRoleId INT = (SELECT RoleId FROM dbo.tblRole WHERE RoleName = 'IT Superadmin');

    IF @AttendanceModuleId IS NULL RAISERROR('Module "Attendance" not found on prod.', 16, 1);
    IF @MastersModuleId IS NULL RAISERROR('Module "Masters" not found on prod.', 16, 1);
    IF @ITSuperadminRoleId IS NULL RAISERROR('Role "IT Superadmin" not found on prod.', 16, 1);

    DECLARE @ApplySubId INT, @ApprovalSubId INT, @AdminSubId INT;

    SELECT @ApplySubId = Id FROM dbo.SubModuleMaster WHERE ModuleId=@AttendanceModuleId AND SubModuleName='Official Visit' AND ISNULL(IsDeleted,0)=0;
    IF @ApplySubId IS NULL
    BEGIN
        INSERT INTO dbo.SubModuleMaster (ModuleId, SubModuleName, IsActive, IsDeleted, CreatedBy, CreatedOn)
        VALUES (@AttendanceModuleId, 'Official Visit', 1, 0, 'Admin', GETDATE());
        SET @ApplySubId = SCOPE_IDENTITY();
    END

    SELECT @ApprovalSubId = Id FROM dbo.SubModuleMaster WHERE ModuleId=@AttendanceModuleId AND SubModuleName='Official Visit Approval' AND ISNULL(IsDeleted,0)=0;
    IF @ApprovalSubId IS NULL
    BEGIN
        INSERT INTO dbo.SubModuleMaster (ModuleId, SubModuleName, IsActive, IsDeleted, CreatedBy, CreatedOn)
        VALUES (@AttendanceModuleId, 'Official Visit Approval', 1, 0, 'Admin', GETDATE());
        SET @ApprovalSubId = SCOPE_IDENTITY();
    END

    SELECT @AdminSubId = Id FROM dbo.SubModuleMaster WHERE ModuleId=@MastersModuleId AND SubModuleName='Official Visit Admin' AND ISNULL(IsDeleted,0)=0;
    IF @AdminSubId IS NULL
    BEGIN
        INSERT INTO dbo.SubModuleMaster (ModuleId, SubModuleName, IsActive, IsDeleted, CreatedBy, CreatedOn)
        VALUES (@MastersModuleId, 'Official Visit Admin', 1, 0, 'Admin', GETDATE());
        SET @AdminSubId = SCOPE_IDENTITY();
    END

    -- tblPageRouteMap
    IF NOT EXISTS (SELECT 1 FROM dbo.tblPageRouteMap WHERE RoutePath = '/official-visit')
        INSERT INTO dbo.tblPageRouteMap (RoutePath, SubModuleId, IsActive) VALUES ('/official-visit', @ApplySubId, 1);
    IF NOT EXISTS (SELECT 1 FROM dbo.tblPageRouteMap WHERE RoutePath = '/official-visit-approval')
        INSERT INTO dbo.tblPageRouteMap (RoutePath, SubModuleId, IsActive) VALUES ('/official-visit-approval', @ApprovalSubId, 1);
    IF NOT EXISTS (SELECT 1 FROM dbo.tblPageRouteMap WHERE RoutePath = '/official-visit-admin')
        INSERT INTO dbo.tblPageRouteMap (RoutePath, SubModuleId, IsActive) VALUES ('/official-visit-admin', @AdminSubId, 1);

    -- RBACNode grants: IT Superadmin only (route-enforcement submodules)
    DECLARE @AttModuleNodeId INT, @MastersModuleNodeId INT;
    SELECT @AttModuleNodeId = Id FROM dbo.RBACNode WHERE RoleId=@ITSuperadminRoleId AND NodeType='Module' AND RefId=@AttendanceModuleId;
    IF @AttModuleNodeId IS NULL
    BEGIN
        INSERT INTO dbo.RBACNode (RoleId, NodeType, RefId, ParentNodeId, IsChecked, CreatedBy, CreatedOn)
        VALUES (@ITSuperadminRoleId, 'Module', @AttendanceModuleId, 0, 1, 'Admin', GETDATE());
        SET @AttModuleNodeId = SCOPE_IDENTITY();
    END
    SELECT @MastersModuleNodeId = Id FROM dbo.RBACNode WHERE RoleId=@ITSuperadminRoleId AND NodeType='Module' AND RefId=@MastersModuleId;
    IF @MastersModuleNodeId IS NULL
    BEGIN
        INSERT INTO dbo.RBACNode (RoleId, NodeType, RefId, ParentNodeId, IsChecked, CreatedBy, CreatedOn)
        VALUES (@ITSuperadminRoleId, 'Module', @MastersModuleId, 0, 1, 'Admin', GETDATE());
        SET @MastersModuleNodeId = SCOPE_IDENTITY();
    END

    IF NOT EXISTS (SELECT 1 FROM dbo.RBACNode WHERE RoleId=@ITSuperadminRoleId AND NodeType='SubModule' AND RefId=@ApplySubId)
        INSERT INTO dbo.RBACNode (RoleId, NodeType, RefId, ParentNodeId, IsChecked, CreatedBy, CreatedOn)
        VALUES (@ITSuperadminRoleId, 'SubModule', @ApplySubId, @AttModuleNodeId, 1, 'Admin', GETDATE());
    IF NOT EXISTS (SELECT 1 FROM dbo.RBACNode WHERE RoleId=@ITSuperadminRoleId AND NodeType='SubModule' AND RefId=@ApprovalSubId)
        INSERT INTO dbo.RBACNode (RoleId, NodeType, RefId, ParentNodeId, IsChecked, CreatedBy, CreatedOn)
        VALUES (@ITSuperadminRoleId, 'SubModule', @ApprovalSubId, @AttModuleNodeId, 1, 'Admin', GETDATE());
    IF NOT EXISTS (SELECT 1 FROM dbo.RBACNode WHERE RoleId=@ITSuperadminRoleId AND NodeType='SubModule' AND RefId=@AdminSubId)
        INSERT INTO dbo.RBACNode (RoleId, NodeType, RefId, ParentNodeId, IsChecked, CreatedBy, CreatedOn)
        VALUES (@ITSuperadminRoleId, 'SubModule', @AdminSubId, @MastersModuleNodeId, 1, 'Admin', GETDATE());

    -- ---------------------------------------------------------------
    -- 3. Nav-only Module + SubModules (pure sidebar grouping, no tblPageRouteMap)
    -- ---------------------------------------------------------------
    DECLARE @NavModuleId INT;
    SELECT @NavModuleId = Id FROM dbo.ModuleMaster WHERE ModuleName = 'Official Visit' AND ISNULL(IsDeleted,0) = 0;
    IF @NavModuleId IS NULL
    BEGIN
        INSERT INTO dbo.ModuleMaster (ModuleName, IsActive, IsDeleted, CreatedBy, CreatedOn)
        VALUES ('Official Visit', 1, 0, 'Admin', GETDATE());
        SET @NavModuleId = SCOPE_IDENTITY();
    END

    DECLARE @NavApplySubId INT, @NavApprovalSubId INT, @NavAdminSubId INT;
    SELECT @NavApplySubId = Id FROM dbo.SubModuleMaster WHERE ModuleId=@NavModuleId AND SubModuleName='Official Visit' AND ISNULL(IsDeleted,0)=0;
    IF @NavApplySubId IS NULL
    BEGIN
        INSERT INTO dbo.SubModuleMaster (ModuleId, SubModuleName, IsActive, IsDeleted, CreatedBy, CreatedOn)
        VALUES (@NavModuleId, 'Official Visit', 1, 0, 'Admin', GETDATE());
        SET @NavApplySubId = SCOPE_IDENTITY();
    END
    SELECT @NavApprovalSubId = Id FROM dbo.SubModuleMaster WHERE ModuleId=@NavModuleId AND SubModuleName='Official Visit Approval' AND ISNULL(IsDeleted,0)=0;
    IF @NavApprovalSubId IS NULL
    BEGIN
        INSERT INTO dbo.SubModuleMaster (ModuleId, SubModuleName, IsActive, IsDeleted, CreatedBy, CreatedOn)
        VALUES (@NavModuleId, 'Official Visit Approval', 1, 0, 'Admin', GETDATE());
        SET @NavApprovalSubId = SCOPE_IDENTITY();
    END
    SELECT @NavAdminSubId = Id FROM dbo.SubModuleMaster WHERE ModuleId=@NavModuleId AND SubModuleName='Official Visit Admin' AND ISNULL(IsDeleted,0)=0;
    IF @NavAdminSubId IS NULL
    BEGIN
        INSERT INTO dbo.SubModuleMaster (ModuleId, SubModuleName, IsActive, IsDeleted, CreatedBy, CreatedOn)
        VALUES (@NavModuleId, 'Official Visit Admin', 1, 0, 'Admin', GETDATE());
        SET @NavAdminSubId = SCOPE_IDENTITY();
    END

    DECLARE @NavModuleNodeId INT;
    SELECT @NavModuleNodeId = Id FROM dbo.RBACNode WHERE RoleId=@ITSuperadminRoleId AND NodeType='Module' AND RefId=@NavModuleId;
    IF @NavModuleNodeId IS NULL
    BEGIN
        INSERT INTO dbo.RBACNode (RoleId, NodeType, RefId, ParentNodeId, IsChecked, CreatedBy, CreatedOn)
        VALUES (@ITSuperadminRoleId, 'Module', @NavModuleId, 0, 1, 'Admin', GETDATE());
        SET @NavModuleNodeId = SCOPE_IDENTITY();
    END

    IF NOT EXISTS (SELECT 1 FROM dbo.RBACNode WHERE RoleId=@ITSuperadminRoleId AND NodeType='SubModule' AND RefId=@NavApplySubId)
        INSERT INTO dbo.RBACNode (RoleId, NodeType, RefId, ParentNodeId, IsChecked, CreatedBy, CreatedOn)
        VALUES (@ITSuperadminRoleId, 'SubModule', @NavApplySubId, @NavModuleNodeId, 1, 'Admin', GETDATE());
    IF NOT EXISTS (SELECT 1 FROM dbo.RBACNode WHERE RoleId=@ITSuperadminRoleId AND NodeType='SubModule' AND RefId=@NavApprovalSubId)
        INSERT INTO dbo.RBACNode (RoleId, NodeType, RefId, ParentNodeId, IsChecked, CreatedBy, CreatedOn)
        VALUES (@ITSuperadminRoleId, 'SubModule', @NavApprovalSubId, @NavModuleNodeId, 1, 'Admin', GETDATE());
    IF NOT EXISTS (SELECT 1 FROM dbo.RBACNode WHERE RoleId=@ITSuperadminRoleId AND NodeType='SubModule' AND RefId=@NavAdminSubId)
        INSERT INTO dbo.RBACNode (RoleId, NodeType, RefId, ParentNodeId, IsChecked, CreatedBy, CreatedOn)
        VALUES (@ITSuperadminRoleId, 'SubModule', @NavAdminSubId, @NavModuleNodeId, 1, 'Admin', GETDATE());

    SELECT @ApplySubId AS RouteApplySubId, @ApprovalSubId AS RouteApprovalSubId, @AdminSubId AS RouteAdminSubId,
           @NavModuleId AS NavModuleId, @NavApplySubId AS NavApplySubId, @NavApprovalSubId AS NavApprovalSubId, @NavAdminSubId AS NavAdminSubId;

    COMMIT TRAN;
    PRINT 'Official Visit deployed to PROD: table + route RBAC + nav RBAC, IT Superadmin only.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT 'ERROR: ' + ERROR_MESSAGE();
    THROW;
END CATCH

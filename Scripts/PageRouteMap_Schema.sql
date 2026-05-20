-- =============================================================================
-- Category: RBAC (route-level page access enforcement)
-- =============================================================================
-- Creates dbo.tblPageRouteMap: the mapping from frontend route paths
-- (e.g. '/salary_recal') to the existing RBAC SubModuleMaster entries that
-- gate them. The CheckPageAccess endpoint reads this table; the
-- <RoutePermissionGuard> in the frontend hits that endpoint before mounting
-- each page.
--
-- Safe to re-run (idempotent IF NOT EXISTS guards). Adds NO drops.
-- =============================================================================
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'tblPageRouteMap')
BEGIN
    CREATE TABLE dbo.tblPageRouteMap
    (
        PageRouteId   INT IDENTITY(1,1) NOT NULL,
        RoutePath     NVARCHAR(200)     NOT NULL,
        SubModuleId   INT               NULL,
        -- Active rows are enforced. IsActive=0 = route exists but currently
        -- ungated (rollout knob). Routes absent from the table default to
        -- ALLOW (see IPageAccessService.HasPageAccessAsync).
        IsActive      BIT               NOT NULL CONSTRAINT DF_tblPageRouteMap_IsActive DEFAULT (0),
        Notes         NVARCHAR(500)     NULL,
        CreatedOn     DATETIME2         NOT NULL CONSTRAINT DF_tblPageRouteMap_CreatedOn DEFAULT (SYSUTCDATETIME()),
        LastUpdatedOn DATETIME2         NULL,

        CONSTRAINT PK_tblPageRouteMap PRIMARY KEY CLUSTERED (PageRouteId),
        CONSTRAINT UQ_tblPageRouteMap_RoutePath UNIQUE (RoutePath),
        CONSTRAINT FK_tblPageRouteMap_SubModule FOREIGN KEY (SubModuleId)
            REFERENCES dbo.SubModuleMaster (Id)
    );

    CREATE NONCLUSTERED INDEX IX_tblPageRouteMap_Active_Path
        ON dbo.tblPageRouteMap (IsActive, RoutePath)
        INCLUDE (SubModuleId);

    PRINT 'tblPageRouteMap created.';
END
ELSE
BEGIN
    PRINT 'tblPageRouteMap already exists, skipping.';
END
GO

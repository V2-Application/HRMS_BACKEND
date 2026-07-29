/*
    Generalise the feature kill-switch lock from SubModule-only to ANY RBAC node
    type (SubModule / Action / FurtherPart).  DEV ONLY.  2026-07-28

    Reason: some "features" (e.g. the Regularize button on the View Attendance page)
    are RBAC ACTIONS (ActionMaster), not SubModules — RBACNode stores them as
    NodeType='Action'. The kill switch must be able to stop/restore those too.

    New table keyed by (NodeType, RefId). The old SubModule-only lock table
    (tblSubModuleAccessLock) is dropped ONLY if it is still empty (it was created
    earlier the same day and no stops have been performed).
*/
SET NOCOUNT ON;

IF OBJECT_ID('dbo.tblSubModuleAccessLock','U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM dbo.tblSubModuleAccessLock)
BEGIN
    DROP TABLE dbo.tblSubModuleAccessLock;
    PRINT 'Dropped empty dbo.tblSubModuleAccessLock (superseded).';
END

IF OBJECT_ID('dbo.tblRbacNodeAccessLock','U') IS NULL
BEGIN
    CREATE TABLE dbo.tblRbacNodeAccessLock
    (
        NodeType        NVARCHAR(20)  NOT NULL,   -- 'SubModule' | 'Action' | 'FurtherPart'
        RefId           INT           NOT NULL,   -- SubModuleId / ActionId / FurtherPartId
        IsStopped       BIT           NOT NULL DEFAULT(0),
        PreviousRoleIds NVARCHAR(MAX) NULL,        -- CSV of RoleIds checked at moment of Stop
        UpdatedBy       NVARCHAR(200) NULL,
        UpdatedOn       DATETIME      NULL,
        CONSTRAINT PK_tblRbacNodeAccessLock PRIMARY KEY (NodeType, RefId)
    );
    PRINT 'Created dbo.tblRbacNodeAccessLock (dev).';
END
ELSE
    PRINT 'dbo.tblRbacNodeAccessLock already exists.';

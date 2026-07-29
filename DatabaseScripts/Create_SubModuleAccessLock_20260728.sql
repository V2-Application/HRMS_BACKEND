/*
    Feature (SubModule) global access kill-switch — DEV ONLY.  2026-07-28

    Supports "stop a feature for ALL roles in one click, then restore exactly the
    roles that had it before". We snapshot the set of RoleIds that currently have
    the submodule checked, zero them all in RBACNode, and on restore re-check only
    the snapshotted roles.

    tblSubModuleAccessLock:
      SubModuleId      -> the RBAC submodule being locked (e.g. Regularization)
      IsStopped        -> 1 while access is globally stopped
      PreviousRoleIds  -> CSV of RoleIds that were checked at the moment of Stop
      UpdatedBy/On     -> audit

    Additive + idempotent. No drops/truncates. RBACNode itself is only toggled
    (IsChecked 1<->0) by the Stop/Restore endpoints, never deleted.
*/
SET NOCOUNT ON;

IF OBJECT_ID('dbo.tblSubModuleAccessLock','U') IS NULL
BEGIN
    CREATE TABLE dbo.tblSubModuleAccessLock
    (
        SubModuleId     INT           NOT NULL PRIMARY KEY,
        IsStopped       BIT           NOT NULL DEFAULT(0),
        PreviousRoleIds NVARCHAR(MAX) NULL,
        UpdatedBy       NVARCHAR(200) NULL,
        UpdatedOn       DATETIME      NULL
    );
    PRINT 'Created dbo.tblSubModuleAccessLock (dev).';
END
ELSE
    PRINT 'dbo.tblSubModuleAccessLock already exists.';

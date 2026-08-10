/*
    dbo.tblRbacNodeAccessLock -- PROD apply, 2026-08-04.

    Drop-free variant of Create_RbacNodeAccessLock_20260728.sql: the original
    conditionally DROPs dbo.tblSubModuleAccessLock if it's still empty
    (superseded-by-this-table cleanup). Per the standing no-DELETE/TRUNCATE/DROP
    rule on both DBs, this version omits that statement entirely and only
    creates the new table -- dev's current state keeps BOTH tables side by
    side (the drop never fired there either), so this matches dev exactly.
*/
SET NOCOUNT ON;

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
    PRINT 'Created dbo.tblRbacNodeAccessLock (prod).';
END
ELSE
    PRINT 'dbo.tblRbacNodeAccessLock already exists.';

SET NOCOUNT ON;
SELECT
    @@SERVERNAME      AS server_name,
    DB_NAME()         AS db_name,
    SYSUTCDATETIME()  AS now_utc;

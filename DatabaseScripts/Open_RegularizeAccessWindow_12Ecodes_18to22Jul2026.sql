/*
    Regularize Access Window -- 18-22 Jul 2026, uploaded ecode list.  2026-08-05

    Mirrors exactly what POST api/RegularizeAccess/Save does (Controllers/AccessWindowControllers.cs,
    lines 189-256): one upsert per (Ecode, AccessDate) pair, STCode NULL since we're targeting
    ecodes not stores, OpenApprovals = 1 to match the admin UI's default. Idempotent -- safe to
    re-run, existing rows just get their OpenApprovals/IsActive/UpdatedBy/UpdatedOn refreshed.

    Run against whichever DB's connection this session is using. On dev, only ecodes that exist
    in tblEmployee are inserted -- the other 7 are skipped with a PRINT so it's visible, not silent.
*/
SET NOCOUNT ON;

DECLARE @Ecodes TABLE (Ecode NVARCHAR(50));
INSERT INTO @Ecodes VALUES
('V51395'),('V53439'),('V43366'),('V53485'),('V56471'),('V51591'),
('V44634'),('V55556'),('V56798'),('V47537'),('V53982'),('V56503');

DECLARE @Dates TABLE (AccessDate DATE);
INSERT INTO @Dates VALUES ('2026-07-18'),('2026-07-19'),('2026-07-20'),('2026-07-21'),('2026-07-22');

-- Report which requested ecodes don't exist in THIS database's tblEmployee -- skip them, don't insert orphans.
DECLARE @Missing NVARCHAR(MAX) = (
    SELECT STRING_AGG(e.Ecode, ', ')
    FROM @Ecodes e
    WHERE NOT EXISTS (SELECT 1 FROM dbo.tblEmployee emp WHERE emp.Ecode = e.Ecode)
);
IF @Missing IS NOT NULL PRINT 'Skipped (not in tblEmployee on this DB): ' + @Missing;

DECLARE @by NVARCHAR(200) = 'Admin';
DECLARE @e NVARCHAR(50), @d DATE, @affected INT = 0;

DECLARE cur CURSOR LOCAL FAST_FORWARD FOR
    SELECT e.Ecode, d.AccessDate
    FROM @Ecodes e
    CROSS JOIN @Dates d
    WHERE EXISTS (SELECT 1 FROM dbo.tblEmployee emp WHERE emp.Ecode = e.Ecode);

OPEN cur; FETCH NEXT FROM cur INTO @e, @d;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF EXISTS (SELECT 1 FROM dbo.tblRegularizeAccessWindow
               WHERE AccessDate = @d AND Ecode = @e AND STCode IS NULL)
        UPDATE dbo.tblRegularizeAccessWindow
           SET OpenApprovals = 1, IsActive = 1, UpdatedBy = @by, UpdatedOn = GETDATE()
         WHERE AccessDate = @d AND Ecode = @e AND STCode IS NULL;
    ELSE
        INSERT INTO dbo.tblRegularizeAccessWindow (Ecode, STCode, AccessDate, OpenApprovals, IsActive, CreatedBy, CreatedOn)
        VALUES (@e, NULL, @d, 1, 1, @by, GETDATE());

    SET @affected += 1;
    FETCH NEXT FROM cur INTO @e, @d;
END
CLOSE cur; DEALLOCATE cur;

PRINT CONVERT(VARCHAR(10), @affected) + ' (ecode, date) row(s) upserted.';

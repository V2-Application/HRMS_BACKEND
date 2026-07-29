/*
    PROD data fix — V25134 rejected regularization not reflecting in attendance.  2026-07-28
    Requests 146979 (12-Jul-2026) & 146988 (19-Jul-2026) are Rejected (StatusId=2) but the
    punch rows still had IsRegularize=1, so attendance showed regularized-present.

    Scoped to 2 punch rows + 2 request rows. BACKUP first, then UPDATE only.
    NO DELETE / NO TRUNCATE / NO DROP / NO SYSTEM_VERSIONING toggle.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRAN;

    -- 1) Backups of the exact affected rows (additive; skip if already present)
    IF OBJECT_ID('dbo.bk_MultiPunch_V25134_20260728','U') IS NULL
        SELECT * INTO dbo.bk_MultiPunch_V25134_20260728
        FROM dbo.tblEmployeeMultiPunches WHERE ID IN (7326752, 7326760);

    IF OBJECT_ID('dbo.bk_RegReq_V25134_20260728','U') IS NULL
        SELECT * INTO dbo.bk_RegReq_V25134_20260728
        FROM dbo.tblAttendanceRegularizationRequest WHERE AttendanceRequestId IN (146979, 146988);

    -- 2) Clear the stranded regularization flag on the 2 punch rows
    UPDATE dbo.tblEmployeeMultiPunches
    SET IsRegularize      = 0,
        RegularizePunchIn = NULL,
        RegularizePuncOut = NULL,
        LastUpdatedBy     = 'regfix-20260728'
    WHERE ID IN (7326752, 7326760);

    -- 3) Sync the legacy Status string to match the already-Rejected StatusIds
    UPDATE dbo.tblAttendanceRegularizationRequest
    SET Status = 'Rejected'
    WHERE AttendanceRequestId IN (146979, 146988);

    COMMIT TRAN;
    PRINT 'V25134 regularize-reject fix applied.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT 'ERROR: ' + ERROR_MESSAGE();
    THROW;
END CATCH

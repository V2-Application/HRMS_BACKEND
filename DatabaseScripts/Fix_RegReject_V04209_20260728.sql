/*
    PROD data fix — V04209 (VIKKY KESHRI, EmpId 127). 2026-07-28. Dates 12 & 19 Jul 2026.
    Regularize requests 146480/146975/150839 Rejected(StatusId=2) but multipunch rows
    7338697/7338704 still IsRegularize=1 -> clear flag + sync Status string.
    Scoped UPDATE, BACKUP first. NO DELETE / TRUNCATE / DROP.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRAN;

    IF OBJECT_ID('dbo.bk_MultiPunch_V04209_20260728','U') IS NULL
        SELECT * INTO dbo.bk_MultiPunch_V04209_20260728
        FROM dbo.tblEmployeeMultiPunches WHERE ID IN (7338697, 7338704);

    IF OBJECT_ID('dbo.bk_RegReq_V04209_20260728','U') IS NULL
        SELECT * INTO dbo.bk_RegReq_V04209_20260728
        FROM dbo.tblAttendanceRegularizationRequest WHERE AttendanceRequestId IN (146480, 146975, 150839);

    UPDATE dbo.tblEmployeeMultiPunches
    SET IsRegularize = 0, RegularizePunchIn = NULL, RegularizePuncOut = NULL, LastUpdatedBy = 'regfix-20260728'
    WHERE ID IN (7338697, 7338704);

    UPDATE dbo.tblAttendanceRegularizationRequest
    SET Status = 'Rejected'
    WHERE AttendanceRequestId IN (146480, 146975, 150839);

    COMMIT TRAN;
    PRINT 'V04209 regularize-reject fix applied.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT 'ERROR: ' + ERROR_MESSAGE();
    THROW;
END CATCH

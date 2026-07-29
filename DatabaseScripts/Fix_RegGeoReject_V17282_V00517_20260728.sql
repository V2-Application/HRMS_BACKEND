/*
    PROD data fix — rejected regularize/geofence not reflecting in attendance.  2026-07-28
    Same class of issue as V25134. Dates 12-Jul-2026 & 19-Jul-2026.

    V17282 (749): regularize requests 146989/146998 Rejected(StatusId=2) but multipunch
        IDs 7338743/7338736 still IsRegularize=1  -> clear flag + sync Status string.
    V00517 (43732): geofence approvals 113656/113663 Rejected(FinalStatusId=2) but the
        geo punch rows in AttendanceRecord are still StatusId=1 (Approved) -> set them to 2
        (Rejected) like usp_ApproveGeoAttendance would; also sync regularize requests
        149603/149615 Status string (no multipunch row exists, nothing else to clear).

    Scoped UPDATEs only. BACKUP first. NO DELETE / TRUNCATE / DROP / SYSTEM_VERSIONING toggle.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRAN;

    -- ---------- Backups (additive; skip if already present) ----------
    IF OBJECT_ID('dbo.bk_MultiPunch_V17282_20260728','U') IS NULL
        SELECT * INTO dbo.bk_MultiPunch_V17282_20260728
        FROM dbo.tblEmployeeMultiPunches WHERE ID IN (7338743, 7338736);

    IF OBJECT_ID('dbo.bk_RegReq_V17282_20260728','U') IS NULL
        SELECT * INTO dbo.bk_RegReq_V17282_20260728
        FROM dbo.tblAttendanceRegularizationRequest WHERE AttendanceRequestId IN (146989, 146998);

    IF OBJECT_ID('dbo.bk_AttRec_V00517_20260728','U') IS NULL
        SELECT * INTO dbo.bk_AttRec_V00517_20260728
        FROM dbo.AttendanceRecord
        WHERE EmployeeId = 43732 AND CONVERT(date, PunchTimeUtc) IN ('2026-07-12','2026-07-19');

    IF OBJECT_ID('dbo.bk_GeoApp_V00517_20260728','U') IS NULL
        SELECT * INTO dbo.bk_GeoApp_V00517_20260728
        FROM dbo.GeoAttendanceApproval WHERE Id IN (113656, 113663);

    IF OBJECT_ID('dbo.bk_RegReq_V00517_20260728','U') IS NULL
        SELECT * INTO dbo.bk_RegReq_V00517_20260728
        FROM dbo.tblAttendanceRegularizationRequest WHERE AttendanceRequestId IN (149603, 149615);

    -- ---------- V17282: clear stranded regularize flag ----------
    UPDATE dbo.tblEmployeeMultiPunches
    SET IsRegularize = 0, RegularizePunchIn = NULL, RegularizePuncOut = NULL, LastUpdatedBy = 'regfix-20260728'
    WHERE ID IN (7338743, 7338736);

    UPDATE dbo.tblAttendanceRegularizationRequest
    SET Status = 'Rejected'
    WHERE AttendanceRequestId IN (146989, 146998);

    -- ---------- V00517: reject the geo punch rows to match the rejected geo approval ----------
    UPDATE dbo.AttendanceRecord
    SET StatusId = 2, Remarks = 'geofence rejected (sync fix)', LastUpdatedBy = 'regfix-20260728', LastUpdatedOn = SYSUTCDATETIME()
    WHERE EmployeeId = 43732 AND CONVERT(date, PunchTimeUtc) IN ('2026-07-12','2026-07-19') AND StatusId <> 2;

    UPDATE dbo.tblAttendanceRegularizationRequest
    SET Status = 'Rejected'
    WHERE AttendanceRequestId IN (149603, 149615);

    COMMIT TRAN;
    PRINT 'V17282 + V00517 reg/geo reject fix applied.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT 'ERROR: ' + ERROR_MESSAGE();
    THROW;
END CATCH

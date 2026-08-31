/*==============================================================================
  BULK REJECT geofence requests for a list of Ecode + Date pairs.
  Date : 2026-08-29

  WHAT IT DOES
    For each (Ecode, PunchDate) pair it calls the application's own procedure
        dbo.usp_ApproveGeoAttendance  ... @StatusId = 2 (Rejected)
    exactly as a SuperAdmin rejection from the Geofence Requests screen would,
    so AttendanceRecord and GeoAttendanceApproval always move together.
    Nothing is hand-UPDATEd.

  SAFETY
    @DryRun = 1 (the default) writes NOTHING. It prints a preview: what each
    pair resolves to, its current status, and what would change. Read that
    first. Only set @DryRun = 0 once the preview looks right.

    Pairs that cannot be actioned are reported, never silently skipped:
      - Ecode not found in tblEmployee
      - no geofence punch on that date
      - already Rejected (no-op)

  AFTER RUNNING
    Rejected punches drop out of the attendance calculation. The merged
    attendance table only reflects it once
        dbo.usp_MergeMonthlyPunchesRange_Optimized @FromDate, @ToDate
    runs again. The "Current Month" SQL Agent job (00:10 daily) covers
    26th-prev-month -> today, so recent dates self-correct overnight.

  ROLLBACK
    The preview/result table records each pair's PREVIOUS status and remarks.
    Copy that output before running for real - it is what you need to restore.
==============================================================================*/

USE [HRMS];
GO
SET NOCOUNT ON;

/*=============================== SETTINGS ===================================*/
DECLARE @DryRun        BIT           = 1;          -- 1 = preview only. 0 = actually reject.
DECLARE @ActorEcode    NVARCHAR(50)  = N'V41797';  -- who is recorded as rejecting
DECLARE @Remarks       NVARCHAR(200) = N'Rejected';

/*=============================== THE LIST ===================================
  Paste the Ecode / date pairs here. Dates as 'YYYY-MM-DD'.
  Replace the sample rows below with the real list.
============================================================================*/
DECLARE @Pairs TABLE (Ecode NVARCHAR(50), PunchDate DATE);

INSERT INTO @Pairs (Ecode, PunchDate) VALUES
    (N'V01077', '2026-07-26'),
    (N'V08521', '2026-08-19');
-- ...add the rest here, one row per line, comma-separated, last row ends with ;

/*============================ NOTHING TO EDIT BELOW =========================*/
DECLARE @ActorId NVARCHAR(50);
SELECT @ActorId = CONVERT(NVARCHAR(50), EmployeeId) FROM tblEmployee WHERE Ecode = @ActorEcode;
IF @ActorId IS NULL
BEGIN
    RAISERROR('Actor ecode %s not found in tblEmployee. Fix @ActorEcode.', 16, 1, @ActorEcode);
    RETURN;
END

/* Resolve each pair and capture its CURRENT state (this is your rollback data) */
IF OBJECT_ID('tempdb..#Work') IS NOT NULL DROP TABLE #Work;
SELECT
    p.Ecode,
    p.PunchDate,
    e.EmployeeId,
    PunchRows      = (SELECT COUNT(*) FROM dbo.AttendanceRecord ar
                      WHERE ar.EmployeeId = e.EmployeeId
                        AND CONVERT(date, ar.PunchTimeUtc) = p.PunchDate),
    CurrentStatus  = (SELECT MAX(ar.StatusId) FROM dbo.AttendanceRecord ar
                      WHERE ar.EmployeeId = e.EmployeeId
                        AND CONVERT(date, ar.PunchTimeUtc) = p.PunchDate),
    CurrentRemarks = (SELECT MAX(ar.Remarks) FROM dbo.AttendanceRecord ar
                      WHERE ar.EmployeeId = e.EmployeeId
                        AND CONVERT(date, ar.PunchTimeUtc) = p.PunchDate),
    PrevApproverId = (SELECT MAX(ar.LastUpdatedBy) FROM dbo.AttendanceRecord ar
                      WHERE ar.EmployeeId = e.EmployeeId
                        AND CONVERT(date, ar.PunchTimeUtc) = p.PunchDate),
    Outcome        = CAST(NULL AS NVARCHAR(40)),
    RowsUpdated    = CAST(NULL AS INT)
INTO #Work
FROM @Pairs p
LEFT JOIN tblEmployee e ON e.Ecode = p.Ecode;

UPDATE #Work SET Outcome =
    CASE WHEN EmployeeId   IS NULL THEN N'SKIP - ecode not found'
         WHEN PunchRows    = 0     THEN N'SKIP - no geofence punch that date'
         WHEN CurrentStatus = 2    THEN N'SKIP - already Rejected'
         ELSE N'WILL REJECT' END;

/*------------------------------- PREVIEW ----------------------------------*/
IF @DryRun = 1
BEGIN
    PRINT '*** DRY RUN - nothing was written. Set @DryRun = 0 to apply. ***';
    SELECT Ecode, PunchDate, EmployeeId, PunchRows,
           CurrentStatus,
           CurrentStatusName = CASE CurrentStatus WHEN 1 THEN 'Approved'
                                                  WHEN 2 THEN 'Rejected'
                                                  WHEN 4 THEN 'Pending'
                                                  ELSE CAST(CurrentStatus AS varchar(10)) END,
           CurrentRemarks, PrevApproverId, Outcome
    FROM #Work ORDER BY Outcome, Ecode, PunchDate;

    SELECT Outcome, Pairs = COUNT(*), PunchesAffected = SUM(ISNULL(PunchRows,0))
    FROM #Work GROUP BY Outcome ORDER BY Outcome;
    RETURN;
END

/*------------------------------- EXECUTE ----------------------------------*/
DECLARE @Ecode NVARCHAR(50), @Dt DATE, @EmpId BIGINT;

DECLARE curReject CURSOR LOCAL FAST_FORWARD FOR
    SELECT Ecode, PunchDate, EmployeeId FROM #Work WHERE Outcome = N'WILL REJECT';

OPEN curReject;
FETCH NEXT FROM curReject INTO @Ecode, @Dt, @EmpId;

WHILE @@FETCH_STATUS = 0
BEGIN
    BEGIN TRY
        DECLARE @Res TABLE (RowsUpdated INT, EmployeeId BIGINT, PunchDate DATE,
                            StatusIdApplied INT, StatusNameApplied NVARCHAR(50));
        DELETE FROM @Res;

        INSERT INTO @Res
        EXEC dbo.usp_ApproveGeoAttendance
             @ManagerId     = 0,
             @Role          = N'SuperAdmin',
             @EmployeeId    = @EmpId,
             @PunchDate     = @Dt,
             @StatusId      = 2,             -- Rejected
             @Remarks       = @Remarks,
             @TimeZoneId    = N'UTC',
             @LastUpdatedBy = @ActorId;

        UPDATE w SET Outcome = N'REJECTED',
                     RowsUpdated = (SELECT TOP 1 RowsUpdated FROM @Res)
        FROM #Work w WHERE w.Ecode = @Ecode AND w.PunchDate = @Dt;
    END TRY
    BEGIN CATCH
        UPDATE w SET Outcome = N'ERROR: ' + LEFT(ERROR_MESSAGE(), 30)
        FROM #Work w WHERE w.Ecode = @Ecode AND w.PunchDate = @Dt;
    END CATCH

    FETCH NEXT FROM curReject INTO @Ecode, @Dt, @EmpId;
END

CLOSE curReject;
DEALLOCATE curReject;

/*-------------------------------- RESULT ----------------------------------*/
SELECT Ecode, PunchDate, EmployeeId, PunchRows,
       PreviousStatus = CurrentStatus, PreviousRemarks = CurrentRemarks,
       PreviousApproverId = PrevApproverId,
       Outcome, RowsUpdated
FROM #Work ORDER BY Outcome, Ecode, PunchDate;

SELECT Outcome, Pairs = COUNT(*), PunchesUpdated = SUM(ISNULL(RowsUpdated,0))
FROM #Work GROUP BY Outcome ORDER BY Outcome;

PRINT 'Done. Re-run usp_MergeMonthlyPunchesRange_Optimized for the affected date range';
PRINT 'so the attendance table reflects the rejections.';

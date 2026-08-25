/*
    Repoint_StrandedRegularizeRequests_20260824.sql
    PROD: 192.168.151.28\hrms, database HRMS

    Problem
    -------
    tblAttendanceRegularizationRequest stamps ReportingManagerId at creation time
    and never re-points it when the employee's reporting head changes. Approval
    authority is checked as req.ReportingManagerId == callerEmployeeId
    (Implementation/EmpAttendanceService.cs:812), so once the stamped manager
    leaves (IsActive = 0 / row deleted) the request can never be actioned by the
    employee's real manager - only by SuperAdmin / IT Superadmin / Master /
    Regularize HR, who bypass the manager check.

    Prod state 2026-08-24: 12,394 pending requests are stamped to an inactive or
    missing manager -
        8,142 requests (1,637 employees) whose employee HAS an active reporting
              head today                                    <-- re-pointed by this script
        4,252 requests (1,010 employees) whose employee has NO active head at all
              <-- NOT touched; nothing valid to point them at. These need either a
                  reporting-head assignment in the employee master, or clearing by
                  a SuperAdmin / Regularize HR. Full list:
                  Stranded_RegularizeRequests_20260824.csv (Action = NO ACTIVE HEAD)

    What this does
    --------------
    For PENDING requests (ManagerApprovalStatusId = 4 or NULL) whose stamped
    manager is inactive/missing, sets ReportingManagerId to the EmployeeId of the
    employee's current reporting head, where that head is active and not deleted
    (matched tblEmployee.ReportheadEcode -> tblEmployee.Ecode).

    Nothing else changes: no status, no approval column, no remark. Already-decided
    requests are untouched, and requests whose stamped manager is still active are
    untouched.

    Rollback: the backup table holds every AttendanceRequestId with its old
    ReportingManagerId - see the block at the bottom.
*/

SET XACT_ABORT ON;
BEGIN TRAN;

/* ---------- 1. capture old -> new for exactly the rows being changed ---------- */
IF OBJECT_ID('dbo.RegRequest_RepointBak_20260824') IS NOT NULL
    THROW 50001, 'Backup table RegRequest_RepointBak_20260824 already exists - review it before re-running.', 1;

SELECT req.AttendanceRequestId,
       req.EmployeeId,
       req.RequestDate,
       req.ReportingManagerId AS OldReportingManagerId,
       newm.EmployeeId        AS NewReportingManagerId,
       GETDATE()              AS CapturedOn
INTO dbo.RegRequest_RepointBak_20260824
FROM dbo.tblAttendanceRegularizationRequest req
JOIN dbo.tblEmployee e         ON e.EmployeeId = req.EmployeeId
LEFT JOIN dbo.tblEmployee oldm ON oldm.EmployeeId = req.ReportingManagerId
JOIN dbo.tblEmployee newm      ON newm.Ecode = e.ReportheadEcode
                              AND newm.IsActive = 1
                              AND ISNULL(newm.IsDeleted,0) = 0
WHERE ISNULL(req.ManagerApprovalStatusId,4) = 4
  AND (oldm.EmployeeId IS NULL OR oldm.IsActive = 0)
  AND newm.EmployeeId <> req.ReportingManagerId;

DECLARE @n INT = (SELECT COUNT(*) FROM dbo.RegRequest_RepointBak_20260824);
PRINT CONCAT('requests to re-point: ', @n, '  (expected 8142)');

IF @n NOT BETWEEN 1 AND 15000
BEGIN
    ROLLBACK TRAN;
    THROW 50002, 'Unexpected row count - nothing changed. Re-check the scope before applying.', 1;
END

/* ---------- 2. the re-point ---------- */
UPDATE req
SET req.ReportingManagerId = b.NewReportingManagerId,
    req.UpdatedOn          = GETDATE(),
    req.LastUpdatedBy      = 'repoint-stranded-20260824'
FROM dbo.tblAttendanceRegularizationRequest req
JOIN dbo.RegRequest_RepointBak_20260824 b
     ON b.AttendanceRequestId = req.AttendanceRequestId;

PRINT CONCAT('requests re-pointed: ', @@ROWCOUNT);

COMMIT TRAN;

/* ---------- 3. verification ---------- */
-- pending requests still stamped to an inactive/missing manager, split by whether
-- an active head exists. The "active head exists" bucket should now be 0.
SELECT CASE WHEN newm.EmployeeId IS NULL THEN 'no active head (expected to remain)'
            ELSE 'active head exists (should be 0)' END AS Bucket,
       COUNT(*) AS Requests
FROM dbo.tblAttendanceRegularizationRequest req
JOIN dbo.tblEmployee e         ON e.EmployeeId = req.EmployeeId
LEFT JOIN dbo.tblEmployee oldm ON oldm.EmployeeId = req.ReportingManagerId
LEFT JOIN dbo.tblEmployee newm ON newm.Ecode = e.ReportheadEcode AND newm.IsActive = 1 AND ISNULL(newm.IsDeleted,0) = 0
WHERE ISNULL(req.ManagerApprovalStatusId,4) = 4
  AND (oldm.EmployeeId IS NULL OR oldm.IsActive = 0)
GROUP BY CASE WHEN newm.EmployeeId IS NULL THEN 'no active head (expected to remain)'
              ELSE 'active head exists (should be 0)' END;

-- V54778 Sahil Kumar's 9 requests should now sit with V18992 DEVWART KUMAR (44670)
SELECT req.AttendanceRequestId, req.RequestDate, req.ReportingManagerId, m.Ecode AS MgrEcode, m.IsActive AS MgrActive
FROM dbo.tblAttendanceRegularizationRequest req
LEFT JOIN dbo.tblEmployee m ON m.EmployeeId = req.ReportingManagerId
WHERE req.EmployeeId = 145283
ORDER BY req.RequestDate DESC;

/* ---------------- ROLLBACK (run only if needed) ----------------
UPDATE req SET req.ReportingManagerId = b.OldReportingManagerId
FROM dbo.tblAttendanceRegularizationRequest req
JOIN dbo.RegRequest_RepointBak_20260824 b ON b.AttendanceRequestId = req.AttendanceRequestId;
-- then, once verified:
-- DROP TABLE dbo.RegRequest_RepointBak_20260824;
--------------------------------------------------------------- */

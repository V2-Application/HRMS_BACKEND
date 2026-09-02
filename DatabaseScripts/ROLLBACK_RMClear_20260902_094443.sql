-- Rollback for the reporting-manager cleanup run at 20260902_094443
-- Restores tblEmployee.ReportHeadEcode from the backup taken before the update.
-- Safe to re-run; only touches the 250 employees that were changed.

BEGIN TRANSACTION;

UPDATE e
   SET e.ReportHeadEcode = b.OldReportHeadEcode,
       e.UpdatedBy       = 'rollback-20260902_094443',
       e.UpdatedOn       = GETDATE()
FROM dbo.tblEmployee e
JOIN dbo.bk_tblEmployee_RMClear_20260902_094443 b ON b.EmployeeId = e.EmployeeId;

-- expected: 250 rows
PRINT 'Rows restored: ' + CAST(@@ROWCOUNT AS varchar(10));

COMMIT TRANSACTION;
-- To inspect first, run: SELECT * FROM dbo.bk_tblEmployee_RMClear_20260902_094443;

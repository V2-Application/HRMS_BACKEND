/*
    Official Visit feature -- new table.  2026-08-06

    Employee applies for an official visit over a date range; goes to their reporting
    manager for Approve/Reject (ReportingManagerId snapshotted from tblEmployee.ReportHeadEcode
    at apply time, same convention as tblAttendanceRegularizationRequest). HR can also bulk-upload
    rows via the admin page -- those are auto-approved (SourceTypeId=2), no manager involved.

    Department/Sub-Department/Designation/Base-Location are NOT stored here -- joined live from
    tblEmployee at query time (same principle as usp_GetGeoAttendanceByRange's Department/SubDept
    join added 2026-07-29), so they always reflect the employee's CURRENT org placement.
    VisitStoreCode IS stored (it's the destination being applied for, not derived from the
    employee record) but its name is still joined live from tblLocation, never duplicated.

    Idempotent + purely additive: CREATE TABLE IF NOT EXISTS only. No drops, no updates to any
    other table.
*/
SET NOCOUNT ON;

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'tblOfficialVisitRequest')
BEGIN
    CREATE TABLE dbo.tblOfficialVisitRequest
    (
        OfficialVisitRequestId  BIGINT IDENTITY(1,1) PRIMARY KEY,

        EmployeeId              BIGINT NOT NULL,            -- applicant (tblEmployee.EmployeeId)
        Ecode                   NVARCHAR(50)  NULL,          -- snapshot, avoids a join for grid/export
        EmployeeName            NVARCHAR(200) NULL,          -- snapshot

        FromDate                DATE NOT NULL,
        ToDate                  DATE NOT NULL,
        NoOfDays                INT NOT NULL,                -- computed at insert: DATEDIFF(DAY,FromDate,ToDate)+1
        Purpose                 NVARCHAR(500) NULL,
        VisitStoreCode          NVARCHAR(50)  NULL,          -- destination store's STCode (tblLocation); name joined live
        EmployeeRemarks         NVARCHAR(500) NULL,

        ReportingManagerId      BIGINT NULL,                 -- snapshotted resolved manager EmployeeId; NULL for HR-uploaded rows
        ManagerApprovalStatusId INT NULL,                    -- 1=Approved, 2=Rejected, 4=Pending (HRMSAPI.DTO.AttendanceStatuses); NULL = n/a (HR upload)
        ManagerApproverId       BIGINT NULL,
        ManagerApprovalOn       DATETIME NULL,
        ManagerRemarks          NVARCHAR(500) NULL,

        SourceTypeId            INT NOT NULL DEFAULT 1,      -- 1 = SelfApply (goes to manager), 2 = HRUpload (auto-approved)

        CreatedBy               NVARCHAR(50)  NULL,
        CreatedOn               DATETIME NOT NULL DEFAULT GETDATE(),
        LastUpdatedBy           NVARCHAR(50)  NULL,
        UpdatedOn               DATETIME NULL
    );

    CREATE INDEX IX_tblOfficialVisitRequest_EmployeeId ON dbo.tblOfficialVisitRequest(EmployeeId);
    CREATE INDEX IX_tblOfficialVisitRequest_ManagerQueue ON dbo.tblOfficialVisitRequest(ReportingManagerId, ManagerApprovalStatusId);
    CREATE INDEX IX_tblOfficialVisitRequest_DateRange ON dbo.tblOfficialVisitRequest(FromDate, ToDate);

    PRINT 'Created dbo.tblOfficialVisitRequest.';
END
ELSE
    PRINT 'dbo.tblOfficialVisitRequest already exists -- no changes made.';
GO

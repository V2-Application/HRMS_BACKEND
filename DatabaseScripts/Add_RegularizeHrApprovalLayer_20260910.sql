/* =============================================================================
   Attendance Regularization: third approval layer (HR)
   -----------------------------------------------------------------------------
   Approval chain becomes:  Reporting Manager -> LP/Audit -> HR (IT Superadmin)

   Rules implemented alongside this script (EmpAttendanceService.ApproveRegularizationAsync):
     * HR layer belongs to IT Superadmin (and the purpose-built "Regularize HR"
       role). HR approval also stamps Manager + LP as Approved, so once HR has
       approved, no other approval is needed.
     * SuperAdmin / Master keep stamping Manager + LP, but that no longer
       finalizes the request -- the final status only turns Approved once HR
       has approved.
     * Any layer rejecting still finalizes the request as Rejected.

   This script:
     1. ADDs four nullable HR columns. Existing rows are NOT touched, so the
        81k requests already on file keep their current Manager/LP/final values
        and simply have no HR entry.
     2. Rewrites both export procs to return the HR layer, plus the ecode/name
        of whoever actually approved at each layer (the reports previously only
        carried the reporting manager's ecode).

   ADD COLUMN + ALTER PROCEDURE only. Nothing is dropped, deleted or truncated.
   Re-runnable.
   ============================================================================= */

SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.tblAttendanceRegularizationRequest', N'U') IS NULL
BEGIN
    RAISERROR('dbo.tblAttendanceRegularizationRequest does not exist - aborting.', 16, 1);
    RETURN;
END

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'dbo.tblAttendanceRegularizationRequest') AND name = N'HrApprovalStatusId')
BEGIN
    ALTER TABLE dbo.tblAttendanceRegularizationRequest ADD HrApprovalStatusId int NULL;
    PRINT 'Added HrApprovalStatusId';
END
ELSE PRINT 'HrApprovalStatusId already present - skipped';

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'dbo.tblAttendanceRegularizationRequest') AND name = N'HrApproverId')
BEGIN
    ALTER TABLE dbo.tblAttendanceRegularizationRequest ADD HrApproverId bigint NULL;
    PRINT 'Added HrApproverId';
END
ELSE PRINT 'HrApproverId already present - skipped';

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'dbo.tblAttendanceRegularizationRequest') AND name = N'HrApprovalOn')
BEGIN
    ALTER TABLE dbo.tblAttendanceRegularizationRequest ADD HrApprovalOn datetime NULL;
    PRINT 'Added HrApprovalOn';
END
ELSE PRINT 'HrApprovalOn already present - skipped';

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'dbo.tblAttendanceRegularizationRequest') AND name = N'HrRemarks')
BEGIN
    ALTER TABLE dbo.tblAttendanceRegularizationRequest ADD HrRemarks nvarchar(500) NULL;
    PRINT 'Added HrRemarks';
END
ELSE PRINT 'HrRemarks already present - skipped';

GO

-- Helpful for the HR queue (requests waiting on HR) on an 81k-row table.
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE object_id = OBJECT_ID(N'dbo.tblAttendanceRegularizationRequest')
                 AND name = N'IX_tblAttendanceRegularizationRequest_HrApprovalStatusId')
BEGIN
    CREATE NONCLUSTERED INDEX IX_tblAttendanceRegularizationRequest_HrApprovalStatusId
        ON dbo.tblAttendanceRegularizationRequest (HrApprovalStatusId, StatusId)
        INCLUDE (ManagerApprovalStatusId, LpApprovalStatusId);
    PRINT 'Created IX_tblAttendanceRegularizationRequest_HrApprovalStatusId';
END
ELSE PRINT 'HR index already present - skipped';

GO

-- -----------------------------------------------------------------------------
-- dbo.usp_GetAttendanceRegularization  (month export)
-- Now also returns the HR layer and the ecode/name of each actual approver.
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.usp_GetAttendanceRegularization
    @MonthYear VARCHAR(10)   -- Format: MMM-yy (e.g., 'Nov-25')
AS
BEGIN
    SET NOCOUNT ON;

    -- Month/Year split kept exactly as the original proc did it.
    DECLARE @Month INT, @Year INT;

    SELECT
        @Month = MONTH(CONVERT(DATE, '01-' + @MonthYear, 106)),
        @Year  = YEAR(CONVERT(DATE, '01-' + @MonthYear, 106));

    SELECT
        b.Ecode,
        COALESCE(b.[FULL NAME], b.FirstName + b.MiddleName + b.LastName) AS EmpName,
        h.STCode, h.LocationName,
        i.DepartmentName, j.DesignationName,
        a.[RequestDate],
        a.[Reason],
        f.Ecode AS RM_ECODE,
        COALESCE(f.[FULL NAME], f.FirstName + f.MiddleName + f.LastName) AS ReportManagerName,
        a.[PunchIn],
        a.[PunchOut],
        c.StatusName,
        a.[FileUrl],
        a.[PunchTypeId],
        g.RequestTypeName,
        a.[EmployeeRemarks],
        d.StatusName AS ManagerStatus,
        a.[ManagerApprovalOn],
        a.[ManagerRemarks],
        ma.Ecode AS ManagerApproverEcode,
        COALESCE(ma.[FULL NAME], ma.FirstName + ma.MiddleName + ma.LastName) AS ManagerApproverName,
        e.StatusName AS [LpApprovalStatus],
        a.[LpApprovalOn],
        a.[LpRemarks],
        la.Ecode AS LpApproverEcode,
        COALESCE(la.[FULL NAME], la.FirstName + la.MiddleName + la.LastName) AS LpApproverName,
        hs.StatusName AS HrApprovalStatus,
        a.[HrApprovalOn],
        a.[HrRemarks],
        ha.Ecode AS HrApproverEcode,
        COALESCE(ha.[FULL NAME], ha.FirstName + ha.MiddleName + ha.LastName) AS HrApproverName
    FROM tblAttendanceRegularizationRequest a
    LEFT JOIN tblEmployee b      ON a.EmployeeId = b.EmployeeId
    LEFT JOIN tblLocation h      ON h.LocationId = b.LocationId
    LEFT JOIN tblStatus c        ON c.StatusId = a.StatusId
    LEFT JOIN tblStatus d        ON d.StatusId = a.ManagerApprovalStatusId
    LEFT JOIN tblStatus e        ON e.StatusId = a.LpApprovalStatusId
    LEFT JOIN tblStatus hs       ON hs.StatusId = a.HrApprovalStatusId
    LEFT JOIN tblEmployee f      ON f.EmployeeId = a.ReportingManagerId
    LEFT JOIN tblEmployee ma     ON ma.EmployeeId = a.ManagerApproverId
    LEFT JOIN tblEmployee la     ON la.EmployeeId = a.LpApproverId
    LEFT JOIN tblEmployee ha     ON ha.EmployeeId = a.HrApproverId
    LEFT JOIN tblRequestTypes g  ON a.RequestTypeId = g.RequestTypeId
    LEFT JOIN tblDepartment i    ON b.DepartmentId = i.DepartmentId
    LEFT JOIN tblDesignation j   ON b.DesignationId = j.DesignationId
    WHERE
        MONTH(a.RequestDate) = @Month
        AND YEAR(a.RequestDate) = @Year
    ORDER BY a.RequestDate, b.Ecode;
END

GO

-- -----------------------------------------------------------------------------
-- dbo.usp_GetAttendanceRegularizationByRange
-- Export: filter by date range and optional per-layer status filters.
-- @Status          -> overall request StatusName  (Approved / Pending / Rejected)
-- @ManagerStatus   -> manager approval StatusName
-- @LpStatus        -> LP approval StatusName
-- @HrStatus        -> HR approval StatusName      (NEW third layer)
-- Combine @ManagerStatus='Approved' + @LpStatus='Approved' + @HrStatus='Pending'
-- to get "cleared by Manager and LP, waiting on HR".
-- @HrStatus='Pending' also matches rows with no HR entry at all, which is what
-- every request created before the HR layer existed looks like.
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.usp_GetAttendanceRegularizationByRange
    @StartDate     DATE,
    @EndDate       DATE,
    @Status        VARCHAR(50) = NULL,
    @ManagerStatus VARCHAR(50) = NULL,
    @LpStatus      VARCHAR(50) = NULL,
    @HrStatus      VARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        b.Ecode,
        COALESCE(b.[FULL NAME], b.FirstName + b.MiddleName + b.LastName) AS EmpName,
        h.STCode, h.LocationName,
        i.DepartmentName, j.DesignationName,
        a.[RequestDate],
        a.[Reason],
        f.Ecode AS RM_ECODE,
        COALESCE(f.[FULL NAME], f.FirstName + f.MiddleName + f.LastName) AS ReportManagerName,
        a.[PunchIn],
        a.[PunchOut],
        c.StatusName,
        a.[FileUrl],
        a.[PunchTypeId],
        g.RequestTypeName,
        a.[EmployeeRemarks],
        d.StatusName AS ManagerStatus,
        a.[ManagerApprovalOn],
        a.[ManagerRemarks],
        ma.Ecode AS ManagerApproverEcode,
        COALESCE(ma.[FULL NAME], ma.FirstName + ma.MiddleName + ma.LastName) AS ManagerApproverName,
        e.StatusName AS [LpApprovalStatus],
        a.[LpApprovalOn],
        a.[LpRemarks],
        la.Ecode AS LpApproverEcode,
        COALESCE(la.[FULL NAME], la.FirstName + la.MiddleName + la.LastName) AS LpApproverName,
        hs.StatusName AS HrApprovalStatus,
        a.[HrApprovalOn],
        a.[HrRemarks],
        ha.Ecode AS HrApproverEcode,
        COALESCE(ha.[FULL NAME], ha.FirstName + ha.MiddleName + ha.LastName) AS HrApproverName
    FROM tblAttendanceRegularizationRequest a
    LEFT JOIN tblEmployee b      ON a.EmployeeId = b.EmployeeId
    LEFT JOIN tblLocation h      ON h.LocationId = b.LocationId
    LEFT JOIN tblStatus c        ON c.StatusId = a.StatusId
    LEFT JOIN tblStatus d        ON d.StatusId = a.ManagerApprovalStatusId
    LEFT JOIN tblStatus e        ON e.StatusId = a.LpApprovalStatusId
    LEFT JOIN tblStatus hs       ON hs.StatusId = a.HrApprovalStatusId
    LEFT JOIN tblEmployee f      ON f.EmployeeId = a.ReportingManagerId
    LEFT JOIN tblEmployee ma     ON ma.EmployeeId = a.ManagerApproverId
    LEFT JOIN tblEmployee la     ON la.EmployeeId = a.LpApproverId
    LEFT JOIN tblEmployee ha     ON ha.EmployeeId = a.HrApproverId
    LEFT JOIN tblRequestTypes g  ON a.RequestTypeId = g.RequestTypeId
    LEFT JOIN tblDepartment i    ON b.DepartmentId = i.DepartmentId
    LEFT JOIN tblDesignation j   ON b.DesignationId = j.DesignationId
    WHERE
        a.RequestDate >= @StartDate
        AND a.RequestDate <= @EndDate
        AND (@Status        IS NULL OR @Status        = '' OR c.StatusName = @Status)
        AND (@ManagerStatus IS NULL OR @ManagerStatus = '' OR d.StatusName = @ManagerStatus)
        AND (@LpStatus      IS NULL OR @LpStatus      = '' OR e.StatusName = @LpStatus)
        AND (@HrStatus      IS NULL OR @HrStatus      = ''
             OR hs.StatusName = @HrStatus
             OR (@HrStatus = 'Pending' AND a.HrApprovalStatusId IS NULL))
    ORDER BY a.RequestDate, b.Ecode;
END

GO

-- Verification
SELECT c.name AS ColumnName, t.name AS DataType, c.is_nullable AS IsNullable
FROM sys.columns c JOIN sys.types t ON t.user_type_id = c.user_type_id
WHERE c.object_id = OBJECT_ID(N'dbo.tblAttendanceRegularizationRequest')
  AND c.name IN (N'HrApprovalStatusId', N'HrApproverId', N'HrApprovalOn', N'HrRemarks');

SELECT COUNT(*) AS TotalRequests,
       SUM(CASE WHEN HrApprovalStatusId IS NULL THEN 1 ELSE 0 END) AS NoHrEntryYet
FROM dbo.tblAttendanceRegularizationRequest;

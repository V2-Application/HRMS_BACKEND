-- =============================================================================
-- Category: Regularize
-- Source:   dev DB (192.168.151.27\KARMA / HRMS)
-- Generated: 2026-05-14 12:15:05
-- Objects rewritten as CREATE OR ALTER for safe re-run on production.
-- =============================================================================
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- -----------------------------------------------------------------------------
-- dbo.sp_GetRegularizeRequests
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE sp_GetRegularizeRequests
    @StartDate DATE,
    @EndDate DATE,
    @ManagerEcode VARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        e.Ecode,
        e.[FULL NAME],
        e.FirstName,
        e.LastName,
        r.RequestDate,
        r.Status,
        r.Reason,
        s.StatusName,
        rs.[FULL NAME] AS ReportingManagerName,
        r.EmployeeRemarks,
        r.StatusId,
        r.PunchIn,
        r.PunchOut
    FROM tblAttendanceRegularizationRequest r 
        INNER JOIN tblEmployee e ON e.EmployeeId = r.EmployeeId
        INNER JOIN tblStatus s ON s.StatusId = r.StatusId
        INNER JOIN tblEmployee rs ON rs.Ecode = e.ReportHeadEcode
        INNER JOIN tblEmployeeMultiPunches p ON p.UserID = e.Ecode 
            AND p.PunchDate = r.RequestDate
            AND p.IsRegularize = 1
    WHERE r.RequestDate >= @StartDate 
        AND r.RequestDate <= @EndDate
        AND (@ManagerEcode IS NULL OR e.ReportHeadEcode = @ManagerEcode);
END;
GO

-- -----------------------------------------------------------------------------
-- dbo.sp_GetRegularizeRequestsBulk
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE sp_GetRegularizeRequestsBulk
    @MonthYear VARCHAR(10) = NULL,       -- e.g. 'May-2025'
    @StartDate DATE = NULL,
    @EndDate DATE = NULL,
    @ManagerEcode VARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- Derive StartDate and EndDate from MonthYear if provided
    IF @MonthYear IS NOT NULL AND (@StartDate IS NULL OR @EndDate IS NULL)
    BEGIN
        BEGIN TRY
            SET @StartDate = CONVERT(DATE, '01-' + @MonthYear, 113); -- Format: dd-MMM-yyyy
            SET @EndDate = EOMONTH(@StartDate);
        END TRY
        BEGIN CATCH
            RAISERROR('Invalid MonthYear format. Use format like May-2025.', 16, 1);
            RETURN;
        END CATCH
    END

    -- Validate that we now have start and end dates
    IF @StartDate IS NULL OR @EndDate IS NULL
    BEGIN
        RAISERROR('StartDate and EndDate must be provided, or MonthYear must be valid.', 16, 1);
        RETURN;
    END

    SELECT 
        e.Ecode,
        e.[FULL NAME],
        e.FirstName,
        e.LastName,
        r.RequestDate,
        r.Reason,
        s.StatusName,
        rs.[FULL NAME] AS ReportingManagerName,
        r.EmployeeRemarks,
        r.PunchIn,
        r.PunchOut
    FROM tblAttendanceRegularizationRequest r 
        INNER JOIN tblEmployee e ON e.EmployeeId = r.EmployeeId
        INNER JOIN tblStatus s ON s.StatusId = r.StatusId
        INNER JOIN tblEmployee rs ON rs.Ecode = e.ReportHeadEcode
        INNER JOIN tblEmployeeMultiPunches p ON p.UserID = e.Ecode 
            AND p.PunchDate = r.RequestDate
            AND p.IsRegularize = 1
    WHERE r.RequestDate BETWEEN @StartDate AND @EndDate
        AND (@ManagerEcode IS NULL OR e.ReportHeadEcode = @ManagerEcode);
END;
GO

-- -----------------------------------------------------------------------------
-- dbo.usp_GetAttendanceRegularization
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE usp_GetAttendanceRegularization 
--'Nov-25'
    @MonthYear VARCHAR(10)   -- Format: MMM-yy (e.g., 'Nov-25')
AS
BEGIN
    SET NOCOUNT ON;

    ------------------------------------------------------------
    -- Convert MMM-yy into numeric Month & Year
    ------------------------------------------------------------
    DECLARE @Month INT, @Year INT;

    SELECT 
        @Month = MONTH(CONVERT(DATE, '01-' + @MonthYear, 106)),
        @Year  = YEAR(CONVERT(DATE, '01-' + @MonthYear, 106));

    ------------------------------------------------------------
    -- Main Query
    ------------------------------------------------------------
    SELECT 
        b.Ecode,
        COALESCE(b.[FULL NAME], b.FirstName + b.MiddleName + b.LastName) AS EmpName,
        h.STCode,h.LocationName,
        i.DepartmentName,j.DesignationName,
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
        e.StatusName AS [LpApprovalStatus],
        a.[LpApprovalOn],
        a.[LpRemarks]
    FROM tblAttendanceRegularizationRequest a
    LEFT JOIN tblEmployee b ON a.EmployeeId = b.EmployeeId
    LEFT JOIN tblLocation h ON h.LocationId = b.LocationId
    LEFT JOIN tblStatus c ON c.StatusId = a.StatusId
    LEFT JOIN tblStatus d ON d.StatusId = a.ManagerApprovalStatusId
    LEFT JOIN tblStatus e ON e.StatusId = a.LpApprovalStatusId
    LEFT JOIN tblEmployee f ON f.EmployeeId = a.ReportingManagerId
    LEFT JOIN tblRequestTypes g ON a.RequestTypeId = g.RequestTypeId
    LEFT JOIN tblDepartment i ON b.DepartmentId = i.DepartmentId
    LEFT JOIN tblDesignation j ON b.DesignationId = j.DesignationId
    WHERE
        MONTH(a.RequestDate) = @Month
        AND YEAR(a.RequestDate) = @Year
    order by a.RequestDate,b.Ecode
END
GO

-- -----------------------------------------------------------------------------
-- dbo.usp_GetAttendanceRegularizationByRange
-- SuperAdmin export: filter by date range and optional status filters.
-- @Status          -> overall request StatusName  (Approved / Pending / Rejected)
-- @ManagerStatus   -> manager approval StatusName (Approved / Pending / Rejected)
-- @LpStatus        -> LP approval StatusName      (Approved / Pending / Rejected)
-- Combine @ManagerStatus='Approved' + @LpStatus='Pending' to get
-- "Approved by Manager, Pending by LP".
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE usp_GetAttendanceRegularizationByRange
    @StartDate     DATE,
    @EndDate       DATE,
    @Status        VARCHAR(50) = NULL,
    @ManagerStatus VARCHAR(50) = NULL,
    @LpStatus      VARCHAR(50) = NULL
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
        e.StatusName AS [LpApprovalStatus],
        a.[LpApprovalOn],
        a.[LpRemarks]
    FROM tblAttendanceRegularizationRequest a
    LEFT JOIN tblEmployee b      ON a.EmployeeId = b.EmployeeId
    LEFT JOIN tblLocation h      ON h.LocationId = b.LocationId
    LEFT JOIN tblStatus c        ON c.StatusId = a.StatusId
    LEFT JOIN tblStatus d        ON d.StatusId = a.ManagerApprovalStatusId
    LEFT JOIN tblStatus e        ON e.StatusId = a.LpApprovalStatusId
    LEFT JOIN tblEmployee f      ON f.EmployeeId = a.ReportingManagerId
    LEFT JOIN tblRequestTypes g  ON a.RequestTypeId = g.RequestTypeId
    LEFT JOIN tblDepartment i    ON b.DepartmentId = i.DepartmentId
    LEFT JOIN tblDesignation j   ON b.DesignationId = j.DesignationId
    WHERE
        a.RequestDate >= @StartDate
        AND a.RequestDate <= @EndDate
        AND (@Status        IS NULL OR @Status        = '' OR c.StatusName = @Status)
        AND (@ManagerStatus IS NULL OR @ManagerStatus = '' OR d.StatusName = @ManagerStatus)
        AND (@LpStatus      IS NULL OR @LpStatus      = '' OR e.StatusName = @LpStatus)
    ORDER BY a.RequestDate, b.Ecode;
END
GO


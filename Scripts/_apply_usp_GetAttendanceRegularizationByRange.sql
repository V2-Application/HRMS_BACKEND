SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

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

-- =============================================================================
-- Category: GeoAttendance (Geofence Requests Export)
-- Source:   dev DB (192.168.151.27\KARMA / HRMS)
-- Objects rewritten as CREATE OR ALTER for safe re-run on production.
-- =============================================================================
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- -----------------------------------------------------------------------------
-- dbo.usp_GetGeoAttendanceByRange
-- SuperAdmin export: geofence/geo-attendance approvals for a date range with
-- optional status filters.
--   @FinalStatus    -> tblStatus.StatusName for FinalStatusId
--   @ManagerStatus  -> tblStatus.StatusName for ManagerApprovalStatusId
--   @MasterStatus   -> tblStatus.StatusName for MasterApprovalStatusId
-- One row per (Employee, PunchDate). Includes punch counts and approval trail.
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.usp_GetGeoAttendanceByRange
    @StartDate     DATE,
    @EndDate       DATE,
    @FinalStatus   VARCHAR(50) = NULL,
    @ManagerStatus VARCHAR(50) = NULL,
    @MasterStatus  VARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH PunchAgg AS
    (
        SELECT
            ar.EmployeeId,
            CONVERT(DATE, ar.PunchTimeUtc) AS PunchDate,
            COUNT(*)                                          AS PunchCount,
            SUM(CASE WHEN ar.PunchType = 1 THEN 1 ELSE 0 END) AS PunchInCount,
            SUM(CASE WHEN ar.PunchType = 2 THEN 1 ELSE 0 END) AS PunchOutCount,
            MIN(ar.PunchTimeUtc)                              AS FirstPunchUtc,
            MAX(ar.PunchTimeUtc)                              AS LastPunchUtc
        FROM dbo.AttendanceRecord ar
        WHERE CONVERT(DATE, ar.PunchTimeUtc) BETWEEN @StartDate AND @EndDate
        GROUP BY ar.EmployeeId, CONVERT(DATE, ar.PunchTimeUtc)
    ),
    -- A punch day can carry several proof files (one per punch). DISTINCT first
    -- so a re-used path is not repeated, then collapse to one pipe-separated
    -- cell -- the API splits it back out and turns each into an absolute URL.
    ProofAgg AS
    (
        SELECT
            p.EmployeeId,
            p.PunchDate,
            STRING_AGG(CAST(p.ProofPath AS NVARCHAR(MAX)), N' | ') AS ProofPaths
        FROM
        (
            SELECT DISTINCT
                ar.EmployeeId,
                CONVERT(DATE, ar.PunchTimeUtc)  AS PunchDate,
                LTRIM(RTRIM(ar.ProofPath))      AS ProofPath
            FROM dbo.AttendanceRecord ar
            WHERE CONVERT(DATE, ar.PunchTimeUtc) BETWEEN @StartDate AND @EndDate
              AND NULLIF(LTRIM(RTRIM(ar.ProofPath)), N'') IS NOT NULL
        ) p
        GROUP BY p.EmployeeId, p.PunchDate
    )
    SELECT
        e.Ecode,
        COALESCE(e.[FULL NAME],
                 NULLIF(LTRIM(RTRIM(
                    ISNULL(e.FirstName, N'') + N' ' + ISNULL(e.LastName, N'')
                 )), N''),
                 N'Unknown') AS EmployeeName,
        d.DepartmentName,
        sd1.SubDepartmentName AS SubDepartment1,
        sd2.SubDepartmentName AS SubDepartment2,
        sd3.SubDepartmentName AS SubDepartment3,
        des.DesignationName,
        loc.LocationName,
        loc.STCode,
        rh.Ecode             AS ReportingManagerEcode,
        COALESCE(rh.[FULL NAME],
                 NULLIF(LTRIM(RTRIM(
                    ISNULL(rh.FirstName, N'') + N' ' + ISNULL(rh.LastName, N'')
                 )), N''),
                 N'') AS ReportingManagerName,
        pa.PunchDate,
        pa.PunchCount,
        pa.PunchInCount,
        pa.PunchOutCount,
        pa.FirstPunchUtc,
        pa.LastPunchUtc,
        -- ISNULL → 'Pending' so punch-days with no GeoAttendanceApproval row
        -- (created lazily on first approver action) render as Pending rather
        -- than blank in the SuperAdmin export.
        ISNULL(sm.StatusName,  'Pending') AS ManagerStatus,
        ga.ManagerApproverId,
        ga.ManagerApprovalOn,
        ga.ManagerRemarks,
        ISNULL(sms.StatusName, 'Pending') AS MasterStatus,
        ga.MasterApproverId,
        ga.MasterApprovalOn,
        ga.MasterRemarks,
        ISNULL(sf.StatusName,  'Pending') AS FinalStatus,
        pr.ProofPaths
    FROM PunchAgg pa
    INNER JOIN dbo.tblEmployee e        ON e.EmployeeId = pa.EmployeeId
    LEFT JOIN ProofAgg pr
        ON pr.EmployeeId = pa.EmployeeId AND pr.PunchDate = pa.PunchDate
    LEFT JOIN dbo.GeoAttendanceApproval ga
        ON ga.EmployeeId = pa.EmployeeId AND ga.PunchDate = pa.PunchDate
    LEFT JOIN dbo.tblStatus sm          ON sm.StatusId = ga.ManagerApprovalStatusId
    LEFT JOIN dbo.tblStatus sms         ON sms.StatusId = ga.MasterApprovalStatusId
    LEFT JOIN dbo.tblStatus sf          ON sf.StatusId = ga.FinalStatusId
    LEFT JOIN dbo.tblDepartment d       ON d.DepartmentId = e.DepartmentId
    -- Sub-department chain off the EMPLOYEE record (tblEmployee.SubDepartmentId1/2/3);
    -- any level may be null, hence three independent LEFT JOINs.
    LEFT JOIN dbo.tblSubDepartment sd1   ON sd1.SubDepartmentId = e.SubDepartmentId1
    LEFT JOIN dbo.tblSubDepartment sd2   ON sd2.SubDepartmentId = e.SubDepartmentId2
    LEFT JOIN dbo.tblSubDepartment sd3   ON sd3.SubDepartmentId = e.SubDepartmentId3
    LEFT JOIN dbo.tblDesignation des    ON des.DesignationId = e.DesignationId
    LEFT JOIN dbo.tblLocation loc       ON loc.LocationId = e.LocationId
    LEFT JOIN dbo.tblEmployee rh        ON rh.Ecode = e.ReportheadEcode
    WHERE
        -- Filters mirror the SELECT projection: a NULL status (no approval row)
        -- is treated as 'Pending' for both display AND filtering.
        (@FinalStatus   IS NULL OR @FinalStatus   = '' OR ISNULL(sf.StatusName, 'Pending')  = @FinalStatus)
        AND (@ManagerStatus IS NULL OR @ManagerStatus = '' OR ISNULL(sm.StatusName, 'Pending')  = @ManagerStatus)
        AND (@MasterStatus  IS NULL OR @MasterStatus  = '' OR ISNULL(sms.StatusName, 'Pending') = @MasterStatus)
    ORDER BY pa.PunchDate DESC, e.Ecode;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_EmployeeWiseAbscondingReport
    @AsOfDate DATE = NULL    -- ageing computed against this date; defaults to today
AS
BEGIN
    SET NOCOUNT ON;

    /*
      EMP-WISE ABSCONDING REPORT  (read-only; marks/changes nothing)
      One row per employee whose CURRENT STATUS is absconded:
        tblEmployeeSepration.ResignationTypeId = 10 'Absconding', not revoked, AND the
        employee is currently inactive (tblEmployee.IsActive = 0).
      The IsActive = 0 guard excludes employees who were marked absconding earlier but have
        since returned / been reactivated and are punching again, yet whose old absconding
        separation row was never revoked (these would otherwise show a recent LAST PUNCHING DT).
      Columns:
        LOC CD            = tblLocation.STCode
        LOC NM            = tblLocation.LocationName
        LOC TYPE          = tblLocationType.LocationTypeName (1=HO,2=DC,3=Hub,4=Store); blank/untyped -> 'DC'
        STATS (ACTIVE/UPC)= location's status from v2parivar: 'Active' when tblLocation.IsActive=1,
                            else 'UPC' (upcoming / not-yet-active store; matches LocationService toggle)
        EMP CODE          = tblEmployee.ECode
        EMP NM            = tblEmployee.[FULL NAME]
        DEPT.             = tblDepartment.DepartmentName
        SUB.-DEPT. 1/2/3  = tblSubDepartment.SubDepartmentName for employee SubDepartmentId1/2/3 (all 3 levels)
        DESGN.            = tblDesignation.DesignationName
        LAST PUNCHING DT  = latest actual punch date (biometric tblEmployeeMultiPunches OR approved geofence AttendanceRecord)
        PRESENT AGEING    = days from LAST PUNCHING DT to @AsOfDate
    */

    DECLARE @ToDate DATE = ISNULL(@AsOfDate, CAST(GETDATE() AS DATE));

    ;WITH absc AS (
        SELECT s.EmployeeId, MAX(CAST(s.ResignationDate AS DATE)) AS AbscondDate
        FROM dbo.tblEmployeeSepration s WITH (NOLOCK)
        WHERE s.ResignationTypeId = 10 AND ISNULL(s.IsRevoked, 0) = 0
        GROUP BY s.EmployeeId
    )
    SELECT
        l.STCode                                                  AS [LOC CD],
        l.LocationName                                            AS [LOC NM],
        CASE
            WHEN l.STCode = 'RH01' OR UPPER(LTRIM(RTRIM(l.LocationName))) LIKE 'HO-NEW%' THEN 'HO new'
            WHEN l.STCode = 'RD04' OR UPPER(LTRIM(RTRIM(l.LocationName))) LIKE 'HO-OLD%' THEN 'Old HO'
            WHEN l.STCode = 'RH02' OR UPPER(LTRIM(RTRIM(l.LocationName))) LIKE '%CENTRAL%' THEN 'Central'
            WHEN l.STCode IN ('DW01','DH24') THEN 'DC'
            WHEN UPPER(LTRIM(RTRIM(l.LocationName))) LIKE '%DC%' OR UPPER(LTRIM(RTRIM(l.LocationName))) LIKE '%HUB%' THEN 'Hub'
            ELSE 'Store'
        END                                                       AS [LOC TYPE],
        CASE WHEN l.IsActive = 1 THEN 'Active' ELSE 'UPC' END     AS [STATS (ACTIVE/UPC)],
        e.ECode                                                   AS [EMP CODE],
        e.[FULL NAME]                                             AS [EMP NM],
        d.DepartmentName                                          AS [DEPT.],
        sd1.SubDepartmentName                                     AS [SUB.-DEPT. 1],
        sd2.SubDepartmentName                                     AS [SUB.-DEPT. 2],
        sd3.SubDepartmentName                                     AS [SUB.-DEPT. 3],
        dg.DesignationName                                        AS [DESGN.],
        lp.LastPunchDt                                            AS [LAST PUNCHING DT],
        CASE WHEN lp.LastPunchDt IS NULL THEN NULL
             ELSE DATEDIFF(DAY, lp.LastPunchDt, @ToDate) END      AS [PRESENT AGEING]
    FROM dbo.tblEmployee e WITH (NOLOCK)
    JOIN absc a ON a.EmployeeId = e.EmployeeId
    OUTER APPLY (
        SELECT MAX(d2) AS LastPunchDt FROM (VALUES
            ( (SELECT MAX(p.PunchDate) FROM dbo.tblEmployeeMultiPunches p WITH (NOLOCK) WHERE p.UserID = e.ECode) ),
            ( (SELECT MAX(CONVERT(date, ar.PunchTimeUtc)) FROM dbo.AttendanceRecord ar WITH (NOLOCK) WHERE ar.EmployeeId = e.EmployeeId AND ar.StatusId = 1) )
        ) v(d2)
    ) lp
    LEFT JOIN dbo.tblLocation l       WITH (NOLOCK) ON l.LocationId   = e.LocationId
    LEFT JOIN dbo.tblLocationType lt  WITH (NOLOCK) ON lt.Id          = TRY_CAST(NULLIF(LTRIM(RTRIM(l.LocationType)), '') AS INT)
    LEFT JOIN dbo.tblDepartment d     WITH (NOLOCK) ON d.DepartmentId = e.DepartmentId
    LEFT JOIN dbo.tblDesignation dg   WITH (NOLOCK) ON dg.DesignationId = e.DesignationId
    LEFT JOIN dbo.tblSubDepartment sd1 WITH (NOLOCK) ON sd1.SubDepartmentId = e.SubDepartmentId1
    LEFT JOIN dbo.tblSubDepartment sd2 WITH (NOLOCK) ON sd2.SubDepartmentId = e.SubDepartmentId2
    LEFT JOIN dbo.tblSubDepartment sd3 WITH (NOLOCK) ON sd3.SubDepartmentId = e.SubDepartmentId3
    WHERE e.IsActive = 0
    ORDER BY l.STCode, e.ECode;

    SET NOCOUNT OFF;
END;

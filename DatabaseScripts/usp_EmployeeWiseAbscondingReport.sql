CREATE OR ALTER PROCEDURE dbo.usp_EmployeeWiseAbscondingReport
    @AsOfDate DATE = NULL    -- ageing computed against this date; defaults to today
AS
BEGIN
    SET NOCOUNT ON;

    /*
      EMP-WISE ABSCONDING REPORT  (read-only; marks/changes nothing)
      ABSCONDING IS DETECTED PURELY FROM THE PUNCH GAP (not the system flag):
        an ACTIVE employee (tblEmployee.IsActive = 1) is "absconding" when the number of
        calendar days since their LAST actual punch (incl. Sat/Sun) reaches the threshold:
            HO  (STCode = 'RH01') -> PRESENT AGEING >= 8   (absent 7 days, appears on the 8th)
            all other locations   -> PRESENT AGEING >= 7   (absent 6 days, appears on the 7th)
      LAST actual punch = latest biometric (tblEmployeeMultiPunches) OR approved geofence
        attendance (AttendanceRecord, StatusId = 1).
      Employees who have NEVER punched (no last punch) are EXCLUDED (no gap to measure).
      Store-login accounts (ECode = a store STCode) are EXCLUDED.
      NOTE: requires CURRENT punch data - on environments whose punch data is a stale snapshot
            (e.g. dev) almost everyone will appear; validate on the live (prod) punch data.
      Columns: LOC CD / LOC NM / LOC TYPE / STATS (ACTIVE/UPC) / EMP CODE / EMP NM / DEPT. /
               SUB.-DEPT. 1/2/3 / DESGN. / LAST PUNCHING DT / PRESENT AGEING
    */

    DECLARE @ToDate DATE = ISNULL(@AsOfDate, CAST(GETDATE() AS DATE));

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
        DATEDIFF(DAY, lp.LastPunchDt, @ToDate)                    AS [PRESENT AGEING]
    FROM dbo.tblEmployee e WITH (NOLOCK)
    OUTER APPLY (
        SELECT MAX(d2) AS LastPunchDt FROM (VALUES
            ( (SELECT MAX(p.PunchDate) FROM dbo.tblEmployeeMultiPunches p WITH (NOLOCK) WHERE p.UserID = e.ECode) ),
            ( (SELECT MAX(CONVERT(date, ar.PunchTimeUtc)) FROM dbo.AttendanceRecord ar WITH (NOLOCK) WHERE ar.EmployeeId = e.EmployeeId AND ar.StatusId = 1) )
        ) v(d2)
    ) lp
    LEFT JOIN dbo.tblLocation l       WITH (NOLOCK) ON l.LocationId   = e.LocationId
    LEFT JOIN dbo.tblDepartment d     WITH (NOLOCK) ON d.DepartmentId = e.DepartmentId
    LEFT JOIN dbo.tblDesignation dg   WITH (NOLOCK) ON dg.DesignationId = e.DesignationId
    LEFT JOIN dbo.tblSubDepartment sd1 WITH (NOLOCK) ON sd1.SubDepartmentId = e.SubDepartmentId1
    LEFT JOIN dbo.tblSubDepartment sd2 WITH (NOLOCK) ON sd2.SubDepartmentId = e.SubDepartmentId2
    LEFT JOIN dbo.tblSubDepartment sd3 WITH (NOLOCK) ON sd3.SubDepartmentId = e.SubDepartmentId3
    WHERE e.IsActive = 1
      AND lp.LastPunchDt IS NOT NULL                 -- must have punched at least once
      -- exclude store-login accounts (ECode is actually a store code, not a person)
      AND NOT EXISTS (SELECT 1 FROM dbo.tblLocation lx WITH (NOLOCK) WHERE lx.STCode = e.ECode)
      -- punch-gap absconding threshold: HO (RH01) >= 8 days, all other locations >= 7 days
      AND (
            (l.STCode = 'RH01'        AND DATEDIFF(DAY, lp.LastPunchDt, @ToDate) >= 8)
         OR (ISNULL(l.STCode,'') <> 'RH01' AND DATEDIFF(DAY, lp.LastPunchDt, @ToDate) >= 7)
      )
    ORDER BY l.STCode, e.ECode;

    SET NOCOUNT OFF;
END;

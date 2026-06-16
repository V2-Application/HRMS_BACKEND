CREATE OR ALTER PROCEDURE dbo.usp_SeparatedLastPunchMissingGapReport
    @AsOfDate DATE = NULL    -- kept for signature compatibility (not used)
AS
BEGIN
    SET NOCOUNT ON;

    /*
      SEPARATED BUT LAST PUNCH DT MISSING  (read-only)
      One row per separated employee (latest non-revoked separation) who has NO last punch date
      at all - neither a biometric punch (tblEmployeeMultiPunches) nor an approved geofence
      attendance (AttendanceRecord). i.e. their L.PUNCH DATE is missing.
        LOC CD / LOC NM / LOC TYPE (name-based; store ids NOT used) / STATS OLD/NEW (location Active|UPC)
        EMP CODE / EMP NM / DOJ / DEPT. / SUB.-DEPT. 1/2/3 / DESGN.
        EMP. STATUS (ACT/INACT)  = 'Separated' when tblEmployee.IsActive=0 else 'Active'
        SEPERATION DATE          = latest non-revoked separation (LastDay, else ResignationDate)
        L.PUNCH DATE             = always NULL here (that is the gap being reported)
      Store-login accounts (ECode = a store STCode) are excluded.
    */

    ;WITH sep AS (
        SELECT s.EmployeeId,
               COALESCE(s.LastDay, CAST(s.ResignationDate AS date)) AS SeparationDate,
               ROW_NUMBER() OVER (
                   PARTITION BY s.EmployeeId
                   ORDER BY COALESCE(s.LastDay, CAST(s.ResignationDate AS date)) DESC,
                            s.EmployeeSeprationId DESC) AS rn
        FROM dbo.tblEmployeeSepration s WITH (NOLOCK)
        WHERE ISNULL(s.IsRevoked, 0) = 0
    )
    SELECT
        l.STCode       AS [LOC CD],
        l.LocationName AS [LOC NM],
        CASE
            WHEN UPPER(LTRIM(RTRIM(l.LocationName))) LIKE 'HO-NEW%' THEN 'HO new'
            WHEN UPPER(LTRIM(RTRIM(l.LocationName))) LIKE 'HO-OLD%' THEN 'Old HO'
            WHEN UPPER(LTRIM(RTRIM(l.LocationName))) LIKE '%CENTRAL%' THEN 'Central'
            WHEN UPPER(LTRIM(RTRIM(l.LocationName))) LIKE '%DC%' THEN 'DC'
            WHEN UPPER(LTRIM(RTRIM(l.LocationName))) LIKE '%HUB%' THEN 'Hub'
            ELSE 'Store'
        END            AS [LOC TYPE],
        CASE WHEN l.IsActive = 1 THEN 'Active' ELSE 'UPC' END AS [STATS OLD/NEW],
        e.ECode        AS [EMP CODE],
        e.[FULL NAME]  AS [EMP NM],
        e.DOJ          AS [DOJ],
        d.DepartmentName      AS [DEPT.],
        sd1.SubDepartmentName AS [SUB.-DEPT. 1],
        sd2.SubDepartmentName AS [SUB.-DEPT. 2],
        sd3.SubDepartmentName AS [SUB.-DEPT. 3],
        dg.DesignationName    AS [DESGN.],
        CASE WHEN e.IsActive = 0 THEN 'Separated' ELSE 'Active' END AS [EMP. STATUS ( ACT/INACT)],
        sp.SeparationDate     AS [SEPERATION DATE],
        lp.LastPunchDt        AS [L.PUNCH DATE]
    FROM sep sp
    JOIN dbo.tblEmployee e WITH (NOLOCK) ON e.EmployeeId = sp.EmployeeId
    OUTER APPLY (
        SELECT MAX(d2) AS LastPunchDt FROM (VALUES
            ( (SELECT MAX(p.PunchDate) FROM dbo.tblEmployeeMultiPunches p WITH (NOLOCK) WHERE p.UserID = e.ECode) ),
            ( (SELECT MAX(CONVERT(date, ar.PunchTimeUtc)) FROM dbo.AttendanceRecord ar WITH (NOLOCK) WHERE ar.EmployeeId = e.EmployeeId AND ar.StatusId = 1) )
        ) v(d2)
    ) lp
    LEFT JOIN dbo.tblLocation l        WITH (NOLOCK) ON l.LocationId     = e.LocationId
    LEFT JOIN dbo.tblDepartment d      WITH (NOLOCK) ON d.DepartmentId   = e.DepartmentId
    LEFT JOIN dbo.tblDesignation dg    WITH (NOLOCK) ON dg.DesignationId = e.DesignationId
    LEFT JOIN dbo.tblSubDepartment sd1 WITH (NOLOCK) ON sd1.SubDepartmentId = e.SubDepartmentId1
    LEFT JOIN dbo.tblSubDepartment sd2 WITH (NOLOCK) ON sd2.SubDepartmentId = e.SubDepartmentId2
    LEFT JOIN dbo.tblSubDepartment sd3 WITH (NOLOCK) ON sd3.SubDepartmentId = e.SubDepartmentId3
    WHERE sp.rn = 1
      AND lp.LastPunchDt IS NULL          -- last punch date missing (the gap)
      -- exclude store-login accounts (ECode is actually a store code, not a person)
      AND NOT EXISTS (SELECT 1 FROM dbo.tblLocation lx WITH (NOLOCK) WHERE lx.STCode = e.ECode)
    ORDER BY sp.SeparationDate DESC, l.STCode, e.ECode;

    SET NOCOUNT OFF;
END;

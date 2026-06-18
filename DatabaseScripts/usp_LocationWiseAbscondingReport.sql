CREATE OR ALTER PROCEDURE dbo.usp_LocationWiseAbscondingReport
    @AsOfDate  DATE = NULL,    -- ageing computed against this date; defaults to today
    @BatchSize INT  = 8000     -- kept for signature compatibility (not used)
AS
BEGIN
    SET NOCOUNT ON;

    /*
      LOC-WISE ABSCONDING REPORT  (read-only)
      One row per location:
        LOC CODE = tblLocation.STCode
        LOC NM   = tblLocation.LocationName
        LOC TYPE = name-based (HO new / Old HO / Central / DC(DW01,DH24) / Hub / Store)
        ACT EMP  = ALL active employees (tblEmployee.IsActive=1) mapped to that location
                   (store-login accounts excluded)
        TD       = absconders DETECTED BY PUNCH GAP (same rule as the employee-wise report):
                   active employee, has punched at least once, and days since last punch reach
                   the threshold:  HO (STCode='RH01') >= 8 days ; all other locations >= 7 days.
      Sum of TD reconciles exactly with the employee-wise report row count.
      NOTE: depends on CURRENT punch data; on stale-snapshot environments (dev) TD will be inflated.
      Returns ALL locations.
    */

    DECLARE @ToDate DATE = ISNULL(@AsOfDate, CAST(GETDATE() AS DATE));

    SELECT
        l.STCode       AS [LOC CODE],
        l.LocationName AS [LOC NM],
        CASE
            WHEN l.STCode = 'RH01' OR UPPER(LTRIM(RTRIM(l.LocationName))) LIKE 'HO-NEW%' THEN 'HO new'
            WHEN l.STCode = 'RD04' OR UPPER(LTRIM(RTRIM(l.LocationName))) LIKE 'HO-OLD%' THEN 'Old HO'
            WHEN l.STCode = 'RH02' OR UPPER(LTRIM(RTRIM(l.LocationName))) LIKE '%CENTRAL%' THEN 'Central'
            WHEN l.STCode IN ('DW01','DH24') THEN 'DC'
            WHEN UPPER(LTRIM(RTRIM(l.LocationName))) LIKE '%DC%' OR UPPER(LTRIM(RTRIM(l.LocationName))) LIKE '%HUB%' THEN 'Hub'
            ELSE 'Store'
        END            AS [LOC TYPE],
        ISNULL(ae.ActEmp, 0) AS [ACT EMP],
        ISNULL(ab.TD, 0)     AS [TD]
    FROM dbo.tblLocation l WITH (NOLOCK)
    LEFT JOIN (
        SELECT e.LocationId, COUNT(*) AS ActEmp
        FROM dbo.tblEmployee e WITH (NOLOCK)
        WHERE e.IsActive = 1
          -- exclude store-login accounts (ECode is actually a store code, not a person)
          AND NOT EXISTS (SELECT 1 FROM dbo.tblLocation lx WITH (NOLOCK) WHERE lx.STCode = e.ECode)
        GROUP BY e.LocationId
    ) ae ON ae.LocationId = l.LocationId
    LEFT JOIN (
        SELECT e.LocationId, COUNT(*) AS TD
        FROM dbo.tblEmployee e WITH (NOLOCK)
        LEFT JOIN dbo.tblLocation l2 WITH (NOLOCK) ON l2.LocationId = e.LocationId
        OUTER APPLY (
            SELECT MAX(d2) AS LastPunchDt FROM (VALUES
                ( (SELECT MAX(p.PunchDate) FROM dbo.tblEmployeeMultiPunches p WITH (NOLOCK) WHERE p.UserID = e.ECode) ),
                ( (SELECT MAX(CONVERT(date, ar.PunchTimeUtc)) FROM dbo.AttendanceRecord ar WITH (NOLOCK) WHERE ar.EmployeeId = e.EmployeeId AND ar.StatusId = 1) )
            ) v(d2)
        ) lp
        WHERE e.IsActive = 1
          AND lp.LastPunchDt IS NOT NULL
          -- exclude store-login accounts (ECode is actually a store code, not a person)
          AND NOT EXISTS (SELECT 1 FROM dbo.tblLocation lx WITH (NOLOCK) WHERE lx.STCode = e.ECode)
          -- punch-gap absconding threshold: HO (RH01) >= 8 days, all other locations >= 7 days
          AND (
                (l2.STCode = 'RH01'        AND DATEDIFF(DAY, lp.LastPunchDt, @ToDate) >= 8)
             OR (ISNULL(l2.STCode,'') <> 'RH01' AND DATEDIFF(DAY, lp.LastPunchDt, @ToDate) >= 7)
          )
        GROUP BY e.LocationId
    ) ab ON ab.LocationId = l.LocationId
    ORDER BY l.STCode;

    SET NOCOUNT OFF;
END;

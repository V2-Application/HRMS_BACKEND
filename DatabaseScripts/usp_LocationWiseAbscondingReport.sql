CREATE OR ALTER PROCEDURE dbo.usp_LocationWiseAbscondingReport
    @AsOfDate  DATE = NULL,    -- kept for signature compatibility (not used)
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
        TD       = absconders MARKED at that location -- same definition as the employee-wise
                   report: tblEmployeeSepration.ResignationTypeId=10 (Absconding), not revoked,
                   and the employee is STILL ACTIVE (tblEmployee.IsActive=1).
      NOTE: TD counts only ACTIVE employees flagged absconding (IsActive=1) -- deactivated absconders
      are excluded per requirement. The Location report and the Employee-wise report reconcile exactly
      (Sum of TD = emp-report rows). Returns ALL locations.
    */

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
        SELECT LocationId, COUNT(*) AS ActEmp
        FROM dbo.tblEmployee WITH (NOLOCK)
        WHERE IsActive = 1
        GROUP BY LocationId
    ) ae ON ae.LocationId = l.LocationId
    LEFT JOIN (
        SELECT e.LocationId, COUNT(*) AS TD
        FROM dbo.tblEmployee e WITH (NOLOCK)
        WHERE e.IsActive = 1
          AND EXISTS (
              SELECT 1 FROM dbo.tblEmployeeSepration s WITH (NOLOCK)
              WHERE s.EmployeeId = e.EmployeeId AND s.ResignationTypeId = 10 AND ISNULL(s.IsRevoked,0) = 0
          )
        GROUP BY e.LocationId
    ) ab ON ab.LocationId = l.LocationId
    ORDER BY l.STCode;

    SET NOCOUNT OFF;
END;

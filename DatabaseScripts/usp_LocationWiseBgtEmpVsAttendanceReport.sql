CREATE OR ALTER PROCEDURE dbo.usp_LocationWiseBgtEmpVsAttendanceReport
    @AsOfDate DATE = NULL    -- kept for signature compatibility (not used)
AS
BEGIN
    SET NOCOUNT ON;

    /*
      LOC._BGT EMP. VS. ACT EMP. GAP REPORT  (read-only)
      One row per location:
        LOC CD     = tblLocation.STCode
        LOC NM     = tblLocation.LocationName
        LOC TYPE   = name-based (HO new / Old HO / Central / DC(DW01,DH24) / Hub / Store)
        LOC STATUS = 'Active' if tblLocation.IsActive=1 else 'UPC'
        BGT EMP.   = budgeted head-count = count of ACTIVE budgeted seats (dbo.BGTSEATMaster,
                     ACTIVE=1) whose LOC_CODE = the location's STCode
        ACT EMP.   = ACTUAL active employees currently working at the location
                     (tblEmployee.IsActive = 1 mapped to that location)
        DIFF.      = BGT EMP. - ACT EMP.   (positive => fewer working than budget; negative => more)
        RCA / ATR / HR REMARKS -> left blank (manual follow-up columns, per the template)
      Returns ALL locations. Marks/changes nothing.
    */

    ;WITH bgt AS (
        SELECT LOC_CODE, COUNT(*) AS BgtEmp
        FROM dbo.BGTSEATMaster WITH (NOLOCK)
        WHERE ISNULL(ACTIVE, 1) = 1 AND LOC_CODE IS NOT NULL
        GROUP BY LOC_CODE
    ),
    act AS (
        SELECT LocationId, COUNT(*) AS ActEmp
        FROM dbo.tblEmployee WITH (NOLOCK)
        WHERE IsActive = 1
        GROUP BY LocationId
    )
    SELECT
        l.STCode       AS [LOC CD],
        l.LocationName AS [LOC NM],
        CASE
            WHEN l.STCode = 'RH01' OR UPPER(LTRIM(RTRIM(l.LocationName))) LIKE 'HO-NEW%' THEN 'HO new'
            WHEN l.STCode = 'RD04' OR UPPER(LTRIM(RTRIM(l.LocationName))) LIKE 'HO-OLD%' THEN 'Old HO'
            WHEN l.STCode = 'RH02' OR UPPER(LTRIM(RTRIM(l.LocationName))) LIKE '%CENTRAL%' THEN 'Central'
            WHEN l.STCode IN ('DW01','DH24') THEN 'DC'
            WHEN UPPER(LTRIM(RTRIM(l.LocationName))) LIKE '%DC%' OR UPPER(LTRIM(RTRIM(l.LocationName))) LIKE '%HUB%' THEN 'Hub'
            ELSE 'Store'
        END            AS [LOC TYPE],
        CASE WHEN l.IsActive = 1 THEN 'Active' ELSE 'UPC' END AS [LOC STATUS],
        ISNULL(b.BgtEmp, 0)  AS [BGT EMP.],
        ISNULL(a.ActEmp, 0)  AS [ACT EMP.],
        ISNULL(b.BgtEmp, 0) - ISNULL(a.ActEmp, 0) AS [DIFF.],
        CAST(NULL AS varchar(200)) AS [RCA],
        CAST(NULL AS varchar(200)) AS [ATR],
        CAST(NULL AS varchar(200)) AS [HR REMARKS]
    FROM dbo.tblLocation l WITH (NOLOCK)
    LEFT JOIN bgt b ON b.LOC_CODE   = l.STCode
    LEFT JOIN act a ON a.LocationId = l.LocationId
    ORDER BY l.STCode;

    SET NOCOUNT OFF;
END;

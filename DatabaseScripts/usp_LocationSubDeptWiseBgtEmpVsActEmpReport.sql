CREATE OR ALTER PROCEDURE dbo.usp_LocationSubDeptWiseBgtEmpVsActEmpReport
    @AsOfDate DATE = NULL    -- kept for signature compatibility (not used)
AS
BEGIN
    SET NOCOUNT ON;

    /*
      LOC._SUB DEPT._BGT EMP. VS. ACT EMP. GAP REPORT  (read-only)
      One row per LOCATION + DEPARTMENT + SUB-DEPARTMENT (primary / level 1):
        LOC CD     = tblLocation.STCode
        LOC NM     = tblLocation.LocationName
        LOC TYPE   = name-based (HO new / Old HO / Central / DC(DW01,DH24) / Hub / Store)
        LOC STATUS = 'Active' if tblLocation.IsActive=1 else 'Inactive'
        DEPT.      = department name (tblDepartment.DepartmentName); BGTSEATMaster.DEPT_SNO = DepartmentId
        SUB DEPT.  = primary sub-department name; budget uses BGTSEATMaster.SubDepartment1 (a name),
                     employees use tblEmployee.SubDepartmentId1 -> tblSubDepartment.SubDepartmentName.
                     Matched by name (all budget SubDepartment1 names exist in tblSubDepartment).
        BGT EMP.   = budgeted seats (dbo.BGTSEATMaster, ACTIVE=1) for that LOC_CODE + DEPT + SUB-DEPT
        ACT EMP.   = ACTUAL active employees (IsActive=1) at that location + department + sub-department
        DIFF.      = BGT EMP. - ACT EMP.
        RCA / ATR / HR REMARKS -> left blank (manual follow-up columns, per the template)
      Rows are every (location, dept, sub-dept) combo with a budgeted seat OR an active employee.
      Blank/unmapped sub-department is grouped under an empty SUB DEPT.  Marks/changes nothing.
    */

    -- one location row per STCode (dedup defensively)
    IF OBJECT_ID('tempdb..#Loc') IS NOT NULL DROP TABLE #Loc;
    SELECT STCode, LocationName, IsActive
    INTO #Loc
    FROM (
        SELECT l.STCode, l.LocationName, l.IsActive,
               ROW_NUMBER() OVER (PARTITION BY l.STCode ORDER BY l.IsActive DESC, l.LocationId) AS rn
        FROM dbo.tblLocation l WITH (NOLOCK)
        WHERE l.STCode IS NOT NULL
    ) z WHERE rn = 1;
    CREATE UNIQUE CLUSTERED INDEX IX_Loc_STCode ON #Loc(STCode);

    -- budgeted seats per location + department + sub-department(1)
    IF OBJECT_ID('tempdb..#Bgt') IS NOT NULL DROP TABLE #Bgt;
    SELECT b.LOC_CODE AS STCode,
           TRY_CAST(b.DEPT_SNO AS INT) AS DeptId,
           UPPER(ISNULL(LTRIM(RTRIM(b.SubDepartment1)),'')) AS SubDeptKey,
           MAX(LTRIM(RTRIM(b.SubDepartment1))) AS SubDeptName,
           COUNT(*) AS BgtEmp
    INTO #Bgt
    FROM dbo.BGTSEATMaster b WITH (NOLOCK)
    WHERE ISNULL(b.ACTIVE,1) = 1 AND b.LOC_CODE IS NOT NULL AND TRY_CAST(b.DEPT_SNO AS INT) IS NOT NULL
    GROUP BY b.LOC_CODE, TRY_CAST(b.DEPT_SNO AS INT), UPPER(ISNULL(LTRIM(RTRIM(b.SubDepartment1)),''));
    CREATE UNIQUE CLUSTERED INDEX IX_Bgt ON #Bgt(STCode, DeptId, SubDeptKey);

    -- actual active employees per location + department + sub-department(1)
    IF OBJECT_ID('tempdb..#Act') IS NOT NULL DROP TABLE #Act;
    SELECT l.STCode AS STCode,
           e.DepartmentId AS DeptId,
           UPPER(ISNULL(LTRIM(RTRIM(s.SubDepartmentName)),'')) AS SubDeptKey,
           MAX(s.SubDepartmentName) AS SubDeptName,
           COUNT(*) AS ActEmp
    INTO #Act
    FROM dbo.tblEmployee e WITH (NOLOCK)
    JOIN dbo.tblLocation l WITH (NOLOCK) ON l.LocationId = e.LocationId
    LEFT JOIN dbo.tblSubDepartment s WITH (NOLOCK) ON s.SubDepartmentId = e.SubDepartmentId1
    WHERE e.IsActive = 1 AND e.DepartmentId IS NOT NULL AND l.STCode IS NOT NULL
    GROUP BY l.STCode, e.DepartmentId, UPPER(ISNULL(LTRIM(RTRIM(s.SubDepartmentName)),''));
    CREATE UNIQUE CLUSTERED INDEX IX_Act ON #Act(STCode, DeptId, SubDeptKey);

    ;WITH keys AS (
        SELECT STCode, DeptId, SubDeptKey FROM #Bgt
        UNION
        SELECT STCode, DeptId, SubDeptKey FROM #Act
    )
    SELECT
        k.STCode       AS [LOC CD],
        l.LocationName AS [LOC NM],
        CASE
            WHEN k.STCode = 'RH01' OR UPPER(LTRIM(RTRIM(l.LocationName))) LIKE 'HO-NEW%' THEN 'HO new'
            WHEN k.STCode = 'RD04' OR UPPER(LTRIM(RTRIM(l.LocationName))) LIKE 'HO-OLD%' THEN 'Old HO'
            WHEN k.STCode = 'RH02' OR UPPER(LTRIM(RTRIM(l.LocationName))) LIKE '%CENTRAL%' THEN 'Central'
            WHEN k.STCode IN ('DW01','DH24') THEN 'DC'
            WHEN UPPER(LTRIM(RTRIM(l.LocationName))) LIKE '%DC%' OR UPPER(LTRIM(RTRIM(l.LocationName))) LIKE '%HUB%' THEN 'Hub'
            ELSE 'Store'
        END            AS [LOC TYPE],
        CASE WHEN l.IsActive = 1 THEN 'Active' ELSE 'Inactive' END AS [LOC STATUS],
        d.DepartmentName AS [DEPT.],
        ISNULL(b.SubDeptName, a.SubDeptName) AS [SUB DEPT.],
        ISNULL(b.BgtEmp, 0)  AS [BGT EMP.],
        ISNULL(a.ActEmp, 0)  AS [ACT EMP.],
        ISNULL(b.BgtEmp, 0) - ISNULL(a.ActEmp, 0) AS [DIFF.],
        CAST(NULL AS varchar(200)) AS [RCA],
        CAST(NULL AS varchar(200)) AS [ATR],
        CAST(NULL AS varchar(200)) AS [HR REMARKS]
    FROM keys k
    LEFT JOIN #Bgt b ON b.STCode = k.STCode AND b.DeptId = k.DeptId AND b.SubDeptKey = k.SubDeptKey
    LEFT JOIN #Act a ON a.STCode = k.STCode AND a.DeptId = k.DeptId AND a.SubDeptKey = k.SubDeptKey
    LEFT JOIN #Loc l ON l.STCode = k.STCode
    LEFT JOIN dbo.tblDepartment d WITH (NOLOCK) ON d.DepartmentId = k.DeptId
    ORDER BY k.STCode, d.DepartmentName, [SUB DEPT.];

    SET NOCOUNT OFF;
END;

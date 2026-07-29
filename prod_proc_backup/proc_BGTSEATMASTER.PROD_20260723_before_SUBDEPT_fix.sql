-- LIVE PROD definition of dbo.proc_BGTSEATMASTER captured 2026-07-23 BEFORE the
-- sub-department source fix (had the EXCESS-ACTIVE fix already). Restore verbatim
-- (as CREATE OR ALTER) to revert.
CREATE OR ALTER PROCEDURE [dbo].[proc_BGTSEATMASTER]
as
WITH Emp AS (
    SELECT a.Ecode, a.[FULL NAME] AS FullName, a.DepartmentId, a.DesignationId, UPPER(b.STCode) AS STCode,
        ROW_NUMBER() OVER( PARTITION BY UPPER(b.STCode), a.DepartmentId, a.DesignationId ORDER BY a.Ecode) AS rnEmp,
        c.Ecode as ReportEcode, c.[FULL NAME] AS ReportFullName, d.DesignationName as ReportDesig,
        a.[GROSS SALARY] as ActualSalary, b.IsActive as [StoreStatus], b.LocationName,
        CASE WHEN b.OpeningDate IS NULL THEN NULL
             WHEN TRY_CONVERT(date, b.OpeningDate) IS NOT NULL THEN UPPER(REPLACE(CONVERT(varchar(11), TRY_CONVERT(date, b.OpeningDate), 106),' ','-'))
             ELSE b.OpeningDate END AS OpeningDate
    FROM dbo.tblEmployee a WITH (NOLOCK)
    LEFT JOIN dbo.tblLocation b WITH (NOLOCK) ON a.LocationId = b.LocationId
    Left JOIN tblEmployee c (NOLOCK) on c.Ecode=a.ReportHeadEcode
    Left join tblDesignation d (NOLOCK) on c.DesignationId=d.DesignationId
    WHERE a.IsActive = 1 and b.IsDeleted=0 and a.DepartmentId is not null and a.DesignationId is not null and STCode is not null
),
Seats AS (
    SELECT s.Id, UPPER(s.LOC_CODE) AS LOC_CODE, s.DEPT_SNO, s.DESG_SNO, s.SEAT_MASTER_NO, s.SALARY_BGT, s.ACTIVE,
        b.DesignationName as ReportingDesig,
        s.SubDepartment1, s.SubDepartment2, s.SubDepartment3,
        TRY_CONVERT(int, PARSENAME(REPLACE(s.SEAT_MASTER_NO, '-', '.'), 1)) AS Series,
        ROW_NUMBER() OVER( PARTITION BY UPPER(s.LOC_CODE), s.DEPT_SNO, s.DESG_SNO
            ORDER BY CASE WHEN TRY_CONVERT(int, PARSENAME(REPLACE(s.SEAT_MASTER_NO, '-', '.'), 1)) IS NULL THEN 1 ELSE 0 END,
                TRY_CONVERT(int, PARSENAME(REPLACE(s.SEAT_MASTER_NO, '-', '.'), 1)) ASC, s.Id ASC) AS rnSeat,
        c.IsActive as [StoreStatus], c.LocationName,
        CASE WHEN c.OpeningDate IS NULL THEN NULL
             WHEN TRY_CONVERT(date, c.OpeningDate) IS NOT NULL THEN UPPER(REPLACE(CONVERT(varchar(11), TRY_CONVERT(date, c.OpeningDate), 106),' ','-'))
             ELSE c.OpeningDate END AS OpeningDate
    FROM HRMS.dbo.BGTSEATMASTER s WITH (NOLOCK)
    Left join tblDesignation b on s.REPORTING_MANAGER=b.DesignationId
    Left Join tblLocation c on s.LOC_CODE=c.STCode
    WHERE ISNULL(s.ACTIVE, 1) = 1
),
Matched AS (
    SELECT COALESCE(e.STCode, s.LOC_CODE) AS STCode, COALESCE(e.DepartmentId, s.DEPT_SNO) AS DepartmentId,
        COALESCE(e.DesignationId, s.DESG_SNO) AS DesignationId, e.Ecode, e.FullName, e.ReportEcode, e.ReportFullName,
        e.ReportDesig, e.ActualSalary, s.SEAT_MASTER_NO, s.SALARY_BGT, s.ReportingDesig,
        COALESCE(s.ACTIVE, CASE WHEN e.Ecode IS NOT NULL THEN CAST(1 AS bit) END) AS ACTIVE,
        s.SubDepartment1, s.SubDepartment2, s.SubDepartment3,
        COALESCE(e.StoreStatus,s.StoreStatus) as StoreStatus, COALESCE(e.LocationName,s.LocationName) as LocationName,
        COALESCE(e.OpeningDate,s.OpeningDate) as OpeningDate,
        CASE WHEN s.SEAT_MASTER_NO IS NOT NULL THEN s.SEAT_MASTER_NO
             WHEN e.Ecode IS NOT NULL AND s.SEAT_MASTER_NO IS NULL THEN 'EXCESS'
             WHEN e.Ecode IS NULL AND s.SEAT_MASTER_NO IS NOT NULL THEN 'VACANT' END AS DisplaySeat
    FROM Emp e
    FULL OUTER JOIN Seats s ON e.STCode = s.LOC_CODE AND e.DepartmentId = s.DEPT_SNO AND e.DesignationId= s.DESG_SNO AND e.rnEmp = s.rnSeat
)
SELECT STCode, a.DepartmentId, b.DepartmentName, a.DesignationId, c.DesignationName,
    DisplaySeat AS SeatOrStatus, SALARY_BGT, ActualSalary, ISNULL(Ecode,'Vacant') as Ecode, FullName,
    ReportEcode, ReportFullName, ReportingDesig as BGTReportingDesig, ReportDesig as ActualReportingDesig,
    ACTIVE, StoreStatus, LocationName, OpeningDate,
    SubDepartment1, SubDepartment2, SubDepartment3
FROM Matched a
Left join tblDepartment b on a.DepartmentId=b.DepartmentId
Left join tblDesignation c on a.DesignationId=c.DesignationId
ORDER BY STCode, DepartmentId, DesignationId, SeatOrStatus, Ecode

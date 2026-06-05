--USE [HRMS]
--GO
--/****** Object:  StoredProcedure [dbo].[proc_BGTSEATMASTER]    Script Date: 20-09-2025 15:14:10 ******/
--SET ANSI_NULLS ON
--GO
--SET QUOTED_IDENTIFIER ON
--GO

--[proc_BGTSEATMASTER]

CREATE Procedure [dbo].[proc_BGTSEATMASTER]
as
WITH Emp AS (
    SELECT
        a.Ecode,
        a.[FULL NAME]       AS FullName,
        a.DepartmentId,
        a.DesignationId,
        UPPER(b.STCode)   AS STCode,
        -- Order employees within each (Loc, Dept, Desig) to assign seats deterministically.
        ROW_NUMBER() OVER(
            PARTITION BY UPPER(b.STCode), a.DepartmentId, a.DesignationId
            ORDER BY a.Ecode
        ) AS rnEmp,
		c.Ecode as ReportEcode,
		c.[FULL NAME]       AS ReportFullName,
		d.DesignationName as ReportDesig,
		a.[GROSS SALARY] as ActualSalary,
		b.IsActive as [StoreStatus],
		b.LocationName,
		CASE
    WHEN b.OpeningDate IS NULL THEN NULL                        -- keep NULL
    WHEN TRY_CONVERT(date, b.OpeningDate) IS NOT NULL THEN      -- true datetime/string
         UPPER(REPLACE(
             CONVERT(varchar(11), TRY_CONVERT(date, b.OpeningDate), 106), -- 21 Aug 2025
             ' ', '-'))                                          -- 21-Aug-2025 -> UPPER -> 21-AUG-2025
    ELSE b.OpeningDate                                           -- e.g., 'OCT-25-UPC' stays as-is
  END AS OpeningDate
    FROM dbo.tblEmployee a WITH (NOLOCK)
    LEFT JOIN dbo.tblLocation b WITH (NOLOCK) ON a.LocationId = b.LocationId
	Left JOIN tblEmployee c (NOLOCK) on c.Ecode=a.ReportHeadEcode
	Left join tblDesignation d (NOLOCK) on c.DesignationId=d.DesignationId
     WHERE a.IsActive = 1 and b.IsDeleted=0   -- uncomment if you have an active flag
	 and a.DepartmentId is not null and a.DesignationId is not null and STCode is not null
),
Seats AS (
    SELECT
        s.Id,
        UPPER(s.LOC_CODE) AS LOC_CODE,
        s.DEPT_SNO,
        s.DESG_SNO,
        s.SEAT_MASTER_NO,
		s.SALARY_BGT,
		s.ACTIVE,
		b.DesignationName as ReportingDesig,

        TRY_CONVERT(int, PARSENAME(REPLACE(s.SEAT_MASTER_NO, '-', '.'), 1)) AS Series,
        ROW_NUMBER() OVER(
            PARTITION BY UPPER(s.LOC_CODE), s.DEPT_SNO, s.DESG_SNO
            ORDER BY 
                CASE WHEN TRY_CONVERT(int, PARSENAME(REPLACE(s.SEAT_MASTER_NO, '-', '.'), 1)) IS NULL THEN 1 ELSE 0 END,
                TRY_CONVERT(int, PARSENAME(REPLACE(s.SEAT_MASTER_NO, '-', '.'), 1)) ASC,
                s.Id ASC
        ) AS rnSeat
	,c.IsActive as [StoreStatus]
	,c.LocationName
	,CASE
    WHEN c.OpeningDate IS NULL THEN NULL                        -- keep NULL
    WHEN TRY_CONVERT(date, c.OpeningDate) IS NOT NULL THEN      -- true datetime/string
         UPPER(REPLACE(
             CONVERT(varchar(11), TRY_CONVERT(date, c.OpeningDate), 106), -- 21 Aug 2025
             ' ', '-'))                                          -- 21-Aug-2025 -> UPPER -> 21-AUG-2025
    ELSE c.OpeningDate                                           -- e.g., 'OCT-25-UPC' stays as-is
  END AS OpeningDate
    FROM HRMS.dbo.BGTSEATMASTER s WITH (NOLOCK)
	Left join tblDesignation b on s.REPORTING_MANAGER=b.DesignationId
	Left Join tblLocation c on s.LOC_CODE=c.STCode
    WHERE ISNULL(s.ACTIVE, 1) = 1
),
Matched AS (
    -- Pair employee N with seat N per (Loc,Dept,Desig) using a FULL JOIN on the row_number
    SELECT
        COALESCE(e.STCode,  s.LOC_CODE) AS STCode,
        COALESCE(e.DepartmentId, s.DEPT_SNO) AS DepartmentId,
        COALESCE(e.DesignationId, s.DESG_SNO) AS DesignationId,

        e.Ecode,
        e.FullName,
		e.ReportEcode,
		e.ReportFullName,
		e.ReportDesig,
		e.ActualSalary,
        s.SEAT_MASTER_NO,
		s.SALARY_BGT,
		s.ReportingDesig,
		s.ACTIVE,
		COALESCE(e.StoreStatus,s.StoreStatus) as StoreStatus,
		COALESCE(e.LocationName,s.LocationName) as LocationName,
		COALESCE(e.OpeningDate,s.OpeningDate) as OpeningDate,
        --CASE
        --    WHEN e.Ecode IS NOT NULL AND s.SEAT_MASTER_NO IS NOT NULL THEN 'ASSIGNED'
        --    WHEN e.Ecode IS NOT NULL AND s.SEAT_MASTER_NO IS NULL THEN 'EXCESS'   -- over-hiring
        --    WHEN e.Ecode IS NULL AND s.SEAT_MASTER_NO IS NOT NULL THEN 'VACANT'   -- seat without employee
        --END AS SeatStatus,

        -- Display column exactly as you asked: show 'EXCESS'/'VACANT' in place of seat no when not assigned
        CASE
            WHEN s.SEAT_MASTER_NO IS NOT NULL THEN s.SEAT_MASTER_NO
            WHEN e.Ecode IS NOT NULL AND s.SEAT_MASTER_NO IS NULL THEN 'EXCESS'
            WHEN e.Ecode IS NULL AND s.SEAT_MASTER_NO IS NOT NULL THEN 'VACANT'
        END AS DisplaySeat
    FROM Emp   e
    FULL OUTER JOIN Seats s
      ON e.STCode       = s.LOC_CODE
     AND e.DepartmentId = s.DEPT_SNO
     AND e.DesignationId= s.DESG_SNO
     AND e.rnEmp        = s.rnSeat
)
SELECT
    STCode,
    a.DepartmentId,
	b.DepartmentName,
    a.DesignationId,
	c.DesignationName,
    DisplaySeat AS SeatOrStatus,  -- shows seat no, or 'EXCESS'/'VACANT'
	SALARY_BGT,
	ActualSalary,
    ISNULL(Ecode,'Vacant') as Ecode,
    FullName,
	ReportEcode,
	ReportFullName,
	ReportingDesig as BGTReportingDesig,
	ReportDesig as ActualReportingDesig,
	ACTIVE,
	StoreStatus,
	LocationName,
	OpeningDate
FROM Matched a
Left join tblDepartment b on a.DepartmentId=b.DepartmentId
Left join tblDesignation c on a.DesignationId=c.DesignationId
--where DepartmentName='RETAIL OPERATION'
--where DisplaySeat <> 'EXCESS'
ORDER BY
    STCode,
    DepartmentId,
    DesignationId,
	SeatOrStatus,
	Ecode


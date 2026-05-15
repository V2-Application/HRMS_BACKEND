-- =============================================================================
-- Category: InactiveReports
-- Source:   dev DB (192.168.151.27\KARMA / HRMS)
-- Generated: 2026-05-14 12:15:06
-- Objects rewritten as CREATE OR ALTER for safe re-run on production.
-- =============================================================================
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- -----------------------------------------------------------------------------
-- dbo.sp_Report_InactiveEmployees_NoDuesNotSubmitted
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE sp_Report_InactiveEmployees_NoDuesNotSubmitted
as
begin
	set nocount on;

	select
		e.Ecode,
		e.[FULL NAME] as 'Full Name',
		e.GENDER,
		e.DOJ,
		e.MOBILE,
		e.[EMAIL ADDRESS] as 'Email',
		e.[FATHER'S NAME] as 'Father Name',
		e.ReportHeadEcode,
		rm.[FULL NAME] as 'Reporthead Name',
		sm.ShiftName,
		loc.LocationName,
		e.DateOfLeft
	from tblEmployee e
		left join tblDepartment dept with (nolock) on e.DepartmentId = dept.DepartmentId
		left join tblDesignation desg with (nolock) on e.DesignationId = desg.DesignationId
		left join tblLocation loc with (nolock) on e.LocationId = loc.LocationId
		left join tblEmployee rm on e.ReportHeadEcode = rm.Ecode
		left join tblShiftMaster sm on e.ShiftID = sm.ShiftID
		left join EmployeeResignationChecklistResponse res on e.EmployeeId = res.EmployeeId
	where
		res.EmployeeResignationChecklistMasterId = 5
		and res.Attachment is null
		and e.IsActive = 0
end
GO

-- -----------------------------------------------------------------------------
-- dbo.sp_ReportInactiveEmployeesWithFNF
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_ReportInactiveEmployeesWithFNF]
(
    @Months INT = 2
)
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH InactiveChange AS
    (
        SELECT
            h.EmployeeId,
            h.ValidFrom AS WentInactiveOn,
            LAG(h.IsActive) OVER
            (
                PARTITION BY h.EmployeeId
                ORDER BY h.ValidFrom
            ) AS PreviousStatus,
            h.IsActive
        FROM HRMS.dbo.tblEmployee_History h
        WHERE h.ValidFrom >= DATEADD(MONTH, -@Months, GETDATE())
    )

    SELECT
        e.Ecode,
        e.[FULL NAME] AS EmployeeName,
        ic.WentInactiveOn,
        
        CASE
            WHEN fp.Status = 'Paid'
                THEN 'UTR Exists'
            ELSE 'UTR Pending'
        END AS UTRStatus,
        fp.ChequeNo AS ChequeNumber,
        CASE
            WHEN rt.Attachment IS NOT NULL
                THEN CONCAT('https://v2parivar.v2retail.com:9987/', rt.Attachment)
            ELSE NULL
        END AS AttachmentLink

    FROM HRMS.dbo.tblEmployee e

    LEFT JOIN InactiveChange ic
        ON ic.EmployeeId = e.EmployeeId
        AND ic.PreviousStatus = 1
        AND ic.IsActive = 0

    LEFT JOIN HRMS.dbo.fnf_header fh
        ON fh.EmployeeId = e.EmployeeId

    LEFT JOIN HRMS.dbo.fnf_payment fp
        ON fp.FNFId = fh.FNFId

    OUTER APPLY
    (
        SELECT TOP 1 Attachment
        FROM HRMS.dbo.EmployeeResignationChecklistResponse r
        WHERE r.EmployeeId = e.EmployeeId
        AND r.Attachment IS NOT NULL
        ORDER BY r.EmployeeId
    ) rt

    WHERE e.IsActive = 0
    ORDER BY ic.WentInactiveOn DESC, e.Ecode;

END

--select * from fnf_payment
GO

-- -----------------------------------------------------------------------------
-- dbo.sp_ReportActiveInEmpMasterinActiveHRMS
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE sp_ReportActiveInEmpMasterinActiveHRMS  
as  
begin  
set nocount on;  
  
SELECT   
    e.EmployeeId,  
    e.Ecode,  
    e.[FULL NAME] AS EmployeeName,  
    e.GENDER,  
    e.DOJ,  
    e.MOBILE,  
    e.[EMAIL ADDRESS],  
  
    d.DepartmentName,  
    de.DesignationName,  
  
    e.ReportHeadEcode,  
    rh.[FULL NAME] AS ReportingHeadName,  
  
    s.ShiftName,  
    l.LocationName,  
    st.StateName,  
  
    eam.E_CODE AS HRMSEcode  
FROM tblEmployee e  
  
LEFT JOIN EmpAttendanceMaster eam  
    ON eam.E_CODE = e.Ecode  
  
LEFT JOIN tblEmployee rh  
    ON rh.Ecode = e.ReportHeadEcode  
  
LEFT JOIN tblDepartment d  
    ON d.DepartmentId = e.DepartmentId  
  
LEFT JOIN tblDesignation de  
    ON de.DesignationId = e.DesignationId  
  
LEFT JOIN tblShiftMaster s  
    ON s.ShiftID = e.ShiftID  
  
LEFT JOIN tblLocation l  
    ON l.LocationId = e.LocationId  
  
LEFT JOIN tblState st  
    ON st.StateId = l.StateId  
  
WHERE   
    e.IsActive = 1  
    AND e.IsDeleted = 0  
    AND eam.E_CODE IS NULL    
ORDER BY e.Ecode  
end  
  
;
GO

-- -----------------------------------------------------------------------------
-- dbo.sp_ReportActiveInHRMSinActiveEmpMaster
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE sp_ReportActiveInHRMSinActiveEmpMaster  
as  
begin  
set nocount on;  
  
SELECT   
    e.EmployeeId,  
    e.Ecode,  
    e.[FULL NAME] AS EmployeeName,  
    e.GENDER,  
    e.DOJ,  
    e.MOBILE,  
    e.[EMAIL ADDRESS],  
  
    d.DepartmentName,  
    de.DesignationName,  
  
    e.ReportHeadEcode,  
    rh.[FULL NAME] AS ReportingHeadName,  
  
    s.ShiftName,  
    l.LocationName,  
    st.StateName,  
  
    eam.IsActive AS HRMSStatus  
FROM tblEmployee e  
  
INNER JOIN EmpAttendanceMaster eam  
    ON eam.E_CODE = e.Ecode  
  
LEFT JOIN tblEmployee rh  
    ON rh.Ecode = e.ReportHeadEcode  
  
LEFT JOIN tblDepartment d  
    ON d.DepartmentId = e.DepartmentId  
  
LEFT JOIN tblDesignation de  
    ON de.DesignationId = e.DesignationId  
  
LEFT JOIN tblShiftMaster s  
    ON s.ShiftID = e.ShiftID  
  
LEFT JOIN tblLocation l  
    ON l.LocationId = e.LocationId  
  
LEFT JOIN tblState st  
    ON st.StateId = l.StateId  
  
WHERE   
    e.IsActive = 1  
    AND e.IsDeleted = 0  
    AND eam.IsActive = 0  
ORDER BY e.Ecode  
  
end
GO

-- -----------------------------------------------------------------------------
-- dbo.sp_ReportNoResignationApprovalStillInactive
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE sp_ReportNoResignationApprovalStillInactive
as
begin
set nocount on;

select 
	 e.EmployeeId,
	 e.Ecode,
	 e.[FULL NAME] as EmployeeName,
	 desg.DesignationName,
	 s.ShiftName,
	 dept.DepartmentName,
	 st.StateName,
	 l.LocationName,
	 e.ReportHeadEcode,
	 rh.[Full Name] as ReportHeadName,
	 e.GENDER,
	 e.DOJ,
	 e.MOBILE,
	 e.[EMAIL ADDRESS]
	from tblEmployee e

	left join tblEmployee rh on
	e.EmployeeId = rh.EmployeeId

	left join tblShiftMaster s on
	s.ShiftID = e.ShiftID

	left join tblDesignation desg on
	desg.DesignationId = e.DesignationId
	
	left join tblDepartment dept on
	dept.DepartmentId = e.DepartmentId

	left join tblLocation l on
	l.LocationId = e.LocationId

	left join tblState st on
	st.StateId = l.StateId

	left join tblEmployeeSepration sp on
	sp.EmployeeId = e.EmployeeId

	where (sp.IsApprovedByManager = 0 or sp.IsApprovedByHR = 0) 
	and e.IsActive =0

	order by e.ecode
end
GO

-- -----------------------------------------------------------------------------
-- dbo.sp_ReportInactiveStillWorking
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_ReportInactiveStillWorking  
AS  
BEGIN  
    SET NOCOUNT ON;  

    BEGIN TRY  

        DECLARE @Today DATE = CAST(GETDATE() AS DATE);  
        DECLARE @Last3DaysStart DATE = DATEADD(DAY, -3, @Today);  

        SELECT   
            e.Ecode,
            e.[FULL NAME]        AS FullName,
            e.GENDER,
            e.DOB,
            e.DOJ,
            e.MOBILE,
            e.[EMAIL ADDRESS],
            d.DepartmentName,
            de.DesignationName,
            e.[FATHER'S NAME],
            e.ReportHeadEcode,
            rh.[FULL NAME]       AS ReportingHeadName,
            s.ShiftName,
            l.LocationName,
            stat.StateName,
            e.DateOfLeft,
            e.IsActive,
            p.LastValidPunchDate AS LastPunch

        FROM dbo.tblEmployee e  

        -- Get Last Valid Punch
        OUTER APPLY  
        (  
            SELECT MAX(x.AttendanceDate) AS LastValidPunchDate  
            FROM dbo.tbl_fn_GetMonthlyPunchesRange_productionnewnick_test x  
            WHERE x.ECode = e.Ecode  
              AND TRY_CAST(x.TotalWorkingMinutes AS TIME) >= '04:30'  
        ) p  

        -- Self Join for Reporting Head
        LEFT JOIN dbo.tblEmployee rh 
            ON rh.Ecode = e.ReportHeadEcode

        INNER JOIN dbo.tblDepartment d 
            ON e.DepartmentId = d.DepartmentId

        INNER JOIN dbo.tblDesignation de 
            ON de.DesignationId = e.DesignationId

        INNER JOIN dbo.tblShiftMaster s 
            ON s.ShiftID = e.ShiftID

        INNER JOIN dbo.tblLocation l 
            ON l.LocationId = e.LocationId

        INNER JOIN dbo.tblState stat 
            ON stat.StateId = l.StateId

        WHERE   
            e.IsActive = 0  
            AND p.LastValidPunchDate IS NOT NULL  
            AND p.LastValidPunchDate >= @Last3DaysStart  
            AND p.LastValidPunchDate <= @Today  

        ORDER BY p.LastValidPunchDate DESC;

    END TRY  
    BEGIN CATCH  
        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();  
        RAISERROR(@ErrorMessage, 16, 1);  
    END CATCH  
END
GO

-- -----------------------------------------------------------------------------
-- dbo.sp_GetInactiveEmployees_LastPunch_LastUpdate
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_GetInactiveEmployees_LastPunch_LastUpdate
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        e.EmployeeId,
        e.Ecode AS EmployeeCode,
        e.[FULL NAME] AS EmployeeName,

        e.[JOINING DATE] AS DateOfJoining,
        e.[DateOfLeft] AS DateOfLeaving,

        e.IsActive,
        ISNULL(e.IsFNFCompleted, 0) AS IsFNFCompleted,

        -- ✅ LAST PUNCH FROM ATTENDANCE
        lp.LastPunchDate,

        -- ✅ LAST UPDATED INFO FROM EMPLOYEE TABLE
        e.LastUpdatedBy,
        e.UpdatedOn AS LastUpdatedDate,

        -- OPTIONAL MASTER DATA
        ISNULL(d.DepartmentName, '') AS Department,
        ISNULL(g.DesignationName, '') AS Designation

    FROM dbo.tblEmployee e

    LEFT JOIN dbo.tblDepartment d 
        ON d.DepartmentId = e.DepartmentId

    LEFT JOIN dbo.tblDesignation g 
        ON g.DesignationId = e.DesignationId

    -- ✅ LAST PUNCH DATE PER EMPLOYEE (BY ECODE)
    LEFT JOIN (
        SELECT
            t.ECode,
            MAX(t.AttendanceDate) AS LastPunchDate
        FROM dbo.tbl_fn_GetMonthlyPunchesRange_productionnewnick_test t
        WHERE
              ISNULL(t.IsOnLeave,0) = 1
           OR ISNULL(t.IsRegularize,0) = 1
           OR ISNULL(t.ValidPunchCount,0) > 0
           OR TRY_CAST(NULLIF(LTRIM(RTRIM(ISNULL(t.TotalWorkingMinutes,''))), '') AS TIME) > '00:00:00'
           OR TRY_CAST(NULLIF(LTRIM(RTRIM(ISNULL(t.PunchIn,''))), '') AS TIME) > '00:00:00'
           OR TRY_CAST(NULLIF(LTRIM(RTRIM(ISNULL(t.PunchOut,''))), '') AS TIME) > '00:00:00'
        GROUP BY t.ECode
    ) lp 
        ON lp.ECode = e.Ecode

    WHERE
        ISNULL(e.IsStore, 0) <> 1
        AND ISNULL(e.IsActive, 0) = 0;   -- ✅ ONLY INACTIVE EMPLOYEES

END
GO

-- -----------------------------------------------------------------------------
-- dbo.sp_GetInactiveEmployeesWithLastPunch
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_GetInactiveEmployeesWithLastPunch
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @TwoMonthsAgo DATE = DATEADD(YEAR, -1, GETDATE());

    SELECT 
        e.EmployeeId,
        e.Ecode AS EmployeeCode,
        e.[FULL NAME] AS Name,
        ISNULL(d.DepartmentName, '') AS Department,
        ISNULL(g.DesignationName, '') AS Designation,
        e.[JOINING DATE] AS DateOfJoining,
        e.[DateOfLeft] AS DateOfLeaving,
        ISNULL(e.IsFNFCompleted, 0) AS IsFNFCompleted,

        0 AS UnpaidSalaryAmount,
        0 AS UnpaidSalaryDays,
        NULL AS UnpaidSalaryMonth,

        ISNULL(rt.ResignationTypeName, '') AS ResignationType,
        ts.ResignationDate,
        ts.LastDay AS SeparationLastDay,

        lp.LastPunchDate,   -- ✅ LAST PUNCH DATE

        ISNULL(ts.IsApprovedByManager, 0) AS ManagerApproved,
        ISNULL(ts.IsApprovedByHR, 0) AS HRApproved,
        r.Attachment AS ResignationAttachment

    FROM dbo.tblEmployee e
    LEFT JOIN dbo.tblEmployeeSepration ts 
        ON ts.EmployeeId = e.EmployeeId
    LEFT JOIN dbo.tblDepartment d 
        ON d.DepartmentId = e.DepartmentId
    LEFT JOIN dbo.tblDesignation g 
        ON g.DesignationId = e.DesignationId
    LEFT JOIN dbo.tblResignationType rt 
        ON rt.ResignationTypeId = ts.ResignationTypeId

    -- ✅ LAST PUNCH DATE BY ECODE
    LEFT JOIN (
        SELECT
            t.ECode,
            MAX(t.AttendanceDate) AS LastPunchDate
        FROM dbo.tbl_fn_GetMonthlyPunchesRange_productionnewnick_test t
        WHERE
              ISNULL(t.IsOnLeave,0) = 1
           OR ISNULL(t.IsRegularize,0) = 1
           OR ISNULL(t.ValidPunchCount,0) > 0
           OR TRY_CAST(NULLIF(LTRIM(RTRIM(ISNULL(t.TotalWorkingMinutes,''))), '') AS TIME) > '00:00:00'
           OR TRY_CAST(NULLIF(LTRIM(RTRIM(ISNULL(t.PunchIn,''))), '') AS TIME) > '00:00:00'
           OR TRY_CAST(NULLIF(LTRIM(RTRIM(ISNULL(t.PunchOut,''))), '') AS TIME) > '00:00:00'
        GROUP BY t.ECode
    ) lp 
        ON lp.ECode = e.Ecode

    LEFT JOIN (
        SELECT TOP 1 er.EmployeeId, er.Attachment
        FROM dbo.EmployeeResignationChecklistResponse er
        WHERE er.Attachment IS NOT NULL
        GROUP BY er.EmployeeId, er.Attachment
    ) r 
        ON r.EmployeeId = e.EmployeeId

    WHERE 
        ISNULL(e.IsStore, 0) <> 1 
        AND ISNULL(e.IsActive, 0) = 0
        AND e.[DateOfLeft] IS NOT NULL
        AND e.[DateOfLeft] < @TwoMonthsAgo;

END
GO


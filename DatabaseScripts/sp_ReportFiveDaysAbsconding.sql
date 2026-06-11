CREATE   PROCEDURE dbo.sp_ReportFiveDaysAbsconding  
AS  
BEGIN  
    SET NOCOUNT ON;  

    BEGIN TRY  

        DECLARE @Today DATE = CAST(GETDATE() AS DATE);  

        SELECT   
            e.EmployeeId,  
            e.Ecode,  
            e.[FULL NAME]               AS FullName,
            e.GENDER,
            e.DOJ,
            e.MOBILE,
            e.[EMAIL ADDRESS],
            d.DepartmentName,
            de.DesignationName,
            e.[FATHER'S NAME],
            e.ReportHeadEcode,
            rh.[FULL NAME]              AS ReportingHeadName,
            s.ShiftName,
            l.LocationName,
            stat.StateName,
            e.DateOfLeft,  

            DATEDIFF(DAY, CAST(e.DateOfLeft AS DATE), @Today) AS AbscondDays  

        FROM tblEmployee e  

        INNER JOIN tblEmployeeSepration sep   
            ON sep.EmployeeId = e.EmployeeId  

        INNER JOIN tblResignationType trt   
            ON trt.ResignationTypeId = sep.ResignationTypeId  

        -- Reporting Head
        LEFT JOIN tblEmployee rh
            ON rh.Ecode = e.ReportHeadEcode

        INNER JOIN tblDepartment d
            ON e.DepartmentId = d.DepartmentId

        INNER JOIN tblDesignation de
            ON de.DesignationId = e.DesignationId

        INNER JOIN tblShiftMaster s
            ON s.ShiftID = e.ShiftID

        INNER JOIN tblLocation l
            ON l.LocationId = e.LocationId

        INNER JOIN tblState stat
            ON stat.StateId = l.StateId

        WHERE   
            e.IsActive = 0  
            AND (trt.ResignationTypeId = 8 OR trt.ResignationTypeId = 10)  
            AND e.DateOfLeft IS NOT NULL  
            AND DATEDIFF(DAY, CAST(e.DateOfLeft AS DATE), @Today) > 5  

        ORDER BY AbscondDays DESC;  

    END TRY  
    BEGIN CATCH  

        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();  
        RAISERROR(@ErrorMessage, 16, 1);  

    END CATCH  
END

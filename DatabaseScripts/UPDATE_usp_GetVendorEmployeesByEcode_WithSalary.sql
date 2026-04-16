-- Updated stored procedure to include salary fields
CREATE OR ALTER PROCEDURE dbo.usp_GetVendorEmployeesByEcode            
(          
    @Ecode VARCHAR(20),           -- required          
    @ContractorCode NVARCHAR(200) -- required          
)          
AS            
BEGIN            
    SET NOCOUNT ON;            
          
    -- Validate input          
    IF @Ecode IS NULL OR @ContractorCode IS NULL          
    BEGIN          
        RAISERROR('Ecode and ContractorCode are required parameters.', 16, 1);          
        RETURN;          
    END          
          
    SELECT             
        e.EmployeeId,            
        e.Ecode,            
        e.[FULL NAME],            
        e.FirstName,            
        e.MiddleName,            
        e.LastName,            
        e.[EMAIL ADDRESS],            
        e.MOBILE,            
        e.[PRESENT ADDRESS],            
        e.[PRESENT ADDRESS PIN CODE],            
        e.DOB,            
        e.GENDER,            
        e.[UAN NO],            
        e.[PAN NO],            
        e.[AADHAR NO],            
        e.PFApplicable,            
        e.ESICApplicable,            
        e.ShiftID,   
        m.ShiftName,  
        e.ESICNO,      
        e.[FATHER'S NAME] as FatherName,      
        e.[Husband Name],            
        e.ContractorCode,            
        d.DepartmentId,            
        d.DepartmentName,            
        dg.DesignationId,            
        dg.DesignationName,            
        v.ContractorName,            
        l.LocationId,            
        l.LocationName,    
        e.IsActive,  
        e.DOJ ,  
        e.[From] as ContractStartDate,  
        e.[To] as ContractEndDate,
        -- Salary Fields
        e.BasicSalary,
        e.CCA,
        e.DA,
        e.ExtraAllowance,
        e.SpecialAllowance,
        e.HRA,
        e.[GROSS SALARY],
        e.monthlyGrossCTC,
        e.annuallyNetCTC
    FROM tblEmployee e            
    LEFT JOIN tblDepartment d             
        ON e.DepartmentId = d.DepartmentId            
    LEFT JOIN tblDesignation dg            
        ON e.DesignationId = dg.DesignationId            
    INNER JOIN tblVendorMaster v            
        ON e.ContractorCode = v.ContractorCode AND v.IsActive = 1            
    LEFT JOIN tblLocation l            
        ON e.LocationId = l.LocationId   
    LEFT JOIN tblShiftMaster m  
        ON e.ShiftID = m.ShiftID  
  
    WHERE            
        e.IsDeleted = 0  -- only active employees            
        AND e.Ecode = @Ecode            
        AND e.ContractorCode = @ContractorCode        
        AND e.CompanyId = 4        
    ORDER BY e.Ecode;            
END

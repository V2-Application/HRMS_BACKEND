CREATE OR ALTER PROCEDURE [dbo].[GetEmployeeDetails]    
    @PageNumber INT = 0,            
    @PageSize INT = 0,              
    @SearchTerm NVARCHAR(100) = '',  
    @Email NVARCHAR(150) = NULL,
    @DesignationName NVARCHAR(100) = NULL,
    @TotalEmployees INT OUTPUT,      
    @CurrentPageNumber INT OUTPUT    
AS    
BEGIN    
    SET NOCOUNT ON;    

    DECLARE @NormalizedSearch NVARCHAR(100) = ISNULL(@SearchTerm, '');
    DECLARE @NormalizedEmail NVARCHAR(150) = NULLIF(LTRIM(RTRIM(ISNULL(@Email, ''))), '');
    DECLARE @NormalizedDesignation NVARCHAR(100) = NULLIF(LTRIM(RTRIM(ISNULL(@DesignationName, ''))), '');
    
    SELECT @TotalEmployees = COUNT(*)    
    FROM tblEmployee e    
    LEFT JOIN tblDepartment d ON d.DepartmentId = e.DepartmentId    
    LEFT JOIN tblDesignation dg ON dg.DesignationId = e.DesignationId    
    LEFT JOIN tblLocation l ON l.LocationId = e.LocationId    
    WHERE 
        (
            @NormalizedSearch = ''     
            OR e.[FULL NAME] LIKE '%' + @NormalizedSearch + '%'    
            OR e.Ecode = @NormalizedSearch
            OR d.DepartmentName LIKE '%' + @NormalizedSearch + '%'    
            OR dg.DesignationName LIKE '%' + @NormalizedSearch + '%'    
            OR l.LocationName LIKE '%' + @NormalizedSearch + '%'    
            OR l.STCode LIKE '%' + @NormalizedSearch + '%'
            OR e.EMAIL_ADDRESS LIKE '%' + @NormalizedSearch + '%'
        )
        AND (@NormalizedEmail IS NULL OR e.EMAIL_ADDRESS LIKE '%' + @NormalizedEmail + '%')
        AND (@NormalizedDesignation IS NULL OR dg.DesignationName LIKE '%' + @NormalizedDesignation + '%');
    
    SET @CurrentPageNumber = @PageNumber;    
    
    IF @PageNumber > 0 AND @PageSize > 0    
    BEGIN    
        SELECT    
            e.EmployeeId,    
            e.[FULL NAME] AS FullName,    
            e.EMAIL_ADDRESS AS EmailAddress,
            d.DepartmentName,    
            dg.DesignationName,    
            l.LocationName,    
            l.STCode,    
            e.Ecode,    
            e.ReportHeadEcode,    
            e.IsActive,    
            e.IsDeleted,    
            ISNULL(CONVERT(VARCHAR(10), e.[JOINING DATE], 120), '') AS JoiningDate,    
            ISNULL(rh.[FULL NAME], '') AS ReportHeadName    
        FROM tblEmployee e    
        LEFT JOIN tblDepartment d ON d.DepartmentId = e.DepartmentId    
        LEFT JOIN tblDesignation dg ON dg.DesignationId = e.DesignationId    
        LEFT JOIN tblLocation l ON l.LocationId = e.LocationId    
        LEFT JOIN tblEmployee rh ON rh.Ecode = e.ReportHeadEcode    
        WHERE 
            (
                @NormalizedSearch = '' 
                OR (
                    ISNULL(e.[FULL NAME], '') + ' ' +  
                    ISNULL(e.Ecode, '') + ' ' +  
                    ISNULL(d.DepartmentName, '') + ' ' +  
                    ISNULL(dg.DesignationName, '') + ' ' +  
                    ISNULL(l.LocationName, '') + ' ' +  
                    ISNULL(l.STCode, '')  
                ) LIKE '%' + @NormalizedSearch + '%'
                OR e.EMAIL_ADDRESS LIKE '%' + @NormalizedSearch + '%'
            )
            AND (@NormalizedEmail IS NULL OR e.EMAIL_ADDRESS LIKE '%' + @NormalizedEmail + '%')
            AND (@NormalizedDesignation IS NULL OR dg.DesignationName LIKE '%' + @NormalizedDesignation + '%')
        ORDER BY e.EmployeeId DESC    
        OFFSET (@PageNumber - 1) * @PageSize ROWS    
        FETCH NEXT @PageSize ROWS ONLY;    
    END    
    ELSE    
    BEGIN    
        SELECT    
            e.EmployeeId,    
            e.[FULL NAME] AS FullName,    
            e.EMAIL_ADDRESS AS EmailAddress,
            d.DepartmentName,    
            dg.DesignationName,    
            l.LocationName,    
            l.STCode,    
            e.Ecode,    
            e.ReportHeadEcode,    
            e.IsActive,    
            e.IsDeleted,    
            ISNULL(CONVERT(VARCHAR(10), e.[JOINING DATE], 120), '') AS JoiningDate,    
            ISNULL(rh.[FULL NAME], '') AS ReportHeadName    
        FROM tblEmployee e    
        LEFT JOIN tblDepartment d ON d.DepartmentId = e.DepartmentId    
        LEFT JOIN tblDesignation dg ON dg.DesignationId = e.DesignationId    
        LEFT JOIN tblLocation l ON l.LocationId = e.LocationId    
        LEFT JOIN tblEmployee rh ON rh.Ecode = e.ReportHeadEcode    
        WHERE 
            (
                @NormalizedSearch = '' 
                OR (
                    ISNULL(e.[FULL NAME], '') + ' ' +  
                    ISNULL(e.Ecode, '') + ' ' +  
                    ISNULL(d.DepartmentName, '') + ' ' +  
                    ISNULL(dg.DesignationName, '') + ' ' +  
                    ISNULL(l.LocationName, '') + ' ' +  
                    ISNULL(l.STCode, '')  
                ) LIKE '%' + @NormalizedSearch + '%'
                OR e.EMAIL_ADDRESS LIKE '%' + @NormalizedSearch + '%'
            )
            AND (@NormalizedEmail IS NULL OR e.EMAIL_ADDRESS LIKE '%' + @NormalizedEmail + '%')
            AND (@NormalizedDesignation IS NULL OR dg.DesignationName LIKE '%' + @NormalizedDesignation + '%')
        ORDER BY e.EmployeeId DESC;    
    END    
END;


-- This "_back" variant is the proc actually wired to the live Employees List UI
-- (VendorController.GetVendorEmployeesByContractorCode -> GetVendorEmployeesListAsync1).
-- Adds the same SubDepartmentName1/2/3 columns as the primary list proc.

CREATE OR ALTER PROCEDURE dbo.usp_GetVendorEmployeesListByFilter_back
(
    @ContractorCode NVARCHAR(200),
    @SearchTerm NVARCHAR(100) = '',
    @IsActiveFilter INT = NULL,
    @ContractStartDate DATE = NULL,
    @ContractEndDate DATE = NULL,
    @PageNumber INT = 1,
    @PageSize INT = 10,
    @TotalRecords INT OUTPUT
)
AS
BEGIN
    SET NOCOUNT ON;

    IF @ContractorCode IS NULL OR LTRIM(RTRIM(@ContractorCode)) = ''
    BEGIN
        RAISERROR('ContractorCode is required.', 16, 1);
        RETURN;
    END

    DECLARE @FilteredEmployees TABLE
    (
        EmployeeId BIGINT,
        Ecode NVARCHAR(50),
        FullName NVARCHAR(100),
        DOJ DATE,
        IsActive BIT,
        DepartmentName NVARCHAR(100),
        SubDepartmentName1 NVARCHAR(200),
        SubDepartmentName2 NVARCHAR(200),
        SubDepartmentName3 NVARCHAR(200),
        DesignationName NVARCHAR(100),
        ContractorName NVARCHAR(200),
        ShiftName NVARCHAR(100),
        ContractStartDate DATE,
        ContractEndDate DATE,
        ContractorRatePerDay DECIMAL
    );

    INSERT INTO @FilteredEmployees
    SELECT
        e.EmployeeId,
        e.Ecode,
        e.[FULL NAME] AS FullName,
        e.DOJ,
        e.IsActive,
        d.DepartmentName,
        sd1.SubDepartmentName,
        sd2.SubDepartmentName,
        sd3.SubDepartmentName,
        dg.DesignationName,
        v.ContractorName,
        s.ShiftName,
        e.[From] as ContractStartDate,
        e.[To] as ContractEndDate,
        e.ContractorRatePerDay
    FROM tblEmployee e
    LEFT JOIN tblDepartment d ON e.DepartmentId = d.DepartmentId
    LEFT JOIN tblSubDepartment sd1 ON e.SubDepartmentId1 = sd1.SubDepartmentId
    LEFT JOIN tblSubDepartment sd2 ON e.SubDepartmentId2 = sd2.SubDepartmentId
    LEFT JOIN tblSubDepartment sd3 ON e.SubDepartmentId3 = sd3.SubDepartmentId
    LEFT JOIN tblDesignation dg ON e.DesignationId = dg.DesignationId
    INNER JOIN tblVendorMaster v ON e.ContractorCode = v.ContractorCode AND v.IsActive = 1
    LEFT JOIN tblShiftMaster s ON e.ShiftID = s.ShiftID
    WHERE
         e.ContractorCode = @ContractorCode
        AND e.CompanyId = 4
        AND (@ContractStartDate IS NULL OR e.[From] >= @ContractStartDate)
        AND (@ContractEndDate IS NULL OR e.[To] <= @ContractEndDate)
        AND (
            @SearchTerm = ''
            OR e.[FULL NAME] LIKE '%' + @SearchTerm + '%'
            OR e.Ecode LIKE '%' + @SearchTerm + '%'
            OR d.DepartmentName LIKE '%' + @SearchTerm + '%'
            OR sd1.SubDepartmentName LIKE '%' + @SearchTerm + '%'
            OR sd2.SubDepartmentName LIKE '%' + @SearchTerm + '%'
            OR sd3.SubDepartmentName LIKE '%' + @SearchTerm + '%'
            OR dg.DesignationName LIKE '%' + @SearchTerm + '%'
            OR s.ShiftName LIKE '%' + @SearchTerm + '%'
            OR v.ContractorName LIKE '%' + @SearchTerm + '%'
            OR CONVERT(VARCHAR, e.[From], 120) LIKE '%' + @SearchTerm + '%'
            OR CONVERT(VARCHAR, e.[To], 120) LIKE '%' + @SearchTerm + '%'
        );

    SELECT @TotalRecords = COUNT(*) FROM @FilteredEmployees;

    SELECT *
    FROM @FilteredEmployees
    ORDER BY Ecode
    OFFSET CASE WHEN @PageNumber > 0 AND @PageSize > 0 THEN (@PageNumber - 1) * @PageSize ELSE 0 END ROWS
    FETCH NEXT CASE WHEN @PageNumber > 0 AND @PageSize > 0 THEN @PageSize ELSE 2147483647 END ROWS ONLY;
END
GO

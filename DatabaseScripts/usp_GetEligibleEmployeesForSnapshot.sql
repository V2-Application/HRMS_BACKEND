CREATE OR ALTER PROC dbo.usp_GetEligibleEmployeesForSnapshot 
    @StCode      NVARCHAR(50) = N'RH01',
    @MonthKey    NVARCHAR(16) = NULL -- e.g. 'Oct-25'
AS
BEGIN
    SET NOCOUNT ON;

    -- Default month to current if NULL
    IF (@MonthKey IS NULL)
    BEGIN
        SET @MonthKey = UPPER(FORMAT(GETDATE(), 'MMM-yy'));
    END

    ;WITH EmpBase AS
    (
        SELECT
            e.Ecode,
            e.IsActive,
            EmployeeName =
                CASE
                    WHEN NULLIF(LTRIM(RTRIM(e.[FULL NAME])), '') IS NOT NULL THEN LTRIM(RTRIM(e.[FULL NAME]))
                    ELSE LTRIM(RTRIM(CONCAT(COALESCE(e.FirstName, ''), ' ', COALESCE(e.LastName, ''))))
                END,
            l.STCode,
            l.LocationName,
            dept.DepartmentName,
            desig.DesignationName
        FROM dbo.tblEmployee e (NOLOCK)
        LEFT JOIN dbo.tblLocation l (NOLOCK)
            ON l.LocationId = e.LocationId
        LEFT JOIN dbo.tblDepartment dept (NOLOCK)
            ON dept.DepartmentId = e.DepartmentId
        LEFT JOIN dbo.tblDesignation desig (NOLOCK)
            ON desig.DesignationId = e.DesignationId
        LEFT JOIN EMpAttendanceMaster eam (NOLOCK)
            ON e.Ecode = eam.E_CODE AND eam.[Month] = @MonthKey  
        WHERE (@StCode IS NULL OR LOWER(l.STCode) = LOWER(@StCode))
          AND (  
              e.IsActive = 1    
              OR (e.IsActive = 0 AND ISNULL(eam.TOTAL_PRESENT, 0) > 0)  
          )
    )
    SELECT
        b.Ecode,
        b.EmployeeName,
        b.STCode,
        b.LocationName,
        b.IsActive,
        b.DepartmentName,
        b.DesignationName
    FROM EmpBase b
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.EmpAttendanceViewSnapshot s WITH (NOLOCK)
        WHERE s.Ecode = b.Ecode
          AND s.[Month] = @MonthKey
    )
    OR EXISTS
    (
        SELECT 1
        FROM
        (
            SELECT TOP (1) s.SalaryStatus
            FROM dbo.EmpAttendanceViewSnapshot s WITH (NOLOCK)
            WHERE s.Ecode = b.Ecode
              AND s.[Month] = @MonthKey
            ORDER BY s.ID DESC
        ) t
        WHERE t.SalaryStatus = 5
    )
    ORDER BY b.Ecode;
END
GO


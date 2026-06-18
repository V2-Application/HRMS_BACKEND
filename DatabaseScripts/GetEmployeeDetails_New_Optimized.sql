CREATE OR ALTER PROCEDURE [dbo].[GetEmployeeDetails_New]
    @PageNumber INT = 0,
    @PageSize INT = 0,
    @SearchTerm NVARCHAR(100) = '',
    @TotalEmployees INT OUTPUT,
    @CurrentPageNumber INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @CurrentPageNumber = @PageNumber;

    /*
      OPTIMIZED list proc.
      - No-search (the common page load) uses a clean COUNT(*) on tblEmployee with NO joins and NO
        OR/LIKE tree, and the page is fetched straight off the clustered PK (EmployeeId DESC) joining
        only the 20 display rows. This removes the previous double full-scan-with-joins per request.
      - Search path keeps the multi-column LIKE (contains) but isolates it and uses OPTION(RECOMPILE)
        so the optimizer builds a plan for the actual term instead of a cached generic plan.
      Output columns/shape are unchanged from the original.
    */

    IF @SearchTerm IS NULL OR @SearchTerm = N''
    BEGIN
        /* ---------- FAST PATH: no search ---------- */
        SELECT @TotalEmployees = COUNT(*) FROM dbo.tblEmployee;

        IF @PageNumber > 0 AND @PageSize > 0
        BEGIN
            SELECT
                e.EmployeeId, e.CandidateId,
                'E-'+l.STCode+'-'+TRY_CAST(d.DepartmentId AS varchar(50))+'-'+TRY_CAST(dg.DesignationId AS varchar(50))+'-'+
                CASE WHEN e.CompanyId = 1 THEN RIGHT(e.Ecode, LEN(e.Ecode) - 1)
                     WHEN e.CompanyId = 2 THEN RIGHT(e.Ecode, LEN(e.Ecode) - 3)
                     WHEN e.CompanyId = 3 THEN RIGHT(e.Ecode, LEN(e.Ecode) - 2)
                     ELSE e.Ecode END AS [LocBasedECode],
                e.[FULL NAME] AS FullName, d.DepartmentName, dg.DesignationName,
                l.LocationName, l.STCode, e.Ecode, e.ReportHeadEcode, e.IsActive, e.IsDeleted
            FROM dbo.tblEmployee e
            LEFT JOIN dbo.tblDepartment  d  ON d.DepartmentId  = e.DepartmentId
            LEFT JOIN dbo.tblDesignation dg ON dg.DesignationId = e.DesignationId
            LEFT JOIN dbo.tblLocation    l  ON l.LocationId    = e.LocationId
            ORDER BY e.EmployeeId DESC
            OFFSET (@PageNumber - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;
        END
        ELSE
        BEGIN
            SELECT
                e.EmployeeId, e.[FULL NAME] AS FullName, d.DepartmentName, dg.DesignationName,
                l.LocationName, l.STCode, e.Ecode, e.ReportHeadEcode, e.IsActive, e.IsDeleted
            FROM dbo.tblEmployee e
            LEFT JOIN dbo.tblDepartment  d  ON d.DepartmentId  = e.DepartmentId
            LEFT JOIN dbo.tblDesignation dg ON dg.DesignationId = e.DesignationId
            LEFT JOIN dbo.tblLocation    l  ON l.LocationId    = e.LocationId
            ORDER BY e.EmployeeId DESC;
        END
        RETURN;
    END

    /* ---------- SEARCH PATH ---------- */
    DECLARE @Search NVARCHAR(102) = N'%' + @SearchTerm + N'%';

    SELECT @TotalEmployees = COUNT(*)
    FROM dbo.tblEmployee e
    LEFT JOIN dbo.tblDepartment  d  ON d.DepartmentId  = e.DepartmentId
    LEFT JOIN dbo.tblDesignation dg ON dg.DesignationId = e.DesignationId
    LEFT JOIN dbo.tblLocation    l  ON l.LocationId    = e.LocationId
    WHERE e.[FULL NAME] LIKE @Search
       OR e.Ecode LIKE @Search
       OR d.DepartmentName LIKE @Search
       OR dg.DesignationName LIKE @Search
       OR l.LocationName LIKE @Search
       OR l.STCode LIKE @Search
       OR e.ReportHeadEcode LIKE @Search
    OPTION (RECOMPILE);

    IF @PageNumber > 0 AND @PageSize > 0
    BEGIN
        SELECT
            e.EmployeeId, e.CandidateId,
            'E-'+l.STCode+'-'+TRY_CAST(d.DepartmentId AS varchar(50))+'-'+TRY_CAST(dg.DesignationId AS varchar(50))+'-'+
            CASE WHEN e.CompanyId = 1 THEN RIGHT(e.Ecode, LEN(e.Ecode) - 1)
                 WHEN e.CompanyId = 2 THEN RIGHT(e.Ecode, LEN(e.Ecode) - 3)
                 WHEN e.CompanyId = 3 THEN RIGHT(e.Ecode, LEN(e.Ecode) - 2)
                 ELSE e.Ecode END AS [LocBasedECode],
            e.[FULL NAME] AS FullName, d.DepartmentName, dg.DesignationName,
            l.LocationName, l.STCode, e.Ecode, e.ReportHeadEcode, e.IsActive, e.IsDeleted
        FROM dbo.tblEmployee e
        LEFT JOIN dbo.tblDepartment  d  ON d.DepartmentId  = e.DepartmentId
        LEFT JOIN dbo.tblDesignation dg ON dg.DesignationId = e.DesignationId
        LEFT JOIN dbo.tblLocation    l  ON l.LocationId    = e.LocationId
        WHERE e.[FULL NAME] LIKE @Search
           OR e.Ecode LIKE @Search
           OR d.DepartmentName LIKE @Search
           OR dg.DesignationName LIKE @Search
           OR l.LocationName LIKE @Search
           OR l.STCode LIKE @Search
           OR e.ReportHeadEcode LIKE @Search
        ORDER BY e.EmployeeId DESC
        OFFSET (@PageNumber - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY
        OPTION (RECOMPILE);
    END
    ELSE
    BEGIN
        SELECT
            e.EmployeeId, e.[FULL NAME] AS FullName, d.DepartmentName, dg.DesignationName,
            l.LocationName, l.STCode, e.Ecode, e.ReportHeadEcode, e.IsActive, e.IsDeleted
        FROM dbo.tblEmployee e
        LEFT JOIN dbo.tblDepartment  d  ON d.DepartmentId  = e.DepartmentId
        LEFT JOIN dbo.tblDesignation dg ON dg.DesignationId = e.DesignationId
        LEFT JOIN dbo.tblLocation    l  ON l.LocationId    = e.LocationId
        WHERE e.[FULL NAME] LIKE @Search
           OR e.Ecode LIKE @Search
           OR d.DepartmentName LIKE @Search
           OR dg.DesignationName LIKE @Search
           OR l.LocationName LIKE @Search
           OR l.STCode LIKE @Search
           OR e.ReportHeadEcode LIKE @Search
        ORDER BY e.EmployeeId DESC
        OPTION (RECOMPILE);
    END
END;

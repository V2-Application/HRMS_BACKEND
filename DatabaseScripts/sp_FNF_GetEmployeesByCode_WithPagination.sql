-- Updated stored procedure with pagination support
-- Drop existing procedure if it exists
IF OBJECT_ID('[dbo].[sp_FNF_GetEmployeesByCode]', 'P') IS NOT NULL
    DROP PROCEDURE [dbo].[sp_FNF_GetEmployeesByCode];
GO

-- Create updated procedure with pagination
CREATE PROCEDURE [dbo].[sp_FNF_GetEmployeesByCode]    
--'V15042'    
    @SearchEcode nvarchar(50) = NULL,    
    @TopRows     int = 50000,
    @Page        int = 1,
    @PageSize    int = 20
AS    
BEGIN    
    SET NOCOUNT ON;    
    SET @TopRows = 50000;    
    
    -- Validate pagination parameters
    IF @Page < 1 SET @Page = 1;
    IF @PageSize < 1 SET @PageSize = 20;
    
    DECLARE @q nvarchar(50) = NULLIF(LTRIM(RTRIM(@SearchEcode)), '');    
    DECLARE @TwoMonthsAgo DATE = DATEADD(YEAR, -1, GETDATE());    
    
    -- Create a temporary table to hold all filtered data
    CREATE TABLE #FilteredData (
        EmployeeId BIGINT,
        EmployeeCode NVARCHAR(20),
        Name NVARCHAR(50),
        Department NVARCHAR(255),
        Designation NVARCHAR(255),
        DateOfJoining DATETIME,
        DateOfLeaving DATETIME,
        IsFNFCompleted BIT,
        UnpaidSalaryAmount DECIMAL(18,2),
        UnpaidSalaryDays INT,
        UnpaidSalaryMonth NVARCHAR(50),
        ResignationType NVARCHAR(50),
        ResignationDate DATETIME,
        SeparationLastDay DATETIME,
        ManagerApproved BIT,
        HRApproved BIT,
        IsRevoked BIT,
        ResignationAttachment NVARCHAR(250),
        SortDate DATETIME
    );
    
    ;WITH SalaryBase AS    
    (    
        SELECT e.EmployeeId, e.Ecode, e.DateOfLeft,    
               e.monthlyGrossCTC, e.BasicSalary, e.HRA, e.DA, e.SpecialAllowance,    
               e.ExtraAllowance, e.DOJ,    
               e.[FULL NAME], e.FirstName, e.MiddleName, e.LastName,    
               e.DepartmentId, e.DesignationId, e.IsFNFCompleted, e.UpdatedOn    
        FROM dbo.tblEmployee e    
        WHERE ISNULL(e.IsStore, 0) <> 1    
          AND ISNULL(e.IsActive, 0) = 0    
    ),    
    SalarySource AS    
    (    
        SELECT sb.EmployeeId, sb.Ecode, sb.[DateOfLeft],    
               sb.monthlyGrossCTC,    
               ess.[Month]  AS UnpaidSalaryMonth,    
               ess.[Status],    
               ess.[Remarks]    
        FROM SalaryBase sb    
        LEFT JOIN dbo.EmpSalaryStatus ess    
          ON ess.ECode = sb.Ecode    
         AND ess.[Status] = 'Unpaid'    
    ),    
    DayCalc AS    
    (    
        SELECT ss.*,    
               TRY_CONVERT(INT,    
                   CASE    
                       WHEN ss.Remarks LIKE '%days=%'    
                           THEN SUBSTRING(ss.Remarks, CHARINDEX('days=', ss.Remarks) + 5, 10)    
                       WHEN ISNUMERIC(ss.Remarks) = 1    
                           THEN ss.Remarks    
                       ELSE NULL    
                   END) AS UnpaidSalaryDays    
        FROM SalarySource ss    
    ),    
    AmountCalc AS    
    (    
        SELECT dc.EmployeeId,    
               dc.Ecode,    
               dc.[DateOfLeft],    
               dc.monthlyGrossCTC,    
               dc.UnpaidSalaryMonth,    
               dc.UnpaidSalaryDays,    
               CASE    
                   WHEN dc.UnpaidSalaryDays IS NOT NULL AND dc.monthlyGrossCTC IS NOT NULL    
                        THEN ROUND((dc.monthlyGrossCTC / 30.0) * dc.UnpaidSalaryDays, 2)    
                   ELSE CAST(0.00 AS DECIMAL(18,2))    
               END AS UnpaidSalaryAmount    
        FROM DayCalc dc    
    ),    
    AmountAgg AS    
    (    
        SELECT    
            EmployeeId,    
            SUM(UnpaidSalaryAmount) AS UnpaidSalaryAmount,    
            SUM(UnpaidSalaryDays)   AS UnpaidSalaryDays,    
            MAX(UnpaidSalaryMonth)  AS UnpaidSalaryMonth    
        FROM AmountCalc    
        GROUP BY EmployeeId    
    ),    
    FinalData AS    
    (    
        SELECT    
           e.EmployeeId,    
           e.Ecode AS EmployeeCode,    
           ISNULL(e.[FULL NAME], CONCAT(ISNULL(e.FirstName,''),    
                CASE WHEN ISNULL(e.MiddleName,'') <> '' THEN ' ' + e.MiddleName ELSE '' END,    
                CASE WHEN ISNULL(e.LastName,'')   <> '' THEN ' ' + e.LastName   ELSE '' END)) AS [Name],    
           d.DepartmentName AS [Department],    
           g.DesignationName AS [Designation],    
           e.DOJ AS [DateOfJoining],    
           COALESCE(ts.LastDay, e.[DateOfLeft]) AS [DateOfLeaving],    
           e.IsFNFCompleted AS [IsFNFCompleted],    
           aa.UnpaidSalaryAmount AS [UnpaidSalaryAmount],    
           aa.UnpaidSalaryDays AS [UnpaidSalaryDays],    
           aa.UnpaidSalaryMonth AS [UnpaidSalaryMonth],    
           rt.ResignationTypeName AS [ResignationType],    
           ts.ResignationDate AS [ResignationDate],    
           ts.LastDay AS [SeparationLastDay],    
           ts.IsApprovedByManager AS [ManagerApproved],    
           ts.IsApprovedByHR AS [HRApproved],    
           ts.IsRevoked AS [IsRevoked],    
    
           -- ✅ NEW: nullable attachment (latest one)    
           r.Attachment AS [ResignationAttachment],    
    
           COALESCE(ts.LastDay, ts.ResignationDate, e.[DateOfLeft], e.[JOINING DATE], CONVERT(date,'19000101')) AS SortDate    
        FROM dbo.tblEmployee e    
        LEFT JOIN dbo.tblEmployeeSepration ts ON ts.EmployeeId = e.EmployeeId    
        LEFT JOIN dbo.tblDepartment  d        ON d.DepartmentId  = e.DepartmentId    
        LEFT JOIN dbo.tblDesignation g        ON g.DesignationId = e.DesignationId    
        LEFT JOIN AmountAgg aa                ON aa.EmployeeId   = e.EmployeeId    
        LEFT JOIN dbo.tblResignationType rt   ON rt.ResignationTypeId = ts.ResignationTypeId    
    
        OUTER APPLY    
        (    
            SELECT TOP 1 er.Attachment    
            FROM dbo.EmployeeResignationChecklistResponse er    
            WHERE er.EmployeeId = e.EmployeeId    
              AND er.Attachment IS NOT NULL    
            ORDER BY    
                ISNULL(er.LastUpdatedOn, er.CreatedOn) DESC,    
                er.EmployeeResignationChecklistResponseId DESC    
        ) r    
    
        WHERE    
            ISNULL(e.IsStore, 0) <> 1    
            AND ISNULL(e.IsActive, 0) = 0    
            AND    
            (    
                ts.EmployeeId IS NOT NULL    
                OR (e.UpdatedOn >= @TwoMonthsAgo)    
            )    
            AND    
            (    
                @q IS NULL    
                OR e.Ecode = @q    
                OR e.Ecode LIKE @q + '%'    
                OR e.[FULL NAME] LIKE '%' + @q + '%'    
                OR CONCAT(ISNULL(e.FirstName, ''),    
                          CASE WHEN ISNULL(e.MiddleName,'') <> '' THEN ' ' + e.MiddleName ELSE '' END,    
                          CASE WHEN ISNULL(e.LastName,'') <> '' THEN ' ' + e.LastName ELSE '' END) LIKE '%' + @q + '%'    
            )    
    ),    
    Dedup AS    
    (    
        SELECT fd.*,    
               ROW_NUMBER() OVER    
               (    
                   PARTITION BY fd.EmployeeCode    
                   ORDER BY fd.SortDate DESC, fd.EmployeeId DESC    
               ) AS rn    
        FROM FinalData fd    
    )
    -- Insert filtered data into temporary table
    INSERT INTO #FilteredData
    SELECT    
           EmployeeId,    
           EmployeeCode,    
           [Name],    
           [Department],    
           [Designation],    
           [DateOfJoining],    
           [DateOfLeaving],    
           [IsFNFCompleted],    
           [UnpaidSalaryAmount],    
           [UnpaidSalaryDays],    
           [UnpaidSalaryMonth],    
           [ResignationType],    
           [ResignationDate],    
           [SeparationLastDay],    
           [ManagerApproved],    
           [HRApproved],    
           [IsRevoked],    
           [ResignationAttachment],
           SortDate    
    FROM Dedup    
    WHERE rn = 1;
    
    -- Return total count
    SELECT COUNT(*) AS TotalCount
    FROM #FilteredData;
    
    -- Return paginated data
    SELECT    
           EmployeeId,    
           EmployeeCode,    
           [Name],    
           [Department],    
           [Designation],    
           [DateOfJoining],    
           [DateOfLeaving],    
           [IsFNFCompleted],    
           [UnpaidSalaryAmount],    
           [UnpaidSalaryDays],    
           [UnpaidSalaryMonth],    
           [ResignationType],    
           [ResignationDate],    
           [SeparationLastDay],    
           [ManagerApproved],    
           [HRApproved],    
           [IsRevoked],    
           [ResignationAttachment]   
    FROM #FilteredData
    ORDER BY [Name] ASC, EmployeeCode ASC
    OFFSET (@Page - 1) * @PageSize ROWS
    FETCH NEXT @PageSize ROWS ONLY;
    
    -- Clean up
    DROP TABLE #FilteredData;
END 
GO

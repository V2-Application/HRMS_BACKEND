IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tblSalaryProcess' AND type = 'U')
BEGIN
    CREATE TABLE tblSalaryProcess (
        SalaryProcessId INT IDENTITY(1,1) PRIMARY KEY,
        Ecode NVARCHAR(50),
        Location_Code NVARCHAR(50),
        LocationName NVARCHAR(100),
        EmployeeName NVARCHAR(100),
        Designation NVARCHAR(100),
        Department NVARCHAR(100),
        MonthYear NVARCHAR(20),
        TtlBgtDays DECIMAL(18, 2) DEFAULT 0,
        ActualTtlDays DECIMAL(18, 2) DEFAULT 0,
        GF DECIMAL(18, 2) DEFAULT 0,
        Machine DECIMAL(18, 2) DEFAULT 0,
        MachineWP DECIMAL(18, 2) DEFAULT 0,
        MANUAL DECIMAL(18, 2) DEFAULT 0,
        ActualWeekly DECIMAL(18, 2) DEFAULT 0,
        PresentWeeklyOff DECIMAL(18, 2) DEFAULT 0,
        HolidayOff INT DEFAULT 0,
        PaybleDays DECIMAL(18, 2) DEFAULT 0,
        ExtraDays DECIMAL(18, 2) DEFAULT 0,
        Absent DECIMAL(18, 2) DEFAULT 0,
        LWP DECIMAL(18, 2) DEFAULT 0,
        AdjustedDays DECIMAL(18, 2) DEFAULT 0,
        Status NVARCHAR(50),
        BasicSalaryBud DECIMAL(18, 2) DEFAULT 0,
        HRABud DECIMAL(18, 2) DEFAULT 0,
        CCABud DECIMAL(18, 2) DEFAULT 0,
        SpecialAllowanceBud DECIMAL(18, 2) DEFAULT 0,
        DABud DECIMAL(18, 2) DEFAULT 0,
        ReimbersmentBud DECIMAL(18, 2) DEFAULT 0,
        FuelAndMaintenanceBud DECIMAL(18, 2) DEFAULT 0,
        BooksAndPeriodicalsBud DECIMAL(18, 2) DEFAULT 0,
        ProfessionalAttireBud DECIMAL(18, 2) DEFAULT 0,
        DriverWagesBud DECIMAL(18, 2) DEFAULT 0,
        MobileBillBud DECIMAL(18, 2) DEFAULT 0,
        MealVoucherBud DECIMAL(18, 2) DEFAULT 0,
        MonthlyGrossCTCBud DECIMAL(18, 2) DEFAULT 0,
        BasicSalaryActual DECIMAL(18, 2) DEFAULT 0,
        HRAActual DECIMAL(18, 2) DEFAULT 0,
        CCAActual DECIMAL(18, 2) DEFAULT 0,
        SpecialAllowanceActual DECIMAL(18, 2) DEFAULT 0,
        DAActual DECIMAL(18, 2) DEFAULT 0,
        ExtraDayAllowance DECIMAL(18, 2) DEFAULT 0,
        ReimbersmentActual DECIMAL(18, 2) DEFAULT 0,
        FuelAndMaintenanceActual DECIMAL(18, 2) DEFAULT 0,
        BooksAndPeriodicalsActual DECIMAL(18, 2) DEFAULT 0,
        ProfessionalAttireActual DECIMAL(18, 2) DEFAULT 0,
        DriverWagesActual DECIMAL(18, 2) DEFAULT 0,
        MobileBillActual DECIMAL(18, 2) DEFAULT 0,
        MealVoucherActual DECIMAL(18, 2) DEFAULT 0,
        PFEmployee DECIMAL(18, 2) DEFAULT 0,
        PFEmployer DECIMAL(18, 2) DEFAULT 0,
        PFTotal DECIMAL(18, 2) DEFAULT 0,
        ESICEmployee DECIMAL(18, 2) DEFAULT 0,
        ESICEmployer DECIMAL(18, 2) DEFAULT 0,
        ESICTotal DECIMAL(18, 2) DEFAULT 0,
        TDS DECIMAL(18, 2) DEFAULT 0,
        PTax DECIMAL(18, 2) DEFAULT 0,
        Loan DECIMAL(18, 2) DEFAULT 0,
        CashShort DECIMAL(18, 2) DEFAULT 0,
        DieselDeduction DECIMAL(18, 2) DEFAULT 0,
        Penality DECIMAL(18, 2) DEFAULT 0,
        Lwf DECIMAL(18, 2) DEFAULT 0,
        TotalDeductions DECIMAL(18, 2) DEFAULT 0,
        Incentive DECIMAL(18, 2) DEFAULT 0,
        ARREAR DECIMAL(18, 2) DEFAULT 0,
        Overtime DECIMAL(18, 2) DEFAULT 0,
        FoodingAllowance DECIMAL(18, 2) DEFAULT 0,
        MobileBill DECIMAL(18, 2) DEFAULT 0,
        MonthlyGrossCTCActual DECIMAL(18, 2) DEFAULT 0,
        MonthlyGrossCTCActualAfterDeductionAndAddOns DECIMAL(18, 2) DEFAULT 0,
        Payble_Days2 DECIMAL(18, 2) DEFAULT 0,
        LeaveUsed DECIMAL(18, 2) DEFAULT 0,
        OpeningEL DECIMAL(18, 2) DEFAULT 0,
        EarnedLeaveAcquired DECIMAL(18, 2) DEFAULT 0,
        EarnedLeaveUsed DECIMAL(18, 2) DEFAULT 0,
        EarnedLeaveBalance DECIMAL(18, 2) DEFAULT 0,
        OpeningCL DECIMAL(18, 2) DEFAULT 0,
        CasualLeaveAcquired DECIMAL(18, 2) DEFAULT 0,
        CasualLeaveUsed DECIMAL(18, 2) DEFAULT 0,
        CasualLeaveBalance DECIMAL(18, 2) DEFAULT 0,
        OpeningCompoOff DECIMAL(18, 2) DEFAULT 0,
        CompoOffAcquired DECIMAL(18, 2) DEFAULT 0,
        CompoOffUsed DECIMAL(18, 2) DEFAULT 0,
        CompoOffBalance DECIMAL(18, 2) DEFAULT 0,
        MONTH NVARCHAR(50),
        BatchNo INT,
        RunAt DATETIME,
        SalaryStatus NVARCHAR(50),
        ID INT NULL,  -- Renamed back to ID as per request, nullable
        CreatedOn DATETIME DEFAULT GETDATE(),
        CreatedBy NVARCHAR(50)
    );
END
GO
CREATE OR ALTER PROCEDURE sp_ProcessSalary_List
    @SearchTerm NVARCHAR(100) = NULL,
    @PageNumber INT = 1,
    @PageSize INT = 10
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Offset INT = (@PageNumber - 1) * @PageSize;

    -- Count Total Records
    SELECT COUNT(*) AS TotalCount
    FROM tblSalaryProcess
    WHERE 
        (@SearchTerm IS NULL OR @SearchTerm = '' OR 
         Ecode LIKE '%' + @SearchTerm + '%' OR 
         EmployeeName LIKE '%' + @SearchTerm + '%' OR 
         LocationName LIKE '%' + @SearchTerm + '%' OR
         MonthYear LIKE '%' + @SearchTerm + '%' OR
         CAST(BatchNo AS NVARCHAR(20)) LIKE '%' + @SearchTerm + '%');

    -- Get Page Data
    SELECT *
    FROM tblSalaryProcess
    WHERE 
        (@SearchTerm IS NULL OR @SearchTerm = '' OR 
         Ecode LIKE '%' + @SearchTerm + '%' OR 
         EmployeeName LIKE '%' + @SearchTerm + '%' OR 
         LocationName LIKE '%' + @SearchTerm + '%' OR
         MonthYear LIKE '%' + @SearchTerm + '%' OR
         CAST(BatchNo AS NVARCHAR(20)) LIKE '%' + @SearchTerm + '%')
    ORDER BY SalaryProcessId DESC
    OFFSET @Offset ROWS
    FETCH NEXT @PageSize ROWS ONLY;
END
GO
CREATE OR ALTER PROCEDURE sp_ProcessSalary_Export
    @SearchTerm NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- Get All Data Matching Search
    SELECT *
    FROM tblSalaryProcess
    WHERE 
        (@SearchTerm IS NULL OR @SearchTerm = '' OR 
         Ecode LIKE '%' + @SearchTerm + '%' OR 
         EmployeeName LIKE '%' + @SearchTerm + '%' OR 
         LocationName LIKE '%' + @SearchTerm + '%' OR
         MonthYear LIKE '%' + @SearchTerm + '%' OR
         CAST(BatchNo AS NVARCHAR(20)) LIKE '%' + @SearchTerm + '%')
    ORDER BY SalaryProcessId DESC;
END
GO

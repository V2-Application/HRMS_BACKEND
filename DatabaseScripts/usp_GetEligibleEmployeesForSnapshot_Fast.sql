CREATE OR ALTER PROC [dbo].[usp_GetEligibleEmployeesForSnapshot_Fast]
    @Ecode       NVARCHAR(50) = NULL,
    @MonthKey    NVARCHAR(16) = NULL -- e.g. 'Oct-25'
AS
BEGIN
    SET NOCOUNT ON;

    IF (@MonthKey IS NULL)
        SET @MonthKey = UPPER(FORMAT(GETDATE(), 'MMM-yy'));

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
        FROM dbo.tblEmployee e WITH (NOLOCK)
        LEFT JOIN dbo.tblLocation l WITH (NOLOCK) ON l.LocationId = e.LocationId
        LEFT JOIN dbo.tblDepartment dept WITH (NOLOCK) ON dept.DepartmentId = e.DepartmentId
        LEFT JOIN dbo.tblDesignation desig WITH (NOLOCK) ON desig.DesignationId = e.DesignationId
        LEFT JOIN dbo.EMpAttendanceMaster eam WITH (NOLOCK)
               ON e.Ecode = eam.E_CODE AND eam.[Month] = @MonthKey
        WHERE (@Ecode IS NULL OR e.Ecode = @Ecode)
          AND (
                e.IsActive = 1
                OR (e.IsActive = 0 AND ISNULL(eam.TOTAL_PRESENT,0) > 0)
              )
    )
    SELECT  
        -- Base fields (keep these)  
        b.Ecode,  
        b.STCode            AS [Location_Code],  
        b.LocationName      AS [Location_Name],  
        b.EmployeeName      AS [Employee_Name],  
        b.DesignationName   AS [designation],  
        b.DepartmentName    AS [department],  
  
        -- Snapshot fields (ALL)  
        s.[Month-Year]          AS [Month_Year],  
        s.[ttl bgt days]        AS [ttl_bgt_days],  
        s.[actualttl days]      AS [actualttl_days],  
        s.[GF],  
        s.[Machine],  
        s.[MachineWP],  
        s.[MANUAL],  
        s.[actualweekly],  
        s.[presentweeklyoff],  
        s.[HolidayOff],  
        s.[paybledays],  
        s.[extradays],  
        s.[Absent],  
        s.[LWP],  
        s.[AdjustedDays],  
        s.[Status],  
        s.[BasicSalary(Bud.)]      AS [BasicSalary_Bud_],  
        s.[HRA(Bud.)]              AS [HRA_Bud_],  
        s.[CCA(Bud.)]              AS [CCA_Bud_],  
        s.[SpecialAllowance(Bud.)] AS [SpecialAllowance_Bud_],  
        s.[DA(Bud.)]               AS [DA_Bud_],  
        s.[Reimbersment(Bud.)]     AS [Reimbersment_Bud_],  
        s.[Fuel and Maintenance(Bud.)] AS [Fuel_and_Maintenance_Bud_],  
        s.[Books and Periodicals(Bud.)] AS [Books_and_Periodicals_Bud_],  
        s.[Professional Attire(Bud.)] AS [Professional_Attire_Bud_],  
        s.[Driver Wages(Bud.)]     AS [Driver_Wages_Bud_],  
        s.[Mobile Bill(Bud.)]       AS [Mobile_Bill_Bud_],  
        s.[Meal Voucher(Bud.)]      AS [Meal_Voucher_Bud_],  
        s.[Monthly Gross CTC(Bud.)] AS [Monthly_Gross_CTC_Bud_],  
        s.[BasicSalary(Actual)]     AS [BasicSalary_Actual_],  
        s.[HRA(Actual)]             AS [HRA_Actual_],  
        s.[CCA(Actual)]             AS [CCA_Actual_],  
        s.[SpecialAllowance(Actual)] AS [SpecialAllowance_Actual_],  
        s.[DA(Actual)]              AS [DA_Actual_],  
        s.[ExtraDayAllowance],  
        s.[Reimbersment(Actual)]    AS [Reimbersment_Actual_],  
        s.[Fuel and Maintenance(Actual)] AS [Fuel_and_Maintenance_Actual_],  
        s.[Books and Periodicals(Actual)] AS [Books_and_Periodicals_Actual_],  
        s.[Professional Attire(Actual)] AS [Professional_Attire_Actual_],  
        s.[Driver Wages(Actual)]    AS [Driver_Wages_Actual_],  
        s.[Mobile Bill(Actual)]     AS [Mobile_Bill_Actual_],  
        s.[Meal Voucher(Actual)]    AS [Meal_Voucher_Actual_],  
        s.[PF(Employee)]            AS [PF_Employee_],  
        s.[PF(Employeer)]           AS [PF_Employeer_],  
        s.[PF(Total)]               AS [PF_Total_],  
        s.[ESIC(Employee)]          AS [ESIC_Employee_],  
        s.[ESIC(Employeer)]         AS [ESIC_Employeer_],  
        s.[ESIC(Total)]             AS [ESIC_Total_],  
        s.[TDS],  
        s.[PTax],  
        s.[Loan],  
        s.[CashShort],  
        s.[DieselDeduction],  
        s.[Penality],  
        s.[Lwf],  
        s.[TotalDeductions],  
        s.[Incentive],  
        s.[ARREAR],  
        s.[Overtime],  
        s.[Fooding_Allowance],  
        s.[Mobile_Bill],  
        s.[Monthly Gross CTC(Actual)] AS [Monthly_Gross_CTC_Actual_],  
        s.[Monthly Gross CTC(Actual After Deduction AND AddONS)] AS [Monthly_Gross_CTC_Actual_After_Deduction_AND_AddONS_],  
        s.[Payble_Days],  
        s.[Leave-Used]              AS [Leave_Used],  
        s.[Opening EL]              AS [Opening_EL],  
        s.[EarnedLeaveAcquired],  
        s.[EarnedLeaveUsed],  
        s.[EarnedLeaveBalance],  
        s.[Opening CL]              AS [Opening_CL],  
        s.[CasualLeaveAcquired],  
        s.[CasualLeaveUsed],  
        s.[CasualLeaveBalance],  
        s.[Opening CompoOff]        AS [Opening_CompoOff],  
        s.[CompoOffAcquired],  
        s.[CompoOffUsed],  
        s.[CompoOffBalance],  
        s.[MONTH],  
        s.[BatchNo],  
        s.[RunAt],  
        s.[SalaryStatus],  
        s.[ID]
    FROM EmpBase b
    OUTER APPLY
    (
        SELECT TOP (1) *
        FROM dbo.EmpAttendanceViewSnapshot s WITH (NOLOCK)
        WHERE s.Ecode = b.Ecode
          AND s.[Month] = @MonthKey
        ORDER BY s.ID DESC
    ) s
    WHERE
        s.ID IS NULL
        OR s.SalaryStatus IN (0, -1, 5)
    ORDER BY b.Ecode DESC;
END

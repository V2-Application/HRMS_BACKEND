CREATE OR ALTER PROCEDURE [dbo].[GetEmployeeDetailsforexcel_Ishu]              
    @IsActive BIT = 1,              
    @AllEmployee BIT = 0,              
    @CompanyId INT = 0              
AS              
BEGIN              
    SET NOCOUNT ON;              
      ;WITH LastPunch AS    
(    
    SELECT     
        x.ECode,    
        MAX(x.AttendanceDate) AS LastPunchDate    
    FROM dbo.tbl_fn_GetMonthlyPunchesRange_productionnewnick_test x    
    WHERE TRY_CAST(x.TotalWorkingMinutes AS time) >= '04:30'   and x.ValidPunchCount >=1  
    GROUP BY x.ECode    
),    
Separation AS    
(    
    SELECT     
        EmpId,    
        MAX(UpdatedOn) AS SeparationDate    
    FROM tblEmployeeActiveInActiveHistories    
    WHERE ActionPerformed = 'False'    
    GROUP BY EmpId    
),    
Attachments AS    
(    
    SELECT     
        EmployeeId,    
        MAX(Attachment) AS Attachment    
    FROM HRMS.dbo.EmployeeResignationChecklistResponse    
    WHERE Attachment IS NOT NULL    
    GROUP BY EmployeeId    
)    
    SELECT                  
        e.Ecode AS [Employee Code],                
        e.AOCode AS [AO Code],                
          
        'E-'+l.STCode+'-'+TRY_CAST(d.DepartmentId AS VARCHAR(50))+'-'+TRY_CAST(dg.DesignationId AS VARCHAR(50))+'-'+                  
            CASE                   
                WHEN e.CompanyId = 1 THEN RIGHT(e.Ecode, LEN(e.Ecode) - 1)          -- remove 'V'                  
                WHEN e.CompanyId = 2 THEN RIGHT(e.Ecode, LEN(e.Ecode) - 3)         -- remove 'V2S'                  
                WHEN e.CompanyId = 3 THEN RIGHT(e.Ecode, LEN(e.Ecode) - 2)         -- remove 'PT'                  
                ELSE e.Ecode                  
            END AS [LocBasedECode],                  
          
        e.[FULL NAME] AS [Name of Employee],                  
        l.LocationName AS [Posted Location],                  
        l.LocationName AS [Joined Location],                  
        e.[GENDER] AS [Sex],                  
        REPLACE(CONVERT(VARCHAR(9), e.[DOB], 6),' ','-') AS [D.O.B.],              
          
        d.DepartmentName AS [Department],                  
        dg.DesignationName AS [Designation],                  
          
        sm.ShiftName AS [Shift Name],                
          
        e.[PLACE OF BIRTH] AS [Home Town],                  
        REPLACE(CONVERT(VARCHAR(9), e.DOJ, 6),' ','-') AS [D.O.J.],               
        REPLACE(CONVERT(VARCHAR(9), sep.SeparationDate, 6),' ','-') AS [D.O.L.], 
        REPLACE(CONVERT(VARCHAR(9), e.DateOfResignation, 6),' ','-') AS [Resignation Date],               
          
        COALESCE(NULLIF(NULLIF(e.[BANK NAME], ''), 'NA'), NULLIF(c.[BANK NAME], ''), 'NA') AS [Name of Bank],                  
        COALESCE(NULLIF(NULLIF(e.[A/C NO], ''), 'NA'), NULLIF(c.[A/C NO], ''), 'NA') AS [A/c No.],                  
        COALESCE(NULLIF(NULLIF(e.[BANK IFSC CODE], ''), 'NA'), NULLIF(c.[BANK IFSC CODE], ''), 'NA') AS [IFSC Code],                  
          
        e.[PERMANENT ADDRESS] AS [Permanent Addess],                  
        e.[PRESENT ADDRESS] AS [Present Address],                  
        e.[MOBILE] AS [Mob No.],                  
        e.MOBILE2 AS [Phone No.],                  
        e.[EMAIL ADDRESS] AS [Email Id],                  
        e.[AADHAR NO] AS [Aadhar No.],                  
        e.[PAN NO] AS [PAN No.],                  
          
        COALESCE(NULLIF(NULLIF(e.[HIGHEST QUALIFICATION], ''), 'NA'), NULLIF(c.[HIGHEST QUALIFICATION], ''), 'NA') AS [Qualification],                  
          
        e.[FATHER'S NAME] AS [Father's Name],                  
        e.[MOTHER'S NAME] AS [Mothers Name],                  
        e.[MARITIAL STATUS] AS [Marital Status],                  
          
        e.ReportHeadEcode AS [Reporting Head ECode],                  
        rh.[FULL NAME] AS [Reporting Head Name],                  
          
        COALESCE(NULLIF(NULLIF(e.[FAMILY MEMBER Relation], ''), 'NA'), NULLIF(c.[FAMILY MEMBER Relation], ''), 'NA') AS [Relation],                 
        COALESCE(                  
            NULLIF(CONVERT(VARCHAR(10), e.[FAMILY MEMBER DOB], 103), ''),                   
            NULLIF(CONVERT(VARCHAR(10), c.[FAMILY MEMBER DOB], 103), ''),                   
            'NA'                  
        ) AS [CHILD DOB],                  
          
        COALESCE(NULLIF(NULLIF(e.[COMPANY 1], ''), 'NA'), NULLIF(c.[COMPANY 1], ''), 'NA') AS [Company],                  
          
        /* ? Gross Salary = Basic + DA + CCA + Special Allowance + Extra Allowance + HRA */              
        COALESCE(              
            CONVERT(VARCHAR(50), NULLIF(gsE.GrossSalaryCalc, 0)),              
            CONVERT(VARCHAR(50), NULLIF(gsC.GrossSalaryCalc, 0)),              
            'NA'              
        ) AS [Gross Salary],              
          
        COALESCE(                  
            NULLIF(NULLIF(CAST(e.[In Hand Salary] AS VARCHAR), '0'), ''),                   
            NULLIF(NULLIF(CAST(c.[In Hand Salary] AS VARCHAR), '0'), ''),                   
            'NA'                  
        ) AS [Joining Salary],                  
          
        COALESCE(                  
            NULLIF(NULLIF(CAST(e.[LAST CTC(ANNUAL)] AS VARCHAR), '0'), ''),                   
            NULLIF(NULLIF(CAST(c.[LAST CTC(ANNUAL)] AS VARCHAR), '0'), ''),                   
            'NA'                  
        ) AS [Annual CTC],                  
          
        'NA' AS [Sal Structure],                  
        COALESCE(e.DOJ, c.[JOINING DATE], NULL) AS [D.O.J.Group],                  
        COALESCE(e.PFApplicable, c.PFApplicable, NULL) AS [P.F. Applicable?],                  
        'NA' AS [P.F. No.],                  
        'NA' AS [Previous P.F. No.],                  
        COALESCE(e.ESICApplicable, c.ESICApplicable, NULL) AS [E.S.I. Applicable?],                  
          
        COALESCE(                  
            NULLIF(NULLIF(CAST(e.ESICNO AS VARCHAR), ''), ''),                   
            NULLIF(NULLIF(CAST(c.[PREV. EST NO.] AS VARCHAR), ''), ''),                   
            'NA'                  
        ) AS [ESIC_NO],                  
          
        'NA' AS [E.S.I. No.],                  
        COALESCE(NULLIF(NULLIF(e.[UAN NO], ''), 'NA'), NULLIF(c.[UAN NO], ''), 'NA') AS [Universal A/c Number],                  
          
        COALESCE(                  
            NULLIF(NULLIF(CAST(e.BasicSalary AS VARCHAR), '0'), ''),                   
            NULLIF(NULLIF(CAST(c.BasicSalary AS VARCHAR), '0'), ''),                   
            'NA'                  
        ) AS [Basic Salary],                  
          
        COALESCE(                  
            NULLIF(NULLIF(CAST(e.DA AS VARCHAR), '0'), ''),                   
            NULLIF(NULLIF(CAST(c.DA AS VARCHAR), '0'), ''),                   
            'NA'                  
        ) AS [D.A.],                  
          
        COALESCE(                  
            NULLIF(NULLIF(CAST(e.HRA AS VARCHAR), '0'), ''),                   
            NULLIF(NULLIF(CAST(c.HRA AS VARCHAR), '0'), ''),                   
            'NA'                  
        ) AS [H.R.A.],                  
          
        COALESCE(                  
            NULLIF(NULLIF(CAST(e.CCA AS VARCHAR), '0'), ''),                   
            NULLIF(NULLIF(CAST(c.CCA AS VARCHAR), '0'), ''),                   
            'NA'                  
        ) AS [C.C.A.],                  
          
        COALESCE(                  
            NULLIF(NULLIF(CAST(e.SpecialAllowance AS VARCHAR), '0'), ''),                   
            NULLIF(NULLIF(CAST(c.SpecialAllowance AS VARCHAR), '0'), ''),                   
            'NA'                  
        ) AS [Special Allowance],                  
          
        COALESCE(                  
            NULLIF(NULLIF(CAST(e.ExtraAllowance AS VARCHAR), '0'), ''),                   
            NULLIF(NULLIF(CAST(c.ExtraAllowance AS VARCHAR), '0'), ''),                   
            'NA'                  
        ) AS [Extra Allowance],                  
          
        COALESCE(                  
            NULLIF(NULLIF(CAST(e.monthlyGrossCTC AS VARCHAR), '0'), ''),                   
            NULLIF(NULLIF(CAST(c.monthlyGrossCTC AS VARCHAR), '0'), ''),                   
          'NA'                  
        ) AS [MONTHLY CTC.],                  
          
        COALESCE(                  
            NULLIF(NULLIF(CAST(e.annuallyNetCTC AS VARCHAR), '0'), ''),                   
            NULLIF(NULLIF(CAST(c.annuallyNetCTC AS VARCHAR), '0'), ''),                   
            'NA'                  
        ) AS [Annually Net CTC],                  
          
        COALESCE(                  
            NULLIF(NULLIF(CAST(e.SalaryExpectation AS VARCHAR), '0'), ''),                   
            NULLIF(NULLIF(CAST(c.SalaryExpectation AS VARCHAR), '0'), ''),                   
            'NA'                  
        ) AS [Salary Expectation],                  
          
        0 AS [Conveyance],                  
        0 AS [Medical Allowance],                  
        0 AS [Incentive],                  
        0 AS [Fooding Allowance],                  
        0 AS [Leave Encashment],                  
        0 AS [Medical Reim],                  
        0 AS [Lta],        
  CASE WHEN ISNULL(e.BonusApplicable, N'No') IN (N'Ctc', N'Stat', N'Yes') THEN 'Yes' ELSE 'No' END AS [Bonus/Ex-Gratia],                  
        0 AS [Cca],                  
        0 AS [P.Tax],           
        0 AS [L.W.F.],                  
        0 AS [Inc.Paid],                  
        0 AS [Tds],                  
        0 AS [Esi],                  
        0 AS [Recovery],                  
        0 AS [Cash Short],                  
        0 AS [Diesel Deduction],                  
        0 AS [Penalty],                  
        0 AS [Lwf],                  
        0 AS [Medical],                  
          
        ISNULL(TRY_CONVERT(DECIMAL(18,2), e.Reimbersment), 0)            AS [Reimbersment],          
        ISNULL(TRY_CONVERT(DECIMAL(18,2), e.Fuel_and_Maintainence), 0)   AS [Fuel & Maintenance],          
        ISNULL(TRY_CONVERT(DECIMAL(18,2), e.Books_and_Periodicals), 0)   AS [Books & Periodicals],          
        ISNULL(TRY_CONVERT(DECIMAL(18,2), e.[Professional Attire]), 0)   AS [Professional Attire],          
        ISNULL(TRY_CONVERT(DECIMAL(18,2), e.[Driver Wages]), 0)          AS [Driver Wages],          
        ISNULL(TRY_CONVERT(DECIMAL(18,2), e.[Meal Voucher]), 0)          AS [Meal Voucher],          
        ISNULL(TRY_CONVERT(DECIMAL(18,2), e.[Mobile Bill]), 0)           AS [Mobile Bill],          
          
        0 AS [Gross Salary_2],                  
        0 AS [Employeer PF],                  
        0 AS [Employeer ESI],                  
        0 AS [AC NO.2],                  
        0 AS [AC NO.21],                  
        0 AS [GRATITY],                  
        0 AS [T.D.S.],                  
        'NA' AS [Payment Mode],                  
        'NA' AS [Passport No.],                  
        'NA' AS [FATHER DOB],                  
        'NA' AS [MOTHER DOB],                  
        'NA' AS [SPOUSE DOB],                  
        'NA' AS [CHILD NAME_2],                  
        'NA' AS [Relation_2],                  
        'NA' AS [CHILD DOB_2],                  
        'NA' AS [LIC No.],                  
        'NA' AS [P.A.policy no.],                  
        'NA' AS [Mediclaim No.],                  
        'NA' AS [Fooding Details],                  
        'NA' AS [Accomodation Details],                  
        'NA' AS [Desig. Band],                  
        'NA' AS [Annual Gross],                  
        'NA' AS [Hold Salary ?],                  
        'NA' AS [Hold Reason/Remark],                  
        'NA' AS [Reimbursement A/c No.],                  
        'NA' AS [Reimbursement Bank],                  
        'NA' AS [Notice Days],                  
     'NA' AS [Date of Confirmation],                  
        'NA' AS [branch],                  
        'NA' AS [empstatus],                  
        'NA' AS [trfreason],                  
        'NA' AS [trfrdate],                  
        'NA' AS [trfremark],                  
        'NA' AS [senior],                  
        'NA' AS [junior],                  
        'NA' AS [icustomer],                  
        'NA' AS [hod],                  
        'NA' AS [rmanager],                        'NA' AS [jbname],                  
        'NA' AS [jobprofile],                  
        'NA' AS [subdesig],                  
        'NA' AS [sdsgrade],                  
          
        l.STCODE AS [states],      
      
        -- ? CHANGE HERE: Always show Active/Separated based on e.IsActive      
        CASE       
            WHEN ISNULL(e.IsActive,0) = 0 THEN 'Separated'      
            ELSE 'Active'      
        END AS EmployeeStatus,      
      
        e.IsStore AS [Is Store],                  
        er.EmployeeRoleId AS [Employee Role ID],                  
        COALESCE(r.RoleName, 'Employee') AS [Role Name],            
        cc.[FULL NAME] +' ('+cc.Ecode+')' as CreatedBy,              
        uu.[FULL NAME] +' ('+uu.Ecode+')' as UpdatedBy,    
       REPLACE(CONVERT(VARCHAR(9), sep.SeparationDate, 6),' ','-') AS [Separation Date],
REPLACE(CONVERT(VARCHAR(9), lp.LastPunchDate, 6),' ','-') AS [Last Punch Date],   
    
CASE    
WHEN rt.Attachment IS NOT NULL    
THEN CONCAT('https://v2parivar.v2retail.com:9987/', rt.Attachment)    
ELSE NULL    
END AS [Attachment Link],    
         CASE    
        WHEN ISNULL(fp.Status,'') = 'Paid'    
            THEN 'UTR Exists'    
        ELSE 'UTR Pending'    
    END AS [UTR Status],    
       fp.ChequeNo AS [UTR / Cheque Number],  
        e.UpdatedOn            
          
    FROM tblEmployee e                  
        LEFT JOIN tblDepartment d ON d.DepartmentId = e.DepartmentId                  
        LEFT JOIN tblDesignation dg ON dg.DesignationId = e.DesignationId                  
        LEFT JOIN tblLocation l ON l.LocationId = e.LocationId                  
        LEFT JOIN tblEmployee rh ON rh.Ecode = e.ReportHeadEcode            
        LEFT JOIN Candidate c ON c.id = e.CandidateId AND e.CandidateId IS NOT NULL AND e.CandidateId > 0                
        --LEFT JOIN tblEmployee (NOLOCK) uu ON TRY_CAST(e.UpdatedBy AS INT) = TRY_CAST(uu.EmployeeId AS INT)              
        LEFT JOIN tblEmployee uu    
ON uu.EmployeeId = TRY_CAST(e.UpdatedBy AS INT)    
        --LEFT JOIN tblEmployee (NOLOCK) cc ON TRY_CAST(e.CreatedBy AS INT) = TRY_CAST(cc.EmployeeId AS INT)              
      LEFT JOIN tblEmployee cc    
ON cc.EmployeeId = TRY_CAST(e.CreatedBy AS INT)    
      LEFT JOIN fnf_header fh    
    ON fh.EmployeeId = e.EmployeeId    
    LEFT JOIN Separation sep    
ON sep.EmpId = CAST(e.EmployeeId AS NVARCHAR(50))    
LEFT JOIN LastPunch lp    
ON lp.ECode = e.Ecode    
LEFT JOIN Attachments rt    
ON rt.EmployeeId = e.EmployeeId    
LEFT JOIN fnf_payment fp    
    ON fp.FNFId = fh.FNFId    
        
    
        OUTER APPLY (              
            SELECT CAST(            
                ISNULL(TRY_CONVERT(DECIMAL(18,2), e.BasicSalary), 0) +              
                ISNULL(TRY_CONVERT(DECIMAL(18,2), e.DA), 0) +              
                ISNULL(TRY_CONVERT(DECIMAL(18,2), e.CCA), 0) +              
                ISNULL(TRY_CONVERT(DECIMAL(18,2), e.SpecialAllowance), 0) +              
                ISNULL(TRY_CONVERT(DECIMAL(18,2), e.ExtraAllowance), 0) +              
                ISNULL(TRY_CONVERT(DECIMAL(18,2), e.HRA), 0)              
            AS DECIMAL(18,2)) AS GrossSalaryCalc              
        ) gsE              
          
        OUTER APPLY (              
            SELECT CAST(              
                ISNULL(TRY_CONVERT(DECIMAL(18,2), c.BasicSalary), 0) +              
                ISNULL(TRY_CONVERT(DECIMAL(18,2), c.DA), 0) +              
                ISNULL(TRY_CONVERT(DECIMAL(18,2), c.CCA), 0) +              
                ISNULL(TRY_CONVERT(DECIMAL(18,2), c.SpecialAllowance), 0) +              
                ISNULL(TRY_CONVERT(DECIMAL(18,2), c.ExtraAllowance), 0) +              
                ISNULL(TRY_CONVERT(DECIMAL(18,2), c.HRA), 0)              
            AS DECIMAL(18,2)) AS GrossSalaryCalc              
        ) gsC              
          
        LEFT JOIN tblShiftMaster sm ON sm.ShiftID = e.ShiftID                
        LEFT JOIN tblEmployeeRole er ON er.EmployeeId = e.EmployeeId                  
        LEFT JOIN tblRole r ON r.RoleId = er.RoleId                  
          
    WHERE                  
        (@AllEmployee = 1 OR e.IsActive = @IsActive)                
        AND (@CompanyId = 0 OR e.CompanyId = @CompanyId)                
          
    ORDER BY                  
        e.EmployeeId DESC;                  
END


-- =============================================================================
-- Category: Bonus
-- Source:   dev DB (192.168.151.27\KARMA / HRMS)
-- Generated: 2026-05-14 12:15:05
-- Objects rewritten as CREATE OR ALTER for safe re-run on production.
-- =============================================================================
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- -----------------------------------------------------------------------------
-- dbo.usp_ProcessBonusAndPayments
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[usp_ProcessBonusAndPayments]  
(  
      @Month     NVARCHAR(20),   -- e.g. 'Nov-2025' or '2025-11'  
      @Ecode     NVARCHAR(20),   -- specific Ecode  
      @CreatedBy NVARCHAR(100)   -- e.g. 'system' or login name  
)  
AS  
BEGIN  
    SET NOCOUNT ON;  
      DECLARE @PolicyId NVARCHAR(50);  
    ------------------------------------------------------------  
    -- GET CURRENT POLICY FOR THIS ECODE  
    ------------------------------------------------------------  
    SELECT @PolicyId = BonusProvisioningPolicyMaster  
    FROM EcodeWiseBonusProvisioningPolicyMapping (NOLOCK)  
    WHERE Ecode = @Ecode  
      AND IsActive = 1  
      AND IsDeleted = 0;  
      ------------------------------------------------------------  
    -- VALIDATE: Ecode Must Have Policy Mapped  
    ------------------------------------------------------------  
    IF @PolicyId IS NULL  
    BEGIN  
        --RAISERROR('No Bonus Policy defined for the given Ecode.', 16, 1);  
        RETURN;  
    END  
  
    ------------------------------------------------------------  
    -- CONDITIONAL CLEANUP BEFORE RECALCULATION  
    -- If policy is C6B/2366  -> clear from AdditionalPaymentHold  
    -- Else                   -> reset Bonus in tblPayments  
    ------------------------------------------------------------  
    IF @PolicyId IN ('C6BDAC6D-6EC3-F011-B1EA-8C84747E00C5',  
                     '2366FC08-6EC3-F011-B1EA-8C84747E00C5')  
    BEGIN  
        -- For C6B / 2366 policies, Bonus lives in tblPayments.  
        -- So remove any old record from AdditionalPaymentHold.  
        UPDATE AdditionalPaymentHold  
        SET Bonus = 0,  
            ExGratia = 0,  
            UpdatedOn = GETDATE(),  
            UpdatedBy = @CreatedBy  
        WHERE Ecode = @Ecode  
          AND [Month] = @Month;  
    END  
    ELSE if @PolicyId IN (  
                'C4BDAC6D-6EC3-F011-B1EA-8C84747E00C5',  
                '2166FC08-6EC3-F011-B1EA-8C84747E00C5',  
                '2266FC08-6EC3-F011-B1EA-8C84747E00C5',  
                'C5BDAC6D-6EC3-F011-B1EA-8C84747E00C5'  
              )  
    BEGIN  
        -- For other policies, Bonus lives in AdditionalPaymentHold.  
        -- So clear Bonus from tblPayments for this Ecode+Month.  
        UPDATE tblPayments  
        SET Bonus = 0  
        WHERE E_CODE = @Ecode  
          AND [MONTH] = @Month;  
    END  
  
    ------------------------------------------------------------  
    -- 1) MERGE INTO AdditionalPaymentHold (4 policies)  
    ------------------------------------------------------------  
    ;WITH BonusSource AS  
    (  
        SELECT  
              emp.Ecode,  
              @Month AS [Month],  
  
              Bonus = CASE   
                  WHEN map.BonusProvisioningPolicyMaster IN (  
                           'C4BDAC6D-6EC3-F011-B1EA-8C84747E00C5',  
                           '2166FC08-6EC3-F011-B1EA-8C84747E00C5'  
                       )  
                  THEN CASE WHEN emp.BasicSalary <= 21000   
                            THEN ROUND(emp.BasicSalary * 0.0833, 2)  
                            ELSE 0 END  
  
                  WHEN map.BonusProvisioningPolicyMaster IN (  
                           '2266FC08-6EC3-F011-B1EA-8C84747E00C5',  
                           'C5BDAC6D-6EC3-F011-B1EA-8C84747E00C5'  
                       )  
                  THEN CASE WHEN emp.BasicSalary <= 21000  
                            THEN ROUND(emp.BasicSalary * 0.0833, 2)  
                            ELSE 0 END  
  
                  ELSE 0  
              END,  
  
              ExGratia = CASE   
                  WHEN map.BonusProvisioningPolicyMaster IN (  
                           'C4BDAC6D-6EC3-F011-B1EA-8C84747E00C5',  
                           '2166FC08-6EC3-F011-B1EA-8C84747E00C5'  
                       )  
                  THEN CASE WHEN emp.BasicSalary <= 21000  
                            THEN 0  
                            ELSE ROUND(emp.BasicSalary * 0.0833, 2) END  
  
                  WHEN map.BonusProvisioningPolicyMaster IN (  
                           '2266FC08-6EC3-F011-B1EA-8C84747E00C5',  
                           'C5BDAC6D-6EC3-F011-B1EA-8C84747E00C5'  
                       )  
                  THEN CASE WHEN emp.BasicSalary <= 21000  
                            THEN ROUND(emp.[GROSS SALARY] * 0.0833, 2) - ROUND(emp.BasicSalary * 0.0833, 2)  
                            ELSE ROUND(emp.[GROSS SALARY] * 0.0833, 2) END  
  
                  ELSE 0  
              END,  
  
              @CreatedBy AS CreatedBy,  
              @CreatedBy AS UpdatedBy  
  
        FROM EcodeWiseBonusProvisioningPolicyMapping map  
        LEFT JOIN tblEmployee emp ON emp.Ecode = map.Ecode  
        LEFT JOIN BonusProvisioningPolicyMaster bpm ON bpm.Id = map.BonusProvisioningPolicyMaster  
        WHERE map.IsActive = 1  
          AND map.IsDeleted = 0  
          AND bpm.IsActive = 1  
          AND bpm.IsDeleted = 0  
          AND map.BonusProvisioningPolicyMaster IN (  
                'C4BDAC6D-6EC3-F011-B1EA-8C84747E00C5',  
                '2166FC08-6EC3-F011-B1EA-8C84747E00C5',  
                '2266FC08-6EC3-F011-B1EA-8C84747E00C5',  
                'C5BDAC6D-6EC3-F011-B1EA-8C84747E00C5'  
              )  
          AND map.Ecode = @Ecode  
    )  
    MERGE AdditionalPaymentHold AS tgt  
    USING BonusSource AS src  
        ON tgt.Ecode = src.Ecode  
       AND tgt.[Month] = src.[Month]  
       AND tgt.IsDeleted = 0  
    WHEN MATCHED THEN  
        UPDATE SET  
            tgt.Bonus = src.Bonus,  
            tgt.ExGratia = src.ExGratia,  
            tgt.UpdatedBy = src.UpdatedBy,  
            tgt.UpdatedOn = GETDATE(),  
            tgt.IsActive = 1  
    WHEN NOT MATCHED BY TARGET THEN  
        INSERT (Ecode, [Month], Bonus, ExGratia, CreatedBy, CreatedOn, UpdatedBy, UpdatedOn, IsActive, IsDeleted)  
        VALUES (src.Ecode, src.[Month], src.Bonus, src.ExGratia, src.CreatedBy, GETDATE(), src.UpdatedBy, GETDATE(), 1, 0);  
  
    ------------------------------------------------------------  
    -- 2) MERGE INTO tblPayments (2 policies: C6B..., 2366...)  
    ------------------------------------------------------------  
  
    ;WITH PaymentSource AS  
    (  
        SELECT  
              emp.Ecode AS E_CODE,  
              @Month AS [MONTH],  
              Bonus = CASE WHEN emp.BasicSalary > 21000  
                           THEN ROUND(emp.[GROSS SALARY] * 0.0833, 2)  
                           ELSE 0 END  
        FROM EcodeWiseBonusProvisioningPolicyMapping map  
        LEFT JOIN tblEmployee emp ON emp.Ecode = map.Ecode  
        LEFT JOIN BonusProvisioningPolicyMaster bpm ON bpm.Id = map.BonusProvisioningPolicyMaster  
        WHERE map.IsActive = 1  
          AND map.IsDeleted = 0  
          AND bpm.IsActive = 1  
          AND bpm.IsDeleted = 0  
          AND map.BonusProvisioningPolicyMaster IN (  
                'C6BDAC6D-6EC3-F011-B1EA-8C84747E00C5',  
                '2366FC08-6EC3-F011-B1EA-8C84747E00C5'  
              )  
          AND emp.BasicSalary > 21000  
          AND map.Ecode = @Ecode  
    )  
    MERGE tblPayments AS tgt  
    USING PaymentSource AS src  
        ON tgt.E_CODE = src.E_CODE  
       AND tgt.[MONTH] = src.[MONTH]  
    WHEN MATCHED THEN  
        UPDATE SET tgt.Bonus = src.Bonus  
    WHEN NOT MATCHED BY TARGET THEN  
        INSERT (E_CODE, Incentive, ARREAR, Overtime, Fooding_Allowance, Mobile_Bill, [MONTH], Bonus)  
        VALUES (src.E_CODE, 0, 0, 0, 0, 0, src.[MONTH], src.Bonus);  
  
END;
GO

-- -----------------------------------------------------------------------------
-- dbo.usp_ProcessBonusAndPayments_MonthWise_Dev
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[usp_ProcessBonusAndPayments_MonthWise_Dev]
    @Month     NVARCHAR(20),
    @CreatedBy NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    -- Pre-load policy + employee data for all active mapped employees
    IF OBJECT_ID('tempdb..#PolicyMap') IS NOT NULL DROP TABLE #PolicyMap;
    SELECT map.Ecode, map.BonusProvisioningPolicyMaster AS PolicyId,
           emp.BasicSalary, emp.[GROSS SALARY] AS GrossSalary
    INTO #PolicyMap
    FROM EcodeWiseBonusProvisioningPolicyMapping map WITH (NOLOCK)
    INNER JOIN tblEmployee emp WITH (NOLOCK) ON emp.Ecode=map.Ecode
    INNER JOIN BonusProvisioningPolicyMaster bpm WITH (NOLOCK) ON bpm.Id=map.BonusProvisioningPolicyMaster
    WHERE map.IsActive=1 AND map.IsDeleted=0 AND bpm.IsActive=1 AND bpm.IsDeleted=0;
    CREATE INDEX IX_PolicyMap_Ecode    ON #PolicyMap (Ecode);
    CREATE INDEX IX_PolicyMap_PolicyId ON #PolicyMap (PolicyId);

    -- Cleanup: clear AdditionalPaymentHold for C6B/2366 employees
    UPDATE aph SET aph.Bonus=0, aph.ExGratia=0, aph.UpdatedOn=GETDATE(), aph.UpdatedBy=@CreatedBy
    FROM AdditionalPaymentHold aph
    INNER JOIN #PolicyMap pm ON pm.Ecode=aph.Ecode
    WHERE aph.[Month]=@Month
      AND pm.PolicyId IN ('C6BDAC6D-6EC3-F011-B1EA-8C84747E00C5','2366FC08-6EC3-F011-B1EA-8C84747E00C5');

    -- Cleanup: clear tblPayments.Bonus for C4B/2166/2266/C5B employees
    UPDATE tp SET tp.Bonus=0
    FROM tblPayments tp
    INNER JOIN #PolicyMap pm ON pm.Ecode=tp.E_CODE
    WHERE tp.[MONTH]=@Month
      AND pm.PolicyId IN (
          'C4BDAC6D-6EC3-F011-B1EA-8C84747E00C5','2166FC08-6EC3-F011-B1EA-8C84747E00C5',
          '2266FC08-6EC3-F011-B1EA-8C84747E00C5','C5BDAC6D-6EC3-F011-B1EA-8C84747E00C5');

    -- Merge AdditionalPaymentHold (C4B/2166/2266/C5B policies)
    ;WITH BonusSource AS (
        SELECT pm.Ecode, @Month AS [Month],
            CASE WHEN pm.PolicyId IN (
                     'C4BDAC6D-6EC3-F011-B1EA-8C84747E00C5','2166FC08-6EC3-F011-B1EA-8C84747E00C5',
                     '2266FC08-6EC3-F011-B1EA-8C84747E00C5','C5BDAC6D-6EC3-F011-B1EA-8C84747E00C5')
                 THEN CASE WHEN pm.BasicSalary<=21000 THEN ROUND(pm.BasicSalary*0.0833,2) ELSE 0 END
                 ELSE 0 END AS Bonus,
            CASE WHEN pm.PolicyId IN ('C4BDAC6D-6EC3-F011-B1EA-8C84747E00C5','2166FC08-6EC3-F011-B1EA-8C84747E00C5')
                 THEN CASE WHEN pm.BasicSalary<=21000 THEN 0 ELSE ROUND(pm.BasicSalary*0.0833,2) END
                 WHEN pm.PolicyId IN ('2266FC08-6EC3-F011-B1EA-8C84747E00C5','C5BDAC6D-6EC3-F011-B1EA-8C84747E00C5')
                 THEN CASE WHEN pm.BasicSalary<=21000
                           THEN ROUND(pm.GrossSalary*0.0833,2)-ROUND(pm.BasicSalary*0.0833,2)
                           ELSE ROUND(pm.GrossSalary*0.0833,2) END
                 ELSE 0 END AS ExGratia,
            @CreatedBy AS CreatedBy, @CreatedBy AS UpdatedBy
        FROM #PolicyMap pm
        WHERE pm.PolicyId IN (
            'C4BDAC6D-6EC3-F011-B1EA-8C84747E00C5','2166FC08-6EC3-F011-B1EA-8C84747E00C5',
            '2266FC08-6EC3-F011-B1EA-8C84747E00C5','C5BDAC6D-6EC3-F011-B1EA-8C84747E00C5')
    )
    MERGE AdditionalPaymentHold AS tgt
    USING BonusSource AS src ON tgt.Ecode=src.Ecode AND tgt.[Month]=src.[Month] AND tgt.IsDeleted=0
    WHEN MATCHED THEN UPDATE SET
        tgt.Bonus=src.Bonus, tgt.ExGratia=src.ExGratia,
        tgt.UpdatedBy=src.UpdatedBy, tgt.UpdatedOn=GETDATE(), tgt.IsActive=1
    WHEN NOT MATCHED BY TARGET THEN INSERT
        (Ecode,[Month],Bonus,ExGratia,CreatedBy,CreatedOn,UpdatedBy,UpdatedOn,IsActive,IsDeleted)
    VALUES (src.Ecode,src.[Month],src.Bonus,src.ExGratia,src.CreatedBy,GETDATE(),src.UpdatedBy,GETDATE(),1,0);

    -- Merge tblPayments (C6B/2366 policies)
    ;WITH PaymentSource AS (
        SELECT pm.Ecode AS E_CODE, @Month AS [MONTH],
               ROUND(pm.GrossSalary*0.0833,2) AS Bonus
        FROM #PolicyMap pm
        WHERE pm.PolicyId IN ('C6BDAC6D-6EC3-F011-B1EA-8C84747E00C5','2366FC08-6EC3-F011-B1EA-8C84747E00C5')
          AND pm.BasicSalary>21000
    )
    MERGE tblPayments AS tgt
    USING PaymentSource AS src ON tgt.E_CODE=src.E_CODE AND tgt.[MONTH]=src.[MONTH]
    WHEN MATCHED THEN UPDATE SET tgt.Bonus=src.Bonus
    WHEN NOT MATCHED BY TARGET THEN INSERT
        (E_CODE,Incentive,ARREAR,Overtime,Fooding_Allowance,Mobile_Bill,[MONTH],Bonus)
    VALUES (src.E_CODE,0,0,0,0,0,src.[MONTH],src.Bonus);

    DROP TABLE IF EXISTS #PolicyMap;
END;
GO

-- -----------------------------------------------------------------------------
-- dbo.usp_ExportEmployeeBonusGratuity
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[usp_ExportEmployeeBonusGratuity]  
(  
    @SearchTerm NVARCHAR(100) = '',  
    @Ecode NVARCHAR(20) = NULL,
    @PageNumber INT = 0,
    @PageSize INT = 0,
    @TotalEmployees INT OUTPUT,
    @CurrentPageNumber INT OUTPUT
)  
AS  
BEGIN  
    SET NOCOUNT ON;  

    -- 1) Filter and calculate basic data into a temp table
    SELECT   
        e.Ecode AS [Employee Code],  
        e.[Full Name] AS [Employee Name],  
        e.GENDER,
        e.DOB,
        e.DOJ,
        e.MOBILE,
        e.[EMAIL ADDRESS],
        d.DepartmentName,
        de.DesignationName,
        e.[FATHER'S NAME],
        e.ReportHeadEcode,
        rh.[FULL NAME] AS ReportingHeadName,
        s.ShiftName,
        l.LocationName,
        stat.StateName,
        FORMAT(GETDATE(), 'MMM-yyyy') AS [Month],  
        SUM(CASE   
                WHEN YEAR(dt) = YEAR(GETDATE())   
                 AND MONTH(dt) = MONTH(GETDATE())  
                THEN ISNULL(b.ActualBonus, 0)  
                ELSE 0  
            END) AS [Current Month Bonus],  
        SUM(ISNULL(b.ActualBonus, 0)) AS [Total Bonus],  
        SUM(CASE   
                WHEN YEAR(dt) = YEAR(GETDATE())   
                 AND MONTH(dt) = MONTH(GETDATE())  
                THEN ISNULL(b.Gratuity, 0)  
                ELSE 0  
            END) AS [Current Month Gratuity],  
        SUM(ISNULL(b.ClosingGratuity, 0)) AS [Total Gratuity]
    INTO #FinalResults
    FROM tblEmployee e  
    INNER JOIN (  
        SELECT *,  
               TRY_CONVERT(date, '01-' + [Month], 106) AS dt  
        FROM BonusAndGratutityOpening  
    ) b ON e.Ecode = b.ECode  
    INNER JOIN tblEmployee rh ON rh.Ecode = e.ReportHeadEcode
    INNER JOIN tblDepartment d ON e.DepartmentId = d.DepartmentId
    INNER JOIN tblDesignation de ON de.DesignationId = e.DesignationId
    INNER JOIN tblShiftMaster s ON s.ShiftID = e.ShiftID
    INNER JOIN tblLocation l ON e.LocationId = l.LocationId
    INNER JOIN tblState stat ON stat.StateId = l.StateId
    WHERE   
        e.IsActive = 1   
        AND e.IsDeleted = 0  
        AND (@Ecode IS NULL OR e.Ecode = @Ecode)  
        AND (@SearchTerm = '' OR   
             e.Ecode LIKE '%' + @SearchTerm + '%' OR  
             e.[Full Name] LIKE '%' + @SearchTerm + '%')  
        AND b.dt BETWEEN DATEFROMPARTS(YEAR(GETDATE()) - 1, 10, 1)    
                    AND EOMONTH(GETDATE())  
    GROUP BY   
        e.Ecode,  
        e.[Full Name],  
        e.GENDER,
        e.DOB,
        e.DOJ,
        e.MOBILE,
        e.[EMAIL ADDRESS],
        d.DepartmentName,
        de.DesignationName,
        e.[FATHER'S NAME],
        e.ReportHeadEcode,
        rh.[FULL NAME],
        s.ShiftName,
        l.LocationName,
        stat.StateName;

    -- 2) Set outputs
    SELECT @TotalEmployees = COUNT(*) FROM #FinalResults;
    SET @CurrentPageNumber = @PageNumber;

    -- 3) Return results based on pagination
    IF @PageNumber = 0 AND @PageSize = 0
    BEGIN
        SELECT * FROM #FinalResults ORDER BY [Employee Code];
    END
    ELSE
    BEGIN
        SELECT * FROM #FinalResults
        ORDER BY [Employee Code]
        OFFSET (@PageNumber - 1) * @PageSize ROWS
        FETCH NEXT @PageSize ROWS ONLY;
    END

    DROP TABLE #FinalResults;
END
GO

-- -----------------------------------------------------------------------------
-- dbo.USP_GENERATE_EMP_GRATUITY_BONUS
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[USP_GENERATE_EMP_GRATUITY_BONUS]
AS
BEGIN
    SET NOCOUNT ON;

    DROP TABLE IF EXISTS ##TEMPDATA;

    DECLARE @COL NVARCHAR(MAX) = N'',
            @SQL NVARCHAR(MAX) = N'',
            @SELECTCOLNAME NVARCHAR(MAX) = N'';

    ;WITH BASE_CTE AS
    (
        SELECT DISTINCT
            ISNULL(LOC.STCode,'') AS [Location CODE],
            ISNULL(LocationName,'') AS [LOCATION],
            ISNULL(StateName ,'') AS [STATE],
            [Employee Code],
            ISNULL([Name of Employee],'') AS [NAME],
            ISNULL(Sex ,'') AS [GENDER],
            CASE WHEN [D.O.J.] IS NULL THEN '' ELSE CONVERT(VARCHAR(10), [D.O.J.], 120) END AS [JOINING DATE],
            ISNULL([Mob No.],'') AS [MOBILE NO.],
            CASE WHEN [D.O.L.] IS NULL THEN '' ELSE CONVERT(VARCHAR(10), [D.O.L.], 120) END AS [LEAVING DATE],
            ISNULL(EMP.Department,'') AS [DEPARTMENT],
            ISNULL(EMP.Designation,'') AS [DESIGNATION],
            CASE WHEN [Is Active] = 1 THEN 'ACTIVE' ELSE 'NOT ACTIVE' END AS [STATUS]
        FROM HRMS.dbo.NEW_EmployeeViewWithExp EMP
        LEFT JOIN HRMS.[dbo].[LOCATIONMASTER] LOC
            ON EMP.STATES = LOC.STCODE
        WHERE [Is Active] = 1
          AND ([Employee Code] LIKE 'V%' OR [Employee Code] LIKE 'N%')
          AND NOT ([Employee Code] LIKE 'V2S%' AND LOC.STCode = 'DB01')
    ),
    cte_final AS
    (
        SELECT DISTINCT
            EMP.*,
            ISNULL([BONUS B/F FROM LAST MTH],'') AS [BONUS B/F FROM LAST MTH],
            ISNULL([BONUS EARNED],'') AS [BONUS EARNED],
            ISNULL([BONUS C/F FROM NEXT MTH],'') AS [BONUS C/F FROM NEXT MTH],
            ISNULL([GRATUITY B/F FROM LAST MTH],'') AS [GRATUITY B/F FROM LAST MTH],
            ISNULL([GRATUITY EARNED],'') AS [GRATUITY EARNED],
            ISNULL([GRATUITY C/F FROM NEXT MTH],'') AS [GRATUITY C/F FROM NEXT MTH],
            [MONTH]
        FROM BASE_CTE EMP
        LEFT JOIN hrms.dbo.VW_SALARY_FORMAT SAL
            ON EMP.[Employee Code] = SAL.[E.CODE]
    ),
    CTE_UNPIVOT AS
    (
        SELECT *,
               'BONUS B/F FROM LAST MTH_' + UPPER(LEFT([MONTH], 3)) + '_20' + RIGHT([MONTH], 2) AS PARTICULARS_NAME,
               ISNULL(TRY_CAST([BONUS B/F FROM LAST MTH] AS NUMERIC(18,2)),0) AS VALUE
        FROM cte_final

        UNION ALL
        SELECT *,
               'BONUS EARNED_' + UPPER(LEFT([MONTH], 3)) + '_20' + RIGHT([MONTH], 2),
               ISNULL(TRY_CAST([BONUS EARNED] AS NUMERIC(18,2)),0)
        FROM cte_final

        UNION ALL
        SELECT *,
               'BONUS C/F FROM NEXT MTH_' + UPPER(LEFT([MONTH], 3)) + '_20' + RIGHT([MONTH], 2),
               ISNULL(TRY_CAST([BONUS C/F FROM NEXT MTH] AS NUMERIC(18,2)),0)
        FROM cte_final

        UNION ALL
        SELECT *,
               'GRATUITY B/F FROM LAST MTH_' + UPPER(LEFT([MONTH], 3)) + '_20' + RIGHT([MONTH], 2),
               ISNULL(TRY_CAST([GRATUITY B/F FROM LAST MTH] AS NUMERIC(18,2)),0)
        FROM cte_final

        UNION ALL
        SELECT *,
               'GRATUITY EARNED_' + UPPER(LEFT([MONTH], 3)) + '_20' + RIGHT([MONTH], 2),
               ISNULL(TRY_CAST([GRATUITY EARNED] AS NUMERIC(18,2)),0)
        FROM cte_final

        UNION ALL
        SELECT *,
               'GRATUITY C/F FROM NEXT MTH_' + UPPER(LEFT([MONTH], 3)) + '_20' + RIGHT([MONTH], 2),
               ISNULL(TRY_CAST([GRATUITY C/F FROM NEXT MTH] AS NUMERIC(18,2)),0)
        FROM cte_final
    )
    SELECT *
    INTO ##TEMPDATA
    FROM CTE_UNPIVOT;

    /* ===== Build column list safely as NVARCHAR(MAX) ===== */

    SELECT @COL =
        STRING_AGG(
            CAST('ISNULL(' + QUOTENAME(PARTICULARS_NAME) + ', 0) AS ' + QUOTENAME(PARTICULARS_NAME) AS NVARCHAR(MAX)),
            N', '
        )
    WITHIN GROUP
    (
        ORDER BY
            TRY_CAST('01-' + REPLACE(RIGHT(PARTICULARS_NAME, 8), '_', '-') AS DATE),
            CASE
                WHEN PARTICULARS_NAME LIKE 'BONUS B/F FROM LAST MTH%' THEN 1
                WHEN PARTICULARS_NAME LIKE 'BONUS EARNED%' THEN 2
                WHEN PARTICULARS_NAME LIKE 'BONUS C/F FROM NEXT MTH%' THEN 3
                WHEN PARTICULARS_NAME LIKE 'GRATUITY B/F FROM LAST MTH%' THEN 4
                WHEN PARTICULARS_NAME LIKE 'GRATUITY EARNED%' THEN 5
                WHEN PARTICULARS_NAME LIKE 'GRATUITY C/F FROM NEXT MTH%' THEN 6
                ELSE 99
            END
    )
    FROM (SELECT DISTINCT PARTICULARS_NAME FROM ##TEMPDATA) AS OrderedCols;

    SELECT @SELECTCOLNAME =
        STRING_AGG(
            CAST(QUOTENAME(PARTICULARS_NAME) AS NVARCHAR(MAX)),
            N', '
        )
    WITHIN GROUP
    (
        ORDER BY
            TRY_CAST('01-' + REPLACE(RIGHT(PARTICULARS_NAME, 8), '_', '-') AS DATE),
            CASE
                WHEN PARTICULARS_NAME LIKE 'BONUS B/F FROM LAST MTH%' THEN 1
                WHEN PARTICULARS_NAME LIKE 'BONUS EARNED%' THEN 2
                WHEN PARTICULARS_NAME LIKE 'BONUS C/F FROM NEXT MTH%' THEN 3
                WHEN PARTICULARS_NAME LIKE 'GRATUITY B/F FROM LAST MTH%' THEN 4
                WHEN PARTICULARS_NAME LIKE 'GRATUITY EARNED%' THEN 5
                WHEN PARTICULARS_NAME LIKE 'GRATUITY C/F FROM NEXT MTH%' THEN 6
                ELSE 99
            END
    )
    FROM (SELECT DISTINCT PARTICULARS_NAME FROM ##TEMPDATA) AS OrderedCols;

    /* ===== Dynamic Pivot ===== */

    SET @SQL = N'
SELECT
    ROW_NUMBER() OVER (ORDER BY [Location CODE], [Employee Code]) AS [S.No],
    [Location CODE], [LOCATION], [STATE], [Employee Code], [NAME],
    [GENDER], [JOINING DATE], [MOBILE NO.], [LEAVING DATE],
    [DEPARTMENT], [DESIGNATION], [STATUS], ' + @COL + N'
FROM
(
    SELECT
        [Location CODE], [LOCATION], [STATE], [Employee Code], [NAME],
        [GENDER], [JOINING DATE], [MOBILE NO.], [LEAVING DATE],
        [DEPARTMENT], [DESIGNATION], [STATUS],
        PARTICULARS_NAME, ISNULL(VALUE, 0) AS VALUE
    FROM ##TEMPDATA
) AS src
PIVOT
(
    SUM(VALUE) FOR PARTICULARS_NAME IN (' + @SELECTCOLNAME + N')
) AS pvt;';

    EXEC sp_executesql @SQL;
END
GO

-- -----------------------------------------------------------------------------
-- dbo.usp_GetEmployeeFinalBonus
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.usp_GetEmployeeFinalBonus
(
    @Ecode NVARCHAR(20)
)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE
        @PolicyId NVARCHAR(50),
        @JoiningDate DATE,
        @StartMonth DATE,
        @EndMonth DATE,
        @FinalBonus DECIMAL(18,2) = 0,
        @BonusStartMonth NVARCHAR(10) = NULL,
        @BonusEndMonth NVARCHAR(10) = NULL,
        @Remarks NVARCHAR(200) = NULL;

    /* ================= POLICY ================= */
    SELECT @PolicyId = BonusProvisioningPolicyMaster
    FROM EcodeWiseBonusProvisioningPolicyMapping
    WHERE Ecode = @Ecode
      AND IsActive = 1
      AND IsDeleted = 0;

    IF @PolicyId IS NULL
    BEGIN
        SELECT
            @Ecode AS Ecode,
            NULL AS BonusStartMonth,
            NULL AS BonusEndMonth,
            0 AS FinalBonus,
            'No Policy Defined' AS Remarks;
        RETURN;
    END

    /* ================= JOINING DATE ================= */
    SELECT @JoiningDate = DOJ
    FROM tblEmployee
    WHERE Ecode = @Ecode;

    /* ================= LAST PUNCH MONTH ================= */
    SELECT
        @EndMonth = DATEFROMPARTS(
                        YEAR(MAX(PunchDate)),
                        MONTH(MAX(PunchDate)),
                        1
                    )
    FROM tblEmployeeMultiPunches
    WHERE UserID = @Ecode
      AND (
            CAST(PARSENAME(TotalHours,2) AS INT) * 60 +
            CAST(PARSENAME(TotalHours,1) AS INT)
          ) >= 270;

    IF @EndMonth IS NULL
    BEGIN
        SELECT
            @Ecode AS Ecode,
            NULL AS BonusStartMonth,
            NULL AS BonusEndMonth,
            0 AS FinalBonus,
            'No valid punch data' AS Remarks;
        RETURN;
    END

    /* ================= START MONTH (LAST OCT LOGIC) ================= */
    IF MONTH(@EndMonth) >= 10
        SET @StartMonth = DATEFROMPARTS(YEAR(@EndMonth), 10, 1);
    ELSE
        SET @StartMonth = DATEFROMPARTS(YEAR(@EndMonth) - 1, 10, 1);

    /* ================= BONUS CALCULATION ================= */
    ;WITH MonthRange AS
    (
        SELECT @StartMonth AS M
        UNION ALL
        SELECT DATEADD(MONTH, 1, M)
        FROM MonthRange
        WHERE M < @EndMonth
    )
    SELECT
        @FinalBonus = SUM(
            CASE
                WHEN @PolicyId = '2166FC08-6EC3-F011-B1EA-8C84747E00C5'
                     AND ISNULL(a.TOTAL_PRESENT,0) < 30 THEN 0

                WHEN @PolicyId = '2266FC08-6EC3-F011-B1EA-8C84747E00C5'
                     AND ISNULL(a.TOTAL_PRESENT,0) = 0 THEN 0

                WHEN sal.[BasicSalary(Bud.)] <= 21000
                    THEN sal.[BasicSalary(Actual)] * 0.0833

                ELSE
                    sal.[Monthly Gross CTC(Actual After Deduction AND AddONS)] * 0.0833
            END
        )
    FROM MonthRange m
    LEFT JOIN EmpAttendanceMaster a
        ON a.E_CODE = @Ecode
       AND a.[MONTH] = FORMAT(m.M, 'MMM-yy')
    LEFT JOIN vw_Emp_Attendance_Format sal
        ON sal.Ecode = @Ecode
       AND sal.[Month-Year] = FORMAT(m.M, 'MMM-yy')
    OPTION (MAXRECURSION 0);

    SET @BonusStartMonth = FORMAT(@StartMonth, 'MMM-yy');
    SET @BonusEndMonth   = FORMAT(@EndMonth, 'MMM-yy');

    /* ================= FINAL OUTPUT ================= */
    SELECT
        @Ecode AS Ecode,
        @BonusStartMonth AS BonusStartMonth,
        @BonusEndMonth AS BonusEndMonth,
        ISNULL(@FinalBonus, 0) AS FinalBonus,
        NULL AS Remarks;
END;
GO

-- -----------------------------------------------------------------------------
-- dbo.usp_GetEmployeeBonus
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.usp_GetEmployeeBonus
(
    @Month NVARCHAR(20),   -- e.g. 'Nov-2025'
    @Ecode NVARCHAR(20)
)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @PolicyId NVARCHAR(50);

    ------------------------------------------------------------
    -- Get Active Policy for Ecode
    ------------------------------------------------------------
    SELECT @PolicyId = BonusProvisioningPolicyMaster
    FROM EcodeWiseBonusProvisioningPolicyMapping WITH (NOLOCK)
    WHERE Ecode = @Ecode
      AND IsActive = 1
      AND IsDeleted = 0;

    IF @PolicyId IS NULL
    BEGIN
        SELECT 
            @Ecode AS Ecode,
            @Month AS [Month],
            0 AS Bonus,
            0 AS ExGratia,
            'No Policy Mapped' AS Remarks;
        RETURN;
    END

    ------------------------------------------------------------
    -- Calculate Bonus / ExGratia
    ------------------------------------------------------------
    SELECT
        emp.Ecode,
        @Month AS [Month],
        Bonus =
            CASE
                -- Policies where bonus based on Basic <= 21000
                WHEN @PolicyId IN (
                        'C4BDAC6D-6EC3-F011-B1EA-8C84747E00C5',
                        '2166FC08-6EC3-F011-B1EA-8C84747E00C5',
                        '2266FC08-6EC3-F011-B1EA-8C84747E00C5',
                        'C5BDAC6D-6EC3-F011-B1EA-8C84747E00C5'
                     )
                THEN
                    CASE
                        WHEN emp.BasicSalary <= 21000
                            THEN ROUND(emp.BasicSalary * 0.0833, 2)
                        ELSE 0
                    END

                -- C6B / 2366 policies
                WHEN @PolicyId IN (
                        'C6BDAC6D-6EC3-F011-B1EA-8C84747E00C5',
                        '2366FC08-6EC3-F011-B1EA-8C84747E00C5'
                     )
                THEN
                    CASE
                        WHEN emp.BasicSalary > 21000
                            THEN ROUND(emp.[GROSS SALARY] * 0.0833, 2)
                        ELSE 0
                    END
                ELSE 0
            END,

        ExGratia =
            CASE
                WHEN @PolicyId IN (
                        'C4BDAC6D-6EC3-F011-B1EA-8C84747E00C5',
                        '2166FC08-6EC3-F011-B1EA-8C84747E00C5'
                     )
                THEN
                    CASE
                        WHEN emp.BasicSalary > 21000
                            THEN ROUND(emp.BasicSalary * 0.0833, 2)
                        ELSE 0
                    END

                WHEN @PolicyId IN (
                        '2266FC08-6EC3-F011-B1EA-8C84747E00C5',
                        'C5BDAC6D-6EC3-F011-B1EA-8C84747E00C5'
                     )
                THEN
                    CASE
                        WHEN emp.BasicSalary <= 21000
                            THEN ROUND(emp.[GROSS SALARY] * 0.0833, 2)
                                 - ROUND(emp.BasicSalary * 0.0833, 2)
                        ELSE ROUND(emp.[GROSS SALARY] * 0.0833, 2)
                    END
                ELSE 0
            END,

        @PolicyId AS PolicyId
    FROM tblEmployee emp
    WHERE emp.Ecode = @Ecode;
END
GO

-- -----------------------------------------------------------------------------
-- dbo.GETEMPBONUSLIST
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE GETEMPBONUSLIST  
AS  
BEGIN  
 select B.E_Code,E.FirstName + ' ' + E.LastName AS FullName,B.Date AS BonusDate,B.Amount,B.Acc_Number,B.UTR from tblBonus_Upload B  
 left join tblEmployee E on B.E_Code=E.Ecode  
END
GO

-- -----------------------------------------------------------------------------
-- dbo.usp_ProcessRetentionBonus
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.usp_ProcessRetentionBonus
(
      @ECode      VARCHAR(20)       -- e.g. 'E001'
    , @MonthToken VARCHAR(7)        -- format MMM-YY, e.g. 'Jan-25'
)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ProcessMonth DATE;
    DECLARE @MonthStr     VARCHAR(11);

    -- Convert 'Jan-25' -> '01 Jan 25' (dd mon yy) then to DATE
    SET @MonthStr = '01 ' + REPLACE(@MonthToken, '-', ' ');  -- '01 Jan 25'
    SET @ProcessMonth = TRY_CONVERT(DATE, @MonthStr, 6);     -- style 6 = dd mon yy

    IF @ProcessMonth IS NULL
    BEGIN
        RAISERROR('Invalid Month format. Expected MMM-YY, e.g. Jan-25.', 16, 1);
        RETURN;
    END;

    ;WITH RB AS
    (
        SELECT
              rb.ECode
            , rb.BonusAmount
            , rb.RetentionStart
            , rb.RetentionEnd
            , TotalMonths =
                DATEDIFF(
                    MONTH,
                    DATEFROMPARTS(YEAR(rb.RetentionStart), MONTH(rb.RetentionStart), 1),
                    DATEFROMPARTS(YEAR(rb.RetentionEnd),   MONTH(rb.RetentionEnd),   1)
                ) + 1
        FROM dbo.tblRetentionBonus rb
        WHERE rb.Accepted = 1
          AND rb.IsActive = 1
          AND rb.IsDeleted = 0
          AND rb.ECode = @ECode
          AND @ProcessMonth BETWEEN 
                DATEFROMPARTS(YEAR(rb.RetentionStart), MONTH(rb.RetentionStart), 1)
            AND DATEFROMPARTS(YEAR(rb.RetentionEnd),   MONTH(rb.RetentionEnd),   1)
    ),
    FinalRB AS
    (
        -- If multiple retention letters overlap, sum their monthly bonus
        SELECT
              ECode
            , @MonthToken AS MonthToken
            , SUM(CAST(BonusAmount / NULLIF(TotalMonths, 0) AS DECIMAL(18,2))) AS MonthlyRetentionBonus
        FROM RB
        GROUP BY ECode
    )
    MERGE dbo.AdditionalPaymentHold AS T
    USING FinalRB AS S
       ON  T.Ecode = S.ECode
       AND T.[Month] = S.MonthToken        -- Month stored as MMM-YY
    WHEN MATCHED THEN
        UPDATE SET
              T.RetentionBonus = S.MonthlyRetentionBonus
            , T.UpdatedOn      = GETDATE()
            , T.UpdatedBy      = 'RetentionBonus_Auto'
    WHEN NOT MATCHED BY TARGET THEN
        INSERT (
              Ecode
            , [Month]
            , Bonus
            , ExGratia
            , CreatedBy
            , CreatedOn
            , UpdatedBy
            , UpdatedOn
            , IsActive
            , IsDeleted
            , GratuityMonthlyProvision
            , RetentionBonus
        )
        VALUES (
              S.ECode
            , S.MonthToken
            , 0                        -- Bonus
            , 0                        -- ExGratia
            , 'RetentionBonus_Auto'
            , GETDATE()
            , NULL
            , NULL
            , 1                        -- IsActive
            , 0                        -- IsDeleted
            , 0                        -- GratuityMonthlyProvision
            , S.MonthlyRetentionBonus
        );
END;
GO

-- -----------------------------------------------------------------------------
-- dbo.vw_Bonus_Gratuity
-- -----------------------------------------------------------------------------

/****** Object:  View [dbo].[vw_Bonus_Gratuity]    Script Date: 08-07-2025 15:39:54 ******/
--SET ANSI_NULLS ON
--GO

--SET QUOTED_IDENTIFIER ON
--GO
--Select BonusApplicable from tblEmployee where Ecode='V18426'
--Truncate table BonusAndGratutityOpening
--Select * from [vw_Bonus_Gratuity]
CREATE OR ALTER VIEW [dbo].[vw_Bonus_Gratuity] as
Select a.ECode,a.Month,Gratuity,Bonus,b.[BasicSalary(Bud.)],b.[BasicSalary(Actual)],
CASE 
    WHEN c.DOJ IS NULL OR c.DOJ > GETDATE() THEN 0
    ELSE 
        dbo.fn_GetMonthPortion(c.DOJ, c.DateOfLeft, a.Month)
END AS [Months],
c.DOJ,
ISNULL(try_cast(c.DateOfLeft as nvarchar(50)),'') DateOfLeft,
b.[Monthly Gross CTC(Actual)] as 'Gross(with Reimbursement)',

ActualGratuity,ActualBonus,case when ISNULL(BonusApplicable,0)=0 then 'Not Applicable' else 'Applicable' end 'IsBonusApplicable'
from BonusAndGratutityOpening a (NOLOCK) 
Left Join vw_Emp_Attendance_Format b (Nolock) on a.ECode=b.Ecode
Left Join tblEmployee c (NOLOCK) on a.ECode=c.Ecode
--where ECode = 'v00025'

--Select [BasicSalary(Actual)] from vw_Emp_Attendance_Format where Ecode='v00025'
--Select Doj from tblEmployee where Ecode = 'v00025'
--GO
GO


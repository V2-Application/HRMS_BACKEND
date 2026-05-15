-- =============================================================================
-- Category: BulkInactivate
-- Source:   dev DB (192.168.151.27\KARMA / HRMS)
-- Generated: 2026-05-14 12:15:06
-- Objects rewritten as CREATE OR ALTER for safe re-run on production.
-- =============================================================================
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- -----------------------------------------------------------------------------
-- dbo.sp_GetEmployeeEffectiveLeavingDate
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_GetEmployeeEffectiveLeavingDate
    @EmployeeId BIGINT  
AS  
BEGIN  
    SET NOCOUNT ON;  
  
    SELECT   
        COALESCE(  
            p.LastValidPunchDate,  
            TRY_CONVERT(DATETIME2(0), e.[DateOfLeft])  
        ) AS EffectiveDateOfLeaving  
    FROM dbo.tblEmployee e  
    OUTER APPLY    
    (    
        SELECT MAX(x.AttendanceDate) AS LastValidPunchDate    
        FROM dbo.tbl_fn_GetMonthlyPunchesRange_productionnewnick_test x    
        WHERE x.ECode = e.Ecode    
          AND TRY_CAST(x.TotalWorkingMinutes AS time) >= '04:30'  
    ) p  
    WHERE e.EmployeeId = @EmployeeId;  
END
GO


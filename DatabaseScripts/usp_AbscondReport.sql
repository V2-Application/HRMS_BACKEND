CREATE   PROCEDURE [dbo].[usp_AbscondReport] 
(     
@PageNumber INT = 0,     
@PageSize INT = 0,    
@SearchTerm NVARCHAR(100) = '',
@ManagerId NVARCHAR(10) = '',    
@TotalEmployees INT OUTPUT,    
@CurrentPageNumber INT OUTPUT,    
@FromDate DATE = NULL,  
@ToDate DATE = NULL 
) 
AS
BEGIN  
SET NOCOUNT ON; 
IF @FromDate IS NULL 
SET @FromDate = DATEADD(DAY, -30, CONVERT(date, GETDATE())); 
IF @ToDate   IS NULL SET @ToDate   = CONVERT(date, GETDATE()); 

DECLARE @MECode NVARCHAR(10) = NULL; 
DECLARE @IsStore BIT = NULL;    
DECLARE @ApplyFilter BIT = 0; 
IF NULLIF(@ManagerId, '') IS NOT NULL

BEGIN         

SELECT           
@MECode = Ecode, 
@IsStore = IsStore  
FROM dbo.tblEmployee WITH (NOLOCK)    

WHERE EmployeeId = TRY_CONVERT(BIGINT, @ManagerId); 

IF (ISNULL(@MECode, '') <> '' AND @IsStore IS NOT NULL)      
SET @ApplyFilter = 1;     END;   

------------------------------------------------------------   
-- 1) Weekoff policy    
------------------------------------------------------------   

CREATE TABLE #WeekoffPolicy     
(         
LocationCode NVARCHAR(50) NOT NULL,         
SatCount NVARCHAR(20) NULL,         
AllowedSaturdays INT NULL,         
AllowedSundays INT NULL,         
CONSTRAINT PK_WeekoffPolicy PRIMARY KEY CLUSTERED (LocationCode)
);      

;WITH WP AS     
(         
SELECT             
LocationCode,             
SatCount =                 
CASE                     
WHEN SUM(CASE WHEN SatCount = '0'   THEN 1 ELSE 0 END) > 0 THEN '0'                     
WHEN SUM(CASE WHEN SatCount = 'All' THEN 1 ELSE 0 END) > 0 THEN 'All'                     
WHEN MAX(TRY_CONVERT(int, SatCount)) IS NOT NULL THEN CONVERT(nvarchar(20), 
MAX(TRY_CONVERT(int, SatCount)))                     
ELSE MAX(SatCount)                 
END,             
AllowedSundays = COUNT(*)         
FROM dbo.BudgetedWeekOffPolicyMaster WITH (NOLOCK)         
WHERE LocationCode IS NOT NULL         
GROUP BY LocationCode     
)     
INSERT INTO #WeekoffPolicy 
(LocationCode, SatCount,AllowedSaturdays, AllowedSundays)
SELECT         
LocationCode,         
SatCount,         
CASE             
WHEN SatCount = 'All' THEN 999             
WHEN SatCount = '0' THEN 0             
ELSE TRY_CAST(SatCount AS INT)         
END AS AllowedSaturdays,         
AllowedSundays     
FROM WP;      
------------------------------------------------------------     
-- 2) Employee details     
------------------------------------------------------------     
CREATE TABLE #EmployeeDetails     
(         
EmployeeId BIGINT,         
Ecode NVARCHAR(50) NOT NULL,         
FullName NVARCHAR(100),         
DepartmentId INT,         
DesignationId INT,         
DepartmentName NVARCHAR(100),         
DesignationName NVARCHAR(100),         
LocationId INT,         
LocationName NVARCHAR(100),         
STCode NVARCHAR(50),        
ZoneName NVARCHAR(50),      
RegionName NVARCHAR(50),      
ClusterName NVARCHAR(50),      
ReportHeadEcode NVARCHAR(50), 
ReportHeadFullName NVARCHAR(100),       
MOBILE NVARCHAR(50),        
EMAIL_ADDRESS NVARCHAR(200),    
DOJ DATE,         
IsActive BIT,       
IsDeleted BIT,       
IsStore BIT,      
LastPunchDate DATE NULL, 
LastPunchStatus NVARCHAR(50) NULL,  
ConsecutiveAbsentDays INT NOT NULL CONSTRAINT DF_Ed_ConsecutiveAbsentDays DEFAULT(0), 
AbscondStatus NVARCHAR(20) NOT NULL CONSTRAINT DF_Ed_AbscondStatus DEFAULT('Active'), 
AbscondDate DATE NULL,  
ExpectedReturnDate DATE NULL,
HasRegularization BIT NOT NULL CONSTRAINT DF_Ed_HasRegularization DEFAULT(0),
RegularizationCount INT NOT NULL CONSTRAINT DF_Ed_RegularizationCount DEFAULT(0),  
CONSTRAINT PK_EmployeeDetails PRIMARY KEY CLUSTERED (Ecode)
);   
CREATE INDEX IX_Ed_STCode ON #EmployeeDetails(STCode);  
INSERT INTO #EmployeeDetails  
(         
EmployeeId, Ecode, FullName, DepartmentId, DesignationId, DepartmentName, DesignationName,        
LocationId, LocationName, STCode, ZoneName, RegionName, ClusterName,      
ReportHeadEcode, ReportHeadFullName, MOBILE, EMAIL_ADDRESS, DOJ,     
IsActive, IsDeleted, IsStore  
)     
SELECT     
e.EmployeeId,
e.Ecode,      
e.[FULL NAME] AS FullName,   
e.DepartmentId,      
e.DesignationId,   
d.DepartmentName,   
dg.DesignationName,    
e.LocationId,        
l.LocationName,      
l.STCode,        
CASE WHEN l.STCode='RH01' THEN ISNULL(eZone.Zone,'-') ELSE zone.ZoneName END AS ZoneName,  
CASE WHEN l.STCode='RH01' THEN ISNULL(eZone.Region,'-') ELSE reg.RegionName END AS RegionName,  
CASE WHEN l.STCode='RH01' THEN ISNULL(eZone.Cluster,'-') ELSE cl.ClusterName END AS ClusterName,
e.ReportHeadEcode,       
report.[FULL NAME] AS ReportHeadFullName,       
e.MOBILE, 
e.[EMAIL ADDRESS],     
e.DOJ, 
e.IsActive,      
e.IsDeleted,     
e.IsStore   
FROM dbo.tblEmployee e WITH (NOLOCK)  
LEFT JOIN dbo.tblDepartment d WITH (NOLOCK) ON d.DepartmentId = e.DepartmentId     
LEFT JOIN dbo.tblDesignation dg WITH (NOLOCK) ON dg.DesignationId = e.DesignationId   
LEFT JOIN dbo.tblLocation l WITH (NOLOCK) ON l.LocationId = e.LocationId  
LEFT JOIN dbo.tblRegion reg WITH (NOLOCK) ON l.RegionId = reg.RegionId
LEFT JOIN dbo.tblZone zone WITH (NOLOCK) ON l.ZoneId = zone.Id  
LEFT JOIN dbo.Cluster cl WITH (NOLOCK) ON l.ClusterId = cl.Id    
LEFT JOIN     
(       
SELECT Ecode, MAX(Zone) AS Zone, MAX(Region) AS Region, MAX(Cluster) AS Cluster  
FROM dbo.EcodeZoneRegionClusterMapping WITH (NOLOCK)  
GROUP BY Ecode    
) eZone ON eZone.Ecode = e.Ecode 
LEFT JOIN dbo.tblEmployee report WITH (NOLOCK) ON e.ReportHeadEcode = report.Ecode  
WHERE  
e.IsDeleted = 0 
AND e.IsStore = 0     
AND e.IsActive = 1      
AND (             
@ApplyFilter = 0       
OR (@IsStore = 1 AND l.STCode = @MECode)     
OR (@IsStore = 0 AND e.ReportHeadEcode = @MECode)    
)       
AND (   
@SearchTerm = ''    
OR e.[FULL NAME] LIKE '%' + @SearchTerm + '%'    
OR e.Ecode = @SearchTerm           
OR d.DepartmentName LIKE '%' + @SearchTerm + '%'         
OR dg.DesignationName LIKE '%' + @SearchTerm + '%'     
OR l.LocationName LIKE '%' + @SearchTerm + '%'     
);    
------------------------------------------------------------  
-- 3) Attendance data (ALL days)     
------------------------------------------------------------   
CREATE TABLE #AttendanceData     (       
Ecode NVARCHAR(50) NOT NULL,       
PunchDate DATE NOT NULL,       
PunchStatus NVARCHAR(50) NULL,   
TotalWorkingMinutes NVARCHAR(20) NULL, 
IsHoliday BIT NOT NULL,     
IsRegularized BIT NOT NULL,   
CONSTRAINT PK_AttendanceData PRIMARY KEY CLUSTERED (Ecode, PunchDate)
);     
INSERT INTO #AttendanceData (Ecode, PunchDate, PunchStatus, TotalWorkingMinutes, IsHoliday, IsRegularized)
SELECT    
ar.ECode,    
CAST(ar.AttendanceDate AS DATE) AS PunchDate,
ar.Status AS PunchStatus,      
LTRIM(RTRIM(ar.TotalWorkingMinutes)) AS TotalWorkingMinutes,  
CASE WHEN ar.IsHoliday = 1 THEN 1 ELSE 0 END AS IsHoliday, 
CASE WHEN ar.IsRegularize = 1 THEN 1 ELSE 0 END AS IsRegularized 
FROM dbo.tbl_fn_GetMonthlyPunchesRange_productionnewnick_test AS ar WITH (NOLOCK) 
JOIN #EmployeeDetails ed ON ed.Ecode = ar.ECode

     WHERE CAST(ar.AttendanceDate AS DATE) BETWEEN @FromDate AND @ToDate;  
------------------------------------------------------------     
-- 4) Last punch (any day with work minutes) + Regularization    
------------------------------------------------------------  
;WITH LastPunch AS     
(       
SELECT       
Ecode,         
PunchDate,     
PunchStatus,    
ROW_NUMBER() OVER (PARTITION BY Ecode ORDER BY PunchDate DESC) AS rn  
FROM #AttendanceData       
WHERE NULLIF(TotalWorkingMinutes, '') IS NOT NULL 
AND TotalWorkingMinutes <> '00:00'   )  
UPDATE ed   
SET      
ed.LastPunchDate = lp.PunchDate,
ed.LastPunchStatus = lp.PunchStatus    
FROM #EmployeeDetails ed   
JOIN LastPunch lp ON lp.Ecode = ed.Ecode AND lp.rn = 1;   
DECLARE @RegFrom DATE = DATEADD(DAY, -30, CONVERT(date, GETDATE())); 
DECLARE @RegTo   DATE = CONVERT(date, GETDATE());  
;WITH RegAgg AS  
(    
SELECT       
Ecode, 
COUNT(*) AS RegularizationCount    
FROM #AttendanceData  
WHERE IsRegularized = 1     
AND PunchDate BETWEEN @RegFrom AND @RegTo 
GROUP BY Ecode 
)   
UPDATE ed   
SET      
ed.RegularizationCount = ISNULL(r.RegularizationCount, 0),  
ed.HasRegularization = CASE WHEN ISNULL(r.RegularizationCount, 0) > 0 THEN 1 ELSE 0 END  
FROM #EmployeeDetails ed   
LEFT JOIN RegAgg r ON r.Ecode = ed.Ecode; 
------------------------------------------------------------  
-- 5) Date series     
------------------------------------------------------------   
CREATE TABLE #DateSeries
(         
DateValue DATE NOT NULL PRIMARY KEY  
);    
;WITH DateCTE AS  
(     
SELECT @FromDate AS DateValue       
UNION ALL        
SELECT DATEADD(DAY, 1, DateValue)        
FROM DateCTE    
WHERE DateValue < @ToDate    
)    
INSERT INTO #DateSeries (DateValue)  
SELECT DateValue  
FROM DateCTE   
OPTION (MAXRECURSION 0); 
------------------------------------------------------------
-- 6) Working-day / absent flags   
------------------------------------------------------------    
CREATE TABLE #DailyFlags   
(        
Ecode NVARCHAR(50) NOT NULL,        
DateValue DATE NOT NULL,    
IsWorkingDay BIT NOT NULL,   
IsAbsentWorkingDay BIT NOT NULL,  
CONSTRAINT PK_DailyFlags PRIMARY KEY CLUSTERED (Ecode, DateValue)   
);      
INSERT INTO #DailyFlags (Ecode, DateValue, IsWorkingDay, IsAbsentWorkingDay)  
SELECT  
ed.Ecode,  
ds.DateValue,  
CAST(          
CASE      
WHEN ds.DateValue < ed.DOJ THEN 0   -- before joining 
WHEN DATENAME(WEEKDAY, ds.DateValue) = 'Sunday' THEN 0        
WHEN DATENAME(WEEKDAY, ds.DateValue) = 'Saturday' AND ISNULL(wp.SatCount,'') = '0' THEN 0   
WHEN ad.IsHoliday = 1 THEN 0         
ELSE 1       
END      
AS bit) AS IsWorkingDay,    
CAST(
CASE          
WHEN ds.DateValue < ed.DOJ THEN 0  
WHEN DATENAME(WEEKDAY, ds.DateValue) = 'Sunday' THEN 0  
WHEN DATENAME(WEEKDAY, ds.DateValue) = 'Saturday' AND ISNULL(wp.SatCount,'') = '0' THEN 0   
WHEN ad.IsHoliday = 1 THEN 0    
ELSE        
CASE                         -- working day: absent if no row OR TotalWorkingMinutes = '00:00'       
WHEN ad.PunchDate IS NULL 
OR ad.TotalWorkingMinutes IS NULL    
OR ad.TotalWorkingMinutes = '00:00' 
THEN 1                      
 ELSE 0          
 END          
 END       
 AS bit) AS IsAbsentWorkingDay   
 FROM #EmployeeDetails ed   
 CROSS JOIN #DateSeries ds    
 LEFT JOIN #AttendanceData ad  
 ON ad.Ecode = ed.Ecode 
 AND ad.PunchDate =ds.DateValue   
 LEFT JOIN #WeekoffPolicy wp  
 ON wp.LocationCode = ed.STCode 
 WHERE ds.DateValue <= @ToDate;   
 ------------------------------------------------------------  
 -- 7) Current consecutive absent WORKING days (ending at @ToDate)
     ------------------------------------------------------------  
;WITH WorkDays AS   
(       
SELECT        
Ecode,  
DateValue,     
IsAbsentWorkingDay,
ROW_NUMBER() OVER (PARTITION BY Ecode ORDER BY DateValue DESC) AS rn 
FROM #DailyFlags     
WHERE IsWorkingDay = 1  
AND DateValue <= @ToDate   
),    
BreakPoint AS  
(      
SELECT        
Ecode,          
MIN(rn) AS BreakRn 
FROM WorkDays   
WHERE IsAbsentWorkingDay = 0 
        GROUP BY Ecode   
        ),    
        CurrentStreak AS 
        (        
        SELECT      
        w.Ecode,  
        COUNT(*) AS CurrentAbsentDays       
        FROM WorkDays w       
        LEFT JOIN BreakPoint b ON b.Ecode = w.Ecode    
        WHERE w.IsAbsentWorkingDay = 1      
     AND w.rn < ISNULL(b.BreakRn, 999999) 
     GROUP BY w.Ecode    
     )    
     UPDATE ed 
     SET ed.ConsecutiveAbsentDays = ISNULL(cs.CurrentAbsentDays, 0) 
     FROM #EmployeeDetails ed  
     LEFT JOIN CurrentStreak cs ON cs.Ecode = ed.Ecode; 
------------------------------------------------------------  
-- 8) Abscond flag + dates     
--    Conditions:     
--    - 5+ consecutive absent working days 
--    - last punch (any day, even weekly off) is at least 5 days before @ToDate  
------------------------------------------------------------  
UPDATE 
ed    
SET    
ed.AbscondStatus = 'Abscond', 
ed.AbscondDate = DATEADD(DAY, -(ed.ConsecutiveAbsentDays - 1), @ToDate),
ed.ExpectedReturnDate = DATEADD(DAY, 1, @ToDate)  
FROM #EmployeeDetails ed   
WHERE ed.ConsecutiveAbsentDays >= 5      
AND ed.LastPunchDate IS NOT NULL    
AND DATEDIFF(DAY, ed.LastPunchDate, @ToDate) >= 5;     
DELETE FROM #EmployeeDetails    
WHERE AbscondStatus <> 'Abscond';    
------------------------------------------------------------ 
-- 9) Output    
------------------------------------------------------------  
SELECT @TotalEmployees = COUNT(*) FROM #EmployeeDetails;    
SET @CurrentPageNumber = @PageNumber;  
IF @PageNumber > 0 AND @PageSize > 0   
BEGIN         
SELECT        
EmployeeId, Ecode, FullName, DepartmentId, DesignationId, DepartmentName, DesignationName,  
LocationId, LocationName, STCode, ZoneName, RegionName, ClusterName,       
ReportHeadEcode, ReportHeadFullName, MOBILE, EMAIL_ADDRESS, DOJ,        
LastPunchDate, LastPunchStatus, ConsecutiveAbsentDays, AbscondStatus, AbscondDate, ExpectedReturnDate,  
HasRegularization, RegularizationCount, IsActive,    
CASE WHEN HasRegularization =1 THEN 'Regularization Applied' ELSE 'No Regularization' END AS
RegularizationStatus 
FROM #EmployeeDetails     
ORDER BY AbscondDate DESC, ConsecutiveAbsentDays DESC     
OFFSET (@PageNumber - 1) * @PageSize ROWS  
FETCH NEXT @PageSize ROWS ONLY;   
END   
ELSE     
BEGIN    
SELECT      
EmployeeId, Ecode, FullName, DepartmentId, DesignationId, DepartmentName, DesignationName,
LocationId, LocationName, STCode, ZoneName, RegionName, ClusterName,     
ReportHeadEcode, ReportHeadFullName, MOBILE, EMAIL_ADDRESS, DOJ,     
LastPunchDate, LastPunchStatus, ConsecutiveAbsentDays, AbscondStatus, AbscondDate, ExpectedReturnDate, 
HasRegularization, RegularizationCount, IsActive,  
CASE 
WHEN HasRegularization = 1 THEN 'Regularization Applied' ELSE 'No Regularization' END AS RegularizationStatus  
FROM #EmployeeDetails    
ORDER BY AbscondDate DESC, ConsecutiveAbsentDays DESC;  
END
END 

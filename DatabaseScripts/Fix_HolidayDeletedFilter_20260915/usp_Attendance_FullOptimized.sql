ALTER PROCEDURE dbo.usp_Attendance_FullOptimized
(
    @FromDate DATE,
    @ToDate   DATE,
    @ECode    NVARCHAR(50) = NULL
)
AS
BEGIN
SET NOCOUNT ON;
SET XACT_ABORT ON;

-------------------------------------------------------
-- PART 1 — DATE GRID
-------------------------------------------------------
DROP TABLE IF EXISTS #Dates;

;WITH n AS
(
    SELECT TOP (DATEDIFF(DAY,@FromDate,@ToDate)+1)
           ROW_NUMBER() OVER (ORDER BY (SELECT NULL))-1 n
    FROM sys.all_objects
)
SELECT DATEADD(DAY,n,@FromDate) PunchDate
INTO #Dates
FROM n;

CREATE CLUSTERED INDEX IX_Dates ON #Dates(PunchDate);

-------------------------------------------------------
-- PART 1 — EMPLOYEES
-------------------------------------------------------
DROP TABLE IF EXISTS #Emp;

SELECT
 e.EmployeeId,
 e.ECode,
 EmployeeName =
  CASE 
    WHEN e.FirstName IS NULL AND e.[FULL NAME] IS NULL THEN 'NA'
    WHEN e.FirstName IS NULL THEN ISNULL(e.[FULL NAME],'NA')
    ELSE LTRIM(ISNULL(e.FirstName,'')+' '+ISNULL(e.LastName,'')) 
  END
INTO #Emp
FROM tblEmployee e
WHERE (@ECode IS NULL OR e.ECode=@ECode);

CREATE CLUSTERED INDEX IX_Emp ON #Emp(EmployeeId);

-------------------------------------------------------
-- PART 1 — HOLIDAYS
-------------------------------------------------------
DROP TABLE IF EXISTS #Holiday;

SELECT tl.STCode, hm.HolidayDate, MIN(hm.HolidayName) HolidayName
INTO #Holiday
FROM HolidayMaster hm
JOIN LocationTypeMaster ltm ON ltm.Id=hm.LocationType
JOIN GroupMaster gm ON gm.Id=hm.LocationValue
JOIN GroupWiseStoreCodeMapping g ON g.GroupId=gm.Id
JOIN tblLocation tl ON tl.STCode=g.ST_CD
WHERE hm.HolidayDate BETWEEN @FromDate AND @ToDate
  AND ISNULL(hm.IsDeleted,0) = 0 AND ISNULL(hm.IsActive,1) = 1   -- deleted/inactive holidays must not apply
GROUP BY tl.STCode, hm.HolidayDate;

CREATE CLUSTERED INDEX IX_Holiday ON #Holiday(STCode,HolidayDate);

-------------------------------------------------------
-- PART 1 — EMPLOYEE DATE GRID + SHIFT
-------------------------------------------------------
DROP TABLE IF EXISTS #EDG;

SELECT
 e.EmployeeId,
 e.ECode,
 e.EmployeeName,

 d.PunchDate,

 ShiftID = COALESCE(hs.ShiftId,e.ShiftID),
 sm.ShiftName,
 COALESCE(CAST(sm.StartTime AS TIME),'08:00:00') ShiftStartTime,
 CAST(sm.EndTime AS TIME) ShiftEndTime,

 IsCrossMidnight = CASE 
      WHEN CAST(sm.StartTime AS TIME)>CAST(sm.EndTime AS TIME) THEN 1 ELSE 0 END,

 IsWeekend = CASE WHEN DATEPART(WEEKDAY,d.PunchDate) IN (1,7) THEN 1 ELSE 0 END,
 IsFutureDate = CASE WHEN d.PunchDate>CAST(GETDATE() AS DATE) THEN 1 ELSE 0 END,
 IsHoliday = CASE WHEN h.HolidayDate IS NULL THEN 0 ELSE 1 END
INTO #EDG
FROM #Emp e
CROSS JOIN #Dates d
OUTER APPLY
(
 SELECT TOP 1 ShiftId
 FROM EmployeeShiftHistory sh
 WHERE sh.EmployeeId=e.EmployeeId
 AND d.PunchDate BETWEEN sh.EffectiveFrom AND ISNULL(sh.EffectiveTo,'9999-12-31')
 ORDER BY sh.EffectiveFrom DESC, sh.HistoryId DESC
) hs
LEFT JOIN tblShiftMaster sm ON sm.ShiftID=COALESCE(hs.ShiftId,e.ShiftID)
LEFT JOIN #Holiday h ON h.STCode='DEFAULT' AND h.HolidayDate=d.PunchDate;

CREATE CLUSTERED INDEX IX_EDG ON #EDG(EmployeeId,PunchDate);

-------------------------------------------------------
-- PART 2 — PUNCH AGGREGATION
-------------------------------------------------------
DROP TABLE IF EXISTS #PunchAgg;

SELECT
 e.EmployeeId,
 p.PunchDate,
 PunchCount = SUM(CASE WHEN v.p IS NOT NULL AND v.p<>'00:00:00' THEN 1 ELSE 0 END),
 PunchIn = MIN(v.p),
 PunchOut = MAX(v.p)
INTO #PunchAgg
FROM tblEmployeeMultiPunches p
JOIN #Emp e ON e.ECode=p.UserID
CROSS APPLY (VALUES
(p.Punch1),(p.Punch2),(p.Punch3),(p.Punch4),(p.Punch5),(p.Punch6),
(p.Punch7),(p.Punch8),(p.Punch9),(p.Punch10),(p.Punch11),(p.Punch12)
) v(p)
WHERE p.PunchDate BETWEEN @FromDate AND @ToDate
GROUP BY e.EmployeeId,p.PunchDate;

CREATE CLUSTERED INDEX IX_PunchAgg ON #PunchAgg(EmployeeId,PunchDate);

-------------------------------------------------------
-- PART 3 — GEO DAILY
-------------------------------------------------------
DROP TABLE IF EXISTS #GeoDaily;

;WITH g AS (
 SELECT EmployeeId,
        CONVERT(date,PunchTimeUtc) PunchDate,
        PunchTimeUtc,
        ROW_NUMBER() OVER(PARTITION BY EmployeeId,CONVERT(date,PunchTimeUtc) ORDER BY PunchTimeUtc) rn
 FROM AttendanceRecord
 WHERE StatusId=1
 AND PunchTimeUtc BETWEEN @FromDate AND DATEADD(DAY,1,@ToDate)
),
pairs AS (
 SELECT a.EmployeeId,a.PunchDate,
        DATEDIFF(MINUTE,a.PunchTimeUtc,b.PunchTimeUtc) WorkedMinutes
 FROM g a JOIN g b
  ON b.EmployeeId=a.EmployeeId AND b.PunchDate=a.PunchDate AND b.rn=a.rn+1
 WHERE a.rn%2=1
),
cnt AS (
 SELECT EmployeeId,PunchDate,COUNT(*) TotalPunches
 FROM g GROUP BY EmployeeId,PunchDate
)
SELECT
 p.EmployeeId,p.PunchDate,
 GeoWorkedMinutes=SUM(p.WorkedMinutes),
 GeoTotalPunches=c.TotalPunches,
 GeoStatus = CASE WHEN c.TotalPunches%2<>0 THEN 'MP' ELSE 'GF' END
INTO #GeoDaily
FROM pairs p
JOIN cnt c ON c.EmployeeId=p.EmployeeId AND c.PunchDate=p.PunchDate
GROUP BY p.EmployeeId,p.PunchDate,c.TotalPunches;

CREATE CLUSTERED INDEX IX_GeoDaily ON #GeoDaily(EmployeeId,PunchDate);

-------------------------------------------------------
-- PART 4 — FINAL DAILY + STATUS (FIXED)
-------------------------------------------------------
DROP TABLE IF EXISTS #Daily;

SELECT
 edg.EmployeeId,
 edg.PunchDate,

 TotalMinutes =
  CASE
   WHEN gd.GeoStatus='GF' THEN gd.GeoWorkedMinutes
   WHEN pa.PunchIn IS NOT NULL AND pa.PunchOut IS NOT NULL
   THEN DATEDIFF(MINUTE,pa.PunchIn,pa.PunchOut)
   ELSE 0 END,

 PunchCount = ISNULL(pa.PunchCount,0)+ISNULL(gd.GeoTotalPunches,0),

 GeoStatus = ISNULL(gd.GeoStatus,''),

 IsHoliday = edg.IsHoliday,
 IsWeekend = edg.IsWeekend,
 IsFutureDate = edg.IsFutureDate,

 CAST('' AS NVARCHAR(50)) AS Status
INTO #Daily
FROM #EDG edg
LEFT JOIN #PunchAgg pa 
 ON pa.EmployeeId=edg.EmployeeId AND pa.PunchDate=edg.PunchDate
LEFT JOIN #GeoDaily gd 
 ON gd.EmployeeId=edg.EmployeeId AND gd.PunchDate=edg.PunchDate;

CREATE CLUSTERED INDEX IX_Daily ON #Daily(EmployeeId,PunchDate);

UPDATE #Daily
SET Status =
CASE
 WHEN IsFutureDate=1 THEN ''
 WHEN IsHoliday=1 THEN 'Holiday'
 WHEN IsWeekend=1 AND TotalMinutes=0 THEN 'Weekly Off'
 WHEN GeoStatus='MP' THEN 'MIS'
 WHEN PunchCount%2=1 AND PunchCount>0 THEN 'MIS'
 WHEN TotalMinutes>=510 THEN 'Present'
 WHEN TotalMinutes BETWEEN 240 AND 509 THEN 'Half Day Absent'
 ELSE 'Absent'
END;

-------------------------------------------------------
-- FINAL RESULT
-------------------------------------------------------
SELECT *
FROM #Daily
ORDER BY EmployeeId,PunchDate;

END

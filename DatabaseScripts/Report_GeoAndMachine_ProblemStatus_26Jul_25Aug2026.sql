/*==============================================================================
  REPORT - days where the employee punched BOTH the geofence app AND the
           biometric machine, and the resulting status is NOT a clean
           attendance ('Present', 'Manual Present', 'GF').

  Window  : 2026-07-26 .. 2026-08-25   Pan-India, no store filter
  Grain   : ONE LINE per employee per date.
            Machine punches and geofence punches are rolled into columns on
            that single line rather than one row per punch.

  Statuses shown  : Absent, Half Day Absent, Half Day Present, Quarter Day
                    Absent, MIS, Holiday, Weekly Off, POW ... anything except
                    the three clean ones.
  Statuses hidden : Present, Manual Present, GF.

  Sources (payroll's own buckets)
      MACHINE  -> tblEmployeeMultiPunches (Punch1..Punch12)
      GEOFENCE -> dbo.AttendanceRecord, StatusId = 1 (approved app punches)

  READ ONLY - selects only.
==============================================================================*/
SET NOCOUNT ON;

DECLARE @From date = '2026-07-26',
        @To   date = '2026-08-25';

/*--- 1. Candidate days: payroll saw both sources, status is not a clean one ---*/
IF OBJECT_ID('tempdb..#Day') IS NOT NULL DROP TABLE #Day;
SELECT v.ECode, v.AttendanceDate, v.EmployeeName, v.STCode, v.LocationName,
       v.DepartmentName, v.DesignationName, v.ShiftName,
       v.PunchSource, v.Status, v.ValidPunchCount,
       v.PunchIn, v.PunchOut, v.TotalWorkingMinutes,
       v.IsRegularize, v.IsHoliday, v.IsOnLeave
INTO #Day
FROM tbl_fn_GetMonthlyPunchesRange_productionnewnick_test v
WHERE v.AttendanceDate BETWEEN @From AND @To
  AND v.PunchSource = 'BOTH'                       -- geofence AND machine
  AND v.Status NOT IN ('Present', 'Manual Present', 'GF');

CREATE CLUSTERED INDEX IX_Day ON #Day(ECode, AttendanceDate);

/*--- 2. Machine punches for those days ---*/
IF OBJECT_ID('tempdb..#Mach') IS NOT NULL DROP TABLE #Mach;
SELECT d.ECode, d.AttendanceDate,
       MachinePunchCount = COUNT(*),
       MachineFirst      = MIN(p.T),
       MachineLast       = MAX(p.T),
       MachinePunches    = STRING_AGG(CONVERT(varchar(8), p.T, 108), ' | ')
                             WITHIN GROUP (ORDER BY p.T),
       MachineTotalHours = MAX(m.TotalHours)
INTO #Mach
FROM #Day d
JOIN tblEmployeeMultiPunches m
  ON m.UserID = d.ECode AND m.PunchDate = d.AttendanceDate
CROSS APPLY (VALUES (m.Punch1),(m.Punch2),(m.Punch3),(m.Punch4),(m.Punch5),(m.Punch6),
                    (m.Punch7),(m.Punch8),(m.Punch9),(m.Punch10),(m.Punch11),(m.Punch12)) p(T)
WHERE p.T <> '00:00:00'
GROUP BY d.ECode, d.AttendanceDate;

CREATE CLUSTERED INDEX IX_Mach ON #Mach(ECode, AttendanceDate);

/*--- 3. Geofence punches for those days ---*/
IF OBJECT_ID('tempdb..#Geo') IS NOT NULL DROP TABLE #Geo;
SELECT d.ECode, d.AttendanceDate,
       GeoPunchCount = COUNT(*),
       GeoFirst      = MIN(CAST(ar.PunchTimeUtc AS time)),
       GeoLast       = MAX(CAST(ar.PunchTimeUtc AS time)),
       GeoPunches    = STRING_AGG(
                          CONVERT(varchar(8), ar.PunchTimeUtc, 108)
                        + CASE ar.PunchType WHEN 1 THEN ' IN' WHEN 2 THEN ' OUT' ELSE '' END,
                        ' | ') WITHIN GROUP (ORDER BY ar.PunchTimeUtc),
       GeoOutsideFence = SUM(CASE WHEN ar.WithinGeofence = 0 THEN 1 ELSE 0 END),
       GeoAddress      = MIN(ar.Address)
INTO #Geo
FROM #Day d
JOIN tblEmployee e  ON e.Ecode = d.ECode
JOIN dbo.AttendanceRecord ar
  ON ar.EmployeeId = e.EmployeeId
 AND ar.StatusId   = 1
 AND CONVERT(date, ar.PunchTimeUtc) = d.AttendanceDate
GROUP BY d.ECode, d.AttendanceDate;

CREATE CLUSTERED INDEX IX_Geo ON #Geo(ECode, AttendanceDate);

/*--- 4. One line per employee per date ---*/
SELECT
    Ecode              = d.ECode,
    EmpName            = d.EmployeeName,
    STCode             = d.STCode,
    LocationName       = d.LocationName,
    DepartmentName     = d.DepartmentName,
    DesignationName    = d.DesignationName,
    AttendanceDate     = CONVERT(varchar(10), d.AttendanceDate, 105),   -- dd-mm-yyyy
    DayName            = DATENAME(WEEKDAY, d.AttendanceDate),
    [Status]           = d.Status,
    PunchSource        = d.PunchSource,

    /* what the machine recorded */
    MachinePunchCount  = ISNULL(m.MachinePunchCount, 0),
    MachinePunches     = ISNULL(m.MachinePunches, ''),
    MachineTotalHours  = ISNULL(m.MachineTotalHours, 0),

    /* what the geofence app recorded */
    GeoPunchCount      = ISNULL(g.GeoPunchCount, 0),
    GeoPunches         = ISNULL(g.GeoPunches, ''),
    GeoOutsideFence    = ISNULL(g.GeoOutsideFence, 0),

    /* what payroll concluded */
    TotalPunchesUsed   = d.ValidPunchCount,
    OddPunchCount      = CASE WHEN d.ValidPunchCount % 2 = 1 THEN 'YES' ELSE 'NO' END,
    DayPunchIn         = CONVERT(varchar(8), d.PunchIn,  108),
    DayPunchOut        = CONVERT(varchar(8), d.PunchOut, 108),
    CreditedHHMM       = d.TotalWorkingMinutes,
    CreditedMinutes    = DATEDIFF(MINUTE, 0, CAST(d.TotalWorkingMinutes AS time)),

    /* the gap that matters: real time on site vs what payroll credited */
    ActualSpanMinutes  = DATEDIFF(MINUTE, CAST(d.PunchIn AS time), CAST(d.PunchOut AS time)),
    LostMinutes        = DATEDIFF(MINUTE, CAST(d.PunchIn AS time), CAST(d.PunchOut AS time))
                       - DATEDIFF(MINUTE, 0, CAST(d.TotalWorkingMinutes AS time)),

    IsRegularize       = d.IsRegularize,
    IsHoliday          = d.IsHoliday,
    IsOnLeave          = d.IsOnLeave,
    ShiftName          = ISNULL(d.ShiftName, ''),
    GeoAddress         = ISNULL(g.GeoAddress, '')
FROM #Day d
LEFT JOIN #Mach m ON m.ECode = d.ECode AND m.AttendanceDate = d.AttendanceDate
LEFT JOIN #Geo  g ON g.ECode = d.ECode AND g.AttendanceDate = d.AttendanceDate
ORDER BY d.STCode, d.ECode, d.AttendanceDate;

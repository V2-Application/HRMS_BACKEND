/*==============================================================================
  REPORT - employees who used ALL THREE punch sources in the Aug-26 cycle
           (geofence app + biometric machine + manual/regularised)
  Window : 2026-07-26 .. 2026-08-25   Pan-India, no store filter
  Output : one row per PUNCH EVENT, labelled with where it came from.

  Source definitions match payroll's own buckets
  (usp_GetMonthlyAttendanceSummary_WithStoreRules_Single_Dev):
      MACHINE  -> tblEmployeeMultiPunches (Punch1..Punch12), fed by the
                  biometric machines via prc_Daily_Attendance
      GEOFENCE -> dbo.AttendanceRecord, StatusId = 1 (approved app punches)
      MANUAL   -> IsRegularize = 1, i.e. a regularisation request supplied the
                  in/out times instead of a device (payroll's MANUAL bucket)
  READ ONLY - selects only.
==============================================================================*/
SET NOCOUNT ON;

DECLARE @From date = '2026-07-26',
        @To   date = '2026-08-25';

/*--- 1. Employees having at least one punch from EACH of the three sources ---*/
IF OBJECT_ID('tempdb..#mach') IS NOT NULL DROP TABLE #mach;
SELECT DISTINCT m.UserID AS Ecode
INTO #mach
FROM tblEmployeeMultiPunches m
CROSS APPLY (VALUES (m.Punch1),(m.Punch2),(m.Punch3),(m.Punch4),(m.Punch5),(m.Punch6),
                    (m.Punch7),(m.Punch8),(m.Punch9),(m.Punch10),(m.Punch11),(m.Punch12)) p(T)
WHERE m.PunchDate BETWEEN @From AND @To AND p.T <> '00:00:00';

IF OBJECT_ID('tempdb..#geo') IS NOT NULL DROP TABLE #geo;
SELECT DISTINCT e.Ecode
INTO #geo
FROM dbo.AttendanceRecord ar
JOIN tblEmployee e ON e.EmployeeId = ar.EmployeeId
WHERE ar.StatusId = 1 AND CONVERT(date, ar.PunchTimeUtc) BETWEEN @From AND @To;

IF OBJECT_ID('tempdb..#man') IS NOT NULL DROP TABLE #man;
SELECT DISTINCT ECode AS Ecode
INTO #man
FROM tbl_fn_GetMonthlyPunchesRange_productionnewnick_test
WHERE AttendanceDate BETWEEN @From AND @To AND IsRegularize = 1;

/* Population = everyone who used BOTH the machine AND the geofence app.
   SourceMix then says whether they ALSO have manual/regularised days, so the
   report answers both questions at once:
       MACHINE+GEO+MANUAL  -> all three sources
       MACHINE+GEO         -> the two device sources only                     */
IF OBJECT_ID('tempdb..#Tri') IS NOT NULL DROP TABLE #Tri;
SELECT a.Ecode,
       SourceMix = CASE WHEN c.Ecode IS NOT NULL
                        THEN 'MACHINE+GEO+MANUAL' ELSE 'MACHINE+GEO' END
INTO #Tri
FROM #mach a
JOIN #geo  b ON b.Ecode = a.Ecode
LEFT JOIN #man c ON c.Ecode = a.Ecode;

CREATE CLUSTERED INDEX IX_Tri ON #Tri(Ecode);

/*--- 2. Day-level context straight from what payroll consumed ---*/
IF OBJECT_ID('tempdb..#Day') IS NOT NULL DROP TABLE #Day;
SELECT v.ECode, v.AttendanceDate, v.EmployeeName, v.STCode, v.LocationName,
       v.DepartmentName, v.DesignationName, v.ShiftName,
       v.PunchSource, v.Status, v.ValidPunchCount,
       v.PunchIn, v.PunchOut, v.TotalWorkingMinutes,
       v.IsRegularize, v.IsHoliday, v.IsOnLeave,
       v.RegularizePunchIn, v.RegularizePuncOut
INTO #Day
FROM tbl_fn_GetMonthlyPunchesRange_productionnewnick_test v
JOIN #Tri t ON t.Ecode = v.ECode
WHERE v.AttendanceDate BETWEEN @From AND @To;

CREATE CLUSTERED INDEX IX_Day ON #Day(ECode, AttendanceDate);

/*--- 3. Every individual punch event, labelled by origin ---*/
;WITH Punches AS
(
    /* MACHINE - biometric */
    SELECT ECode        = m.UserID,
           PunchDate    = m.PunchDate,
           PunchOrigin  = 'MACHINE',
           PunchType    = CAST(NULL AS varchar(10)),
           PunchTime    = CONVERT(varchar(8), p.T, 108),
           WithinGeofence = CAST(NULL AS bit),
           Address      = CAST(NULL AS nvarchar(200))
    FROM tblEmployeeMultiPunches m
    JOIN #Tri t ON t.Ecode = m.UserID
    CROSS APPLY (VALUES (m.Punch1),(m.Punch2),(m.Punch3),(m.Punch4),(m.Punch5),(m.Punch6),
                        (m.Punch7),(m.Punch8),(m.Punch9),(m.Punch10),(m.Punch11),(m.Punch12)) p(T)
    WHERE m.PunchDate BETWEEN @From AND @To AND p.T <> '00:00:00'

    UNION ALL

    /* GEOFENCE - approved app punches */
    SELECT e.Ecode,
           CONVERT(date, ar.PunchTimeUtc),
           'GEOFENCE',
           CASE ar.PunchType WHEN 1 THEN 'IN' WHEN 2 THEN 'OUT'
                             ELSE CAST(ar.PunchType AS varchar(10)) END,
           CONVERT(varchar(8), ar.PunchTimeUtc, 108),
           ar.WithinGeofence,
           ar.Address
    FROM dbo.AttendanceRecord ar
    JOIN tblEmployee e ON e.EmployeeId = ar.EmployeeId
    JOIN #Tri t ON t.Ecode = e.Ecode
    WHERE ar.StatusId = 1 AND CONVERT(date, ar.PunchTimeUtc) BETWEEN @From AND @To

    UNION ALL

    /* MANUAL - regularised in/out (no device involved) */
    SELECT d.ECode, d.AttendanceDate, 'MANUAL', 'IN',
           CONVERT(varchar(8), d.RegularizePunchIn, 108), NULL, NULL
    FROM #Day d
    WHERE d.IsRegularize = 1
      AND d.RegularizePunchIn IS NOT NULL AND d.RegularizePunchIn <> '00:00:00'

    UNION ALL

    SELECT d.ECode, d.AttendanceDate, 'MANUAL', 'OUT',
           CONVERT(varchar(8), d.RegularizePuncOut, 108), NULL, NULL
    FROM #Day d
    WHERE d.IsRegularize = 1
      AND d.RegularizePuncOut IS NOT NULL AND d.RegularizePuncOut <> '00:00:00'
)
SELECT
    p.ECode,
    SourceMix          = t.SourceMix,      -- MACHINE+GEO+MANUAL | MACHINE+GEO
    EmpName            = d.EmployeeName,
    STCode             = d.STCode,
    LocationName       = d.LocationName,
    DepartmentName     = d.DepartmentName,
    DesignationName    = d.DesignationName,
    AttendanceDate     = CONVERT(varchar(10), p.PunchDate, 105),   -- dd-mm-yyyy
    PunchTime          = p.PunchTime,
    PunchOrigin        = p.PunchOrigin,                            -- MACHINE | GEOFENCE | MANUAL
    PunchType          = ISNULL(p.PunchType, ''),
    WithinGeofence     = CASE WHEN p.PunchOrigin <> 'GEOFENCE' THEN ''
                              WHEN p.WithinGeofence = 1 THEN 'YES' ELSE 'NO' END,
    PunchAddress       = ISNULL(p.Address, ''),
    /* day-level context as payroll saw it */
    DaySource          = ISNULL(d.PunchSource, ''),
    DayStatus          = ISNULL(d.Status, ''),
    DayPunchCount      = d.ValidPunchCount,
    DayPunchIn         = CONVERT(varchar(8), d.PunchIn, 108),
    DayPunchOut        = CONVERT(varchar(8), d.PunchOut, 108),
    DayWorkedHHMM      = d.TotalWorkingMinutes,
    DayIsRegularize    = d.IsRegularize,
    DayIsHoliday       = d.IsHoliday,
    DayIsOnLeave       = d.IsOnLeave,
    ShiftName          = ISNULL(d.ShiftName, '')
FROM Punches p
JOIN #Tri t ON t.Ecode = p.ECode
LEFT JOIN #Day d
       ON d.ECode = p.ECode AND d.AttendanceDate = p.PunchDate
ORDER BY t.SourceMix, p.ECode, p.PunchDate, p.PunchTime, p.PunchOrigin;

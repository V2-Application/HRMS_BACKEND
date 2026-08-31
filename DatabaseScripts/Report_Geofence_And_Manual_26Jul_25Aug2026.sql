/*==============================================================================
  REPORT - employees who used BOTH the geofence app AND manual (regularised)
           attendance in the Aug-26 cycle.
  Window : 2026-07-26 .. 2026-08-25   Pan-India, no store filter
  Grain  : ONE LINE per employee per date, for every date in the window that
           carried a geofence punch or a manual/regularised entry.

  Source definitions match payroll's own buckets
  (usp_GetMonthlyAttendanceSummary_WithStoreRules_Single_Dev):
      GEOFENCE -> dbo.AttendanceRecord   (the mobile app)
      MANUAL   -> IsRegularize = 1       (a regularisation supplied the in/out
                                          times; no device involved)

  NOTE ON GEOFENCE STATUS
    Population is built on geofence punches of ANY status, so days rejected in
    the 2026-08-29 bulk rejection still appear. Each line reports the
    Approved / Rejected / Pending split, and payroll only ever counts the
    Approved ones (StatusId = 1). Filter GeoApproved > 0 for the payroll view.

  READ ONLY - selects only.
==============================================================================*/
SET NOCOUNT ON;

DECLARE @From date = '2026-07-26',
        @To   date = '2026-08-25';

/*--- 1. Population: at least one geofence punch AND one manual day ---*/
IF OBJECT_ID('tempdb..#geo') IS NOT NULL DROP TABLE #geo;
SELECT DISTINCT e.Ecode, e.EmployeeId
INTO #geo
FROM dbo.AttendanceRecord ar
JOIN tblEmployee e ON e.EmployeeId = ar.EmployeeId
WHERE CONVERT(date, ar.PunchTimeUtc) BETWEEN @From AND @To;

IF OBJECT_ID('tempdb..#man') IS NOT NULL DROP TABLE #man;
SELECT DISTINCT ECode AS Ecode
INTO #man
FROM tbl_fn_GetMonthlyPunchesRange_productionnewnick_test
WHERE AttendanceDate BETWEEN @From AND @To AND IsRegularize = 1;

IF OBJECT_ID('tempdb..#Pop') IS NOT NULL DROP TABLE #Pop;
SELECT g.Ecode, g.EmployeeId
INTO #Pop
FROM #geo g JOIN #man m ON m.Ecode = g.Ecode;

CREATE CLUSTERED INDEX IX_Pop ON #Pop(Ecode);

/*--- 2. Day-level context as payroll consumed it ---*/
IF OBJECT_ID('tempdb..#Day') IS NOT NULL DROP TABLE #Day;
SELECT v.ECode, v.AttendanceDate, v.EmployeeName, v.STCode, v.LocationName,
       v.DepartmentName, v.DesignationName, v.ShiftName,
       v.PunchSource, v.Status, v.ValidPunchCount,
       v.PunchIn, v.PunchOut, v.TotalWorkingMinutes,
       v.IsRegularize, v.IsHoliday, v.IsOnLeave,
       v.RegularizePunchIn, v.RegularizePuncOut
INTO #Day
FROM tbl_fn_GetMonthlyPunchesRange_productionnewnick_test v
JOIN #Pop p ON p.Ecode = v.ECode
WHERE v.AttendanceDate BETWEEN @From AND @To;

CREATE CLUSTERED INDEX IX_Day ON #Day(ECode, AttendanceDate);

/*--- 3. Geofence punches per employee-date, with status split ---*/
IF OBJECT_ID('tempdb..#G') IS NOT NULL DROP TABLE #G;
SELECT p.Ecode, PunchDate = CONVERT(date, ar.PunchTimeUtc),
       GeoPunchCount   = COUNT(*),
       GeoApproved     = SUM(CASE WHEN ar.StatusId = 1 THEN 1 ELSE 0 END),
       GeoRejected     = SUM(CASE WHEN ar.StatusId = 2 THEN 1 ELSE 0 END),
       GeoPending      = SUM(CASE WHEN ar.StatusId = 4 THEN 1 ELSE 0 END),
       GeoOutsideFence = SUM(CASE WHEN ar.WithinGeofence = 0 THEN 1 ELSE 0 END),
       GeoPunches      = STRING_AGG(
                            CONVERT(varchar(8), ar.PunchTimeUtc, 108)
                          + CASE ar.PunchType WHEN 1 THEN ' IN' WHEN 2 THEN ' OUT' ELSE '' END
                          + CASE ar.StatusId  WHEN 1 THEN ' (Appr)' WHEN 2 THEN ' (Rej)'
                                              WHEN 4 THEN ' (Pend)' ELSE '' END,
                            ' | ') WITHIN GROUP (ORDER BY ar.PunchTimeUtc),
       GeoAddress      = MIN(ar.Address)
INTO #G
FROM #Pop p
JOIN dbo.AttendanceRecord ar ON ar.EmployeeId = p.EmployeeId
WHERE CONVERT(date, ar.PunchTimeUtc) BETWEEN @From AND @To
GROUP BY p.Ecode, CONVERT(date, ar.PunchTimeUtc);

CREATE CLUSTERED INDEX IX_G ON #G(Ecode, PunchDate);

/*--- 4. Regularisation request detail per employee-date ---*/
IF OBJECT_ID('tempdb..#R') IS NOT NULL DROP TABLE #R;
SELECT p.Ecode, RequestDate = CONVERT(date, r.RequestDate),
       ReqCount    = COUNT(*),
       ReqReasons  = STRING_AGG(CAST(ISNULL(r.Reason, '') AS nvarchar(max)), ' | '),
       ReqStatuses = STRING_AGG(CAST(CASE r.StatusId WHEN 1 THEN 'Approved' WHEN 2 THEN 'Rejected'
                                                     WHEN 4 THEN 'Pending'
                                     ELSE CAST(ISNULL(r.StatusId,0) AS varchar(10)) END AS nvarchar(max)), ' | '),
       HasProof    = MAX(CASE WHEN r.FileUrl IS NOT NULL AND LTRIM(RTRIM(r.FileUrl)) <> '' THEN 1 ELSE 0 END)
INTO #R
FROM #Pop p
JOIN dbo.tblAttendanceRegularizationRequest r ON r.EmployeeId = p.EmployeeId
WHERE CONVERT(date, r.RequestDate) BETWEEN @From AND @To
GROUP BY p.Ecode, CONVERT(date, r.RequestDate);

CREATE CLUSTERED INDEX IX_R ON #R(Ecode, RequestDate);

/*--- 5. Employee-level totals, so each line carries the month's shape ---*/
IF OBJECT_ID('tempdb..#Tot') IS NOT NULL DROP TABLE #Tot;
SELECT d.ECode,
       TotalGeoDays    = COUNT(DISTINCT CASE WHEN g.Ecode IS NOT NULL THEN d.AttendanceDate END),
       TotalManualDays = COUNT(DISTINCT CASE WHEN d.IsRegularize = 1 THEN d.AttendanceDate END),
       -- how many dates in the window carried BOTH on the same day
       TotalSameDayBoth = COUNT(DISTINCT CASE WHEN g.Ecode IS NOT NULL AND d.IsRegularize = 1
                                              THEN d.AttendanceDate END)
INTO #Tot
FROM #Day d
LEFT JOIN #G g ON g.Ecode = d.ECode AND g.PunchDate = d.AttendanceDate
GROUP BY d.ECode;

/*--- 6. One line per employee per date ---*/
SELECT
    Ecode              = d.ECode,
    EmpName            = d.EmployeeName,
    STCode             = d.STCode,
    LocationName       = d.LocationName,
    DepartmentName     = d.DepartmentName,
    DesignationName    = d.DesignationName,
    AttendanceDate     = CONVERT(varchar(10), d.AttendanceDate, 105),   -- dd-mm-yyyy
    DayName            = DATENAME(WEEKDAY, d.AttendanceDate),

    /* which mechanism was used on this date */
    PunchMode          = CASE WHEN g.Ecode IS NOT NULL AND d.IsRegularize = 1 THEN 'GEOFENCE + MANUAL'
                              WHEN g.Ecode IS NOT NULL                        THEN 'GEOFENCE'
                              ELSE 'MANUAL' END,

    /* geofence side */
    GeoPunchCount      = ISNULL(g.GeoPunchCount, 0),
    GeoPunches         = ISNULL(g.GeoPunches, ''),
    GeoApproved        = ISNULL(g.GeoApproved, 0),
    GeoRejected        = ISNULL(g.GeoRejected, 0),
    GeoPending         = ISNULL(g.GeoPending, 0),
    GeoOutsideFence    = ISNULL(g.GeoOutsideFence, 0),

    /* manual side */
    IsManual           = d.IsRegularize,
    ManualPunchIn      = CONVERT(varchar(8), d.RegularizePunchIn,  108),
    ManualPunchOut     = CONVERT(varchar(8), d.RegularizePuncOut, 108),
    ManualReqCount     = ISNULL(r.ReqCount, 0),
    ManualReasons      = ISNULL(r.ReqReasons, ''),
    ManualReqStatuses  = ISNULL(r.ReqStatuses, ''),
    ManualHasProof     = CASE WHEN r.HasProof = 1 THEN 'YES'
                              WHEN r.Ecode IS NULL THEN '' ELSE 'NO PROOF' END,

    /* what payroll concluded for the day */
    DaySource          = ISNULL(d.PunchSource, ''),
    DayStatus          = ISNULL(d.Status, ''),
    DayPunchCount      = d.ValidPunchCount,
    DayPunchIn         = CONVERT(varchar(8), d.PunchIn,  108),
    DayPunchOut        = CONVERT(varchar(8), d.PunchOut, 108),
    DayWorkedHHMM      = d.TotalWorkingMinutes,
    IsHoliday          = d.IsHoliday,
    IsOnLeave          = d.IsOnLeave,
    ShiftName          = ISNULL(d.ShiftName, ''),

    /* month shape for this employee */
    EmpTotalGeoDays     = t.TotalGeoDays,
    EmpTotalManualDays  = t.TotalManualDays,
    EmpTotalSameDayBoth = t.TotalSameDayBoth,
    GeoAddress         = ISNULL(g.GeoAddress, '')
FROM #Day d
LEFT JOIN #G   g ON g.Ecode = d.ECode AND g.PunchDate   = d.AttendanceDate
LEFT JOIN #R   r ON r.Ecode = d.ECode AND r.RequestDate = d.AttendanceDate
LEFT JOIN #Tot t ON t.ECode = d.ECode
-- SAME DAY only: the date must carry a geofence punch AND a manual/regularised
-- entry. Days that used just one of the two are excluded.
WHERE g.Ecode IS NOT NULL AND d.IsRegularize = 1
ORDER BY d.AttendanceDate, d.STCode, d.ECode;       -- date wise
